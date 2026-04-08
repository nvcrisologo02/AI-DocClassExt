using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

public class GptDirectExtraerDataProvider
{
    private readonly GptFallbackExtraerDataProvider _gptExecutor;
    private readonly ExtractionModelRegistryLoader _modelRegistryLoader;
    private readonly ILogger<GptDirectExtraerDataProvider> _logger;

    public GptDirectExtraerDataProvider(
        GptFallbackExtraerDataProvider gptExecutor,
        ExtractionModelRegistryLoader modelRegistryLoader,
        ILogger<GptDirectExtraerDataProvider> logger)
    {
        _gptExecutor = gptExecutor;
        _modelRegistryLoader = modelRegistryLoader;
        _logger = logger;
    }

    public virtual async Task<ExtraccionResultado> ObtenerDatosAsync(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        CancellationToken cancellationToken = default)
    {
        var modelKey = ResolveDirectModelKey(input, tipologiaConfig);
        var model = _gptExecutor.ResolveModel(modelKey);
        
        _logger.LogInformation(
            "Iniciando extracción GPT directa. Tipología={Tipologia}, ModelKey={ModelKey}, Provider={Provider}",
            input.Tipologia,
            modelKey,
            model.Provider);

        var (markdown, paginas, origen) = EnsureMarkdown(input);

        _logger.LogInformation(
            "Markdown listo para extracción GPT directa. Tipología={Tipologia}, Origen={Origen}, Longitud={Longitud}",
            input.Tipologia,
            origen,
            markdown.Length);

        var resultado = await _gptExecutor.ObtenerDatosConModeloAsync(
            input,
            tipologiaConfig,
            modelKey,
            markdown,
            cancellationToken);

        resultado.FallbackUsado = false;
        resultado.FallbackRazon = null;
        resultado.MarkdownExtraido = markdown;
        resultado.DatosExtraidos["Markdown"] = markdown;

        if (resultado.Paginas <= 0 && paginas > 0)
        {
            resultado.Paginas = paginas;
        }

        return resultado;
    }

    private string ResolveDirectModelKey(ExtraccionInput input, TipologiaValidationConfig tipologiaConfig)
    {
        if (!string.IsNullOrWhiteSpace(input.ModelKeyEfectivo))
        {
            return input.ModelKeyEfectivo;
        }

        if (!string.IsNullOrWhiteSpace(tipologiaConfig.Extraction.ModelKey))
        {
            return tipologiaConfig.Extraction.ModelKey;
        }

        return _modelRegistryLoader.GetDefaultModel("azure-openai").Key;
    }

    private (string Markdown, int Paginas, string Origen) EnsureMarkdown(ExtraccionInput input)
    {
        if (TryGetMarkdown(input.DatosNormalizados, out var markdown))
        {
            _logger.LogInformation(
                "Markdown obtenido de datos normalizados para extracción GPT directa. Tipología={Tipologia}, Longitud={Longitud}",
                input.Tipologia,
                markdown.Length);
            return (markdown, 0, "datos-normalizados");
        }

        _logger.LogWarning(
            "No hay markdown en datos normalizados para extracción GPT directa. Tipología={Tipologia}. El orquestador debe haberlo preparado en Paso 3.5.",
            input.Tipologia);

        return (string.Empty, 0, "sin-markdown");
    }

    private static bool TryGetMarkdown(IDictionary<string, object> datosNormalizados, out string markdown)
    {
        markdown = string.Empty;
        if (datosNormalizados is null || datosNormalizados.Count == 0)
        {
            return false;
        }

        foreach (var clave in new[] { "Markdown", "markdown" })
        {
            if (!datosNormalizados.TryGetValue(clave, out var raw) || raw is null)
            {
                continue;
            }

            if (raw is string texto && !string.IsNullOrWhiteSpace(texto))
            {
                markdown = texto;
                return true;
            }

            if (raw is JsonElement json && json.ValueKind == JsonValueKind.String)
            {
                var value = json.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    markdown = value;
                    return true;
                }
            }
        }

        return false;
    }
}
