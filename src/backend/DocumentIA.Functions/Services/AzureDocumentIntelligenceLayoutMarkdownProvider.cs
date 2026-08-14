using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

public class AzureDocumentIntelligenceLayoutMarkdownProvider : ILayoutMarkdownProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LayoutModelRegistryLoader _modelRegistryLoader;
    private readonly DocumentIntelligenceSourceResolver _sourceResolver;
    private readonly ILogger<AzureDocumentIntelligenceLayoutMarkdownProvider> _logger;

    public AzureDocumentIntelligenceLayoutMarkdownProvider(
        IHttpClientFactory httpClientFactory,
        LayoutModelRegistryLoader modelRegistryLoader,
        DocumentIntelligenceSourceResolver sourceResolver,
        ILogger<AzureDocumentIntelligenceLayoutMarkdownProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _modelRegistryLoader = modelRegistryLoader;
        _sourceResolver = sourceResolver;
        _logger = logger;
    }

    public virtual async Task<ExtraerMarkdownLayoutResultado> ExtraerMarkdownAsync(
        ExtraerMarkdownLayoutInput input,
        CancellationToken cancellationToken = default)
    {
        var model = _modelRegistryLoader.GetDefaultModel();

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            throw new InvalidOperationException("El modelo de layout no tiene Endpoint configurado en base de datos.");
        }

        if (!string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(model.ApiKey))
        {
            throw new InvalidOperationException("El modelo de layout no tiene ApiKey configurado en base de datos (AuthMode=ApiKey).");
        }

        var apiVersion = string.IsNullOrWhiteSpace(model.ApiVersion) ? "2024-11-30" : model.ApiVersion;
        var baseEndpoint = model.Endpoint.TrimEnd('/');
        var analyzeUrl =
            $"{baseEndpoint}/documentintelligence/documentModels/prebuilt-layout:analyze?outputContentFormat=markdown&api-version={Uri.EscapeDataString(apiVersion)}";

        var source = await _sourceResolver.ResolveAsync(
            input.BlobPath,
            base64Override: null,
            base64Entrada: input.DocumentoBase64,
            cancellationToken);

        var requestBody = JsonSerializer.Serialize(source.Body);

        using var client = _httpClientFactory.CreateClient();

        async Task<HttpResponseMessage> EnviarAsync(string cuerpo)
        {
            var peticion = new HttpRequestMessage(HttpMethod.Post, analyzeUrl)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
            };

            peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            await DocumentIntelligenceAuthHelper.ApplyAuthAsync(peticion, model.AuthMode, model.ApiKey, cancellationToken);

            using (peticion)
            {
                return await client.SendAsync(peticion, cancellationToken);
            }
        }

        var startResponse = await EnviarAsync(requestBody);

        if (!startResponse.IsSuccessStatusCode)
        {
            var body = await startResponse.Content.ReadAsStringAsync(cancellationToken);

            // Red de seguridad: si el storage dejó de ser alcanzable para DI y el flag de
            // transporte inline no está activo, se reintenta empujando el documento.
            if (source.UsingUrlSource
                && !string.IsNullOrWhiteSpace(input.BlobPath)
                && (int)startResponse.StatusCode == 400
                && body.Contains("InvalidContent", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "DI layout respondió InvalidContent con urlSource para BlobPath={BlobPath}. Reintentando con base64Source.",
                    input.BlobPath);

                var inlineBody = await _sourceResolver.BuildInlineBodyAsync(input.BlobPath!, cancellationToken);
                startResponse.Dispose();
                startResponse = await EnviarAsync(JsonSerializer.Serialize(inlineBody));

                if (!startResponse.IsSuccessStatusCode)
                {
                    body = await startResponse.Content.ReadAsStringAsync(cancellationToken);
                    startResponse.Dispose();
                    throw new InvalidOperationException($"Error iniciando DI layout. Status={(int)startResponse.StatusCode}. Body={body}");
                }
            }
            else
            {
                startResponse.Dispose();
                throw new InvalidOperationException($"Error iniciando DI layout. Status={(int)startResponse.StatusCode}. Body={body}");
            }
        }

        var operationLocation = startResponse.Headers.TryGetValues("operation-location", out var values)
            ? values.FirstOrDefault()
            : null;

        startResponse.Dispose();

        if (string.IsNullOrWhiteSpace(operationLocation))
        {
            throw new InvalidOperationException("La respuesta de DI layout no devolvió operation-location");
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
