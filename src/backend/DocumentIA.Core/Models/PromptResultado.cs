namespace DocumentIA.Core.Models;

/// <summary>
/// Resultado de la ejecución del prompt libre de una tipología.
/// </summary>
public class PromptResultado
{
    /// <summary>Nombre del modelo LLM utilizado (deployment name).</summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>Respuesta en texto libre devuelta por el LLM.</summary>
    public string Resultado { get; set; } = string.Empty;

    public string Resumen { get; set; } = string.Empty;

    /// <summary>Duración de la llamada al LLM en milisegundos.</summary>
    public int TiempoMs { get; set; }

    /// <summary>Mensaje de error si la ejecución falló. Null si fue exitosa.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Indica si el resultado fue obtenido en una llamada combinada con el fallback de extracción,
    /// es decir, una única llamada LLM que produjo tanto los campos extraídos como este resultado.
    /// </summary>
    public bool CombinedWithFallback { get; set; }
}
