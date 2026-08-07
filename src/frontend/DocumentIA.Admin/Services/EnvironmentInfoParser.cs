using System.Text.Json;

namespace DocumentIA.Admin.Services;

/// <summary>Extrae información de entorno de la respuesta de management/configuration.</summary>
public static class EnvironmentInfoParser
{
    public static string? GetEnvironment(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("environment", out var env) ? env.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
