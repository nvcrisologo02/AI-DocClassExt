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
    /// Versión del prompt utilizado (coincide con PromptTemplateEntity.Version).
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Origen de los prompts: "Database" si provienen de BD, "Fallback" si provienen de appsettings.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Timestamp de cuando se resolvieron los prompts (para auditoría/telemetría).
    /// </summary>
    public DateTime ResolvedAtUtc { get; init; } = DateTime.UtcNow;
}
