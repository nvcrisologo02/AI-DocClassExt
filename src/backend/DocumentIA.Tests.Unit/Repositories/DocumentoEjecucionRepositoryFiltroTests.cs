using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

public class DocumentoEjecucionRepositoryFiltroTests
{
    private static readonly DateTime Base = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPagedAsync_Should_AcotarPorRangoDeFechas()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (items, total) = await repo.GetPagedAsync(
            Filtro(Base, Base.AddDays(1)), page: 1, pageSize: 25);

        total.Should().Be(2);
        items.Should().OnlyContain(e => e.FechaEjecucion < Base.AddDays(1));
    }

    [Fact]
    public async Task GetPagedAsync_Should_DevolverTotalDelFiltroNoDeLaPagina()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (items, total) = await repo.GetPagedAsync(
            Filtro(Base, Base.AddDays(10)), page: 1, pageSize: 2);

        items.Should().HaveCount(2);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedAsync_Should_OrdenarPorFechaDescendente()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (items, _) = await repo.GetPagedAsync(
            Filtro(Base, Base.AddDays(10)), page: 1, pageSize: 25);

        items.Select(e => e.FechaEjecucion).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetPagedAsync_Should_DevolverLaSegundaPagina()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (pagina1, _) = await repo.GetPagedAsync(Filtro(Base, Base.AddDays(10)), 1, 2);
        var (pagina2, _) = await repo.GetPagedAsync(Filtro(Base, Base.AddDays(10)), 2, 2);

        pagina2.Should().HaveCount(2);
        pagina2.Select(e => e.Id).Should().NotIntersectWith(pagina1.Select(e => e.Id));
    }

    [Fact]
    public async Task GetPagedAsync_Should_FiltrarPorTipologia()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Tipologia = "NOTS";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(2);
        items.Should().OnlyContain(e => e.Tipologia == "NOTS");
    }

    [Fact]
    public async Task GetPagedAsync_Should_FiltrarPorEstadoNormalizado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Estado = "ERROR";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(2, "hay una ejecucion 'Error' y otra 'Fallido'");
        items.Should().OnlyContain(e => EstadoEjecucion.Error.Contains(e.EstadoFinal));
    }

    [Fact]
    public async Task GetPagedAsync_Should_FiltrarPorFlujo()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Flujo = "Clasificacion";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(2);
        items.Should().OnlyContain(e => e.ClassificationOnly);
    }

    [Fact]
    public async Task GetPagedAsync_Should_BuscarPorGuidExacto()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Busqueda = "11111111-1111-1111-1111-111111111111";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(1);
        items[0].EjecucionGuid.Should().Be("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task GetPagedAsync_Should_BuscarPorFragmentoDeNombreDeDocumento()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Busqueda = "escritura";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(1);
        items[0].Documento!.NombreArchivo.Should().Contain("escritura");
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_ContarPorCategoriaDeEstado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetAgregadosAsync(Filtro(Base, Base.AddDays(10)));

        r.TotalEjecuciones.Should().Be(5);
        r.Ok.Should().Be(2, "OK y Completado cuentan como exito");
        r.Error.Should().Be(2, "Error y Fallido cuentan como error");
        r.Revision.Should().Be(1);
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_IncluirDiasSinEjecucionesAcero()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetAgregadosAsync(Filtro(Base, Base.AddDays(7)));

        r.Serie.Should().HaveCount(7, "un punto por dia natural del rango, con huecos incluidos");
        r.Serie.Select(p => p.Fecha).Should().BeInAscendingOrder();
        r.Serie.Should().Contain(p => p.Fecha.Date == Base.AddDays(1).Date && p.Total == 0);
        r.Serie.Single(p => p.Fecha.Date == Base.Date).Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_IncluirElDiaEnCursoCuandoHastaNoEsMedianoche()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(3).AddHours(15));

        var r = await repo.GetAgregadosAsync(filtro);
        var (_, totalListado) = await repo.GetPagedAsync(filtro, 1, 25);

        r.Serie.Should().HaveCount(4, "el dia en curso debe entrar en la serie aunque Hasta no sea medianoche");
        r.Serie.Select(p => p.Fecha).Should().BeInAscendingOrder();
        r.Serie.Last().Fecha.Date.Should().Be(Base.AddDays(3).Date);
        r.Serie.Sum(p => p.Total).Should().Be(totalListado,
            "la serie debe seguir sumando el total aunque Hasta no sea medianoche");
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_DevolverUnSoloDiaCuandoDesdeYHastaCoincidenElMismoDia()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddHours(12));

        var r = await repo.GetAgregadosAsync(filtro);
        var (_, totalListado) = await repo.GetPagedAsync(filtro, 1, 25);

        r.Serie.Should().HaveCount(1, "Desde y Hasta caen el mismo dia natural");
        r.Serie.Single().Fecha.Date.Should().Be(Base.Date);
        r.Serie.Sum(p => p.Total).Should().Be(totalListado,
            "la serie no debe salir vacia cuando el rango cae en un solo dia con ejecuciones");
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_RespetarElMismoFiltroQueElListado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Tipologia = "NOTS";

        var agregados = await repo.GetAgregadosAsync(filtro);
        var (_, totalListado) = await repo.GetPagedAsync(filtro, 1, 25);

        agregados.TotalEjecuciones.Should().Be(totalListado,
            "la cabecera de KPIs y la tabla deben describir el mismo conjunto");
        agregados.Serie.Sum(p => p.Total).Should().Be(totalListado,
            "la serie debe sumar exactamente el total del filtro");
    }

    private static EjecucionFiltro Filtro(DateTime desde, DateTime hasta) =>
        new() { Desde = desde, Hasta = hasta };

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"ejecuciones-filtro-tests-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }

    private static void Seed(DocumentIADbContext context)
    {
        var doc1 = new DocumentoEntity { Id = 1, NombreArchivo = "nota-simple.pdf", SHA256 = "a" };
        var doc2 = new DocumentoEntity { Id = 2, NombreArchivo = "escritura-compraventa.pdf", SHA256 = "b" };
        context.Documentos.AddRange(doc1, doc2);

        context.DocumentoEjecuciones.AddRange(
            Ejecucion(1, "11111111-1111-1111-1111-111111111111", Base,             "NOTS", "OK",        docId: 1, soloClasificacion: true),
            Ejecucion(2, "22222222-2222-2222-2222-222222222222", Base.AddHours(5), "NOTS", "Error",     docId: 1, soloClasificacion: true),
            Ejecucion(3, "33333333-3333-3333-3333-333333333333", Base.AddDays(2),  "ESCR", "Fallido",   docId: 2, soloClasificacion: false),
            Ejecucion(4, "44444444-4444-4444-4444-444444444444", Base.AddDays(4),  "ESCR", "Completado", docId: 1, soloClasificacion: false),
            Ejecucion(5, "55555555-5555-5555-5555-555555555555", Base.AddDays(6),  "CERA", "REVISION",  docId: 1, soloClasificacion: false));

        context.SaveChanges();
    }

    private static DocumentoEjecucionEntity Ejecucion(
        int id, string guid, DateTime fecha, string tipologia, string estado, int docId, bool soloClasificacion) =>
        new()
        {
            Id = id,
            DocumentoId = docId,
            EjecucionGuid = guid,
            FechaEjecucion = fecha,
            Tipologia = tipologia,
            EstadoFinal = estado,
            ClassificationOnly = soloClasificacion,
            ConfianzaGlobal = 0.9,
            DuracionTotalMs = 1000
        };
}
