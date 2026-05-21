using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using System.Net;
using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;

namespace DocumentIA.Functions.Triggers;

public class IngestAPITrigger
{
    private readonly ILogger<IngestAPITrigger> _logger;
    private readonly PromptInstruccionesValidator _promptInstruccionesValidator;
    private readonly IBlobStorageService _blobStorageService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IngestAPITrigger(
        ILogger<IngestAPITrigger> logger,
        PromptInstruccionesValidator promptInstruccionesValidator,
        IBlobStorageService blobStorageService)
    {
        _logger = logger;
        _promptInstruccionesValidator = promptInstruccionesValidator;
        _blobStorageService = blobStorageService;
    }

    [Function("IngestDocument")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        _logger.LogInformation("Recibiendo documento para procesamiento");

        try
        {
            ContratoEntrada? contratoEntrada;

            var contentType = req.Headers.TryGetValues("Content-Type", out var ctValues)
                ? ctValues.FirstOrDefault() ?? "" : "";

            if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                // Multipart: el fichero llega como bytes binarios (sin overhead base64)
                contratoEntrada = await ParseMultipartAndUploadAsync(req, contentType);
                if (contratoEntrada == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Multipart inválido: se requieren partes 'file' y 'metadata'.");
                    return bad;
                }
            }
            else
            {
                // application/json — comportamiento compatible con versiones anteriores
                var requestBody = await req.ReadAsStringAsync();
                contratoEntrada = JsonSerializer.Deserialize<ContratoEntrada>(requestBody!, JsonOptions);

                if (contratoEntrada == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Contrato de entrada inválido");
                    return bad;
                }

                // Blob-first: si hay base64 → subir al blob antes de iniciar la orquestación
                // Así la pipeline Durable nunca recibe el payload grande
                var base64 = contratoEntrada.Documento.Content.Base64?.Trim();
                if (!string.IsNullOrEmpty(base64))
                {
                    var fileBytes = Convert.FromBase64String(base64);
                    await UploadToBlobAndSetHashesAsync(contratoEntrada, fileBytes);
                }
            }

            // --- Validaciones comunes ---
            if (!_promptInstruccionesValidator.TryValidate(contratoEntrada.Instrucciones.Prompt, out var promptErr))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync(promptErr ?? "instrucciones.prompt inválido.");
                return bad;
            }

