using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

/// <summary>
/// Regla de escritura del markdown (AB#100250). Va sobre SQLite en memoria porque
/// ExecuteUpdateAsync lanza con el proveedor InMemory. La conexion se mantiene abierta
/// durante el test: SQLite en memoria muere al cerrarla.
/// </summary>
public sealed class DocumentoRepositoryMarkdownTests : IDisposable
{
    private static readonly byte[] GzipViejo = { 1, 2, 3 };
    private static readonly byte[] GzipNuevo = { 9, 9, 9 };

    private readonly SqliteConnection _conn;
    private readonly DocumentIADbContext _ctx;
    private readonly DocumentoRepository _repo;

    public DocumentoRepositoryMarkdownTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var opciones = new DbContextOptionsBuilder<DocumentIADbContext>().UseSqlite(_conn).Options;
        _ctx = new DocumentIADbContext(opciones);
        _ctx.Database.EnsureCreated();
        _repo = new DocumentoRepository(_ctx);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _conn.Dispose();
    }

    private async Task<DocumentoEntity> SembrarAsync(
        string sha, byte[]? gzip, string? base64, int? paginas, bool completo)
    {
        var doc = new DocumentoEntity
        {
            Guid = Guid.NewGuid().ToString(),
            NombreArchivo = "doc.pdf",
            SHA256 = sha,
            MD5 = "md5-" + sha,
            CRC32 = "AABBCCDD",
            NormalizacionMarkdownGzip = gzip,
            NormalizacionMarkdownCompressed = base64,
            MarkdownPaginas = paginas,
            MarkdownCompleto = completo
        };
        _ctx.Documentos.Add(doc);
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear(); // el UPDATE va por SQL, no por el tracker
        return doc;
    }

    private async Task<DocumentoEntity> LeerAsync(string sha)
        => await _ctx.Documentos.AsNoTracking().SingleAsync(d => d.SHA256 == sha);

    [Fact]
    public async Task SinFila_DevuelveCero()
    {
        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("no-existe", GzipNuevo, "b64", 3, false, false);
        filas.Should().Be(0);
    }

    [Fact]
    public async Task SinMarkdownPrevio_Escribe()
    {
        await SembrarAsync("s1", null, null, null, false);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s1", GzipNuevo, "b64", 3, false, false);

        filas.Should().Be(1);
        var d = await LeerAsync("s1");
        d.NormalizacionMarkdownGzip.Should().Equal(GzipNuevo);
        d.NormalizacionMarkdownCompressed.Should().Be("b64");
        d.MarkdownPaginas.Should().Be(3);
        d.MarkdownCompleto.Should().BeFalse();
        d.FechaActualizacion.Should().NotBeNull();
    }

    [Fact]
    public async Task ParcialConMasPaginas_Escribe()
    {
        await SembrarAsync("s2", GzipViejo, "viejo", 3, false);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s2", GzipNuevo, "nuevo", 5, false, false);

        filas.Should().Be(1);
        (await LeerAsync("s2")).MarkdownPaginas.Should().Be(5);
    }

    [Fact]
    public async Task ParcialConMenosPaginas_NoEscribe()
    {
        await SembrarAsync("s3", GzipViejo, "viejo", 5, false);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s3", GzipNuevo, "nuevo", 3, false, false);

        filas.Should().Be(0);
        var d = await LeerAsync("s3");
        d.NormalizacionMarkdownGzip.Should().Equal(GzipViejo);
        d.MarkdownPaginas.Should().Be(5);
    }

    [Fact]
    public async Task CompletoExistente_ParcialNoLoPisa()
    {
        await SembrarAsync("s4", GzipViejo, "viejo", 14, true);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s4", GzipNuevo, "nuevo", 3, false, false);

        filas.Should().Be(0);
        (await LeerAsync("s4")).MarkdownCompleto.Should().BeTrue();
    }

    [Fact]
    public async Task ParcialExistente_CompletoLoSustituye()
    {
        await SembrarAsync("s5", GzipViejo, "viejo", 3, false);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s5", GzipNuevo, "nuevo", 14, true, false);

        filas.Should().Be(1);
        var d = await LeerAsync("s5");
        d.MarkdownCompleto.Should().BeTrue();
        d.MarkdownPaginas.Should().Be(14);
    }

    [Fact]
    public async Task CoberturaDesconocida_UnParcialNoLaPisa()
    {
        // Historico: hay markdown pero no se sabe cuanto cubre. Podria ser el documento entero.
        await SembrarAsync("s6", GzipViejo, "viejo", null, false);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s6", GzipNuevo, "nuevo", 3, false, false);

        filas.Should().Be(0);
        (await LeerAsync("s6")).NormalizacionMarkdownGzip.Should().Equal(GzipViejo);
    }

    [Fact]
    public async Task CoberturaDesconocida_UnCompletoSiLaSustituye()
    {
        await SembrarAsync("s7", GzipViejo, "viejo", null, false);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s7", GzipNuevo, "nuevo", 14, true, false);

        filas.Should().Be(1);
        (await LeerAsync("s7")).MarkdownCompleto.Should().BeTrue();
    }

    [Fact]
    public async Task SoloBase64Historico_SeTrataComoQueHayMarkdown()
    {
        // Filas anteriores a AB#100169 aun sin migrar al binario: tienen Base64 y no Gzip.
        await SembrarAsync("s8", null, "b64-historico", null, false);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s8", GzipNuevo, "nuevo", 3, false, false);

        filas.Should().Be(0);
    }

    [Fact]
    public async Task Forzar_SobrescribeSiempreInclusoDegradando()
    {
        await SembrarAsync("s9", GzipViejo, "viejo", 14, true);

        var filas = await _repo.ActualizarMarkdownSiMejoraAsync("s9", GzipNuevo, "nuevo", 3, false, forzar: true);

        filas.Should().Be(1);
        var d = await LeerAsync("s9");
        d.MarkdownCompleto.Should().BeFalse();
        d.MarkdownPaginas.Should().Be(3);
        d.NormalizacionMarkdownGzip.Should().Equal(GzipNuevo);
    }

    [Fact]
    public async Task UpdateAsync_NoRevierteLaCoberturaQueEscribioOtraEjecucionConcurrente()
    {
        // AB#100254: escenario de dos ejecuciones concurrentes del mismo SHA256 (ForceReprocess o
        // SkipDuplicateCheck). A entra en PersistirActivity y lee la fila sin markdown; B, mientras
        // tanto, escribe el documento completo; el SaveChanges de A revertia esas cuatro columnas
        // porque Update() marca TODA la entidad como modificada.
        await SembrarAsync("s10", null, null, null, false);

        // A lee la fila: su copia en memoria no tiene markdown.
        var deLaEjecucionA = await _repo.GetBySHA256Async("s10");
        deLaEjecucionA!.NormalizacionMarkdownGzip.Should().BeNull();

        // B persiste el documento completo por la via atomica.
        (await _repo.ActualizarMarkdownSiMejoraAsync("s10", GzipNuevo, "completo", 14, true, false))
            .Should().Be(1);

        // A cierra su persistencia con los campos que si le tocan.
        deLaEjecucionA.Estado = "OK";
        deLaEjecucionA.ConfianzaGlobal = 0.93;
        await _repo.UpdateAsync(deLaEjecucionA);

        var d = await LeerAsync("s10");
        d.NormalizacionMarkdownGzip.Should().Equal(GzipNuevo, "la actualizacion no puede pisar la cobertura ajena");
        d.NormalizacionMarkdownCompressed.Should().Be("completo");
        d.MarkdownPaginas.Should().Be(14);
        d.MarkdownCompleto.Should().BeTrue();

        // Y los campos que la actualizacion si debe escribir siguen escribiendose.
        d.Estado.Should().Be("OK");
        d.ConfianzaGlobal.Should().Be(0.93);
    }
}
