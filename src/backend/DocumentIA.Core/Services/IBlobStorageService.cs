namespace DocumentIA.Core.Services;

public interface IBlobStorageService
{
    Task<string> UploadDocumentAsync(byte[] content, string fileName, string containerName = "documents");
    Task<byte[]> DownloadDocumentAsync(string blobPath);
    Task<bool> DeleteDocumentAsync(string blobPath);
    Task<bool> ExistsAsync(string blobPath);
    string GenerateBlobPath(string sha256, string fileName);
}
