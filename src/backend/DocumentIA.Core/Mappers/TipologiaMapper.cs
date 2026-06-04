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
/// - FromRequestDto(): Parsear request, migrar legacy fields → ConfiguracionJson
/// 
/// Status: En producción desde v1.4
/// </summary>
public class TipologiaMapper
{
    private readonly ILogger<TipologiaMapper> _logger;

    public TipologiaMapper(ILogger<TipologiaMapper> logger)
    {
        _logger = logger;
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
    public TipologiaResponseDtoLegacy ToResponseDtoLegacy(
        DocumentIA.Data.Entities.TipologiaEntity entity)
    {
        _logger.LogWarning(
            "Legacy response format requested for tipologia {Codigo}. " +
            "Deprecated fields: PromptGPT, ModeloClasificacionDI, etc. " +
            "Client should migrate to v1.4+ format.",
            entity.Codigo
        );

        return TipologiaResponseDtoLegacy.FromEntity(entity);
    }

    /// <summary>
    /// Convierte TipologiaRequestDto → TipologiaEntity.
    /// 
    /// Maneja:
    /// - Legacy fields (PromptGPT, Modelo*, Umbral*) → Migra a ConfiguracionJson
    /// - Valida JSON de entrada
    /// - Loguea migraciones automáticas
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

        // Procesar ConfiguracionJson, migrando legacy fields si existen
        entity.ConfiguracionJson = ProcessConfiguracionJson(
            request.ConfiguracionJson,
            request.PromptGPT,
            request.ModeloClasificacionDI,
            request.UmbralClasificacion,
            request.ModeloExtraccionDI,
            request.UmbralExtraccion
        );

        return entity;
    }

    /// <summary>
    /// Parsea y normaliza ConfiguracionJson.
    /// Si legacy fields están presentes, los migra a JSON.
    /// </summary>
    private string ProcessConfiguracionJson(
        string newConfigJson,
        string? legacyPromptGPT = null,
        string? legacyModeloClasificacion = null,
        double? legacyUmbralClasificacion = null,
        string? legacyModeloExtraccion = null,
        double? legacyUmbralExtraccion = null)
    {
        // Parse existing JSON
        TipologiaValidationConfig config;
        
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            config = System.Text.Json.JsonSerializer.Deserialize<TipologiaValidationConfig>(
                newConfigJson,
                options
            ) ?? new TipologiaValidationConfig();
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning("Invalid JSON in ConfiguracionJson: {Error}. Using empty config.", ex.Message);
            config = new TipologiaValidationConfig();
        }

        // Migrar legacy fields a JSON si existen y JSON no los tiene
        bool migrationOccurred = false;

        if (!string.IsNullOrWhiteSpace(legacyPromptGPT))
        {
            if (config.PromptConfig?.SystemPrompt == null)
            {
                if (config.PromptConfig == null)
                {
                    config.PromptConfig = new TipologiaPromptConfig();
                }
                config.PromptConfig.SystemPrompt = legacyPromptGPT;
                _logger.LogInformation("Migrated legacy PromptGPT → ConfiguracionJson.PromptConfig");
                migrationOccurred = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(legacyModeloClasificacion))
        {
            if (config.ClassificationTdn?.Modelo == null)
            {
                if (config.ClassificationTdn == null)
                {
                    config.ClassificationTdn = new ClassificationTdnConfig();
                }
                config.ClassificationTdn.Modelo = legacyModeloClasificacion;
                _logger.LogInformation("Migrated legacy ModeloClasificacionDI → ConfiguracionJson.Classification.Modelo");
                migrationOccurred = true;
            }
        }

        if (legacyUmbralClasificacion.HasValue && legacyUmbralClasificacion > 0)
        {
            if (config.ClassificationPolicy?.UmbralMinimo == null)
            {
                if (config.ClassificationPolicy == null)
                {
                    config.ClassificationPolicy = new ClassificationPolicyConfig();
                }
                config.ClassificationPolicy.UmbralMinimo = legacyUmbralClasificacion.Value;
                _logger.LogInformation("Migrated legacy UmbralClasificacion → ConfiguracionJson.ClassificationPolicy.UmbralMinimo");
                migrationOccurred = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(legacyModeloExtraccion))
        {
            if (config.Extraction?.Modelo == null)
            {
                if (config.Extraction == null)
                {
                    config.Extraction = new TipologiaExtractionConfig();
                }
                config.Extraction.Modelo = legacyModeloExtraccion;
                _logger.LogInformation("Migrated legacy ModeloExtraccionDI → ConfiguracionJson.Extraction.Modelo");
                migrationOccurred = true;
            }
        }

        if (legacyUmbralExtraccion.HasValue && legacyUmbralExtraccion > 0)
        {
            if (config.Extraction?.UmbralMinimo == null)
            {
                if (config.Extraction == null)
                {
                    config.Extraction = new TipologiaExtractionConfig();
                }
                config.Extraction.UmbralMinimo = legacyUmbralExtraccion.Value;
                _logger.LogInformation("Migrated legacy UmbralExtraccion → ConfiguracionJson.Extraction.UmbralMinimo");
                migrationOccurred = true;
            }
        }

        if (migrationOccurred)
        {
            _logger.LogInformation("Legacy field migration completed. Serializing updated config.");
        }

        // Serializar config de vuelta a JSON
        return System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
}
