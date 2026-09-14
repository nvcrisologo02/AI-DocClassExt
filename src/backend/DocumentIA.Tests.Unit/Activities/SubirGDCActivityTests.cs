#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class SubirGDCActivityTests
{
    private readonly Mock<IGdcService> _gdcServiceMock = new();
    private readonly Mock<IBlobStorageService> _blobServiceMock = new();
    private readonly SubirGDCActivity _sut;

    public SubirGDCActivityTests()
    {
        var loader = new TipologiaConfigLoader(
            new MemoryCache(new MemoryCacheOptions()),
            new Mock<IServiceScopeFactory>().Object);

        _sut = new SubirGDCActivity(
            new Mock<ILogger<SubirGDCActivity>>().Object,
            _gdcServiceMock.Object,
            loader,
            Options.Create(new GdcSettings()),
            _blobServiceMock.Object);
    }

    [Fact]
    public async Task Run_SinBase64ConBlobPath_DescargaDelBlobYSubeElContenido()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        _blobServiceMock
            .Setup(b => b.DownloadDocumentAsync("documents/2026/09/abc.pdf"))
            .ReturnsAsync(bytes);
        _gdcServiceMock
            .Setup(g => g.ConsultarDocumentoAsync("612039", "md5", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (string?)null));
        _gdcServiceMock
            .Setup(g => g.SubirDocumentoAsync(It.IsAny<SubirGDCInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoGDC { Exitoso = true, ObjectId = "GDC-1", Mensaje = "OK" });

        var result = await _sut.Run(new SubirGDCActivity.SubirGDCActivityInput
        {
            Input = new SubirGDCInput
            {
                IdActivo = "612039",
                MD5 = "md5",
                NombreArchivo = "abc.pdf",
                ContenidoBase64 = string.Empty,
                BlobPath = "documents/2026/09/abc.pdf"
            }
        });

        result.Exitoso.Should().BeTrue();
        _gdcServiceMock.Verify(
            g => g.SubirDocumentoAsync(
                It.Is<SubirGDCInput>(i => i.ContenidoBase64 == Convert.ToBase64String(bytes)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_SinBase64NiBlobPath_FallaSinLlamarAGdc()
    {
        var result = await _sut.Run(new SubirGDCActivity.SubirGDCActivityInput
        {
            Input = new SubirGDCInput
            {
                IdActivo = "612039",
                MD5 = "md5",
                NombreArchivo = "abc.pdf",
                ContenidoBase64 = string.Empty,
                BlobPath = null
            }
        });

        result.Should().NotBeNull();
        result.Exitoso.Should().BeFalse();
        result.Mensaje.Should().Contain("sin contenido");
        _gdcServiceMock.Verify(
            g => g.ConsultarDocumentoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gdcServiceMock.Verify(
            g => g.SubirDocumentoAsync(It.IsAny<SubirGDCInput>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
