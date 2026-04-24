using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class ObtenerMetadatosDocumentoGDCActivity
{
    private readonly ILogger<ObtenerMetadatosDocumentoGDCActivity> _logger;
    private readonly IGdcService _gdcService;

    public ObtenerMetadatosDocumentoGDCActivity(
        ILogger<ObtenerMetadatosDocumentoGDCActivity> logger,
        IGdcService gdcService)
    {
        _logger = logger;
        _gdcService = gdcService;
    }

    [Function("ObtenerMetadatosDocumentoGDCActivity")]
    public async Task<GdcDocumentoMetadatos> Run([ActivityTrigger] string objectIdGdc)
    {
        _logger.LogInformation("Obteniendo metadatos GDC para ObjectId={ObjectId}", objectIdGdc);
        return await _gdcService.ObtenerMetadatosDocumentoAsync(objectIdGdc);
    }
}
