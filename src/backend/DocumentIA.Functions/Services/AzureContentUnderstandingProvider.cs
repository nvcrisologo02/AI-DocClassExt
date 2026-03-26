using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Core;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Abstractions;
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
    private readonly ContentUnderstandingClient _client;
    private readonly AzureContentUnderstandingSettings _settings;

    public AzureContentUnderstandingProvider(
        ILogger<AzureContentUnderstandingProvider> logger,
        TipologiaConfigLoader tipologiaConfigLoader,
        ExtractionModelRegistryLoader modelRegistryLoader,
        ContentUnderstandingResultMapper resultMapper,
        IOptions<AzureContentUnderstandingSettings> settings)
    {
        _logger = logger;
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _modelRegistryLoader = modelRegistryLoader;
        _resultMapper = resultMapper;
        _settings = settings.Value;
        _client = CreateClient(_settings);
    }

    public virtual async Task<ExtraccionResultado> ObtenerDatosAsync(ExtraccionInput input, CancellationToken cancellationToken = default)
    {
        var tipologiaConfig = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
        var extractionConfig = tipologiaConfig.Extraction;

        if (!extractionConfig.Enabled)
        {
            throw new InvalidOperationException($"La extracción Azure no está habilitada para la tipología '{input.Tipologia}'");
        }

        var model = _modelRegistryLoader.GetModel(extractionConfig.ModelKey);
        var fileName = input.Entrada.Documento.Name;
        var binaryData = BinaryData.FromBytes(Convert.FromBase64String(input.Entrada.Documento.Content.Base64));
        var contentType = ResolveContentType(model, fileName, binaryData);
        var processingLocation = ResolveProcessingLocation(model);
        var contentRange = string.IsNullOrWhiteSpace(model.InputRange) ? null : model.InputRange;
        var requestContent = RequestContent.Create(binaryData.ToArray());

        var stopwatch = Stopwatch.StartNew();
        var operation = contentRange is { } range
            ? await _client.AnalyzeBinaryAsync(
                WaitUntil.Completed,
                model.AnalyzerId,
                contentType,
                requestContent,
                processingLocation: processingLocation,
                contentRange: range)
            : await _client.AnalyzeBinaryAsync(
                WaitUntil.Completed,
                model.AnalyzerId,
                contentType,
                requestContent,
                processingLocation: processingLocation);
        stopwatch.Stop();

        using var analysisDocument = JsonDocument.Parse(operation.Value.ToString());
        var datosExtraidos = _resultMapper.Map(analysisDocument, tipologiaConfig);
        var paginas = ResolvePageCount(analysisDocument);
        var markdownExtraido = TryExtractMarkdown(analysisDocument);

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
        var fieldConfs = TryExtractFieldConfidences(analysisDocument);
        var confidenceConfig = tipologiaConfig.ConfidenceConfig;

        var (confianzaExtraccion, metricasDebug) = ConfidenceCalculator.ExtracCU(
            fieldConfs: fieldConfs,
            camposPresentes: camposPresentes,
            camposTotales: tipologiaConfig.Fields.Count,
            camposRequeridos: camposRequeridos,
            camposRequeridosPresentes: camposRequeridosPresentes,
            warnings: 0,
            cfg: confidenceConfig);

        _logger.LogInformation(
            "Azure Content Understanding completado para tipología {Tipologia} con analyzer {AnalyzerId}. Campos: {Count}. Paginas: {Paginas}",
            input.Tipologia,
            model.AnalyzerId,
            datosExtraidos.Count,
            paginas);

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
                ["analysis"] = (int)stopwatch.ElapsedMilliseconds
            },
            DatosExtraidos = datosExtraidos
        };
    }

    private static ContentUnderstandingClient CreateClient(AzureContentUnderstandingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            throw new InvalidOperationException("Extraction:AzureContentUnderstanding:Endpoint es obligatorio");
        }

        if (string.Equals(settings.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentUnderstandingClient(new Uri(settings.Endpoint), new DefaultAzureCredential());
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("Extraction:AzureContentUnderstanding:ApiKey es obligatorio cuando AuthMode=ApiKey");
        }

        return new ContentUnderstandingClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey));
    }

    private string ResolveProcessingLocation(ExtractionModelConfig model)
    {
        if (!string.IsNullOrWhiteSpace(model.ProcessingLocation))
        {
            return model.ProcessingLocation;
        }

        return string.IsNullOrWhiteSpace(_settings.DefaultProcessingLocation)
            ? "global"
            : _settings.DefaultProcessingLocation;
    }

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
    private static IReadOnlyList<double?>? TryExtractFieldConfidences(JsonDocument analysisDocument)
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

        var confs = new List<double?>();
        foreach (var field in fieldsEl.EnumerateObject())
        {
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