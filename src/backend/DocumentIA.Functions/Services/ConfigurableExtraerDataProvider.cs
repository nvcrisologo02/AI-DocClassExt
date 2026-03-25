using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

public class ConfigurableExtraerDataProvider : IExtraerDataProvider
{
    private readonly TipologiaConfigLoader _tipologiaConfigLoader;
    private readonly MockExtraerDataProvider _mockProvider;
    private readonly AzureContentUnderstandingProvider _azureProvider;
    private readonly GptFallbackExtraerDataProvider _gptFallbackProvider;
    private readonly ExtractionRoutingSettings _routingSettings;
    private readonly GptFallbackExtraerSettings _fallbackSettings;
    private readonly ILogger<ConfigurableExtraerDataProvider> _logger;

    public ConfigurableExtraerDataProvider(
        TipologiaConfigLoader tipologiaConfigLoader,
        MockExtraerDataProvider mockProvider,
        AzureContentUnderstandingProvider azureProvider,
        GptFallbackExtraerDataProvider gptFallbackProvider,
        IOptions<ExtractionRoutingSettings> routingSettings,
        IOptions<GptFallbackExtraerSettings> fallbackSettings,
        ILogger<ConfigurableExtraerDataProvider> logger)
    {
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _mockProvider = mockProvider;
        _azureProvider = azureProvider;
        _gptFallbackProvider = gptFallbackProvider;
        _routingSettings = routingSettings.Value;
        _fallbackSettings = fallbackSettings.Value;
        _logger = logger;
    }

    public async Task<ExtraccionResultado> ObtenerDatosAsync(ExtraccionInput input, CancellationToken cancellationToken = default)
    {
        var config = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
        var provider = string.IsNullOrWhiteSpace(config.Extraction.Provider)
            ? _routingSettings.DefaultProvider
            : config.Extraction.Provider;

        _logger.LogInformation("Proveedor de extracción resuelto para tipología {Tipologia}: {Provider}", input.Tipologia, provider);

        if (!IsAzureContentUnderstandingProvider(provider) || !_fallbackSettings.Enabled)
        {
            return provider.ToLowerInvariant() switch
            {
                "azure-content-understanding" => await _azureProvider.ObtenerDatosAsync(input, cancellationToken),
                "mock" => await _mockProvider.ObtenerDatosAsync(input, cancellationToken),
                _ => throw new NotSupportedException($"Proveedor de extracción '{provider}' no soportado para tipología '{input.Tipologia}'")
            };
        }

        ExtraccionResultado? resultadoCu = null;
        string? fallbackRazon = null;

        try
        {
            resultadoCu = await _azureProvider.ObtenerDatosAsync(input, cancellationToken);

            if (EsResultadoCuSuficiente(config, resultadoCu, out var ratio, out var esperados, out var obtenidos))
            {
                return resultadoCu;
            }

            fallbackRazon = $"insufficient_fields:{obtenidos}/{esperados} ratio={ratio:F3}";
            _logger.LogWarning(
                "Extracción CU insuficiente para {Tipologia}. Ratio={Ratio:F3}, obtenidos={Obtenidos}, esperados={Esperados}. Activando fallback GPT.",
                input.Tipologia,
                ratio,
                obtenidos,
                esperados);
        }
        catch (Exception ex)
        {
            fallbackRazon = $"exception:{ex.GetType().Name}";
            _logger.LogWarning(ex, "Extracción CU falló para {Tipologia}. Activando fallback GPT.", input.Tipologia);
        }

        var resultadoGpt = await _gptFallbackProvider.ObtenerDatosConFallbackAsync(
            input,
            config,
            resultadoCu?.MarkdownExtraido,
            cancellationToken);

        resultadoGpt.FallbackUsado = true;
        resultadoGpt.FallbackRazon = fallbackRazon;

        if (resultadoGpt.Paginas <= 0 && resultadoCu?.Paginas > 0)
        {
            resultadoGpt.Paginas = resultadoCu.Paginas;
        }

        return resultadoGpt;
    }

    private bool EsResultadoCuSuficiente(
        TipologiaValidationConfig config,
        ExtraccionResultado resultadoCu,
        out double ratio,
        out int esperados,
        out int obtenidos)
    {
        esperados = config.Fields.Count;
        obtenidos = resultadoCu.DatosExtraidos.Count;

        if (esperados <= 0)
        {
            ratio = 1.0;
            return true;
        }

        ratio = (double)obtenidos / esperados;
        return ratio >= _fallbackSettings.MinFieldsRatio;
    }

    private static bool IsAzureContentUnderstandingProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-content-understanding" or "azure-cu" or "cu";
}