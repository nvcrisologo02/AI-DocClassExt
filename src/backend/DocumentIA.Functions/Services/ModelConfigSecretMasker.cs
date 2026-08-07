using System.Text.Json.Nodes;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Enmascara y desenmascara valores sensibles (ApiKey, Password, Secret, AccountKey) dentro del
/// ConfiguracionJson de un modelo, para evitar exponer secretos en las respuestas del Admin API
/// mientras se preserva un round-trip seguro en las escrituras (PUT) cuando el cliente reenvia
/// el valor enmascarado sin modificarlo.
/// </summary>
public static class ModelConfigSecretMasker
{
    public const string Mask = "***";

    private static readonly string[] SensitiveFragments =
    {
        "apikey",
        "password",
        "secret",
        "accountkey"
    };

    /// <summary>
    /// Indica si el nombre de una propiedad debe tratarse como sensible (contiene, sin distinguir
    /// mayusculas/minusculas, alguno de los fragmentos: "apikey", "password", "secret", "accountkey").
    /// </summary>
    public static bool IsSensitiveKey(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var fragment in SensitiveFragments)
        {
            if (name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Devuelve una copia de <paramref name="configuracionJson"/> en la que todo valor string de una
    /// propiedad sensible (recursivamente, en objetos y arrays) se sustituye por <see cref="Mask"/>.
    /// Si el JSON es invalido o esta vacio, se devuelve tal cual.
    /// </summary>
    public static string? MaskJson(string? configuracionJson)
    {
        if (string.IsNullOrWhiteSpace(configuracionJson))
        {
            return configuracionJson;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(configuracionJson);
        }
        catch
        {
            return configuracionJson;
        }

        MaskNode(root);
        return root?.ToJsonString() ?? configuracionJson;
    }

    /// <summary>
    /// Para cada propiedad sensible del JSON entrante cuyo valor sea exactamente <see cref="Mask"/>,
    /// sustituye por el valor que tenga la misma ruta en <paramref name="storedJson"/> (si existe; en
    /// caso contrario, deja <see cref="Mask"/> tal cual). El resto del JSON entrante se respeta.
    /// Si el JSON entrante es invalido, se devuelve tal cual.
    /// </summary>
    public static string? UnmaskJson(string? incomingJson, string? storedJson)
    {
        if (string.IsNullOrWhiteSpace(incomingJson))
        {
            return incomingJson;
        }

        JsonNode? incomingRoot;
        try
        {
            incomingRoot = JsonNode.Parse(incomingJson);
        }
        catch
        {
            return incomingJson;
        }

        JsonNode? storedRoot = null;
        if (!string.IsNullOrWhiteSpace(storedJson))
        {
            try
            {
                storedRoot = JsonNode.Parse(storedJson);
            }
            catch
            {
                storedRoot = null;
            }
        }

        UnmaskNode(incomingRoot, storedRoot);
        return incomingRoot?.ToJsonString() ?? incomingJson;
    }

    private static void MaskNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    var value = obj[key];
                    if (IsSensitiveKey(key) && value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out _))
                    {
                        obj[key] = JsonValue.Create(Mask);
                    }
                    else
                    {
                        MaskNode(value);
                    }
                }
                break;

            case JsonArray arr:
                foreach (var item in arr)
                {
                    MaskNode(item);
                }
                break;
        }
    }

    private static void UnmaskNode(JsonNode? incoming, JsonNode? stored)
    {
        switch (incoming)
        {
            case JsonObject incomingObj:
                var storedObj = stored as JsonObject;
                foreach (var key in incomingObj.Select(kv => kv.Key).ToList())
                {
                    var value = incomingObj[key];
                    var hasStoredValue = storedObj is not null && storedObj.TryGetPropertyValue(key, out var storedValue) && storedValue is not null;

                    if (IsSensitiveKey(key) && value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue) && stringValue == Mask)
                    {
                        if (hasStoredValue)
                        {
                            incomingObj[key] = storedObj!.TryGetPropertyValue(key, out var restored) && restored is not null
                                ? restored.DeepClone()
                                : JsonValue.Create(Mask);
                        }
                        // Si no existe stored, se deja "***" tal cual.
                    }
                    else
                    {
                        var storedChild = hasStoredValue && storedObj!.TryGetPropertyValue(key, out var child) ? child : null;
                        UnmaskNode(value, storedChild);
                    }
                }
                break;

            case JsonArray incomingArr:
                var storedArr = stored as JsonArray;
                for (var i = 0; i < incomingArr.Count; i++)
                {
                    var storedChild = storedArr is not null && i < storedArr.Count ? storedArr[i] : null;
                    UnmaskNode(incomingArr[i], storedChild);
                }
                break;
        }
    }
}
