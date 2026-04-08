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
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

public class GptFallbackExtraerDataProvider
{
    private readonly ExtractionModelRegistryLoader _modelRegistryLoader;
    private readonly ILogger<GptFallbackExtraerDataProvider> _logger;
    private readonly Lazy<ExtractionModelConfig> _fallbackModel;
    private readonly Lazy<ChatClient> _chatClient;

    public GptFallbackExtraerDataProvider(
        ExtractionModelRegistryLoader modelRegistryLoader,
        ILogger<GptFallbackExtraerDataProvider> logger)
    {
        _modelRegistryLoader = modelRegistryLoader;
        _logger = logger;
        _fallbackModel = new Lazy<ExtractionModelConfig>(ResolveFallbackModel);
        _chatClient = new Lazy<ChatClient>(CreateChatClient);
    }

    public virtual async Task<ExtraccionResultado> ObtenerDatosConFallbackAsync(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        string? markdownContexto,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var model = _fallbackModel.Value;

        _logger.LogInformation(
            "Iniciando fallback GPT para extracción. Tipología={Tipologia}, Deployment={Deployment}",
            input.Tipologia,
            model.DeploymentName);

        var systemMessage = new SystemChatMessage(
            "Eres un extractor de datos de documentos inmobiliarios y registrales españoles. " +
            "Devuelve EXCLUSIVAMENTE un objeto JSON válido, sin texto adicional, con las siguientes claves: " +
            "'campos_extraidos' (objeto con los campos extraídos, null para no encontrados), " +
            "'confianza_extraccion' (número entre 0.0 y 1.0 que refleja tu confianza global en la extracción), " +
            "'confianza_por_campo' (objeto con confianza 0..1 por cada campo extraído). " +
            "Para cada campo se indican el tipo esperado, si es obligatorio y las reglas de validación " +
            "(formatos permitidos, patrones, valores de enumeración, rangos numéricos). " +
            "Respeta estrictamente esas reglas al extraer el valor de cada campo. " +
            "Usa null para campos no encontrados.");

        var fieldList = BuildFieldList(tipologiaConfig);
        var userPrompt =
            $"Tipo de documento: {tipologiaConfig.TipologiaId} ({tipologiaConfig.TipologiaNombre})\n\n" +
            "Extrae los siguientes campos. Para cada uno se indica tipo, obligatoriedad y las reglas de validación " +
            "que debe cumplir el valor extraído (respétalas en el formato del dato devuelto):\n" +
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

        var confianzaExtraccionGpt = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("confianza_extraccion", out var ceElement)
            && ceElement.TryGetDouble(out var ceVal)
            ? (double?)Math.Clamp(ceVal, 0.0, 1.0)
            : null;

        var (confianzaCalculada, metricasDebug) = BuildFallbackMetricas(input, tipologiaConfig, datos, confianzaPorCampo);

        return new ExtraccionResultado
        {
            Proveedor = "azure-openai",
            Modelo = model.DeploymentName,
            LayoutEnabled = false,
            FallbackUsado = true,
            ConfianzaExtraccion = confianzaExtraccionGpt ?? confianzaCalculada,
            ProveedorExtrac = "GPT4oMini",
            TiemposMs = new Dictionary<string, int>
            {
                ["gpt-fallback"] = (int)stopwatch.ElapsedMilliseconds
            },
            MetricasDebug = metricasDebug,
            DatosExtraidos = datos
        };
    }

