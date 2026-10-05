#nullable enable
using System.Text;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using FluentAssertions;
using Moq;

namespace DocumentIA.Tests.Unit.Configuration;

public class ModeloEmbeddingsLoaderTests
{
    private const string Container = "documentai";
    private const string Ruta = "modelos/clasificador-embeddings/v1/clasificador-embeddings-v1.json";
    private const string RutaCompleta = Container + "/" + Ruta;

    private static readonly byte[] FixtureBytes = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "clasificador-embeddings", "clasificador-embeddings-fixture.json"));

    private sealed class Entorno
    {
        public Mock<IBlobStorageService> Blobs { get; } = new();
        public DateTimeOffset Ahora { get; set; } = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        public ModeloEmbeddingsLoader Loader { get; }

        public Entorno(string? etag = "\"0x1\"", byte[]? contenido = null)
        {
            Blobs.Setup(b => b.GetETagAsync(RutaCompleta)).ReturnsAsync(etag);
            Blobs.Setup(b => b.DownloadDocumentAsync(RutaCompleta)).ReturnsAsync(contenido ?? FixtureBytes);
            Loader = new ModeloEmbeddingsLoader(Blobs.Object, logger: null, ahora: () => Ahora);
        }
    }

    [Fact]
    public async Task Obtener_DescargaYParseaElArtefacto()
    {
        var env = new Entorno();

        var modelo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        modelo.Should().NotBeNull();
        modelo!.Manifiesto.Version.Should().Be("fixture");
        env.Blobs.Verify(b => b.DownloadDocumentAsync(RutaCompleta), Times.Once);
    }

    [Fact]
    public async Task Obtener_DentroDeLosCincoMinutos_NoVuelveAlStorage()
    {
        var env = new Entorno();
        await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Ahora = env.Ahora.AddMinutes(4);

        var modelo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        modelo.Should().NotBeNull();
        env.Blobs.Verify(b => b.GetETagAsync(RutaCompleta), Times.Once);
        env.Blobs.Verify(b => b.DownloadDocumentAsync(RutaCompleta), Times.Once);
    }

    [Fact]
    public async Task Obtener_PasadosCincoMinutosConElMismoETag_RevalidaSinDescargar()
    {
        var env = new Entorno();
        await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Ahora = env.Ahora.AddMinutes(6);

        await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        env.Blobs.Verify(b => b.GetETagAsync(RutaCompleta), Times.Exactly(2));
        env.Blobs.Verify(b => b.DownloadDocumentAsync(RutaCompleta), Times.Once);
    }

    [Fact]
    public async Task Obtener_PasadosCincoMinutosConOtroETag_VuelveADescargar()
    {
        var env = new Entorno();
        await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Blobs.Setup(b => b.GetETagAsync(RutaCompleta)).ReturnsAsync("\"0x2\"");
        env.Ahora = env.Ahora.AddMinutes(6);

        await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        env.Blobs.Verify(b => b.DownloadDocumentAsync(RutaCompleta), Times.Exactly(2));
    }

    [Fact]
    public async Task Obtener_BlobInexistente_DevuelveNuloSinLanzar()
    {
        var env = new Entorno(etag: null);

        var modelo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        modelo.Should().BeNull();
        env.Blobs.Verify(b => b.DownloadDocumentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Obtener_ArtefactoInvalido_DevuelveNuloSinLanzar()
    {
        var env = new Entorno(contenido: Encoding.UTF8.GetBytes("{ esto no es json"));

        var modelo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        modelo.Should().BeNull();
    }

    [Fact]
    public async Task Obtener_ArtefactoBienFormadoConNulos_DevuelveNuloYNoVuelveADescargarDentroDeLosCincoMinutos()
    {
        const string json = """{"manifiesto":{"dimensiones":3},"tdn1":{"clases":["a","b"],"coef":null,"intercept":[0]}}""";
        var env = new Entorno(contenido: Encoding.UTF8.GetBytes(json));

        var primero = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Ahora = env.Ahora.AddMinutes(4);
        var segundo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        primero.Should().BeNull();
        segundo.Should().BeNull();
        env.Blobs.Verify(b => b.DownloadDocumentAsync(RutaCompleta), Times.Once);
    }

    [Fact]
    public async Task Obtener_ErrorDeStorageTrasUnaCargaBuena_ConservaElUltimoModelo()
    {
        var env = new Entorno();
        var primero = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Blobs.Setup(b => b.GetETagAsync(RutaCompleta)).ThrowsAsync(new IOException("storage caido"));
        env.Ahora = env.Ahora.AddMinutes(6);

        var segundo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        segundo.Should().BeSameAs(primero);
    }

    [Fact]
    public async Task Obtener_ErrorDeStorageSinModeloPrevio_DevuelveNuloYNoVuelveAlStorageDentroDeLosCincoMinutos()
    {
        var env = new Entorno();
        env.Blobs.Setup(b => b.GetETagAsync(RutaCompleta)).ThrowsAsync(new IOException("storage caido"));

        var primero = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Ahora = env.Ahora.AddMinutes(4);
        var segundo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        primero.Should().BeNull();
        segundo.Should().BeNull();
        env.Blobs.Verify(b => b.GetETagAsync(RutaCompleta), Times.Once);
    }

    [Fact]
    public async Task Obtener_BlobDesaparecidoTrasUnaCargaBuena_ConservaElUltimoModelo()
    {
        var env = new Entorno();
        var primero = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Blobs.Setup(b => b.GetETagAsync(RutaCompleta)).ReturnsAsync((string?)null);
        env.Ahora = env.Ahora.AddMinutes(6);

        var segundo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        segundo.Should().BeSameAs(primero, "un 404 transitorio no debe dejar el clasificador sin modelo");
    }

    [Fact]
    public async Task Obtener_ArtefactoInvalidoTrasUnaCargaBuena_ConservaElUltimoModeloYNoReintentaDentroDeLosCincoMinutos()
    {
        var env = new Entorno();
        var primero = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Blobs.Setup(b => b.GetETagAsync(RutaCompleta)).ReturnsAsync("\"0x2\"");
        env.Blobs.Setup(b => b.DownloadDocumentAsync(RutaCompleta)).ReturnsAsync(Encoding.UTF8.GetBytes("{ esto no es json"));
        env.Ahora = env.Ahora.AddMinutes(6);

        var segundo = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);
        env.Ahora = env.Ahora.AddMinutes(4);
        var tercero = await env.Loader.ObtenerAsync(Container, Ruta, CancellationToken.None);

        segundo.Should().BeSameAs(primero, "un artefacto nuevo roto no debe sustituir al ultimo bueno");
        tercero.Should().BeSameAs(primero);
        env.Blobs.Verify(b => b.DownloadDocumentAsync(RutaCompleta), Times.Exactly(2));
    }
}
