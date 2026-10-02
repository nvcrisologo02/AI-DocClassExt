using System.Diagnostics;
using System.Globalization;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services.Classification;
using DocumentIA.Functions.Services.Abstractions;
using DocumentIA.Functions.Services.Resilience;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services.Classification;

public interface IEmbeddingsClasificarProvider
{
    /// <summary>Nunca lanza: cualquier fallo vuelve como DerivarGpt con Error.</summary>
    Task<ResultadoEmbeddings> ClasificarAsync(ClasificarEmbeddingsInput input, CancellationToken cancellationToken);
}

/// <summary>
/// Encadena configuracion, artefacto, llamada de embeddings, inferencia, decisor y
/// telemetria. Es el unico punto que emite el evento Classification.Embeddings. AB#100779.
/// </summary>
public sealed class EmbeddingsClasificarProvider : IEmbeddingsClasificarProvider
{
    public const string EventoTelemetria = "Classification.Embeddings";
    public const string MetricaLatencia = "Classification.Embeddings.LatenciaMs";

    private readonly EmbeddingsClasificadorConfigLoader _config;
    private readonly ModeloEmbeddingsLoader _modelos;
    private readonly CatalogoParesTdnLoader _catalogo;
    private readonly IEmbeddingsClienteFactory _clientes;
    private readonly IAzureOpenAIResilienceExecutor _resiliencia;
    private readonly ITelemetryService _telemetria;
    private readonly ILogger<EmbeddingsClasificarProvider> _logger;

    public EmbeddingsClasificarProvider(
        EmbeddingsClasificadorConfigLoader config,
        ModeloEmbeddingsLoader modelos,
        CatalogoParesTdnLoader catalogo,
        IEmbeddingsClienteFactory clientes,
        IAzureOpenAIResilienceExecutor resiliencia,
        ITelemetryService telemetria,
        ILogger<EmbeddingsClasificarProvider> logger)
    {
        _config = config;
        _modelos = modelos;
        _catalogo = catalogo;
        _clientes = clientes;
        _resiliencia = resiliencia;
        _telemetria = telemetria;
        _logger = logger;
    }

