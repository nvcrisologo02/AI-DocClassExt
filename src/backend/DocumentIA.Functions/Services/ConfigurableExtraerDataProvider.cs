using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace DocumentIA.Functions.Services;

public class ConfigurableExtraerDataProvider : IExtraerDataProvider
{
    private readonly TipologiaConfigLoader _tipologiaConfigLoader;
    private readonly MockExtraerDataProvider _mockProvider;
    private readonly AzureContentUnderstandingProvider _azureProvider;
    private readonly AzureDocumentIntelligenceExtraerDataProvider _diExtraerProvider;
    private readonly GptDirectExtraerDataProvider _gptDirectProvider;
    private readonly GptFallbackExtraerDataProvider _gptFallbackProvider;
    private readonly ExtractionModelRegistryLoader _extractionModelRegistryLoader;
    private readonly PromptModelRegistryLoader _promptModelRegistryLoader;
    private readonly ExtractionRoutingSettings _routingSettings;
    private readonly ILogger<ConfigurableExtraerDataProvider> _logger;

    public ConfigurableExtraerDataProvider(
        TipologiaConfigLoader tipologiaConfigLoader,
        MockExtraerDataProvider mockProvider,
        AzureContentUnderstandingProvider azureProvider,
        AzureDocumentIntelligenceExtraerDataProvider diExtraerProvider,
        GptDirectExtraerDataProvider gptDirectProvider,
        GptFallbackExtraerDataProvider gptFallbackProvider,
        ExtractionModelRegistryLoader extractionModelRegistryLoader,
        PromptModelRegistryLoader promptModelRegistryLoader,
        IOptions<ExtractionRoutingSettings> routingSettings,
        ILogger<ConfigurableExtraerDataProvider> logger)
    {
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _mockProvider = mockProvider;
        _azureProvider = azureProvider;
        _diExtraerProvider = diExtraerProvider;
        _gptDirectProvider = gptDirectProvider;
        _gptFallbackProvider = gptFallbackProvider;
        _extractionModelRegistryLoader = extractionModelRegistryLoader;
        _promptModelRegistryLoader = promptModelRegistryLoader;
        _routingSettings = routingSettings.Value;
        _logger = logger;
    }

    public async Task<ExtraccionResultado> ObtenerDatosAsync(ExtraccionInput input, CancellationToken cancellationToken = default)
    {
        var config = _tipologiaConfigLoader.LoadConfig(input.Tipologia);

        if (!config.Extraction.Enabled)
        {
            _logger.LogInformation(
                "Extracción deshabilitada para tipología {Tipologia}. Se devuelve resultado vacío.",
                input.Tipologia);

            return new ExtraccionResultado
            {
                Proveedor = "none",
                Modelo = "disabled",
                LayoutEnabled = false,
                DatosExtraidos = new Dictionary<string, object>()
            };
        }

        var provider = !string.IsNullOrWhiteSpace(input.ProviderEfectivo)
            ? input.ProviderEfectivo
            : string.IsNullOrWhiteSpace(config.Extraction.Provider)
                ? _routingSettings.DefaultProvider
                : config.Extraction.Provider;

        var fallbackEnabled = TryResolveFallbackModel(out var fallbackModel);
        var minFieldsRatio = fallbackModel?.MinFieldsRatio ?? 0.5;

        _logger.LogInformation("Proveedor de extracción resuelto para tipología {Tipologia}: {Provider}", input.Tipologia, provider);

        if (!IsAzureContentUnderstandingProvider(provider) || !fallbackEnabled)
        {
            return provider.ToLowerInvariant() switch
            {
                "azure-content-understanding" or "azure-cu" or "cu"
                    => await _azureProvider.ObtenerDatosAsync(input, cancellationToken),
                "azure-document-intelligence" or "azure-di" or "di"
                    => await _diExtraerProvider.ObtenerDatosAsync(input, cancellationToken),
                "azure-openai" or "openai" or "gpt"
                    => await _gptDirectProvider.ObtenerDatosAsync(input, config, cancellationToken),
                "mock" => await _mockProvider.ObtenerDatosAsync(input, cancellationToken),
                _ => throw new NotSupportedException($"Proveedor de extracción '{provider}' no soportado para tipología '{input.Tipologia}'")
            };
        }

        ExtraccionResultado? resultadoCu = null;
        string? fallbackRazon = null;

        try
        {
            resultadoCu = await _azureProvider.ObtenerDatosAsync(input, cancellationToken);

            if (EsResultadoCuSuficiente(
                config,
                resultadoCu,
                input.UmbralFallbackEfectivo,
                input.UmbralFallbackEfectivoCompletitud,
                input.UmbralFallbackEfectivoConfianza,
                out var ratioCompletitud,
                out var confianzaCu,
                out var esperados,
                out var obtenidosEsperados,
                out var umbralCompletitud,
                out var umbralConfianza,
                minFieldsRatio))
            {
                return resultadoCu;
            }

            fallbackRazon = string.Format(
                CultureInfo.InvariantCulture,
                "insufficient_extraction:ratio={0:F3}<{1:F3};conf={2:F3}<{3:F3};fields={4}/{5}",
                ratioCompletitud,
                umbralCompletitud,
                confianzaCu,
                umbralConfianza,
                obtenidosEsperados,
                esperados);
            _logger.LogWarning(
                "Extracción CU insuficiente para {Tipologia}. Ratio={Ratio:F3} (umbral={UmbralRatio:F3}), Confianza={Confianza:F3} (umbral={UmbralConfianza:F3}), obtenidosEsperados={ObtenidosEsperados}, esperados={Esperados}. Activando fallback GPT.",
                input.Tipologia,
                ratioCompletitud,
                umbralCompletitud,
                confianzaCu,
                umbralConfianza,
                obtenidosEsperados,
                esperados);
        }
        catch (Exception ex)
        {
            fallbackRazon = $"exception:{ex.GetType().Name}";
            _logger.LogWarning(ex, "Extracción CU falló para {Tipologia}. Activando fallback GPT.", input.Tipologia);
        }

        ExtraccionResultado resultadoGpt;
        var promptConfig = config.PromptConfig;

        // Optimización: si prompt está habilitado y usa el mismo modelo/deployment que el fallback,
        // se ejecuta extracción + prompt en una única llamada LLM.
        if (promptConfig is not null && DebeUsarModoCombinado(config))
        {
            _logger.LogInformation(
                "Activando modo combinado fallback+prompt para tipología {Tipologia}.",
                input.Tipologia);

            resultadoGpt = await _gptFallbackProvider.ObtenerDatosConFallbackYPromptAsync(
                input,
                config,
                promptConfig,
                resultadoCu?.MarkdownExtraido,
                cancellationToken);
        }
        else
        {
            resultadoGpt = await _gptFallbackProvider.ObtenerDatosConFallbackAsync(
                input,
                config,
                resultadoCu?.MarkdownExtraido,
                cancellationToken);

            var paginasCu = resultadoCu?.Paginas ?? 0;
            if (resultadoGpt.Paginas <= 0 && paginasCu > 0)
            {
                resultadoGpt.Paginas = paginasCu;
            }
        }

        resultadoGpt.FallbackUsado = true;
        resultadoGpt.FallbackRazon = fallbackRazon;

        return resultadoGpt;
    }

