using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

/// <summary>Cuerpo de la petición a Document Intelligence y cómo se resolvió su origen.</summary>
public sealed record DiSource(object Body, bool UsingUrlSource);

/// <summary>
/// Única decisión sobre cómo viaja el documento hacia Document Intelligence: por referencia
/// (urlSource con SAS, que exige que DI alcance el storage) o empujado en el cuerpo
/// (base64Source, que solo exige que lo alcance el Function App).
/// </summary>
public class DocumentIntelligenceSourceResolver
{
    private static readonly TimeSpan SasExpiry = TimeSpan.FromMinutes(30);

    private readonly IBlobStorageService _blobStorageService;
    private readonly DocumentIntelligenceSettings _settings;
    private readonly ILogger<DocumentIntelligenceSourceResolver> _logger;

    public DocumentIntelligenceSourceResolver(
        IBlobStorageService blobStorageService,
        IOptions<DocumentIntelligenceSettings> options,
        ILogger<DocumentIntelligenceSourceResolver> logger)
    {
        _blobStorageService = blobStorageService;
        _settings = options.Value;
        _logger = logger;
    }

    /// <param name="base64Override">Contenido que tiene prioridad sobre el blob.</param>
    /// <param name="base64Entrada">Contenido de la petición; solo se usa si no hay blobPath.</param>
    public virtual async Task<DiSource> ResolveAsync(
        string? blobPath,
        string? base64Override,
        string? base64Entrada,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(base64Override))
        {
            return new DiSource(new { base64Source = base64Override }, false);
        }

        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return new DiSource(new { base64Source = base64Entrada ?? string.Empty }, false);
        }

        if (_settings.UseInlineContent)
        {
            return await DescargarInlineAsync(blobPath, "UseInlineContent activo");
        }

        var sasUrl = await _blobStorageService.GenerateSasUrlAsync(blobPath, SasExpiry);

        if (EsLoopback(sasUrl))
        {
            return await DescargarInlineAsync(blobPath, "SAS local o loopback (Azurite)");
        }

        _logger.LogInformation("DI usando urlSource (SAS) para BlobPath={BlobPath}", blobPath);
        return new DiSource(new { urlSource = sasUrl }, true);
    }

    /// <summary>
    /// Cuerpo inline para el reintento tras un 400 InvalidContent con urlSource.
    /// </summary>
    public async Task<object> BuildInlineBodyAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var source = await DescargarInlineAsync(blobPath, "reintento tras InvalidContent");
        return source.Body;
    }

    private async Task<DiSource> DescargarInlineAsync(string blobPath, string motivo)
    {
        var bytes = await _blobStorageService.DownloadDocumentAsync(blobPath);

        _logger.LogInformation(
            "DI usando base64Source para BlobPath={BlobPath}. Motivo={Motivo}. Bytes={Bytes}",
            blobPath,
            motivo,
            bytes.Length);

        return new DiSource(new { base64Source = Convert.ToBase64String(bytes) }, false);
    }

    private static bool EsLoopback(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
