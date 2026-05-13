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

        public FoundryTdnRescueClassifier(ILogger<FoundryTdnRescueClassifier> logger)
        {
            _logger = logger;
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
                            "Rescate Foundry para {Documento}, intento {Attempt}/{MaxRetries}",
                            window.DocumentName,
                            attempt + 1,
                            maxRetries + 1);

                        // Simular llamada a LLM (en producción sería Azure OpenAI vía SDK Foundry)
                        var tipoResuelto = await SimulateLlmClassificationAsync(window, cts.Token);

                        result.TipologiaDetectada = tipoResuelto;
                        result.Confianza = 0.65; // Confianza base para rescate LLM
                        result.Razon = "foundry_llm_classification";
                        result.ExitoDespuesIntento = attempt;

                        _logger.LogInformation(
                            "Clasificación de rescate exitosa para {Documento}: {Tipologia}, confianza={Confianza}",
                            window.DocumentName,
                            tipoResuelto,
                            0.65);

                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        result.Razon = "timeout_exceeded";
                        _logger.LogWarning("Timeout en rescate LLM para {Documento} después de {TimeoutMs}ms",
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
                                "Error en rescate LLM (intento {Attempt}): {Error}. Reintentando en {BackoffMs}ms",
                                attempt + 1, ex.Message, backoffMs);

                            await Task.Delay(backoffMs, cts.Token);
                        }
                        else
                        {
                            result.Razon = "max_retries_exceeded";
                            _logger.LogError("Rescate LLM falló después de {MaxRetries} reintentos: {Error}",
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

        private async Task<string> SimulateLlmClassificationAsync(DocumentClassificationWindow window, CancellationToken cancellationToken)
        {
            // En producción, esto llamaría a Azure OpenAI Foundry SDK
            // Por ahora simulamos una clasificación simple basada en palabras clave
            await Task.Delay(100, cancellationToken); // Simular latencia de red

            var texto = window.ExtractedText?.ToLowerInvariant() ?? string.Empty;

            if (texto.Contains("compraventa"))
                return "escr.compraventa";
            if (texto.Contains("hipoteca"))
                return "escr.prestamo-originario";
            if (texto.Contains("cancelacion"))
                return "escr.cancelacion-hipotecaria";

            return "escr.titularidad.otro";
        }
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
