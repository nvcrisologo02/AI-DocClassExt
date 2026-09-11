using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Context;
using DocumentIA.Data.Repositories;

namespace DocumentIA.Functions.Triggers.Admin;

public class EjecucionesAdminFunction
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDocumentoEjecucionRepository _ejecucionRepository;
    private readonly DocumentIADbContext _dbContext;
    private readonly ILogger<EjecucionesAdminFunction> _logger;

    public EjecucionesAdminFunction(
        IDocumentoEjecucionRepository ejecucionRepository,
        DocumentIADbContext dbContext,
        ILogger<EjecucionesAdminFunction> logger)
    {
        _ejecucionRepository = ejecucionRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    internal static (int Page, int PageSize) ParsePaginacion(string? pageStr, string? pageSizeStr)
    {
        var page = int.TryParse(pageStr, out var p) ? Math.Max(1, p) : 1;
        var pageSize = int.TryParse(pageSizeStr, out var ps) ? Math.Clamp(ps, 1, 200) : 25;
        return (page, pageSize);
    }

    internal static EjecucionFiltro ParseFiltro(IDictionary<string, string> query)
    {
        string? Valor(string clave) =>
            query.TryGetValue(clave, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        // Los tramos de confianza viajan con punto decimal, independientemente de
        // la cultura del proceso que atienda la peticion.
        double? Numero(string clave) =>
            double.TryParse(Valor(clave), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

        var ahora = DateTime.UtcNow;
        var hasta = DateTime.TryParse(Valor("hasta"), null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var h) ? h : ahora;
        var desde = DateTime.TryParse(Valor("desde"), null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var d) ? d : hasta.AddDays(-7);

        // Un rango invertido devolveria siempre vacio sin que el usuario entienda
        // por que; se corrige silenciosamente intercambiando los extremos.
        if (desde > hasta)
        {
            (desde, hasta) = (hasta, desde);
        }

        return new EjecucionFiltro
        {
            Desde = desde,
            Hasta = hasta,
            Tipologia = Valor("tipologia"),
            Estado = Valor("estado"),
            Flujo = Valor("flujo"),
            Busqueda = Valor("q"),
            SubmittedBy = Valor("submittedby"),
            EstadoProceso = Valor("estadoproceso"),
            Calidad = Valor("calidad"),
            ConfianzaMin = Numero("confmin"),
            ConfianzaMax = Numero("confmax"),
            IncluirEstimados = string.Equals(Valor("incluirestimados"), "true", StringComparison.OrdinalIgnoreCase),

            // AB#100258: sin parametro se excluyen, que es lo que ha visto siempre el
            // Monitor. Un valor desconocido tambien excluye: mejor la cifra historica
            // que una mezcla silenciosa.
            Reutilizadas = Valor("reutilizadas")?.ToLowerInvariant() switch
            {
                "incluir" => FiltroReutilizadas.Incluir,
                "solo" => FiltroReutilizadas.Solo,
                _ => FiltroReutilizadas.Excluir
            }
        };
    }

    /// <summary>
    /// Agregados de coste de IA del periodo (AB#100237). Mismo filtro que el resto
    /// del Monitor mas <c>incluirestimados=true</c> para sumar tambien las
    /// ejecuciones rellenadas retroactivamente.
    /// </summary>
    [Function("Admin_GetCostes")]
    public async Task<HttpResponseData> GetCostes(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/ejecuciones/costes")] HttpRequestData req)
    {
        var query = req.Query.AllKeys
            .Where(k => k is not null)
            .ToDictionary(k => k!.ToLowerInvariant(), k => req.Query[k] ?? string.Empty);

        var filtro = ParseFiltro(query);

        _logger.LogInformation(
            "Admin_GetCostes: desde={Desde} hasta={Hasta} incluirEstimados={IncluirEstimados}",
            filtro.Desde, filtro.Hasta, filtro.IncluirEstimados);

        var costes = await _ejecucionRepository.GetCostesAsync(filtro);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(costes);
        return response;
    }

    [Function("Admin_GetUltimasEjecuciones")]
    public async Task<HttpResponseData> GetUltimasEjecuciones(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/ejecuciones")] HttpRequestData req)
    {
        var query = req.Query.AllKeys
            .Where(k => k is not null)
            .ToDictionary(k => k!.ToLowerInvariant(), k => req.Query[k] ?? string.Empty);

        var filtro = ParseFiltro(query);
        var (page, pageSize) = ParsePaginacion(
            query.TryGetValue("page", out var pg) ? pg : null,
            query.TryGetValue("pagesize", out var pgs) ? pgs : null);

        _logger.LogInformation(
            "Admin_GetUltimasEjecuciones: desde={Desde} hasta={Hasta} page={Page} pageSize={PageSize}",
            filtro.Desde, filtro.Hasta, page, pageSize);

        var (ejecuciones, total) = await _ejecucionRepository.GetPagedAsync(filtro, page, pageSize);

        var items = ejecuciones.Select(e => new
        {
            e.Id,
            e.EjecucionGuid,
            FechaEjecucion = e.FechaEjecucion,
            e.Tipologia,
            e.ClassificationOnly,
            TipoFlujo = e.ClassificationOnly ? "Clasificacion" : "Completo",
            e.EstadoFinal,
            e.ConfianzaGlobal,
            e.ConfianzaClasificacion,
            e.UseFallbackLLM,
            e.DuracionTotalMs,
            e.DuracionClasificacionMs,
            e.DuracionExtraccionMs,
            e.DuracionGDCMs,
            e.DuracionValidacionMs,
            e.DuracionIntegracionMs,
            e.DuracionPersistenciaMs,
            NombreDocumento = e.NombreDocumento,
            SubmittedBy = e.SubmittedBy,
            Actividades = ParseActivitySummaries(e.ActivityTimelineJson),
            e.CosteIAEur,
            e.CosteEstimado,
            e.ReutilizadaPorDuplicado,
            e.EjecucionOriginalId
        }).ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { items, total, page, pageSize });
        return response;
    }

    [Function("Admin_GetEjecucionDetalle")]
    public async Task<HttpResponseData> GetEjecucionDetalle(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/ejecuciones/{guid}/detalle")] HttpRequestData req,
        string guid)
    {
        _logger.LogInformation("Admin_GetEjecucionDetalle: guid={Guid}", guid);

        var ejecucion = await _ejecucionRepository.GetByGuidAsync(guid);
        if (ejecucion is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        // Intentar parsear el contrato de salida completo para enriquecer la respuesta
        ContratoSalida? contrato = null;
        if (!string.IsNullOrEmpty(ejecucion.ContratoSalidaCompletoJson))
        {
            try
            {
                contrato = JsonSerializer.Deserialize<ContratoSalida>(ejecucion.ContratoSalidaCompletoJson, _jsonOpts);

                // AB#100167: el contrato persistido va sin Seguimiento.Actividades desde
                // AB#100166; el timeline del detalle se recompone desde su columna.
                ContratoTimelineRehidratador.Rehidratar(contrato, ejecucion.ActivityTimelineJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo deserializar ContratoSalidaCompletoJson para ejecucion {Guid}", guid);
            }
        }

        var identificacion = contrato == null ? null : new
        {
            contrato.Identificacion.Documento,
            contrato.Identificacion.Tipologia,
            contrato.Identificacion.TipologiaFamilia,
            contrato.Identificacion.TipologiaVersion,
            contrato.Identificacion.Paginas,
            FechaProceso = contrato.Identificacion.FechaProceso
        };

        var integridad = contrato == null ? null : new
        {
            contrato.Integridad.SHA256,
            contrato.Integridad.MD5,
            contrato.Integridad.CRC32,
            contrato.Integridad.RutaBlobStorage,
            contrato.Integridad.IdActivo,
            contrato.Integridad.GestorDocumental,
            contrato.Integridad.IdActivoEntrada,
            contrato.Integridad.IdActivoCambiado
        };

        var resultado = contrato == null ? null : new
        {
            contrato.Resultado.Estado,
            contrato.Resultado.EstadoCalidad,
            contrato.Resultado.ConfianzaGlobal,
            contrato.Resultado.ConfianzaClasificacion,
            contrato.Resultado.ConfianzaExtraccion,
            contrato.Resultado.ConfianzaValidacion,
            contrato.Resultado.MensajeError,
            contrato.Resultado.ReutilizadaPorDuplicado,
            contrato.Resultado.MensajeReutilizacion
        };

        var clasificacion = contrato == null ? null : new
        {
            contrato.DetalleEjecucion.Clasificacion.Modelo,
            contrato.DetalleEjecucion.Clasificacion.ProveedorClasif,
            contrato.DetalleEjecucion.Clasificacion.Confianza,
            contrato.DetalleEjecucion.Clasificacion.ConfianzaDI,
            contrato.DetalleEjecucion.Clasificacion.ConfianzaGPT,
            contrato.DetalleEjecucion.Clasificacion.FallbackLLM,
            contrato.DetalleEjecucion.Clasificacion.FallbackRazon,
            contrato.DetalleEjecucion.Clasificacion.TipologiaDetectada,
            contrato.DetalleEjecucion.Clasificacion.UmbralFallbackAplicado
        };

        var extraccion = contrato == null ? null : new
        {
            contrato.DetalleEjecucion.Extraccion.Modelo,
            contrato.DetalleEjecucion.Extraccion.ProveedorExtrac,
            contrato.DetalleEjecucion.Extraccion.ConfianzaExtraccion,
            contrato.DetalleEjecucion.Extraccion.FallbackUsado,
            contrato.DetalleEjecucion.Extraccion.FallbackRazon,
            contrato.DetalleEjecucion.Extraccion.CamposConDuda,
            contrato.DetalleEjecucion.Extraccion.ConfianzaPorCampo
        };

        var gdc = contrato == null ? null : new
        {
            contrato.DetalleEjecucion.GDC.Exitoso,
            contrato.DetalleEjecucion.GDC.ObjectId,
            contrato.DetalleEjecucion.GDC.Mensaje,
            contrato.DetalleEjecucion.GDC.ErrorDetalle,
            contrato.DetalleEjecucion.GDC.YaExistia,
            contrato.DetalleEjecucion.GDC.Intentos,
            contrato.DetalleEjecucion.GDC.DuracionMs
        };

        // AB#100239: desglose por llamada, tal como quedo en el contrato. Es la unica
        // via de ver el detalle de consumos sin abrir el JSON completo.
        var costes = contrato?.DetalleEjecucion.Costes == null ? null : new
        {
            contrato.DetalleEjecucion.Costes.CosteTotalEur,
            contrato.DetalleEjecucion.Costes.TokensTotales,
            contrato.DetalleEjecucion.Costes.PaginasTotales,
            contrato.DetalleEjecucion.Costes.TarifasCompletas,
            contrato.DetalleEjecucion.Costes.ModelosSinTarifa,
            contrato.DetalleEjecucion.Costes.ReutilizadaPorDuplicado,
            contrato.DetalleEjecucion.Costes.CosteEjecucionOriginalEur,
            Consumos = contrato.DetalleEjecucion.Costes.Consumos.Select(c => new
            {
                c.Actividad,
                c.Operacion,
                c.Proveedor,
                c.Modelo,
                c.TokensEntrada,
                c.TokensEntradaCache,
                c.TokensSalida,
                c.TokensContextualizacion,
                c.Paginas,
                c.CosteEur,
                c.TarifaAplicada,
                c.Descartado
            }).ToList()
        };

        var timeline = contrato?.DetalleEjecucion.Seguimiento.Actividades
            .Select(a => new
            {
                a.Nombre,
                a.Estado,
                a.DuracionMs,
                a.Mensaje,
                a.FallbackActivado,
                a.FallbackRazon,
                InicioUtc = a.InicioUtc != default ? a.InicioUtc : (DateTime?)null,
                FinUtc = a.FinUtc
            })
            .ToList();

        var datosExtraidos = contrato?.DatosExtraidos
            .Where(kv => kv.Value != null)
            .Select(kv => new { Campo = kv.Key, Valor = ToDisplayString(kv.Value) })
            .ToList();

        var validaciones = ejecucion.Validaciones
            .OrderBy(v => v.Pasado)
            .ThenBy(v => v.Severidad)
            .ThenBy(v => v.Campo)
            .Select(v => new
            {
                v.Campo,
                v.Severidad,
                v.Mensaje,
                v.ValorOriginal,
                v.ValorEsperado,
                v.Pasado
            })
            .ToList();

        var plugins = ejecucion.PluginsEjecutados
            .OrderBy(p => p.Priority)
            .Select(p => new
            {
                p.PluginKey,
                p.Priority,
                p.Success,
                p.Mensaje,
                p.StatusCode,
                p.DurationMs,
                p.Error
            })
            .ToList();

        var (tipologiaNombreCatalogo, tipologiaFamiliaNombreCatalogo) =
            await ResolverNombresCatalogoAsync(ejecucion.Tipologia);

        // AB#100258: trazabilidad en los dos sentidos. Una reutilizacion no tiene contrato
        // propio y apunta a la original; una original enseña cuantas veces se sirvio. Sin
        // la vuelta, quien llega a la ejecucion que produjo el contenido no ve las
        // peticiones posteriores que se resolvieron con el.
        object? reutilizacion = null;
        if (ejecucion.ReutilizadaPorDuplicado)
        {
            var original = ejecucion.EjecucionOriginalId is { } originalId
                ? await _ejecucionRepository.GetByIdAsync(originalId)
                : null;

            reutilizacion = new
            {
                EsReutilizacion = true,
                OriginalId = original?.Id,
                OriginalGuid = original?.EjecucionGuid,
                OriginalFecha = original?.FechaEjecucion
            };
        }

        var reutilizaciones = ejecucion.ReutilizadaPorDuplicado
            ? new List<object>()
            : (await _ejecucionRepository.GetReutilizacionesAsync(ejecucion.Id))
                .Select(r => (object)new
                {
                    r.EjecucionGuid,
                    r.FechaEjecucion,
                    r.SubmittedBy
                })
                .ToList();

        var result = new
        {
            ejecucion.Id,
            ejecucion.EjecucionGuid,
            ejecucion.ModeloClasificacion,
            ejecucion.ClassificationOnly,
            ejecucion.Tipologia,
            TipologiaNombreCatalogo = tipologiaNombreCatalogo,
            TipologiaFamiliaNombreCatalogo = tipologiaFamiliaNombreCatalogo,
            // JSON original tal cual se almaceno, para el visor crudo en el detalle.
            ContratoSalidaCompletoJson = ejecucion.ContratoSalidaCompletoJson,
            Identificacion = identificacion,
            Integridad = integridad,
            Resultado = resultado,
            Clasificacion = clasificacion,
            Extraccion = extraccion,
            GDC = gdc,
            Costes = costes,
            ejecucion.CosteEstimado,
            Timeline = timeline,
            DatosExtraidos = datosExtraidos,
            Validaciones = validaciones,
            Plugins = plugins,
            ejecucion.ReutilizadaPorDuplicado,
            Reutilizacion = reutilizacion,
            Reutilizaciones = reutilizaciones
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("Admin_GetAgregados")]
    public async Task<HttpResponseData> GetAgregados(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/ejecuciones/agregados")] HttpRequestData req)
    {
        var query = req.Query.AllKeys
            .Where(k => k is not null)
            .ToDictionary(k => k!.ToLowerInvariant(), k => req.Query[k] ?? string.Empty);

        var filtro = ParseFiltro(query);

        _logger.LogInformation(
            "Admin_GetAgregados: desde={Desde} hasta={Hasta}", filtro.Desde, filtro.Hasta);

        var agregados = await _ejecucionRepository.GetAgregadosAsync(filtro);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(agregados);
        return response;
    }

    /// <summary>
    /// Resuelve el nombre de subtipo (CatalogoTdn2) y familia (CatalogoTdn1) a partir del
    /// codigo de tipologia guardado en la ejecucion. Los codigos de ejecucion se persisten
    /// en minusculas y con punto (ej "decl.08") mientras el catalogo usa mayusculas y guion
    /// (ej "DECL-08"); hay que normalizar antes de comparar. Ejecuciones antiguas pueden
    /// referenciar codigos ya retirados del catalogo: en ese caso se devuelve null sin fallar.
    /// Muchas ejecuciones solo registran el codigo de familia sin subtipo (ej "ESIN"); si no
    /// hay subtipo se reintenta contra CatalogoTdn1 con el prefijo antes del primer guion.
    /// Internal para permitir pruebas directas via InternalsVisibleTo (DocumentIA.Tests.Unit).
    /// </summary>
    internal async Task<(string? NombreSubtipo, string? NombreFamilia)> ResolverNombresCatalogoAsync(string? codigoTipologia)
    {
        if (string.IsNullOrWhiteSpace(codigoTipologia))
        {
            return (null, null);
        }

        var codigoNormalizado = codigoTipologia.Trim().ToUpperInvariant().Replace('.', '-');

        var subtipo = await _dbContext.CatalogoTdn2
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Codigo == codigoNormalizado);
        if (subtipo is not null)
        {
            var familiaDelSubtipo = await _dbContext.CatalogoTdn1
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Codigo == subtipo.CodigoTdn1);

            return (subtipo.Nombre, familiaDelSubtipo?.Nombre);
        }

        var codigoFamilia = codigoNormalizado.Split('-', 2)[0];
        var familia = await _dbContext.CatalogoTdn1
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Codigo == codigoFamilia);

        return (null, familia?.Nombre);
    }

    private static string ToDisplayString(object? value) => value switch
    {
        null => "",
        JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? "",
        JsonElement je when je.ValueKind == JsonValueKind.Null => "",
        JsonElement je => je.ToString(),
        _ => value.ToString() ?? ""
    };

    private static List<ActivitySummaryDto> ParseActivitySummaries(string? activityTimelineJson)
    {
        if (string.IsNullOrWhiteSpace(activityTimelineJson))
        {
            return [];
        }

        try
        {
            var activities = JsonSerializer.Deserialize<List<ActivitySummaryDto>>(activityTimelineJson, _jsonOpts);
            return activities?
                .Where(a => !string.IsNullOrWhiteSpace(a.Nombre))
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed class ActivitySummaryDto
    {
        [JsonPropertyName("Nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("Estado")]
        public string Estado { get; set; } = string.Empty;
    }
}
