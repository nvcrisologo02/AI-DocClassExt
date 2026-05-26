using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services.Classification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentIA.Core.Configuration;
using System.Globalization;

namespace DocumentIA.Functions.Services;

public class ConfigurableClasificarDataProvider : IClasificarDataProvider
{
    private readonly MockClasificarDataProvider _mockProvider;
    private readonly AzureDocumentIntelligenceClasificarProvider _azureProvider;
    private readonly GptClasificarDataProvider _gptProvider;
    private readonly HybridTdnClasificarProvider _hybridTdnProvider;
    private readonly RuleBasedTdnClassifier _ruleClassifier;
    private readonly DocumentWindowExtractor _windowExtractor;
    private readonly HybridTdnOptions _hybridOptions;
    private readonly ClassificationModelRegistryLoader _modelRegistryLoader;
    private readonly ClassificationRoutingSettings _routingSettings;
    private readonly ILogger<ConfigurableClasificarDataProvider> _logger;

    public ConfigurableClasificarDataProvider(
        MockClasificarDataProvider mockProvider,
        AzureDocumentIntelligenceClasificarProvider azureProvider,
        GptClasificarDataProvider gptProvider,
        HybridTdnClasificarProvider hybridTdnProvider,
        RuleBasedTdnClassifier ruleClassifier,
        DocumentWindowExtractor windowExtractor,
        IOptions<HybridTdnOptions> hybridOptions,
        ClassificationModelRegistryLoader modelRegistryLoader,
        IOptions<ClassificationRoutingSettings> routingSettings,
        ILogger<ConfigurableClasificarDataProvider> logger)
    {
        _mockProvider = mockProvider;
        _azureProvider = azureProvider;
        _gptProvider = gptProvider;
        _hybridTdnProvider = hybridTdnProvider;
        _ruleClassifier = ruleClassifier;
        _windowExtractor = windowExtractor;
        _hybridOptions = hybridOptions.Value;
        _modelRegistryLoader = modelRegistryLoader;
        _routingSettings = routingSettings.Value;
        _logger = logger;
    }

