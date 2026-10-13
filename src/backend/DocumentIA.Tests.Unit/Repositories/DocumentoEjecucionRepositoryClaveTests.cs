#nullable enable
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
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

    // Los tests de GetCandidatasPorClaveAsync, GetConContratoSinClaveAsync,
    // TieneEjecucionesConContratoAsync y ActualizarClaveIntencionAsync se anaden en la Task 4.
}
