using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocumentIA.Core.Mappers;

/// <summary>
/// Mapper para convertir TipologiaEntity ↔ DTOs.
/// Propósito (AB#99735): Normalizar conversiones, eliminar redundancia, mantener backward-compat.
/// 
/// Estrategia:
/// - ToResponseDto(): Respuesta limpia (v1.4+)
/// - ToResponseDtoLegacy(): Respuesta legacy (solo si cliente solicita)
/// - FromRequestDto(): Parsear request, aceptar legacy fields pero priorizar ConfiguracionJson
/// 
/// Status: En producción desde v1.4
/// </summary>
public class TipologiaMapper
{
    private readonly ILogger<TipologiaMapper>? _logger;

    /// <summary>Constructor para inyección de dependencias (producción).</summary>
    public TipologiaMapper(ILogger<TipologiaMapper> logger)
    {
        _logger = logger;
    }

    /// <summary>Constructor por defecto (usado por Moq/Castle en tests).</summary>
    public TipologiaMapper()
    {
        _logger = null;
    }

    /// <summary>
    /// Convierte TipologiaEntity → DTO de respuesta LIMPIO (sin campos redundantes).
    /// Este es el formato por defecto en v1.4+.
    /// </summary>
    public TipologiaResponseDto ToResponseDto(DocumentIA.Data.Entities.TipologiaEntity entity)
    {
        return TipologiaResponseDto.FromEntity(entity);
    }

    /// <summary>
    /// Convierte lista de TipologiaEntity → DTOs limpios (batch).
    /// </summary>
    public List<TipologiaResponseDto> ToResponseDtos(
        IEnumerable<DocumentIA.Data.Entities.TipologiaEntity> entities)
    {
        return entities
            .Select(ToResponseDto)
            .ToList();
    }

    /// <summary>
    /// Convierte TipologiaEntity → DTO LEGACY (backward compatibility).
    /// ⚠️ SOLO usar si cliente solicita explícitamente (header Accept-Version: legacy o query param).
    /// 
    /// Loguea advertencia si se usa (para auditoría de clientes legacy).
    /// </summary>
    #pragma warning disable CS0618 // Type or member is obsolete
    public TipologiaResponseDtoLegacy ToResponseDtoLegacy(
        DocumentIA.Data.Entities.TipologiaEntity entity)
    {
        _logger?.LogWarning(
            "Legacy response format requested for tipologia {Codigo}. " +
            "Deprecated fields: PromptGPT, ModeloClasificacionDI, etc. " +
            "Client should migrate to v1.4+ format.",
            entity.Codigo
        );

        return TipologiaResponseDtoLegacy.FromEntity(entity);
    }
    #pragma warning restore CS0618 // Type or member is obsolete

    /// <summary>
    /// Convierte TipologiaRequestDto → TipologiaEntity.
    /// 
    /// Prioriza ConfiguracionJson sobre legacy fields.
    /// Si ambos están presentes, ConfiguracionJson prevalece (no migración automática compleja).
    /// Loguea si legacy fields son detectados.
    /// </summary>
    public DocumentIA.Data.Entities.TipologiaEntity FromRequestDto(
        TipologiaRequestDto request,
        DocumentIA.Data.Entities.TipologiaEntity? existingEntity = null)
    {
        var entity = existingEntity ?? new DocumentIA.Data.Entities.TipologiaEntity();

        entity.Codigo = request.Codigo;
        entity.Nombre = request.Nombre;
        entity.Version = request.Version;
        entity.Activa = request.Activa;

        // Use ConfiguracionJson as primary source
        // Legacy fields are logged but NOT merged (ConfiguracionJson takes precedence)
        if (!string.IsNullOrWhiteSpace(request.ConfiguracionJson))
        {
            entity.ConfiguracionJson = request.ConfiguracionJson;
            
            #pragma warning disable CS0618 // Type or member is obsolete
            // Log if legacy fields are also present (detect client migration status)
            var hasLegacyFields = 
                !string.IsNullOrWhiteSpace(request.PromptGPT) ||
                !string.IsNullOrWhiteSpace(request.ModeloClasificacionDI) ||
                !string.IsNullOrWhiteSpace(request.ModeloExtraccionDI) ||
                (request.UmbralClasificacion.HasValue && request.UmbralClasificacion > 0) ||
                (request.UmbralExtraccion.HasValue && request.UmbralExtraccion > 0);
            #pragma warning restore CS0618 // Type or member is obsolete
            
            if (hasLegacyFields)
            {
                _logger?.LogWarning(
                    "Request for tipologia {Codigo} includes deprecated fields (PromptGPT, Modelo*, Umbral*). " +
                    "These are ignored in favor of ConfiguracionJson. Client should remove legacy fields.",
                    entity.Codigo
                );
            }
        }
        #pragma warning disable CS0618 // Type or member is obsolete
        else if (!string.IsNullOrWhiteSpace(request.PromptGPT) || 
                 !string.IsNullOrWhiteSpace(request.ModeloClasificacionDI) ||
                 !string.IsNullOrWhiteSpace(request.ModeloExtraccionDI))
        #pragma warning restore CS0618 // Type or member is obsolete
        {
            // If NO ConfiguracionJson provided, create minimal JSON to avoid losing data
            _logger?.LogWarning(
                "Request for tipologia {Codigo} has NO ConfiguracionJson. " +
                "Creating minimal config. Please provide ConfiguracionJson in future requests.",
                entity.Codigo
            );
            entity.ConfiguracionJson = "{}";
        }

        return entity;
    }
}
