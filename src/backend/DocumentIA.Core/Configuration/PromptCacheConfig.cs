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

    /// <summary>Indica si se marcan puntos de corte de caché tras el mensaje de sistema y tras el tramo estable del mensaje de usuario.</summary>
    public bool Breakpoint { get; set; } = true;

    /// <summary>Tiempo de retención de la caché (por ejemplo "30m").</summary>
    public string Ttl { get; set; } = "30m";

    /// <summary>
    /// Modo de caché (<c>prompt_cache_options.mode</c>): <c>implicit</c> o <c>explicit</c>. Con <c>implicit</c> el
    /// servicio marca siempre el último mensaje y escribe el prompt entero (documento incluido) en cada llamada;
    /// con <c>explicit</c> solo se escriben los puntos de corte propios (system y tramo estable del usuario), una vez por TTL.
    /// Un valor vacío o desconocido se trata como <c>implicit</c> (sin aviso).
    /// </summary>
    public string Mode { get; set; } = "implicit";
}
