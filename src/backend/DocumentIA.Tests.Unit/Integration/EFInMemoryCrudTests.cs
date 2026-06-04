#nullable enable
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Integration;

/// <summary>
/// Tests T-3: Pruebas de CRUD básico sobre DocumentIADbContext usando EF Core InMemory.
/// Validan que la configuración del modelo y las operaciones Add/Query/Update funcionan
/// correctamente antes de conectar a SQL Server real.
/// </summary>
public class EFInMemoryCrudTests : IDisposable
{
    private readonly DocumentIADbContext _context;

    public EFInMemoryCrudTests()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DocumentIADbContext(options);
    }

    public void Dispose() => _context.Dispose();

    // ── DocumentoEntity ──────────────────────────────────────────────────────

    [Fact]
    public async Task DocumentoEntity_AgregarYConsultarPorSHA256_DevuelveEntidadCorrecta()
    {
        var doc = new DocumentoEntity
        {
            Guid = Guid.NewGuid().ToString(),
            NombreArchivo = "test.pdf",
            SHA256 = "sha256-unico-abc",
            MD5 = "md5abc",
            CRC32 = "crc32abc",
            TamanoBytes = 1024,
            Tipologia = "nota.simple.1_0",
            Estado = "OK",
            Paginas = 3
        };

        _context.Documentos.Add(doc);
        await _context.SaveChangesAsync();

        var found = await _context.Documentos.FirstOrDefaultAsync(d => d.SHA256 == "sha256-unico-abc");

        found.Should().NotBeNull();
        found!.NombreArchivo.Should().Be("test.pdf");
        found.Tipologia.Should().Be("nota.simple.1_0");
        found.Paginas.Should().Be(3);
    }

    [Fact]
    public async Task DocumentoEntity_ActualizarEstado_PersistiCambio()
    {
        var doc = new DocumentoEntity
        {
            Guid = Guid.NewGuid().ToString(),
            NombreArchivo = "update.pdf",
            SHA256 = "sha256-update",
            MD5 = "md5upd",
            CRC32 = "crc32upd",
            Estado = "Pendiente"
        };

        _context.Documentos.Add(doc);
        await _context.SaveChangesAsync();

        doc.Estado = "OK";
        doc.ConfianzaGlobal = 0.95;
        await _context.SaveChangesAsync();

        var updated = await _context.Documentos.FindAsync(doc.Id);
        updated!.Estado.Should().Be("OK");
        updated.ConfianzaGlobal.Should().Be(0.95);
    }

    [Fact]
    public async Task DocumentoEntity_EliminarEntidad_YaNoApareceEnConsulta()
    {
        var doc = new DocumentoEntity
        {
            Guid = Guid.NewGuid().ToString(),
            NombreArchivo = "delete.pdf",
            SHA256 = "sha256-delete",
            MD5 = "md5del",
            CRC32 = "crc32del"
        };

        _context.Documentos.Add(doc);
        await _context.SaveChangesAsync();

        _context.Documentos.Remove(doc);
        await _context.SaveChangesAsync();

        var found = await _context.Documentos.FirstOrDefaultAsync(d => d.SHA256 == "sha256-delete");
        found.Should().BeNull();
    }

    // ── TipologiaEntity ──────────────────────────────────────────────────────

    [Fact]
    public async Task TipologiaEntity_AgregarYConsultarPorCodigo_DevuelveEntidadCorrecta()
    {
        var tipologia = new TipologiaEntity
        {
            Codigo = "nota.simple",
            Nombre = "Nota Simple",
            Version = "1.0",
            Activa = true,
            Estado = EstadoTipologia.Draft,
            CreadoPor = "test-user",
            FechaCreacion = DateTime.UtcNow
        };

        _context.Tipologias.Add(tipologia);
        await _context.SaveChangesAsync();

        var found = await _context.Tipologias.FirstOrDefaultAsync(t => t.Codigo == "nota.simple");

        found.Should().NotBeNull();
        found!.Nombre.Should().Be("Nota Simple");
        found.Estado.Should().Be(EstadoTipologia.Draft);
        found.Activa.Should().BeTrue();
    }

    [Fact]
    public async Task TipologiaEntity_CambioEstado_PersistiPublicada()
    {
        var tipologia = new TipologiaEntity
        {
            Codigo = "tasacion",
            Nombre = "Tasación",
            Version = "1.0",
            Estado = EstadoTipologia.Draft,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Tipologias.Add(tipologia);
        await _context.SaveChangesAsync();

        tipologia.Estado = EstadoTipologia.Published;
        tipologia.PublicadaEn = DateTime.UtcNow;
        tipologia.PublicadaPor = "SYSTEM";
        await _context.SaveChangesAsync();

        var updated = await _context.Tipologias.FindAsync(tipologia.Id);
        updated!.Estado.Should().Be(EstadoTipologia.Published);
        updated.PublicadaPor.Should().Be("SYSTEM");
    }

    // ── Aislamiento de base de datos ─────────────────────────────────────────

    [Fact]
    public async Task DocumentIADbContext_BaseDatosInMemory_AisladaPorInstancia()
    {
        // Esta instancia tiene una DB distinta (nombre Guid único en constructor)
        // Confirmar que no hay datos de otros tests
        var count = await _context.Documentos.CountAsync();
        count.Should().Be(0);

        var tipologiaCount = await _context.Tipologias.CountAsync();
        tipologiaCount.Should().Be(0);
    }

    [Fact]
    public async Task DocumentIADbContext_VariasEntidades_SeConsultanIndependientemente()
    {
        _context.Documentos.Add(new DocumentoEntity
        {
            Guid = Guid.NewGuid().ToString(), NombreArchivo = "a.pdf",
            SHA256 = "sha1", MD5 = "md1", CRC32 = "cr1"
        });
        _context.Documentos.Add(new DocumentoEntity
        {
            Guid = Guid.NewGuid().ToString(), NombreArchivo = "b.pdf",
            SHA256 = "sha2", MD5 = "md2", CRC32 = "cr2"
        });
        _context.Tipologias.Add(new TipologiaEntity
        {
            Codigo = "tipo1", Nombre = "Tipo 1", FechaCreacion = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var docCount = await _context.Documentos.CountAsync();
        var tipCount = await _context.Tipologias.CountAsync();

        docCount.Should().Be(2);
        tipCount.Should().Be(1);
    }
}
