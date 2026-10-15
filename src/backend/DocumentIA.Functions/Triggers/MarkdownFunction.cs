using System.Net;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Triggers;

/// <summary>
/// Sirve el markdown persistido de un documento (AB#100968). Es la unica via de obtenerlo fuera de
/// la respuesta sincrona del ingest: el contrato persistido se guarda sin el (AB#100169) y las
/// reutilizaciones por duplicado lo rehidratan tal cual. Solo lee base de datos; nunca llama a
/// Layout ni escribe nada.
/// </summary>
public class MarkdownFunction
{
    private const string ContentType = "text/markdown; charset=utf-8";

    private readonly IDocumentoRepository _documentos;
    private readonly IDocumentoEjecucionRepository _ejecuciones;
    private readonly ILogger<MarkdownFunction> _logger;

    public MarkdownFunction(
        IDocumentoRepository documentos,
        IDocumentoEjecucionRepository ejecuciones,
        ILogger<MarkdownFunction> logger)
    {
        _documentos = documentos;
        _ejecuciones = ejecuciones;
        _logger = logger;
    }

    [Function("GetMarkdownPorDocumento")]
    public async Task<HttpResponseData> GetPorDocumento(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "documentos/{sha256}/markdown")] HttpRequestData req,
        string sha256)
    {
        var documento = await _documentos.GetBySHA256Async(sha256);
        if (documento is null)
        {
            _logger.LogInformation("GetMarkdownPorDocumento: documento {Sha256} no encontrado", sha256);
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        return Responder(req, documento);
    }

    [Function("GetMarkdownPorEjecucion")]
    public async Task<HttpResponseData> GetPorEjecucion(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ejecuciones/{guid}/markdown")] HttpRequestData req,
        string guid)
    {
        var ejecucion = await _ejecuciones.GetByGuidAsync(guid);
        if (ejecucion is null)
        {
            _logger.LogInformation("GetMarkdownPorEjecucion: ejecucion {Guid} no encontrada", guid);
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var documento = await _documentos.GetByIdAsync(ejecucion.DocumentoId);
        if (documento is null)
        {
            _logger.LogWarning(
                "GetMarkdownPorEjecucion: la ejecucion {Guid} apunta al documento {DocumentoId}, que no existe",
                guid, ejecucion.DocumentoId);
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        return Responder(req, documento);
    }

    private HttpResponseData Responder(HttpRequestData req, DocumentoEntity documento)
    {
        // La forma binaria es la actual; la Base64 es el mismo GZip y cubre las filas historicas
        // sin migrar (AB#100169).
        var gzip = documento.NormalizacionMarkdownGzip ?? DesdeBase64(documento.NormalizacionMarkdownCompressed);
        if (gzip is null || gzip.Length == 0)
        {
            _logger.LogInformation("Markdown no persistido para el documento {Sha256}", documento.SHA256);
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        // La cobertura forma parte del validador: una ejecucion posterior puede sustituir un
        // recorte por el documento entero bajo el mismo SHA256 y el cliente debe enterarse.
        var paginas = documento.MarkdownPaginas ?? 0;
        var etag = $"\"{documento.SHA256}-{paginas}-{(documento.MarkdownCompleto ? "completo" : "parcial")}\"";

        if (CoincideIfNoneMatch(req, etag))
        {
            var noModificado = req.CreateResponse(HttpStatusCode.NotModified);
            noModificado.Headers.Add("ETag", etag);
            return noModificado;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", ContentType);
        response.Headers.Add("ETag", etag);
        response.Headers.Add("X-Markdown-Paginas", paginas.ToString());
        response.Headers.Add("X-Markdown-Completo", documento.MarkdownCompleto ? "true" : "false");

        if (AceptaGzip(req))
        {
            // Lo almacenado ya es GZip: se envia tal cual, sin descomprimir ni recomprimir.
            response.Headers.Add("Content-Encoding", "gzip");
            response.Body.Write(gzip, 0, gzip.Length);
            return response;
        }

        var texto = MarkdownCompression.Decompress(gzip);
        if (texto is null)
        {
            _logger.LogError("Markdown persistido del documento {Sha256} no es GZip valido", documento.SHA256);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        response.WriteString(texto);
        return response;
    }

    private static byte[]? DesdeBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool AceptaGzip(HttpRequestData req) =>
        req.Headers.TryGetValues("Accept-Encoding", out var valores)
        && valores.SelectMany(v => v.Split(',')).Any(e => e.Trim().StartsWith("gzip", StringComparison.OrdinalIgnoreCase));

    private static bool CoincideIfNoneMatch(HttpRequestData req, string etag)
    {
        if (!req.Headers.TryGetValues("If-None-Match", out var valores))
        {
            return false;
        }

        return valores
            .SelectMany(v => v.Split(','))
            .Select(e => e.Trim())
            .Any(e => e == "*" || string.Equals(e, etag, StringComparison.Ordinal));
    }
}
