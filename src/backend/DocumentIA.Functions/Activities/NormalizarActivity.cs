using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using System.Security.Cryptography;

namespace DocumentIA.Functions.Activities;

public class NormalizarActivity
{
    private readonly ILogger<NormalizarActivity> _logger;

    public NormalizarActivity(ILogger<NormalizarActivity> logger)
    {
        _logger = logger;
    }

    [Function("NormalizarActivity")]
    public Dictionary<string, object> Run([ActivityTrigger] ContratoEntrada entrada)
    {
        _logger.LogInformation($"Normalizando documento: {entrada.Documento.Name}");

        // Decodificar Base64
        var documentBytes = Convert.FromBase64String(entrada.Documento.Content.Base64);

        // Calcular SHA256
        var sha256 = CalcularSHA256(documentBytes);

        // Calcular CRC32
        var crc32 = CalcularCRC32(documentBytes);

        var resultado = new Dictionary<string, object>
        {
            ["SHA256"] = sha256,
            ["CRC32"] = crc32,
            ["TamañoBytes"] = documentBytes.Length,
            ["NombreNormalizado"] = entrada.Documento.Name.Trim().ToLowerInvariant(),
            ["FechaNormalizacion"] = DateTime.UtcNow
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
