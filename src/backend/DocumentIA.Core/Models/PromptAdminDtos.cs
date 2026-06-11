using System.ComponentModel.DataAnnotations;

namespace DocumentIA.Core.Models;

/// <summary>
/// DTO de response para un PromptTemplate.
/// </summary>
public sealed class PromptTemplateDto
{
    public required long Id { get; init; }
    public required string PromptKey { get; init; }
    public required int Version { get; init; }
    public required string Content { get; init; }
    public required bool IsActive { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public string? PublishedBy { get; init; }
}

/// <summary>
/// DTO para listar prompts (vista resumida).
/// </summary>
public sealed class PromptTemplateListItemDto
{
    public required long Id { get; init; }
    public required string PromptKey { get; init; }
    public required int Version { get; init; }
    public required bool IsActive { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public int ContentLength { get; init; }
}

/// <summary>
/// Request para crear un nuevo borrador de PromptTemplate.
/// </summary>
public sealed class CreatePromptTemplateRequest
{
    [Required(ErrorMessage = "PromptKey es requerido.")]
    [StringLength(100, MinimumLength = 5, ErrorMessage = "PromptKey debe tener entre 5 y 100 caracteres.")]
    public required string PromptKey { get; init; }

    [Required(ErrorMessage = "Content es requerido.")]
    [StringLength(16000, MinimumLength = 10, ErrorMessage = "Content debe tener entre 10 y 16000 caracteres.")]
    public required string Content { get; init; }

    [StringLength(500, ErrorMessage = "Description no puede exceder 500 caracteres.")]
    public string? Description { get; init; }

    public string? CreatedBy { get; init; }
}

/// <summary>
/// Request para activar una versión de prompt (desactivará la versión activa anterior).
/// </summary>
public sealed class ActivatePromptVersionRequest
{
    [Required(ErrorMessage = "Id es requerido.")]
    [Range(1, long.MaxValue, ErrorMessage = "Id debe ser un número positivo.")]
    public required long Id { get; init; }

    public string? PublishedBy { get; init; }
}

/// <summary>
/// Request para actualizar un borrador existente (solo si no está activo).
/// </summary>
public sealed class UpdatePromptTemplateRequest
{
    [Required(ErrorMessage = "Content es requerido.")]
    [StringLength(16000, MinimumLength = 10, ErrorMessage = "Content debe tener entre 10 y 16000 caracteres.")]
    public required string Content { get; init; }

    [StringLength(500, ErrorMessage = "Description no puede exceder 500 caracteres.")]
    public string? Description { get; init; }

    public string? UpdatedBy { get; init; }
}

/// <summary>
/// Request para rollback: desactivar versión actual y activar una versión anterior.
/// </summary>
public sealed class RollbackPromptVersionRequest
{
    [Required(ErrorMessage = "PromptKey es requerido.")]
    [StringLength(100, MinimumLength = 5, ErrorMessage = "PromptKey debe tener entre 5 y 100 caracteres.")]
    public required string PromptKey { get; init; }

    [Required(ErrorMessage = "TargetVersion es requerido.")]
    [Range(1, int.MaxValue, ErrorMessage = "TargetVersion debe ser un número positivo.")]
    public required int TargetVersion { get; init; }

    public string? PublishedBy { get; init; }
}

/// <summary>
/// Response de error de validación con detalles.
/// </summary>
public sealed class ValidationErrorResponse
{
    public required string Message { get; init; }
    public Dictionary<string, List<string>>? Errors { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
