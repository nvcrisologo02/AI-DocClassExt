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

    /// <summary>
    /// Escribe el markdown solo si mejora la cobertura persistida, o si se fuerza. Devuelve las
    /// filas afectadas: 0 cuando no hay fila para ese SHA256 o lo persistido ya es igual o mejor.
    /// Nunca degrada: un markdown de cobertura desconocida solo lo sustituye uno completo. La
    /// condicion va en el propio UPDATE para que dos ejecuciones concurrentes del mismo documento
    /// no se pisen (AB#100250).
    /// </summary>
    Task<int> ActualizarMarkdownSiMejoraAsync(
        string sha256, byte[] gzip, string base64, int paginas, bool completo, bool forzar);
}
