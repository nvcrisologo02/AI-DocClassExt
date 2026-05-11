using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Configuration;

namespace DocumentIA.Functions.Services;

public interface ISystemHealthService
{
    Task<ComponentsHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Servicio que agrega sondas de salud de los componentes clave del sistema.
/// Los resultados de sondas externas se cachean 45 segundos para evitar sobrecarga.
/// </summary>
public class SystemHealthService : ISystemHealthService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
    private const string CacheKey = "SystemHealthService:ComponentsHealth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGdcService _gdcService;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SystemHealthService> _logger;

    private readonly ExtractionModelRegistryLoader? _extractionLoader;
    private readonly ClassificationModelRegistryLoader? _classificationLoader;
    private readonly PromptModelRegistryLoader? _promptLoader;

    public SystemHealthService(
        IHttpClientFactory httpClientFactory,
        IGdcService gdcService,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<SystemHealthService> logger,
        ExtractionModelRegistryLoader? extractionLoader = null,
        ClassificationModelRegistryLoader? classificationLoader = null,
        PromptModelRegistryLoader? promptLoader = null)
    {
        _httpClientFactory = httpClientFactory;
        _gdcService = gdcService;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
        _extractionLoader = extractionLoader;
        _classificationLoader = classificationLoader;
        _promptLoader = promptLoader;
    }

    public async Task<ComponentsHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out ComponentsHealthSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        var snapshot = await ProbeAllComponentsAsync(cancellationToken);

        _cache.Set(CacheKey, snapshot, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });

        return snapshot;
    }

    private async Task<ComponentsHealthSnapshot> ProbeAllComponentsAsync(CancellationToken cancellationToken)
    {
        var assetResolverTask = ProbeAssetResolverAsync(cancellationToken);
        var gdcTask = ProbeGdcAsync(cancellationToken);

        await Task.WhenAll(assetResolverTask, gdcTask);

        var assetResolver = await assetResolverTask;
        var gdc = await gdcTask;
        var modelProviders = ProbeModelProviders();

        return new ComponentsHealthSnapshot
        {
            Functions = ComponentHealth.Healthy("Running"),
            AssetResolver = assetResolver,
            Gdc = gdc,
            ModelProviders = modelProviders
        };
    }

    private async Task<ComponentHealth> ProbeAssetResolverAsync(CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["AssetResolver:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return ComponentHealth.Unconfigured("AssetResolver:BaseUrl not configured");

        var apiKey = _configuration["AssetResolver:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return ComponentHealth.Unconfigured("AssetResolver:ApiKey not configured");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var client = _httpClientFactory.CreateClient("AssetResolver");
            var response = await client.GetAsync("api/assets/ping", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? ComponentHealth.Healthy($"HTTP {(int)response.StatusCode}")
                : ComponentHealth.Degraded($"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AssetResolver health probe timeout");
            return ComponentHealth.Unhealthy("Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AssetResolver health probe failed");
            return ComponentHealth.Unhealthy(ex.Message);
        }
    }

    private async Task<ComponentHealth> ProbeGdcAsync(CancellationToken cancellationToken)
    {
        var endpoint = _configuration["GDC:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
            return ComponentHealth.Unconfigured("GDC:Endpoint not configured");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var (exists, _) = await _gdcService.ConsultarDocumentoAsync(
                "HEALTHCHECK_PROBE", "00000000000000000000000000000000", "HLTH", cts.Token)
                .ConfigureAwait(false);

            return ComponentHealth.Healthy(exists ? "Reachable (document found)" : "Reachable (document not found)");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GDC health probe timeout");
            return ComponentHealth.Degraded("Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GDC health probe failed");
            return ComponentHealth.Unhealthy(ex.Message);
        }
    }

    private ModelProvidersHealth ProbeModelProviders()
    {
        var classification = _classificationLoader is not null
            ? ComponentHealth.Healthy("Loader registered")
            : ComponentHealth.Unconfigured("ClassificationModelRegistryLoader not registered");

        var extraction = _extractionLoader is not null
            ? ComponentHealth.Healthy("Loader registered")
            : ComponentHealth.Unconfigured("ExtractionModelRegistryLoader not registered");

        var prompt = _promptLoader is not null
            ? ComponentHealth.Healthy("Loader registered")
            : ComponentHealth.Unconfigured("PromptModelRegistryLoader not registered");

        var statuses = new[] { classification.Status, extraction.Status, prompt.Status };
        var aggregate = AggregateStatus(statuses);

        return new ModelProvidersHealth
        {
            Status = aggregate,
            Classification = classification,
            Extraction = extraction,
            Prompt = prompt
        };
    }

    internal static string AggregateStatus(IEnumerable<string> statuses)
    {
        var hasUnhealthy = false;
        var hasDegraded = false;
        var hasUnconfigured = false;

        foreach (var s in statuses)
        {
            switch (s)
            {
                case ComponentHealth.StatusUnhealthy: hasUnhealthy = true; break;
                case ComponentHealth.StatusDegraded: hasDegraded = true; break;
                case ComponentHealth.StatusUnconfigured: hasUnconfigured = true; break;
            }
        }

        if (hasUnhealthy) return ComponentHealth.StatusUnhealthy;
        if (hasDegraded) return ComponentHealth.StatusDegraded;
        if (hasUnconfigured) return ComponentHealth.StatusUnconfigured;
        return ComponentHealth.StatusHealthy;
    }
}

public sealed record ComponentHealth(string Status, string? Message = null)
{
    public const string StatusHealthy = "healthy";
    public const string StatusDegraded = "degraded";
    public const string StatusUnhealthy = "unhealthy";
    public const string StatusUnconfigured = "unconfigured";

    public static ComponentHealth Healthy(string? message = null) => new(StatusHealthy, message);
    public static ComponentHealth Degraded(string? message = null) => new(StatusDegraded, message);
    public static ComponentHealth Unhealthy(string? message = null) => new(StatusUnhealthy, message);
    public static ComponentHealth Unconfigured(string? message = null) => new(StatusUnconfigured, message);
}

public sealed record ModelProvidersHealth
{
    public string Status { get; init; } = ComponentHealth.StatusHealthy;
    public ComponentHealth Classification { get; init; } = ComponentHealth.Healthy();
    public ComponentHealth Extraction { get; init; } = ComponentHealth.Healthy();
    public ComponentHealth Prompt { get; init; } = ComponentHealth.Healthy();
}

public sealed record ComponentsHealthSnapshot
{
    public ComponentHealth Functions { get; init; } = ComponentHealth.Healthy();
    public ComponentHealth AssetResolver { get; init; } = ComponentHealth.Healthy();
    public ComponentHealth Gdc { get; init; } = ComponentHealth.Healthy();
    public ModelProvidersHealth ModelProviders { get; init; } = new();

    public string AggregateStatus => SystemHealthService.AggregateStatus(new[]
    {
        Functions.Status,
        AssetResolver.Status,
        Gdc.Status,
        ModelProviders.Status
    });
}