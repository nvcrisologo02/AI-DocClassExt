using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
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
            "Preferiblemente devuelve un objeto con claves 'campos_extraidos' y 'confianza_por_campo'. " +
            "En 'confianza_por_campo' informa un valor de 0 a 1 por cada campo extraído. " +
            "Usa null para campos no encontrados.");

        var fieldList = BuildFieldList(tipologiaConfig);
        var userPrompt =
            $"Tipo de documento: {tipologiaConfig.TipologiaId} ({tipologiaConfig.TipologiaNombre})\n\n" +
            "Extrae estos campos y devuelve exactamente estos nombres de clave:\n" +
            fieldList;

        var contextoTexto = string.IsNullOrWhiteSpace(markdownContexto)
            ? ObtenerContextoTexto(input.DatosNormalizados)
            : markdownContexto;

        var userMessage = !string.IsNullOrWhiteSpace(contextoTexto)
            ? new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(
                    $"{userPrompt}\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{contextoTexto}"))
            : new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(
                    $"{userPrompt}\n\nNo hay contenido textual disponible. " +
                    $"Nombre de archivo: {input.Entrada.Documento.Name}."));

        if (string.IsNullOrWhiteSpace(contextoTexto))
        {
            _logger.LogWarning(
                "No hay contexto textual preprocesado para fallback de extracción en {Documento}. Se continuará con contexto mínimo.",
                input.Entrada.Documento.Name);
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

        using var responseDoc = JsonDocument.Parse(responseText);
        var root = responseDoc.RootElement;

        Dictionary<string, object> datos;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("campos_extraidos", out var camposElement))
        {
            datos = ParseExtractedFields(camposElement, tipologiaConfig);
        }
        else
        {
            datos = ParseJsonObjectResponse(responseText, tipologiaConfig);
        }

        var confianzaPorCampo = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("confianza_por_campo", out var confidenceElement)
            ? ParseFieldConfidenceMap(confidenceElement, tipologiaConfig)
            : null;

        var metricasDebug = BuildFallbackMetricas(input, tipologiaConfig, datos, confianzaPorCampo);

        return new ExtraccionResultado
        {
            Proveedor = "azure-openai",
            Modelo = _settings.DeploymentName,
            LayoutEnabled = false,
            FallbackUsado = true,
            ConfianzaExtraccion = ConfidenceCalculator.ExtracGPT(),
            ProveedorExtrac = "GPT4oMini",
            TiemposMs = new Dictionary<string, int>
            {
                ["gpt-fallback"] = (int)stopwatch.ElapsedMilliseconds
            },
            MetricasDebug = metricasDebug,
            DatosExtraidos = datos
        };
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
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Iniciando fallback GPT (modo combinado con prompt) para tipología={Tipologia}, Deployment={Deployment}",
            input.Tipologia,
            _settings.DeploymentName);

        var combinedSystemPrompt =
            "Eres un extractor de datos de documentos inmobiliarios y registrales españoles. " +
            "Devuelve EXCLUSIVAMENTE un objeto JSON válido, sin texto adicional, " +
            "con hasta tres claves: " +
            "'campos_extraidos' (objeto JSON con los campos del documento, usa null para no encontrados) y " +
            "'resultado_prompt' (string con la respuesta a la instrucción adicional a continuación) y " +
            "'confianza_por_campo' (objeto con confianza 0..1 por campo extraído).";

        if (!string.IsNullOrWhiteSpace(promptConfig.SystemPrompt))
        {
            combinedSystemPrompt += $"\n\nINSTRUCCIÓN ADICIONAL PARA LA RESPUESTA DE 'resultado_prompt':\n{promptConfig.SystemPrompt}";
        }

        var systemMessage = new SystemChatMessage(combinedSystemPrompt);

        var fieldList = BuildFieldList(tipologiaConfig);

        // Para el modo combinado, {contenido} se sustituye por el marcador ya que el contenido
        // se pasa al modelo directamente como parte del mensaje. Los {campo:X} no se resuelven
        // aquí porque la extracción y el prompt ocurren simultáneamente.
        var promptInstruction = promptConfig.UserPromptTemplate
            .Replace("{contenido}", "[contenido del documento proporcionado en este mensaje]",
                StringComparison.OrdinalIgnoreCase);
        promptInstruction = Regex.Replace(promptInstruction, "\\{campo:[^}]+\\}", string.Empty, RegexOptions.IgnoreCase);

        var userPromptText =
            $"Tipo de documento: {tipologiaConfig.TipologiaId} ({tipologiaConfig.TipologiaNombre})\n\n" +
            "**Parte 1 — Extracción de campos** ('campos_extraidos'):\n" +
            "Extrae estos campos y devuelve exactamente estos nombres de clave:\n" +
            fieldList + "\n\n" +
            "**Parte 2 — Instrucción adicional** ('resultado_prompt'):\n" +
            promptInstruction;

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
                "No hay contexto textual preprocesado para fallback combinado de extracción en {Documento}. Se continuará con contexto mínimo.",
                input.Entrada.Documento.Name);
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

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        Dictionary<string, object> campos;
        string resultadoPrompt;

        if (root.TryGetProperty("campos_extraidos", out var camposElement))
        {
            campos = ParseExtractedFields(camposElement, tipologiaConfig);
        }
        else
        {
            // Fallback: si el modelo devolvió el JSON directamente sin la estructura combinada
            _logger.LogWarning("Respuesta combinada sin clave 'campos_extraidos'. Intentando parseo directo.");
            campos = ParseJsonObjectResponse(responseText, tipologiaConfig);
        }

        if (root.TryGetProperty("resultado_prompt", out var promptElement))
        {
            resultadoPrompt = promptElement.ValueKind == JsonValueKind.String
                ? promptElement.GetString() ?? string.Empty
                : promptElement.GetRawText();
        }
        else
        {
            _logger.LogWarning("Respuesta combinada sin clave 'resultado_prompt'.");
            resultadoPrompt = string.Empty;
        }

        var confianzaPorCampo = root.TryGetProperty("confianza_por_campo", out var confidenceElement)
            ? ParseFieldConfidenceMap(confidenceElement, tipologiaConfig)
            : null;

        var metricasDebug = BuildFallbackMetricas(input, tipologiaConfig, campos, confianzaPorCampo);

        return new ExtraccionResultado
        {
            Proveedor = "azure-openai",
            Modelo = _settings.DeploymentName,
            LayoutEnabled = false,
            FallbackUsado = true,
            ConfianzaExtraccion = ConfidenceCalculator.ExtracGPT(),
            ProveedorExtrac = "GPT4oMini",
            TiemposMs = new Dictionary<string, int>
            {
                ["gpt-fallback-combined"] = (int)stopwatch.ElapsedMilliseconds
            },
            MetricasDebug = metricasDebug,
            DatosExtraidos = campos,
            ResultadoPromptCombinado = resultadoPrompt
        };
    }

    private ConfidenceMetricasExtraccion BuildFallbackMetricas(
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

        var (confianzaCalculada, metricas) = ConfidenceCalculator.ExtracCU(
            fieldConfs: confianzaPorCampo?.Values.Select(v => (double?)v).ToList(),
            camposPresentes: camposPresentes,
            camposTotales: camposTotales,
            camposRequeridos: camposRequeridos,
            camposRequeridosPresentes: camposRequeridosPresentes,
            warnings: 0,
            cfg: tipologiaConfig.ConfidenceConfig);

        _ = confianzaCalculada; // Métrica informativa para debug; la confianza efectiva del fallback sigue siendo ExtracGPT().

        metricas.ConfianzaPorCampo = confianzaPorCampo ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var umbralDuda = input.UmbralFallbackEfectivo
            ?? tipologiaConfig.ConfidenceConfig?.ExtracUmbralFallback
            ?? _settings.MinFieldsRatio;

        metricas.CamposBajaConfianza = metricas.ConfianzaPorCampo
            .Where(kvp => kvp.Value < umbralDuda)
            .Select(kvp => kvp.Key)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return metricas;
    }

    private Dictionary<string, double>? ParseFieldConfidenceMap(
        JsonElement confidenceElement,
        TipologiaValidationConfig tipologiaConfig)
    {
        if (confidenceElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var expected = new HashSet<string>(
            tipologiaConfig.Fields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in confidenceElement.EnumerateObject())
        {
            if (expected.Count > 0 && !expected.Contains(prop.Name))
            {
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var confidence))
            {
                map[prop.Name] = Math.Clamp(confidence, 0.0, 1.0);
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.String
                && double.TryParse(prop.Value.GetString(), out var confidenceFromString))
            {
                map[prop.Name] = Math.Clamp(confidenceFromString, 0.0, 1.0);
            }
        }

        return map.Count > 0 ? map : null;
    }

    private Dictionary<string, object> ParseExtractedFields(
        JsonElement camposElement,
        TipologiaValidationConfig tipologiaConfig)
    {
        if (camposElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object>();
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var expected = new HashSet<string>(
            tipologiaConfig.Fields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var prop in camposElement.EnumerateObject())
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
