using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Serialization;

/// <summary>
/// Deserializa <see cref="ContenidoDocumento"/> en la ingesta decodificando "base64" directamente
/// a <see cref="ContenidoDocumento.Bytes"/> desde los bytes UTF-8 del JSON, sin construir el
/// string base64 (AB#100814). Si el valor no es base64 estricto, prueba la decodificación
/// tolerante de Convert (espacios y saltos de línea); si tampoco vale, deja el string en
/// Base64 para que el trigger responda el 400 de siempre.
/// </summary>
public sealed class ContenidoDocumentoIngestaConverter : JsonConverter<ContenidoDocumento>
{
    public override ContenidoDocumento Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new ContenidoDocumento();
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("documento.content debe ser un objeto.");
        }

        var contenido = new ContenidoDocumento();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return contenido;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Se esperaba un nombre de propiedad en documento.content.");
            }

            var esBase64 = reader.ValueTextEquals("base64"u8)
                || string.Equals(reader.GetString(), "base64", StringComparison.OrdinalIgnoreCase);

            reader.Read();

            if (!esBase64)
            {
                reader.Skip();
                continue;
            }

            LeerBase64(ref reader, contenido);
        }

        throw new JsonException("documento.content sin cerrar.");
    }

    private static void LeerBase64(ref Utf8JsonReader reader, ContenidoDocumento contenido)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return;
            case JsonTokenType.String:
                break;
            default:
                throw new JsonException("documento.content.base64 debe ser un string.");
        }

        if (reader.TryGetBytesFromBase64(out var bytes))
        {
            contenido.Bytes = bytes;
            return;
        }

        // Fallback tolerante (clientes que insertan saltos de linea): cuesta el string, pero
        // mantiene el contrato. Si tampoco es base64, el trigger responde 400.
        var texto = reader.GetString();
        if (string.IsNullOrWhiteSpace(texto))
        {
            return;
        }

        try
        {
            contenido.Bytes = Convert.FromBase64String(texto.Trim());
        }
        catch (FormatException)
        {
            contenido.Base64 = texto;
        }
    }

    public override void Write(Utf8JsonWriter writer, ContenidoDocumento value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Base64 is not null)
        {
            writer.WriteString("base64", value.Base64);
        }
        writer.WriteEndObject();
    }
}
