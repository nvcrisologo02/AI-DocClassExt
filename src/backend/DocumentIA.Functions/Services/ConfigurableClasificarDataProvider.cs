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
            "Proveedor de clasificación resuelto: {Provider}. Model solicitado: {ModelInstruction}",
            provider,
            input.Entrada.Instrucciones.Classification.Model);

        // Proveedores no-DI: ruta directa sin fallback
        if (!IsAzureDiProvider(provider))
        {
            return provider.ToLowerInvariant() switch
            {
                "mock" => await _mockProvider.ClasificarAsync(input, cancellationToken),
                _ => throw new NotSupportedException($"Proveedor de clasificación '{provider}' no soportado")
            };
        }

        // Ruta Azure DI — sin fallback si está desactivado
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

            if (resultadoDI.Confianza >= _fallbackSettings.FallbackThreshold)
            {
                return resultadoDI;
            }

            fallbackRazon = $"low_confidence:{resultadoDI.Confianza:F3}";
            _logger.LogWarning(
                "Confianza DI ({Confianza:F3}) inferior al umbral de fallback ({Umbral:F3}) para {Documento}. Activando fallback GPT.",
                resultadoDI.Confianza,
                _fallbackSettings.FallbackThreshold,
                input.Entrada.Documento.Name);
        }
        catch (Exception ex)
        {
            fallbackRazon = $"exception:{ex.GetType().Name}";
            _logger.LogWarning(
                ex,
                "Azure DI falló para {Documento}. Activando fallback GPT. Razón: {Razon}",
                input.Entrada.Documento.Name,
                fallbackRazon);
        }

        var resultadoGpt = await _gptProvider.ClasificarAsync(input, cancellationToken);
        resultadoGpt.FallbackLLM = true;
        resultadoGpt.FallbackRazon = fallbackRazon;

        _logger.LogInformation(
            "Fallback GPT completado para {Documento}. Tipología: {Tipologia}, Confianza: {Confianza:F3}",
            input.Entrada.Documento.Name,
            resultadoGpt.TipologiaDetectada,
            resultadoGpt.Confianza);

        return resultadoGpt;
    }

    private static bool IsAzureDiProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-document-intelligence" or "azure-di" or "di";
}
