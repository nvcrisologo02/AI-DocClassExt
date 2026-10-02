using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services.Classification;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Core.Configuration;

public sealed class ArtefactoEmbeddingsConfig
{
    public string Container { get; set; } = "documentai";
    public string BlobPath { get; set; } = string.Empty;
}

public sealed class RestringidoEmbeddingsConfig
{
    public string Modo { get; set; } = ModosEmbeddings.Sombra;
    public double UmbralMasa { get; set; } = 0.5;
    public double UmbralConfianzaCondicionada { get; set; } = 0.8;

    [JsonIgnore]
    public string ModoNormalizado => ModosEmbeddings.Normalizar(Modo);
}

/// <summary>
/// Fila ModeloConfigs Tipo=Embeddings, Key="clasificador.embeddings". Las claves
/// ResourceAlias, Endpoint, AuthMode y ApiKey siguen la grafia de los demas loaders;
/// el resto es propio de este tipo. Se deserializa sin distinguir mayusculas. AB#100779.
/// </summary>
public sealed class EmbeddingsClasificadorConfig
{
    public const string ClaveRegistro = "clasificador.embeddings";

    public string Key { get; set; } = ClaveRegistro;
    public string Provider { get; set; } = "azure-openai";
    public string DeploymentName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ResourceAlias { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "ApiKey";
    public string ApiKey { get; set; } = string.Empty;
    public ArtefactoEmbeddingsConfig Artefacto { get; set; } = new();
    public string Modo { get; set; } = ModosEmbeddings.Off;
    public double UmbralConfianza { get; set; } = 0.6;
    public RestringidoEmbeddingsConfig Restringido { get; set; } = new();
    /// <summary>Tope de caracteres por defecto del texto; el orquestador lo aplica antes de la activity.</summary>
    public const int MaxCharsPorDefecto = 24_000;

    public int MaxChars { get; set; } = MaxCharsPorDefecto;
    public int TimeoutSeconds { get; set; } = 20;

    [JsonIgnore]
    public string ModoNormalizado => ModosEmbeddings.Normalizar(Modo);

    /// <summary>Algun modo distinto de off: hay que poder llamar al deployment y cargar el artefacto.</summary>
    [JsonIgnore]
    public bool EstaActiva => ModoNormalizado != ModosEmbeddings.Off || Restringido.ModoNormalizado != ModosEmbeddings.Off;

    /// <summary>Por que el loader la ha dejado en off; nulo si viene asi de BD.</summary>
    [JsonIgnore]
    public string? MotivoDesactivacion { get; set; }

    public static EmbeddingsClasificadorConfig Desactivada(string? motivo) => new()
    {
        Modo = ModosEmbeddings.Off,
        Restringido = new RestringidoEmbeddingsConfig { Modo = ModosEmbeddings.Off },
        MotivoDesactivacion = motivo
    };

    public ParametrosDecision ToParametros(bool expectedTypeInformado, IReadOnlyList<string>? restriccion) => new()
    {
        Modo = ModoNormalizado,
        UmbralConfianza = UmbralConfianza,
        ModoRestringido = Restringido.ModoNormalizado,
        UmbralMasa = Restringido.UmbralMasa,
        UmbralConfianzaCondicionada = Restringido.UmbralConfianzaCondicionada,
        ExpectedTypeInformado = expectedTypeInformado,
        RestriccionCodigos = restriccion
    };
}

/// <summary>
/// Carga la fila del clasificador por embeddings con cache de cinco minutos. Tolerante
/// por diseno: fila ausente, inactiva, JSON invalido, alias sin mapear o configuracion
/// incompleta devuelven una configuracion en off con el motivo, nunca una excepcion.
/// </summary>
public class EmbeddingsClasificadorConfigLoader
{
    private const string ClaveCache = "modelos:embeddings";

    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAiEndpointResolver _endpointResolver;
    private readonly ILogger<EmbeddingsClasificadorConfigLoader>? _logger;