            if (contratoEntrada.Instrucciones.ClassificationOnly &&
                !string.IsNullOrWhiteSpace(contratoEntrada.Instrucciones.ExpectedType))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("classificationOnly=true es incompatible con expectedType informado.");
                return bad;
            }

            if (contratoEntrada.Instrucciones.MaxPagesForClassificationOnly < 0)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("instrucciones.maxPagesForClassificationOnly debe ser 0 o mayor.");
                return bad;
            }

            var objectIdGdc = contratoEntrada.Documento.ObjectIdGDC?.Trim();
            var blobPath = contratoEntrada.Documento.BlobPath;
            var hasObjectIdGdc = !string.IsNullOrWhiteSpace(objectIdGdc);
            var hasBlobPath = !string.IsNullOrWhiteSpace(blobPath);

            if (hasObjectIdGdc && hasBlobPath)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("No se puede enviar ObjectIdGDC y BlobPath simultáneamente.");
                return bad;
            }

            if (!hasObjectIdGdc && !hasBlobPath)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync(
                    "Debe proporcionarse el fichero (multipart o JSON base64), Documento.ObjectIdGDC o Documento.BlobPath.");
                return bad;
            }

            if (hasObjectIdGdc)
            {
                contratoEntrada.Documento.ObjectIdGDC = objectIdGdc;
                contratoEntrada.Instrucciones.SkipGDCUpload = true;
            }

            // Capturar el operation_Id de App Insights (W3C TraceId)
            contratoEntrada.Trazabilidad.OperationId = Activity.Current?.TraceId.ToString();

            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                "DocumentProcessOrchestrator",
                contratoEntrada);

            _logger.LogInformation(
                "Orquestación iniciada. InstanceId={InstanceId}, BlobPath={BlobPath}, CorrelationId={CorrelationId}",
                instanceId, blobPath ?? "(GDC)", contratoEntrada.Trazabilidad.CorrelationId);

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                instanceId,
                statusQueryUri = $"{req.Url.Scheme}://{req.Url.Host}/runtime/webhooks/durabletask/instances/{instanceId}",
                correlationId = contratoEntrada.Trazabilidad.CorrelationId
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando solicitud");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Parsea un body multipart/form-data esperando dos partes:
    ///   - "file":     bytes binarios del documento (sin base64)
    ///   - "metadata": JSON con ContratoEntrada (sin campo content.base64)
    /// Sube el fichero al blob antes de retornar para que la orquestación reciba solo el BlobPath.
    /// </summary>
    private async Task<ContratoEntrada?> ParseMultipartAndUploadAsync(
        HttpRequestData req, string contentType)
    {
        var boundary = contentType
            .Split(';')
            .Select(p => p.Trim())
            .Where(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
            .Select(p => p["boundary=".Length..].Trim('"'))
            .FirstOrDefault();

        if (string.IsNullOrEmpty(boundary))
        {
            _logger.LogWarning("Content-Type multipart/form-data sin boundary");
            return null;
        }

        var reader = new MultipartReader(boundary, req.Body);
        byte[]? fileBytes = null;
        string fileName = "document.pdf";
        string? metadataJson = null;

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync()) != null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
                continue;

            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;

            if (string.Equals(name, "file", StringComparison.OrdinalIgnoreCase))
            {
                var fn = HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
                if (!string.IsNullOrEmpty(fn)) fileName = fn;

                using var ms = new MemoryStream();
                await section.Body.CopyToAsync(ms);
                fileBytes = ms.ToArray();
                _logger.LogInformation("Parte 'file' recibida: {FileName}, {Bytes} bytes", fileName, fileBytes.Length);
            }
            else if (string.Equals(name, "metadata", StringComparison.OrdinalIgnoreCase))
            {
                using var sr = new StreamReader(section.Body);
                metadataJson = await sr.ReadToEndAsync();
            }
        }

        if (fileBytes == null || metadataJson == null)
        {
            _logger.LogWarning("Multipart incompleto. file={HasFile}, metadata={HasMeta}",
                fileBytes != null, metadataJson != null);
            return null;
        }

        var entrada = JsonSerializer.Deserialize<ContratoEntrada>(metadataJson, JsonOptions);
        if (entrada == null) return null;

        if (string.IsNullOrEmpty(entrada.Documento.Name))
            entrada.Documento.Name = fileName;

        await UploadToBlobAndSetHashesAsync(entrada, fileBytes);
        return entrada;
    }

    /// <summary>
    /// Sube los bytes al blob, calcula SHA256/MD5/CRC32 y los asigna como campos pre-computados.
    /// Limpia Content.Base64 para que la orquestación no reciba el payload grande.
    /// </summary>
    private async Task UploadToBlobAndSetHashesAsync(ContratoEntrada entrada, byte[] fileBytes)
    {
        var sha256 = ComputeSHA256(fileBytes);
        var md5 = ComputeMD5(fileBytes);
        var crc32 = ComputeCRC32(fileBytes);

        var blobPath = await _blobStorageService.UploadDocumentAsync(
            fileBytes, entrada.Documento.Name, "documents");

        entrada.Documento.BlobPath = blobPath;
        entrada.Documento.PreComputedSHA256 = sha256;
        entrada.Documento.PreComputedMD5 = md5;
        entrada.Documento.PreComputedCRC32 = crc32;
        entrada.Documento.PreComputedTamañoBytes = fileBytes.Length;
        entrada.Documento.Content.Base64 = null!; // limpiar para no serializar en la orquestación

        _logger.LogInformation(
            "Fichero subido a blob antes de orquestación. BlobPath={BlobPath}, SHA256={SHA256}, Bytes={Bytes}",
            blobPath, sha256, fileBytes.Length);
    }

    private static string ComputeSHA256(byte[] data)
    {
        using var alg = SHA256.Create();
        return Convert.ToHexString(alg.ComputeHash(data)).ToLowerInvariant();
    }

    private static string ComputeMD5(byte[] data)
    {
        using var alg = MD5.Create();
        return Convert.ToHexString(alg.ComputeHash(data)).ToLowerInvariant();
    }

    private static string ComputeCRC32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320u & ~((crc & 1) - 1));
        }
        return (~crc).ToString("X8");
    }
}

