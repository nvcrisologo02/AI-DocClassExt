using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Core;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Abstractions;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace DocumentIA.Functions.Services;

public class AzureContentUnderstandingProvider : IExtraerDataProvider
{
    private readonly ILogger<AzureContentUnderstandingProvider> _logger;
    private readonly TipologiaConfigLoader _tipologiaConfigLoader;
    private readonly ExtractionModelRegistryLoader _modelRegistryLoader;
    private readonly ContentUnderstandingResultMapper _resultMapper;
    private readonly IBlobStorageService _blobStorageService;
    private readonly AzureContentUnderstandingOptions _options;
    private readonly SemaphoreSlim _cuLimiter;
    private readonly TelemetryClient _telemetryClient;

    public AzureContentUnderstandingProvider(
        ILogger<AzureContentUnderstandingProvider> logger,
        TipologiaConfigLoader tipologiaConfigLoader,
        ExtractionModelRegistryLoader modelRegistryLoader,
        ContentUnderstandingResultMapper resultMapper,
        IBlobStorageService blobStorageService,
        IOptions<AzureContentUnderstandingOptions> options,
        TelemetryClient telemetryClient)
    {
        _logger = logger;
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _modelRegistryLoader = modelRegistryLoader;
        _resultMapper = resultMapper;
        _blobStorageService = blobStorageService;
        _options = options.Value;
        var maxConcurrentCalls = Math.Max(1, _options.MaxConcurrentCalls);
        _cuLimiter = new SemaphoreSlim(maxConcurrentCalls, maxConcurrentCalls);
        _telemetryClient = telemetryClient;
    }

