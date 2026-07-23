#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class RecuperarMarkdownPersistidoActivityTests
{
    private readonly Mock<ILogger<RecuperarMarkdownPersistidoActivity>> _logger;
    private readonly Mock<IDocumentoRepository> _documentoRepository;
    private readonly RecuperarMarkdownPersistidoActivity _sut;

    public RecuperarMarkdownPersistidoActivityTests()
    {
        _logger = new Mock<ILogger<RecuperarMarkdownPersistidoActivity>>();
        _documentoRepository = new Mock<IDocumentoRepository>(MockBehavior.Strict);

        _sut = new RecuperarMarkdownPersistidoActivity(_logger.Object, _documentoRepository.Object);
    }

    [Fact]
    public async Task Run_EncontradoPorSha256_DevuelveMarkdownDescomprimido()
    {
        const string markdown = "# Markdown persistido en BD via SHA256";
        var documento = new DocumentoEntity
        {
            Id = 10,
            SHA256 = "sha-1",
            NormalizacionMarkdownCompressed = MarkdownCompression.CompressToBase64(markdown)
        };

        _documentoRepository
            .Setup(r => r.GetBySHA256Async("sha-1"))
            .ReturnsAsync(documento);

        var resultado = await _sut.Run(new RecuperarMarkdownPersistidoInput
        {
            Sha256 = "sha-1",
            Md5 = "md5-1",
            NombreDocumento = "documento.pdf"
        });

        resultado.Encontrado.Should().BeTrue();
        resultado.Markdown.Should().Be(markdown);
        resultado.DocumentoId.Should().Be(10);

        _documentoRepository.Verify(r => r.GetBySHA256Async("sha-1"), Times.Once);
        _documentoRepository.Verify(r => r.GetByMD5Async(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_NoEncontradoPorSha256_HaceFallbackAMd5()
    {
        const string markdown = "# Markdown persistido en BD via MD5 fallback";
        var documento = new DocumentoEntity
        {
            Id = 20,
            SHA256 = "sha-2",
            MD5 = "md5-2",
            NormalizacionMarkdownCompressed = MarkdownCompression.CompressToBase64(markdown)
        };

        _documentoRepository
            .Setup(r => r.GetBySHA256Async("sha-2"))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepository
            .Setup(r => r.GetByMD5Async("md5-2"))
            .ReturnsAsync(documento);

        var resultado = await _sut.Run(new RecuperarMarkdownPersistidoInput
        {
            Sha256 = "sha-2",
            Md5 = "md5-2",
            NombreDocumento = "documento.pdf"
        });

        resultado.Encontrado.Should().BeTrue();
        resultado.Markdown.Should().Be(markdown);
        resultado.DocumentoId.Should().Be(20);

        _documentoRepository.Verify(r => r.GetBySHA256Async("sha-2"), Times.Once);
        _documentoRepository.Verify(r => r.GetByMD5Async("md5-2"), Times.Once);
    }

    [Fact]
    public async Task Run_DocumentoNoExiste_DevuelveNoEncontrado()
    {
        _documentoRepository
            .Setup(r => r.GetBySHA256Async("sha-3"))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepository
            .Setup(r => r.GetByMD5Async("md5-3"))
            .ReturnsAsync((DocumentoEntity?)null);

        var resultado = await _sut.Run(new RecuperarMarkdownPersistidoInput
        {
            Sha256 = "sha-3",
            Md5 = "md5-3",
            NombreDocumento = "documento.pdf"
        });

        resultado.Encontrado.Should().BeFalse();
        resultado.Markdown.Should().BeNull();
    }

    [Fact]
    public async Task Run_DocumentoEncontradoSinMarkdownPersistido_DevuelveNoEncontrado()
    {
        var documento = new DocumentoEntity
        {
            Id = 30,
            SHA256 = "sha-4",
            NormalizacionMarkdownCompressed = null
        };

        _documentoRepository
            .Setup(r => r.GetBySHA256Async("sha-4"))
            .ReturnsAsync(documento);

        var resultado = await _sut.Run(new RecuperarMarkdownPersistidoInput
        {
            Sha256 = "sha-4",
            NombreDocumento = "documento.pdf"
        });

        resultado.Encontrado.Should().BeFalse();
        resultado.DocumentoId.Should().Be(30);
    }
}
