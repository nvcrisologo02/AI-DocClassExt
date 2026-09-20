using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories
{
    public interface IDocumentoEjecucionRepository
    {
        Task<DocumentoEjecucionEntity?> GetByIdAsync(int id);
        Task<DocumentoEjecucionEntity?> GetByGuidAsync(string guid);
        Task<IEnumerable<DocumentoEjecucionEntity>> GetByDocumentoIdAsync(int documentoId);
        Task<DocumentoEjecucionEntity> AddAsync(DocumentoEjecucionEntity ejecucion);
        Task<IEnumerable<DocumentoEjecucionEntity>> GetUltimasEjecucionesAsync(int top = 10);
        Task<EjecucionAgregadosResult> GetAgregadosAsync(EjecucionFiltro filtro);
        Task<EjecucionCostesResult> GetCostesAsync(EjecucionFiltro filtro);
        Task<(IReadOnlyList<EjecucionListadoItem> Items, int Total)> GetPagedAsync(
            EjecucionFiltro filtro, int page, int pageSize);

        /// <summary>
        /// Peticiones que se sirvieron con el contrato de la ejecucion indicada, de mas
        /// reciente a mas antigua. Es el sentido de vuelta de la trazabilidad: desde la
        /// ejecucion que produjo el contenido se ven las veces que se reutilizo (AB#100258).
        /// </summary>
        Task<IReadOnlyList<EjecucionListadoItem>> GetReutilizacionesAsync(int ejecucionOriginalId);
    }
}
