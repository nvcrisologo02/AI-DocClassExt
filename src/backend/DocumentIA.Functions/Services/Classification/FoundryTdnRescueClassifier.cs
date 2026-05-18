using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services.Classification
{
    /// <summary>
    /// Clasificador de rescate usando Foundry Azure OpenAI con timeout y retry.
    /// Se invoca cuando la confianza de reglas o DI es baja (fallback LLM).
    /// </summary>
    public class FoundryTdnRescueClassifier
    {
        private readonly ILogger<FoundryTdnRescueClassifier> _logger;
        private readonly GptClasificarDataProvider _gptProvider;

        public FoundryTdnRescueClassifier(ILogger<FoundryTdnRescueClassifier> logger, GptClasificarDataProvider gptProvider)
        {
            _logger = logger;
            _gptProvider = gptProvider;
        }

        /// <summary>
        /// Clasifica el documento usando Foundry LLM como rescate.
        /// Retorna inmediatamente si el timeout se alcanza (fallback a Desconocido).
        /// </summary>
        public async Task<FoundryRescueClassificationResult> ClassifyAsync(
            DocumentClassificationWindow window,
            int timeoutMs = 8000,
            int maxRetries = 1)
        {
            var result = new FoundryRescueClassificationResult
            {
                DocumentName = window.DocumentName,
                TimeoutMs = timeoutMs,
                Timestamp = DateTime.UtcNow
            };

            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                for (int attempt = 0; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Rescate Foundry (GPT) para {Documento}, intento {Attempt}/{MaxRetries}",
                            window.DocumentName,
                            attempt + 1,
                            maxRetries + 1);

                        // Construir input para GptClasificarDataProvider
                        var input = new ClasificacionInput
                        {
                            Entrada = new ContratoEntrada
                            {
                                Documento = new Documento
                                {
                                    Name = window.DocumentName ?? string.Empty,
                                    Content = new ContenidoDocumento { Base64 = string.Empty }
                                },
                                Instrucciones = new Instrucciones()
                            },
                            DatosNormalizados = new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Markdown", window.ExtractedText ?? string.Empty }
                            },
                            TotalPaginas = window.TotalPaginas,
                            CharsTextoNativo = window.CharsTextoNativo
                        };

                        var gptResult = await _gptProvider.ClasificarAsync(input, cts.Token);

                        result.TipologiaDetectada = gptResult.TipologiaDetectada;
                        result.Confianza = gptResult.Confianza;
                        result.Razon = "foundry_llm_classification";
                        result.ExitoDespuesIntento = attempt;

                        _logger.LogInformation(
                            "Clasificación de rescate GPT exitosa para {Documento}: {Tipologia}, confianza={Confianza}",
                            window.DocumentName,
                            gptResult.TipologiaDetectada,
                            gptResult.Confianza);

                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        result.Razon = "timeout_exceeded";
                        _logger.LogWarning("Timeout en rescate GPT para {Documento} después de {TimeoutMs}ms",
                            window.DocumentName, timeoutMs);
                        break;
                    }
                    catch (Exception ex)
                    {
                        result.ErrorMessage = ex.Message;

                        if (attempt < maxRetries)
                        {
                            int backoffMs = 500 * (attempt + 1); // 500ms, 1000ms, ...
                            _logger.LogWarning(
                                "Error en rescate GPT (intento {Attempt}): {Error}. Reintentando en {BackoffMs}ms",
                                attempt + 1, ex.Message, backoffMs);

                            await Task.Delay(backoffMs, cts.Token);
                        }
                        else
                        {
                            result.Razon = "max_retries_exceeded";
                            _logger.LogError("Rescate GPT falló después de {MaxRetries} reintentos: {Error}",
                                maxRetries + 1, ex.Message);
                        }
                    }
                }
            }

            // Si llegamos aquí, falló o timeout
            result.TipologiaDetectada = "Desconocido";
            result.Confianza = 0.0;
            if (string.IsNullOrEmpty(result.Razon))
                result.Razon = "unknown_error";

            return result;
        }

        // Eliminado: SimulateLlmClassificationAsync. Ahora delega en GptClasificarDataProvider.
    }

    /// <summary>
    /// Resultado de la clasificación de rescate Foundry.
    /// </summary>
    public class FoundryRescueClassificationResult
    {
        public string? DocumentName { get; set; }
        public string? TipologiaDetectada { get; set; }
        public double Confianza { get; set; }
        public string? Razon { get; set; }
        public int TimeoutMs { get; set; }
        public int ExitoDespuesIntento { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
