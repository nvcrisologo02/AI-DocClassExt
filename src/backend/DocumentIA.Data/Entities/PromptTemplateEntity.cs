using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

/// <summary>
/// Entidad que representa una plantilla de prompt configurable almacenada en BBDD.
/// Permite gestionar prompts de clasificación sin recompilar código.
/// Epic: AB#99800, Fase 1: Núcleo técnico.
/// </summary>
[Table("PromptTemplates")]
public class PromptTemplateEntity
{
    /// <summary>
    /// Identificador único de la plantilla de prompt.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Clave lógica del prompt. Ejemplos: classification.phase1.system, classification.phase2.user
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string PromptKey { get; set; } = string.Empty;

    /// <summary>
    /// Versión del prompt (autoincremento numérico por PromptKey).
    /// Mayor número = más reciente.
    /// </summary>
    [Required]
    public int Version { get; set; }

    /// <summary>
    /// Contenido del prompt (texto plano con placeholders).
    /// Máximo 16,000 caracteres según decisiones arquitectónicas.
    /// </summary>
    [Required]
    [MaxLength(16000)]
    [Column(TypeName = "nvarchar(max)")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Indica si esta versión es la activa para su PromptKey + Environment.
    /// Solo puede haber 1 versión activa por (PromptKey, Environment).
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Descripción opcional de la versión (cambios realizados, propósito).
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Fecha de creación del registro (UTC).
    /// </summary>
    [Required]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Usuario que creó el registro (email o identificador).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de última actualización del registro (UTC).
    /// </summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Usuario que actualizó por última vez el registro.
    /// </summary>
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Fecha en que se publicó/activó esta versión (UTC).
    /// </summary>
    public DateTime? PublishedAtUtc { get; set; }

    /// <summary>
    /// Usuario que publicó/activó esta versión.
    /// </summary>
    [MaxLength(100)]
    public string? PublishedBy { get; set; }
}
