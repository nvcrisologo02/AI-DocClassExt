using System.Security.Cryptography;
using System.Text;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Construye la clave de caché de prompt a partir del prefijo, la operación y el texto de sistema.
/// </summary>
public static class PromptCacheKeyBuilder
{
    /// <summary>
    /// Devuelve <c>{keyPrefix}:{operation}:{hash12}</c>, donde hash12 son los 12 primeros caracteres
    /// hexadecimales en minúsculas del SHA-256 (UTF-8) del texto de sistema.
    /// </summary>
    public static string Build(string keyPrefix, string operation, string systemText)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(systemText));
        var hash12 = Convert.ToHexString(hash).ToLowerInvariant()[..12];
        return $"{keyPrefix}:{operation}:{hash12}";
    }
}