    public virtual async Task<ExtraccionResultado> ObtenerDatosAsync(ExtraccionInput input, CancellationToken cancellationToken = default)
    {
        var prepareStopwatch = Stopwatch.StartNew();
        var tipologiaConfig = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
        var extractionConfig = tipologiaConfig.Extraction;

        if (!extractionConfig.Enabled)
        {
            throw new InvalidOperationException($"La extracción Azure no está habilitada para la tipología '{input.Tipologia}'");
        }

        var modelKey = !string.IsNullOrWhiteSpace(input.ModelKeyEfectivo)
            ? input.ModelKeyEfectivo
            : extractionConfig.ModelKey;
        var model = _modelRegistryLoader.GetModel(modelKey);
        ValidateAzureCuModel(model);
        var client = CreateClient(model);
        var fileName = input.Entrada.Documento.Name;
        // Blob-first: si hay BlobPath → descargar del blob; si no, usar base64
        byte[] documentBytes;
        var blobPath = input.Entrada.Documento.BlobPath;
        if (!string.IsNullOrWhiteSpace(blobPath))
        {
            _logger.LogInformation("ContentUnderstandingProvider descargando desde blob. BlobPath={BlobPath}", blobPath);
            documentBytes = await _blobStorageService.DownloadDocumentAsync(blobPath);
        }
        else
        {
            documentBytes = Convert.FromBase64String(input.Entrada.Documento.Content.Base64);
        }
        var binaryData = BinaryData.FromBytes(documentBytes);
        var contentType = ResolveContentType(model, fileName, binaryData);
        var processingLocation = ResolveProcessingLocation(model);
        var contentRange = string.IsNullOrWhiteSpace(model.InputRange) ? null : model.InputRange;
        prepareStopwatch.Stop();

        Operation<BinaryData>? operation = null;
        var attempts = 0;
        var analysisElapsedMs = 0L;
        var limiterWaitStopwatch = Stopwatch.StartNew();
        await _cuLimiter.WaitAsync(cancellationToken);
        limiterWaitStopwatch.Stop();

        try
        {
            var maxAttempts = Math.Max(1, _options.MaxRetries);
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                attempts = attempt;
                var analysisStopwatch = Stopwatch.StartNew();
                try
                {
                    var requestContent = RequestContent.Create(binaryData.ToArray());
                    operation = contentRange is { } range
                        ? await client.AnalyzeBinaryAsync(
                            WaitUntil.Completed,
                            model.AnalyzerId,
                            contentType,
                            requestContent,
                            processingLocation: processingLocation,
                            contentRange: range)
                        : await client.AnalyzeBinaryAsync(
                            WaitUntil.Completed,
                            model.AnalyzerId,
                            contentType,
                            requestContent,
                            processingLocation: processingLocation);
                    analysisStopwatch.Stop();
                    analysisElapsedMs += analysisStopwatch.ElapsedMilliseconds;
                    break;
                }
                catch (Exception ex) when (IsTransientCuError(ex) && attempt < maxAttempts)
                {
                    analysisStopwatch.Stop();
                    analysisElapsedMs += analysisStopwatch.ElapsedMilliseconds;
                    TrackTransientError(input.Tipologia, attempt, ex);

                    var retryDelay = ComputeRetryDelay(ex, attempt);
                    _logger.LogWarning(
                        ex,
                        "Error transitorio en Azure Content Understanding para tipología {Tipologia}. Intento {Attempt}/{MaxAttempts}. Reintento en {DelayMs} ms",
                        input.Tipologia,
                        attempt,
                        maxAttempts,
                        retryDelay.TotalMilliseconds);
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
        }
        finally
        {
            _cuLimiter.Release();
        }

        if (operation is null)
        {
            throw new InvalidOperationException("Azure Content Understanding no devolvió una operación de análisis.");
        }

        var parseStopwatch = Stopwatch.StartNew();
        using var analysisDocument = JsonDocument.Parse(operation.Value.ToString());
        var datosExtraidos = _resultMapper.Map(analysisDocument, tipologiaConfig);
        var paginas = ResolvePageCount(analysisDocument);
        var markdownExtraido = TryExtractMarkdown(analysisDocument);
        parseStopwatch.Stop();

        if (paginas > 0)
        {
            datosExtraidos["Paginas"] = paginas;
        }

        if (!string.IsNullOrWhiteSpace(markdownExtraido))
        {
            datosExtraidos["Markdown"] = markdownExtraido;
        }

        var camposPresentes = datosExtraidos.Keys
            .Count(k => k != "Paginas" && k != "Markdown");
        var camposRequeridos = tipologiaConfig.Fields.Count(f => f.Required);
        var camposRequeridosPresentes = tipologiaConfig.Fields
            .Where(f => f.Required)
            .Count(f => datosExtraidos.ContainsKey(f.Name));
        var confidenceMap = TryExtractFieldConfidenceMap(analysisDocument);
        var fieldConfs = confidenceMap?.Values.Select(v => (double?)v).ToList();
        var confidenceConfig = tipologiaConfig.ConfidenceConfig;

        var (confianzaExtraccion, metricasDebug) = ConfidenceCalculator.ExtracCU(
            fieldConfs: fieldConfs,
            camposPresentes: camposPresentes,
            camposTotales: tipologiaConfig.Fields.Count,
            camposRequeridos: camposRequeridos,
            camposRequeridosPresentes: camposRequeridosPresentes,
            warnings: 0,
            cfg: confidenceConfig);

        var umbralDuda = input.UmbralFallbackEfectivo
            ?? confidenceConfig?.ExtracUmbralFallback
            ?? 0.6;

        metricasDebug.ConfianzaPorCampo = confidenceMap ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        metricasDebug.CamposBajaConfianza = metricasDebug.ConfianzaPorCampo
            .Where(kvp => kvp.Value < umbralDuda)
            .Select(kvp => kvp.Key)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Azure Content Understanding completado para tipología {Tipologia} con analyzer {AnalyzerId}. Campos: {Count}. Paginas: {Paginas}",
            input.Tipologia,
            model.AnalyzerId,
            datosExtraidos.Count,
            paginas);

        TrackCuMetrics(
            input.Tipologia,
            prepareStopwatch.ElapsedMilliseconds,
            limiterWaitStopwatch.ElapsedMilliseconds,
            analysisElapsedMs,
            parseStopwatch.ElapsedMilliseconds,
            attempts);

        return new ExtraccionResultado
        {
            Proveedor = model.Provider,
            Modelo = model.AnalyzerId,
            LayoutEnabled = true,
            OperationId = operation.Id,
            Paginas = paginas,
            MarkdownExtraido = markdownExtraido,
            ConfianzaExtraccion = confianzaExtraccion,
            ProveedorExtrac = "AzureContentUnderstanding",
            MetricasDebug = metricasDebug,
            TiemposMs = new Dictionary<string, int>
            {
                ["prepare"] = (int)prepareStopwatch.ElapsedMilliseconds,
                ["limiterWaitMs"] = (int)limiterWaitStopwatch.ElapsedMilliseconds,
                ["analysis"] = (int)analysisElapsedMs,
                ["parse"] = (int)parseStopwatch.ElapsedMilliseconds,
                ["attempts"] = attempts
            },
            DatosExtraidos = datosExtraidos
        };
    }

