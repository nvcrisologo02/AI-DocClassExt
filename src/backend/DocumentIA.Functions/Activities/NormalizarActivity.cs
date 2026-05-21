using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using System.Security.Cryptography;

namespace DocumentIA.Functions.Activities;

public class NormalizarActivity
{
    private readonly ILogger<NormalizarActivity> _logger;
    private readonly IBlobStorageService _blobStorageService;

    public NormalizarActivity(
        ILogger<NormalizarActivity> logger,
        IBlobStorageService blobStorageService)
    {
        _logger = logger;
        _blobStorageService = blobStorageService;
    }

    [Function("NormalizarActivity")]
    public async Task<Dictionary<string, object>> Run([ActivityTrigger] ContratoEntrada entrada)
    {
        _logger.LogInformation("Normalizando documento: {Nombre}", entrada.Documento.Name);

        // Blob-first: si los hashes ya están pre-computados en el trigger, usarlos directamente
        // sin descargar el blob (ahorra un round-trip para ficheros grandes)
        if (!string.IsNullOrEmpty(entrada.Documento.PreComputedSHA256))
        {
            _logger.LogInformation(
                "Usando hashes pre-computados en trigger. SHA256={SHA256}, Bytes={Bytes}",
                entrada.Documento.PreComputedSHA256, entrada.Documento.PreComputedTamañoBytes);

            return new Dictionary<string, object>
            {
                ["SHA256"] = entrada.Documento.PreComputedSHA256,
                ["MD5"] = entrada.Documento.PreComputedMD5 ?? string.Empty,
                ["CRC32"] = entrada.Documento.PreComputedCRC32 ?? string.Empty,
                ["TamañoBytes"] = entrada.Documento.PreComputedTamañoBytes,
                ["NombreNormalizado"] = entrada.Documento.Name.Trim().ToLowerInvariant(),
                ["FechaNormalizacion"] = DateTime.UtcNow
            };
        }

        byte[] documentBytes;

        if (!string.IsNullOrEmpty(entrada.Documento.BlobPath))
        {
            // BlobPath set pero sin pre-computed (ej. flujo GDC tras ObtenerDocumentoGDCActivity)
            _logger.LogInformation("Descargando documento desde blob para normalización. BlobPath={BlobPath}",
                entrada.Documento.BlobPath);
            documentBytes = await _blobStorageService.DownloadDocumentAsync(entrada.Documento.BlobPath);
        }
        else
        {
            // Fallback: flujo legado con base64 (compatibilidad hacia atrás)
            documentBytes = Convert.FromBase64String(entrada.Documento.Content.Base64);
        }

        var sha256 = CalcularSHA256(documentBytes);
        var md5 = CalcularMD5(documentBytes);
        var crc32 = CalcularCRC32(documentBytes);

        _logger.LogInformation("Normalización completada. SHA256={SHA256}, MD5={MD5}, Bytes={Bytes}",
            sha256, md5, documentBytes.Length);

        return new Dictionary<string, object>
        {
            ["SHA256"] = sha256,
            ["MD5"] = md5,
            ["CRC32"] = crc32,
            ["TamañoBytes"] = documentBytes.Length,
            ["NombreNormalizado"] = entrada.Documento.Name.Trim().ToLowerInvariant(),
            ["FechaNormalizacion"] = DateTime.UtcNow
        };
    }

    private static string CalcularSHA256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string CalcularMD5(byte[] data)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string CalcularCRC32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ (0xEDB88320 & ~((crc & 1) - 1));
            }
        }
        return (~crc).ToString("X8");
    }
}

