using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Data.Repositories;

namespace DocumentIA.Functions.Triggers.Admin;

public class EjecucionesAdminFunction
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDocumentoEjecucionRepository _ejecucionRepository;
    private readonly ILogger<EjecucionesAdminFunction> _logger;

    public EjecucionesAdminFunction(
        IDocumentoEjecucionRepository ejecucionRepository,
        ILogger<EjecucionesAdminFunction> logger)
    {
        _ejecucionRepository = ejecucionRepository;
        _logger = logger;
    }

    [Function("Admin_GetUltimasEjecuciones")]
    public async Task<HttpResponseData> GetUltimasEjecuciones(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/ejecuciones")] HttpRequestData req)
    {
        int top = 50;
        if (req.Query["top"] is string topStr && int.TryParse(topStr, out int topParsed))
        {
            top = Math.Clamp(topParsed, 1, 200);
        }

        _logger.LogInformation("Admin_GetUltimasEjecuciones: top={Top}", top);

        var ejecuciones = await _ejecucionRepository.GetUltimasEjecucionesAsync(top);

        var result = ejecuciones.Select(e => new
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
            NombreDocumento = e.Documento?.NombreArchivo,
            Actividades = ParseActivitySummaries(e.ActivityTimelineJson)
        }).ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("Admin_GetEjecucionDetalle")]
    public async Task<HttpResponseData> GetEjecucionDetalle(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/ejecuciones/{id:int}/detalle")] HttpRequestData req,
        int id)
    {
        _logger.LogInformation("Admin_GetEjecucionDetalle: id={Id}", id);

        var ejecucion = await _ejecucionRepository.GetByIdAsync(id);
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo deserializar ContratoSalidaCompletoJson para ejecucion {Id}", id);
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

        var result = new
        {
            ejecucion.Id,
            ejecucion.EjecucionGuid,
            ejecucion.ModeloClasificacion,
            ejecucion.ClassificationOnly,
            Identificacion = identificacion,
            Integridad = integridad,
            Resultado = resultado,
            Clasificacion = clasificacion,
            Extraccion = extraccion,
            GDC = gdc,
            Timeline = timeline,
            DatosExtraidos = datosExtraidos,
            Validaciones = validaciones,
            Plugins = plugins
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("Admin_GetAgregados")]
    public async Task<HttpResponseData> GetAgregados(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/ejecuciones/agregados")] HttpRequestData req)
    {
        int dias = 30;
        if (req.Query["dias"] is string diasStr && int.TryParse(diasStr, out int diasParsed))
            dias = Math.Clamp(diasParsed, 1, 365);

        _logger.LogInformation("Admin_GetAgregados: dias={Dias}", dias);

        var result = await _ejecucionRepository.GetAgregadosAsync(dias);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
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
