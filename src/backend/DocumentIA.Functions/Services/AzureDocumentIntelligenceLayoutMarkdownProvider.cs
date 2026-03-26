using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocumentIA.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

public class AzureDocumentIntelligenceLayoutMarkdownProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureDocumentIntelligenceClassificationSettings _settings;
    private readonly ILogger<AzureDocumentIntelligenceLayoutMarkdownProvider> _logger;

    public AzureDocumentIntelligenceLayoutMarkdownProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureDocumentIntelligenceClassificationSettings> settings,
        ILogger<AzureDocumentIntelligenceLayoutMarkdownProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ExtraerMarkdownLayoutResultado> ExtraerMarkdownAsync(
        ExtraerMarkdownLayoutInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            throw new InvalidOperationException("Classification:AzureDocumentIntelligence:Endpoint es obligatorio");
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Classification:AzureDocumentIntelligence:ApiKey es obligatorio");
        }

        var apiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? "2024-11-30" : _settings.ApiVersion;
        var baseEndpoint = _settings.Endpoint.TrimEnd('/');
        var analyzeUrl =
            $"{baseEndpoint}/documentintelligence/documentModels/prebuilt-layout:analyze?outputContentFormat=markdown&api-version={Uri.EscapeDataString(apiVersion)}";

        var requestBody = JsonSerializer.Serialize(new
        {
            base64Source = input.DocumentoBase64
        });

        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, analyzeUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var startResponse = await client.SendAsync(request, cancellationToken);
        if (!startResponse.IsSuccessStatusCode)
        {
            var body = await startResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Error iniciando DI layout. Status={(int)startResponse.StatusCode}. Body={body}");
        }

        var operationLocation = startResponse.Headers.TryGetValues("operation-location", out var values)
            ? values.FirstOrDefault()
            : null;

        if (string.IsNullOrWhiteSpace(operationLocation))
        {
            throw new InvalidOperationException("La respuesta de DI layout no devolvió operation-location");
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, _settings.TimeoutSeconds));
        JsonDocument? finalResult = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(Math.Max(250, _settings.PollIntervalMs), cancellationToken);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            pollRequest.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
            pollRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var pollResponse = await client.SendAsync(pollRequest, cancellationToken);
            var pollBody = await pollResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!pollResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Error consultando DI layout. Status={(int)pollResponse.StatusCode}. Body={pollBody}");
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
                throw new InvalidOperationException($"DI layout terminó en estado '{status}'. Respuesta={pollBody}");
            }
        }

        if (finalResult is null)
        {
            throw new TimeoutException("Timeout esperando resultado de DI layout");
        }

        using (finalResult)
        {
            var analyzeResult = finalResult.RootElement.TryGetProperty("analyzeResult", out var analyzeResultEl)
                ? analyzeResultEl
                : finalResult.RootElement;

            string? markdown = null;
            if (analyzeResult.TryGetProperty("content", out var contentEl)
                && contentEl.ValueKind == JsonValueKind.String)
            {
                var content = contentEl.GetString();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    markdown = content;
                }
            }

            var paginas = 0;
            if (analyzeResult.TryGetProperty("pages", out var pagesEl)
                && pagesEl.ValueKind == JsonValueKind.Array)
            {
                paginas = pagesEl.GetArrayLength();
            }

            _logger.LogInformation(
                "DI layout markdown completado para tipología {Tipologia}. Longitud={Length}, Páginas={Paginas}",
                input.Tipologia,
                markdown?.Length ?? 0,
                paginas);

            return new ExtraerMarkdownLayoutResultado
            {
                Modelo = "prebuilt-layout",
                Markdown = markdown,
                Paginas = paginas
            };
        }
    }
}
