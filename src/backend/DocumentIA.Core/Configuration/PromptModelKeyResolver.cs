namespace DocumentIA.Core.Configuration;

/// <summary>
/// Resuelve la clave de modelo efectiva de un prompt: petición, tipología y, en último
/// lugar, el defecto <c>PromptDefaults:ModelKey</c>. Una clave vacía o solo con espacios
/// cuenta como no informada, de modo que una tipología que el Admin guarda con
/// <c>"modelKey": ""</c> hereda igual que la que no tiene la propiedad.
/// </summary>
public static class PromptModelKeyResolver
{
    public static string Resolve(string? requestModelKey, string? tipologiaModelKey, string? defaultModelKey)
    {
        if (!string.IsNullOrWhiteSpace(requestModelKey))
        {
            return requestModelKey;
        }

        if (!string.IsNullOrWhiteSpace(tipologiaModelKey))
        {
            return tipologiaModelKey;
        }

        if (!string.IsNullOrWhiteSpace(defaultModelKey))
        {
            return defaultModelKey;
        }

        throw new InvalidOperationException("PromptDefaults:ModelKey no configurado");
    }
}
