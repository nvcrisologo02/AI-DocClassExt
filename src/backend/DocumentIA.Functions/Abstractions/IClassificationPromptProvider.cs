using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Abstractions;

/// <summary>
/// Proveedor de prompts configurables para el proceso de clasificación.
/// Implementa la resolución de prompts con caché y fallback (BD → appsettings).
/// </summary>
public interface IClassificationPromptProvider
{
    /// <summary>
    /// Obtiene el conjunto completo de prompts para clasificación (Fase 1, Fase 2 y clasificación
    /// restringida en fase única, AB#100063). Aplica lógica de resolución: intenta BD con cache de
    /// 120s, fallback a appsettings/constantes de código.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>
    /// Set de prompts con las 6 plantillas (Phase1System/User, Phase2System/User,
    /// RestrictedSystem/User) más metadata.
    /// </returns>
    Task<ClassificationPromptSet> GetPromptSetAsync(CancellationToken cancellationToken = default);
}
