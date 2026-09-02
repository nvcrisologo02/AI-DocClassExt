#nullable enable
using System.Text.Json;
using DocumentIA.Core.Models;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Activities;
using DocumentIA.Functions.Services.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class PersistirActivityTests : IDisposable
{
    private readonly Mock<IDocumentoRepository> _documentoRepoMock;
    private readonly Mock<IDocumentoEjecucionRepository> _ejecucionRepoMock;
    private readonly Mock<IAuditoriaRepository> _auditoriaRepoMock;
    private readonly Mock<ITelemetryService> _telemetryServiceMock;
    private readonly DocumentIADbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PersistirActivity _sut;

    public PersistirActivityTests()
    {
        _documentoRepoMock = new Mock<IDocumentoRepository>();
        _ejecucionRepoMock = new Mock<IDocumentoEjecucionRepository>();
        _auditoriaRepoMock = new Mock<IAuditoriaRepository>();
        _telemetryServiceMock = new Mock<ITelemetryService>();

        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DocumentIADbContext(options);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobRetention:DefaultDays"] = "90"
            })
            .Build();

        _sut = new PersistirActivity(
            new Mock<ILogger<PersistirActivity>>().Object,
            _documentoRepoMock.Object,
            _ejecucionRepoMock.Object,
            _auditoriaRepoMock.Object,
            _context,
            _telemetryServiceMock.Object,
            _configuration);
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

        await _sut.Run(new PersistirInput { Salida = salida });

        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEntity>()), Times.Once);
        _documentoRepoMock.Verify(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()), Times.Never);
    }

    [Fact]
    public async Task Run_IdentificacionDocumentoVacio_UsaFallbackEnNombreArchivo()
    {
        const string sha256 = "sha256_nombre_fallback";
        var salida = BuildSalidaMinima(sha256);
        salida.Identificacion.Documento = string.Empty;

        DocumentoEntity? documentoCapturado = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) =>
            {
                documentoCapturado = d;
                d.Id = 42;
                return d;
            });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida });

        documentoCapturado.Should().NotBeNull();
        documentoCapturado!.NombreArchivo.Should().Be($"documento-{salida.Identificacion.Guid}.pdf");
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

        await _sut.Run(new PersistirInput { Salida = salida });

        _documentoRepoMock.Verify(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()), Times.Once);
        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEntity>()), Times.Never);
    }

    [Fact]
    public async Task Run_DocumentoNuevo_PersisteSubmittedByEnElDocumento()
    {
        const string sha256 = "sha256_alta_con_solicitante";
        var salida = BuildSalidaMinima(sha256);

        DocumentoEntity? documentoCapturado = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) =>
            {
                documentoCapturado = d;
                d.Id = 7;
                return d;
            });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput
        {
            Salida = salida,
            SubmittedBy = "DocumentIA.Batch/nombre.apellido@sareb.es"
        });

        documentoCapturado.Should().NotBeNull();
        documentoCapturado!.SubmittedBy.Should().Be("DocumentIA.Batch/nombre.apellido@sareb.es");
    }

    [Fact]
    public async Task Run_DocumentoExistente_NoPisaElSubmittedByOriginal()
    {
        const string sha256 = "sha256_reproceso_conserva_solicitante";
        var salida = BuildSalidaMinima(sha256);
        var documentoExistente = new DocumentoEntity
        {
            Id = 9,
            SHA256 = sha256,
            SubmittedBy = "DocumentIA.Batch/primer.solicitante@sareb.es"
        };

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

        await _sut.Run(new PersistirInput
        {
            Salida = salida,
            SubmittedBy = "DocumentIA.Batch.ClassificationLite/otro.usuario@sareb.es"
        });

        // El documento conserva quien lo trajo; el reenvio queda recogido en la ejecucion.
        documentoExistente.SubmittedBy.Should().Be("DocumentIA.Batch/primer.solicitante@sareb.es");
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

        await _sut.Run(new PersistirInput { Salida = salida });

        _ejecucionRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()), Times.Once);
    }

    [Fact]
    public async Task Run_ConSubmittedByInformado_PersisteSubmittedByEnEjecucion()
    {
        const string sha256 = "sha256_submittedby";
        var salida = BuildSalidaMinima(sha256);
        var documentoCreado = new DocumentoEntity { Id = 4, SHA256 = sha256 };

        DocumentoEjecucionEntity? ejecucionCapturada = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync(documentoCreado);
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) =>
            {
                ejecucionCapturada = e;
                return e;
            });
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida, SubmittedBy = "usuario-prueba" });

        ejecucionCapturada.Should().NotBeNull();
        ejecucionCapturada!.SubmittedBy.Should().Be("usuario-prueba");
    }

    [Fact]
    public async Task Run_SinSubmittedBy_DejaSubmittedByNuloEnEjecucion()
    {
        const string sha256 = "sha256_sin_submittedby";
        var salida = BuildSalidaMinima(sha256);
        var documentoCreado = new DocumentoEntity { Id = 6, SHA256 = sha256 };

        DocumentoEjecucionEntity? ejecucionCapturada = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync(documentoCreado);
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) =>
            {
                ejecucionCapturada = e;
                return e;
            });
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida, SubmittedBy = null });

        ejecucionCapturada.Should().NotBeNull();
        ejecucionCapturada!.SubmittedBy.Should().BeNull();
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

        await _sut.Run(new PersistirInput { Salida = salida });

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

        await _sut.Run(new PersistirInput { Salida = salida });

        _context.ResultadosProcesamiento.Should().HaveCount(1);
        _context.ResultadosProcesamiento.First().DocumentoId.Should().Be(10);
    }

    [Fact]
    public async Task Run_DocumentoNuevo_AplicaDefaultDaysEnFechaExpiracionBlob()
    {
        const string sha256 = "sha256_expiracion_default";
        var salida = BuildSalidaMinima(sha256);
        salida.Identificacion.FechaProceso = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        DocumentoEntity? documentoCapturado = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) =>
            {
                documentoCapturado = d;
                d.Id = 100;
                return d;
            });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida });

        documentoCapturado.Should().NotBeNull();
        documentoCapturado!.FechaExpiracionBlob.Should().Be(new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Run_TipologiaConRetentionMenosUno_DejaFechaExpiracionBlobNull()
    {
        const string sha256 = "sha256_expiracion_indefinida";
        var salida = BuildSalidaMinima(sha256);
        salida.Identificacion.Tipologia = "NDS";

        _context.Tipologias.Add(new TipologiaEntity
        {
            Codigo = "NDS",
            Nombre = "Nota Simple",
            ConfiguracionJson = "{\"retentionPolicy\":{\"blobRetentionDays\":-1}}"
        });
        await _context.SaveChangesAsync();

        DocumentoEntity? documentoCapturado = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) =>
            {
                documentoCapturado = d;
                d.Id = 101;
                return d;
            });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida });

        documentoCapturado.Should().NotBeNull();
        documentoCapturado!.FechaExpiracionBlob.Should().BeNull();
    }

    [Fact]
    public async Task Run_EmiteTelemetria_TrackEventDocumentProcessed()
    {
        const string sha256 = "sha256_telemetry_event";
        var salida = BuildSalidaMinima(sha256);

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) =>
            {
                d.Id = 500;
                return d;
            });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida });

        _telemetryServiceMock.Verify(t => t.TrackEvent(
            "DocumentProcessed",
            It.Is<IDictionary<string, string>>(d =>
                d.ContainsKey("Tipologia") &&
                d.ContainsKey("EstadoFinal") &&
                d.ContainsKey("UseFallbackLLM") &&
                d.ContainsKey("NombreDocumento") &&
                d.ContainsKey("EjecucionGuid"))), Times.Once);
    }

    [Fact]
    public async Task Run_EmiteTelemetria_TrackMetricPorActividad()
    {
        const string sha256 = "sha256_telemetry_metric";
        var salida = BuildSalidaMinima(sha256);
        salida.DetalleEjecucion.Seguimiento = new SeguimientoOrquestacion
        {
            DuracionTotalMs = 1200
        };

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) =>
            {
                d.Id = 501;
                return d;
            });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida });

        _telemetryServiceMock.Verify(t => t.TrackMetric(
            "DocumentIA.Duracion.Total",
            It.IsAny<double>(),
            It.Is<IDictionary<string, string>>(d => d.ContainsKey("Tipologia"))), Times.Once);
    }

    [Fact]
    public async Task Run_TelemetriaFalla_NoBloqueaFlujo()
    {
        const string sha256 = "sha256_telemetry_fails";
        var salida = BuildSalidaMinima(sha256);

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) =>
            {
                d.Id = 502;
                return d;
            });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        _telemetryServiceMock
            .Setup(t => t.TrackEvent(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .Throws(new InvalidOperationException("telemetry down"));

        var act = async () => await _sut.Run(new PersistirInput { Salida = salida });

        await act.Should().NotThrowAsync();
        _ejecucionRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()), Times.Once);
        _auditoriaRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditoriaEntity>()), Times.Once);
    }

    [Fact]
    public async Task Run_NoGrabaDatosDuplicadosYPodaElTimelineDelContrato()
    {
        // AB#100166: DatosFinales/DatosOriginales ya viajan dentro del contrato y el timeline
        // vive en su propia columna: no se graban dos veces.
        const string sha256 = "sha256_dedup_columnas";
        var salida = BuildSalidaMinima(sha256);
        salida.DatosExtraidos = new Dictionary<string, object> { ["Titular"] = "Prueba" };
        salida.DetalleEjecucion.Integracion.DatosOriginales = new Dictionary<string, object> { ["Titular"] = "Original" };
        salida.DetalleEjecucion.Seguimiento.Actividades = new List<TrazaActividad>
        {
            new() { Nombre = "Clasificar", Estado = "Completed", DuracionMs = 1200 },
            new() { Nombre = "Persistir", Estado = "Running" }
        };

        DocumentoEjecucionEntity? ejecucionCapturada = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) => { d.Id = 7; return d; });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => { ejecucionCapturada = e; return e; });
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida });

        ejecucionCapturada.Should().NotBeNull();
        ejecucionCapturada!.DatosFinalesJson.Should().BeNull("ya viaja en $.DatosExtraidos del contrato");
        ejecucionCapturada.DatosOriginalesJson.Should().BeNull("ya viaja en $.DetalleEjecucion.Integracion.DatosOriginales");

        // El timeline SI se graba en su columna (lo proyecta el listado del Monitor, AB#100183).
        ejecucionCapturada.ActivityTimelineJson.Should().NotBeNullOrWhiteSpace();
        ejecucionCapturada.ActivityTimelineJson.Should().Contain("Clasificar");

        // ...y NO se duplica dentro del contrato persistido.
        var contratoPersistido = JsonSerializer.Deserialize<ContratoSalida>(
            ejecucionCapturada.ContratoSalidaCompletoJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        contratoPersistido!.DetalleEjecucion.Seguimiento.Actividades.Should().BeEmpty();

        // La respuesta al llamador no cambia: el objeto en memoria conserva el timeline.
        salida.DetalleEjecucion.Seguimiento.Actividades.Should().HaveCount(2);
    }

    [Fact]
    public async Task Run_PersisteIdActivoEscalarEnLaEjecucion()
    {
        // AB#100168: el filtro por IdActivo deja de depender de una columna calculada sobre
        // DatosFinalesJson (que ya no se graba) y pasa a una columna escalar real.
        const string sha256 = "sha256_idactivo";
        var salida = BuildSalidaMinima(sha256);
        salida.Integridad.IdActivo = " act-12345 ";

        DocumentoEjecucionEntity? ejecucionCapturada = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) => { d.Id = 11; return d; });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => { ejecucionCapturada = e; return e; });
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida });

        ejecucionCapturada.Should().NotBeNull();
        ejecucionCapturada!.IdActivo.Should().Be("ACT-12345", "se normaliza en escritura igual que hacia la columna calculada");
    }

    [Fact]
    public async Task Run_SinIdActivo_DejaLaColumnaNula()
    {
        const string sha256 = "sha256_sin_idactivo";
        var salida = BuildSalidaMinima(sha256);
        salida.Integridad.IdActivo = null;

        DocumentoEjecucionEntity? ejecucionCapturada = null;

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(sha256))
            .ReturnsAsync((DocumentoEntity?)null);
        _documentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEntity>()))
            .ReturnsAsync((DocumentoEntity d) => { d.Id = 12; return d; });
        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .ReturnsAsync((DocumentoEjecucionEntity e) => { ejecucionCapturada = e; return e; });
        _auditoriaRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AuditoriaEntity>()))
            .Returns(Task.CompletedTask);

        await _sut.Run(new PersistirInput { Salida = salida });

        ejecucionCapturada!.IdActivo.Should().BeNull("sin activo resuelto no debe guardarse cadena vacia");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
