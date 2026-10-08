#nullable enable
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Services;

public class DocumentIntelligenceSourceResolverTests
{
    private const string BlobPath = "documents/2026/08/abc123.pdf";
    private static readonly byte[] ContenidoBlob = [1, 2, 3, 4];

    [Fact]
    public void DocumentIntelligenceSettings_PorDefecto_NoUsaContenidoInline()
    {
        var settings = new DocumentIntelligenceSettings();

        settings.UseInlineContent.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_ConBase64Override_UsaEseContenidoYNoTocaElBlob()
    {
        var blob = CrearBlobMock("https://srbstgdevdocai.blob.core.windows.net/x?sig=y");
        var sut = CrearSut(blob, useInlineContent: false);

        var source = await sut.ResolveAsync(BlobPath, "T1VFUkFT", "IGNORADO");

        source.UsingUrlSource.Should().BeFalse();
        source.InlineBytes.Should().BeNull();
        LeerPropiedad(source.Body!, "base64Source").Should().Be("T1VFUkFT");
        blob.Verify(b => b.DownloadDocumentAsync(It.IsAny<string>()), Times.Never);
        blob.Verify(b => b.GenerateSasUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_SinBlobPath_UsaElContenidoDeLaEntrada()
    {
        var blob = CrearBlobMock("https://srbstgdevdocai.blob.core.windows.net/x?sig=y");
        var sut = CrearSut(blob, useInlineContent: false);

        var source = await sut.ResolveAsync(null, null, "REVOTREVNVE8=");

        source.UsingUrlSource.Should().BeFalse();
        source.InlineBytes.Should().BeNull();
        LeerPropiedad(source.Body!, "base64Source").Should().Be("REVOTREVNVE8=");
        blob.Verify(b => b.DownloadDocumentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_ConUseInlineContent_DescargaElBlobYLoMandaInline()
    {
        var blob = CrearBlobMock("https://srbstgdevdocai.blob.core.windows.net/x?sig=y");
        var sut = CrearSut(blob, useInlineContent: true);

        var source = await sut.ResolveAsync(BlobPath, null, null);

        source.UsingUrlSource.Should().BeFalse();
        source.InlineBytes.Should().Equal(ContenidoBlob);
        source.Body.Should().BeNull();
        using var contenido = source.CrearContenido();
        contenido.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await LeerBase64Source(contenido)).Should().Equal(ContenidoBlob);
        blob.Verify(b => b.DownloadDocumentAsync(BlobPath), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ConSasLoopback_DescargaElBlobAunqueElFlagEsteApagado()
    {
        var blob = CrearBlobMock("http://127.0.0.1:10000/devstoreaccount1/documents/abc123.pdf?sig=y");
        var sut = CrearSut(blob, useInlineContent: false);

        var source = await sut.ResolveAsync(BlobPath, null, null);

        source.UsingUrlSource.Should().BeFalse();
        source.InlineBytes.Should().Equal(ContenidoBlob);
        source.Body.Should().BeNull();
        using var contenido = source.CrearContenido();
        contenido.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await LeerBase64Source(contenido)).Should().Equal(ContenidoBlob);
        blob.Verify(b => b.DownloadDocumentAsync(BlobPath), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ConSasRemotaYFlagApagado_UsaUrlSourceSinDescargar()
    {
        const string sas = "https://srbstgprodocai.blob.core.windows.net/documents/abc123.pdf?sig=y";
        var blob = CrearBlobMock(sas);
        var sut = CrearSut(blob, useInlineContent: false);

        var source = await sut.ResolveAsync(BlobPath, null, null);

        source.UsingUrlSource.Should().BeTrue();
        LeerPropiedad(source.Body!, "urlSource").Should().Be(sas);
        blob.Verify(b => b.DownloadDocumentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BuildInlineSourceAsync_DescargaElBlobYDevuelveLosBytes()
    {
        var blob = CrearBlobMock("https://srbstgprodocai.blob.core.windows.net/x?sig=y");
        var sut = CrearSut(blob, useInlineContent: false);

        var source = await sut.BuildInlineSourceAsync(BlobPath);

        source.UsingUrlSource.Should().BeFalse();
        source.InlineBytes.Should().Equal(ContenidoBlob);
        using var contenido = source.CrearContenido();
        contenido.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await LeerBase64Source(contenido)).Should().Equal(ContenidoBlob);
        blob.Verify(b => b.DownloadDocumentAsync(BlobPath), Times.Once);
    }

    [Fact]
    public async Task CrearContenido_ConBytes_NoProduceStringIntermedio_YElJsonEsValido()
    {
        var source = DiSource.DesdeBytes([1, 2, 3, 4]);

        using var contenido = source.CrearContenido();

        (await contenido.ReadAsStringAsync()).Should().Be("{\"base64Source\":\"AQIDBA==\"}");
        contenido.Should().BeOfType<ByteArrayContent>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(3000)]
    [InlineData(1_000_000)]
    [InlineData(5_000_000)]
    public async Task CrearContenido_ConCualquierResto_NoLanzaYDecodificaIgual(int longitud)
    {
        var bytes = new byte[longitud];
        new Random(12345).NextBytes(bytes);

        using var contenido = DiSource.DesdeBytes(bytes).CrearContenido();

        (await LeerBase64Source(contenido)).Should().Equal(bytes);
        // {"base64Source":" (17) + base64 + "} (2)
        contenido.Headers.ContentLength.Should().Be(19 + ((longitud + 2) / 3) * 4);
    }

    [Fact]
    public async Task CrearContenido_DosLlamadas_DevuelvenContenidosIndependientes()
    {
        var source = DiSource.DesdeBytes([1, 2, 3, 4]);

        using var primero = source.CrearContenido();
        using var segundo = source.CrearContenido();

        primero.Should().NotBeSameAs(segundo);
        primero.Should().BeOfType<ByteArrayContent>();
        segundo.Should().BeOfType<ByteArrayContent>();
        (await primero.ReadAsStringAsync()).Should().Be(await segundo.ReadAsStringAsync());
    }

    private static Mock<IBlobStorageService> CrearBlobMock(string sasUrl)
    {
        var blob = new Mock<IBlobStorageService>();
        blob.Setup(b => b.GenerateSasUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(sasUrl);
        blob.Setup(b => b.DownloadDocumentAsync(It.IsAny<string>()))
            .ReturnsAsync(ContenidoBlob);
        return blob;
    }

    private static DocumentIntelligenceSourceResolver CrearSut(
        Mock<IBlobStorageService> blob,
        bool useInlineContent)
    {
        var settings = Options.Create(new DocumentIntelligenceSettings { UseInlineContent = useInlineContent });
        return new DocumentIntelligenceSourceResolver(
            blob.Object,
            settings,
            NullLogger<DocumentIntelligenceSourceResolver>.Instance);
    }

    private static async Task<byte[]> LeerBase64Source(HttpContent content)
    {
        var json = await content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("base64Source").GetBytesFromBase64();
    }

    private static string? LeerPropiedad(object body, string propiedad)
    {
        // El cuerpo es un tipo anónimo; se serializa para inspeccionarlo igual que hace el provider.
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        return doc.RootElement.TryGetProperty(propiedad, out var el) ? el.GetString() : null;
    }
}
