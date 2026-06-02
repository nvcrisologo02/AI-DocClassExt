using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

public class OpenAIPromptDataProvider : IPromptDataProvider
{
    private readonly TipologiaConfigLoader _tipologiaConfigLoader;
    private readonly PromptModelRegistryLoader _promptModelRegistryLoader;
    private readonly PromptDefaultsSettings _promptDefaults;
    private readonly ILogger<OpenAIPromptDataProvider> _logger;
    private readonly PromptTraceTelemetryService _promptTraceTelemetry;

    // Cache de clientes por endpoint/auth/deployment para evitar recrearlos en cada llamada
    private readonly Dictionary<string, ChatClient> _clientCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _clientCacheLock = new();

    public OpenAIPromptDataProvider(
        TipologiaConfigLoader tipologiaConfigLoader,
        PromptModelRegistryLoader promptModelRegistryLoader,
        IOptions<PromptDefaultsSettings> promptDefaults,
        PromptTraceTelemetryService promptTraceTelemetry,
        ILogger<OpenAIPromptDataProvider> logger)
    {
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _promptModelRegistryLoader = promptModelRegistryLoader;
        _promptDefaults = promptDefaults.Value;
        _promptTraceTelemetry = promptTraceTelemetry;
        _logger = logger;
    }

    public async Task<PromptResultado> EjecutarPromptAsync(
        PromptActivityInput input,
        CancellationToken cancellationToken = default)
    {
        TipologiaValidationConfig? tipologiaConfig = null;
        PromptConfig? tipologiaPromptConfig = null;

        try
        {
            tipologiaConfig = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
            tipologiaPromptConfig = tipologiaConfig.PromptConfig;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "No se encontró configuración publicada para tipología {Tipologia}. Se aplicará fallback de prompt con defaults/request.",
                input.Tipologia);
        }

        var tipologiaConfigEfectiva = tipologiaConfig ?? BuildFallbackTipologiaConfig(input.Tipologia);
        var defaultPromptConfig = _promptDefaults.ToPromptConfig();
        var effectivePromptConfig = ResolvePromptConfig(tipologiaPromptConfig, input.Prompt, defaultPromptConfig);
        var promptActivo = effectivePromptConfig is not null && effectivePromptConfig.Enabled && HasPromptDefinition(effectivePromptConfig);
        var necesitaPrompt = promptActivo && string.IsNullOrWhiteSpace(input.ResultadoPromptCombinado);
        var necesitaResumen = input.ForzarResumenPorDefecto && string.IsNullOrWhiteSpace(input.ResumenCombinado);

        if (!necesitaPrompt && !necesitaResumen)
        {
            _logger.LogInformation(
                "Prompt para tipología {Tipologia}: reutilizando resultados combinados previos.",
                input.Tipologia);

            return new PromptResultado
            {
                Modelo = FirstNonEmpty(effectivePromptConfig?.ModelKey, ResolveDefaultPromptModelKey()),
                Resultado = input.ResultadoPromptCombinado ?? string.Empty,
                Resumen = input.ResumenCombinado ?? string.Empty,
                TiempoMs = 0,
                CombinedWithFallback = true
            };
        }

        var promptConfig = necesitaPrompt ? effectivePromptConfig : null;
        if (!necesitaPrompt && !necesitaResumen)
        {
            return new PromptResultado { Error = "Prompt no habilitado para esta tipología." };
        }

