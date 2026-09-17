using System.Text.Json;

namespace DocumentIA.Data.Context;

/// <summary>
/// Funciones JSON nativas de SQL Server expuestas a LINQ (registradas en
/// <see cref="DocumentIADbContext"/>). Contra SQL Server se traducen a la funcion
/// nativa; el cuerpo C# solo corre con proveedores sin traduccion (InMemory en tests)
/// y reproduce la misma semantica.
/// </summary>
public static class SqlJsonFunctions
{
    /// <summary>
    /// JSON_VALUE en modo lax: ruta "$.a.b" sensible a mayusculas, null si no existe o
    /// no es escalar. Con JSON invalido falla, igual que SQL Server (error 13609): no
    /// usarla sobre columnas que puedan contener algo distinto de JSON o null. ISJSON
    /// como guarda valida el documento entero y multiplica el coste de la consulta.
    /// </summary>
    public static string? JsonValue(string? json, string path)
    {
        if (json is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        var actual = doc.RootElement;
        foreach (var segmento in path.TrimStart('$').Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (actual.ValueKind != JsonValueKind.Object || !actual.TryGetProperty(segmento, out actual))
            {
                return null;
            }
        }

        return actual.ValueKind switch
        {
            JsonValueKind.String => actual.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => actual.GetRawText(),
            _ => null
        };
    }
}
