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
    private readonly ExtractionRoutingSettings _routingSettings;
    private readonly ILogger<ConfigurableExtraerDataProvider> _logger;

    public ConfigurableExtraerDataProvider(
        TipologiaConfigLoader tipologiaConfigLoader,
        MockExtraerDataProvider mockProvider,
        AzureContentUnderstandingProvider azureProvider,
        IOptions<ExtractionRoutingSettings> routingSettings,
        ILogger<ConfigurableExtraerDataProvider> logger)
    {
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _mockProvider = mockProvider;
        _azureProvider = azureProvider;
        _routingSettings = routingSettings.Value;
        _logger = logger;
    }

    public Task<ExtraccionResultado> ObtenerDatosAsync(ExtraccionInput input, CancellationToken cancellationToken = default)
    {
        var config = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
        var provider = string.IsNullOrWhiteSpace(config.Extraction.Provider)
            ? _routingSettings.DefaultProvider
            : config.Extraction.Provider;

        _logger.LogInformation("Proveedor de extracción resuelto para tipología {Tipologia}: {Provider}", input.Tipologia, provider);

        return provider.ToLowerInvariant() switch
        {
            "azure-content-understanding" => _azureProvider.ObtenerDatosAsync(input, cancellationToken),
            "mock" => _mockProvider.ObtenerDatosAsync(input, cancellationToken),
            _ => throw new NotSupportedException($"Proveedor de extracción '{provider}' no soportado para tipología '{input.Tipologia}'")
        };
    }
}