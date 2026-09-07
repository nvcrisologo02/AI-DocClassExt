using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;

namespace DocumentIA.Functions.Services;

public class ConfigurableExtraerDataProvider : IExtraerDataProvider
{
    private readonly TipologiaConfigLoader _tipologiaConfigLoader;
    private readonly MockExtraerDataProvider _mockProvider;
    private readonly AzureContentUnderstandingProvider _azureProvider;
    private readonly AzureDocumentIntelligenceExtraerDataProvider _diExtraerProvider;
    private readonly GptDirectExtraerDataProvider _gptDirectProvider;
    private readonly GptFallbackExtraerDataProvider _gptFallbackProvider;
    private readonly ILayoutMarkdownProvider _layoutMarkdownProvider;
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
        ILayoutMarkdownProvider layoutMarkdownProvider,
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
        _layoutMarkdownProvider = layoutMarkdownProvider;
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

        // AB#100192: una tipología sin bloque 'extraction' hereda Enabled=true y ModelKey vacío;
        // con el DefaultProvider en CU, GetModel("") reventaba con KeyNotFoundException y la
        // ejecución cerraba EXTRACCION_INCOMPLETA tras un fallback GPT sin campos que extraer.
        // Sin ningún modelo utilizable, la semántica es "no requiere extracción": se degrada
        // igual que Enabled=false, sin llamar a ningún proveedor.
        if (IsAzureContentUnderstandingProvider(provider)
            && string.IsNullOrWhiteSpace(input.ModelKeyEfectivo)
            && string.IsNullOrWhiteSpace(config.Extraction.ModelKey)
            && string.IsNullOrWhiteSpace(config.Extraction.SecondaryModelKey))
        {
            _logger.LogWarning(
                "Tipología {Tipologia} sin modelKey de extracción configurado (provider {Provider}). " +
                "Se trata como extracción no configurada y se devuelve resultado vacío.",
                input.Tipologia,
                provider);

            return new ExtraccionResultado
            {
                Proveedor = "none",
                Modelo = "sin-configurar",
                LayoutEnabled = false,
                DatosExtraidos = new Dictionary<string, object>()
            };
        }

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

        // Consumo de todo lo ejecutado antes del fallback (Content Understanding y el
        // layout de contexto). Se devuelve el resultado del fallback, asi que sin esto
        // el gasto del proveedor mas caro del pipeline desapareceria (AB#100227).
        var consumosPrevios = new List<ConsumoIA>();

