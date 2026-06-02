namespace DocumentIA.Functions.Services;

/// <summary>
/// Configuración para trazar prompts enviados a GPT sin exponer texto sensible por defecto.
/// </summary>
public class PromptTracingSettings
{
    /// <summary>Activa/desactiva completamente la traza de prompts.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Si true, incluye fragmentos truncados de prompt en telemetría.
    /// Mantener en false en entornos productivos salvo diagnóstico controlado.
    /// </summary>
    public bool IncludePromptText { get; set; }

    /// <summary>Longitud máxima de fragmentos de prompt cuando IncludePromptText=true.</summary>
    public int MaxPromptTextChars { get; set; } = 512;
}
