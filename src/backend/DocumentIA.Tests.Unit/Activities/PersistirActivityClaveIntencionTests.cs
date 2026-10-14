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
/// La clave de intencion y el motivo viajan en DetalleEjecucion y PersistirActivity los graba
/// en las dos ramas; una reutilizacion completada con prompt persiste contrato y coste (AB#100930).
/// </summary>
public class PersistirActivityClaveIntencionTests : IDisposable
{
    private const string Sha = "sha-clave";
    private const string GuidOriginal = "22222222-2222-2222-2222-222222222222";
    private static readonly ClaveIntencionComponentes Componentes =
        ClaveIntencionBuilder.Normalizar(true, "TDN1_TDN2", "TDN1_TDN2", null, null, "gpt-4o-mini");
    private static readonly string Clave = ClaveIntencionBuilder.Calcular(Componentes);

    private readonly Mock<IDocumentoRepository> _documentoRepoMock = new();
    private readonly Mock<IDocumentoEjecucionRepository> _ejecucionRepoMock = new();
    private readonly DocumentIADbContext _context;
    private readonly PersistirActivity _sut;
    private DocumentoEjecucionEntity? _insertada;

    public PersistirActivityClaveIntencionTests()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"persistir-clave-{Guid.NewGuid()}")
            .Options;
        _context = new DocumentIADbContext(options);

        _documentoRepoMock.Setup(r => r.GetBySHA256Async(Sha))
            .ReturnsAsync(new DocumentoEntity { Id = 7, SHA256 = Sha, NombreArchivo = "doc.pdf" });
        _ejecucionRepoMock.Setup(r => r.AddAsync(It.IsAny<DocumentoEjecucionEntity>()))
            .Callback<DocumentoEjecucionEntity>(e => _insertada = e)
            .ReturnsAsync((DocumentoEjecucionEntity e) => e);
        _ejecucionRepoMock.Setup(r => r.GetByGuidAsync(GuidOriginal))
            .ReturnsAsync(new DocumentoEjecucionEntity { Id = 42, EjecucionGuid = GuidOriginal });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BlobRetention:DefaultDays"] = "90" })
            .Build();

        _sut = new PersistirActivity(
            new Mock<ILogger<PersistirActivity>>().Object,
            _documentoRepoMock.Object,
            _ejecucionRepoMock.Object,
            new Mock<IAuditoriaRepository>().Object,
            _context,
            new Mock<ITelemetryService>().Object,
            configuration);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Reutilizacion_GrabaClaveJsonYMotivoClaveIgual()
    {
        var salida = SalidaBase();
        salida.DetalleEjecucion.MotivoReutilizacion = MotivosReutilizacion.ClaveIgual;

        await _sut.Run(new PersistirInput
        {
            Salida = salida,
            Reutilizacion = new ReutilizacionInput { EjecucionOriginalGuid = GuidOriginal, Sha256 = Sha }
        });

        _insertada!.ClaveIntencion.Should().Be(Clave);
        _insertada.ClaveIntencionJson.Should().Be(ClaveIntencionBuilder.Serializar(Componentes));
        _insertada.MotivoReutilizacion.Should().Be("clave-igual");
        _insertada.ContratoSalidaCompletoJson.Should().BeNull();
        _insertada.CosteIAEur.Should().BeNull();
    }

    [Fact]
    public async Task ReutilizacionCompletada_PersisteContratoYCosteDelPrompt()
    {
        var salida = SalidaBase();
        salida.DetalleEjecucion.MotivoReutilizacion = MotivosReutilizacion.ClaveIgual;
        salida.DatosExtraidos["Resumen"] = "resumen generado";
        salida.DetalleEjecucion.Prompt = new ResultadoPromptEjecucion { Modelo = "gpt-4o-mini", TiempoMs = 800 };
        salida.DetalleEjecucion.Costes = CalculadoraCosteIA.Agregar(new[]
        {
            new ConsumoIA { Actividad = ActividadesIA.Prompt, Proveedor = "azure-openai", Modelo = "gpt-4o-mini", TokensEntrada = 1000, TokensSalida = 200, CosteEur = 0.0012m }
        });
        salida.DetalleEjecucion.Costes.ReutilizadaPorDuplicado = true;

        await _sut.Run(new PersistirInput
        {
            Salida = salida,
            Reutilizacion = new ReutilizacionInput { EjecucionOriginalGuid = GuidOriginal, Sha256 = Sha, Completada = true }
        });

        _insertada!.ReutilizadaPorDuplicado.Should().BeTrue();
        _insertada.ContratoSalidaCompletoJson.Should().Contain("resumen generado");
        _insertada.CosteIAEur.Should().Be(0.0012m);
        _insertada.CostePromptEur.Should().Be(0.0012m);
        _insertada.TokensIA.Should().Be(1200);
    }

    [Fact]
    public async Task AltaNormal_GrabaClaveYMotivoNoReutilizacion()
    {
        var salida = SalidaBase();
        salida.Integridad.SHA256 = Sha;
        salida.DetalleEjecucion.MotivoNoReutilizacion = MotivosReutilizacion.ClaveDistinta;

        await _sut.Run(new PersistirInput { Salida = salida });

        _insertada!.ReutilizadaPorDuplicado.Should().BeFalse();
        _insertada.ClaveIntencion.Should().Be(Clave);
        _insertada.ClaveIntencionJson.Should().Be(ClaveIntencionBuilder.Serializar(Componentes));
        _insertada.MotivoReutilizacion.Should().Be("clave-distinta");
    }

    [Fact]
    public async Task AltaNormal_SinClaveNiDuplicado_DejaLasColumnasANull()
    {
        var salida = SalidaBase();
        salida.Integridad.SHA256 = Sha;
        salida.DetalleEjecucion.ClaveIntencion = null;
        salida.DetalleEjecucion.ClaveIntencionComponentes = null;

        await _sut.Run(new PersistirInput { Salida = salida });

        _insertada!.ClaveIntencion.Should().BeNull();
        _insertada.ClaveIntencionJson.Should().BeNull();
        _insertada.MotivoReutilizacion.Should().BeNull();
    }

    private static ContratoSalida SalidaBase() => new()
    {
        Identificacion = new Identificacion { Documento = "doc.pdf", Tipologia = "tasacion" },
        Integridad = new Integridad { SHA256 = Sha, MD5 = "md5", TamanoBytes = 10 },
        Resultado = new ResultadoFinal { Estado = "OK", ConfianzaGlobal = 0.9 },
        DetalleEjecucion = new DetalleEjecucion
        {
            InstanceId = "inst-clave",
            ClassificationOnly = true,
            NivelClasificacion = "TDN1_TDN2",
            ClaveIntencion = Clave,
            ClaveIntencionComponentes = Componentes,
            Seguimiento = new SeguimientoOrquestacion { DuracionTotalMs = 120 }
        }
    };
}
