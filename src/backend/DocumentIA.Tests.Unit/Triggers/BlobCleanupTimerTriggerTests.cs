#nullable enable
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Triggers;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Triggers;

public class BlobCleanupTimerTriggerTests
{
    private readonly Mock<ILogger<BlobCleanupTimerTrigger>> _loggerMock = new();
    private readonly Mock<IDocumentoRepository> _documentoRepositoryMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IAuditoriaRepository> _auditoriaRepositoryMock = new();
    private readonly IConfiguration _configuration;
    private readonly TelemetryClient _telemetryClient;

    public BlobCleanupTimerTriggerTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobRetention:BatchSize"] = "200"
            })
            .Build();

        _telemetryClient = new TelemetryClient(new TelemetryConfiguration { DisableTelemetry = true });
    }

    [Fact]
    public async Task Run_BlobNoExiste_LimpiaRutaYAuditaBlobNoEncontrado()
    {
        var documento = new DocumentoEntity
        {
            Id = 1,
            RutaBlobStorage = "documents/2026/05/file1.pdf",
            TamanoBytes = 100
        };

        _documentoRepositoryMock
            .Setup(r => r.GetDocumentosConBlobExpiradosAsync(It.IsAny<int>()))
            .ReturnsAsync(new[] { documento });

        _blobStorageServiceMock
            .Setup(s => s.ExistsAsync(documento.RutaBlobStorage!))
            .ReturnsAsync(false);

        _documentoRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()))
            .Returns(Task.CompletedTask);

        _auditoriaRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        await sut.Run(null);

        _documentoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<DocumentoEntity>(d => d.Id == 1 && d.RutaBlobStorage == null)), Times.Once);
        _auditoriaRepositoryMock.Verify(r => r.AddAsync(It.Is<AuditoriaEntity>(a => a.DocumentoId == 1 && a.Accion == "BlobNoEncontrado" && a.Nivel == "Warning")), Times.Once);
    }

    [Fact]
    public async Task Run_BlobExisteYSeElimina_LimpiaRutaYAuditaBlobEliminado()
    {
        var documento = new DocumentoEntity
        {
            Id = 2,
            RutaBlobStorage = "documents/2026/05/file2.pdf",
            TamanoBytes = 250
        };

        _documentoRepositoryMock
            .Setup(r => r.GetDocumentosConBlobExpiradosAsync(It.IsAny<int>()))
            .ReturnsAsync(new[] { documento });

        _blobStorageServiceMock
            .Setup(s => s.ExistsAsync(documento.RutaBlobStorage!))
            .ReturnsAsync(true);

        _blobStorageServiceMock
            .Setup(s => s.DeleteDocumentAsync(documento.RutaBlobStorage!))
            .ReturnsAsync(true);

        _documentoRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()))
            .Returns(Task.CompletedTask);

        _auditoriaRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        await sut.Run(null);

        _documentoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<DocumentoEntity>(d => d.Id == 2 && d.RutaBlobStorage == null)), Times.Once);
        _auditoriaRepositoryMock.Verify(r => r.AddAsync(It.Is<AuditoriaEntity>(a => a.DocumentoId == 2 && a.Accion == "BlobEliminado" && a.Nivel == "Info")), Times.Once);
    }

    [Fact]
    public async Task Run_DeleteDevuelveFalse_AuditaErrorYNoLimpiaRuta()
    {
        var documento = new DocumentoEntity
        {
            Id = 3,
            RutaBlobStorage = "documents/2026/05/file3.pdf",
            TamanoBytes = 300
        };

        _documentoRepositoryMock
            .Setup(r => r.GetDocumentosConBlobExpiradosAsync(It.IsAny<int>()))
            .ReturnsAsync(new[] { documento });

        _blobStorageServiceMock
            .Setup(s => s.ExistsAsync(documento.RutaBlobStorage!))
            .ReturnsAsync(true);

        _blobStorageServiceMock
            .Setup(s => s.DeleteDocumentAsync(documento.RutaBlobStorage!))
            .ReturnsAsync(false);

        _auditoriaRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        await sut.Run(null);

        _documentoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()), Times.Never);
        _auditoriaRepositoryMock.Verify(r => r.AddAsync(It.Is<AuditoriaEntity>(a => a.DocumentoId == 3 && a.Accion == "BlobEliminadoError" && a.Nivel == "Error")), Times.Once);
        documento.RutaBlobStorage.Should().NotBeNullOrWhiteSpace();
    }

    private BlobCleanupTimerTrigger CreateSut()
    {
        return new BlobCleanupTimerTrigger(
            _loggerMock.Object,
            _documentoRepositoryMock.Object,
            _blobStorageServiceMock.Object,
            _auditoriaRepositoryMock.Object,
            _telemetryClient,
            _configuration);
    }
}
