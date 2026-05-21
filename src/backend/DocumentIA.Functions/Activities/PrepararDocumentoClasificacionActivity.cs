using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class PrepararDocumentoClasificacionActivity
{
    private readonly PdfRecorteService _pdfRecorteService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<PrepararDocumentoClasificacionActivity> _logger;

    public PrepararDocumentoClasificacionActivity(
        PdfRecorteService pdfRecorteService,
        IBlobStorageService blobStorageService,
        ILogger<PrepararDocumentoClasificacionActivity> logger)
    {
        _pdfRecorteService = pdfRecorteService;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    [Function("PrepararDocumentoClasificacionActivity")]
    public async Task<PrepararDocumentoClasificacionResultado> Run([ActivityTrigger] PrepararDocumentoClasificacionInput input)
    {
        var maxPaginas = input.MaxPaginasClasificacion ?? 3;

        _logger.LogInformation(
            "Preparando documento para clasificación: {NombreDocumento} | MaxPaginas={MaxPaginas} | BlobPath={BlobPath}",
            input.NombreDocumento,
            maxPaginas,
            input.BlobPath ?? "(ninguno)");

        string documentoBase64;

        if (!string.IsNullOrEmpty(input.BlobPath))
        {
            // Blob-first: descargar desde blob y convertir a base64 para PdfRecorteService
            var bytes = await _blobStorageService.DownloadDocumentAsync(input.BlobPath);
            documentoBase64 = Convert.ToBase64String(bytes);
        }
        else
        {
            documentoBase64 = input.DocumentoBase64;
        }

        var recorte = _pdfRecorteService.RecortarParaClasificacion(documentoBase64, maxPaginas);

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
