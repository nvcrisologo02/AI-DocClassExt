using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

/// <summary>Origen del documento para Document Intelligence: por referencia (urlSource) o inline (bytes).</summary>
public sealed class DiSource
{
    /// <summary>Solo para urlSource y para los fallbacks legados con base64 ya en string.</summary>
    public object? Body { get; }

    /// <summary>Bytes del documento cuando viaja inline. Nunca se convierte a string.</summary>
    public byte[]? InlineBytes { get; }

    public bool UsingUrlSource { get; }

    public static DiSource DesdeUrl(string sasUrl) => new(new { urlSource = sasUrl }, null, true);

    public static DiSource DesdeBase64Legado(string base64) => new(new { base64Source = base64 }, null, false);

    public static DiSource DesdeBytes(byte[] bytes) => new(null, bytes, false);

    private DiSource(object? body, byte[]? inlineBytes, bool usingUrlSource)
    {
        Body = body;
        InlineBytes = inlineBytes;
        UsingUrlSource = usingUrlSource;
    }

    /// <summary>
    /// Cuerpo HTTP listo para enviar. Con bytes inline escribe {"base64Source":"..."} con
    /// Utf8JsonWriter.WriteBase64String sobre un IBufferWriter que apunta a un array del tamaño
    /// del cuerpo más una holgura fija (sin strings ni buffer intermedio) y lo devuelve como
    /// ByteArrayContent sobre ese mismo array.
    /// En los demás casos serializa Body como hasta ahora.
    /// </summary>
    public HttpContent CrearContenido()
    {
        if (InlineBytes is not null)
        {
            // Holgura de 512 bytes: Utf8JsonWriter pide al destino al menos su tamaño de buffer
            // por defecto (256) aunque el JSON sea más corto; el array sobrante no se envía.
            var capacidad = ((InlineBytes.Length + 2) / 3) * 4 + 512;
            var buffer = new byte[capacidad];
            var destino = new BufferFijoWriter(buffer);
            using (var json = new Utf8JsonWriter(destino))
            {
                json.WriteStartObject();
                json.WriteBase64String("base64Source", InlineBytes);
                json.WriteEndObject();
            }

            var contenido = new ByteArrayContent(buffer, 0, destino.Escrito);
            contenido.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            return contenido;
        }

        return new StringContent(JsonSerializer.Serialize(Body), Encoding.UTF8, "application/json");
    }
}

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
            return DiSource.DesdeBase64Legado(base64Override);
        }

        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return DiSource.DesdeBase64Legado(base64Entrada ?? string.Empty);
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
        return DiSource.DesdeUrl(sasUrl);
    }

    /// <summary>
    /// Origen inline (bytes) para el reintento tras un 400 InvalidContent con urlSource.
    /// </summary>
    public async Task<DiSource> BuildInlineSourceAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        return await DescargarInlineAsync(blobPath, "reintento tras InvalidContent");
    }

    private async Task<DiSource> DescargarInlineAsync(string blobPath, string motivo)
    {
        var bytes = await _blobStorageService.DownloadDocumentAsync(blobPath);

        _logger.LogInformation(
            "DI usando base64Source para BlobPath={BlobPath}. Motivo={Motivo}. Bytes={Bytes}",
            blobPath,
            motivo,
            bytes.Length);

        return DiSource.DesdeBytes(bytes);
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

/// <summary>
/// IBufferWriter sobre un array preasignado. Utf8JsonWriter sobre un Stream acumularía el token
/// base64 completo en un buffer interno de ArrayPool; con este destino escribe directamente en
/// el array final, de modo que en memoria solo hay los bytes de entrada y el cuerpo de salida.
/// </summary>
internal sealed class BufferFijoWriter : IBufferWriter<byte>
{
    private readonly byte[] _buffer;
    private int _escrito;

    public BufferFijoWriter(byte[] buffer) => _buffer = buffer;

    public int Escrito => _escrito;

    public void Advance(int count)
    {
        if (count < 0 || count > _buffer.Length - _escrito)
        {
            throw new InvalidOperationException("Se intentó avanzar más allá del buffer fijo.");
        }

        _escrito += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        Comprobar(sizeHint);
        return _buffer.AsMemory(_escrito);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        Comprobar(sizeHint);
        return _buffer.AsSpan(_escrito);
    }

    private void Comprobar(int sizeHint)
    {
        if (sizeHint > _buffer.Length - _escrito)
        {
            throw new InvalidOperationException("El buffer fijo no tiene espacio para el tamaño solicitado.");
        }
    }
}
