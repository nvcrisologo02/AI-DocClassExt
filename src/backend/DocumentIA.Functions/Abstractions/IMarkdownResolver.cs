using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Abstractions;

/// <summary>
/// Politica unica de obtencion y persistencia de markdown (AB#100245). Recibe que se necesita
/// (documento completo o N paginas) y decide entre caller, cache de la ejecucion, base de datos
/// y Document Intelligence Layout, persistiendo lo obtenido con la regla de cobertura.
/// </summary>
public interface IMarkdownResolver
{
    Task<ResultadoMarkdown> ResolverAsync(
        NecesidadMarkdown necesidad,
        ContextoMarkdown contexto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste un markdown que aporto otra actividad (clasificador DI/CU, extraccion CU) con la
    /// misma regla que el obtenido por Layout. Devuelve si llego a escribirse.
    /// </summary>
    Task<bool> PersistirAportadoAsync(
        PersistirMarkdownInput aportado,
        CancellationToken cancellationToken = default);
}
