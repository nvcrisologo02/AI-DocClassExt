using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Core;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
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

    public async Task<ExtraccionResultado> ObtenerDatosAsync(ExtraccionInput input, CancellationToken cancellationToken = default)
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

        _logger.LogInformation(
            "Azure Content Understanding completado para tipología {Tipologia} con analyzer {AnalyzerId}. Campos: {Count}",
            input.Tipologia,
            model.AnalyzerId,
            datosExtraidos.Count);

        return new ExtraccionResultado
        {
            Proveedor = model.Provider,
            Modelo = model.AnalyzerId,
            LayoutEnabled = true,
            OperationId = operation.Id,
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
}