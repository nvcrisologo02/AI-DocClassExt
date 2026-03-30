using System.Globalization;
using System.Text.Json;
using DocumentIA.Core.Configuration;

namespace DocumentIA.Functions.Services;

public class ContentUnderstandingResultMapper
{
    public Dictionary<string, object> Map(JsonDocument analysisDocument, TipologiaValidationConfig tipologiaConfig)
    {
        if (!analysisDocument.RootElement.TryGetProperty("result", out var resultElement)
            || !resultElement.TryGetProperty("contents", out var contentsElement)
            || contentsElement.ValueKind != JsonValueKind.Array
            || contentsElement.GetArrayLength() == 0)
        {
            return new Dictionary<string, object>();
        }

        var firstContent = contentsElement[0];
        if (!firstContent.TryGetProperty("fields", out var fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object>();
        }

        return MapFromFields(fieldsElement, tipologiaConfig);
    }

    /// <summary>
    /// Mapea campos directamente desde un JsonElement que representa el objeto "fields".
    /// Permite reutilizar la lógica de mapeo cuando la ruta al elemento fields difiere
    /// del formato CU (p. ej. en respuestas de DI custom: analyzeResult.documents[0].fields).
    /// </summary>
    public Dictionary<string, object> MapFromFields(JsonElement fieldsElement, TipologiaValidationConfig tipologiaConfig)
    {
        if (fieldsElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object>();
        }

        var result = new Dictionary<string, object>();
        var mappings = tipologiaConfig.Extraction.FieldMappings
            .ToDictionary(m => m.TargetField, m => m.SourcePath, StringComparer.OrdinalIgnoreCase);

        foreach (var fieldConfig in tipologiaConfig.Fields)
        {
            var sourcePath = mappings.TryGetValue(fieldConfig.Name, out var configuredPath)
                ? configuredPath
                : tipologiaConfig.Extraction.AutoMapUnmappedFields
                    ? fieldConfig.Name
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }

            if (TryResolveField(fieldsElement, sourcePath, out var fieldElement))
            {
                var normalized = NormalizeField(fieldElement);
                if (normalized is not null)
                {
                    result[fieldConfig.Name] = normalized;
                }
            }
        }

        return result;
    }

    private static bool TryResolveField(JsonElement fieldsElement, string sourcePath, out JsonElement fieldElement)
    {
        fieldElement = fieldsElement;
        foreach (var segment in sourcePath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (fieldElement.ValueKind == JsonValueKind.Object && fieldElement.TryGetProperty(segment, out var directProperty))
            {
                fieldElement = directProperty;
                continue;
            }

            if (fieldElement.ValueKind == JsonValueKind.Object
                && fieldElement.TryGetProperty("valueObject", out var objectElement)
                && objectElement.ValueKind == JsonValueKind.Object
                && objectElement.TryGetProperty(segment, out var nestedProperty))
            {
                fieldElement = nestedProperty;
                continue;
            }

            if (fieldElement.ValueKind == JsonValueKind.Object
                && fieldElement.TryGetProperty("valueArray", out var arrayElement)
                && arrayElement.ValueKind == JsonValueKind.Array
                && int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && index >= 0
                && index < arrayElement.GetArrayLength())
            {
                fieldElement = arrayElement[index];
                continue;
            }

            fieldElement = default;
            return false;
        }

        return true;
    }

    private static object? NormalizeField(JsonElement fieldElement)
    {
        if (fieldElement.ValueKind == JsonValueKind.Null || fieldElement.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (fieldElement.ValueKind != JsonValueKind.Object)
        {
            return ConvertPrimitive(fieldElement);
        }

        if (fieldElement.TryGetProperty("valueString", out var valueString))
        {
            return valueString.GetString();
        }

        if (fieldElement.TryGetProperty("valueDate", out var valueDate))
        {
            return valueDate.GetString();
        }

        if (fieldElement.TryGetProperty("valueDateTime", out var valueDateTime))
        {
            return valueDateTime.GetString();
        }

        if (fieldElement.TryGetProperty("valueTime", out var valueTime))
        {
            return valueTime.GetString();
        }

        if (fieldElement.TryGetProperty("valuePhoneNumber", out var valuePhone))
        {
            return valuePhone.GetString();
        }

        if (fieldElement.TryGetProperty("valueNumber", out var valueNumber))
        {
            return valueNumber.GetDecimal();
        }

        if (fieldElement.TryGetProperty("valueInteger", out var valueInteger))
        {
            return valueInteger.GetInt64();
        }

        if (fieldElement.TryGetProperty("valueBoolean", out var valueBoolean))
        {
            return valueBoolean.GetBoolean();
        }

        if (fieldElement.TryGetProperty("valueArray", out var valueArray) && valueArray.ValueKind == JsonValueKind.Array)
        {
            return valueArray.EnumerateArray()
                .Select(NormalizeField)
                .Where(value => value is not null)
                .Cast<object>()
                .ToArray();
        }

        if (fieldElement.TryGetProperty("valueObject", out var valueObject) && valueObject.ValueKind == JsonValueKind.Object)
        {
            var dictionary = new Dictionary<string, object>();
            foreach (var property in valueObject.EnumerateObject())
            {
                var normalized = NormalizeField(property.Value);
                if (normalized is not null)
                {
                    dictionary[property.Name] = normalized;
                }
            }

            return dictionary;
        }

        if (fieldElement.TryGetProperty("content", out var contentElement))
        {
            return contentElement.GetString();
        }

        if (HasOnlyMetadataProperties(fieldElement))
        {
            return string.Empty;
        }

        return fieldElement.GetRawText();
    }

    private static bool HasOnlyMetadataProperties(JsonElement fieldElement)
    {
        if (fieldElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var hasProperties = false;
        foreach (var property in fieldElement.EnumerateObject())
        {
            hasProperties = true;

            if (property.Name.StartsWith("value", StringComparison.Ordinal)
                || string.Equals(property.Name, "content", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return hasProperties;
    }

    private static object? ConvertPrimitive(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertPrimitive).Where(value => value is not null).Cast<object>().ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertPrimitive(p.Value) ?? string.Empty),
            _ => null
        };
    }
}