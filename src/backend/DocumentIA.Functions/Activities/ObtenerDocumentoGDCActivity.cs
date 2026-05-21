using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace DocumentIA.Functions.Activities;

public class ObtenerDocumentoGDCActivity
{
    private readonly ILogger<ObtenerDocumentoGDCActivity> _logger;
    private readonly IGdcService _gdcService;
    private readonly IBlobStorageService _blobStorageService;

    public ObtenerDocumentoGDCActivity(
        ILogger<ObtenerDocumentoGDCActivity> logger,
        IGdcService gdcService,
        IBlobStorageService blobStorageService)
    {
        _logger = logger;
        _gdcService = gdcService;
        _blobStorageService = blobStorageService;
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

        // Blob-first: subir al blob para que toda la pipeline use BlobPath en lugar de base64
        if (!string.IsNullOrEmpty(result.Base64))
        {
            try
            {
                var bytes = Convert.FromBase64String(result.Base64);

                result.BlobPath = await _blobStorageService.UploadDocumentAsync(
                    bytes, result.NombreArchivo, "documents");

                // Pre-computar hashes aquí para evitar re-descarga en NormalizarActivity
                using var sha256 = SHA256.Create();
                result.PreComputedSHA256 = Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
                using var md5 = MD5.Create();
                result.PreComputedMD5 = Convert.ToHexString(md5.ComputeHash(bytes)).ToLowerInvariant();
                result.PreComputedTamañoBytes = bytes.Length;

                result.Base64 = null!; // limpiar: la pipeline usará BlobPath

                _logger.LogInformation(
                    "Documento GDC subido a blob. BlobPath={BlobPath}, SHA256={SHA256}, Bytes={Bytes}",
                    result.BlobPath, result.PreComputedSHA256, bytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo subir documento GDC a blob, continuando con base64");
            }
        }

        return result;
    }
}
