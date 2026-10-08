using System.Text.Json;
using DocumentIA.Core.Services.Classification;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Core.Configuration;

/// <summary>
/// Mapa par TDN1/TDN2 -> tipologia publicada y activa, con clave "TDN1|TDN2" sin
/// distinguir mayusculas. Cache de cinco minutos como el resto de loaders. AB#100779.
/// </summary>
public class CatalogoParesTdnLoader
{
    private const string ClaveCache = "tipologias:pares-tdn";

    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogoParesTdnLoader>? _logger;

    public CatalogoParesTdnLoader(IMemoryCache cache, IServiceScopeFactory scopeFactory, ILogger<CatalogoParesTdnLoader>? logger = null)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Para dobles de prueba: Load() es virtual.</summary>
    protected CatalogoParesTdnLoader()
    {
        _cache = null!;
        _scopeFactory = null!;
    }

    public virtual IReadOnlyDictionary<string, TipologiaPar> Load()
    {
        if (_cache is null || _scopeFactory is null)
        {
            return new Dictionary<string, TipologiaPar>(StringComparer.OrdinalIgnoreCase);
        }

        if (_cache.TryGetValue(ClaveCache, out IReadOnlyDictionary<string, TipologiaPar>? cacheado) && cacheado is not null)
        {
            return cacheado;
        }

        // El mapa vacio de un fallo de BD no se cachea: la siguiente ejecucion vuelve a intentarlo.
        var catalogo = LoadFromDatabase();
        if (catalogo is not null)
        {
            _cache.Set(ClaveCache, catalogo, TimeSpan.FromMinutes(5));
            return catalogo;
        }

        return new Dictionary<string, TipologiaPar>(StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyDictionary<string, TipologiaPar>? LoadFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();
            var tipologias = repository.GetAllPublishedAsync().GetAwaiter().GetResult();
            return Construir(tipologias, _logger);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "No se pudo cargar el catalogo de pares TDN. El clasificador por embeddings no podra mapear tipologias.");
            return null;
        }
    }

    /// <summary>Construye el mapa. Ignora inactivas, no publicadas, sin par o con JSON invalido; ante un par duplicado conserva la primera.</summary>
    public static IReadOnlyDictionary<string, TipologiaPar> Construir(IEnumerable<TipologiaEntity> tipologias, ILogger? logger = null)
    {
        var mapa = new Dictionary<string, TipologiaPar>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tipologias)
        {
            if (!t.Activa || t.Estado != EstadoTipologia.Published || string.IsNullOrWhiteSpace(t.ConfiguracionJson))
            {
                continue;
            }

            TipologiaValidationConfig? config;
            try
            {
                config = JsonSerializer.Deserialize<TipologiaValidationConfig>(t.ConfiguracionJson, Opciones);
            }
            catch (JsonException ex)
            {
                logger?.LogWarning(ex, "ConfiguracionJson invalida en la tipologia {Codigo}; se excluye del catalogo de pares.", t.Codigo);
                continue;
            }

            var tdn1 = config?.ResolvedTdn1?.Trim();
            var tdn2 = config?.ResolvedTdn2?.Trim();
            if (string.IsNullOrWhiteSpace(tdn1) || string.IsNullOrWhiteSpace(tdn2))
            {
                continue;
            }

            var par = new TipologiaPar(t.Codigo, tdn1, tdn2);
            if (!mapa.TryAdd(par.ClavePar, par))
            {
                logger?.LogWarning("Las tipologias {Primera} y {Segunda} comparten el par {Par}; se conserva la primera.",
                    mapa[par.ClavePar].Codigo, t.Codigo, par.ClavePar);
            }
        }

        return mapa;
    }
}
