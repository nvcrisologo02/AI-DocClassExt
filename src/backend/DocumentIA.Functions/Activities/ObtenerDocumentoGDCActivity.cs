using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class ObtenerDocumentoGDCActivity
{
    private readonly ILogger<ObtenerDocumentoGDCActivity> _logger;
    private readonly IGdcService _gdcService;

    public ObtenerDocumentoGDCActivity(
        ILogger<ObtenerDocumentoGDCActivity> logger,
        IGdcService gdcService)
    {
        _logger = logger;
        _gdcService = gdcService;
    }

    [Function("ObtenerDocumentoGDCActivity")]
    public async Task<ObtenerDocumentoGDCResult> Run([ActivityTrigger] string objectIdGdc)
    {
        _logger.LogInformation("Descargando documento GDC para ObjectId={ObjectId}", objectIdGdc);
        var result = await _gdcService.ObtenerDocumentoAsync(objectIdGdc);
        _logger.LogInformation(
            "Documento GDC descargado para ObjectId={ObjectId}. Nombre={NombreArchivo}",
            objectIdGdc,
            result.NombreArchivo);
        return result;
    }
}
