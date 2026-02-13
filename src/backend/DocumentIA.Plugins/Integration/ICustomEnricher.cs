using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Interfaz publica que deben implementar los enriquecedores custom externos
    /// Permite crear DLLs independientes con logica de negocio
    /// </summary>
    public interface ICustomEnricher
    {
        /// <summary>
        /// Nombre del enriquecedor
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Version del enriquecedor
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Inicializa el enriquecedor con configuracion personalizada
        /// </summary>
        /// <param name="configuration">Configuracion custom del JSON</param>
        Task InitializeAsync(Dictionary<string, object> configuration);

        /// <summary>
        /// Enriquece los datos recibidos aplicando logica de negocio
        /// </summary>
        /// <param name="data">Datos extraidos del documento</param>
        /// <returns>Datos enriquecidos (incluye originales + nuevos campos)</returns>
        Task<Dictionary<string, object>> EnrichAsync(Dictionary<string, object> data);

        /// <summary>
        /// Health check del enriquecedor (validar conexiones, recursos, etc)
        /// </summary>
        Task<bool> HealthCheckAsync();
    }
}
