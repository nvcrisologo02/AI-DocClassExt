using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class SubirBlobActivity
{
    private readonly ILogger<SubirBlobActivity> _logger;
    private readonly IBlobStorageService _blobStorageService;

    public SubirBlobActivity(
        ILogger<SubirBlobActivity> logger,
        IBlobStorageService blobStorageService)
    {
        _logger = logger;
        _blobStorageService = blobStorageService;
    }

    [Function("SubirBlobActivity")]
    public async Task<string> Run([ActivityTrigger] SubirBlobInput input)
    {
        try
        {
            // Blob-first: si ya tenemos un BlobPath pre-existente, es un no-op
            if (!string.IsNullOrWhiteSpace(input.BlobPath))
            {
                _logger.LogInformation("Documento ya está en blob, omitiendo subida. BlobPath={BlobPath}", input.BlobPath);
                return input.BlobPath;
            }

            if (string.IsNullOrWhiteSpace(input.ContenidoBase64))
            {
                _logger.LogWarning("No hay contenido para subir a blob. NombreArchivo: {NombreArchivo}", input.NombreArchivo);
                return string.Empty;
            }

            var documentBytes = Convert.FromBase64String(input.ContenidoBase64);

            var blobPath = await _blobStorageService.UploadDocumentAsync(
                documentBytes,
                input.NombreArchivo,
                input.Contenedor);

            _logger.LogInformation("Documento subido a blob storage: {BlobPath}", blobPath);
            return blobPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error subiendo a blob storage, continuando sin almacenamiento");
            return string.Empty;
        }
    }
}
