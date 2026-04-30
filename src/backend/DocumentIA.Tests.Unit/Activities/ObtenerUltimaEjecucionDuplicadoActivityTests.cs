#nullable enable
using System.Text.Json;
using DocumentIA.Core.Models;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class ObtenerUltimaEjecucionDuplicadoActivityTests
{
    private readonly Mock<ILogger<ObtenerUltimaEjecucionDuplicadoActivity>> _logger;
    private readonly Mock<IDocumentoRepository> _documentoRepository;
    private readonly Mock<IDocumentoEjecucionRepository> _documentoEjecucionRepository;
    private readonly ObtenerUltimaEjecucionDuplicadoActivity _sut;

    public ObtenerUltimaEjecucionDuplicadoActivityTests()
    {
        _logger = new Mock<ILogger<ObtenerUltimaEjecucionDuplicadoActivity>>();
        _documentoRepository = new Mock<IDocumentoRepository>(MockBehavior.Strict);
        _documentoEjecucionRepository = new Mock<IDocumentoEjecucionRepository>(MockBehavior.Strict);

        _sut = new ObtenerUltimaEjecucionDuplicadoActivity(
            _logger.Object,
            _documentoRepository.Object,
            _documentoEjecucionRepository.Object);
    }

    [Fact]
    public async Task Run_WhenDocumentDoesNotExist_ReturnsNull()
    {
        _documentoRepository
            .Setup(r => r.GetBySHA256Async("sha-1"))
            .ReturnsAsync((DocumentoEntity?)null);

        var result = await _sut.Run("sha-1");

        result.Should().BeNull();
        _documentoRepository.Verify(r => r.GetBySHA256Async("sha-1"), Times.Once);
        _documentoEjecucionRepository.Verify(
            r => r.GetByDocumentoIdAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WhenLastExecutionHasSerializedOutput_ReturnsOutputMarkedAsDuplicateReuse()
    {
        var documento = new DocumentoEntity
        {
            Id = 55,
            SHA256 = "sha-2"
        };

        var salidaHistorica = new ContratoSalida
        {
            Identificacion = new Identificacion
            {
                Documento = "doc.pdf"
            },
            Resultado = new ResultadoFinal
            {
                Estado = "OK",
                ConfianzaGlobal = 0.98
            }
        };

        var ejecucion = new DocumentoEjecucionEntity
        {
            Id = 999,
            DocumentoId = documento.Id,
            ContratoSalidaCompletoJson = JsonSerializer.Serialize(salidaHistorica)
        };

        _documentoRepository
            .Setup(r => r.GetBySHA256Async("sha-2"))
            .ReturnsAsync(documento);

        _documentoEjecucionRepository
            .Setup(r => r.GetByDocumentoIdAsync(documento.Id))
            .ReturnsAsync(new[] { ejecucion });

        var result = await _sut.Run("sha-2");

        result.Should().NotBeNull();
        result!.Identificacion.Documento.Should().Be("doc.pdf");
        result.Resultado.Estado.Should().Be("OK");
        result.Resultado.ReutilizadaPorDuplicado.Should().BeTrue();
        result.Resultado.MensajeReutilizacion.Should().Contain("ya procesado");
    }

    [Fact]
    public async Task Run_WhenSerializedOutputHasDefaultResultado_RehydratesResultadoFromExecution()
    {
        var documento = new DocumentoEntity
        {
            Id = 56,
            SHA256 = "sha-historic"
        };

        var salidaHistorica = new ContratoSalida
        {
            Identificacion = new Identificacion
            {
                Documento = "doc-historic.pdf"
            },
            Resultado = new ResultadoFinal
            {
                Estado = "OK",
                ConfianzaGlobal = 0,
                EstadoCalidad = string.Empty,
                ConfianzaClasificacion = 0,
                ConfianzaExtraccion = 0,
                ConfianzaValidacion = 0
            },
            DetalleEjecucion = new DetalleEjecucion
            {
                Extraccion = new ResultadoExtraccion
                {
                    ConfianzaExtraccion = 0.81
                },
                Postproceso = new InformacionPostproceso
                {
                    ConfianzaValidacion = 0.73
                }
            }
        };

        var ejecucion = new DocumentoEjecucionEntity
        {
            Id = 1000,
            DocumentoId = documento.Id,
            EstadoFinal = "OK",
            ConfianzaGlobal = 0,
            ConfianzaClasificacion = 0.92,
            ContratoSalidaCompletoJson = JsonSerializer.Serialize(salidaHistorica)
        };

        _documentoRepository
            .Setup(r => r.GetBySHA256Async("sha-historic"))
            .ReturnsAsync(documento);

        _documentoEjecucionRepository
            .Setup(r => r.GetByDocumentoIdAsync(documento.Id))
            .ReturnsAsync(new[] { ejecucion });

        var result = await _sut.Run("sha-historic");

        result.Should().NotBeNull();
        result!.Resultado.Estado.Should().Be("OK");
        result.Resultado.ConfianzaGlobal.Should().Be(0.73);
        result.Resultado.EstadoCalidad.Should().Be("REVISION");
        result.Resultado.ConfianzaClasificacion.Should().Be(0.92);
        result.Resultado.ConfianzaExtraccion.Should().Be(0.81);
        result.Resultado.ConfianzaValidacion.Should().Be(0.73);
        result.Resultado.ReutilizadaPorDuplicado.Should().BeTrue();
        result.Resultado.MensajeReutilizacion.Should().Contain("ya procesado");
    }

    [Fact]
    public async Task Run_WhenNoExecutionHasSerializedOutput_ReturnsNull()
    {
        var documento = new DocumentoEntity
        {
            Id = 77,
            SHA256 = "sha-3"
        };

        var ejecucionSinSalida = new DocumentoEjecucionEntity
        {
            Id = 1001,
            DocumentoId = documento.Id,
            ContratoSalidaCompletoJson = null
        };

        _documentoRepository
            .Setup(r => r.GetBySHA256Async("sha-3"))
            .ReturnsAsync(documento);

        _documentoEjecucionRepository
            .Setup(r => r.GetByDocumentoIdAsync(documento.Id))
            .ReturnsAsync(new[] { ejecucionSinSalida });

        var result = await _sut.Run("sha-3");

        result.Should().BeNull();
    }
}
