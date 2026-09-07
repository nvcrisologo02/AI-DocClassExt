using System.ClientModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

public class GptFallbackExtraerDataProvider
{
    /// <summary>
    /// Motivo estable registrado en FallbackRazon (y propagado a DetalleEjecucion/telemetría) cuando
    /// la propia llamada GPT (fallback o directa) agota su TimeoutSeconds configurado. Distingue este
    /// caso de una cancelación externa (caller) y de un fallo de CU que activa el fallback.
    /// </summary>
    public const string RazonExtraccionTimeout = "EXTRACCION_TIMEOUT_GPT";

    private readonly ExtractionModelRegistryLoader _modelRegistryLoader;
    private readonly PromptDefaultsSettings _promptDefaults;
    private readonly ILogger<GptFallbackExtraerDataProvider> _logger;
    private readonly Lazy<ExtractionModelConfig> _fallbackModel;
    private readonly Lazy<ChatClient> _chatClient;
    private readonly IGptPromptBuilder _promptBuilder;
    private readonly IGptJsonResponseParser _responseParser;
    private readonly IOpenAiClientFactory _clientFactory;

    public GptFallbackExtraerDataProvider(
        ExtractionModelRegistryLoader modelRegistryLoader,
        IOptions<PromptDefaultsSettings> promptDefaults,
        ILogger<GptFallbackExtraerDataProvider> logger,
        IGptPromptBuilder promptBuilder,
        IGptJsonResponseParser responseParser,
        IOpenAiClientFactory clientFactory)
    {
        _modelRegistryLoader = modelRegistryLoader;
        _promptDefaults = promptDefaults.Value;
        _logger = logger;
        _promptBuilder = promptBuilder;
        _responseParser = responseParser;
        _clientFactory = clientFactory;
        _fallbackModel = new Lazy<ExtractionModelConfig>(ResolveFallbackModel);
        _chatClient = new Lazy<ChatClient>(CreateChatClient);
    }

    public virtual async Task<ExtraccionResultado> ObtenerDatosConFallbackAsync(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        string? markdownContexto,
        CancellationToken cancellationToken = default)
    {
        var model = _fallbackModel.Value;
        _logger.LogInformation(
            "Iniciando fallback GPT para extracción. Tipología={Tipologia}, Deployment={Deployment}",
            input.Tipologia,
            model.DeploymentName);

        return await ExecuteExtractionAsync(
            input,
            tipologiaConfig,
            markdownContexto,
            model,
            isFallback: true,
            cancellationToken);
    }

    public virtual async Task<ExtraccionResultado> ObtenerDatosConModeloAsync(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        string modelKey,
        string? markdownContexto,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(modelKey);
        _logger.LogInformation(
            "Iniciando extracción GPT directa con modelo={ModelKey}, Deployment={Deployment}",
            modelKey,
            model.DeploymentName);

        return await ExecuteExtractionAsync(
            input,
            tipologiaConfig,
            markdownContexto,
            model,
            isFallback: false,
            cancellationToken);
    }

    /// <summary>
    /// Modo combinado: realiza una única llamada LLM que extrae campos Y ejecuta el prompt libre
    /// de la tipología en la misma petición. Ahorra una iteración cuando el fallback de extracción
    /// y el prompt comparten el mismo modelo.
    /// </summary>
    public virtual async Task<ExtraccionResultado> ObtenerDatosConFallbackYPromptAsync(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        PromptConfig promptConfig,
        string? markdownContexto,
        CancellationToken cancellationToken = default)
    {
        var model = _fallbackModel.Value;

        _logger.LogInformation(
            "Iniciando fallback GPT (modo combinado con prompt) para tipología={Tipologia}, Deployment={Deployment}",
            input.Tipologia,
            model.DeploymentName);

        return await ExecuteExtractionAsync(
            input,
            tipologiaConfig,
            markdownContexto,
            model,
            isFallback: true,
            cancellationToken,
            customPromptConfig: promptConfig);
    }

