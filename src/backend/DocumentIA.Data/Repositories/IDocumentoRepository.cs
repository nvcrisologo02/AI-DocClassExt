using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public interface IDocumentoRepository
{
    Task<DocumentoEntity?> GetByIdAsync(int id);
    Task<DocumentoEntity?> GetByGuidAsync(string guid);
    Task<DocumentoEntity?> GetBySHA256Async(string sha256);
    Task<DocumentoEntity?> GetByMD5Async(string md5);
    Task<DocumentoEntity?> GetByCorrelationIdAsync(string correlationId);
    Task<IEnumerable<DocumentoEntity>> GetAllAsync();
    Task<IEnumerable<DocumentoEntity>> GetByEstadoAsync(string estado);
    Task<IEnumerable<DocumentoEntity>> GetDocumentosConBlobExpiradosAsync(int top);
    Task<DocumentoEntity> AddAsync(DocumentoEntity documento);
    Task UpdateAsync(DocumentoEntity documento);
    Task DeleteAsync(int id);
    Task<bool> ExistsBySHA256Async(string sha256);
}
