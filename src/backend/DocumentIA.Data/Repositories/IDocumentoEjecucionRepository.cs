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

        /// <summary>
        /// Ejecuciones propias (no reutilizaciones, no DUPLICADO vacio, no estados de fallo: EstadoEjecucion.NoReutilizables) del documento con la
        /// clave de intencion dada, de mas reciente a mas antigua, sin Includes (AB#100930).
        /// El contrato se comprueba en memoria. Lo sirve IX_DocumentoEjecuciones_Documento_Clave.
        /// </summary>
        Task<IReadOnlyList<DocumentoEjecucionEntity>> GetCandidatasPorClaveAsync(int documentoId, string claveIntencion, int skip, int take);

        /// <summary>Ejecuciones propias con contrato y sin clave: las que hay que derivar al vuelo.</summary>
        Task<IReadOnlyList<DocumentoEjecucionEntity>> GetConContratoSinClaveAsync(int documentoId, int take);

        /// <summary>Hay alguna ejecucion propia con contrato (distingue sin-historica de clave-distinta).</summary>
        Task<bool> TieneEjecucionesConContratoAsync(int documentoId);

        /// <summary>
        /// Hay alguna ejecucion propia con esa clave en estado de fallo (AB#100956): distingue
        /// historica-fallida de clave-distinta cuando el filtro de candidatas no devuelve nada.
        /// </summary>
        Task<bool> TieneFallidasPorClaveAsync(int documentoId, string claveIntencion);

        /// <summary>Graba la clave derivada en una fila antigua para no repetir la derivacion.</summary>
        Task ActualizarClaveIntencionAsync(int ejecucionId, string claveIntencion, string componentesJson);
    }
}