    /// <summary>
    /// Método privado unificado que consolida la lógica común de extracción para los 3 modos públicos.
    /// </summary>
    private async Task<ExtraccionResultado> ExecuteExtractionAsync(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        string? markdownContexto,
        ExtractionModelConfig model,
        bool isFallback,
        CancellationToken cancellationToken = default,
        PromptConfig? customPromptConfig = null)
    {
        var stopwatch = Stopwatch.StartNew();

        // Resolverresen prompt y configuración
        var resumenPrompt = ResolveResumenPrompt(input, markdownContexto);
        var promptMode = customPromptConfig is not null
            ? PromptMode.ExtractionWithFallback
            : (isFallback ? PromptMode.Extraction : PromptMode.Extraction);

        var systemText = _promptBuilder.BuildSystemPrompt(promptMode, resumenPrompt, customPromptConfig);
        var systemMessage = new SystemChatMessage(systemText);

        // Build user prompt
        var fieldList = _promptBuilder.BuildFieldCatalog(tipologiaConfig);
        var userPromptBase =
            $"Tipo de documento: {tipologiaConfig.TipologiaId} ({tipologiaConfig.TipologiaNombre})\n\n" +
            "Extrae los siguientes campos. Para cada uno se indica tipo, obligatoriedad y las reglas de validación " +
            "que debe cumplir el valor extraído (respétalas en el formato del dato devuelto):\n" +
            fieldList;

        var userPromptText = userPromptBase;

        // Add resume instruction if available
        if (resumenPrompt is not null)
        {
            userPromptText += $"\n\nInstrucción adicional para devolver en resumen:\n{resumenPrompt.UserPromptTemplate}";
        }

        // Handle combined mode with custom prompt
        if (customPromptConfig is not null)
        {
            userPromptText = $"**Parte 1 — Extracción de campos** ('campos_extraidos'):\n{userPromptBase}\n\n";
            
            if (resumenPrompt is not null)
            {
                userPromptText += $"**Parte 2 — Resumen por defecto** ('resumen'):\n{resumenPrompt.UserPromptTemplate}\n\n";
            }

            var promptInstruction = customPromptConfig.UserPromptTemplate
                .Replace("{contenido}", "[contenido del documento proporcionado en este mensaje]", StringComparison.OrdinalIgnoreCase);
            promptInstruction = System.Text.RegularExpressions.Regex.Replace(
                promptInstruction, "\\{campo:[^}]+\\}", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            userPromptText += $"**Parte 3 — Instrucción adicional** ('resultado_prompt'):\n{promptInstruction}";
        }

        // Get document context
        var contextoTexto = string.IsNullOrWhiteSpace(markdownContexto)
            ? ObtenerContextoTexto(input.DatosNormalizados)
            : markdownContexto;

        var userMessage = !string.IsNullOrWhiteSpace(contextoTexto)
            ? new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(
                    $"{userPromptText}\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{contextoTexto}"))
            : new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(
                    $"{userPromptText}\n\nNo hay contenido textual disponible. " +
                    $"Nombre de archivo: {input.Entrada.Documento.Name}."));

        if (string.IsNullOrWhiteSpace(contextoTexto))
        {
            _logger.LogWarning(
                "No hay contexto textual preprocesado para extracción en {Documento}. Se continuará con contexto mínimo.",
                input.Entrada.Documento.Name);
        }

