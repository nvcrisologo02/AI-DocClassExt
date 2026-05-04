#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class PersistirActivityTests : IDisposable
{
    private readonly Mock<IDocumentoRepository> _documentoRepoMock;
    private readonly Mock<IDocumentoEjecucionRepository> _ejecucionRepoMock;
    private readonly Mock<IAuditoriaRepository> _auditoriaRepoMock;
    private readonly DocumentIADbContext _context;
    private readonly PersistirActivity _sut;

    public PersistirActivityTests()
    {
        _documentoRepoMock = new Mock<IDocumentoRepository>();
        _ejecucionRepoMock = new Mock<IDocumentoEjecucionRepository>();
        _auditoriaRepoMock = new Mock<IAuditoriaRepository>();

        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DocumentIADbContext(options);

        var telemetryClient = new TelemetryClient(new TelemetryConfiguration { DisableTelemetry = true });

        _sut = new PersistirActivity(
            new Mock<ILogger<PersistirActivity>>().Object,
            _documentoRepoMock.Object,
            _ejecucionRepoMock.Object,
            _auditoriaRepoMock.Object,
            _context,
            telemetryClient);
    }

    private static ContratoSalida BuildSalidaMinima(string sha256 = "abc123sha256")
        => new()
        {
            Identificacion = new Identificacion
            {
                Documento = "test.pdf",
                Guid = Guid.NewGuid().ToString(),
                Tipologia = "NDS",
                FechaProceso = DateTime.UtcNow
            },
            Integridad = new Integridad
            {
                SHA256 = sha256,
                MD5 = "abc123md5",
                CRC32 = "AABBCCDD"
            },
            Resultado = new ResultadoFinal
            {
                Estado = "OK",
                ConfianzaGlobal = 0.95
            }
        };

    [Fact]
    public async Task Run_DocumentoNuevo_LlamaAddAsync()
    {
        const string sha256 = "sha256_nuevo_doc";
        var salida = BuildSalidaMinima(sha256);
        var documentoCreado = new DocumentoEntity { Id = 1, SHA256 = sha256 };

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync(documentoCreado);
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(salida);

        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEntity>()), Times.Once);
        _documentoRepoMock.Verify(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()), Times.Never);
    }

    [Fact]
    public async Task Run_DocumentoExistente_LlamaUpdateAsync()
    {
        const string sha256 = "sha256_existente";
        var salida = BuildSalidaMinima(sha256);
        var documentoExistente = new DocumentoEntity { Id = 5, SHA256 = sha256 };

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync(documentoExistente);
        _documentoRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()))
            .Returns(Task.CompletedTask);
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(salida);

        _documentoRepoMock.Verify(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()), Times.Once);
        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEntity>()), Times.Never);
    }

    [Fact]
    public async Task Run_DocumentoNuevo_LlamaEjecucionRepoAddAsync()
    {
        const string sha256 = "sha256_ejecucion";
        var salida = BuildSalidaMinima(sha256);
        var documentoCreado = new DocumentoEntity { Id = 2, SHA256 = sha256 };

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync(documentoCreado);
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(salida);

        _ejecucionRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()), Times.Once);
    }

    [Fact]
    public async Task Run_DocumentoNuevo_LlamaAuditoriaRepoAddAsync()
    {
        const string sha256 = "sha256_auditoria";
        var salida = BuildSalidaMinima(sha256);
        var documentoCreado = new DocumentoEntity { Id = 3, SHA256 = sha256 };

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync(documentoCreado);
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(salida);

        _auditoriaRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditoriaEntity>()), Times.Once);
    }

    [Fact]
    public async Task Run_DocumentoNuevo_ResultadoProcesamientoGuardadoEnContexto()
    {
        const string sha256 = "sha256_resultado_proc";
        var salida = BuildSalidaMinima(sha256);
        var documentoCreado = new DocumentoEntity { Id = 10, SHA256 = sha256 };

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync(documentoCreado);
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(salida);

        _context.ResultadosProcesamiento.Should().HaveCount(1);
        _context.ResultadosProcesamiento.First().DocumentoId.Should().Be(10);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
