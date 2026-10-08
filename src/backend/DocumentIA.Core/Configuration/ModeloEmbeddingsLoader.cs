using System.Collections.Concurrent;
using System.Text;
using DocumentIA.Core.Services;
using DocumentIA.Core.Services.Classification;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Core.Configuration;

/// <summary>
/// Descarga y parsea el artefacto del clasificador por embeddings, lo cachea en memoria
/// por ruta y lo revalida por ETag cada cinco minutos. Un artefacto inexistente o
/// invalido deja el modelo en nulo con error en log, nunca una excepcion. AB#100779.
/// </summary>
public class ModeloEmbeddingsLoader
{
    public static readonly TimeSpan IntervaloRevalidacion = TimeSpan.FromMinutes(5);

    private sealed class Entrada
    {
        public ModeloEmbeddings? Modelo { get; init; }
        public string? ETag { get; init; }
        public DateTimeOffset ValidadoEn { get; set; }
    }

    private readonly IBlobStorageService _blobs;
    private readonly ILogger<ModeloEmbeddingsLoader>? _logger;
    private readonly Func<DateTimeOffset> _ahora;
    private readonly ConcurrentDictionary<string, Entrada> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _candado = new(1, 1);

    public ModeloEmbeddingsLoader(
        IBlobStorageService blobs,
        ILogger<ModeloEmbeddingsLoader>? logger = null,
        Func<DateTimeOffset>? ahora = null)
    {
        _blobs = blobs;
        _logger = logger;
        _ahora = ahora ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Para dobles de prueba: ObtenerAsync es virtual.</summary>
    protected ModeloEmbeddingsLoader()
    {
        _blobs = null!;
        _ahora = () => DateTimeOffset.UtcNow;
    }

    public virtual async Task<ModeloEmbeddings?> ObtenerAsync(string container, string blobPath, CancellationToken cancellationToken)
    {
        var ruta = $"{container}/{blobPath}";
        if (_cache.TryGetValue(ruta, out var vigente) && _ahora() - vigente.ValidadoEn < IntervaloRevalidacion)
        {
            return vigente.Modelo;
        }

        await _candado.WaitAsync(cancellationToken);
        try
        {
            // Otro hilo puede haber revalidado mientras esperabamos el candado.
            if (_cache.TryGetValue(ruta, out vigente) && _ahora() - vigente.ValidadoEn < IntervaloRevalidacion)
            {
                return vigente.Modelo;
            }

            var etag = await _blobs.GetETagAsync(ruta);
            if (etag is null)
            {
                // Un 404 tras una carga buena se trata como fallo transitorio: se conserva el ultimo modelo.
                _logger?.LogError("El artefacto de embeddings {Ruta} no existe en el storage. Se mantiene la copia en memoria si existe.", ruta);
                return Conservar(ruta, vigente);
            }

            if (vigente?.Modelo is not null && string.Equals(vigente.ETag, etag, StringComparison.Ordinal))
            {
                vigente.ValidadoEn = _ahora();
                return vigente.Modelo;
            }

            var bytes = await _blobs.DownloadDocumentAsync(ruta);
            ModeloEmbeddings modelo;
            try
            {
                modelo = ModeloEmbeddings.Parse(Encoding.UTF8.GetString(bytes));
                _logger?.LogInformation(
                    "Artefacto de embeddings {Ruta} cargado: version {Version}, {Familias} familias, {Dimensiones} dimensiones.",
                    ruta, modelo.Manifiesto.Version, modelo.Tdn1.Clases.Count, modelo.Manifiesto.Dimensiones);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Parse lanza InvalidDataException, pero un JSON bien formado con nulos
                // puede lanzar ArgumentNullException o NullReferenceException: todo es artefacto invalido.
                // Se conserva el ultimo modelo bueno con su ETag: a los cinco minutos se vuelve a
                // descargar y a avisar, hasta que el artefacto se corrija.
                _logger?.LogError(ex, "El artefacto de embeddings {Ruta} no es valido. Se mantiene la copia en memoria si existe.", ruta);
                return Conservar(ruta, vigente);
            }

            _cache[ruta] = new Entrada { Modelo = modelo, ETag = etag, ValidadoEn = _ahora() };
            return modelo;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Fallo del storage: se conserva el ultimo modelo bueno si lo hay y se cachea
            // el resultado para no reintentar la descarga hasta la siguiente revalidacion.
            _logger?.LogError(ex, "No se pudo revalidar el artefacto de embeddings {Ruta}. Se mantiene la copia en memoria si existe.", ruta);
            return Conservar(ruta, vigente);
        }
        finally
        {
            _candado.Release();
        }
    }

    /// <summary>Deja en cache el ultimo modelo bueno (o nulo si no lo habia) y aplaza el siguiente intento cinco minutos.</summary>
    private ModeloEmbeddings? Conservar(string ruta, Entrada? vigente)
    {
        _cache[ruta] = new Entrada { Modelo = vigente?.Modelo, ETag = vigente?.ETag, ValidadoEn = _ahora() };
        return vigente?.Modelo;
    }
}
