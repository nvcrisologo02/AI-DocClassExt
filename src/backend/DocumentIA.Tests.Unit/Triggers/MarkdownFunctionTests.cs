using System.Net;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Triggers;
using DocumentIA.Tests.Unit.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentIA.Tests.Unit.Triggers;

/// <summary>
/// GET de markdown persistido (AB#100968). Solo lee BD: ningun test configura Layout porque el
/// endpoint no debe llamarlo nunca.
/// </summary>
public class MarkdownFunctionTests
{
    private const string Sha = "334f9beee32e390e76c1c0875d8045c3f3dfd2d07f32b09c8fd6196040f0d819";
    private const string Guid = "1c2ad86e-82da-4377-ba64-10bfb5ab7144";
    private const string Texto = "# Escritura\n\nTexto de la pagina 1.\n\n## Pagina 2\n\nTexto de la pagina 2.";

    private readonly Mock<IDocumentoRepository> _documentos = new(MockBehavior.Strict);
    private readonly Mock<IDocumentoEjecucionRepository> _ejecuciones = new(MockBehavior.Strict);

    private MarkdownFunction CrearSut() =>
        new(_documentos.Object, _ejecuciones.Object, NullLogger<MarkdownFunction>.Instance);

    private static DocumentoEntity DocumentoConMarkdown(int paginas = 2, bool completo = true) => new()
    {
        Id = 128623,
        SHA256 = Sha,
        Paginas = paginas,
        MarkdownPaginas = paginas,
        MarkdownCompleto = completo,
        NormalizacionMarkdownGzip = MarkdownCompression.Compress(Texto),
        NormalizacionMarkdownCompressed = MarkdownCompression.CompressToBase64(Texto)
    };

