using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Proveedor de extraccion que usa Azure Document Intelligence con un modelo custom entrenado.
/// Llama a documentModels/{modelId}:analyze y lee analyzeResult.documents[0].fields,
/// que usa el mismo esquema value* que CU, por lo que ContentUnderstandingResultMapper es reutilizable.
/// La configuracion de endpoint, autenticacion y version de API se lee del ExtractionModelRegistryLoader (base de datos).
/// </summary>
public class AzureDocumentIntelligenceExtraerDataProvider : IExtraerDataProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TipologiaConfigLoader _tipologiaConfigLoader;
    private readonly ExtractionModelRegistryLoader _modelRegistryLoader;
    private readonly ContentUnderstandingResultMapper _resultMapper;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<AzureDocumentIntelligenceExtraerDataProvider> _logger;

    public AzureDocumentIntelligenceExtraerDataProvider(
        IHttpClientFactory httpClientFactory,
        TipologiaConfigLoader tipologiaConfigLoader,
        ExtractionModelRegistryLoader modelRegistryLoader,
        ContentUnderstandingResultMapper resultMapper,
        IBlobStorageService blobStorageService,
        ILogger<AzureDocumentIntelligenceExtraerDataProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _modelRegistryLoader = modelRegistryLoader;
        _resultMapper = resultMapper;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<ExtraccionResultado> ObtenerDatosAsync(
        ExtraccionInput input,
        CancellationToken cancellationToken = default)
    {
        var tipologiaConfig = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
        var model = _modelRegistryLoader.GetModel(tipologiaConfig.Extraction.ModelKey);

        if (string.IsNullOrWhiteSpace(model.Endpoint))
            throw new InvalidOperationException($"El modelo de extraccion '{model.Key}' no tiene Endpoint configurado en base de datos");
        if (!string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(model.ApiKey))
            throw new InvalidOperationException($"El modelo de extraccion '{model.Key}' no tiene ApiKey configurado en base de datos (AuthMode=ApiKey)");

        var baseEndpoint = model.Endpoint.TrimEnd('/');
        var analyzeUrl =
            $"{baseEndpoint}/documentintelligence/documentModels/{Uri.EscapeDataString(model.AnalyzerId)}:analyze" +
            $"?api-version={Uri.EscapeDataString(model.ApiVersion)}";

        // Blob-first: si hay BlobPath → usar urlSource (Azure DI descarga directo del blob vía SAS URL)
        string requestBody;
        var blobPath = input.Entrada.Documento.BlobPath;
        if (!string.IsNullOrWhiteSpace(blobPath))
        {
            var sasUrl = await _blobStorageService.GenerateSasUrlAsync(blobPath, TimeSpan.FromMinutes(30));
            requestBody = JsonSerializer.Serialize(new { urlSource = sasUrl });
            _logger.LogInformation("ExtraerDataProvider usando urlSource (SAS) para BlobPath={BlobPath}", blobPath);
        }
        else
        {
            requestBody = JsonSerializer.Serialize(new { base64Source = input.Entrada.Documento.Content.Base64 });
        }

        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, analyzeUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        await DocumentIntelligenceAuthHelper.ApplyAuthAsync(request, model.AuthMode, model.ApiKey, cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        using var startResponse = await client.SendAsync(request, cancellationToken);
        if (!startResponse.IsSuccessStatusCode)
        {
            var body = await startResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Error iniciando extraccion DI. Status={(int)startResponse.StatusCode}. Body={body}");
        }

        var operationLocation = startResponse.Headers.TryGetValues("operation-location", out var vals)
            ? vals.FirstOrDefault()
            : null;

        if (string.IsNullOrWhiteSpace(operationLocation))
            throw new InvalidOperationException("DI no devolvio operation-location en la respuesta de inicio de analisis");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, model.TimeoutSeconds));
        JsonDocument? finalResult = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(Math.Max(500, model.PollIntervalMs), cancellationToken);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            await DocumentIntelligenceAuthHelper.ApplyAuthAsync(pollRequest, model.AuthMode, model.ApiKey, cancellationToken);

            using var pollResponse = await client.SendAsync(pollRequest, cancellationToken);
            var pollBody = await pollResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!pollResponse.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Error consultando operacion DI. Status={(int)pollResponse.StatusCode}. Body={pollBody}");

            var pollJson = JsonDocument.Parse(pollBody);
            var status = pollJson.RootElement.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString() ?? string.Empty
                : string.Empty;

            if (status.Equals("succeeded", StringComparison.OrdinalIgnoreCase))
            {
                finalResult = JsonDocument.Parse(pollBody);
                break;
            }

            if (status.Equals("failed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("canceled", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"La extraccion DI termino en estado '{status}'. Respuesta={pollBody}");
            }
        }

        stopwatch.Stop();

        if (finalResult is null)
            throw new TimeoutException("Timeout esperando resultado de extraccion en Azure Document Intelligence");

        using (finalResult)
        {
            var fieldsElement = ResolveFieldsElement(finalResult);

            var datosExtraidos = fieldsElement.HasValue
                ? _resultMapper.MapFromFields(fieldsElement.Value, tipologiaConfig)
                : new Dictionary<string, object>();

            var camposPresentes = datosExtraidos.Count;
            var camposRequeridos = tipologiaConfig.Fields.Count(f => f.Required);
            var camposRequeridosPresentes = tipologiaConfig.Fields
                .Where(f => f.Required)
                .Count(f => datosExtraidos.ContainsKey(f.Name));
            var avoidConfidenceFields = ConfidenceFieldFilter.GetAvoidConfidenceFields(tipologiaConfig);
            var fieldConfs = fieldsElement.HasValue
                ? TryExtractFieldConfidences(fieldsElement.Value, avoidConfidenceFields)
                : null;

            var (confianzaExtraccion, metricasDebug) = ConfidenceCalculator.ExtracCU(
                fieldConfs: fieldConfs,
                camposPresentes: camposPresentes,
                camposTotales: tipologiaConfig.Fields.Count,
                camposRequeridos: camposRequeridos,
                camposRequeridosPresentes: camposRequeridosPresentes,
                warnings: 0,
                cfg: tipologiaConfig.ConfidenceConfig);

            metricasDebug.CamposExcluidosConfianza = ConfidenceFieldFilter.ToSortedList(avoidConfidenceFields);

            _logger.LogInformation(
                "Extraccion DI custom completada. Tipologia={Tipologia}, ModelId={ModelId}, Campos={Campos}, Confianza={Confianza:F3}",
                input.Tipologia,
                model.AnalyzerId,
                datosExtraidos.Count,
                confianzaExtraccion);

            return new ExtraccionResultado
            {
                Proveedor = model.Provider,
                Modelo = model.AnalyzerId,
                LayoutEnabled = false,
                ConfianzaExtraccion = confianzaExtraccion,
                ProveedorExtrac = "DocumentIntelligence",
                MetricasDebug = metricasDebug,
                TiemposMs = new Dictionary<string, int> { ["analysis"] = (int)stopwatch.ElapsedMilliseconds },
                DatosExtraidos = datosExtraidos
            };
        }
    }

    /// <summary>
    /// Localiza el elemento "fields" dentro de la respuesta DI:
    /// analyzeResult.documents[0].fields  (o documents[0].fields si analyzeResult no existe en la raiz)
    /// </summary>
    private static JsonElement? ResolveFieldsElement(JsonDocument diResult)
    {
        var root = diResult.RootElement;
        var analyzeResult = root.TryGetProperty("analyzeResult", out var ar) ? ar : root;

        if (!analyzeResult.TryGetProperty("documents", out var docsEl)
            || docsEl.ValueKind != JsonValueKind.Array
            || docsEl.GetArrayLength() == 0)
        {
            return null;
        }

        var firstDoc = docsEl[0];
        return firstDoc.TryGetProperty("fields", out var fieldsEl)
               && fieldsEl.ValueKind == JsonValueKind.Object
            ? fieldsEl
            : null;
    }

    /// <summary>
    /// Extrae la confianza por campo desde el elemento fields de DI:
    /// fields[fieldName].confidence
    /// </summary>
    private static IReadOnlyList<double?>? TryExtractFieldConfidences(JsonElement fieldsElement, ISet<string> avoidConfidenceFields)
    {
        var confs = new List<double?>();
        foreach (var field in fieldsElement.EnumerateObject())
        {
            if (avoidConfidenceFields.Contains(field.Name))
            {
                continue;
            }

            if (field.Value.ValueKind == JsonValueKind.Object
                && field.Value.TryGetProperty("confidence", out var confEl)
                && confEl.TryGetDouble(out var conf))
            {
                confs.Add(conf);
            }
            else
            {
                confs.Add(null);
            }
        }

        return confs.Count > 0 ? confs : null;
    }
}