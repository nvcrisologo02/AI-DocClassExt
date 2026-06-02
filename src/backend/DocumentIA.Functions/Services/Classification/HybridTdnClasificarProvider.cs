using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services.Classification
{
    /// <summary>
    /// Proveedor de clasificación jerárquica TDN usando estrategia de fallback:
    /// 1. Reglas heurísticas (RuleBasedTdnClassifier)
    /// 2. Document Intelligence (DI) con umbral configurable
    /// 3. Foundry Azure OpenAI LLM (rescate con timeout y retry)
    /// </summary>
    public class HybridTdnClasificarProvider : IClasificarDataProvider
    {
        private readonly ILogger<HybridTdnClasificarProvider> _logger;
        private readonly IClasificarDataProvider _diProvider;
        private readonly ILayoutMarkdownProvider _layoutMarkdownProvider;
        private readonly DocumentWindowExtractor _windowExtractor;
        private readonly RuleBasedTdnClassifier _ruleClassifier;
        private readonly FoundryTdnRescueClassifier _rescueClassifier;
        private readonly HybridTdnOptions _options;
        private readonly TelemetryClient _telemetryClient;

        public HybridTdnClasificarProvider(
            ILogger<HybridTdnClasificarProvider> logger,
            IClasificarDataProvider diProvider,
            ILayoutMarkdownProvider layoutMarkdownProvider,
            DocumentWindowExtractor windowExtractor,
            RuleBasedTdnClassifier ruleClassifier,
            FoundryTdnRescueClassifier rescueClassifier,
            IOptions<HybridTdnOptions> options,
            TelemetryClient telemetryClient)
        {
            _logger = logger;
            _diProvider = diProvider;
            _layoutMarkdownProvider = layoutMarkdownProvider;
            _windowExtractor = windowExtractor;
            _ruleClassifier = ruleClassifier;
            _rescueClassifier = rescueClassifier;
            _options = options.Value ?? new HybridTdnOptions();
            _telemetryClient = telemetryClient;
        }

        /// <summary>
        /// Clasifica el documento usando la cadena de fallback:
        /// Reglas → DI (si confianza >= umbral) → Rescate LLM.
        /// </summary>
        public async Task<ResultadoClasificacion> ClasificarAsync(ClasificacionInput input, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var result = new ResultadoClasificacion
            {
                ProveedorClasif = "HybridTDN"
            };

            try
            {
                _logger.LogInformation("Iniciando clasificación HybridTDN para {Documento}", 
                    input.Entrada.Documento.Name);

                _logger.LogInformation(
                    "HybridTDN contexto: expectedType={ExpectedType}, ruleThreshold={RuleThreshold}, diThreshold={DiThreshold}, pagesToInspect={PagesToInspect}, maxWindowChars={MaxWindowChars}",
                    input.Entrada.Instrucciones.ExpectedType,
                    _options.RuleConfidenceThreshold,
                    _options.DiConfidenceThreshold,
                    _options.PagesToInspect,
                    _options.MaxCharactersPerWindow);

                await EnsureMarkdownContextAsync(input, cancellationToken);

                // Paso 1: Extraer ventana de contexto
                var window = _windowExtractor.ExtractWindow(
                    input,
                    _options.MaxCharactersPerWindow,
                    _options.PagesToInspect);

                // Paso 2: Clasificar por reglas (siempre ejecutar para logging/audit)
                var ruleResult = _ruleClassifier.Classify(window);
                _logger.LogInformation(
                    "Clasificación por reglas: tipologia={Tipologia}, confianza={Confianza}",
                    ruleResult.TipologiaDetectada, ruleResult.Confianza);
                _logger.LogInformation(
                    "Detalle reglas: metodo={Metodo}, razon={Razon}, trigger={Trigger}, routeToReview={RouteToReview}, margin={Margin}, señales={Signals}",
                    ruleResult.ClasificacionMetodo,
                    ruleResult.Razon,
                    ruleResult.TriggerActivado,
                    ruleResult.RouteToReview,
                    ruleResult.Margin,
                    ruleResult.SeñalesEncontradas is { Count: > 0 }
                        ? string.Join(", ", ruleResult.SeñalesEncontradas.Select(kv => $"{kv.Key}:{kv.Value}").Take(20))
                        : "none");

                // Si reglas dan alta confianza, retornar con eso
                if (ruleResult.Confianza >= _options.RuleConfidenceThreshold)
                {
                    result.TipologiaDetectada = ruleResult.TipologiaDetectada;
                    result.ConfianzaDI = ruleResult.Confianza;
                    result.ContentExtraido = window.ExtractedText;
                    result.Confianza = ruleResult.Confianza;
                    result.Clasificador = "RuleBasedTDN";
                    result.DetalleProveedores.Add(new() { Proveedor = "Reglas", Tipologia = ruleResult.TipologiaDetectada, Confianza = ruleResult.Confianza });

                    _logger.LogInformation(
                        "Clasificación aceptada por reglas (confianza={Confianza})",
                        ruleResult.Confianza);

                    return result;
                }

                result.DetalleProveedores.Add(new() { Proveedor = "Reglas", Tipologia = ruleResult.TipologiaDetectada, Confianza = ruleResult.Confianza, MotivoDescarte = $"confianza_baja:{ruleResult.Confianza:F3}<{_options.RuleConfidenceThreshold}" });

                _logger.LogInformation(
                    "Confianza de reglas ({Confianza}) menor a umbral ({Umbral}), escalando a DI",
                    ruleResult.Confianza, _options.RuleConfidenceThreshold);

                // Paso 3: DI con umbral de confianza
                var diResult = await _diProvider.ClasificarAsync(input, cancellationToken);
                _logger.LogInformation(
                    "Resultado DI: tipologia={Tipologia}, confianza={Confianza}",
                    diResult.TipologiaDetectada, diResult.Confianza);

                var isDiResto = string.Equals(
                    diResult.TipologiaDetectada?.Trim(),
                    "resto",
                    StringComparison.OrdinalIgnoreCase);

                // Revisar si DI superó umbral y no es RESTO
                if (diResult.Confianza >= _options.DiConfidenceThreshold && 
                    !isDiResto)
                {
                    result.TipologiaDetectada = diResult.TipologiaDetectada;
                    result.ConfianzaDI = diResult.Confianza;
                    result.ContentExtraido = diResult.ContentExtraido;
                    result.Confianza = diResult.Confianza;
                    result.Clasificador = "DocumentIntelligence";
                    result.DetalleProveedores.Add(new() { Proveedor = "DI", Tipologia = diResult.TipologiaDetectada, Confianza = diResult.Confianza });

                    _logger.LogInformation(
                        "Clasificación aceptada por DI (confianza={Confianza})",
                        diResult.Confianza);

                    return result;
                }

                string diDescarte;
                if (isDiResto)
                {
                    diDescarte = "clasificacion_resto";
                    _logger.LogWarning("DI retornó clasificación RESTO, escalando a rescate LLM");
                }
                else
                {
                    diDescarte = $"confianza_baja:{diResult.Confianza:F3}<{_options.DiConfidenceThreshold}";
                    _logger.LogInformation(
                        "DI confianza ({Confianza}) menor a umbral ({Umbral}), escalando a rescate LLM",
                        diResult.Confianza, _options.DiConfidenceThreshold);
                }
                result.DetalleProveedores.Add(new() { Proveedor = "DI", Tipologia = diResult.TipologiaDetectada, Confianza = diResult.Confianza, MotivoDescarte = diDescarte });

                // Paso 4: Rescate con Foundry LLM
                var rescueResult = await _rescueClassifier.ClassifyAsync(
                    window,
                    _options.RescueTimeoutMs,
                    _options.MaxRetries);

                result.TipologiaDetectada = rescueResult.TipologiaDetectada;
                result.Confianza = rescueResult.Confianza;
                result.ContentExtraido = window.ExtractedText;
                result.Clasificador = "FoundryRescue";
                result.FallbackLLM = true;
                result.FallbackRazon = rescueResult.Razon;
                result.ConfianzaDI = diResult.Confianza;

                if (rescueResult.Razon == "timeout_exceeded")
                {
                    _logger.LogWarning("Rescate LLM por timeout, tipologia por defecto");
                    result.TipologiaDetectada = "Desconocido";
                    result.Confianza = 0.0;
                }
                result.DetalleProveedores.Add(new() { Proveedor = "FoundryRescue", Tipologia = result.TipologiaDetectada, Confianza = result.Confianza, MotivoDescarte = rescueResult.Razon == "timeout_exceeded" ? "timeout" : null });

                _logger.LogInformation(
                    "Clasificación finalizada por rescate LLM: tipologia={Tipologia}, confianza={Confianza}",
                    result.TipologiaDetectada, result.Confianza);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en clasificación HybridTDN: {Error}", ex.Message);
                result.TipologiaDetectada = "Error";
                result.Confianza = 0.0;
                result.Clasificador = "Error";
                return result;
            }
            finally
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Clasificación HybridTDN completada en {DurationMs}ms",
                    duration.TotalMilliseconds);

                // === Telemetría ===
                try
                {
                    var properties = new Dictionary<string, string>
                    {
                        ["Clasificador"] = result.Clasificador ?? "Unknown",
                        ["Tipologia"] = result.TipologiaDetectada ?? "Unknown",
                        ["Provider"] = result.ProveedorClasif ?? "Unknown"
                    };

                    var metrics = new Dictionary<string, double>
                    {
                        ["Confianza"] = result.Confianza,
                        ["DuracionMs"] = duration.TotalMilliseconds
                    };

                    _telemetryClient.TrackEvent("Classification.HybridTDN", properties, metrics);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al emitir telemetría de HybridTDN");
                }
            }
        }

        private async Task EnsureMarkdownContextAsync(ClasificacionInput input, CancellationToken cancellationToken)
        {
            if (HasUsefulTextContext(input.DatosNormalizados))
            {
                return;
            }

            var documentoBase64 = !string.IsNullOrWhiteSpace(input.DocumentoBase64Override)
                ? input.DocumentoBase64Override
                : input.Entrada.Documento.Content.Base64;

            if (string.IsNullOrWhiteSpace(documentoBase64))
            {
                _logger.LogWarning(
                    "HybridTDN sin contexto textual y sin documento base64 disponible para extraer markdown previo en {Documento}",
                    input.Entrada.Documento.Name);
                return;
            }

            _logger.LogInformation(
                "HybridTDN sin contexto textual útil. Extrayendo markdown DI Layout previo para {Documento}",
                input.Entrada.Documento.Name);

            try
            {
                var markdownResult = await _layoutMarkdownProvider.ExtraerMarkdownAsync(
                    new ExtraerMarkdownLayoutInput
                    {
                        Tipologia = input.Entrada.Instrucciones.ExpectedType ?? string.Empty,
                        DocumentoBase64 = documentoBase64,
                        NombreDocumento = input.Entrada.Documento.Name
                    },
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(markdownResult.Markdown))
                {
                    input.DatosNormalizados["Markdown"] = markdownResult.Markdown;
                    _logger.LogInformation(
                        "Markdown DI Layout inyectado para HybridTDN ({Length} chars) en {Documento}",
                        markdownResult.Markdown.Length,
                        input.Entrada.Documento.Name);
                }
                else
                {
                    _logger.LogWarning(
                        "DI Layout no devolvió markdown útil para HybridTDN en {Documento}",
                        input.Entrada.Documento.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo extraer markdown DI Layout previo para HybridTDN en {Documento}. Se continúa con el contexto disponible.",
                    input.Entrada.Documento.Name);
            }
        }

        private static bool HasUsefulTextContext(IDictionary<string, object> datosNormalizados)
        {
            if (datosNormalizados is null || datosNormalizados.Count == 0)
            {
                return false;
            }

            var keys = new[] { "Markdown", "markdown", "Texto", "texto", "ContentText", "contentText" };

            foreach (var key in keys)
            {
                if (!datosNormalizados.TryGetValue(key, out var raw) || raw is null)
                {
                    continue;
                }

                if (raw is string text && !string.IsNullOrWhiteSpace(text))
                {
                    return true;
                }

                if (raw is JsonElement json && json.ValueKind == JsonValueKind.String)
                {
                    var value = json.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Opciones de configuración para HybridTdnClasificarProvider.
    /// </summary>
    public class HybridTdnOptions
    {
        public double RuleConfidenceThreshold { get; set; } = 0.75;
        public double DiConfidenceThreshold { get; set; } = 0.75;
        public int MaxCharactersPerWindow { get; set; } = 32000;
        public int PagesToInspect { get; set; } = 3;
        public int RescueTimeoutMs { get; set; } = 8000;
        public int MaxRetries { get; set; } = 1;
        public int RulesCacheSeconds { get; set; } = 300;
    }
}