    public virtual async Task<ExtraccionResultado> ObtenerDatosConModeloAsync(
        ExtraccionInput input,
        TipologiaValidationConfig tipologiaConfig,
        string modelKey,
        string? markdownContexto,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var model = ResolveModel(modelKey);
        var chatClient = CreateChatClient(model);

        var systemMessage = new SystemChatMessage(
            "Eres un extractor de datos de documentos inmobiliarios y registrales españoles. " +
            "Devuelve EXCLUSIVAMENTE un objeto JSON válido, sin texto adicional, con las siguientes claves: " +
            "'campos_extraidos' (objeto con los campos extraídos, null para no encontrados), " +
            "'confianza_extraccion' (número entre 0.0 y 1.0 que refleja tu confianza global en la extracción), " +
            "'confianza_por_campo' (objeto con confianza 0..1 por cada campo extraído). " +
            "Para cada campo se indican el tipo esperado, si es obligatorio y las reglas de validación " +
            "(formatos permitidos, patrones, valores de enumeración, rangos numéricos). " +
            "Respeta estrictamente esas reglas al extraer el valor de cada campo. " +
            "Usa null para campos no encontrados.");

        var fieldList = BuildFieldList(tipologiaConfig);
        var userPrompt =
            $"Tipo de documento: {tipologiaConfig.TipologiaId} ({tipologiaConfig.TipologiaNombre})\n\n" +
            "Extrae los siguientes campos. Para cada uno se indica tipo, obligatoriedad y las reglas de validación " +
            "que debe cumplir el valor extraído (respétalas en el formato del dato devuelto):\n" +
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

        var confianzaExtraccionGpt = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("confianza_extraccion", out var ceElement)
            && ceElement.TryGetDouble(out var ceVal)
            ? (double?)Math.Clamp(ceVal, 0.0, 1.0)
            : null;

        var (confianzaCalculada, metricasDebug) = BuildFallbackMetricas(input, tipologiaConfig, datos, confianzaPorCampo);

        return new ExtraccionResultado
        {
            Proveedor = "azure-openai",
            Modelo = model.DeploymentName,
            LayoutEnabled = false,
            FallbackUsado = false,
            FallbackRazon = null,
            ConfianzaExtraccion = confianzaExtraccionGpt ?? confianzaCalculada,
            ProveedorExtrac = "GPT4oMini",
            TiemposMs = new Dictionary<string, int>
            {
                ["gpt-direct"] = (int)stopwatch.ElapsedMilliseconds
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
        var model = _fallbackModel.Value;

        _logger.LogInformation(
            "Iniciando fallback GPT (modo combinado con prompt) para tipología={Tipologia}, Deployment={Deployment}",
            input.Tipologia,
            model.DeploymentName);

        var combinedSystemPrompt =
            "Eres un extractor de datos de documentos inmobiliarios y registrales españoles. " +
            "Devuelve EXCLUSIVAMENTE un objeto JSON válido, sin texto adicional, con las siguientes claves: " +
            "'campos_extraidos' (objeto JSON con los campos del documento, usa null para no encontrados), " +
            "'resultado_prompt' (string con la respuesta a la instrucción adicional a continuación), " +
            "'confianza_extraccion' (número entre 0.0 y 1.0 que refleja tu confianza global en la extracción) y " +
            "'confianza_por_campo' (objeto con confianza 0..1 por campo extraído). " +
            "Para cada campo en 'campos_extraidos' se indican el tipo esperado, si es obligatorio y las reglas " +
            "de validación (formatos, patrones, enumeraciones, rangos). " +
            "Respeta estrictamente esas reglas al extraer el valor de cada campo.";

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
            "Extrae los siguientes campos. Para cada uno se indica tipo, obligatoriedad y las reglas de validación " +
            "que debe cumplir el valor extraído (respétalas en el formato del dato devuelto):\n" +
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

        var confianzaExtraccionGpt = root.TryGetProperty("confianza_extraccion", out var ceElement2)
            && ceElement2.TryGetDouble(out var ceVal2)
            ? (double?)Math.Clamp(ceVal2, 0.0, 1.0)
            : null;

        var (confianzaCalculada, metricasDebug) = BuildFallbackMetricas(input, tipologiaConfig, campos, confianzaPorCampo);

        return new ExtraccionResultado
        {
            Proveedor = "azure-openai",
            Modelo = model.DeploymentName,
            LayoutEnabled = false,
            FallbackUsado = true,
            ConfianzaExtraccion = confianzaExtraccionGpt ?? confianzaCalculada,
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

        var (confianzaCalculada, metricas) = ConfidenceCalculator.ExtracCU(
            fieldConfs: confianzaPorCampo?.Values.Select(v => (double?)v).ToList(),
            camposPresentes: camposPresentes,
            camposTotales: camposTotales,
            camposRequeridos: camposRequeridos,
            camposRequeridosPresentes: camposRequeridosPresentes,
            warnings: 0,
            cfg: tipologiaConfig.ConfidenceConfig);

        metricas.ConfianzaPorCampo = confianzaPorCampo ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var umbralDuda = input.UmbralFallbackEfectivo
            ?? tipologiaConfig.ConfidenceConfig?.ExtracUmbralFallback
            ?? _fallbackModel.Value.MinFieldsRatio;

        metricas.CamposBajaConfianza = metricas.ConfianzaPorCampo
            .Where(kvp => kvp.Value < umbralDuda)
            .Select(kvp => kvp.Key)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (confianzaCalculada, metricas);
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
            return "- Sin definición de campos en configuración.";

        var sb = new System.Text.StringBuilder();
        foreach (var field in config.Fields)
            AppendFieldLine(sb, field, depth: 0);
        return sb.ToString().TrimEnd();
    }

    private static void AppendFieldLine(System.Text.StringBuilder sb, FieldValidationConfig field, int depth)
    {
        var indent = new string(' ', depth * 4);
        var requerido = field.Required ? " [REQUERIDO]" : "";
        sb.AppendLine($"{indent}- {field.Name}: tipo={field.Type}{requerido}");

        if (!string.IsNullOrWhiteSpace(field.Description))
        {
            sb.AppendLine($"{indent}    -> descripción: {field.Description.Trim()}");
        }

        foreach (var rule in field.Rules)
        {
            var hint = BuildRuleHint(rule);
            if (!string.IsNullOrEmpty(hint))
                sb.AppendLine($"{indent}    -> {hint}");
        }

        if (string.Equals(field.Type, "array", StringComparison.OrdinalIgnoreCase)
            && field.Items?.Properties?.Count > 0)
        {
            sb.AppendLine($"{indent}  (array de objetos; propiedades de cada elemento:)");
            foreach (var subField in field.Items.Properties)
                AppendFieldLine(sb, subField, depth + 1);
        }
    }

    private static string BuildRuleHint(ValidationRuleConfig rule)
    {
        return rule.RuleType.ToLowerInvariant() switch
        {
            "enum" => BuildEnumHint(rule.Parameters),
            "regex" => BuildRegexHint(rule.Parameters),
            "date" => BuildDateHint(rule.Parameters),
            "range" => BuildRangeHint(rule.Parameters),
            "minlength" when TryGetParamString(rule.Parameters, "value", out var v) => $"longitud mínima: {v} caracteres",
            "maxlength" when TryGetParamString(rule.Parameters, "value", out var v) => $"longitud máxima: {v} caracteres",
            "nif" => "NIF/DNI/CIF/NIE español válido (ej: 12345678A, A12345678, X1234567L)",
            "catastral" => "referencia catastral española (20 caracteres alfanuméricos)",
            "address" => BuildAddressHint(rule.Parameters),
            _ => string.Empty
        };
    }

    private static string BuildEnumHint(Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("values", out var raw) || raw is null)
            return string.Empty;

        IEnumerable<string>? values = raw switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Array =>
                je.EnumerateArray()
                  .Where(e => e.ValueKind == JsonValueKind.String)
                  .Select(e => e.GetString()!)
                  .Where(s => !string.IsNullOrWhiteSpace(s)),
            System.Collections.IEnumerable list =>
                list.Cast<object?>()
                    .Select(v => v?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s)),
            _ => null
        };

        if (values is null) return string.Empty;
        var joined = string.Join(", ", values);
        return string.IsNullOrEmpty(joined) ? string.Empty : $"uno de los valores permitidos: {joined}";
    }