        // Get chat client and call LLM
        var chatClient = _clientFactory.CreateClient(model);
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };
        OpenAiModelCapabilities.ConfigureChatOptions(options, model.DeploymentName, model.Temperature, model.MaxTokens);

        var timeoutSeconds = Math.Max(1, model.TimeoutSeconds);
        // Dos CTS separados (timeoutCts + linkedCts), igual que el hard timeout de
        // AzureContentUnderstandingProvider: permite distinguir en el catch si quien disparó la
        // cancelación fue el temporizador propio o el token del caller.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var messages = new List<ChatMessage> { systemMessage, userMessage };

        ClientResult<ChatCompletion> response;
        try
        {
            response = await InvokeChatCompletionAsync(chatClient, messages, options, linkedCts.Token);
        }
        catch (OperationCanceledException ex)
            when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // El timeoutCts propio (ligado a model.TimeoutSeconds) es quien disparó la cancelación,
            // no el caller: se trata de un timeout de negocio, no de un error técnico. Se devuelve un
            // resultado de extracción controlado (sin datos) en lugar de dejar que la excepción
            // tumbe la activity, siguiendo el mismo criterio que CuExtraccionException para CU.
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Timeout de {TimeoutSeconds}s superado en extracción GPT ({Modo}) para tipología={Tipologia}, Deployment={Deployment}. Se devuelve resultado de extracción controlado.",
                timeoutSeconds,
                isFallback ? "fallback" : "directa",
                input.Tipologia,
                model.DeploymentName);

            return BuildTimeoutResultado(model, isFallback, customPromptConfig, stopwatch);
        }

        stopwatch.Stop();

        // Parse response
        var responseText = response.Value.Content[0].Text;
        var parsedResponse = _responseParser.Parse(responseText, tipologiaConfig);

        // Build metrics
        var (confianzaCalculada, metricasDebug) = BuildFallbackMetricas(
            input,
            tipologiaConfig,
            parsedResponse.CamposExtraidos,
            parsedResponse.ConfianzaPorCampo);

        var tiempoKey = customPromptConfig is not null
            ? "gpt-fallback-combined"
            : (isFallback ? "gpt-fallback" : "gpt-direct");

        // Una sola llamada resuelve extraccion y, en modo combinado, tambien el
        // prompt o el resumen: se paga una vez y por tanto es un unico consumo.
        var operacionConsumo = customPromptConfig is not null
            ? "extraction.gpt.combined"
            : (isFallback ? "extraction.gpt.fallback" : "extraction.gpt.direct");

        return new ExtraccionResultado
        {
            Proveedor = "azure-openai",
            Modelo = model.DeploymentName,
            LayoutEnabled = false,
            FallbackUsado = isFallback,
            ConfianzaExtraccion = parsedResponse.ConfianzaExtraccionGpt ?? confianzaCalculada,
            ProveedorExtrac = "GPT4oMini",
            TiemposMs = new Dictionary<string, int>
            {
                [tiempoKey] = (int)stopwatch.ElapsedMilliseconds
            },
            MetricasDebug = metricasDebug,
            DatosExtraidos = parsedResponse.CamposExtraidos,
            ResumenCombinado = parsedResponse.Resumen,
            ResultadoPromptCombinado = parsedResponse.ResultadoPrompt,
            Consumos =
            {
                UsoOpenAiMapper.Mapear(
                    response.Value.Usage,
                    actividad: ActividadesIA.Extraer,
                    operacion: operacionConsumo,
                    modelo: model.DeploymentName)
            }
        };
    }

    /// <summary>
    /// Punto de invocación de la llamada al modelo, aislado como método virtual para poder
    /// sustituirlo en tests unitarios (simular timeouts/cancelaciones de forma determinista sin
    /// depender de red real ni de temporizadores).
    /// </summary>
    protected virtual Task<ClientResult<ChatCompletion>> InvokeChatCompletionAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken)
    {
        return chatClient.CompleteChatAsync(messages, options, cancellationToken);
    }

    /// <summary>
    /// Resultado de extracción controlado cuando la llamada GPT (fallback o directa) agota su propio
    /// timeout. Sin datos extraídos: la extracción NO se realizó (no es que se realizó con confianza
    /// 0), por eso ExtraccionTimeoutPropio=true para que el orquestador la excluya del cálculo de
    /// ConfianzaGlobal (igual que Extraction.Enabled=false), en vez de forzar ConfianzaGlobal=0 y
    /// EstadoCalidad="ERROR" de forma artificial. ConfianzaExtraccion=0 se conserva como dato
    /// informativo del componente. FallbackRazon queda marcado con <see cref="RazonExtraccionTimeout"/>
    /// para trazabilidad en DetalleEjecucion y telemetría.
    /// </summary>
    private static ExtraccionResultado BuildTimeoutResultado(
        ExtractionModelConfig model,
        bool isFallback,
        PromptConfig? customPromptConfig,
        Stopwatch stopwatch)
    {
        var tiempoKey = customPromptConfig is not null
            ? "gpt-fallback-combined"
            : (isFallback ? "gpt-fallback" : "gpt-direct");

        return new ExtraccionResultado
        {
            Proveedor = "azure-openai",
            Modelo = model.DeploymentName,
            LayoutEnabled = false,
            FallbackUsado = isFallback,
            FallbackRazon = RazonExtraccionTimeout,
            ConfianzaExtraccion = 0,
            ExtraccionTimeoutPropio = true,
            ProveedorExtrac = "GPT4oMini",
            TiemposMs = new Dictionary<string, int>
            {
                [tiempoKey] = (int)stopwatch.ElapsedMilliseconds
            },
            DatosExtraidos = new Dictionary<string, object>()
        };
    }

    private PromptConfig? ResolveResumenPrompt(ExtraccionInput input, string? markdownContexto)
    {
        if (!input.GenerarResumenPorDefecto)
        {
            return null;
        }

        var defaults = _promptDefaults.ToPromptConfig();
        if (string.IsNullOrWhiteSpace(defaults.UserPromptTemplate))
        {
            return null;
        }

        var contenido = string.IsNullOrWhiteSpace(markdownContexto)
            ? ObtenerContextoTexto(input.DatosNormalizados)
            : markdownContexto;

        return new PromptConfig
        {
            Enabled = true,
            ModelKey = defaults.ModelKey,
            SystemPrompt = defaults.SystemPrompt,
            UserPromptTemplate = OpenAIPromptDataProvider.InterpolateTemplate(
                defaults.UserPromptTemplate,
                contenido ?? string.Empty,
                input.DatosNormalizados),
            MaxTokens = defaults.MaxTokens,
            Temperature = defaults.Temperature,
            ContentMode = defaults.ContentMode
        };
    }

    private static string? ExtractString(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var element))
        {
            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
        }

        return null;
    }

    private (double Confianza, ConfidenceMetricasExtraccion Metricas) BuildFallbackMetricas(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        Dictionary<string, object> campos,
        Dictionary<string, double>? confianzaPorCampo)
    {
        var camposPresentes = campos.Keys
            .Count(k => !string.Equals(k, "Paginas", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(k, "Markdown", StringComparison.OrdinalIgnoreCase));

        var camposTotales = tipologiaConfig.Fields.Count;
        var camposRequeridos = tipologiaConfig.Fields.Count(f => f.Required);
        var camposRequeridosPresentes = tipologiaConfig.Fields
            .Where(f => f.Required)
            .Count(f => campos.ContainsKey(f.Name));
        var avoidConfidenceFields = ConfidenceFieldFilter.GetAvoidConfidenceFields(tipologiaConfig);

        var (confianzaCalculada, metricas) = ConfidenceCalculator.ExtracCU(
            fieldConfs: ConfidenceFieldFilter.FilterFieldConfidences(confianzaPorCampo, avoidConfidenceFields),
            camposPresentes: camposPresentes,
            camposTotales: camposTotales,
            camposRequeridos: camposRequeridos,
            camposRequeridosPresentes: camposRequeridosPresentes,
            warnings: 0,
            cfg: tipologiaConfig.ConfidenceConfig);

        metricas.ConfianzaPorCampo = ConfidenceFieldFilter.FilterConfidenceMap(
            confianzaPorCampo,
            avoidConfidenceFields);

        var umbralDuda = input.UmbralFallbackEfectivo
            ?? tipologiaConfig.ConfidenceConfig?.ExtracUmbralFallback
            ?? _fallbackModel.Value.MinFieldsRatio;

        metricas.CamposBajaConfianza = ConfidenceFieldFilter.GetLowConfidenceFields(
            metricas.ConfianzaPorCampo,
            umbralDuda,
            avoidConfidenceFields);
        metricas.CamposExcluidosConfianza = ConfidenceFieldFilter.ToSortedList(avoidConfidenceFields);

        return (confianzaCalculada, metricas);
    }

    private ChatClient CreateChatClient()
    {
        var model = _fallbackModel.Value;
        return _clientFactory.CreateClient(model);
    }

    public virtual ExtractionModelConfig ResolveModel(string modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            throw new InvalidOperationException("El modelKey de extracción GPT directa es obligatorio.");
        }

        var model = _modelRegistryLoader.GetModel(modelKey);
        if (!IsAzureOpenAiProvider(model.Provider))
        {
            throw new InvalidOperationException(
                $"El modelo de extracción '{model.Key}' debe ser de provider Azure OpenAI. Provider actual: '{model.Provider}'.");
        }

        return model;
    }

    private ExtractionModelConfig ResolveFallbackModel()
    {
        var model = _modelRegistryLoader.GetFallbackModel();
        if (!IsAzureOpenAiProvider(model.Provider))
        {
            throw new InvalidOperationException(
                $"El modelo de fallback de extracción '{model.Key}' debe ser de provider Azure OpenAI. Provider actual: '{model.Provider}'.");
        }

        return model;
    }

    private static bool IsAzureOpenAiProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-openai" or "gpt" or "openai";

    private static string? ObtenerContextoTexto(IDictionary<string, object> datosNormalizados)
    {
        if (datosNormalizados is null || datosNormalizados.Count == 0)
        {
            return null;
        }

        var claves = new[]
        {
            "Markdown",
            "markdown",
            "Texto",
            "texto",
            "ContentText",
            "contentText"
        };

        foreach (var clave in claves)
        {
            if (!datosNormalizados.TryGetValue(clave, out var raw) || raw is null)
            {
                continue;
            }

            if (raw is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }

            if (raw is JsonElement json && json.ValueKind == JsonValueKind.String)
            {
                var value = json.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
