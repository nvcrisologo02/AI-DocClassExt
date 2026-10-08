using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

/// <summary>
/// Proyeccion minima del documento que coincide con un MD5: lo unico que necesita la
/// verificacion de duplicados previa a la descarga desde GDC (AB#100863).
/// </summary>
public sealed record DocumentoDuplicadoMd5(int Id, string SHA256);

public interface IDocumentoRepository
{
    Task<DocumentoEntity?> GetByIdAsync(int id);
    Task<DocumentoEntity?> GetByGuidAsync(string guid);
    Task<DocumentoEntity?> GetBySHA256Async(string sha256);
    Task<DocumentoEntity?> GetByMD5Async(string md5);

    /// <summary>
    /// Devuelve Id y SHA256 del documento con ese MD5 sin cargar la entidad ni su Resultado,
    /// o null si no existe. Resuelto en el indice IX_Documentos_MD5 (AB#100863).
    /// </summary>
    Task<DocumentoDuplicadoMd5?> GetDuplicadoByMD5Async(string md5);
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
