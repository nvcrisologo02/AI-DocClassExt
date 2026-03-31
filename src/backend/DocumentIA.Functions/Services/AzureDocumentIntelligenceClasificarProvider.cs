using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

public class AzureDocumentIntelligenceClasificarProvider : IClasificarDataProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClassificationModelRegistryLoader _modelRegistryLoader;
    private readonly ClassificationRoutingSettings _routingSettings;
    private readonly AzureDocumentIntelligenceClassificationSettings _settings;
    private readonly ILogger<AzureDocumentIntelligenceClasificarProvider> _logger;

    public AzureDocumentIntelligenceClasificarProvider(
        IHttpClientFactory httpClientFactory,
        ClassificationModelRegistryLoader modelRegistryLoader,
        IOptions<ClassificationRoutingSettings> routingSettings,
        IOptions<AzureDocumentIntelligenceClassificationSettings> settings,
        ILogger<AzureDocumentIntelligenceClasificarProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _modelRegistryLoader = modelRegistryLoader;
        _routingSettings = routingSettings.Value;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ResultadoClasificacion> ClasificarAsync(ClasificacionInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            throw new InvalidOperationException("Classification:AzureDocumentIntelligence:Endpoint es obligatorio");
        }

        if (_settings.AuthMode != "DefaultAzureCredential" && string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Classification:AzureDocumentIntelligence:ApiKey es obligatorio cuando AuthMode=ApiKey");
        }

        var requestedModel = input.Entrada.Instrucciones.Classification.Model;
        var modelKey = ResolveModelKey(requestedModel);
        var model = _modelRegistryLoader.GetModel(modelKey);
        var apiVersion = string.IsNullOrWhiteSpace(model.ApiVersion) ? _settings.ApiVersion : model.ApiVersion;

        var baseEndpoint = _settings.Endpoint.TrimEnd('/');
        var analyzeUrl = $"{baseEndpoint}/documentintelligence/documentClassifiers/{Uri.EscapeDataString(model.ClassifierId)}:analyze?_overload=classifyDocument&api-version={Uri.EscapeDataString(apiVersion)}";

        var requestBody = JsonSerializer.Serialize(new
        {
            base64Source = input.Entrada.Documento.Content.Base64
        });

        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, analyzeUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        await DocumentIntelligenceAuthHelper.ApplyAuthAsync(request, _settings, cancellationToken);

        using var startResponse = await client.SendAsync(request, cancellationToken);
        if (!startResponse.IsSuccessStatusCode)
        {
            var body = await startResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Error iniciando clasificación DI. Status={(int)startResponse.StatusCode}. Body={body}");
        }

        var operationLocation = startResponse.Headers.TryGetValues("operation-location", out var values)
            ? values.FirstOrDefault()
            : null;

        if (string.IsNullOrWhiteSpace(operationLocation))
        {
            throw new InvalidOperationException("La respuesta de Azure DI no devolvió operation-location");
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, _settings.TimeoutSeconds));
        JsonDocument? finalResult = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(Math.Max(250, _settings.PollIntervalMs), cancellationToken);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            await DocumentIntelligenceAuthHelper.ApplyAuthAsync(pollRequest, _settings, cancellationToken);
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
                modelKey,
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

    private string ResolveModelKey(string requestedModel)
    {
        if (string.IsNullOrWhiteSpace(requestedModel) || string.Equals(requestedModel, "auto", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_routingSettings.DefaultModelKey))
            {
                throw new InvalidOperationException("Classification:DefaultModelKey es obligatorio cuando Classification.Model=auto");
            }

            return _routingSettings.DefaultModelKey;
        }

        return requestedModel;
    }
}
