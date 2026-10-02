using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocumentIA.Functions.Services.Classification;

/// <summary>Texto de DatosNormalizados para el clasificador por embeddings. AB#100779.</summary>
public static partial class TextoClasificacionResolver
{
    private static readonly string[] Claves = { "Markdown", "markdown", "Texto", "texto", "ContentText", "contentText" };

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosEnBlanco();

    /// <summary>Primer valor no vacio de las claves conocidas, sin recortar. Nulo si no hay texto.</summary>
    public static string? Obtener(IDictionary<string, object> datosNormalizados)
    {
        foreach (var clave in Claves)
        {
            if (!datosNormalizados.TryGetValue(clave, out var raw) || raw is null)
            {
                continue;
            }

            var texto = raw switch
            {
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(texto))
            {
                return texto;
            }
        }

        return null;
    }

    /// <summary>Espacios en blanco colapsados a uno y recorte a maxChars: lo mismo que embeddings.recortar en el spike.</summary>
    public static string Preprocesar(string? texto, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var limpio = EspaciosEnBlanco().Replace(texto, " ").Trim();
        return maxChars > 0 && limpio.Length > maxChars ? limpio[..maxChars] : limpio;
    }
}
