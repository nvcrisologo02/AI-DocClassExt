namespace DocumentIA.Core.Models;

/// <summary>
/// Conjunto completo de prompts para el proceso de clasificación en dos fases.
/// </summary>
public sealed class ClassificationPromptSet
{
    /// <summary>
    /// Prompt del sistema para la Fase 1 (clasificación TDN1 - familia).
    /// </summary>
    public required string Phase1SystemPrompt { get; init; }

    /// <summary>
    /// Prompt del usuario para la Fase 1 (clasificación TDN1 - familia).
    /// </summary>
    public required string Phase1UserPrompt { get; init; }

    /// <summary>
    /// Prompt del sistema para la Fase 2 (clasificación TDN2 - específica).
    /// </summary>
    public required string Phase2SystemPrompt { get; init; }

    /// <summary>
    /// Prompt del usuario para la Fase 2 (clasificación TDN2 - específica).
    /// </summary>
    public required string Phase2UserPrompt { get; init; }

    /// <summary>
    /// Prompt del sistema para la clasificación restringida en fase única (AB#100063), contra el
    /// conjunto acotado de tipologías candidatas. No reutiliza Phase1SystemPrompt: aquel incrusta
    /// el formato de respuesta jerárquico ("tdn1"/familias), incompatible con el formato plano
    /// ("tipologia") de este modo.
    /// </summary>
    public required string RestrictedSystemPrompt { get; init; }

    /// <summary>
    /// Prompt del usuario para la clasificación restringida en fase única (AB#100063). No reutiliza
    /// Phase1UserPrompt: aquel etiqueta el catálogo como "Familias TDN1 disponibles", lenguaje
    /// jerárquico que no aplica al catálogo plano restringido.
    /// </summary>
    public required string RestrictedUserPrompt { get; init; }

    /// <summary>
    /// Versión del prompt utilizado (coincide con PromptTemplateEntity.Version).
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Origen de los 4 prompts jerárquicos (Fase 1 / Fase 2): "Database" si provienen de BD,
    /// "Fallback" si provienen de appsettings. AB#100063: el par restringido se resuelve de forma
    /// independiente (ver <see cref="RestrictedSource"/>) y NO afecta este valor.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Origen del par restringido (AB#100063): "Database" si <c>classification.restricted.system</c>
    /// y <c>.user</c> están ambos activos en BD, "Fallback" si se usan las constantes de código de
    /// <c>GptClasificarDataProvider</c> (por ausencia, par incompleto, o error de consulta). Resuelto
    /// independientemente de <see cref="Source"/>.
    /// </summary>
    public string RestrictedSource { get; init; } = "Fallback";

    /// <summary>
    /// Timestamp de cuando se resolvieron los prompts (para auditoría/telemetría).
    /// </summary>
    public DateTime ResolvedAtUtc { get; init; } = DateTime.UtcNow;
}
