using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

/// <summary>
/// Recupera el markdown ya persistido en BD (Documentos.NormalizacionMarkdownCompressed) para un
/// documento ya conocido (por SHA256 o, en su defecto, por MD5). Se usa como respaldo del Paso 2.8
/// del orquestador cuando la extraccion de markdown DI Layout previa a clasificacion falla o no
/// devuelve contenido util.
/// </summary>
public class RecuperarMarkdownPersistidoActivity
{
    private readonly ILogger<RecuperarMarkdownPersistidoActivity> _logger;
    private readonly IDocumentoRepository _documentoRepository;

    public RecuperarMarkdownPersistidoActivity(
        ILogger<RecuperarMarkdownPersistidoActivity> logger,
        IDocumentoRepository documentoRepository)
    {
        _logger = logger;
        _documentoRepository = documentoRepository;
    }

    [Function(nameof(RecuperarMarkdownPersistidoActivity))]
    public async Task<RecuperarMarkdownPersistidoResultado> Run([ActivityTrigger] RecuperarMarkdownPersistidoInput input)
    {
        var documento = await BuscarDocumentoAsync(input);

        if (documento is null)
        {
            _logger.LogWarning(
                "Paso 2.8b: no se encontró documento persistido para {Documento} (SHA256/MD5 no coinciden)",
                input.NombreDocumento);
            return new RecuperarMarkdownPersistidoResultado { Encontrado = false };
        }

        // AB#100169: columna binaria primero; la Base64 es el respaldo del historico sin migrar
        // y de cualquier fila escrita por una version anterior tras una vuelta atras.
        var markdown = MarkdownCompression.Decompress(documento.NormalizacionMarkdownGzip)
            ?? MarkdownCompression.DecompressFromBase64(documento.NormalizacionMarkdownCompressed);

        if (string.IsNullOrWhiteSpace(markdown))
        {
            _logger.LogWarning(
                "Paso 2.8b: documento {DocumentoId} ({Documento}) no tiene markdown persistido utilizable",
                documento.Id,
                input.NombreDocumento);
            return new RecuperarMarkdownPersistidoResultado { Encontrado = false, DocumentoId = documento.Id };
        }

        _logger.LogInformation(
            "Paso 2.8b: markdown recuperado de BD para documento {DocumentoId} ({Documento}), {Len} chars",
            documento.Id,
            input.NombreDocumento,
            markdown.Length);

        return new RecuperarMarkdownPersistidoResultado
        {
            Encontrado = true,
            Markdown = markdown,
            DocumentoId = documento.Id
        };
    }

    private async Task<Data.Entities.DocumentoEntity?> BuscarDocumentoAsync(RecuperarMarkdownPersistidoInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Sha256))
        {
            var documento = await _documentoRepository.GetBySHA256Async(input.Sha256);
            if (documento is not null)
            {
                return documento;
            }
        }

        if (!string.IsNullOrWhiteSpace(input.Md5))
        {
            return await _documentoRepository.GetByMD5Async(input.Md5);
        }

        return null;
    }
}
