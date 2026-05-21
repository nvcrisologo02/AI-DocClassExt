using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class PrepararDocumentoClasificacionActivity
{
    private readonly PdfRecorteService _pdfRecorteService;
    private readonly ILogger<PrepararDocumentoClasificacionActivity> _logger;

    public PrepararDocumentoClasificacionActivity(
        PdfRecorteService pdfRecorteService,
        ILogger<PrepararDocumentoClasificacionActivity> logger)
    {
        _pdfRecorteService = pdfRecorteService;
        _logger = logger;
    }

    [Function("PrepararDocumentoClasificacionActivity")]
    public PrepararDocumentoClasificacionResultado Run([ActivityTrigger] PrepararDocumentoClasificacionInput input)
    {
        var maxPaginas = input.MaxPaginasClasificacion ?? 3;

        _logger.LogInformation(
            "Preparando documento para clasificación: {NombreDocumento} | MaxPaginas={MaxPaginas}",
            input.NombreDocumento,
            maxPaginas);

        var recorte = _pdfRecorteService.RecortarParaClasificacion(input.DocumentoBase64, maxPaginas);

        return new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = recorte.DocumentoBase64Recortado,
            TotalPaginas = recorte.TotalPaginas,
            CharsTextoNativo = recorte.CharsTextoNativo,
            PaginasIncluidas = recorte.PaginasIncluidas,
            RecorteAplicado = recorte.RecorteAplicado
        };
    }
}