        try
        {
            resultadoCu = await _azureProvider.ObtenerDatosAsync(input, cancellationToken);
            consumosPrevios.AddRange(resultadoCu.Consumos);

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
        catch (CuExtraccionException ex)
        {
            fallbackRazon = $"exception:{ex.RazonTipo}:cuModelKey={ex.ModelKey}";
            _logger.LogWarning(
                ex,
                "Extracción CU falló para {Tipologia} con modelKey {ModelKey} ({RazonTipo}). Activando fallback GPT.",
                input.Tipologia,
                ex.ModelKey,
                ex.RazonTipo);
        }
        catch (Exception ex)
        {
            // AB#100192: se conserva el mensaje (truncado) — solo con el tipo, diagnosticar un
            // KeyNotFoundException exigió reconstruir a mano qué clave faltaba.
            var mensaje = ex.Message.Length > 120 ? ex.Message[..120] : ex.Message;
            fallbackRazon = $"exception:{ex.GetType().Name}:{mensaje}";
            _logger.LogWarning(ex, "Extracción CU falló para {Tipologia}. Activando fallback GPT.", input.Tipologia);
        }

        var markdownContexto = resultadoCu?.MarkdownExtraido;
        var paginasLayout = 0;

        // Si CU no dejó markdown y la normalización tampoco trae texto, generar contexto
        // con DI prebuilt-layout: sin él, el LLM de fallback no tiene documento que leer.
        if (string.IsNullOrWhiteSpace(markdownContexto) && !TieneContextoTextual(input.DatosNormalizados))
        {
            try
            {
                var layout = await _layoutMarkdownProvider.ExtraerMarkdownAsync(
                    new ExtraerMarkdownLayoutInput
                    {
                        Tipologia = input.Tipologia,
                        DocumentoBase64 = input.Entrada.Documento.Content?.Base64 ?? string.Empty,
                        NombreDocumento = input.Entrada.Documento.Name,
                        BlobPath = input.Entrada.Documento.BlobPath
                    },
                    cancellationToken);

                markdownContexto = layout.Markdown;
                paginasLayout = layout.Paginas;
                // Quinto punto de llamada a layout, fuera de los del orquestador:
                // factura sus paginas igual (AB#100229).
                consumosPrevios.AddRange(layout.Consumos);

                _logger.LogInformation(
                    "Contexto de fallback generado con DI layout para {Tipologia}. Longitud={Length}, Paginas={Paginas}",
                    input.Tipologia,
                    markdownContexto?.Length ?? 0,
                    paginasLayout);
            }
            catch (Exception layoutEx)
            {
                _logger.LogWarning(
                    layoutEx,
                    "No se pudo generar markdown de layout para el fallback de {Tipologia}. Se continúa sin contexto.",
                    input.Tipologia);
            }
        }

        ExtraccionResultado resultadoGpt;
        var promptConfig = HasPromptDefinition(config.PromptConfig)
            ? OpenAIPromptDataProvider.ResolvePromptConfig(config.PromptConfig, null)
            : null;

        // Optimización: si prompt está habilitado y usa el mismo modelo/deployment que el fallback,
        // se ejecuta extracción + prompt en una única llamada LLM.
        if (promptConfig is not null && DebeUsarModoCombinado(promptConfig))
        {
            _logger.LogInformation(
                "Activando modo combinado fallback+prompt para tipología {Tipologia}.",
                input.Tipologia);

            resultadoGpt = await _gptFallbackProvider.ObtenerDatosConFallbackYPromptAsync(
                input,
                config,
                promptConfig,
                markdownContexto,
                cancellationToken);
        }
        else
        {
            resultadoGpt = await _gptFallbackProvider.ObtenerDatosConFallbackAsync(
                input,
                config,
                markdownContexto,
                cancellationToken);
        }

        var paginasContexto = resultadoCu?.Paginas ?? 0;
        if (paginasContexto <= 0)
        {
            paginasContexto = paginasLayout;
        }

        if (resultadoGpt.Paginas <= 0 && paginasContexto > 0)
        {
            resultadoGpt.Paginas = paginasContexto;
        }

        if (string.IsNullOrWhiteSpace(resultadoGpt.MarkdownExtraido))
        {
            resultadoGpt.MarkdownExtraido = markdownContexto;
        }

        resultadoGpt.FallbackUsado = true;
        // Si el propio fallback GPT devolvió un motivo (p.ej. su timeout propio), se conserva y se
        // antepone la razón que activó el fallback CU->GPT, en lugar de sobreescribirla: ambas son
        // relevantes para el diagnóstico ("por qué se activó el fallback" + "por qué el fallback no
        // completó").
        resultadoGpt.FallbackRazon = string.IsNullOrWhiteSpace(resultadoGpt.FallbackRazon)
            ? fallbackRazon
            : $"{fallbackRazon};{resultadoGpt.FallbackRazon}";

        // Content Understanding y el layout de contexto se han pagado aunque su
        // resultado se descarte en favor del fallback.
        ConsumosIA.Fusionar(resultadoGpt.Consumos, consumosPrevios, marcarDescartados: true);

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

    private bool DebeUsarModoCombinado(PromptConfig prompt)
    {
        if (!TryResolveFallbackModel(out var fallbackModel) || string.IsNullOrWhiteSpace(fallbackModel?.DeploymentName))
        {
            return false;
        }

        if (!prompt.Enabled)
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

    private static bool HasPromptDefinition(PromptConfig? promptConfig)
    {
        return promptConfig is not null && promptConfig.Enabled &&
            (!string.IsNullOrWhiteSpace(promptConfig.SystemPrompt) ||
             !string.IsNullOrWhiteSpace(promptConfig.UserPromptTemplate));
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

    private static bool TieneContextoTextual(IDictionary<string, object> datosNormalizados)
    {
        if (datosNormalizados is null || datosNormalizados.Count == 0)
        {
            return false;
        }

        // Mismas claves que GptFallbackExtraerDataProvider.ObtenerContextoTexto
        var claves = new[] { "Markdown", "markdown", "Texto", "texto", "ContentText", "contentText" };

        foreach (var clave in claves)
        {
            if (!datosNormalizados.TryGetValue(clave, out var raw) || raw is null)
            {
                continue;
            }

            if (raw is string s && !string.IsNullOrWhiteSpace(s))
            {
                return true;
            }

            if (raw is JsonElement json
                && json.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(json.GetString()))
            {
                return true;
            }
        }

        return false;
    }
}