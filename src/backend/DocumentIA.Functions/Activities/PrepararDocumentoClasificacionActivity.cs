using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class PrepararDocumentoClasificacionActivity
{
    /// <summary>Contenedor de los recortes de clasificación. Lo limpia una política de ciclo de vida (AB#100814).</summary>
    public const string ContenedorRecortes = "documents-clasif";

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

        if (string.IsNullOrWhiteSpace(input.BlobPath))
        {
            throw new InvalidOperationException("La entrada no trae BlobPath: el documento debe estar en blob antes de prepararlo.");
        }

        _logger.LogInformation(
            "Preparando documento para clasificación: {NombreDocumento} | MaxPaginas={MaxPaginas} | BlobPath={BlobPath}",
            input.NombreDocumento,
            maxPaginas,
            input.BlobPath);

        // AB#100814: bytes de principio a fin. Nada de base64 en memoria ni en la salida: la
        // clasificacion recibe una ruta de blob y resuelve el binario por SAS cuando lo necesita.
        var documento = await _blobStorageService.DownloadDocumentAsync(input.BlobPath);
        var recorte = _pdfRecorteService.RecortarParaClasificacion(documento, maxPaginas);

        var blobPathClasificacion = input.BlobPath;
        if (recorte.RecorteAplicado && recorte.PdfRecortado is { Length: > 0 })
        {
            blobPathClasificacion = await _blobStorageService.UploadDocumentAsync(
                recorte.PdfRecortado,
                input.NombreDocumento,
                ContenedorRecortes);

            _logger.LogInformation(
                "Recorte de clasificación subido: {BlobPathClasificacion} ({Bytes} bytes)",
                blobPathClasificacion,
                recorte.PdfRecortado.Length);
        }

        return new PrepararDocumentoClasificacionResultado
        {
            BlobPathClasificacion = blobPathClasificacion,
            TotalPaginas = recorte.TotalPaginas,
            CharsTextoNativo = recorte.CharsTextoNativo,
            PaginasIncluidas = recorte.PaginasIncluidas,
            RecorteAplicado = recorte.RecorteAplicado
        };
    }
}
