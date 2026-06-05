using System.Text.Json.Serialization;
using DocumentIA.Core.Configuration;

namespace DocumentIA.Core.Models;

/// <summary>
/// DTO de respuesta para GET /api/admin/tipologias.
/// Propósito (AB#99735): NO devolver campos redundantes a consumidores de API.
/// 
/// Cambios en v1.4:
/// - PromptGPT: OMITIDO (leer desde ConfiguracionJson)
/// - ModeloClasificacionDI: OMITIDO (redundante)
/// - UmbralClasificacion: OMITIDO (redundante)
/// - ModeloExtraccionDI: OMITIDO (redundante)
/// - UmbralExtraccion: OMITIDO (redundante)
/// 
/// API backward-compatible: Clientes legacy no recibirán estos campos.
/// Migration guide: Ver DEPRECATION_PROMPTGPT.md
/// 
/// Status: En producción desde v1.4
/// </summary>
public class TipologiaResponseDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("activa")]
    public bool Activa { get; set; }

    [JsonPropertyName("estado")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("configuracionJson")]
    public string ConfiguracionJson { get; set; } = string.Empty;

    [JsonPropertyName("fechaCreacion")]
    public DateTime FechaCreacion { get; set; }

    [JsonPropertyName("fechaActualizacion")]
    public DateTime? FechaActualizacion { get; set; }

    [JsonPropertyName("publicadaEn")]
    public DateTime? PublicadaEn { get; set; }

    [JsonPropertyName("versionPublicada")]
    public string? VersionPublicada { get; set; }

    // ⚠️ DEPRECATED FIELDS - OMITIDAS EN v1.4:
    // - promptGPT (use ConfiguracionJson.PromptConfig)
    // - modeloClasificacionDI (use ConfiguracionJson.Classification)
    // - umbralClasificacion (use ConfiguracionJson.ClassificationPolicy)
    // - modeloExtraccionDI (use ConfiguracionJson.Extraction)
    // - umbralExtraccion (use ConfiguracionJson.Extraction)
    //
    // Para clientes legacy que necesiten estos campos, usar TipologiaResponseDtoLegacy

    /// <summary>
    /// Convierte TipologiaEntity a DTO de respuesta (limpio, sin campos redundantes).
    /// </summary>
    public static TipologiaResponseDto FromEntity(DocumentIA.Data.Entities.TipologiaEntity entity)
    {
        return new TipologiaResponseDto
        {
            Id = entity.Id,
            Codigo = entity.Codigo,
            Nombre = entity.Nombre,
            Version = entity.Version,
            Activa = entity.Activa,
            Estado = entity.Estado.ToString(),
            ConfiguracionJson = entity.ConfiguracionJson ?? "{}",
            FechaCreacion = entity.FechaCreacion,
            FechaActualizacion = entity.FechaActualizacion,
            PublicadaEn = entity.PublicadaEn,
            VersionPublicada = entity.VersionPublicada
        };
    }
}

