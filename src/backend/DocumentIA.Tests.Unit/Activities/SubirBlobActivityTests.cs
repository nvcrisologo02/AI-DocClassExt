#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class SubirBlobActivityTests
{
    private readonly Mock<IBlobStorageService> _blobServiceMock;
    private readonly SubirBlobActivity _sut;

    public SubirBlobActivityTests()
    {
        _blobServiceMock = new Mock<IBlobStorageService>();
        _sut = new SubirBlobActivity(
            new Mock<ILogger<SubirBlobActivity>>().Object,
            _blobServiceMock.Object);
    }

    [Fact]
    public async Task Run_ContenidoValido_LlamaUploadYDevuelvePath()
    {
        const string expectedPath = "documents/abc123/doc.pdf";
        var input = new SubirBlobInput
        {
            ContenidoBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            NombreArchivo = "doc.pdf",
            Contenedor = "documents"
        };
        _blobServiceMock
            .Setup(s => s.UploadDocumentAsync(It.IsAny<byte[]>(), "doc.pdf", "documents"))
            .ReturnsAsync(expectedPath);

        var result = await _sut.Run(input);

        result.Should().Be(expectedPath);
        _blobServiceMock.Verify(
            s => s.UploadDocumentAsync(It.IsAny<byte[]>(), "doc.pdf", "documents"),
            Times.Once);
    }

    [Fact]
    public async Task Run_ContenidoVacio_RetornaStringVacioSinLlamarUpload()
    {
        var input = new SubirBlobInput
        {
            ContenidoBase64 = string.Empty,
            NombreArchivo = "doc.pdf",
            Contenedor = "documents"
        };

        var result = await _sut.Run(input);

        result.Should().BeEmpty();
        _blobServiceMock.Verify(
            s => s.UploadDocumentAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_ContenidoNull_RetornaStringVacioSinLlamarUpload()
    {
        var input = new SubirBlobInput
        {
            ContenidoBase64 = null!,
            NombreArchivo = "doc.pdf",
            Contenedor = "documents"
        };

        var result = await _sut.Run(input);

        result.Should().BeEmpty();
        _blobServiceMock.Verify(
            s => s.UploadDocumentAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_UploadLanzaExcepcion_RetornaStringVacioSinPropagar()
    {
        var input = new SubirBlobInput
        {
            ContenidoBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            NombreArchivo = "doc.pdf",
            Contenedor = "documents"
        };
        _blobServiceMock
            .Setup(s => s.UploadDocumentAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Blob storage no disponible"));

        var act = async () => await _sut.Run(input);

        await act.Should().NotThrowAsync();
        var result = await _sut.Run(input);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_ContenidoValido_BytesDecodificadosCorrectamenteEnUpload()
    {
        var originalBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var input = new SubirBlobInput
        {
            ContenidoBase64 = Convert.ToBase64String(originalBytes),
            NombreArchivo = "test.pdf",
            Contenedor = "documents"
        };
        byte[]? capturedBytes = null;
        _blobServiceMock
            .Setup(s => s.UploadDocumentAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<byte[], string, string>((bytes, _, _) => capturedBytes = bytes)
            .ReturnsAsync("path/test.pdf");

        await _sut.Run(input);

        capturedBytes.Should().BeEquivalentTo(originalBytes);
    }
}
