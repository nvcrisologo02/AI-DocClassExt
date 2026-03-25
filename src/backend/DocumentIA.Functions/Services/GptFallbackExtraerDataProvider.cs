using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

public class GptFallbackExtraerDataProvider
{
    private readonly GptFallbackExtraerSettings _settings;
    private readonly ILogger<GptFallbackExtraerDataProvider> _logger;
    private readonly Lazy<ChatClient> _chatClient;

    public GptFallbackExtraerDataProvider(
        IOptions<GptFallbackExtraerSettings> settings,
        ILogger<GptFallbackExtraerDataProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _chatClient = new Lazy<ChatClient>(CreateChatClient);
    }

    public virtual async Task<ExtraccionResultado> ObtenerDatosConFallbackAsync(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        string? markdownContexto,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Iniciando fallback GPT para extracción. Tipología={Tipologia}, Deployment={Deployment}",
            input.Tipologia,
            _settings.DeploymentName);

        var systemMessage = new SystemChatMessage(
            "Eres un extractor de datos de documentos inmobiliarios y registrales españoles. " +
            "Devuelve EXCLUSIVAMENTE un objeto JSON válido, sin texto adicional. " +
            "Usa null para campos no encontrados.");

        var fieldList = BuildFieldList(tipologiaConfig);
        var userPrompt =
            $"Tipo de documento: {tipologiaConfig.TipologiaId} ({tipologiaConfig.TipologiaNombre})\n\n" +
            "Extrae estos campos y devuelve exactamente estos nombres de clave:\n" +
            fieldList;

        UserChatMessage userMessage;

        if (!string.IsNullOrWhiteSpace(markdownContexto))
        {
            userMessage = new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(
                    $"{userPrompt}\n\nCONTENIDO DEL DOCUMENTO (markdown):\n{markdownContexto}"));
        }
        else
        {
            var pdfBytes = Convert.FromBase64String(input.Entrada.Documento.Content.Base64);
            userMessage = new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(userPrompt),
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(pdfBytes), "application/pdf"));
        }

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
        var datos = ParseJsonObjectResponse(responseText, tipologiaConfig);

        return new ExtraccionResultado
        {
            Proveedor = "azure-openai",
            Modelo = _settings.DeploymentName,
            LayoutEnabled = false,
            FallbackUsado = true,
            TiemposMs = new Dictionary<string, int>
            {
                ["gpt-fallback"] = (int)stopwatch.ElapsedMilliseconds
            },
            DatosExtraidos = datos
        };
    }

    private Dictionary<string, object> ParseJsonObjectResponse(string responseText, TipologiaValidationConfig tipologiaConfig)
    {
        using var document = JsonDocument.Parse(responseText);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("La respuesta del fallback GPT no es un objeto JSON válido.");
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var expected = new HashSet<string>(tipologiaConfig.Fields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var prop in document.RootElement.EnumerateObject())
        {
            if (expected.Count > 0 && !expected.Contains(prop.Name))
            {
                continue;
            }

            var value = ConvertJsonValue(prop.Value);
            if (value is not null)
            {
                result[prop.Name] = value;
            }
        }

        return result;
    }

    private static object? ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonValue)
                .Where(v => v is not null)
                .Cast<object>()
                .ToArray(),
            JsonValueKind.Object => element.EnumerateObject()
                .Select(p => (p.Name, Value: ConvertJsonValue(p.Value)))
                .Where(p => p.Value is not null)
                .ToDictionary(p => p.Name, p => p.Value!, StringComparer.OrdinalIgnoreCase),
            _ => null
        };
    }

    private static string BuildFieldList(TipologiaValidationConfig config)
    {
        if (config.Fields.Count == 0)
        {
            return "- Sin definición de campos en configuración.";
        }

        return string.Join(
            "\n",
            config.Fields.Select(f => $"- {f.Name} (tipo={f.Type}, requerido={f.Required})"));
    }

    private ChatClient CreateChatClient()
    {
        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            throw new InvalidOperationException("Extraction:GptFallback:Endpoint es obligatorio cuando el fallback está habilitado.");
        }

        if (string.IsNullOrWhiteSpace(_settings.DeploymentName))
        {
            throw new InvalidOperationException("Extraction:GptFallback:DeploymentName es obligatorio cuando el fallback está habilitado.");
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
                throw new InvalidOperationException("Extraction:GptFallback:ApiKey es obligatorio cuando AuthMode=ApiKey.");
            }

            azureClient = new AzureOpenAIClient(
                new Uri(_settings.Endpoint),
                new AzureKeyCredential(_settings.ApiKey));
        }

        return azureClient.GetChatClient(_settings.DeploymentName);
    }
}
