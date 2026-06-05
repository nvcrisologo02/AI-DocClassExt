using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

public class GptJsonResponseParser : IGptJsonResponseParser
{
    private readonly ILogger<GptJsonResponseParser> _logger;

    public GptJsonResponseParser(ILogger<GptJsonResponseParser> logger)
    {
        _logger = logger;
    }

    public GptExtractionResponse Parse(string jsonText, TipologiaValidationConfig config)
    {
        using var document = JsonDocument.Parse(jsonText);
        var root = document.RootElement;

        var response = new GptExtractionResponse();

        // Parse campos_extraidos
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("campos_extraidos", out var camposElement))
        {
            response.CamposExtraidos = ParseExtractedFields(camposElement, config);
        }
        else
        {
            response.CamposExtraidos = ParseJsonObjectResponse(jsonText, config);
        }

        // Parse confianza_por_campo
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("confianza_por_campo", out var confidenceElement))
        {
            response.ConfianzaPorCampo = ParseFieldConfidenceMap(confidenceElement, config);
        }

        // Parse confianza_extraccion
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("confianza_extraccion", out var ceElement) && ceElement.TryGetDouble(out var ceVal))
        {
            response.ConfianzaExtraccionGpt = Math.Clamp(ceVal, 0.0, 1.0);
        }

        // Parse resumen
        response.Resumen = ExtractString(root, "resumen");

        // Parse resultado_prompt
        response.ResultadoPrompt = ExtractString(root, "resultado_prompt");

        return response;
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
}