    private static string BuildRegexHint(Dictionary<string, object?> parameters)
    {
        if (!TryGetParamString(parameters, "pattern", out var pattern) || string.IsNullOrWhiteSpace(pattern))
            return string.Empty;
        return $"patrón esperado (regex): {pattern}";
    }

    private static string BuildDateHint(Dictionary<string, object?> parameters)
    {
        var hints = new List<string>();

        if (parameters.TryGetValue("formats", out var fmtRaw) && fmtRaw != null)
        {
            IEnumerable<string>? formats = fmtRaw switch
            {
                JsonElement je when je.ValueKind == JsonValueKind.Array =>
                    je.EnumerateArray()
                      .Where(e => e.ValueKind == JsonValueKind.String)
                      .Select(e => e.GetString()!)
                      .Where(s => !string.IsNullOrWhiteSpace(s)),
                System.Collections.IEnumerable list =>
                    list.Cast<object?>()
                        .Select(v => v?.ToString() ?? string.Empty)
                        .Where(s => !string.IsNullOrWhiteSpace(s)),
                _ => null
            };
            if (formats != null)
            {
                var joined = string.Join(", ", formats);
                if (!string.IsNullOrEmpty(joined))
                    hints.Add($"formatos: {joined}");
            }
        }

        if (parameters.TryGetValue("allowFuture", out var af) && af != null)
        {
            var allowFuture = af is JsonElement je ? je.GetBoolean() : Convert.ToBoolean(af);
            if (!allowFuture) hints.Add("no puede ser fecha futura");
        }

        return hints.Count > 0 ? string.Join("; ", hints) : string.Empty;
    }

