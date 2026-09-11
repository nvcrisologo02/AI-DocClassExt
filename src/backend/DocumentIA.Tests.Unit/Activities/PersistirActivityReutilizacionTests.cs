#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
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

/// <summary>
/// Registro de una peticion servida con el contrato de otra ejecucion: deja traza sin
/// tocar el documento, sin reserializar el contrato y sin sumar coste (AB#100258).
/// </summary>
public class PersistirActivityReutilizacionTests : IDisposable
{
    private const string Sha = "sha-reutilizacion";
    private const string GuidOriginal = "11111111-1111-1111-1111-111111111111";

    private readonly Mock<IDocumentoRepository> _documentoRepoMock = new();
    private readonly Mock<IDocumentoEjecucionRepository> _ejecucionRepoMock = new();
    private readonly Mock<IAuditoriaRepository> _auditoriaRepoMock = new();
    private readonly DocumentIADbContext _context;
    private readonly PersistirActivity _sut;
    private DocumentoEjecucionEntity? _insertada;

    public PersistirActivityReutilizacionTests()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"persistir-reutilizacion-{Guid.NewGuid()}")
            .Options;
        _context = new DocumentIADbContext(options);

        _documentoRepoMock
            .Setup(r => r.GetBySHA256Async(Sha))
            .ReturnsAsync(new DocumentoEntity { Id = 7, SHA256 = Sha, NombreArchivo = "doc.pdf" });

        _ejecucionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .Callback<DocumentoEjecucionEntity>(e => _insertada = e)
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);

        _ejecucionRepoMock
            .Setup(r => r.GetByGuidAsync(GuidOriginal))
            .ReturnsAsync(new DocumentoEjecucionEntity { Id = 42, EjecucionGuid = GuidOriginal });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BlobRetention:DefaultDays"] = "90" })
            .Build();

        _sut = new PersistirActivity(
            new Mock<ILogger<PersistirActivity>>().Object,
            _documentoRepoMock.Object,
            _ejecucionRepoMock.Object,
            _auditoriaRepoMock.Object,
            _context,
            new Mock<ITelemetryService>().Object,
            configuration);
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_InsertarLaFilaMarcadaYVinculada()
    {
        await _sut.Run(BuildInput());

        _insertada.Should().NotBeNull();
        _insertada!.ReutilizadaPorDuplicado.Should().BeTrue();
        _insertada.EjecucionOriginalId.Should().Be(42);
        _insertada.DocumentoId.Should().Be(7);
        _insertada.EstadoFinal.Should().Be("OK");
        _insertada.InstanceId.Should().Be("instancia-de-ahora");
        _insertada.SubmittedBy.Should().Be("portal-colabora");
        _insertada.DuracionTotalMs.Should().Be(45);
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_DejarContratoYCostesVacios()
    {
        await _sut.Run(BuildInput());

        _insertada!.ContratoSalidaCompletoJson.Should().BeNull();
        _insertada.CosteIAEur.Should().BeNull();
        _insertada.CosteClasificacionEur.Should().BeNull();
        _insertada.CosteEstimado.Should().BeFalse();
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_NoTocarElDocumento()
    {
        await _sut.Run(BuildInput());

        _documentoRepoMock.Verify(r => r.UpdateAsync(It.IsAny<DocumentoEntity>()), Times.Never);
        _documentoRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEntity>()), Times.Never);
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_NoEscribirResultadosProcesamiento()
    {
        await _sut.Run(BuildInput());

        _context.ResultadosProcesamiento.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_RegistrarAuditoria()
    {
        await _sut.Run(BuildInput());

        _auditoriaRepoMock.Verify(
            r => r.AddAsync(It.Is<AuditoriaEntity>(a =>
                a.DocumentoId == 7 && a.Accion == "REUTILIZACION_DUPLICADO")),
            Times.Once);
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_NoInsertarDosVecesElMismoInstanceId()
    {
        _context.DocumentoEjecuciones.Add(new DocumentoEjecucionEntity
        {
            Id = 99,
            DocumentoId = 7,
            EjecucionGuid = "99999999-9999-9999-9999-999999999999",
            EstadoFinal = "OK",
            InstanceId = "instancia-de-ahora",
            ReutilizadaPorDuplicado = true
        });
        await _context.SaveChangesAsync();

        await _sut.Run(BuildInput());

        _ejecucionRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()), Times.Never);
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_NoInsertarCuandoElDocumentoNoExiste()
    {
        _documentoRepoMock.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync((DocumentoEntity?)null);

        await _sut.Run(BuildInput());

        _ejecucionRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()), Times.Never);
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_RegistrarSinVinculoSiLaOriginalYaNoExiste()
    {
        // La original puede haberse purgado. Perder el vinculo es aceptable; perder la
        // traza de que la peticion existio, no.
        _ejecucionRepoMock
            .Setup(r => r.GetByGuidAsync(GuidOriginal))
            .ReturnsAsync((DocumentoEjecucionEntity?)null);

        await _sut.Run(BuildInput());

        _insertada.Should().NotBeNull();
        _insertada!.ReutilizadaPorDuplicado.Should().BeTrue();
        _insertada.EjecucionOriginalId.Should().BeNull();
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_InsertarSinGuardaCuandoNoHayInstanceId()
    {
        // Sin InstanceId no hay clave natural para deduplicar el registro, pero la
        // alternativa seria no registrar nada.
        var input = BuildInput();
        input.Salida.DetalleEjecucion.InstanceId = null;

        await _sut.Run(input);

        _insertada.Should().NotBeNull();
        _insertada!.InstanceId.Should().BeNull();
    }

    [Fact]
    public async Task Run_Reutilizacion_Should_ConservarElEstadoDeErrorDelOriginal()
    {
        // Reutilizar una ejecucion que fallo no convierte la peticion en correcta: el
        // Monitor tiene que seguir contandola como error.
        var input = BuildInput();
        input.Salida.Resultado.Estado = "ERROR";

        await _sut.Run(input);

        _insertada!.EstadoFinal.Should().Be("ERROR");
    }

    private static PersistirInput BuildInput() => new()
    {
        SubmittedBy = "portal-colabora",
        Reutilizacion = new ReutilizacionInput
        {
            EjecucionOriginalGuid = GuidOriginal,
            Sha256 = Sha
        },
        Salida = new ContratoSalida
        {
            Identificacion = new Identificacion { Documento = "doc.pdf", Tipologia = "inli.13" },
            Integridad = new Integridad { SHA256 = Sha },
            Resultado = new ResultadoFinal { Estado = "OK", ConfianzaGlobal = 0.91 },
            DetalleEjecucion = new DetalleEjecucion
            {
                InstanceId = "instancia-de-ahora",
                OperationId = "operacion-de-ahora",
                ClassificationOnly = true,
                NivelClasificacion = "TDN1_TDN2",
                Clasificacion = new ResultadoClasificacion { Modelo = "gpt5-mini", Confianza = 0.91 },
                Seguimiento = new SeguimientoOrquestacion { DuracionTotalMs = 45 },
                Costes = new CostesIA { ReutilizadaPorDuplicado = true }
            }
        }
    };

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
