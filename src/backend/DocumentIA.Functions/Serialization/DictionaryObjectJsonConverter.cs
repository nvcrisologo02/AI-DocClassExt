using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentIA.Functions.Serialization;

/// <summary>
/// Convertidor personalizado para Dictionary&lt;string, object&gt; que resuelve problemas
/// de serialización en Durable Tasks.
/// 
/// Problema: System.Text.Json tiene limitaciones serializando objetos complejos en diccionarios.
/// Solución: Convertir todos los valores a primitivos JSON-serializables (string, número, bool, null).
/// </summary>
public class DictionaryObjectJsonConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Se esperaba un objeto JSON, pero se recibió {reader.TokenType}");
        }

        var dictionary = new Dictionary<string, object>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Propiedad esperada, pero se recibió {reader.TokenType}");
            }

            var propertyName = reader.GetString();
            if (propertyName == null)
            {
                throw new JsonException("Nombre de propiedad nulo");
            }

            reader.Read();

            object? value = reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Number => reader.TryGetInt64(out var longVal)
                    ? longVal
                    : reader.GetDouble(),
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.StartObject or JsonTokenType.StartArray => 
                    JsonSerializer.Deserialize<object>(ref reader, options),
                _ => throw new JsonException($"Token JSON no soportado: {reader.TokenType}")
            };

            dictionary[propertyName] = value!;
        }

        throw new JsonException("Objeto JSON no terminado correctamente");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);

            if (kvp.Value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                var valueType = kvp.Value.GetType();

                if (valueType == typeof(string))
                {
                    writer.WriteStringValue((string)kvp.Value);
                }
                else if (valueType == typeof(int))
                {
                    writer.WriteNumberValue((int)kvp.Value);
                }
                else if (valueType == typeof(long))
                {
                    writer.WriteNumberValue((long)kvp.Value);
                }
                else if (valueType == typeof(double))
                {
                    writer.WriteNumberValue((double)kvp.Value);
                }
                else if (valueType == typeof(float))
                {
                    writer.WriteNumberValue((float)kvp.Value);
                }
                else if (valueType == typeof(decimal))
                {
                    writer.WriteNumberValue((decimal)kvp.Value);
                }
                else if (valueType == typeof(bool))
                {
                    writer.WriteBooleanValue((bool)kvp.Value);
                }
                else if (valueType == typeof(DateTime))
                {
                    writer.WriteStringValue(((DateTime)kvp.Value).ToString("O"));
                }
                else if (valueType == typeof(Guid))
                {
                    writer.WriteStringValue(((Guid)kvp.Value).ToString());
                }
                else if (kvp.Value is IEnumerable<object> enumerable && valueType != typeof(string))
                {
                    // Para IEnumerable, serializarlo como array JSON
                    var jsonString = JsonSerializer.Serialize(enumerable.ToList(), options);
                    using var jsonDoc = JsonDocument.Parse(jsonString);
                    jsonDoc.RootElement.WriteTo(writer);
                }
                else if (kvp.Value is IDictionary<string, object> dict)
                {
                    // Para diccionarios anidados, serializarlos recursivamente
                    var jsonString = JsonSerializer.Serialize(dict, options);
                    using var jsonDoc = JsonDocument.Parse(jsonString);
                    jsonDoc.RootElement.WriteTo(writer);
                }
                else
                {
                    // Para otros tipos complejos, convertir a JSON string como fallback
                    try
                    {
                        var jsonString = JsonSerializer.Serialize(kvp.Value, valueType, options);
                        writer.WriteStringValue(jsonString);
                    }
                    catch
                    {
                        // Si todo falla, usar el ToString() del objeto
                        writer.WriteStringValue(kvp.Value.ToString() ?? "");
                    }
                }
            }
        }

        writer.WriteEndObject();
    }
}