    private static string BuildRangeHint(Dictionary<string, object?> parameters)
    {
        var parts = new List<string>();
        if (TryGetParamString(parameters, "min", out var min)) parts.Add($"mín={min}");
        if (TryGetParamString(parameters, "max", out var max)) parts.Add($"máx={max}");
        return parts.Count > 0 ? $"rango numérico: {string.Join(", ", parts)}" : string.Empty;
    }

    private static string BuildAddressHint(Dictionary<string, object?> parameters)
    {
        var required = new List<string>();
        if (TryGetBoolParam(parameters, "requireStreetNumber")) required.Add("número de calle");
        if (TryGetBoolParam(parameters, "requireMunicipality")) required.Add("municipio");
        if (TryGetBoolParam(parameters, "requireProvince")) required.Add("provincia");
        return required.Count > 0
            ? $"dirección postal completa (incluir: {string.Join(", ", required)})"
            : "dirección postal completa";
    }

    private static bool TryGetParamString(Dictionary<string, object?> parameters, string key, out string value)
    {
        value = string.Empty;
        if (!parameters.TryGetValue(key, out var raw) || raw is null) return false;
        value = raw is JsonElement je
            ? (je.ValueKind == JsonValueKind.String ? je.GetString() ?? string.Empty : je.GetRawText())
            : raw.ToString() ?? string.Empty;
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryGetBoolParam(Dictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var raw) || raw is null) return false;
        return raw is JsonElement je ? je.GetBoolean() : Convert.ToBoolean(raw);
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

        return CreateChatClient(model);
    }

    private ChatClient CreateChatClient(ExtractionModelConfig model)
    {

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            throw new InvalidOperationException($"ExtractionModelConfig.Endpoint es obligatorio para el modelo de fallback '{model.Key}'.");
        }

        if (string.IsNullOrWhiteSpace(model.DeploymentName))
        {
            throw new InvalidOperationException($"ExtractionModelConfig.DeploymentName es obligatorio para el modelo de fallback '{model.Key}'.");
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
                throw new InvalidOperationException($"ExtractionModelConfig.ApiKey es obligatorio para el modelo de fallback '{model.Key}' cuando AuthMode=ApiKey.");
            }

            azureClient = new AzureOpenAIClient(
                new Uri(model.Endpoint),
                new AzureKeyCredential(model.ApiKey));
        }

        return azureClient.GetChatClient(model.DeploymentName);
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

        ValidateModelConfiguration(model);
        return model;
    }

    /// <summary>
    /// Valida que la configuración del modelo OpenAI sea completa (endpoint, apikey si es necesario, deployment).
    /// </summary>
    private void ValidateModelConfiguration(ExtractionModelConfig model)
    {
        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            throw new InvalidOperationException(
                $"Extracción GPT: modelo '{model.Key}' requiere Endpoint configurado. Verifica la configuración de '{model.Key}' en appsettings/KeyVault.");
        }

        if (string.IsNullOrWhiteSpace(model.DeploymentName))
        {
            throw new InvalidOperationException(
                $"Extracción GPT: modelo '{model.Key}' requiere DeploymentName configurado. Verifica la configuración de '{model.Key}' en appsettings/KeyVault.");
        }

        if (!string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(model.ApiKey))
        {
            throw new InvalidOperationException(
                $"Extracción GPT: modelo '{model.Key}' requiere ApiKey configurada cuando AuthMode={model.AuthMode}. Verifica la configuración en appsettings/KeyVault (ej: Extraction:GptFallback:ApiKey).");
        }
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
}
