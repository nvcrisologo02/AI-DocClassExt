using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentIA.Core.Models;

/// <summary>
/// Convierte null (y cadena vacia) a false para mantener compatibilidad hacia atras en contratos de entrada.
/// </summary>
public sealed class NullToFalseBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return false;
        }

        if (reader.TokenType == JsonTokenType.True)
        {
            return true;
        }

        if (reader.TokenType == JsonTokenType.False)
        {
            return false;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            if (bool.TryParse(raw, out var parsed))
            {
                return parsed;
            }
        }

        throw new JsonException("El valor no se puede convertir a booleano.");
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}