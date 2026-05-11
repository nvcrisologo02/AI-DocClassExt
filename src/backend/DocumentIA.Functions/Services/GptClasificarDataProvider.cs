using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
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

/// <summary>
/// Proveedor de clasificación basado en un deployment configurable de Azure OpenAI.
/// Se usa como fallback cuando Azure Document Intelligence falla o devuelve confianza insuficiente.
/// </summary>
public class GptClasificarDataProvider : IClasificarDataProvider
{
    private readonly ClassificationModelRegistryLoader _modelRegistryLoader;
    private readonly ClassificationTipologiaPromptBuilder _tipologiaPromptBuilder;
    private readonly ILogger<GptClasificarDataProvider> _logger;
    private readonly Lazy<ClassificationModelConfig> _fallbackModel;
    private readonly Lazy<ChatClient> _chatClient;
    private readonly Lazy<string> _tipologiasPromptSection;

    public GptClasificarDataProvider(
        ClassificationModelRegistryLoader modelRegistryLoader,
        ClassificationTipologiaPromptBuilder tipologiaPromptBuilder,
        ILogger<GptClasificarDataProvider> logger)
    {
        _modelRegistryLoader = modelRegistryLoader;
        _tipologiaPromptBuilder = tipologiaPromptBuilder;
        _logger = logger;
        _fallbackModel = new Lazy<ClassificationModelConfig>(ResolveFallbackModel);
        _chatClient = new Lazy<ChatClient>(CreateChatClient);
        _tipologiasPromptSection = new Lazy<string>(() => _tipologiaPromptBuilder.Build());
    }