    public async Task<ResultadoClasificacion> ClasificarAsync(
        ClasificacionInput input,
        CancellationToken cancellationToken = default)
    {
        var requestedProvider = input.Entrada.Instrucciones.Classification.Provider;
        var resolvedFlow = ResolveFlowName(requestedProvider);
        var flowProviders = ResolveFlowProviders(resolvedFlow);

        _logger.LogInformation(
            "Flujo de clasificación resuelto: {Flow}. Providers: {Providers}. Model solicitado: {ModelInstruction}",
            resolvedFlow,
            string.Join(" -> ", flowProviders),
            input.Entrada.Instrucciones.Classification.Model);

        var evaluated = new List<ResultadoClasificacion>();

        foreach (var provider in flowProviders)
        {
            var providerResult = await ExecuteProviderAsync(provider, input, cancellationToken);
            evaluated.Add(providerResult);

            if (IsSatisfactory(providerResult, input))
            {
                providerResult.UmbralFallbackAplicado = ResolveFallbackThreshold(input);
                providerResult.FallbackLLM = false;
                providerResult.FallbackRazon = null;
                providerResult.DetalleProveedores = BuildDetalle(evaluated, null);
                return providerResult;
            }

            _logger.LogInformation(
                "Provider {Provider} no satisfactorio. Tipologia={Tipologia}, Confianza={Confianza:F3}. Continuando flujo.",
                provider,
                providerResult.TipologiaDetectada,
                providerResult.Confianza);
        }

        ClassificationModelConfig? fallbackModel = null;
        var globalFallbackEnabled = _routingSettings.UseGlobalFallback && TryResolveFallbackModel(out fallbackModel);
        var umbralFallback = input.UmbralFallbackEfectivo ?? fallbackModel?.FallbackThreshold ?? 0.6;

        if (!globalFallbackEnabled)
        {
            var best = evaluated.LastOrDefault() ?? BuildUnknownResult("no_provider_executed");
            best.UmbralFallbackAplicado = umbralFallback;
            best.FallbackLLM = false;
            best.FallbackRazon = "fallback_global_disabled";
            best.DetalleProveedores = BuildDetalle(evaluated, "sin_resultado_satisfactorio");
            return best;
        }

        var fallbackProvider = ResolveGlobalFallbackProvider();

        // No ejecutar GlobalFallback si el flujo ya incluyó ese mismo provider.
        // Evita llamadas dobles a GPT cuando provider="gpt" y GlobalFallbackProvider="gpt".
        if (flowProviders.Any(p => string.Equals(p, fallbackProvider, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogInformation(
                "GlobalFallback omitido: el flujo {Flow} ya ejecutó el provider de fallback '{FallbackProvider}'.",
                resolvedFlow,
                fallbackProvider);
            var best = evaluated.LastOrDefault() ?? BuildUnknownResult("no_provider_executed");
            best.UmbralFallbackAplicado = umbralFallback;
            best.FallbackLLM = false;
            best.DetalleProveedores = BuildDetalle(evaluated, "sin_resultado_satisfactorio");
            return best;
        }

        _logger.LogWarning(
            "Ningún provider del flujo {Flow} fue satisfactorio. Ejecutando fallback global: {FallbackProvider}",
            resolvedFlow,
            fallbackProvider);

        try
        {
            var fallbackResult = await ExecuteProviderAsync(fallbackProvider, input, cancellationToken);
            fallbackResult.FallbackLLM = true;
            fallbackResult.FallbackRazon = "global_fallback_final";
            fallbackResult.UmbralFallbackAplicado = umbralFallback;

            evaluated.Add(fallbackResult);
            fallbackResult.DetalleProveedores = BuildDetalle(evaluated, "sin_resultado_satisfactorio");

            if (!IsSatisfactory(fallbackResult, input))
            {
                fallbackResult.TipologiaDetectada = "Desconocido";
                fallbackResult.FallbackRazon = "global_fallback_sin_clasificacion";
            }

            return fallbackResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback global {FallbackProvider} falló.", fallbackProvider);
            var best = evaluated.LastOrDefault() ?? BuildUnknownResult("no_provider_executed");
            best.UmbralFallbackAplicado = umbralFallback;
            best.FallbackLLM = false;
            best.FallbackRazon = $"global_fallback_failed:{ex.GetType().Name}";
            best.DetalleProveedores = BuildDetalle(evaluated, "sin_resultado_satisfactorio");
            return best;
        }
    }

    private string ResolveFlowName(string? requestedProvider)
    {
        if (string.IsNullOrWhiteSpace(requestedProvider)
            || string.Equals(requestedProvider, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(_routingSettings.DefaultFlow)
                ? _routingSettings.DefaultFlow
                : _routingSettings.DefaultProvider;
        }

        return requestedProvider;
    }

    private List<string> ResolveFlowProviders(string flowName)
    {
        if (_routingSettings.Flows.TryGetValue(flowName, out var flow)
            && flow.Providers.Count > 0)
        {
            return flow.Providers;
        }

        return [flowName];
    }

    private string ResolveGlobalFallbackProvider()
    {
        if (!string.IsNullOrWhiteSpace(_routingSettings.GlobalFallbackProvider))
        {
            return _routingSettings.GlobalFallbackProvider;
        }

        return "gpt";
    }

    private double ResolveFallbackThreshold(ClasificacionInput input)
    {
        return input.UmbralFallbackEfectivo
            ?? (TryResolveFallbackModel(out var fallbackModel) ? fallbackModel?.FallbackThreshold : null)
            ?? 0.6;
    }

    private async Task<ResultadoClasificacion> ExecuteProviderAsync(
        string provider,
        ClasificacionInput input,
        CancellationToken cancellationToken)
    {
        return provider.ToLowerInvariant() switch
        {
            "mock" => await _mockProvider.ClasificarAsync(input, cancellationToken),
            "azure-openai" or "gpt" => await _gptProvider.ClasificarAsync(input, cancellationToken),
            "hybrid-tdn" or "hybrid" => await _hybridTdnProvider.ClasificarAsync(input, cancellationToken),
            "azure-document-intelligence" or "azure-di" or "di" => await _azureProvider.ClasificarAsync(input, cancellationToken),
            "rules" or "reglas" => ExecuteRulesProvider(input),
            _ => throw new NotSupportedException($"Proveedor de clasificación '{provider}' no soportado")
        };
    }

    private ResultadoClasificacion ExecuteRulesProvider(ClasificacionInput input)
    {
        var window = _windowExtractor.ExtractWindow(
            input,
            _hybridOptions.MaxCharactersPerWindow,
            _hybridOptions.PagesToInspect);

        var ruleResult = _ruleClassifier.Classify(window);

        var resultado = new ResultadoClasificacion
        {
            Modelo = "rules-v1",
            ProveedorClasif = "Reglas",
            TipologiaDetectada = ruleResult.TipologiaDetectada,
            Confianza = ruleResult.Confianza,
            ConfianzaDI = ruleResult.Confianza,
            ContentExtraido = window.ExtractedText,
            Clasificador = "RuleBasedTDN"
        };

        return resultado;
    }

    private bool IsSatisfactory(ResultadoClasificacion resultado, ClasificacionInput input)
    {
        var threshold = input.UmbralFallbackEfectivo ?? 0.6;

        if (string.IsNullOrWhiteSpace(resultado.TipologiaDetectada))
        {
            return false;
        }

        if (string.Equals(resultado.TipologiaDetectada, "Desconocido", StringComparison.OrdinalIgnoreCase)
            || string.Equals(resultado.TipologiaDetectada, "RESTO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return resultado.Confianza >= threshold;
    }

    private List<PropuestaProveedor> BuildDetalle(List<ResultadoClasificacion> evaluated, string? baseDiscardReason)
    {
        var result = new List<PropuestaProveedor>();

        for (var i = 0; i < evaluated.Count; i++)
        {
            var item = evaluated[i];
            var providerName = string.IsNullOrWhiteSpace(item.ProveedorClasif) ? "desconocido" : item.ProveedorClasif;
            var isLast = i == evaluated.Count - 1;

            result.Add(new PropuestaProveedor
            {
                Proveedor = providerName,
                Tipologia = item.TipologiaDetectada,
                Confianza = item.Confianza,
                MotivoDescarte = isLast && string.IsNullOrWhiteSpace(baseDiscardReason)
                    ? null
                    : $"{baseDiscardReason ?? "no_satisfactorio"}:{item.Confianza.ToString("F3", CultureInfo.InvariantCulture)}"
            });
        }

        return result;
    }

    private static ResultadoClasificacion BuildUnknownResult(string reason)
    {
        return new ResultadoClasificacion
        {
            Modelo = "n/a",
            ProveedorClasif = "None",
            TipologiaDetectada = "Desconocido",
            Confianza = 0.0,
            FallbackRazon = reason
        };
    }

    private bool TryResolveFallbackModel(out ClassificationModelConfig? model)
    {
        try
        {
            model = _modelRegistryLoader.GetFallbackModel();
            return true;
        }
        catch (KeyNotFoundException)
        {
            model = null;
            return false;
        }
    }

}

