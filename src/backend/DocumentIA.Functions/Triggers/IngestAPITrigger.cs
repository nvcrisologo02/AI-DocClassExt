using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using System.Net;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Net.Http.Headers;
using System.Security.Cryptography;

namespace DocumentIA.Functions.Triggers;

public class IngestAPITrigger
{
    private readonly ILogger<IngestAPITrigger> _logger;
    private readonly PromptInstruccionesValidator _promptInstruccionesValidator;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ClassificationRoutingSettings _classificationRoutingSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IngestAPITrigger(
        ILogger<IngestAPITrigger> logger,
        PromptInstruccionesValidator promptInstruccionesValidator,
        IBlobStorageService blobStorageService,
        IOptions<ClassificationRoutingSettings> classificationRoutingOptions)
    {
        _logger = logger;
        _promptInstruccionesValidator = promptInstruccionesValidator;
        _blobStorageService = blobStorageService;
        _classificationRoutingSettings = classificationRoutingOptions.Value;
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
                ? ctValues.FirstOrDefault() ?? string.Empty
                : string.Empty;

            if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                contratoEntrada = await ParseMultipartAndUploadAsync(req, contentType);
                if (contratoEntrada is null)
                {
                    var badMultipartResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badMultipartResponse.WriteStringAsync("Multipart inválido: se requieren partes 'file' y 'metadata'.");
                    return badMultipartResponse;
                }
            }
            else
            {
                var requestBody = await req.ReadAsStringAsync();
                try
                {
                    contratoEntrada = JsonSerializer.Deserialize<ContratoEntrada>(requestBody!, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "JSON de entrada mal formado.");
                    var badJsonResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badJsonResponse.WriteStringAsync("JSON mal formado.");
                    return badJsonResponse;
                }

                if (contratoEntrada is not null)
                {
                    var base64FromJson = contratoEntrada.Documento?.Content?.Base64?.Trim();
                    if (!string.IsNullOrEmpty(base64FromJson))
                    {
                        var fileBytes = Convert.FromBase64String(base64FromJson);
                        await UploadToBlobAndSetHashesAsync(contratoEntrada, fileBytes);
                    }
                }
            }

            if (contratoEntrada == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Contrato de entrada inválido");
                return badResponse;
            }

            contratoEntrada.Instrucciones ??= new Instrucciones();
            contratoEntrada.Instrucciones.Classification ??= new ConfiguracionIA();