    public async Task<ResultadoClasificacion> ClasificarAsync(
        ClasificacionInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var model = _fallbackModel.Value;
        _logger.LogInformation(
            "Iniciando clasificación Azure OpenAI fallback para documento {Documento} con deployment {DeploymentName}",
            input.Entrada.Documento.Name,
            model.DeploymentName);

        var tipologias = _tipologiasPromptSection.Value;
        var contextoTexto = ObtenerContextoTexto(input.DatosNormalizados);

        var systemMessage = new SystemChatMessage(
            "Eres un sistema experto en clasificación de documentos del sector inmobiliario español, " +
            "especialmente documentos de SAREB (Sociedad de Gestión de Activos procedentes de la Reestructuración Bancaria). " +
            "Analiza el documento adjunto y clasifícalo en una de las tipologías indicadas. " +
            "Responde ÚNICAMENTE con un objeto JSON válido, sin texto adicional, " +
            "usando exactamente este formato: " +
            "{\"tipologia\": \"<tipologiaId>\", \"confianza\": <número entre 0.0 y 1.0>, \"razon\": \"<explicación breve en español>\"}");

        var textoUsuario =
            $"Clasifica este documento entre las siguientes tipologías conocidas:\n\n{tipologias}\n\n" +
            "Si el documento no corresponde a ninguna tipología conocida, " +
            "responde con tipologiaId=\"Desconocido\" y confianza inferior a 0.3.";

        if (!string.IsNullOrWhiteSpace(contextoTexto))
        {
            textoUsuario += $"\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{contextoTexto}";
        }
        else
        {
            _logger.LogWarning(
                "No hay contexto textual preprocesado para el fallback de clasificación en {Documento}. Se continuará con contexto mínimo.",
                input.Entrada.Documento.Name);

            textoUsuario +=
                $"\n\nNo hay contenido textual disponible para este fallback. " +
                $"Nombre de archivo: {input.Entrada.Documento.Name}.";
        }

        var userMessage = new UserChatMessage(ChatMessageContentPart.CreateTextPart(textoUsuario));

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = (float)model.Temperature,
            MaxOutputTokenCount = model.MaxTokens
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, model.TimeoutSeconds)));

        var response = await _chatClient.Value.CompleteChatAsync(
            new List<ChatMessage> { systemMessage, userMessage },
            options,
            cts.Token);

        stopwatch.Stop();
        var responseText = response.Value.Content[0].Text;

        _logger.LogDebug(
            "Respuesta GPT clasificación ({Ms}ms): {Response}",
            stopwatch.ElapsedMilliseconds,
            responseText);

        return ParseGptResponse(responseText, model);
    }

    private ResultadoClasificacion ParseGptResponse(string responseText, ClassificationModelConfig model)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            var tipologia = root.TryGetProperty("tipologia", out var tp) ? tp.GetString() : null;
            var confianza = root.TryGetProperty("confianza", out var conf)
                && conf.TryGetDouble(out var c) ? c : 0.0;

            if (!string.IsNullOrWhiteSpace(tipologia))
            {
                return new ResultadoClasificacion
                {
                    Modelo = model.DeploymentName,
                    TipologiaDetectada = tipologia,
                    Confianza = Math.Clamp(confianza, 0.0, 1.0),
                    ConfianzaGPT = Math.Clamp(confianza, 0.0, 1.0),
                    ProveedorClasif = "GPT4oMini"
                };
            }
        }
        catch (JsonException)
        {
            _logger.LogWarning(
                "Respuesta GPT no es JSON válido. Intentando extracción regex. Respuesta: {Response}",
                responseText);
        }

        // Fallback regex si el JSON está malformado
        var tipologiaMatch = Regex.Match(responseText, @"""tipologia""\s*:\s*""([^""]+)""");
        var confianzaMatch = Regex.Match(responseText, @"""confianza""\s*:\s*([0-9]*\.?[0-9]+)");

        var tipologiaFallback = tipologiaMatch.Success
            ? tipologiaMatch.Groups[1].Value
            : "Desconocido";
        var confianzaFallback = confianzaMatch.Success
            && double.TryParse(confianzaMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cf)
            ? cf : 0.1;

        _logger.LogWarning(
            "Tipología extraída via regex: {Tipologia} (confianza: {Confianza:F3})",
            tipologiaFallback,
            confianzaFallback);

        return new ResultadoClasificacion
        {
            Modelo = model.DeploymentName,
            TipologiaDetectada = tipologiaFallback,
            Confianza = Math.Clamp(confianzaFallback, 0.0, 1.0),
            ConfianzaGPT = Math.Clamp(confianzaFallback, 0.0, 1.0),
            ProveedorClasif = "GPT4oMini"
        };
    }

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

    private ChatClient CreateChatClient()
    {
        var model = _fallbackModel.Value;

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            throw new InvalidOperationException(
                $"ClassificationModelConfig.Endpoint es obligatorio para el modelo de fallback '{model.Key}'.");
        }

        if (string.IsNullOrWhiteSpace(model.DeploymentName))
        {
            throw new InvalidOperationException(
                $"ClassificationModelConfig.DeploymentName es obligatorio para el modelo de fallback '{model.Key}'.");
        }

        AzureOpenAIClient azureClient;

        if (string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            azureClient = new AzureOpenAIClient(new Uri(model.Endpoint), new DefaultAzureCredential());
        }
        else
        {
            if (string.IsNullOrWhiteSpace(model.ApiKey))
            {
                throw new InvalidOperationException(
                    $"ClassificationModelConfig.ApiKey es obligatorio para el modelo de fallback '{model.Key}' cuando AuthMode=ApiKey.");
            }
            azureClient = new AzureOpenAIClient(
                new Uri(model.Endpoint),
                new AzureKeyCredential(model.ApiKey));
        }

        return azureClient.GetChatClient(model.DeploymentName);
    }

    private ClassificationModelConfig ResolveFallbackModel()
    {
        var model = _modelRegistryLoader.GetFallbackModel();
        if (!IsAzureOpenAiProvider(model.Provider))
        {
            throw new InvalidOperationException(
                $"El modelo de fallback '{model.Key}' debe ser de provider Azure OpenAI. Provider actual: '{model.Provider}'.");
        }

        return model;
    }

    private static bool IsAzureOpenAiProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-openai" or "gpt" or "openai";
}
