using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentIA.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services.Abstractions;

public class GptPromptBuilder : IGptPromptBuilder
{
    private readonly ILogger<GptPromptBuilder> _logger;

    public GptPromptBuilder(ILogger<GptPromptBuilder> logger)
    {
        _logger = logger;
    }

    public string BuildSystemPrompt(PromptMode mode, PromptConfig? resumeConfig, PromptConfig? customConfig)
    {
        var systemText =
            "Eres un extractor de datos de documentos inmobiliarios y registrales españoles. " +
            "Devuelve EXCLUSIVAMENTE un objeto JSON válido, sin texto adicional, con las siguientes claves: " +
            "'campos_extraidos' (objeto con los campos extraídos, null para no encontrados), " +
            "'confianza_extraccion' (número entre 0.0 y 1.0 que refleja tu confianza global en la extracción), " +
            "'confianza_por_campo' (objeto con confianza 0..1 por cada campo extraído). " +
            "Para cada campo se indican el tipo esperado, si es obligatorio y las reglas de validación " +
            "(formatos permitidos, patrones, valores de enumeración, rangos numéricos). " +
            "Respeta estrictamente esas reglas al extraer el valor de cada campo. " +
            "Usa null para campos no encontrados.";

        if (resumeConfig is not null)
        {
            systemText += " Incluye además la clave 'resumen' como string.";
        }

        if (mode == PromptMode.ExtractionWithFallback || customConfig is not null)
        {
            systemText += " Incluye además la clave 'resultado_prompt' como string con la respuesta a la instrucción adicional.";
        }

        if (customConfig?.SystemPrompt is not null)
        {
            systemText += $"\n\nINSTRUCCIÓN ADICIONAL PARA LA RESPUESTA DE 'resultado_prompt':\n{customConfig.SystemPrompt}";
        }

        return systemText;
    }

    public string BuildUserPrompt(TipologiaValidationConfig tipologia, string? contentMarker, string? markdownContent)
    {
        var fieldList = BuildFieldCatalog(tipologia);
        return $"Tipo de documento: {tipologia.TipologiaId} ({tipologia.TipologiaNombre})\n\n" +
            "Extrae los siguientes campos. Para cada uno se indica tipo, obligatoriedad y las reglas de validación " +
            "que debe cumplir el valor extraído (respétalas en el formato del dato devuelto):\n" +
            fieldList;
    }

    public string BuildFieldCatalog(TipologiaValidationConfig config)
    {
        if (config.Fields.Count == 0)
            return "- Sin definición de campos en configuración.";

        var sb = new StringBuilder();
        foreach (var field in config.Fields)
            AppendFieldLine(sb, field, depth: 0);
        return sb.ToString().TrimEnd();
    }

    private static void AppendFieldLine(StringBuilder sb, FieldValidationConfig field, int depth)
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
}
