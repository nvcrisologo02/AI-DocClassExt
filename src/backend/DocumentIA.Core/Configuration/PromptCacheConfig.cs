namespace DocumentIA.Core.Configuration;

/// <summary>
/// Configuración de la caché de prompts de un modelo de clasificación (solo familia GPT-5.6 y posteriores).
/// </summary>
public sealed class PromptCacheConfig
{
    /// <summary>Indica si el modelo envía los campos de caché de prompt. Por defecto desactivada.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Prefijo de la clave de caché (<c>prompt_cache_key</c>).</summary>
    public string KeyPrefix { get; set; } = "documentia-clasif";

    /// <summary>Indica si se marca un punto de corte de caché tras el mensaje de sistema.</summary>
    public bool Breakpoint { get; set; } = true;

    /// <summary>Tiempo de retención de la caché (por ejemplo "30m").</summary>
    public string Ttl { get; set; } = "30m";
}
