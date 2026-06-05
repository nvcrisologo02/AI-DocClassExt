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
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = (float)model.Temperature,
            MaxOutputTokenCount = model.MaxTokens
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, model.TimeoutSeconds)));

        var response = await chatClient.CompleteChatAsync(
            new List<ChatMessage> { systemMessage, userMessage },
            options,
            cts.Token);

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
            ResultadoPromptCombinado = parsedResponse.ResultadoPrompt
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