    private bool EsResultadoCuSuficiente(
        TipologiaValidationConfig config,
        ExtraccionResultado resultadoCu,
        double? umbralFallback,
        double? umbralFallbackCompletitudRequest,
        double? umbralFallbackConfianzaRequest,
        out double ratioCompletitud,
        out double confianzaCu,
        out int esperados,
        out int obtenidosEsperados,
        out double umbralCompletitud,
        out double umbralConfianza,
        double minFieldsRatio)
    {
        var confidenceConfig = config.ConfidenceConfig;

        var camposEsperados = config.Fields
            .Select(f => f.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        esperados = camposEsperados.Count;

        // Umbral legático (request.umbral ?? tipología.ExtracUmbralFallback) usado como fallback para ambos criterios
        var umbralLegado = umbralFallback ?? confidenceConfig?.ExtracUmbralFallback;

        if (esperados <= 0)
        {
            obtenidosEsperados = 0;
            ratioCompletitud = 1.0;
            confianzaCu = resultadoCu.ConfianzaExtraccion;

            // Prioridad: request-específico > tipología-específico > umbral-legático > global
            umbralCompletitud = umbralFallbackCompletitudRequest
                ?? confidenceConfig?.ExtracUmbralFallbackCompletitud
                ?? umbralLegado
                ?? minFieldsRatio;
            umbralConfianza = umbralFallbackConfianzaRequest
                ?? confidenceConfig?.ExtracUmbralFallbackConfianza
                ?? umbralLegado
                ?? minFieldsRatio;

            return true;
        }

        var camposObtenidos = resultadoCu.DatosExtraidos.Keys
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        obtenidosEsperados = camposObtenidos.Count(camposEsperados.Contains);
        ratioCompletitud = (double)obtenidosEsperados / esperados;
        confianzaCu = resultadoCu.ConfianzaExtraccion;

        // Prioridad: request-específico > tipología-específico > umbral-legático > global
        umbralCompletitud = umbralFallbackCompletitudRequest
            ?? confidenceConfig?.ExtracUmbralFallbackCompletitud
            ?? umbralLegado
            ?? minFieldsRatio;
        umbralConfianza = umbralFallbackConfianzaRequest
            ?? confidenceConfig?.ExtracUmbralFallbackConfianza
            ?? umbralLegado
            ?? minFieldsRatio;

        return ratioCompletitud >= umbralCompletitud && confianzaCu >= umbralConfianza;
    }

    private static bool IsAzureContentUnderstandingProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-content-understanding" or "azure-cu" or "cu";

    private bool DebeUsarModoCombinado(TipologiaValidationConfig config)
    {
        if (!TryResolveFallbackModel(out var fallbackModel) || string.IsNullOrWhiteSpace(fallbackModel?.DeploymentName))
        {
            return false;
        }

        var prompt = config.PromptConfig;
        if (prompt == null || !prompt.Enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(prompt.ModelKey))
        {
            return false;
        }

        PromptModelConfig promptModel;
        try
        {
            promptModel = _promptModelRegistryLoader.GetModel(prompt.ModelKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudo resolver PromptModelConfig para modelKey={ModelKey}. Se omite optimización combinada.",
                prompt.ModelKey);
            return false;
        }

        return string.Equals(
            promptModel.DeploymentName,
            fallbackModel!.DeploymentName,
            StringComparison.OrdinalIgnoreCase);
    }

    private bool TryResolveFallbackModel(out ExtractionModelConfig? model)
    {
        try
        {
            model = _extractionModelRegistryLoader.GetFallbackModel();
            return true;
        }
        catch (KeyNotFoundException)
        {
            model = null;
            return false;
        }
    }
}