#nullable enable
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

/// <summary>
/// Seleccion de la historica por clave de intencion (AB#100930). Sqlite en memoria porque
/// ActualizarClaveIntencionAsync usa ExecuteUpdate, que el proveedor InMemory no soporta.
/// </summary>
public class DocumentoEjecucionRepositoryClaveTests : IDisposable
{
    private static readonly DateTime Base = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly SqliteConnection _conexion;
    private readonly DocumentIADbContext _context;

    public DocumentoEjecucionRepositoryClaveTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        var options = new DbContextOptionsBuilder<DocumentIADbContext>().UseSqlite(_conexion).Options;
        _context = new DocumentIADbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    [Fact]
    public void Entidad_TieneLasTresColumnasNuevasConLongitudes()
    {
        var entidad = _context.Model.FindEntityType(typeof(DocumentoEjecucionEntity))!;

        entidad.FindProperty("ClaveIntencion")!.GetMaxLength().Should().Be(32);
        entidad.FindProperty("ClaveIntencionJson").Should().NotBeNull();
        entidad.FindProperty("MotivoReutilizacion")!.GetMaxLength().Should().Be(40);
        entidad.GetIndexes().Should().Contain(i => i.GetDatabaseName() == "IX_DocumentoEjecuciones_Documento_Clave");
    }

    [Fact]
    public async Task GetCandidatasPorClave_DevuelveLaMasRecienteDeEsaClaveSinReutilizacionesNiDuplicadosVacios()
    {
        Sembrar(
            Fila(1, "k1", Base.AddDays(1), estado: "OK", contrato: "{\"a\":1}"),
            Fila(2, "k1", Base.AddDays(2), estado: "OK", contrato: "{\"a\":2}"),
            Fila(3, "k1", Base.AddDays(3), estado: "OK", contrato: null, reutilizada: true, originalId: 2),
            Fila(4, "k1", Base.AddDays(4), estado: "DUPLICADO", contrato: "{}"),
            Fila(5, "k2", Base.AddDays(5), estado: "OK", contrato: "{\"a\":5}"));
        var repo = new DocumentoEjecucionRepository(_context);

        var primera = await repo.GetCandidatasPorClaveAsync(10, "k1", 0, 1);
        var siguientes = await repo.GetCandidatasPorClaveAsync(10, "k1", 1, 4);

        primera.Should().ContainSingle().Which.Id.Should().Be(2);
        siguientes.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetCandidatasPorClave_OtroDocumentoNoCuenta()
    {
        Sembrar(Fila(1, "k1", Base, estado: "OK", contrato: "{}", documentoId: 99));
        var repo = new DocumentoEjecucionRepository(_context);

        (await repo.GetCandidatasPorClaveAsync(10, "k1", 0, 1)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetConContratoSinClave_SoloFilasPropiasConContratoYSinClave()
    {
        Sembrar(
            Fila(1, null, Base.AddDays(1), estado: "OK", contrato: "{\"a\":1}"),
            Fila(2, "k1", Base.AddDays(2), estado: "OK", contrato: "{\"a\":2}"),
            Fila(3, null, Base.AddDays(3), estado: "OK", contrato: null, reutilizada: true, originalId: 1),
            Fila(4, null, Base.AddDays(4), estado: "ERROR", contrato: null));
        var repo = new DocumentoEjecucionRepository(_context);

        var filas = await repo.GetConContratoSinClaveAsync(10, 50);

        filas.Select(f => f.Id).Should().Equal(1);
    }

    [Fact]
    public async Task TieneEjecucionesConContrato_IgnoraReutilizaciones()
    {
        Sembrar(
            Fila(1, null, Base, estado: "ERROR", contrato: null),
            Fila(3, null, Base, estado: "OK", contrato: null, reutilizada: true, originalId: 1));
        var repo = new DocumentoEjecucionRepository(_context);

        (await repo.TieneEjecucionesConContratoAsync(10)).Should().BeFalse();

        Sembrar(Fila(4, null, Base, estado: "OK", contrato: "{}"));
        (await repo.TieneEjecucionesConContratoAsync(10)).Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarClaveIntencion_EscribeClaveYJsonSinTocarElResto()
    {
        Sembrar(Fila(1, null, Base, estado: "OK", contrato: "{\"a\":1}"));
        var repo = new DocumentoEjecucionRepository(_context);

        await repo.ActualizarClaveIntencionAsync(1, "k-nueva", "{\"modo\":\"completo\"}");

        _context.ChangeTracker.Clear();
        var fila = await _context.DocumentoEjecuciones.SingleAsync(e => e.Id == 1);
        fila.ClaveIntencion.Should().Be("k-nueva");
        fila.ClaveIntencionJson.Should().Be("{\"modo\":\"completo\"}");
        fila.ContratoSalidaCompletoJson.Should().Be("{\"a\":1}");
        fila.MotivoReutilizacion.Should().BeNull();
    }

    private void Sembrar(params DocumentoEjecucionEntity[] filas)
    {
        if (!_context.Documentos.Any(d => d.Id == 10))
        {
            _context.Documentos.Add(new DocumentoEntity { Id = 10, SHA256 = "sha-10", NombreArchivo = "doc.pdf" });
            _context.Documentos.Add(new DocumentoEntity { Id = 99, SHA256 = "sha-99", NombreArchivo = "otro.pdf" });
        }

        _context.DocumentoEjecuciones.AddRange(filas);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static DocumentoEjecucionEntity Fila(
        int id, string? clave, DateTime fecha, string estado, string? contrato,
        bool reutilizada = false, int? originalId = null, int documentoId = 10) => new()
        {
            Id = id,
            DocumentoId = documentoId,
            EjecucionGuid = $"guid-{id}",
            FechaEjecucion = fecha,
            EstadoFinal = estado,
            ClaveIntencion = clave,
            ContratoSalidaCompletoJson = contrato,
            ReutilizadaPorDuplicado = reutilizada,
            EjecucionOriginalId = originalId
        };
}