    public EmbeddingsClasificadorConfigLoader(
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        IAiEndpointResolver? endpointResolver = null,
        ILogger<EmbeddingsClasificadorConfigLoader>? logger = null)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _endpointResolver = endpointResolver ?? AiEndpointResolver.Empty;
        _logger = logger;
    }

    /// <summary>Para dobles de prueba: Load() es virtual.</summary>
    protected EmbeddingsClasificadorConfigLoader()
    {
        _cache = null!;
        _scopeFactory = null!;
        _endpointResolver = AiEndpointResolver.Empty;
    }

    public virtual EmbeddingsClasificadorConfig Load()
    {
        if (_cache is null || _scopeFactory is null)
        {
            return EmbeddingsClasificadorConfig.Desactivada("loader_sin_configurar");
        }

        return _cache.GetOrCreate(ClaveCache, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return LoadFromDatabase();
        })!;
    }

    private EmbeddingsClasificadorConfig LoadFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IModeloConfigRepository>();
            var filas = repository.GetAllActivosByTipoAsync(TipoModelo.Embeddings).GetAwaiter().GetResult();

            var fila = filas.FirstOrDefault(f =>
                f.Activo && string.Equals(f.Key, EmbeddingsClasificadorConfig.ClaveRegistro, StringComparison.OrdinalIgnoreCase));

            if (fila is null)
            {
                _logger?.LogWarning("La fila {Key} de ModeloConfigs no existe o esta inactiva. El clasificador por embeddings queda en off.",
                    EmbeddingsClasificadorConfig.ClaveRegistro);
                return EmbeddingsClasificadorConfig.Desactivada("fila_ausente");
            }

            var config = Parse(fila.ConfiguracionJson);
            if (config.MotivoDesactivacion == "json_invalido")
            {
                _logger?.LogWarning("La fila {Key} de ModeloConfigs tiene un ConfiguracionJson invalido. El clasificador por embeddings queda en off.",
                    EmbeddingsClasificadorConfig.ClaveRegistro);
                return config;
            }

            config.Key = fila.Key;
            config.Provider = fila.Provider;
            return Validar(config);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "No se pudo cargar la fila {Key} de ModeloConfigs. El clasificador por embeddings queda en off.",
                EmbeddingsClasificadorConfig.ClaveRegistro);
            return EmbeddingsClasificadorConfig.Desactivada("error_carga");
        }
    }

    /// <summary>Deserializa la configuracion. JSON nulo, vacio o invalido devuelve off con motivo json_invalido.</summary>
    public static EmbeddingsClasificadorConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmbeddingsClasificadorConfig.Desactivada("json_invalido");
        }

        try
        {
            return JsonSerializer.Deserialize<EmbeddingsClasificadorConfig>(json, Opciones)
                ?? EmbeddingsClasificadorConfig.Desactivada("json_invalido");
        }
        catch (JsonException)
        {
            return EmbeddingsClasificadorConfig.Desactivada("json_invalido");
        }
    }

    private EmbeddingsClasificadorConfig Validar(EmbeddingsClasificadorConfig config)
    {
        if (!config.EstaActiva)
        {
            return config;
        }

        if (string.IsNullOrWhiteSpace(config.DeploymentName) || string.IsNullOrWhiteSpace(config.Artefacto.BlobPath))
        {
            _logger?.LogError("La fila {Key} esta en modo {Modo} sin DeploymentName o sin Artefacto.BlobPath. Queda en off.",
                config.Key, config.Modo);
            return EmbeddingsClasificadorConfig.Desactivada("configuracion_incompleta");
        }

        try
        {
            config.Endpoint = _endpointResolver.Resolve(config.Endpoint, config.ResourceAlias, config.Key);
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogError(ex, "La fila {Key} referencia un alias sin mapear. Queda en off.", config.Key);
            return EmbeddingsClasificadorConfig.Desactivada($"alias_sin_mapear:{config.ResourceAlias}");
        }

        if (string.IsNullOrWhiteSpace(config.Endpoint))
        {
            return EmbeddingsClasificadorConfig.Desactivada("configuracion_incompleta");
        }

        return config;
    }
}
