using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

public class ConfigurableClasificarDataProvider : IClasificarDataProvider
{
    private readonly MockClasificarDataProvider _mockProvider;
    private readonly AzureDocumentIntelligenceClasificarProvider _azureProvider;
    private readonly ClassificationRoutingSettings _routingSettings;
    private readonly ILogger<ConfigurableClasificarDataProvider> _logger;

    public ConfigurableClasificarDataProvider(
        MockClasificarDataProvider mockProvider,
        AzureDocumentIntelligenceClasificarProvider azureProvider,
        IOptions<ClassificationRoutingSettings> routingSettings,
        ILogger<ConfigurableClasificarDataProvider> logger)
    {
        _mockProvider = mockProvider;
        _azureProvider = azureProvider;
        _routingSettings = routingSettings.Value;
        _logger = logger;
    }

    public Task<ResultadoClasificacion> ClasificarAsync(ClasificacionInput input, CancellationToken cancellationToken = default)
    {
        var requestedProvider = input.Entrada.Instrucciones.Classification.Provider;
        var provider = string.IsNullOrWhiteSpace(requestedProvider) || string.Equals(requestedProvider, "auto", StringComparison.OrdinalIgnoreCase)
            ? _routingSettings.DefaultProvider
            : requestedProvider;

        _logger.LogInformation(
            "Proveedor de clasificación resuelto: {Provider}. Model solicitado: {ModelInstruction}",
            provider,
            input.Entrada.Instrucciones.Classification.Model);

        return provider.ToLowerInvariant() switch
        {
            "azure-document-intelligence" or "azure-di" or "di" => _azureProvider.ClasificarAsync(input, cancellationToken),
            "mock" => _mockProvider.ClasificarAsync(input, cancellationToken),
            _ => throw new NotSupportedException($"Proveedor de clasificación '{provider}' no soportado")
        };
    }
}