/// <summary>
/// DTO LEGACY: Incluye campos redundantes para backward compatibility.
/// ⚠️ USAR SOLO en endpoints con fallback para clientes antiguos.
/// 
/// Deprecación: Esta DTO será eliminada en v2.0.
/// Clientes deben migrar a TipologiaResponseDto en v1.5.
/// </summary>
[Obsolete("DEPRECATED: Use TipologiaResponseDto en su lugar. Legacy support solo en endpoints específicos.", false)]
public class TipologiaResponseDtoLegacy
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("activa")]
    public bool Activa { get; set; }

    [JsonPropertyName("estado")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("configuracionJson")]
    public string ConfiguracionJson { get; set; } = string.Empty;

    // ⚠️ DEPRECATED - Solo para legacy clients
    [JsonPropertyName("promptGPT")]
    [Obsolete("Use ConfiguracionJson.PromptConfig instead")]
    public string? PromptGPT { get; set; }

    [JsonPropertyName("modeloClasificacionDI")]
    [Obsolete("Use ConfiguracionJson.Classification instead")]
    public string? ModeloClasificacionDI { get; set; }

    [JsonPropertyName("umbralClasificacion")]
    [Obsolete("Use ConfiguracionJson.ClassificationPolicy instead")]
    public double UmbralClasificacion { get; set; }

    [JsonPropertyName("modeloExtraccionDI")]
    [Obsolete("Use ConfiguracionJson.Extraction instead")]
    public string? ModeloExtraccionDI { get; set; }

    [JsonPropertyName("umbralExtraccion")]
    [Obsolete("Use ConfiguracionJson.Extraction instead")]
    public double UmbralExtraccion { get; set; }

    [JsonPropertyName("fechaCreacion")]
    public DateTime FechaCreacion { get; set; }

    [JsonPropertyName("fechaActualizacion")]
    public DateTime? FechaActualizacion { get; set; }

    [JsonPropertyName("publicadaEn")]
    public DateTime? PublicadaEn { get; set; }

    [JsonPropertyName("versionPublicada")]
    public string? VersionPublicada { get; set; }

    /// <summary>
    /// Convierte TipologiaEntity a DTO legacy (SOLO para backward compatibility).
    /// </summary>
    public static TipologiaResponseDtoLegacy FromEntity(DocumentIA.Data.Entities.TipologiaEntity entity)
    {
        return new TipologiaResponseDtoLegacy
        {
            Id = entity.Id,
            Codigo = entity.Codigo,
            Nombre = entity.Nombre,
            Version = entity.Version,
            Activa = entity.Activa,
            Estado = entity.Estado.ToString(),
            ConfiguracionJson = entity.ConfiguracionJson ?? "{}",
            PromptGPT = entity.PromptGPT,  // ⚠️ DEPRECATED
            ModeloClasificacionDI = entity.ModeloClasificacionDI,  // ⚠️ DEPRECATED
            UmbralClasificacion = entity.UmbralClasificacion,  // ⚠️ DEPRECATED
            ModeloExtraccionDI = entity.ModeloExtraccionDI,  // ⚠️ DEPRECATED
            UmbralExtraccion = entity.UmbralExtraccion,  // ⚠️ DEPRECATED
            FechaCreacion = entity.FechaCreacion,
            FechaActualizacion = entity.FechaActualizacion,
            PublicadaEn = entity.PublicadaEn,
            VersionPublicada = entity.VersionPublicada
        };
    }
}

/// <summary>
/// Request DTO para POST/PUT de tipologías.
/// Aceptar ambos formatos (new + legacy) durante transición v1.4 → v1.5.
/// </summary>
public class TipologiaRequestDto
{
    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("activa")]
    public bool Activa { get; set; } = true;

    [JsonPropertyName("configuracionJson")]
    public string ConfiguracionJson { get; set; } = "{}";

    // ⚠️ DEPRECATED - Solo aceptar durante transición
    [JsonPropertyName("promptGPT")]
    [Obsolete("Incluir en ConfiguracionJson.PromptConfig")]
    public string? PromptGPT { get; set; }

    [JsonPropertyName("modeloClasificacionDI")]
    [Obsolete("Incluir en ConfiguracionJson.Classification")]
    public string? ModeloClasificacionDI { get; set; }

    [JsonPropertyName("umbralClasificacion")]
    [Obsolete("Incluir en ConfiguracionJson.ClassificationPolicy")]
    public double? UmbralClasificacion { get; set; }

    [JsonPropertyName("modeloExtraccionDI")]
    [Obsolete("Incluir en ConfiguracionJson.Extraction")]
    public string? ModeloExtraccionDI { get; set; }

    [JsonPropertyName("umbralExtraccion")]
    [Obsolete("Incluir en ConfiguracionJson.Extraction")]
    public double? UmbralExtraccion { get; set; }
}
