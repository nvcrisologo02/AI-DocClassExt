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
    }
}
