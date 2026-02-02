using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace DocumentIA.Core.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        var connectionString = configuration["AzureStorageConnectionString"];
        _blobServiceClient = new BlobServiceClient(connectionString);
        _logger = logger;
    }

    public async Task<string> UploadDocumentAsync(byte[] content, string fileName, string containerName = "documents")
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            var blobPath = GenerateBlobPath(ComputeSHA256(content), fileName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            using var stream = new MemoryStream(content);
            await blobClient.UploadAsync(stream, overwrite: true);

            _logger.LogInformation("Documento subido a blob storage: {BlobPath}", blobPath);
            return $"{containerName}/{blobPath}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subiendo documento a blob storage");
            throw;
        }
    }

    public async Task<byte[]> DownloadDocumentAsync(string blobPath)
    {
        try
        {
            var parts = blobPath.Split('/', 2);
            var containerName = parts[0];
            var blobName = parts[1];

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DownloadAsync();
            using var memoryStream = new MemoryStream();
            await response.Value.Content.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error descargando documento de blob storage: {BlobPath}", blobPath);
            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(string blobPath)
    {
        try
        {
            var parts = blobPath.Split('/', 2);
            var containerName = parts[0];
            var blobName = parts[1];

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DeleteIfExistsAsync();
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando documento de blob storage: {BlobPath}", blobPath);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string blobPath)
    {
        try
        {
            var parts = blobPath.Split('/', 2);
            var containerName = parts[0];
            var blobName = parts[1];

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            return await blobClient.ExistsAsync();
        }
        catch
        {
            return false;
        }
    }

    public string GenerateBlobPath(string sha256, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var year = DateTime.UtcNow.Year;
        var month = DateTime.UtcNow.Month.ToString("D2");
        
        return $"{year}/{month}/{sha256}{extension}";
    }

    private static string ComputeSHA256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
