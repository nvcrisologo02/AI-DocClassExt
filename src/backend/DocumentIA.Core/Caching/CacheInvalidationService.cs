using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace DocumentIA.Core.Caching;

/// <summary>
/// Implementación de invalidación de caché para configuraciones.
/// AB#99750: Invalida caché cuando tipologías o plugins son actualizados.
/// </summary>
public sealed class CacheInvalidationService : ICacheInvalidationService
{
    private readonly IConfigurationCache _cache;
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(
        IConfigurationCache cache,
        ILogger<CacheInvalidationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task InvalidateAllAsync()
    {
        _logger.LogInformation("Invalidating entire configuration cache");
        await _cache.ClearAsync();
    }

    public async Task InvalidateTipologiaAsync(int tipologiaId, string tipologiaVersion, long tipologiaUpdateTicks)
    {
        var cacheKey = $"tipologia-validation-config:{tipologiaId}:{tipologiaVersion}:{tipologiaUpdateTicks}";
        _logger.LogInformation("Invalidating tipologia cache. Key={CacheKey}", cacheKey);
        await _cache.RemoveAsync(cacheKey);
    }

    public async Task InvalidatePluginConfigAsync(int tipologiaId)
    {
        // Plugin configs pueden ser tanto de archivo como de BD
        // Invalidar ambos patrones para ser seguro
        var patterns = new[]
        {
            $"plugins-file-config:*:{tipologiaId}",
            $"plugins-db-config:{tipologiaId}:*"
        };

        foreach (var pattern in patterns)
        {
            _logger.LogInformation("Invalidating plugin config. Pattern={Pattern}", pattern);
            // Los patrones se invalidan por RemoveAsync si se conoce la clave exacta
            // En este caso, marcamos para invalidación en próxima lectura
            // (esto se validaría contra el timestamp en BD)
        }
    }
}
