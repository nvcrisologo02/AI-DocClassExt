#nullable enable
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

/// <summary>
/// AB#100863: la actividad consulta la proyeccion Id/SHA256 por MD5, no la entidad completa.
/// </summary>
public class VerificarDuplicadoPorMD5ActivityTests
{
    private readonly Mock<IDocumentoRepository> _documentoRepository;
    private readonly VerificarDuplicadoPorMD5Activity _sut;

    public VerificarDuplicadoPorMD5ActivityTests()
    {
        _documentoRepository = new Mock<IDocumentoRepository>(MockBehavior.Strict);
        _sut = new VerificarDuplicadoPorMD5Activity(
            Mock.Of<ILogger<VerificarDuplicadoPorMD5Activity>>(),
            _documentoRepository.Object);
    }

    [Fact]
    public async Task Run_ConDuplicado_DevuelveExisteConSha256()
    {
        _documentoRepository
            .Setup(r => r.GetDuplicadoByMD5Async("md5-1"))
            .ReturnsAsync(new DocumentoDuplicadoMd5(42, "sha-1"));

        var resultado = await _sut.Run("md5-1");

        resultado.Existe.Should().BeTrue();
        resultado.SHA256.Should().Be("sha-1");
        _documentoRepository.Verify(r => r.GetDuplicadoByMD5Async("md5-1"), Times.Once);
    }

    [Fact]
    public async Task Run_SinDuplicado_DevuelveNoExiste()
    {
        _documentoRepository
            .Setup(r => r.GetDuplicadoByMD5Async("md5-2"))
            .ReturnsAsync((DocumentoDuplicadoMd5?)null);

        var resultado = await _sut.Run("md5-2");

        resultado.Existe.Should().BeFalse();
        resultado.SHA256.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_ConMd5Vacio_NoConsultaElRepositorio()
    {
        var resultado = await _sut.Run("   ");

        resultado.Existe.Should().BeFalse();
        _documentoRepository.VerifyNoOtherCalls();
    }
}
