using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

/// <summary>
/// Tarifa de un modelo fisico a partir de una fecha. Cambiar un precio es anadir
/// una linea nueva, nunca editar la anterior: asi las ejecuciones antiguas siguen
/// cuadrando con lo que se facturo entonces.
/// </summary>
public class TarifaIA
{
    /// <summary>Nombre del modelo fisico: deployment, classifierId, analyzerId o "prebuilt-layout".</summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>Fecha desde la que aplica esta linea, en UTC.</summary>
    public DateTime VigenteDesde { get; set; }

    /// <summary>Euros por millon de tokens de entrada a precio pleno.</summary>
    public decimal? EurEntradaPor1M { get; set; }

    /// <summary>Euros por millon de tokens de entrada servidos desde cache.</summary>
    public decimal? EurEntradaCachePor1M { get; set; }

    /// <summary>Euros por millon de tokens de salida.</summary>
    public decimal? EurSalidaPor1M { get; set; }

    /// <summary>Euros por millon de tokens de contextualizacion de Content Understanding.</summary>
    public decimal? EurContextualizacionPor1M { get; set; }

    /// <summary>Euros por pagina para los servicios que facturan por pagina.</summary>
    public decimal? EurPorPagina { get; set; }
}

/// <summary>
/// Catalogo de tarifas vigente. Se carga de la fila unica "tarifas.ia".
/// </summary>
public class TarifaRegistry
{
    public string Moneda { get; set; } = "EUR";
    public List<TarifaIA> Tarifas { get; set; } = new();
}

/// <summary>
/// Carga el catalogo de tarifas desde la fila unica de ModeloConfigs con
/// Tipo=Tarifas y Key="tarifas.ia". Mismo patron de cache que el resto de
/// cargadores de registro: cinco minutos en memoria.
///
/// Es tolerante por diseno: sin catalogo, con la fila ausente o con JSON
/// invalido devuelve un registro vacio. El control de costes nunca puede
/// tumbar un procesamiento.
/// </summary>
public class TarifaRegistryLoader
{
    public const string ClaveRegistro = "tarifas.ia";
    private const string ClaveCache = "modelos:tarifas";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public TarifaRegistryLoader(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Constructor sin dependencias para escenarios sin base de datos (tests y
    /// arranques degradados): siempre devuelve catalogo vacio.
    /// </summary>
    protected TarifaRegistryLoader()
    {
        _cache = null!;
        _scopeFactory = null!;
    }

    public virtual TarifaRegistry Load()
    {
        if (_cache is null || _scopeFactory is null)
        {
            return new TarifaRegistry();
        }

        return _cache.GetOrCreate(ClaveCache, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return LoadFromDatabase();
        })!;
    }

    private TarifaRegistry LoadFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IModeloConfigRepository>();
            var modelos = repository.GetAllActivosByTipoAsync(TipoModelo.Tarifas)
                .GetAwaiter()
                .GetResult();

            var fila = modelos.FirstOrDefault(m =>
                string.Equals(m.Key, ClaveRegistro, StringComparison.OrdinalIgnoreCase));

            return Parse(fila?.ConfiguracionJson);
        }
        catch
        {
            // Sin catalogo el sistema sigue funcionando: los consumos se registran
            // con coste nulo y el agregado se marca incompleto.
            return new TarifaRegistry();
        }
    }

    /// <summary>
    /// Deserializa el catalogo. Tolerante: cualquier entrada invalida devuelve un
    /// registro vacio en lugar de lanzar.
    /// </summary>
    public static TarifaRegistry Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TarifaRegistry();
        }

        try
        {
            return JsonSerializer.Deserialize<TarifaRegistry>(json, Opciones) ?? new TarifaRegistry();
        }
        catch (JsonException)
        {
            return new TarifaRegistry();
        }
    }
}
