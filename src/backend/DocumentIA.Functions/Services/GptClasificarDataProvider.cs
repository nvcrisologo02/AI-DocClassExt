using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Proveedor de clasificación basado en un deployment configurable de Azure OpenAI.
/// Se usa como fallback cuando Azure Document Intelligence falla o devuelve confianza insuficiente.
/// </summary>
public class GptClasificarDataProvider : IClasificarDataProvider
{
    private readonly GptClasificarSettings _settings;
    private readonly ILogger<GptClasificarDataProvider> _logger;
    private readonly Lazy<ChatClient> _chatClient;
    private readonly Lazy<string> _tipologiasPromptSection;

    public GptClasificarDataProvider(
        IOptions<GptClasificarSettings> settings,
        string tipologiasConfigPath,
        ILogger<GptClasificarDataProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _chatClient = new Lazy<ChatClient>(CreateChatClient);
        _tipologiasPromptSection = new Lazy<string>(() => BuildTipologiasPromptSection(tipologiasConfigPath));
    }

    public async Task<ResultadoClasificacion> ClasificarAsync(
        ClasificacionInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Iniciando clasificación Azure OpenAI fallback para documento {Documento} con deployment {DeploymentName}",
            input.Entrada.Documento.Name,
            _settings.DeploymentName);

        var pdfBytes = Convert.FromBase64String(input.Entrada.Documento.Content.Base64);
        var tipologias = _tipologiasPromptSection.Value;

        var systemMessage = new SystemChatMessage(
            "Eres un sistema experto en clasificación de documentos del sector inmobiliario español, " +
            "especialmente documentos de SAREB (Sociedad de Gestión de Activos procedentes de la Reestructuración Bancaria). " +
            "Analiza el documento adjunto y clasifícalo en una de las tipologías indicadas. " +
            "Responde ÚNICAMENTE con un objeto JSON válido, sin texto adicional, " +
            "usando exactamente este formato: " +
            "{\"tipologia\": \"<tipologiaId>\", \"confianza\": <número entre 0.0 y 1.0>, \"razon\": \"<explicación breve en español>\"}");

        var userMessage = new UserChatMessage(
            ChatMessageContentPart.CreateTextPart(
                $"Clasifica este documento entre las siguientes tipologías conocidas:\n\n{tipologias}\n\n" +
                "Si el documento no corresponde a ninguna tipología conocida, " +
                "responde con tipologiaId=\"Desconocido\" y confianza inferior a 0.3."),
            ChatMessageContentPart.CreateImagePart(
                BinaryData.FromBytes(pdfBytes), "application/pdf"));

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = (float)_settings.Temperature,
            MaxOutputTokenCount = _settings.MaxTokens
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

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

        return ParseGptResponse(responseText);
    }

    private ResultadoClasificacion ParseGptResponse(string responseText)
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
                    Modelo = _settings.DeploymentName,
                    TipologiaDetectada = tipologia,
                    Confianza = Math.Clamp(confianza, 0.0, 1.0)
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
            Modelo = _settings.DeploymentName,
            TipologiaDetectada = tipologiaFallback,
            Confianza = Math.Clamp(confianzaFallback, 0.0, 1.0)
        };
    }

    private ChatClient CreateChatClient()
    {
        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            throw new InvalidOperationException(
                "Classification:GptFallback:Endpoint es obligatorio cuando el fallback GPT está habilitado.");
        }

        if (string.IsNullOrWhiteSpace(_settings.DeploymentName))
        {
            throw new InvalidOperationException(
                "Classification:GptFallback:DeploymentName es obligatorio cuando el fallback GPT está habilitado.");
        }

        AzureOpenAIClient azureClient;

        if (string.Equals(_settings.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            azureClient = new AzureOpenAIClient(new Uri(_settings.Endpoint), new DefaultAzureCredential());
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException(
                    "Classification:GptFallback:ApiKey es obligatorio cuando AuthMode=ApiKey.");
            }
            azureClient = new AzureOpenAIClient(
                new Uri(_settings.Endpoint),
                new AzureKeyCredential(_settings.ApiKey));
        }

        return azureClient.GetChatClient(_settings.DeploymentName);
    }

    /// <summary>
    /// Escanea los ficheros *.validation.json del directorio de tipologías y construye
    /// la sección del prompt con las tipologías marcadas como isDefault.
    /// Se evalúa una sola vez (Lazy) al primer uso.
    /// </summary>
    private static string BuildTipologiasPromptSection(string configBasePath)
    {
        if (!Directory.Exists(configBasePath))
        {
            return string.Empty;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        foreach (var file in Directory.EnumerateFiles(configBasePath, "*.validation.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Solo tipologías marcadas como default para evitar duplicados por familia
                var isDefault = root.TryGetProperty("isDefault", out var isDefaultProp)
                    && isDefaultProp.GetBoolean();
                if (!isDefault) continue;

                var tipologiaId = root.TryGetProperty("tipologiaId", out var idProp)
                    ? idProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(tipologiaId) || !seen.Add(tipologiaId))
                    continue;

                var nombre = root.TryGetProperty("tipologiaNombre", out var nombreProp)
                    ? nombreProp.GetString() : tipologiaId;
                var descripcion = root.TryGetProperty("gptDescripcion", out var descProp)
                    ? descProp.GetString() : null;

                var definition = string.IsNullOrWhiteSpace(descripcion)
                    ? $"- {tipologiaId}: {nombre}"
                    : $"- {tipologiaId}: {descripcion}";

                lines.Add(definition);
            }
            catch
            {
                // Ignorar archivos malformados — el prompt seguirá con las tipologías válidas
            }
        }

        return string.Join("\n", lines);
    }
}
