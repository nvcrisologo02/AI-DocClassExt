using System.Diagnostics;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Proveedor de prompts configurables para clasificación con resolución BD → cache → fallback.
/// Implementa caché en memoria de 120 segundos para evitar consultas repetidas a BD.
/// </summary>
public sealed class ClassificationPromptProvider : IClassificationPromptProvider
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClassificationPromptsSettings _fallbackSettings;
    private readonly ILogger<ClassificationPromptProvider> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(120);
    private const string CacheKeyPrefix = "classification_prompts_";

    // Claves canónicas definidas en documentación arquitectónica
    private const string Phase1SystemKey = "classification.phase1.system";
    private const string Phase1UserKey = "classification.phase1.user";
    private const string Phase2SystemKey = "classification.phase2.system";
    private const string Phase2UserKey = "classification.phase2.user";

    public ClassificationPromptProvider(
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        IOptions<ClassificationPromptsSettings> fallbackSettings,
        ILogger<ClassificationPromptProvider> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _fallbackSettings = fallbackSettings?.Value ?? throw new ArgumentNullException(nameof(fallbackSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClassificationPromptSet> GetPromptSetAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}all";
        var stopwatch = Stopwatch.StartNew();

        // Intento 1: Buscar en caché
        if (_cache.TryGetValue<ClassificationPromptSet>(cacheKey, out var cachedSet) && cachedSet is not null)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "[PromptResolution] Cache HIT. Source={Source}, Version={Version}, DurationMs={DurationMs}, CacheTtl={CacheTtl}s",
                cachedSet.Source,
                cachedSet.Version,
                stopwatch.ElapsedMilliseconds,
                CacheTtl.TotalSeconds);
            return cachedSet;
        }

        _logger.LogInformation("[PromptResolution] Cache MISS. Intentando resolución desde BD...");

        // Intento 2: Buscar en BD
        try
        {
            var dbStopwatch = Stopwatch.StartNew();
            var promptSet = await TryLoadFromDatabaseAsync(cancellationToken);
            dbStopwatch.Stop();

            if (promptSet is not null)
            {
                stopwatch.Stop();
                // Almacenar en caché por 120 segundos
                _cache.Set(cacheKey, promptSet, CacheTtl);
                _logger.LogInformation(
                    "[PromptResolution] Resolved from DATABASE. Source={Source}, Version={Version}, DbQueryMs={DbQueryMs}, TotalResolutionMs={TotalMs}, CacheTtl={CacheTtl}s",
                    promptSet.Source,
                    promptSet.Version,
                    dbStopwatch.ElapsedMilliseconds,
                    stopwatch.ElapsedMilliseconds,
                    CacheTtl.TotalSeconds);
                return promptSet;
            }
            else
            {
                _logger.LogWarning(
                    "[PromptResolution] Database query returned incomplete set. DbQueryMs={DbQueryMs}. Falling back to appsettings.",
                    dbStopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PromptResolution] Database query FAILED. Falling back to appsettings configuration.");
        }

        // Intento 3: Fallback a appsettings
        var fallbackSet = LoadFromFallbackConfiguration();
        stopwatch.Stop();
        _logger.LogWarning(
            "[PromptResolution] Resolved from FALLBACK (appsettings). Source={Source}, Version={Version}, TotalResolutionMs={TotalMs}, CacheTtl={CacheTtl}s",
            fallbackSet.Source,
            fallbackSet.Version,
            stopwatch.ElapsedMilliseconds,
            CacheTtl.TotalSeconds);
        
        // Cachear también el fallback para evitar reconstrucción en cada llamada
        _cache.Set(cacheKey, fallbackSet, CacheTtl);
        
        return fallbackSet;
    }

    private async Task<ClassificationPromptSet?> TryLoadFromDatabaseAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPromptTemplateRepository>();

        // Cargar los 4 prompts desde BD
        var phase1System = await repository.GetActivePromptAsync(Phase1SystemKey, cancellationToken);
        var phase1User = await repository.GetActivePromptAsync(Phase1UserKey, cancellationToken);
        var phase2System = await repository.GetActivePromptAsync(Phase2SystemKey, cancellationToken);
        var phase2User = await repository.GetActivePromptAsync(Phase2UserKey, cancellationToken);

        // Si alguno falta, no podemos usar BD (necesitamos los 4)
        if (phase1System is null || phase1User is null || phase2System is null || phase2User is null)
        {
            if (phase1System is not null || phase1User is not null || phase2System is not null || phase2User is not null)
            {
                _logger.LogWarning(
                    "[PromptResolution] Incomplete database configuration. Phase1System={P1SExists}, Phase1User={P1UExists}, Phase2System={P2SExists}, Phase2User={P2UExists}. All 4 active prompts required.",
                    phase1System is not null,
                    phase1User is not null,
                    phase2System is not null,
                    phase2User is not null);
            }
            return null;
        }

        // Todos los prompts deben tener la misma versión para coherencia
        var versions = new[] { phase1System.Version, phase1User.Version, phase2System.Version, phase2User.Version };
        if (versions.Distinct().Count() > 1)
        {
            _logger.LogWarning(
                "[PromptResolution] VERSION MISMATCH detected. Phase1SystemV={V1S}, Phase1UserV={V1U}, Phase2SystemV={V2S}, Phase2UserV={V2U}. Recommend activating prompts from same version.",
                phase1System.Version,
                phase1User.Version,
                phase2System.Version,
                phase2User.Version);
        }
        else
        {
            _logger.LogInformation(
                "[PromptResolution] All 4 prompts loaded from database with consistent version {Version}",
                phase1System.Version);
        }

        return new ClassificationPromptSet
        {
            Phase1SystemPrompt = phase1System.Content,
            Phase1UserPrompt = phase1User.Content,
            Phase2SystemPrompt = phase2System.Content,
            Phase2UserPrompt = phase2User.Content,
            Version = phase1System.Version, // Usar versión del primer prompt como referencia
            Source = "Database",
            ResolvedAtUtc = DateTime.UtcNow
        };
    }

    private ClassificationPromptSet LoadFromFallbackConfiguration()
    {
        return new ClassificationPromptSet
        {
            Phase1SystemPrompt = _fallbackSettings.Phase1.SystemPrompt,
            Phase1UserPrompt = _fallbackSettings.Phase1.UserPromptTemplate,
            Phase2SystemPrompt = _fallbackSettings.Phase2.SystemPrompt,
            Phase2UserPrompt = _fallbackSettings.Phase2.UserPromptTemplate,
            Version = 0, // Versión 0 indica fallback
            Source = "Fallback",
            ResolvedAtUtc = DateTime.UtcNow
        };
    }
}
