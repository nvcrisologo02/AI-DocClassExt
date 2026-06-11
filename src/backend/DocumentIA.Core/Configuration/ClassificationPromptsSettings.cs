namespace DocumentIA.Core.Configuration;

/// <summary>
/// Configuración de prompts para el proceso de clasificación en dos fases.
/// Estos prompts se usan como fallback cuando no hay prompts configurables en BD.
/// </summary>
public class ClassificationPromptsSettings
{
    /// <summary>Configuración de prompts para la Fase 1 (clasificación TDN1 - familia).</summary>
    public required ClassificationPhasePromptSettings Phase1 { get; set; }

    /// <summary>Configuración de prompts para la Fase 2 (clasificación TDN2 - específica).</summary>
    public required ClassificationPhasePromptSettings Phase2 { get; set; }
}

/// <summary>
/// Configuración de prompts para una fase específica de clasificación.
/// </summary>
public class ClassificationPhasePromptSettings
{
    /// <summary>
    /// Prompt del sistema que define el rol y comportamiento del modelo.
    /// Define las instrucciones generales para la clasificación.
    /// </summary>
    public required string SystemPrompt { get; set; }

    /// <summary>
    /// Plantilla del prompt de usuario con placeholders a reemplazar en runtime.
    /// Placeholders soportados:
    /// - {CONTEXT_PROMPT}   — Prompt adicional de instrucciones
    /// - {TDN1_CATALOG}     — Catálogo de familias TDN1 (solo Phase1)
    /// - {TDN2_CATALOG}     — Catálogo de tipologías TDN2 de la familia (solo Phase2)
    /// - {TDN1_CODE}        — Código de familia TDN1 resuelta (solo Phase2)
    /// - {DOCUMENT_TEXT}    — Contenido del documento en formato texto/markdown
    /// </summary>
    public required string UserPromptTemplate { get; set; }
}
