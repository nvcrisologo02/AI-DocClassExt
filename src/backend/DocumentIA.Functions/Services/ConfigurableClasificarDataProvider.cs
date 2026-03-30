using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

public class ConfigurableClasificarDataProvider : IClasificarDataProvider
{
    private readonly MockClasificarDataProvider _mockProvider;
    private readonly AzureDocumentIntelligenceClasificarProvider _azureProvider;
    private readonly GptClasificarDataProvider _gptProvider;
    private readonly ClassificationRoutingSettings _routingSettings;
    private readonly GptClasificarSettings _fallbackSettings;
    private readonly ILogger<ConfigurableClasificarDataProvider> _logger;

    public ConfigurableClasificarDataProvider(
        MockClasificarDataProvider mockProvider,
        AzureDocumentIntelligenceClasificarProvider azureProvider,
        GptClasificarDataProvider gptProvider,
        IOptions<ClassificationRoutingSettings> routingSettings,
        IOptions<GptClasificarSettings> fallbackSettings,
        ILogger<ConfigurableClasificarDataProvider> logger)
    {
        _mockProvider = mockProvider;
        _azureProvider = azureProvider;
        _gptProvider = gptProvider;
        _routingSettings = routingSettings.Value;
        _fallbackSettings = fallbackSettings.Value;
        _logger = logger;
    }

    public async Task<ResultadoClasificacion> ClasificarAsync(
        ClasificacionInput input,
        CancellationToken cancellationToken = default)
    {
        var requestedProvider = input.Entrada.Instrucciones.Classification.Provider;
        var provider = string.IsNullOrWhiteSpace(requestedProvider)
            || string.Equals(requestedProvider, "auto", StringComparison.OrdinalIgnoreCase)
            ? _routingSettings.DefaultProvider
            : requestedProvider;

        _logger.LogInformation(
            "Proveedor de clasificaciÃ³n resuelto: {Provider}. Model solicitado: {ModelInstruction}",
            provider,
            input.Entrada.Instrucciones.Classification.Model);

        // Proveedores no-DI: ruta directa sin fallback
        if (!IsAzureDiProvider(provider))
        {
            return provider.ToLowerInvariant() switch
            {
                "mock" => await _mockProvider.ClasificarAsync(input, cancellationToken),
                _ => throw new NotSupportedException($"Proveedor de clasificaciÃ³n '{provider}' no soportado")
            };
        }

        // Ruta Azure DI â€” sin fallback si estÃ¡ desactivado
        if (!_fallbackSettings.Enabled)
        {
            return await _azureProvider.ClasificarAsync(input, cancellationToken);
        }

        // Ruta Azure DI con fallback GPT activo
        ResultadoClasificacion? resultadoDI = null;
        string? fallbackRazon = null;

        try
        {
            resultadoDI = await _azureProvider.ClasificarAsync(input, cancellationToken);

            // Si DI devuelve RESTO (tipologÃ­a genÃ©rica/desconocida), activar fallback GPT sin importar confianza
            if (string.Equals(resultadoDI.TipologiaDetectada, "RESTO", StringComparison.OrdinalIgnoreCase))
            {
                fallbackRazon = $"resto_classification:{resultadoDI.Confianza:F3}";
                _logger.LogWarning(
                    "Azure DI clasificÃ³ como RESTO para {Documento}. Activando fallback GPT obligatorio.",
                    input.Entrada.Documento.Name);
            }
            else if (resultadoDI.Confianza >= (input.UmbralFallbackEfectivo ?? _fallbackSettings.FallbackThreshold))
            {
                return resultadoDI;
            }
            else
            {
                var umbralEfectivo = input.UmbralFallbackEfectivo ?? _fallbackSettings.FallbackThreshold;
                fallbackRazon = $"low_confidence:{resultadoDI.Confianza:F3}";
                _logger.LogWarning(
                    "Confianza DI ({Confianza:F3}) inferior al umbral de fallback ({Umbral:F3}) para {Documento}. Activando fallback GPT.",
                    resultadoDI.Confianza,
                    umbralEfectivo,
                    input.Entrada.Documento.Name);
            }
        }
        catch (Exception ex)
        {
            fallbackRazon = $"exception:{ex.GetType().Name}";
            _logger.LogWarning(
                ex,
                "Azure DI fallÃ³ para {Documento}. Activando fallback GPT. RazÃ³n: {Razon}",
                input.Entrada.Documento.Name,
                fallbackRazon);
        }

        // Si DI extrajo contenido textual, inyectarlo en DatosNormalizados para que el fallback GPT lo use
        if (!string.IsNullOrWhiteSpace(resultadoDI?.ContentExtraido)
            && !input.DatosNormalizados.ContainsKey("Markdown"))
        {
            input.DatosNormalizados["Markdown"] = resultadoDI.ContentExtraido;
            _logger.LogInformation(
                "Contenido DI ({Length} chars) inyectado en DatosNormalizados para fallback GPT de clasificaciÃ³n.",
                resultadoDI.ContentExtraido.Length);
        }

        try
        {
            var resultadoGpt = await _gptProvider.ClasificarAsync(input, cancellationToken);
            resultadoGpt.FallbackLLM = true;
            resultadoGpt.FallbackRazon = fallbackRazon;
            // Propagar la confianza DI original para observabilidad
            if (resultadoDI is not null)
                resultadoGpt.ConfianzaDI = resultadoDI.ConfianzaDI;

            _logger.LogInformation(
                "Fallback GPT completado para {Documento}. TipologÃ­a: {Tipologia}, Confianza: {Confianza:F3}",
                input.Entrada.Documento.Name,
                resultadoGpt.TipologiaDetectada,
                resultadoGpt.Confianza);

            // ValidaciÃ³n: Si el fallback GPT no logrÃ³ clasificar (devuelve Desconocido o muy baja confianza),
            // lanzar excepciÃ³n para terminar la clasificaciÃ³n sin intentar resolver la tipologÃ­a
            if (string.Equals(resultadoGpt.TipologiaDetectada, "Desconocido", StringComparison.OrdinalIgnoreCase)
                || resultadoGpt.Confianza < 0.3)
            {
                _logger.LogWarning(
                    "Fallback GPT no logrÃ³ clasificar el documento {Documento}. " +
                    "TipologÃ­a: {Tipologia}, Confianza: {Confianza:F3}. Abortando procesamiento.",
                    input.Entrada.Documento.Name,
                    resultadoGpt.TipologiaDetectada,
                    resultadoGpt.Confianza);

                throw new InvalidOperationException(
                    $"No se ha podido identificar la tipologia del documento. " +
                    $"Fallback GPT devolviÃ³: {resultadoGpt.TipologiaDetectada} (confianza: {resultadoGpt.Confianza:F3})");
            }

            return resultadoGpt;
        }
        catch (InvalidOperationException)
        {
            // Si fallback GPT no logrÃ³ clasificar documentos Desconocido, propagar la excepciÃ³n
            // para que el orquestador termine con ERROR sin retroceder a DI
            throw;
        }
        catch (Exception ex) when (resultadoDI is not null)
        {
            resultadoDI.FallbackLLM = false;
            resultadoDI.FallbackRazon = $"fallback_attempt_failed:{ex.GetType().Name}";

            _logger.LogWarning(
                ex,
                "Fallback GPT fallÃ³ para {Documento}. Se mantiene el resultado de Azure DI.",
                input.Entrada.Documento.Name);

            return resultadoDI;
        }
    }

    private static bool IsAzureDiProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-document-intelligence" or "azure-di" or "di";
}

