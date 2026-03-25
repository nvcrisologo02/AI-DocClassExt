using System.Diagnostics;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

public class OpenAIPromptDataProvider : IPromptDataProvider
{
    private readonly TipologiaConfigLoader _tipologiaConfigLoader;
    private readonly PromptModelRegistryLoader _promptModelRegistryLoader;
    private readonly ILogger<OpenAIPromptDataProvider> _logger;

    // Cache de clientes por endpoint/auth/deployment para evitar recrearlos en cada llamada
    private readonly Dictionary<string, ChatClient> _clientCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _clientCacheLock = new();

    public OpenAIPromptDataProvider(
        TipologiaConfigLoader tipologiaConfigLoader,
        PromptModelRegistryLoader promptModelRegistryLoader,
        ILogger<OpenAIPromptDataProvider> logger)
    {
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _promptModelRegistryLoader = promptModelRegistryLoader;
        _logger = logger;
    }

    public async Task<PromptResultado> EjecutarPromptAsync(
        PromptActivityInput input,
        CancellationToken cancellationToken = default)
    {
        // Modo optimizado: resultado ya calculado en la llamada combinada con el fallback
        if (!string.IsNullOrWhiteSpace(input.ResultadoPromptCombinado))
        {
            _logger.LogInformation(
                "Prompt para tipología {Tipologia}: reutilizando resultado combinado del fallback de extracción.",
                input.Tipologia);

            var config = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
            return new PromptResultado
            {
                Modelo = config.PromptConfig?.ModelKey ?? string.Empty,
                Resultado = input.ResultadoPromptCombinado,
                TiempoMs = 0,
                CombinedWithFallback = true
            };
        }

        var tipologiaConfig = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
        var promptConfig = tipologiaConfig.PromptConfig;

        if (promptConfig == null || !promptConfig.Enabled)
        {
            return new PromptResultado { Error = "Prompt no habilitado para esta tipología." };
        }

        if (string.IsNullOrWhiteSpace(promptConfig.ModelKey))
        {
            var mensaje = "PromptConfig requiere ModelKey para ejecutar el prompt.";
            _logger.LogError(mensaje);
            return new PromptResultado { Modelo = string.Empty, Error = mensaje };
        }

        PromptModelConfig modelConfig;
        try
        {
            modelConfig = _promptModelRegistryLoader.GetModel(promptConfig.ModelKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo resolver el modelo de prompt para key={ModelKey}", promptConfig.ModelKey);
            return new PromptResultado { Modelo = promptConfig.ModelKey, Error = ex.Message };
        }

        _logger.LogInformation(
            "Ejecutando prompt para tipología {Tipologia} con modelKey={ModelKey} deployment={DeploymentName}.",
            input.Tipologia, promptConfig.ModelKey, modelConfig.DeploymentName);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var chatClient = GetOrCreateClient(modelConfig);

            var systemMessage = new SystemChatMessage(
                string.IsNullOrWhiteSpace(promptConfig.SystemPrompt)
                    ? "Eres un asistente experto en análisis de documentos inmobiliarios y registrales españoles."
                    : promptConfig.SystemPrompt);

            var userMessage = BuildUserMessage(promptConfig, input, tipologiaConfig);

            var options = new ChatCompletionOptions
            {
                Temperature = (float)promptConfig.Temperature,
                MaxOutputTokenCount = promptConfig.MaxTokens
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(modelConfig.TimeoutSeconds));

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage> { systemMessage, userMessage },
                options,
                cts.Token);

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

    private UserChatMessage BuildUserMessage(
        PromptConfig promptConfig,
        PromptActivityInput input,
        TipologiaValidationConfig tipologiaConfig)
    {
        // Determinar el contenido del documento a interpolar en {contenido}
        string contenido;
        if (!string.IsNullOrWhiteSpace(input.MarkdownExtraido) &&
            !string.Equals(promptConfig.ContentMode, "vision", StringComparison.OrdinalIgnoreCase))
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
            (string.IsNullOrWhiteSpace(input.MarkdownExtraido) ||
             string.Equals(promptConfig.ContentMode, "vision", StringComparison.OrdinalIgnoreCase)))
        {
            var pdfBytes = Convert.FromBase64String(input.DocumentoBase64);
            var ct = string.IsNullOrWhiteSpace(input.ContentType) ? "application/pdf" : input.ContentType;
            return new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(promptText),
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(pdfBytes), ct));
        }

        return new UserChatMessage(ChatMessageContentPart.CreateTextPart(promptText));
    }

    private static readonly Regex CampoPlaceholderRegex =
        new(@"\{campo:([^}]+)\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