            if (!ClassificationLevelResolver.TryResolve(
                null,
                _classificationRoutingSettings.NivelClasificacionDefault,
                out _,
                out var defaultLevelError))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Configuración de clasificación inválida: {defaultLevelError}");
                return errorResponse;
            }

            if (!ClassificationLevelResolver.TryResolve(
                contratoEntrada.Instrucciones.Classification.NivelClasificacion,
                _classificationRoutingSettings.NivelClasificacionDefault,
                out _,
                out var levelValidationError))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync(levelValidationError ?? "instrucciones.classification.nivelClasificacion inválido.");
                return badResponse;
            }

            // Capturar si el caller informó nivelClasificacion explícitamente (antes de normalizar con default)
            var nivelClasificacionExplicito = !string.IsNullOrWhiteSpace(
                contratoEntrada.Instrucciones.Classification.NivelClasificacion);

            ClassificationLevelResolver.ApplyTo(
                contratoEntrada.Instrucciones.Classification,
                _classificationRoutingSettings.NivelClasificacionDefault);

            // Regla de control backend: en nivel TDN1, el flujo siempre es de solo clasificación.
            if (string.Equals(
                contratoEntrada.Instrucciones.Classification.NivelClasificacion,
                ClassificationLevelResolver.LevelTdn1,
                StringComparison.OrdinalIgnoreCase))
            {
                contratoEntrada.Instrucciones.ClassificationOnly = true;
            }

            // D2: si el caller especifica nivelClasificacion, forzar provider=gpt (único que lo interpreta)
            if (nivelClasificacionExplicito)
            {
                contratoEntrada.Instrucciones.Classification.Provider = "gpt";
            }

            if (!_promptInstruccionesValidator.TryValidate(contratoEntrada.Instrucciones.Prompt, out var promptValidationError))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync(promptValidationError ?? "instrucciones.prompt inválido.");
                return badResponse;
            }

            if (contratoEntrada.Instrucciones.ClassificationOnly &&
                !string.IsNullOrWhiteSpace(contratoEntrada.Instrucciones.ExpectedType))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("classificationOnly=true es incompatible con expectedType informado.");
                return badResponse;
            }

            if (contratoEntrada.Instrucciones.ClassificationOnly && contratoEntrada.Instrucciones.MaxPagesForClassificationOnly < 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("instrucciones.maxPagesForClassificationOnly debe ser 0 o mayor.");
                return badResponse;
            }

            if (contratoEntrada.Documento == null || contratoEntrada.Documento.Content == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("documento/content inválido.");
                return badResponse;
            }

            var objectIdGdc = contratoEntrada.Documento.ObjectIdGDC?.Trim();
            var base64 = contratoEntrada.Documento.Content.Base64?.Trim();
            var blobPath = contratoEntrada.Documento.BlobPath?.Trim();
            var hasObjectIdGdc = !string.IsNullOrWhiteSpace(objectIdGdc);
            var hasBase64 = !string.IsNullOrWhiteSpace(base64);
            var hasBlobPath = !string.IsNullOrWhiteSpace(blobPath);

            if (hasObjectIdGdc && (hasBase64 || hasBlobPath))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("No se puede enviar Documento.ObjectIdGDC junto con contenido del documento (Base64 o BlobPath).");
                return badResponse;
            }

            if (!hasObjectIdGdc && !hasBase64 && !hasBlobPath)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Debe proporcionarse Documento.Content.Base64, Documento.BlobPath o Documento.ObjectIdGDC.");
                return badResponse;
            }

            if (hasObjectIdGdc)
            {
                contratoEntrada.Documento.ObjectIdGDC = objectIdGdc;
                contratoEntrada.Instrucciones.SkipGDCUpload = true;
            }

            // Iniciar orquestación
            // Capturar el operation_Id de App Insights (W3C TraceId) en el contexto del trigger HTTP
            contratoEntrada.Trazabilidad.OperationId = Activity.Current?.TraceId.ToString();

            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                "DocumentProcessOrchestrator",
                contratoEntrada);

            _logger.LogInformation($"Orquestación iniciada con ID: {instanceId}");

            // Responder con el ID de instancia
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                instanceId,
                statusQueryUri = $"{req.Url.Scheme}://{req.Url.Authority}/runtime/webhooks/durabletask/instances/{instanceId}",
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

    private async Task<ContratoEntrada?> ParseMultipartAndUploadAsync(HttpRequestData req, string contentType)
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
            {
                continue;
            }

            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;

            if (string.Equals(name, "file", StringComparison.OrdinalIgnoreCase))
            {
                var foundName = HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
                if (!string.IsNullOrWhiteSpace(foundName))
                {
                    fileName = foundName;
                }

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

        if (fileBytes is null || metadataJson is null)
        {
            _logger.LogWarning("Multipart incompleto. file={HasFile}, metadata={HasMeta}", fileBytes is not null, metadataJson is not null);
            return null;
        }

        var entrada = JsonSerializer.Deserialize<ContratoEntrada>(metadataJson, JsonOptions);
        if (entrada is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(entrada.Documento.Name))
        {
            entrada.Documento.Name = fileName;
        }

        await UploadToBlobAndSetHashesAsync(entrada, fileBytes);
        return entrada;
    }

    private async Task UploadToBlobAndSetHashesAsync(ContratoEntrada entrada, byte[] fileBytes)
    {
        var sha256 = ComputeSHA256(fileBytes);
        var md5 = ComputeMD5(fileBytes);
        var crc32 = ComputeCRC32(fileBytes);

        var blobPath = await _blobStorageService.UploadDocumentAsync(fileBytes, entrada.Documento.Name, "documents");

        entrada.Documento.BlobPath = blobPath;
        entrada.Documento.PreComputedSHA256 = sha256;
        entrada.Documento.PreComputedMD5 = md5;
        entrada.Documento.PreComputedCRC32 = crc32;
        entrada.Documento.PreComputedTamañoBytes = fileBytes.Length;
        entrada.Documento.Content.Base64 = null!;

        _logger.LogInformation(
            "Fichero subido a blob antes de orquestación. BlobPath={BlobPath}, SHA256={SHA256}, Bytes={Bytes}",
            blobPath,
            sha256,
            fileBytes.Length);
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
            {
                crc = (crc >> 1) ^ (0xEDB88320u & ~((crc & 1) - 1));
            }
        }

        return (~crc).ToString("X8");
    }
}
