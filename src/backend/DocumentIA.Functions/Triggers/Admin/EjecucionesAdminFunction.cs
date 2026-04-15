using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using DocumentIA.Data.Repositories;

namespace DocumentIA.Functions.Triggers.Admin;

public class EjecucionesAdminFunction
{
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
            NombreDocumento = e.Documento?.NombreArchivo
        }).ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }
}
