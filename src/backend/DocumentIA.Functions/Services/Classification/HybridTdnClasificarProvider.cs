using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
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
        private readonly DocumentWindowExtractor _windowExtractor;
        private readonly RuleBasedTdnClassifier _ruleClassifier;
        private readonly FoundryTdnRescueClassifier _rescueClassifier;
        private readonly HybridTdnOptions _options;
        private readonly TelemetryClient _telemetryClient;

        public HybridTdnClasificarProvider(
            ILogger<HybridTdnClasificarProvider> logger,
            IClasificarDataProvider diProvider,
            DocumentWindowExtractor windowExtractor,
            RuleBasedTdnClassifier ruleClassifier,
            FoundryTdnRescueClassifier rescueClassifier,
            IOptions<HybridTdnOptions> options,
            TelemetryClient telemetryClient)
        {
            _logger = logger;
            _diProvider = diProvider;
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

                // Si reglas dan alta confianza, retornar con eso
                if (ruleResult.Confianza >= _options.RuleConfidenceThreshold)
                {
                    result.TipologiaDetectada = ruleResult.TipologiaDetectada;
                    result.ConfianzaDI = ruleResult.Confianza;
                    result.ContentExtraido = window.ExtractedText;
                    result.Confianza = ruleResult.Confianza;
                    result.Clasificador = "RuleBasedTDN";

                    _logger.LogInformation(
                        "Clasificación aceptada por reglas (confianza={Confianza})",
                        ruleResult.Confianza);

                    return result;
                }

                _logger.LogInformation(
                    "Confianza de reglas ({Confianza}) menor a umbral ({Umbral}), escalando a DI",
                    ruleResult.Confianza, _options.RuleConfidenceThreshold);

                // Paso 3: DI con umbral de confianza
                var diResult = await _diProvider.ClasificarAsync(input, cancellationToken);
                _logger.LogInformation(
                    "Resultado DI: tipologia={Tipologia}, confianza={Confianza}",
                    diResult.TipologiaDetectada, diResult.Confianza);

                // Revisar si DI superó umbral y no es RESTO
                if (diResult.Confianza >= _options.DiConfidenceThreshold && 
                    diResult.TipologiaDetectada?.ToLowerInvariant() != "resto")
                {
                    result.TipologiaDetectada = diResult.TipologiaDetectada;
                    result.ConfianzaDI = diResult.Confianza;
                    result.ContentExtraido = diResult.ContentExtraido;
                    result.Confianza = diResult.Confianza;
                    result.Clasificador = "DocumentIntelligence";

                    _logger.LogInformation(
                        "Clasificación aceptada por DI (confianza={Confianza})",
                        diResult.Confianza);

                    return result;
                }

                if (diResult.TipologiaDetectada?.ToLowerInvariant() == "resto")
                {
                    _logger.LogWarning("DI retornó clasificación RESTO, escalando a rescate LLM");
                }
                else
                {
                    _logger.LogInformation(
                        "DI confianza ({Confianza}) menor a umbral ({Umbral}), escalando a rescate LLM",
                        diResult.Confianza, _options.DiConfidenceThreshold);
                }

                // Paso 4: Rescate con Foundry LLM
                var rescueResult = await _rescueClassifier.ClassifyAsync(
                    window,
                    _options.RescueTimeoutMs,
                    _options.MaxRetries);

                result.TipologiaDetectada = rescueResult.TipologiaDetectada;
                result.Confianza = rescueResult.Confianza;
                result.ContentExtraido = window.ExtractedText;
                result.Clasificador = "FoundryRescue";
                result.ConfianzaDI = rescueResult.Confianza; // Rescate confidence

                if (rescueResult.Razon == "timeout_exceeded")
                {
                    _logger.LogWarning("Rescate LLM por timeout, tipologia por defecto");
                    result.TipologiaDetectada = "Desconocido";
                    result.Confianza = 0.0;
                }

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
    }

    /// <summary>
    /// Opciones de configuración para HybridTdnClasificarProvider.
    /// </summary>
    public class HybridTdnOptions
    {
        public double RuleConfidenceThreshold { get; set; } = 0.75;
        public double DiConfidenceThreshold { get; set; } = 0.85;
        public int MaxCharactersPerWindow { get; set; } = 8000;
        public int PagesToInspect { get; set; } = 3;
        public int RescueTimeoutMs { get; set; } = 8000;
        public int MaxRetries { get; set; } = 1;
    }
}
