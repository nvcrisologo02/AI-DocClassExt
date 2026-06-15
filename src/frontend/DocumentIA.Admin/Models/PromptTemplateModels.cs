using System.Text.Json.Serialization;

namespace DocumentIA.Admin.Models;

/// <summary>DTOs para administración de plantillas de prompts configurables.</summary>

/// <summary>Vista resumida de un template de prompt para listados.</summary>
public record PromptTemplateListItemDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("promptKey")] string PromptKey,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("publishedAtUtc")] DateTime? PublishedAtUtc,
    [property: JsonPropertyName("contentLength")] int ContentLength
);

/// <summary>Vista completa de un template de prompt con todos los metadatos.</summary>
public record PromptTemplateDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("promptKey")] string PromptKey,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("updatedAtUtc")] DateTime UpdatedAtUtc,
    [property: JsonPropertyName("createdBy")] string? CreatedBy,
    [property: JsonPropertyName("updatedBy")] string? UpdatedBy,
    [property: JsonPropertyName("publishedAtUtc")] DateTime? PublishedAtUtc,
    [property: JsonPropertyName("publishedBy")] string? PublishedBy
);

/// <summary>Request para crear un nuevo template de prompt.</summary>
public record CreatePromptTemplateRequest(
    [property: JsonPropertyName("promptKey")] string PromptKey,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("description")] string? Description
);

/// <summary>Request para actualizar contenido de un template en draft.</summary>
public record UpdatePromptTemplateRequest(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("updatedBy")] string UpdatedBy
);

/// <summary>Request para activar una versión de un template.</summary>
public record ActivatePromptVersionRequest(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("publishedBy")] string PublishedBy
);

/// <summary>Request para hacer rollback a una versión anterior.</summary>
public record RollbackPromptVersionRequest(
    [property: JsonPropertyName("promptKey")] string PromptKey,
    [property: JsonPropertyName("targetVersion")] int TargetVersion,
    [property: JsonPropertyName("publishedBy")] string PublishedBy
);

/// <summary>Error de validación del servidor con detalles por campo.</summary>
public record ValidationErrorResponse(
    [property: JsonPropertyName("errors")] Dictionary<string, List<string>> Errors
);

/// <summary>Respuesta de error genérica del servidor.</summary>
public record ApiErrorResponse(
    [property: JsonPropertyName("errorCode")] string ErrorCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] string? Details = null
);

/// <summary>Placeholders permitidos en templates de prompts.</summary>
public static class PromptPlaceholders
{
    public const string ContextPrompt = "{CONTEXT_PROMPT}";
    public const string DocumentText = "{DOCUMENT_TEXT}";
    public const string Tdn1Catalog = "{TDN1_CATALOG}";
    public const string Tdn2Catalog = "{TDN2_CATALOG}";
    public const string Tdn1Code = "{TDN1_CODE}";

    public static readonly List<string> AllPlaceholders = new()
    {
        ContextPrompt,
        DocumentText,
        Tdn1Catalog,
        Tdn2Catalog,
        Tdn1Code
    };

    public static readonly Dictionary<string, string> PlaceholderDescriptions = new()
    {
        { ContextPrompt, "Prompt adicional de instrucciones específicas (Phase 1)" },
        { DocumentText, "Contenido del documento en formato texto/markdown (requerido)" },
        { Tdn1Catalog, "Catálogo de familias TDN1 disponibles (Phase 1)" },
        { Tdn2Catalog, "Catálogo de tipologías TDN2 de la familia (Phase 2)" },
        { Tdn1Code, "Código de familia TDN1 resuelta (Phase 2)" }
    };
}

/// <summary>Reglas de validación de placeholders por tipo de prompt.</summary>
public static class PromptKeyRules
{
    private const string Phase1Prefix = "classification.phase1";
    private const string Phase2Prefix = "classification.phase2";

    public static List<string> GetRequiredPlaceholders(string promptKey)
    {
        var normalizedKey = promptKey.ToLowerInvariant();

        if (normalizedKey.Contains(Phase1Prefix))
        {
            return new() { PromptPlaceholders.ContextPrompt, PromptPlaceholders.DocumentText };
        }

        if (normalizedKey.Contains(Phase2Prefix))
        {
            return new() { PromptPlaceholders.Tdn1Code, PromptPlaceholders.DocumentText };
        }

        // Default: DOCUMENT_TEXT es siempre requerido
        return new() { PromptPlaceholders.DocumentText };
    }
}