    public async Task<ResultadoEmbeddings> ClasificarAsync(ClasificarEmbeddingsInput input, CancellationToken cancellationToken)
    {
        input ??= new ClasificarEmbeddingsInput();
        var reloj = Stopwatch.StartNew();
        var resultado = new ResultadoEmbeddings();
        var restringida = input.RestriccionCodigos is { Count: > 0 };

        try
        {
            var config = _config.Load();
            resultado.Modo = config.ModoNormalizado;
            resultado.ModoRestringido = restringida ? config.Restringido.ModoNormalizado : null;
            resultado.Deployment = config.DeploymentName;

            var modo = DecisorHibrido.ModoEfectivo(config.ModoNormalizado, config.Restringido.ModoNormalizado, restringida);
            if (modo == ModosEmbeddings.Off)
            {
                // Con motivo de desactivacion (fila ausente, inactiva o JSON invalido) se informa como error
                // para que no pase inadvertido; un off deseado no deja rastro.
                resultado.Error = config.MotivoDesactivacion;
                return Omitir(resultado, MotivosEmbeddings.Off);
            }

            var texto = TextoClasificacionResolver.Preprocesar(input.Texto, config.MaxChars);
            if (texto.Length == 0)
            {
                return Omitir(resultado, MotivosEmbeddings.SinTexto);
            }

            // La descarga del artefacto va fuera del temporizador: el timeout acota solo la llamada de embeddings.
            var modelo = await _modelos.ObtenerAsync(config.Artefacto.Container, config.Artefacto.BlobPath, cancellationToken);
            if (modelo is null)
            {
                return Derivar(resultado, MotivosEmbeddings.Error, "artefacto_no_disponible");
            }

            resultado.VersionModelo = modelo.Manifiesto.Version;

            var cliente = _clientes.Crear(config);
            var circuitKey = $"{config.Endpoint}|{config.DeploymentName}";
            using var temporizador = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            temporizador.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, config.TimeoutSeconds)));

            var respuesta = await _resiliencia.ExecuteOnceAsync(
                circuitKey,
                ct => cliente.GenerarAsync(texto, ct),
                temporizador.Token);

            if (respuesta.Consumo is not null)
            {
                resultado.Consumos = new List<ConsumoIA> { respuesta.Consumo };
            }

            if (respuesta.Vector.Length != modelo.Manifiesto.Dimensiones)
            {
                return Derivar(resultado, MotivosEmbeddings.Error,
                    $"dimensiones: el vector tiene {respuesta.Vector.Length} y el modelo espera {modelo.Manifiesto.Dimensiones}");
            }

            var distribucion = ClasificadorEmbeddings.Inferir(modelo, respuesta.Vector);
            var decision = DecisorHibrido.Decidir(
                distribucion,
                modelo.Manifiesto,
                config.ToParametros(input.ExpectedTypeInformado, input.RestriccionCodigos),
                _catalogo.Load());

            resultado.Tdn1 = decision.Tdn1;
            resultado.Tdn2 = decision.Tdn2;
            resultado.Tipologia = decision.Tipologia;
            resultado.Confianza = decision.Confianza;
            resultado.Top3 = distribucion.Top(3).Select(t => new ProbabilidadTdn1 { Tdn1 = t.Tdn1, Probabilidad = t.Probabilidad }).ToList();
            resultado.Decision = decision.Decision;
            resultado.Motivo = decision.Motivo;
            resultado.Restringido = decision.Restringido;
            return resultado;
        }
        catch (RateLimitExhaustedException ex)
        {
            return Derivar(resultado, MotivosEmbeddings.CircuitoAbierto, ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Cancelacion del llamante frente al temporizador propio del proveedor.
            return Derivar(resultado, MotivosEmbeddings.Error, cancellationToken.IsCancellationRequested ? "cancelado" : "timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clasificador por embeddings: fallo no controlado; se deriva al GPT.");
            return Derivar(resultado, MotivosEmbeddings.Error, $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            resultado.LatenciaMs = reloj.ElapsedMilliseconds;
            EmitirTelemetria(resultado, input.InstanceId);
        }
    }

    private static ResultadoEmbeddings Omitir(ResultadoEmbeddings r, string motivo)
    {
        r.Decision = DecisionesEmbeddings.Omitido;
        r.Motivo = motivo;
        return r;
    }

    private static ResultadoEmbeddings Derivar(ResultadoEmbeddings r, string motivo, string error)
    {
        r.Decision = DecisionesEmbeddings.DerivarGpt;
        r.Motivo = motivo;
        r.Error = error;
        return r;
    }

    private void EmitirTelemetria(ResultadoEmbeddings r, string? instanceId)
    {
        // Un off deseado (sin error) no emite evento ni metrica por ejecucion.
        if (r.Motivo == MotivosEmbeddings.Off && r.Error is null)
        {
            return;
        }

        try
        {
            var propiedades = new Dictionary<string, string>
            {
                ["VersionModelo"] = r.VersionModelo ?? string.Empty,
                ["Modo"] = r.Modo,
                ["ModoRestringido"] = r.ModoRestringido ?? string.Empty,
                ["Decision"] = r.Decision,
                ["Motivo"] = r.Motivo ?? string.Empty,
                ["Confianza"] = r.Confianza.ToString("F4", CultureInfo.InvariantCulture),
                ["Tdn1"] = r.Tdn1 ?? string.Empty,
                ["LatenciaMs"] = r.LatenciaMs.ToString(CultureInfo.InvariantCulture),
                ["Error"] = r.Error ?? string.Empty,
                ["InstanceId"] = instanceId ?? string.Empty
            };
            _telemetria.TrackEvent(EventoTelemetria, propiedades);
            _telemetria.TrackMetric(MetricaLatencia, r.LatenciaMs, new Dictionary<string, string>
            {
                ["Modo"] = r.Modo,
                ["Decision"] = r.Decision
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo emitir la telemetria del clasificador por embeddings.");
        }
    }
}
