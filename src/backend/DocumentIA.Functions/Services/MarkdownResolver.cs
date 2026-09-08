using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Orden de resolucion (spec AB#100245): caller > cache de la ejecucion > base de datos si cubre
/// > Layout > base de datos como fallback aunque no cubra > nada. Singleton: abre un scope por
/// operacion de BD porque IDocumentoRepository es Scoped y dos de sus consumidores (Hybrid y
/// ConfigurableClasificar) son Singleton. Mismo patron que TipologiaVersionResolver.
/// </summary>
public sealed class MarkdownResolver : IMarkdownResolver
{
    private readonly ILayoutMarkdownProvider _layout;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarkdownResolver> _logger;

    public MarkdownResolver(
        ILayoutMarkdownProvider layout,
        IServiceScopeFactory scopeFactory,
        ILogger<MarkdownResolver> logger)
    {
        _layout = layout;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<ResultadoMarkdown> ResolverAsync(
        NecesidadMarkdown necesidad,
        ContextoMarkdown contexto,
        CancellationToken cancellationToken = default)
    {
        // 1. Caller: gana siempre y no se persiste. Su validez es la de esta peticion.
        if (!string.IsNullOrWhiteSpace(contexto.MarkdownCaller))
        {
            return new ResultadoMarkdown
            {
                Markdown = contexto.MarkdownCaller,
                Paginas = contexto.TotalPaginas,
                Completo = true,
                Fuente = FuenteMarkdown.Caller
            };
        }

        // 2. Cache de la ejecucion.
        if (contexto.CacheEjecucion is { } cache && cache.Cubre(necesidad))
        {
            return new ResultadoMarkdown
            {
                Markdown = cache.Markdown,
                Paginas = cache.Paginas,
                Completo = cache.Completo,
                Fuente = FuenteMarkdown.CacheEjecucion
            };
        }

        // 3. Base de datos. Se lee siempre (tambien con ForceReprocess) porque hace de fallback
        //    en el paso 5; con ForceReprocess simplemente no se acepta como respuesta aqui.
        var fila = await BuscarFilaAsync(contexto);
        var persistido = DesdeFila(fila);
        if (!contexto.ForceReprocess && persistido is not null && persistido.Cubre(necesidad))
        {
            return persistido;
        }

        // 4. Layout.
        ResultadoMarkdown? layout = null;
        var puedeLlamarALayout = !string.IsNullOrWhiteSpace(contexto.BlobPath)
            || !string.IsNullOrWhiteSpace(contexto.DocumentoBase64);

        if (puedeLlamarALayout)
        {
            try
            {
                layout = await ExtraerConLayoutAsync(necesidad, contexto, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Layout fallo para {Documento} (necesidad completo={Completo}, paginas={Paginas}).",
                    contexto.NombreDocumento,
                    necesidad.DocumentoCompleto,
                    necesidad.PaginasMinimas);
            }
        }
        else
        {
            _logger.LogWarning(
                "Sin BlobPath ni base64 para {Documento}: no se puede llamar a Layout.",
                contexto.NombreDocumento);
        }

        if (layout is { TieneContenido: true })
        {
            layout.Persistido = await PersistirAsync(
                contexto.Sha256, layout.Markdown!, layout.Paginas, layout.Completo, contexto.ForceReprocess, cancellationToken);
            return layout;
        }

        // 5. Fallback: lo que haya en BD, aunque no cubra. Mejor que nada.
        if (persistido is not null)
        {
            _logger.LogWarning(
                "Layout sin resultado para {Documento}; se usa el markdown persistido (paginas={Paginas}, completo={Completo}).",
                contexto.NombreDocumento,
                persistido.Paginas,
                persistido.Completo);
            persistido.Consumos = layout?.Consumos ?? new List<ConsumoIA>();
            return persistido;
        }

        // 6. Nada. Las guardas de contenido de las actividades deciden.
        return new ResultadoMarkdown
        {
            Fuente = FuenteMarkdown.Ninguna,
            Consumos = layout?.Consumos ?? new List<ConsumoIA>()
        };
    }

    public async Task<bool> PersistirAportadoAsync(
        PersistirMarkdownInput aportado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aportado.Sha256) || string.IsNullOrWhiteSpace(aportado.Markdown))
        {
            return false;
        }

        return await PersistirAsync(
            aportado.Sha256, aportado.Markdown, aportado.Paginas, aportado.Completo, aportado.Forzar, cancellationToken);
    }

    private async Task<ResultadoMarkdown> ExtraerConLayoutAsync(
        NecesidadMarkdown necesidad,
        ContextoMarkdown contexto,
        CancellationToken cancellationToken)
    {
        int? paginasSolicitadas = necesidad.DocumentoCompleto ? null : necesidad.PaginasMinimas;

        var resultado = await _layout.ExtraerMarkdownAsync(
            new ExtraerMarkdownLayoutInput
            {
                Tipologia = contexto.Tipologia ?? string.Empty,
                NombreDocumento = contexto.NombreDocumento,
                BlobPath = contexto.BlobPath,
                DocumentoBase64 = contexto.DocumentoBase64 ?? string.Empty,
                PaginasSolicitadas = paginasSolicitadas
            },
            cancellationToken);

        // Completo por definicion si no se pidio rango; o si el documento tenia menos paginas
        // que las pedidas (DI devolvio todas las que hay).
        var completo = paginasSolicitadas is null
            || (contexto.TotalPaginas > 0 && resultado.Paginas >= contexto.TotalPaginas);

        var paginas = resultado.Paginas > 0
            ? resultado.Paginas
            : (completo ? contexto.TotalPaginas : necesidad.PaginasMinimas);

        return new ResultadoMarkdown
        {
            Markdown = resultado.Markdown,
            Paginas = paginas,
            Completo = completo,
            Fuente = FuenteMarkdown.Layout,
            Consumos = resultado.Consumos ?? new List<ConsumoIA>()
        };
    }

    private async Task<DocumentoEntity?> BuscarFilaAsync(ContextoMarkdown contexto)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDocumentoRepository>();

            if (!string.IsNullOrWhiteSpace(contexto.Sha256))
            {
                var porSha = await repo.GetBySHA256Async(contexto.Sha256);
                if (porSha is not null)
                {
                    return porSha;
                }
            }

            if (!string.IsNullOrWhiteSpace(contexto.Md5))
            {
                return await repo.GetByMD5Async(contexto.Md5);
            }

            return null;
        }
        catch (Exception ex)
        {
            // Sin BD se sigue: se tratara como "no hay nada persistido".
            _logger.LogWarning(ex, "No se pudo leer el markdown persistido de {Documento}.", contexto.NombreDocumento);
            return null;
        }
    }

    private static ResultadoMarkdown? DesdeFila(DocumentoEntity? fila)
    {
        if (fila is null)
        {
            return null;
        }

        var markdown = MarkdownCompression.Decompress(fila.NormalizacionMarkdownGzip)
            ?? MarkdownCompression.DecompressFromBase64(fila.NormalizacionMarkdownCompressed);

        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        return new ResultadoMarkdown
        {
            Markdown = markdown,
            Paginas = fila.MarkdownPaginas ?? 0,
            Completo = fila.MarkdownCompleto,
            Fuente = FuenteMarkdown.BaseDatos
        };
    }

    private async Task<bool> PersistirAsync(
        string? sha256, string markdown, int paginas, bool completo, bool forzar, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            return false;
        }

        try
        {
            var gzip = MarkdownCompression.Compress(markdown);
            var base64 = MarkdownCompression.CompressToBase64(markdown);
            if (gzip is null || base64 is null)
            {
                return false;
            }

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDocumentoRepository>();
            var filas = await repo.ActualizarMarkdownSiMejoraAsync(sha256, gzip, base64, paginas, completo, forzar);
            return filas > 0;
        }
        catch (Exception ex)
        {
            // El markdown se usa en la ejecucion igualmente; solo se pierde el ahorro futuro.
            _logger.LogWarning(ex, "No se pudo persistir el markdown de {Sha256}.", sha256);
            return false;
        }
    }
}