        var modelKey = FirstNonEmpty(promptConfig?.ModelKey, ResolveDefaultPromptModelKey());
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            var mensaje = "PromptConfig requiere ModelKey para ejecutar el prompt.";
            _logger.LogError(mensaje);
            return new PromptResultado { Modelo = string.Empty, Error = mensaje };
        }

        PromptModelConfig modelConfig;
        try
        {
            modelConfig = _promptModelRegistryLoader.GetModel(modelKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo resolver el modelo de prompt para key={ModelKey}", modelKey);
            return new PromptResultado { Modelo = modelKey, Error = ex.Message };
        }

        _logger.LogInformation(
            "Ejecutando prompt para tipología {Tipologia} con modelKey={ModelKey} deployment={DeploymentName}.",
            input.Tipologia, modelKey, modelConfig.DeploymentName);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var chatClient = GetOrCreateClient(modelConfig);

            if (necesitaResumen)
            {
                var resultado = await EjecutarPromptJsonAsync(
                    chatClient,
                    modelConfig,
                    promptConfig,
                    input,
                    tipologiaConfigEfectiva,
                    necesitaPrompt,
                    ctsToken: cancellationToken);

                if (input.ForzarResumenPorDefecto && string.IsNullOrWhiteSpace(resultado.Resumen))
                {
                    resultado.Resumen = BuildFallbackSummary(input);
                    _logger.LogWarning(
                        "Prompt para tipología {Tipologia} devolvió resumen vacío. Se aplica resumen fallback por política ForzarResumenPorDefecto.",
                        input.Tipologia);
                }

                stopwatch.Stop();
                resultado.Modelo = modelConfig.DeploymentName;
                resultado.TiempoMs = (int)stopwatch.ElapsedMilliseconds;
                if (!string.IsNullOrWhiteSpace(input.ResultadoPromptCombinado) && string.IsNullOrWhiteSpace(resultado.Resultado))
                {
                    resultado.Resultado = input.ResultadoPromptCombinado;
                }
                return resultado;
            }

            var systemPrompt = string.IsNullOrWhiteSpace(promptConfig!.SystemPrompt)
                ? "Eres un asistente experto en análisis de documentos inmobiliarios y registrales españoles."
                : promptConfig.SystemPrompt;

            var userMessage = BuildUserMessage(promptConfig, input, tipologiaConfigEfectiva);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                userMessage
            };

            var options = new ChatCompletionOptions
            {
                Temperature = (float)promptConfig.Temperature,
                MaxOutputTokenCount = promptConfig.MaxTokens
            };

            var traceContenido = !string.IsNullOrWhiteSpace(input.MarkdownExtraido)
                ? input.MarkdownExtraido!
                : string.Empty;
            var userText = InterpolateTemplate(
                promptConfig.UserPromptTemplate,
                traceContenido,
                input.DatosExtraidos);

            _promptTraceTelemetry.TrackPrompt(
                provider: "gpt-prompt",
                operation: "prompt.resultado",
                tipologia: input.Tipologia,
                modelKey: modelConfig.Key,
                deployment: modelConfig.DeploymentName,
                systemPrompt: systemPrompt,
                userPrompt: userText);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(modelConfig.TimeoutSeconds));

            var response = await chatClient.CompleteChatAsync(messages, options, cts.Token);

            stopwatch.Stop();

            return new PromptResultado
            {
                Modelo = modelConfig.DeploymentName,
                Resultado = response.Value.Content[0].Text,
                TiempoMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error ejecutando prompt para tipología {Tipologia}.", input.Tipologia);
            return new PromptResultado
            {
                Modelo = modelConfig.DeploymentName,
                TiempoMs = (int)stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private static TipologiaValidationConfig BuildFallbackTipologiaConfig(string? tipologia)
    {
        var tipologiaValue = string.IsNullOrWhiteSpace(tipologia) ? "Desconocido" : tipologia.Trim();

        return new TipologiaValidationConfig
        {
            TipologiaId = tipologiaValue,
            TipologiaNombre = tipologiaValue,
            Version = "N/A"
        };
    }

    private async Task<PromptResultado> EjecutarPromptJsonAsync(
        ChatClient chatClient,
        PromptModelConfig modelConfig,
        PromptConfig? promptConfig,
        PromptActivityInput input,
        TipologiaValidationConfig tipologiaConfig,
        bool incluirPromptPropio,
        CancellationToken ctsToken)
    {
        var resumenConfig = _promptDefaults.ToPromptConfig();
        var contenido = !string.IsNullOrWhiteSpace(input.MarkdownExtraido)
            ? input.MarkdownExtraido!
            : string.Empty;

        var resumenInstruction = InterpolateTemplate(
            resumenConfig.UserPromptTemplate,
            contenido,
            input.DatosExtraidos);

        var systemPrompt = "Eres un analista documental experto. Devuelve exclusivamente JSON válido.";
        if (!string.IsNullOrWhiteSpace(resumenConfig.SystemPrompt))
        {
            systemPrompt += $"\n\nInstrucciones para resumen:\n{resumenConfig.SystemPrompt}";
        }
        if (incluirPromptPropio && promptConfig is not null && !string.IsNullOrWhiteSpace(promptConfig.SystemPrompt))
        {
            systemPrompt += $"\n\nInstrucciones para resultado_prompt:\n{promptConfig.SystemPrompt}";
        }

        var userText = "Devuelve un objeto JSON con la clave 'resumen' como string." +
            $"\n\nInstrucción para resumen:\n{resumenInstruction}";

        if (incluirPromptPropio && promptConfig is not null)
        {
            var promptInstruction = InterpolateTemplate(
                promptConfig.UserPromptTemplate,
                contenido,
                input.DatosExtraidos);

            userText += $"\n\nIncluye también la clave 'resultado_prompt' como string. Instrucción para resultado_prompt:\n{promptInstruction}";
        }

        if (!string.IsNullOrWhiteSpace(contenido))
        {
            userText += $"\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{contenido}";
        }

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = (float)resumenConfig.Temperature,
            MaxOutputTokenCount = Math.Max(resumenConfig.MaxTokens, promptConfig?.MaxTokens ?? 0)
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsToken);
        cts.CancelAfter(TimeSpan.FromSeconds(modelConfig.TimeoutSeconds));

        _promptTraceTelemetry.TrackPrompt(
            provider: "gpt-prompt",
            operation: "prompt.json",
            tipologia: input.Tipologia,
            modelKey: modelConfig.Key,
            deployment: modelConfig.DeploymentName,
            systemPrompt: systemPrompt,
            userPrompt: userText);

        var response = await chatClient.CompleteChatAsync(
            new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(ChatMessageContentPart.CreateTextPart(userText))
            },
            options,
            cts.Token);

        var text = response.Value.Content[0].Text;
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;

        return new PromptResultado
        {
            Resumen = FirstNonEmpty(
                ExtractString(root, "resumen"),
                ExtractString(root, "summary")) ?? string.Empty,
            Resultado = ExtractString(root, "resultado_prompt") ?? string.Empty
        };
    }

    private static string BuildFallbackSummary(PromptActivityInput input)
    {
        var tipologia = string.IsNullOrWhiteSpace(input.Tipologia) ? "desconocida" : input.Tipologia;
        return $"Documento clasificado como {tipologia}.";
    }

    private UserChatMessage BuildUserMessage(
        PromptConfig promptConfig,
        PromptActivityInput input,
        TipologiaValidationConfig tipologiaConfig)
    {
        // Determinar el contenido del documento a interpolar en {contenido}
        string contenido;
        if (!string.IsNullOrWhiteSpace(input.MarkdownExtraido))
        {
            contenido = input.MarkdownExtraido;
        }
        else if (!string.IsNullOrWhiteSpace(input.DocumentoBase64))
        {
            // Modo vision: el documento se adjunta como imagen; {contenido} se sustituye con un marcador
            contenido = "[ver documento adjunto]";
        }
        else
        {
            contenido = string.Empty;
        }

        var promptText = InterpolateTemplate(promptConfig.UserPromptTemplate, contenido, input.DatosExtraidos);

        // Si hay documento en base64 y el modo es vision (o no hay markdown), adjuntar como imagen
        if (!string.IsNullOrWhiteSpace(input.DocumentoBase64) &&
            string.IsNullOrWhiteSpace(input.MarkdownExtraido) &&
            string.Equals(promptConfig.ContentMode, "vision", StringComparison.OrdinalIgnoreCase))
        {
            var pdfBytes = Convert.FromBase64String(input.DocumentoBase64);
            var ct = string.IsNullOrWhiteSpace(input.ContentType) ? "application/pdf" : input.ContentType;

            if (!ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Prompt vision omitido para tipología {Tipologia}: ContentType {ContentType} no es imagen. Se envía solo texto.",
                    input.Tipologia,
                    ct);

                return new UserChatMessage(ChatMessageContentPart.CreateTextPart(promptText));
            }

            return new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(promptText),
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(pdfBytes), ct));
        }

        return new UserChatMessage(ChatMessageContentPart.CreateTextPart(promptText));
    }

    private static readonly Regex CampoPlaceholderRegex =
        new(@"\{campo:([^}]+)\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static PromptConfig? ResolvePromptConfig(
        PromptConfig? tipologiaPromptConfig,
        PromptInstrucciones? requestPrompt,
        PromptConfig? defaultPromptConfig = null)
    {
        // Si no hay ningún prompt configurado, no hay nada que hacer
        if (tipologiaPromptConfig is null && requestPrompt is null)
        {
            return null;
        }

        // Prompt ad-hoc: la tipología no tiene PromptConfig pero la petición trae instrucciones completas
        if (tipologiaPromptConfig is null)
        {
            return ApplyDefaults(new PromptConfig
            {
                Enabled = true,
                ModelKey = requestPrompt!.ModelKey ?? string.Empty,
                SystemPrompt = requestPrompt.SystemPrompt ?? string.Empty,
                UserPromptTemplate = requestPrompt.UserPromptTemplate ?? string.Empty,
                MaxTokens = requestPrompt.MaxTokens ?? 2000,
                Temperature = requestPrompt.Temperature ?? 0.0,
                ContentMode = requestPrompt.ContentMode ?? "markdown"
            }, defaultPromptConfig);
        }

        if (requestPrompt is null)
        {
            return ApplyDefaults(tipologiaPromptConfig, defaultPromptConfig);
        }

        // Override campo a campo: request tiene precedencia sobre tipología
        return ApplyDefaults(new PromptConfig
        {
            Enabled = tipologiaPromptConfig.Enabled,
            ModelKey = FirstNonEmpty(requestPrompt.ModelKey, tipologiaPromptConfig.ModelKey),
            SystemPrompt = FirstNonEmpty(requestPrompt.SystemPrompt, tipologiaPromptConfig.SystemPrompt),
            UserPromptTemplate = FirstNonEmpty(requestPrompt.UserPromptTemplate, tipologiaPromptConfig.UserPromptTemplate),
            MaxTokens = requestPrompt.MaxTokens ?? tipologiaPromptConfig.MaxTokens,
            Temperature = requestPrompt.Temperature ?? tipologiaPromptConfig.Temperature,
            ContentMode = FirstNonEmpty(requestPrompt.ContentMode, tipologiaPromptConfig.ContentMode)
        }, defaultPromptConfig);
    }

    public static PromptConfig ApplyDefaults(PromptConfig promptConfig, PromptConfig? defaultPromptConfig)
    {
        if (defaultPromptConfig is null)
        {
            return promptConfig;
        }

        return new PromptConfig
        {
            Enabled = promptConfig.Enabled,
            ModelKey = FirstNonEmpty(promptConfig.ModelKey, defaultPromptConfig.ModelKey),
            SystemPrompt = FirstNonEmpty(promptConfig.SystemPrompt, defaultPromptConfig.SystemPrompt),
            UserPromptTemplate = FirstNonEmpty(promptConfig.UserPromptTemplate, defaultPromptConfig.UserPromptTemplate),
            MaxTokens = promptConfig.MaxTokens > 0 ? promptConfig.MaxTokens : defaultPromptConfig.MaxTokens,
            Temperature = promptConfig.Temperature,
            ContentMode = FirstNonEmpty(promptConfig.ContentMode, defaultPromptConfig.ContentMode)
        };
    }

    private static string FirstNonEmpty(string? preferred, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        return fallback;
    }

    private string ResolveDefaultPromptModelKey()
    {
        return !string.IsNullOrWhiteSpace(_promptDefaults.ModelKey)
            ? _promptDefaults.ModelKey
            : "default.gpt4o-mini";
    }

    private static bool HasPromptDefinition(PromptConfig promptConfig)
    {
        return !string.IsNullOrWhiteSpace(promptConfig.SystemPrompt) ||
            !string.IsNullOrWhiteSpace(promptConfig.UserPromptTemplate);
    }

    private static string? ExtractString(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(root, propertyName, out var element))
        {
            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string propertyName, out JsonElement element)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                element = property.Value;
                return true;
            }
        }

        element = default;
        return false;
    }

    internal static string InterpolateTemplate(
        string template,
        string contenido,
        Dictionary<string, object> datos)
    {
        // Sustituir {contenido}
        var result = template.Replace("{contenido}", contenido, StringComparison.OrdinalIgnoreCase);

        // Sustituir {campo:NombreCampo}
        result = CampoPlaceholderRegex.Replace(result, match =>
        {
            var nombreCampo = match.Groups[1].Value;
            if (datos.TryGetValue(nombreCampo, out var valor) && valor is not null)
            {
                return valor.ToString() ?? string.Empty;
            }
            return string.Empty;
        });

        return result;
    }

    private ChatClient GetOrCreateClient(PromptModelConfig modelConfig)
    {
        var cacheKey = BuildCacheKey(modelConfig);

        lock (_clientCacheLock)
        {
            if (_clientCache.TryGetValue(cacheKey, out var existing))
            {
                return existing;
            }

            if (string.IsNullOrWhiteSpace(modelConfig.Endpoint))
            {
                throw new InvalidOperationException(
                    "PromptModelConfig.Endpoint es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(modelConfig.DeploymentName))
            {
                throw new InvalidOperationException(
                    "PromptModelConfig.DeploymentName es obligatorio.");
            }

            AzureOpenAIClient azureClient;
            if (string.Equals(modelConfig.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
            {
                azureClient = new AzureOpenAIClient(new Uri(modelConfig.Endpoint), new DefaultAzureCredential());
            }
            else
            {
                if (string.IsNullOrWhiteSpace(modelConfig.ApiKey))
                {
                    throw new InvalidOperationException(
                        "PromptModelConfig.ApiKey es obligatorio cuando AuthMode=ApiKey.");
                }

                azureClient = new AzureOpenAIClient(
                    new Uri(modelConfig.Endpoint),
                    new AzureKeyCredential(modelConfig.ApiKey));
            }

            var client = azureClient.GetChatClient(modelConfig.DeploymentName);
            _clientCache[cacheKey] = client;
            return client;
        }
    }

    private static string BuildCacheKey(PromptModelConfig modelConfig) =>
        $"{modelConfig.Endpoint}|{modelConfig.AuthMode}|{modelConfig.DeploymentName}";
}
