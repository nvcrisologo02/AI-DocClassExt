using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

public class AzureDocumentIntelligenceClasificarProvider : IClasificarDataProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClassificationModelRegistryLoader _modelRegistryLoader;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<AzureDocumentIntelligenceClasificarProvider> _logger;

    public AzureDocumentIntelligenceClasificarProvider(
        IHttpClientFactory httpClientFactory,
        ClassificationModelRegistryLoader modelRegistryLoader,
        IBlobStorageService blobStorageService,
        ILogger<AzureDocumentIntelligenceClasificarProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _modelRegistryLoader = modelRegistryLoader;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<ResultadoClasificacion> ClasificarAsync(ClasificacionInput input, CancellationToken cancellationToken = default)
    {
        var requestedModel = input.Entrada.Instrucciones.Classification.Model;
        var model = ResolveModel(requestedModel);
        var apiVersion = string.IsNullOrWhiteSpace(model.ApiVersion) ? "2024-11-30" : model.ApiVersion;

        var baseEndpoint = model.Endpoint.TrimEnd('/');
        var analyzeUrl = $"{baseEndpoint}/documentintelligence/documentClassifiers/{Uri.EscapeDataString(model.ClassifierId)}:analyze?_overload=classifyDocument&api-version={Uri.EscapeDataString(apiVersion)}";
        // Blob-first: si no hay override y hay BlobPath → usar urlSource (Azure DI descarga directo del blob)
        // Si el SAS apunta a loopback (Azulite/local), usar base64Source porque DI no puede descargar desde localhost.
        object requestBodyObj;
        var usingUrlSource = false;
        var blobPath = input.Entrada.Documento.BlobPath;
        if (string.IsNullOrWhiteSpace(input.DocumentoBase64Override) && !string.IsNullOrWhiteSpace(blobPath))
        {
            var sasUrl = await _blobStorageService.GenerateSasUrlAsync(blobPath, TimeSpan.FromMinutes(30));
            if (IsLoopbackUrl(sasUrl))
            {
                var bytes = await _blobStorageService.DownloadDocumentAsync(blobPath);
                requestBodyObj = new { base64Source = Convert.ToBase64String(bytes) };
                _logger.LogWarning("ClasificarProvider detectó SAS local/loopback. Se usa base64Source para BlobPath={BlobPath}", blobPath);
            }
            else
            {
                requestBodyObj = new { urlSource = sasUrl };
                usingUrlSource = true;
                _logger.LogInformation("ClasificarProvider usando urlSource (SAS) para BlobPath={BlobPath}", blobPath);
            }
        }
        else
        {
            var documentoBase64 = !string.IsNullOrWhiteSpace(input.DocumentoBase64Override)
                ? input.DocumentoBase64Override
                : input.Entrada.Documento.Content.Base64;
            requestBodyObj = new { base64Source = documentoBase64 };
        }

        var requestBody = JsonSerializer.Serialize(requestBodyObj);

        using var client = _httpClientFactory.CreateClient();
        var startResponse = await SendAnalyzeRequestAsync(client, analyzeUrl, requestBody, model, cancellationToken);
        if (!startResponse.IsSuccessStatusCode)
        {
            var body = await startResponse.Content.ReadAsStringAsync(cancellationToken);

            if (usingUrlSource
                && !string.IsNullOrWhiteSpace(blobPath)
                && (int)startResponse.StatusCode == 400
                && body.Contains("InvalidContent", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("DI respondió InvalidContent con urlSource para BlobPath={BlobPath}. Reintentando con base64Source.", blobPath);

                var bytes = await _blobStorageService.DownloadDocumentAsync(blobPath);
                requestBody = JsonSerializer.Serialize(new { base64Source = Convert.ToBase64String(bytes) });
                startResponse.Dispose();
                startResponse = await SendAnalyzeRequestAsync(client, analyzeUrl, requestBody, model, cancellationToken);

                if (!startResponse.IsSuccessStatusCode)
                {
                    body = await startResponse.Content.ReadAsStringAsync(cancellationToken);
                    throw new InvalidOperationException($"Error iniciando clasificación DI. Status={(int)startResponse.StatusCode}. Body={body}");
                }
            }
            else
            {
                throw new InvalidOperationException($"Error iniciando clasificación DI. Status={(int)startResponse.StatusCode}. Body={body}");
            }
        }

        var operationLocation = startResponse.Headers.TryGetValues("operation-location", out var values)
            ? values.FirstOrDefault()
            : null;

        if (string.IsNullOrWhiteSpace(operationLocation))
        {
            throw new InvalidOperationException("La respuesta de Azure DI no devolvió operation-location");
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, model.TimeoutSeconds));
        JsonDocument? finalResult = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(Math.Max(250, model.PollIntervalMs), cancellationToken);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            await DocumentIntelligenceAuthHelper.ApplyAuthAsync(pollRequest, model.AuthMode, model.ApiKey, cancellationToken);
            pollRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var pollResponse = await client.SendAsync(pollRequest, cancellationToken);
            var pollBody = await pollResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!pollResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Error consultando operación de clasificación DI. Status={(int)pollResponse.StatusCode}. Body={pollBody}");
            }

            var pollJson = JsonDocument.Parse(pollBody);
            var status = pollJson.RootElement.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString() ?? string.Empty
                : string.Empty;

            if (status.Equals("succeeded", StringComparison.OrdinalIgnoreCase))
            {
                finalResult = JsonDocument.Parse(pollBody);
                break;
            }

            if (status.Equals("failed", StringComparison.OrdinalIgnoreCase) || status.Equals("canceled", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"La clasificación DI terminó en estado '{status}'. Respuesta={pollBody}");
            }
        }

        if (finalResult is null)
        {
            throw new TimeoutException("Timeout esperando resultado de clasificación en Azure DI");
        }

        using (finalResult)
        {
            var analyzeResult = finalResult.RootElement.TryGetProperty("analyzeResult", out var analyzeResultEl)
                ? analyzeResultEl
                : finalResult.RootElement;

            string? detectedType = null;
            double confidence = 0.0;

            if (analyzeResult.TryGetProperty("documents", out var documentsEl) &&
                documentsEl.ValueKind == JsonValueKind.Array &&
                documentsEl.GetArrayLength() > 0)
            {
                var firstDoc = documentsEl[0];
                if (firstDoc.TryGetProperty("docType", out var docTypeEl))
                {
                    detectedType = docTypeEl.GetString();
                }

                if (firstDoc.TryGetProperty("confidence", out var confidenceEl) && confidenceEl.TryGetDouble(out var parsedConfidence))
                {
                    confidence = parsedConfidence;
                }
            }

            string? contentExtraido = null;
            if (analyzeResult.TryGetProperty("content", out var contentEl)
                && contentEl.ValueKind == JsonValueKind.String)
            {
                var text = contentEl.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    contentExtraido = text;
                }
            }

            _logger.LogInformation(
                "Clasificación Azure DI completada. modelKey={ModelKey}, classifierId={ClassifierId}, detectedType={DetectedType}, confidence={Confidence}, contentLength={ContentLength}",
                model.Key,
                model.ClassifierId,
                detectedType,
                confidence,
                contentExtraido?.Length ?? 0);

            return new ResultadoClasificacion
            {
                Modelo = model.ClassifierId,
                Confianza = confidence,
                ConfianzaDI = confidence,
                ProveedorClasif = "DocumentIntelligence",
                FallbackLLM = false,
                TipologiaDetectada = detectedType,
                ContentExtraido = contentExtraido
            };
        }
    }

    private ClassificationModelConfig ResolveModel(string requestedModel)
    {
        if (string.IsNullOrWhiteSpace(requestedModel) || string.Equals(requestedModel, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateModel(_modelRegistryLoader.GetDefaultModel("azure-document-intelligence"));
        }

        return ValidateModel(_modelRegistryLoader.GetModel(requestedModel));
    }

    private static ClassificationModelConfig ValidateModel(ClassificationModelConfig model)
    {
        if (!IsAzureDiProvider(model.Provider))
        {
            throw new InvalidOperationException(
                $"El modelo de clasificación '{model.Key}' no es compatible con Azure Document Intelligence. Provider actual: '{model.Provider}'.");
        }

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            throw new InvalidOperationException(
                $"ClassificationModelConfig.Endpoint es obligatorio para el modelo '{model.Key}'.");
        }

        if (string.IsNullOrWhiteSpace(model.ClassifierId))
        {
            throw new InvalidOperationException(
                $"ClassificationModelConfig.ClassifierId es obligatorio para el modelo '{model.Key}'.");
        }

        if (!string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(model.ApiKey))
        {
            throw new InvalidOperationException(
                $"ClassificationModelConfig.ApiKey es obligatorio para el modelo '{model.Key}' cuando AuthMode=ApiKey.");
        }

        return model;
    }

    private static bool IsAzureDiProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-document-intelligence" or "azure-di" or "di";

    private static bool IsLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback ||
               string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> SendAnalyzeRequestAsync(
        HttpClient client,
        string analyzeUrl,
        string requestBody,
        ClassificationModelConfig model,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, analyzeUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        await DocumentIntelligenceAuthHelper.ApplyAuthAsync(request, model.AuthMode, model.ApiKey, cancellationToken);
        return await client.SendAsync(request, cancellationToken);
    }
}
