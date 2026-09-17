using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

// El listado del Monitor proyecta un DTO ligero: nada de LOBs (contrato, datos
// originales/finales, markdown del documento), solo escalares + ActivityTimelineJson.
public class DocumentoEjecucionRepositoryListadoTests
{
    private static readonly DateTime Base = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPaged_ProyectaLosCamposDelListado()
    {
        await using var ctx = CreateContext();
        ctx.Documentos.Add(new DocumentoEntity
        {
            Id = 1,
            NombreArchivo = "nota-simple.pdf",
            SHA256 = "a",
            SubmittedBy = "srv_batch"
        });
        ctx.DocumentoEjecuciones.Add(new DocumentoEjecucionEntity
        {
            Id = 1,
            DocumentoId = 1,
            EjecucionGuid = "11111111-1111-1111-1111-111111111111",
            FechaEjecucion = Base.AddHours(1),
            Tipologia = "TDN1-NOTA",
            EstadoFinal = "OK",
            ConfianzaGlobal = 0.95,
            ConfianzaClasificacion = 0.97,
            DuracionTotalMs = 1200,
            DuracionClasificacionMs = 800,
            ActivityTimelineJson = "[{\"Nombre\":\"Clasificar\",\"Estado\":\"OK\"}]",
            ContratoSalidaCompletoJson = "{\"Relleno\":\"" + new string('x', 100_000) + "\"}"
        });
        ctx.SaveChanges();
        var repo = new DocumentoEjecucionRepository(ctx);

        var (items, total) = await repo.GetPagedAsync(Filtro(), 1, 25);

        total.Should().Be(1);
        var item = items.Single();
        item.Should().BeOfType<EjecucionListadoItem>();
        item.EjecucionGuid.Should().Be("11111111-1111-1111-1111-111111111111");
        item.NombreDocumento.Should().Be("nota-simple.pdf");
        item.SubmittedBy.Should().Be("srv_batch", "sin SubmittedBy propio cae al del documento");
        item.ActivityTimelineJson.Should().Contain("Clasificar");
        item.DuracionClasificacionMs.Should().Be(800);
    }

    [Fact]
    public async Task GetPaged_ElSubmittedByPropioTienePrioridadSobreElDelDocumento()
    {
        await using var ctx = CreateContext();
        ctx.Documentos.Add(new DocumentoEntity
        {
            Id = 1,
            NombreArchivo = "nota-simple.pdf",
            SHA256 = "a",
            SubmittedBy = "srv_batch"
        });
        ctx.DocumentoEjecuciones.Add(new DocumentoEjecucionEntity
        {
            Id = 1,
            DocumentoId = 1,
            FechaEjecucion = Base.AddHours(1),
            EstadoFinal = "OK",
            ConfianzaGlobal = 0.9,
            DuracionTotalMs = 100,
            SubmittedBy = "reproceso-manual"
        });
        ctx.SaveChanges();
        var repo = new DocumentoEjecucionRepository(ctx);

        var (items, _) = await repo.GetPagedAsync(Filtro(), 1, 25);

        items.Single().SubmittedBy.Should().Be("reproceso-manual");
    }

    // Documentos se deduplica por SHA-256 y conserva el primer nombre: si el mismo
    // contenido llega con otro nombre, la fila debe mostrar el de su propia peticion.
    [Fact]
    public async Task GetPaged_ElNombreDeLaEjecucionTienePrioridadSobreElDelDocumento()
    {
        await using var ctx = CreateContext();
        SeedDocumentoConEjecucion(ctx,
            "{\"Identificacion\":{\"Documento\":\"documento-demo-documentia.pdf\"}}");
        var repo = new DocumentoEjecucionRepository(ctx);

        var (items, _) = await repo.GetPagedAsync(Filtro(), 1, 25);

        items.Single().NombreDocumento.Should().Be("documento-demo-documentia.pdf");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"Identificacion\":{\"Documento\":\"\"}}")]
    [InlineData("{\"Identificacion\":{}}")]
    public async Task GetPaged_SinNombreEnElContratoCaeAlDelDocumento(string? contrato)
    {
        await using var ctx = CreateContext();
        SeedDocumentoConEjecucion(ctx, contrato);
        var repo = new DocumentoEjecucionRepository(ctx);

        var (items, _) = await repo.GetPagedAsync(Filtro(), 1, 25);

        items.Single().NombreDocumento.Should().Be("nota-simple-sintetica.pdf");
    }

    [Fact]
    public async Task GetReutilizaciones_UsaElNombreDeLaEjecucion()
    {
        await using var ctx = CreateContext();
        SeedDocumentoConEjecucion(ctx, contrato: null);
        ctx.DocumentoEjecuciones.Add(new DocumentoEjecucionEntity
        {
            Id = 2,
            DocumentoId = 1,
            FechaEjecucion = Base.AddHours(2),
            EstadoFinal = "OK",
            DuracionTotalMs = 10,
            ReutilizadaPorDuplicado = true,
            EjecucionOriginalId = 1,
            ContratoSalidaCompletoJson = "{\"Identificacion\":{\"Documento\":\"reenvio.pdf\"}}"
        });
        ctx.SaveChanges();
        var repo = new DocumentoEjecucionRepository(ctx);

        var reutilizaciones = await repo.GetReutilizacionesAsync(1);

        reutilizaciones.Single().NombreDocumento.Should().Be("reenvio.pdf");
    }

    private static void SeedDocumentoConEjecucion(DocumentIADbContext ctx, string? contrato)
    {
        ctx.Documentos.Add(new DocumentoEntity { Id = 1, NombreArchivo = "nota-simple-sintetica.pdf", SHA256 = "a" });
        ctx.DocumentoEjecuciones.Add(new DocumentoEjecucionEntity
        {
            Id = 1,
            DocumentoId = 1,
            FechaEjecucion = Base.AddHours(1),
            EstadoFinal = "OK",
            ConfianzaGlobal = 0.9,
            DuracionTotalMs = 100,
            ContratoSalidaCompletoJson = contrato
        });
        ctx.SaveChanges();
    }

    private static EjecucionFiltro Filtro() => new() { Desde = Base, Hasta = Base.AddDays(30) };

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"listado-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }
}
