namespace DocumentIA.Core.Services;

public interface IBlobStorageService
{
    Task<string> UploadDocumentAsync(byte[] content, string fileName, string containerName = "documents");
    /// <summary>
    /// Sube un stream directamente a blob storage sin cargar todo en RAM.
    /// Útil para streaming desde HTTP request body en el trigger.
    /// Retorna el blobPath completo: "container/year/month/{sha256}.ext"
    /// </summary>
    Task<string> UploadStreamAsync(Stream stream, string fileName, string containerName = "documents");
    Task<byte[]> DownloadDocumentAsync(string blobPath);
    Task<bool> DeleteDocumentAsync(string blobPath);
    Task<bool> ExistsAsync(string blobPath);
    string GenerateBlobPath(string sha256, string fileName);
    /// <summary>
    /// Genera una SAS URL de lectura para que servicios externos (Azure DI, CU) accedan al blob.
    /// </summary>
    Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan? expiry = null);
}