    private ContentUnderstandingClient CreateClient(ExtractionModelConfig model)
    {
        var clientOptions = new ContentUnderstandingClientOptions();
        clientOptions.Retry.MaxRetries = 0;

        if (string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentUnderstandingClient(new Uri(model.Endpoint), new DefaultAzureCredential(), clientOptions);
        }

        return new ContentUnderstandingClient(new Uri(model.Endpoint), new AzureKeyCredential(model.ApiKey), clientOptions);
    }

    private static bool IsTransientCuError(Exception ex)
    {
        if (ex is not RequestFailedException requestFailedException)
        {
            return false;
        }

        return requestFailedException.Status is 429 or 500 or 502 or 503 or 504
            || requestFailedException.Message.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase);
    }

    private TimeSpan ComputeRetryDelay(Exception ex, int attempt)
    {
        if (ex is RequestFailedException requestFailedException
            && requestFailedException.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var retryAfter) == true
            && int.TryParse(retryAfter, out var retryAfterSeconds)
            && retryAfterSeconds > 0)
        {
            return TimeSpan.FromSeconds(retryAfterSeconds);
        }

        var baseDelayMs = Math.Max(1, _options.InitialRetryDelayMs) * Math.Pow(2, Math.Max(0, attempt - 1));
        var jitterFactor = 0.8 + Random.Shared.NextDouble() * 0.4;
        return TimeSpan.FromMilliseconds(baseDelayMs * jitterFactor);
    }

    private void TrackTransientError(string tipologia, int attempt, Exception ex)
    {
        _telemetryClient.TrackEvent("CU.TransientError", new Dictionary<string, string>
        {
            ["attempt"] = attempt.ToString(),
            ["statusCode"] = ex is RequestFailedException requestFailedException
                ? requestFailedException.Status.ToString()
                : "unknown",
            ["tipologia"] = tipologia
        });
    }

    private void TrackCuMetrics(
        string tipologia,
        long prepareMs,
        long limiterWaitMs,
        long analysisMs,
        long parseMs,
        int attempts)
    {
        var properties = new Dictionary<string, string>
        {
            ["Tipologia"] = tipologia
        };

        _telemetryClient.TrackMetric("CU.PrepareMs", prepareMs, properties);
        _telemetryClient.TrackMetric("CU.LimiterWaitMs", limiterWaitMs, properties);
        _telemetryClient.TrackMetric("CU.AnalysisMs", analysisMs, properties);
        _telemetryClient.TrackMetric("CU.ParseMs", parseMs, properties);
        _telemetryClient.TrackMetric("CU.Attempts", attempts, properties);
    }

    private static string ResolveProcessingLocation(ExtractionModelConfig model)
    {
        if (!string.IsNullOrWhiteSpace(model.ProcessingLocation))
        {
            return model.ProcessingLocation;
        }

        return "global";
    }

    private static void ValidateAzureCuModel(ExtractionModelConfig model)
    {
        if (!IsAzureCuProvider(model.Provider))
        {
            throw new InvalidOperationException(
                $"El modelo de extracción '{model.Key}' no es compatible con Azure Content Understanding. Provider actual: '{model.Provider}'.");
        }

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            throw new InvalidOperationException($"ExtractionModelConfig.Endpoint es obligatorio para el modelo '{model.Key}'.");
        }

        if (string.IsNullOrWhiteSpace(model.AnalyzerId))
        {
            throw new InvalidOperationException($"ExtractionModelConfig.AnalyzerId es obligatorio para el modelo '{model.Key}'.");
        }

        if (!string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(model.ApiKey))
        {
            throw new InvalidOperationException(
                $"ExtractionModelConfig.ApiKey es obligatorio para el modelo '{model.Key}' cuando AuthMode=ApiKey.");
        }
    }

    private static bool IsAzureCuProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-content-understanding" or "azure-cu" or "cu";

    private static string ResolveContentType(ExtractionModelConfig model, string fileName, BinaryData binaryData)
    {
        if (!string.IsNullOrWhiteSpace(model.ContentType))
        {
            return model.ContentType;
        }

        if (!string.IsNullOrWhiteSpace(binaryData.MediaType))
        {
            return binaryData.MediaType;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".tif" or ".tiff" => "image/tiff",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".html" => "text/html",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static int ResolvePageCount(JsonDocument analysisDocument)
    {
        if (TryGetPagesFromContents(analysisDocument.RootElement, out var pagesFromContents))
        {
            return pagesFromContents;
        }

        if (analysisDocument.RootElement.TryGetProperty("usage", out var usageElement)
            && usageElement.ValueKind == JsonValueKind.Object
            && usageElement.TryGetProperty("documentPagesStandard", out var pagesUsageElement)
            && pagesUsageElement.TryGetInt32(out var pagesFromUsage)
            && pagesFromUsage > 0)
        {
            return pagesFromUsage;
        }

        return 0;
    }

    private static bool TryGetPagesFromContents(JsonElement rootElement, out int pages)
    {
        pages = 0;

        if (!rootElement.TryGetProperty("result", out var resultElement)
            || resultElement.ValueKind != JsonValueKind.Object
            || !resultElement.TryGetProperty("contents", out var contentsElement)
            || contentsElement.ValueKind != JsonValueKind.Array
            || contentsElement.GetArrayLength() == 0)
        {
            return false;
        }

        var firstContent = contentsElement[0];

        if (firstContent.TryGetProperty("pages", out var pagesElement)
            && pagesElement.ValueKind == JsonValueKind.Array
            && pagesElement.GetArrayLength() > 0)
        {
            pages = pagesElement.GetArrayLength();
            return true;
        }

        if (firstContent.TryGetProperty("startPageNumber", out var startPageElement)
            && firstContent.TryGetProperty("endPageNumber", out var endPageElement)
            && startPageElement.TryGetInt32(out var startPage)
            && endPageElement.TryGetInt32(out var endPage)
            && endPage >= startPage)
        {
            pages = endPage - startPage + 1;
            return true;
        }

        return false;
    }

    private static string? TryExtractMarkdown(JsonDocument analysisDocument)
    {
        if (analysisDocument.RootElement.TryGetProperty("result", out var resultElement)
            && resultElement.ValueKind == JsonValueKind.Object
            && resultElement.TryGetProperty("contents", out var contentsElement)
            && contentsElement.ValueKind == JsonValueKind.Array
            && contentsElement.GetArrayLength() > 0)
        {
            foreach (var content in contentsElement.EnumerateArray())
            {
                if (content.ValueKind == JsonValueKind.Object
                    && content.TryGetProperty("markdown", out var markdownElement)
                    && markdownElement.ValueKind == JsonValueKind.String)
                {
                    var markdown = markdownElement.GetString();
                    if (!string.IsNullOrWhiteSpace(markdown))
                    {
                        return markdown;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Intenta extraer las confianzas de cada campo del response CU.
    /// Estructura esperada: result.contents[0].fields[fieldName].confidence
    /// Devuelve null si la API no incluye confianzas de campo.
    /// </summary>
    private static Dictionary<string, double>? TryExtractFieldConfidenceMap(JsonDocument analysisDocument)
    {
        if (!analysisDocument.RootElement.TryGetProperty("result", out var resultEl)
            || resultEl.ValueKind != JsonValueKind.Object
            || !resultEl.TryGetProperty("contents", out var contentsEl)
            || contentsEl.ValueKind != JsonValueKind.Array
            || contentsEl.GetArrayLength() == 0)
        {
            return null;
        }

        var firstContent = contentsEl[0];
        if (!firstContent.TryGetProperty("fields", out var fieldsEl)
            || fieldsEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var confByField = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fieldsEl.EnumerateObject())
        {
            if (field.Value.ValueKind == JsonValueKind.Object
                && field.Value.TryGetProperty("confidence", out var confEl)
                && confEl.TryGetDouble(out var conf))
            {
                confByField[field.Name] = conf;
            }
        }

        return confByField.Count > 0 ? confByField : null;
    }
}