    [Fact]
    public async Task PorDocumento_SinAccept_DevuelveElTextoDescomprimidoConCoberturaYEtag()
    {
        _documentos.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync(DocumentoConMarkdown());
        var req = HttpFunctionTestFactory.CreateRequest(url: $"http://localhost/api/documentos/{Sha}/markdown");

        var response = await CrearSut().GetPorDocumento(req, Sha);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await HttpFunctionTestFactory.ReadBodyAsync(response)).Should().Be(Texto);
        response.Headers.GetValues("Content-Type").Should().ContainSingle(v => v.StartsWith("text/markdown"));
        response.Headers.Contains("Content-Encoding").Should().BeFalse();
        response.Headers.GetValues("ETag").Should().ContainSingle($"\"{Sha}-2-completo\"");
        response.Headers.GetValues("X-Markdown-Paginas").Should().ContainSingle("2");
        response.Headers.GetValues("X-Markdown-Completo").Should().ContainSingle("true");
    }

    [Fact]
    public async Task PorDocumento_ConAcceptGzip_DevuelveLosBytesAlmacenadosSinRecomprimir()
    {
        var documento = DocumentoConMarkdown();
        _documentos.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync(documento);
        var req = HttpFunctionTestFactory.CreateRequest(
            url: $"http://localhost/api/documentos/{Sha}/markdown",
            headers: new Dictionary<string, string> { ["Accept-Encoding"] = "br, gzip, deflate" });

        var response = await CrearSut().GetPorDocumento(req, Sha);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Content-Encoding").Should().ContainSingle("gzip");
        response.Body.Seek(0, SeekOrigin.Begin);
        using var ms = new MemoryStream();
        await response.Body.CopyToAsync(ms);
        ms.ToArray().Should().Equal(documento.NormalizacionMarkdownGzip!);
    }

    [Fact]
    public async Task PorDocumento_ConIfNoneMatchCoincidente_Devuelve304SinCuerpo()
    {
        _documentos.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync(DocumentoConMarkdown());
        var req = HttpFunctionTestFactory.CreateRequest(
            url: $"http://localhost/api/documentos/{Sha}/markdown",
            headers: new Dictionary<string, string> { ["If-None-Match"] = $"\"otro\", \"{Sha}-2-completo\"" });

        var response = await CrearSut().GetPorDocumento(req, Sha);

        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
        response.Headers.GetValues("ETag").Should().ContainSingle($"\"{Sha}-2-completo\"");
        response.Body.Length.Should().Be(0);
    }

    [Fact]
    public async Task PorDocumento_ElEtagCambiaConLaCobertura()
    {
        // La fila puede pasar de un recorte a documento completo en una ejecucion posterior:
        // el ETag anterior debe dejar de valer.
        _documentos.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync(DocumentoConMarkdown(paginas: 3, completo: false));
        var req = HttpFunctionTestFactory.CreateRequest(
            url: $"http://localhost/api/documentos/{Sha}/markdown",
            headers: new Dictionary<string, string> { ["If-None-Match"] = $"\"{Sha}-2-completo\"" });

        var response = await CrearSut().GetPorDocumento(req, Sha);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("ETag").Should().ContainSingle($"\"{Sha}-3-parcial\"");
        response.Headers.GetValues("X-Markdown-Completo").Should().ContainSingle("false");
    }

    [Fact]
    public async Task PorDocumento_SoloConLaFormaBase64Historica_LaSirveIgual()
    {
        var documento = DocumentoConMarkdown();
        documento.NormalizacionMarkdownGzip = null;
        _documentos.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync(documento);
        var req = HttpFunctionTestFactory.CreateRequest(url: $"http://localhost/api/documentos/{Sha}/markdown");

        var response = await CrearSut().GetPorDocumento(req, Sha);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await HttpFunctionTestFactory.ReadBodyAsync(response)).Should().Be(Texto);
    }

    [Fact]
    public async Task PorDocumento_DocumentoInexistente_Devuelve404()
    {
        _documentos.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync((DocumentoEntity?)null);
        var req = HttpFunctionTestFactory.CreateRequest(url: $"http://localhost/api/documentos/{Sha}/markdown");

        var response = await CrearSut().GetPorDocumento(req, Sha);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PorDocumento_SinMarkdownPersistido_Devuelve404()
    {
        var documento = DocumentoConMarkdown();
        documento.NormalizacionMarkdownGzip = null;
        documento.NormalizacionMarkdownCompressed = null;
        _documentos.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync(documento);
        var req = HttpFunctionTestFactory.CreateRequest(url: $"http://localhost/api/documentos/{Sha}/markdown");

        var response = await CrearSut().GetPorDocumento(req, Sha);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PorEjecucion_ResuelveElDocumentoDeLaEjecucionYDevuelveSuMarkdown()
    {
        _ejecuciones.Setup(r => r.GetByGuidAsync(Guid))
            .ReturnsAsync(new DocumentoEjecucionEntity { Id = 128733, EjecucionGuid = Guid, DocumentoId = 128623 });
        _documentos.Setup(r => r.GetByIdAsync(128623)).ReturnsAsync(DocumentoConMarkdown());
        var req = HttpFunctionTestFactory.CreateRequest(url: $"http://localhost/api/ejecuciones/{Guid}/markdown");

        var response = await CrearSut().GetPorEjecucion(req, Guid);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await HttpFunctionTestFactory.ReadBodyAsync(response)).Should().Be(Texto);
        response.Headers.GetValues("ETag").Should().ContainSingle($"\"{Sha}-2-completo\"");
    }

    [Fact]
    public async Task PorEjecucion_EjecucionInexistente_Devuelve404()
    {
        _ejecuciones.Setup(r => r.GetByGuidAsync(Guid)).ReturnsAsync((DocumentoEjecucionEntity?)null);
        var req = HttpFunctionTestFactory.CreateRequest(url: $"http://localhost/api/ejecuciones/{Guid}/markdown");

        var response = await CrearSut().GetPorEjecucion(req, Guid);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
