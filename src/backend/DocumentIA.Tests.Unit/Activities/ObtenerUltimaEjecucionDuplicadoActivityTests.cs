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
    private readonly Mock<ITipologiaRepository> _tipologiaRepository;
    private readonly ObtenerUltimaEjecucionDuplicadoActivity _sut;

    public ObtenerUltimaEjecucionDuplicadoActivityTests()
    {
        _logger = new Mock<ILogger<ObtenerUltimaEjecucionDuplicadoActivity>>();
        _documentoRepository = new Mock<IDocumentoRepository>(MockBehavior.Strict);
        _documentoEjecucionRepository = new Mock<IDocumentoEjecucionRepository>(MockBehavior.Strict);
        _tipologiaRepository = new Mock<ITipologiaRepository>(MockBehavior.Strict);

        _sut = new ObtenerUltimaEjecucionDuplicadoActivity(
            _logger.Object,
            _documentoRepository.Object,
            _documentoEjecucionRepository.Object,
            _tipologiaRepository.Object);
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

    [Fact]
    public async Task Run_WhenStoredOutputHasTdn_KeepsStoredValues()
    {
        var documento = new DocumentoEntity
        {
            Id = 80,
            SHA256 = "sha-tdn-1",
            Tdn1 = "OTRO",
            Tdn2 = "OTRO-99"
        };

        SetupEjecucionConSalida(documento, new ContratoSalida
        {
            Identificacion = new Identificacion
            {
                Documento = "doc.pdf",
                Tipologia = "comu.48",
                Tdn1 = "COMU",
                Tdn2 = "COMU-48"
            }
        });

        var result = await _sut.Run(documento.SHA256);

        result.Should().NotBeNull();
        result!.Identificacion.Tdn1.Should().Be("COMU");
        result.Identificacion.Tdn2.Should().Be("COMU-48");
        _tipologiaRepository.Verify(r => r.GetByCodigoAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_WhenStoredOutputLacksTdn_FillsFromDocumentoEntity()
    {
        var documento = new DocumentoEntity
        {
            Id = 81,
            SHA256 = "sha-tdn-2",
            Tdn1 = "COMU",
            Tdn2 = "COMU-48"
        };

        SetupEjecucionConSalida(documento, new ContratoSalida
        {
            Identificacion = new Identificacion
            {
                Documento = "doc.pdf",
                Tipologia = "comu.48"
            }
        });

        var result = await _sut.Run(documento.SHA256);

        result.Should().NotBeNull();
        result!.Identificacion.Tdn1.Should().Be("COMU");
        result.Identificacion.Tdn2.Should().Be("COMU-48");
        _tipologiaRepository.Verify(r => r.GetByCodigoAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_WhenStoredOutputAndEntityLackTdn_FillsFromTipologiaConfig()
    {
        var documento = new DocumentoEntity
        {
            Id = 82,
            SHA256 = "sha-tdn-3"
        };

        SetupEjecucionConSalida(documento, new ContratoSalida
        {
            Identificacion = new Identificacion
            {
                Documento = "doc.pdf",
                Tipologia = "comu.48"
            }
        });

        _tipologiaRepository
            .Setup(r => r.GetByCodigoAsync("comu.48"))
            .ReturnsAsync(new TipologiaEntity
            {
                Codigo = "comu.48",
                ConfiguracionJson = "{\"classification\":{\"tdn1\":\"COMU\",\"tdn2\":\"COMU-48\"}}"
            });

        var result = await _sut.Run(documento.SHA256);

        result.Should().NotBeNull();
        result!.Identificacion.Tdn1.Should().Be("COMU");
        result.Identificacion.Tdn2.Should().Be("COMU-48");
        result.Resultado.ReutilizadaPorDuplicado.Should().BeTrue();
    }

    [Fact]
    public async Task Run_WhenTipologiaDesconocida_LeavesTdnNull()
    {
        var documento = new DocumentoEntity
        {
            Id = 83,
            SHA256 = "sha-tdn-4"
        };

        SetupEjecucionConSalida(documento, new ContratoSalida
        {
            Identificacion = new Identificacion
            {
                Documento = "doc.pdf",
                Tipologia = "Desconocido"
            }
        });

        var result = await _sut.Run(documento.SHA256);

        result.Should().NotBeNull();
        result!.Identificacion.Tdn1.Should().BeNull();
        result.Identificacion.Tdn2.Should().BeNull();
        _tipologiaRepository.Verify(r => r.GetByCodigoAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_WhenTipologiaLookupFails_StillReturnsReusedOutput()
    {
        var documento = new DocumentoEntity
        {
            Id = 84,
            SHA256 = "sha-tdn-5"
        };

        SetupEjecucionConSalida(documento, new ContratoSalida
        {
            Identificacion = new Identificacion
            {
                Documento = "doc.pdf",
                Tipologia = "comu.48"
            }
        });

        _tipologiaRepository
            .Setup(r => r.GetByCodigoAsync("comu.48"))
            .ThrowsAsync(new InvalidOperationException("BD no disponible"));

        var result = await _sut.Run(documento.SHA256);

        result.Should().NotBeNull();
        result!.Identificacion.Tdn1.Should().BeNull();
        result.Identificacion.Tdn2.Should().BeNull();
        result.Resultado.ReutilizadaPorDuplicado.Should().BeTrue();
    }

    private void SetupEjecucionConSalida(DocumentoEntity documento, ContratoSalida salidaHistorica)
    {
        var ejecucion = new DocumentoEjecucionEntity
        {
            Id = 2000 + documento.Id,
            DocumentoId = documento.Id,
            ContratoSalidaCompletoJson = JsonSerializer.Serialize(salidaHistorica)
        };

        _documentoRepository
            .Setup(r => r.GetBySHA256Async(documento.SHA256))
            .ReturnsAsync(documento);

        _documentoEjecucionRepository
            .Setup(r => r.GetByDocumentoIdAsync(documento.Id))
            .ReturnsAsync(new[] { ejecucion });
    }
}
