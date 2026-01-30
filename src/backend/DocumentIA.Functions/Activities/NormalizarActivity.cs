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
        _logger.LogInformation($"Normalizando documento: {entrada.Documento.Name}");

        // Decodificar Base64
        var documentBytes = Convert.FromBase64String(entrada.Documento.Content.Base64);

        // Calcular SHA256
        var sha256 = CalcularSHA256(documentBytes);

        // Calcular CRC32
        var crc32 = CalcularCRC32(documentBytes);

        // Subir a Blob Storage
        string blobPath;
        try
        {
            blobPath = await _blobStorageService.UploadDocumentAsync(
                documentBytes, 
                entrada.Documento.Name, 
                "documents");
            
            _logger.LogInformation($"Documento subido a blob storage: {blobPath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error subiendo a blob storage, continuando sin almacenamiento");
            blobPath = string.Empty;
        }

        var resultado = new Dictionary<string, object>
        {
            ["SHA256"] = sha256,
            ["CRC32"] = crc32,
            ["TamañoBytes"] = documentBytes.Length,
            ["NombreNormalizado"] = entrada.Documento.Name.Trim().ToLowerInvariant(),
            ["FechaNormalizacion"] = DateTime.UtcNow,
            ["DocumentoBytes"] = documentBytes,
            ["BlobPath"] = blobPath
        };

        _logger.LogInformation($"Normalización completada. SHA256: {sha256}");
        return resultado;
    }

    private static string CalcularSHA256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
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
