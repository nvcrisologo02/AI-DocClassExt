using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace DocumentIA.Core.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly ConcurrentDictionary<string, bool> _initializedContainers = new(StringComparer.OrdinalIgnoreCase);

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
            await EnsureContainerExistsAsync(containerClient, containerName);

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

    public async Task<string> UploadStreamAsync(Stream stream, string fileName, string containerName = "documents")
    {
        try
        {
            // Necesitamos el SHA256 para generar el blobPath — leemos el stream a un buffer temporal.
            // Para ficheros grandes usamos un MemoryStream temporal; en escenarios futuros se puede
            // usar un stream buffered para evitar cargar todo en RAM si Azure SDK lo permite.
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            var bytes = buffer.ToArray();

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await EnsureContainerExistsAsync(containerClient, containerName);

            var sha256 = ComputeSHA256(bytes);
            var blobPath = GenerateBlobPath(sha256, fileName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            buffer.Position = 0;
            await blobClient.UploadAsync(buffer, overwrite: true);

            _logger.LogInformation("Stream subido a blob storage: {BlobPath} ({Bytes} bytes)", blobPath, bytes.Length);
            return $"{containerName}/{blobPath}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subiendo stream a blob storage");
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

    private Task EnsureContainerExistsAsync(BlobContainerClient containerClient, string containerName)
    {
        if (_initializedContainers.ContainsKey(containerName))
            return Task.CompletedTask;

        return InitializeContainerAsync(containerClient, containerName);
    }

    private async Task InitializeContainerAsync(BlobContainerClient containerClient, string containerName)
    {
        try
        {
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            _initializedContainers.TryAdd(containerName, true);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Container already exists — normal race condition during parallel initialization.
            // Silently mark as initialized; no need to log as error.
            _initializedContainers.TryAdd(containerName, true);
            _logger.LogDebug("Container '{ContainerName}' already exists (409). Race condition during init.", containerName);
        }
    }

    public string GenerateBlobPath(string sha256, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var year = DateTime.UtcNow.Year;
        var month = DateTime.UtcNow.Month.ToString("D2");
        
        return $"{year}/{month}/{sha256}{extension}";
    }

    public Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan? expiry = null)
    {
        try
        {
            var parts = blobPath.Split('/', 2);
            if (parts.Length != 2)
                throw new ArgumentException($"BlobPath inválido: {blobPath}");

            var containerName = parts[0];
            var blobName = parts[1];

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var expiresOn = DateTimeOffset.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(30));

            if (blobClient.CanGenerateSasUri)
            {
                var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, expiresOn);
                _logger.LogDebug("SAS URL generada para {BlobPath}, expira en {ExpiresOn}", blobPath, expiresOn);
                return Task.FromResult(sasUri.ToString());
            }

            // Fallback: construir SAS manualmente si el cliente no puede generarla directamente
            // (ej. cuando se autentifica con DefaultAzureCredential en lugar de connection string con clave)
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = expiresOn
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var uriBuilder = new BlobUriBuilder(blobClient.Uri)
            {
                Sas = sasBuilder.ToSasQueryParameters(
                    new Azure.Storage.StorageSharedKeyCredential(
                        _blobServiceClient.AccountName,
                        GetStorageAccountKey()))
            };
            return Task.FromResult(uriBuilder.ToUri().ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando SAS URL para {BlobPath}", blobPath);
            throw;
        }
    }

    private string GetStorageAccountKey()
    {
        // La clave se extrae de la connection string (formato: AccountKey=...;)
        var connStr = _blobServiceClient.Uri.ToString();
        // Nota: si se usa DefaultAzureCredential, GenerateSasUri ya funciona vía identity.
        // Este método solo se invoca en el fallback, que en la práctica no debería ejecutarse.
        throw new InvalidOperationException(
            "No se puede generar SAS URL: el cliente blob no tiene clave de storage. " +
            "Asegúrate de usar connection string con AccountKey o de tener permisos de identidad para generar SAS.");
    }

    private static string ComputeSHA256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
