using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

/// <summary>
/// Repositorio para acceso a plantillas de prompts configurables.
/// </summary>
public interface IPromptTemplateRepository
{
    /// <summary>
    /// Obtiene el prompt activo (IsActive=true) para la clave especificada.
    /// </summary>
    /// <param name="promptKey">Clave del prompt (ej: "classification.phase1.system")</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Entidad del prompt activo o null si no existe ninguno activo</returns>
    Task<PromptTemplateEntity?> GetActivePromptAsync(string promptKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene todos los prompts activos (IsActive=true) con claves que coincidan con el prefijo.
    /// Útil para obtener todos los prompts de clasificación de una vez.
    /// </summary>
    /// <param name="keyPrefix">Prefijo de la clave (ej: "classification.")</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Lista de prompts activos con el prefijo especificado</returns>
    Task<IReadOnlyList<PromptTemplateEntity>> GetActivePromptsByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default);
}
