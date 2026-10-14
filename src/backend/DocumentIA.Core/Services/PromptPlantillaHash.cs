using System.Security.Cryptography;
using System.Text;
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Identidad de un prompt ad hoc (AB#100930): plantilla de sistema, plantilla de usuario y
/// clave de modelo. Temperatura, tokens y modo de contenido no cambian "que se pregunto".
/// </summary>
public static class PromptPlantillaHash
{
    public static string? Calcular(PromptInstrucciones? prompt)
    {
        if (prompt is null)
        {
            return null;
        }

        var canonica = string.Join("|",
            (prompt.SystemPrompt ?? string.Empty).Trim(),
            (prompt.UserPromptTemplate ?? string.Empty).Trim(),
            (prompt.ModelKey ?? string.Empty).Trim().ToLowerInvariant());

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonica))).ToLowerInvariant()[..16];
    }
}
