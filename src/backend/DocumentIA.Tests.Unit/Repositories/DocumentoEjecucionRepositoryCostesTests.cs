using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

/// <summary>
/// AB#100237: agregados de coste de IA. Se calculan sobre columnas escalares, y lo
/// estimado por el relleno retroactivo queda fuera de los importes salvo que se
/// pida, aunque siempre se cuente para que se vea cuanto hay de cada cosa.
/// </summary>
public class DocumentoEjecucionRepositoryCostesTests
{
    private static readonly DateTime Base = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetCostesAsync_SumaSoloLoMedidoPorDefecto()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetCostesAsync(Filtro(Base, Base.AddDays(10)));

        r.TotalEjecuciones.Should().Be(5);
        r.ConCosteReal.Should().Be(2);
        r.ConCosteEstimado.Should().Be(2);
        r.SinCoste.Should().Be(1);
        r.IncluyeEstimados.Should().BeFalse();
        r.EjecucionesConImporte.Should().Be(2);
        r.CosteTotalEur.Should().Be(0.30m);
        r.CosteMedioEur.Should().Be(0.15m);
        r.TokensTotales.Should().Be(3000);
    }

    [Fact]
    public async Task GetCostesAsync_ConIncluirEstimados_SumaTambienLoEstimado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.IncluirEstimados = true;
        var r = await repo.GetCostesAsync(filtro);

        r.IncluyeEstimados.Should().BeTrue();
        r.EjecucionesConImporte.Should().Be(4);
        r.CosteTotalEur.Should().Be(0.30m + 0.50m);
        // Los recuentos por origen no cambian con el flag.
        r.ConCosteReal.Should().Be(2);
        r.ConCosteEstimado.Should().Be(2);
    }

    [Fact]
    public async Task GetCostesAsync_DesglosaPorActividad()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetCostesAsync(Filtro(Base, Base.AddDays(10)));

        r.LayoutEur.Should().Be(0.20m);
        r.ClasificacionEur.Should().Be(0.10m);
        r.ExtraccionEur.Should().Be(0m);
        r.PromptEur.Should().Be(0m);
        (r.LayoutEur + r.ClasificacionEur + r.ExtraccionEur + r.PromptEur).Should().Be(r.CosteTotalEur);
    }

    [Fact]
    public async Task GetCostesAsync_AgrupaPorTipologiaOrdenadoPorCoste()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetCostesAsync(Filtro(Base, Base.AddDays(10)));

        r.PorTipologia.Should().HaveCount(2);
        r.PorTipologia[0].Grupo.Should().Be("NOTS");
        r.PorTipologia[0].CosteEur.Should().Be(0.25m);
        r.PorTipologia[0].ConImporte.Should().Be(1);
        r.PorTipologia[0].Total.Should().Be(3);
        r.PorTipologia[1].Grupo.Should().Be("ESCR");
        r.PorTipologia[1].CosteEur.Should().Be(0.05m);
    }

    [Fact]
    public async Task GetCostesAsync_SerieDiariaConDiasACero()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetCostesAsync(Filtro(Base, Base.AddDays(3)));

        r.Serie.Should().HaveCount(3);
        r.Serie[0].Fecha.Should().Be(Base.Date);
        r.Serie[0].CosteEur.Should().Be(0.25m);
        // Dia sin ejecuciones: punto a cero, no ausente.
        r.Serie[1].Total.Should().Be(0);
        r.Serie[1].CosteEur.Should().Be(0m);
    }

    [Fact]
    public async Task GetCostesAsync_RespetaElFiltroDeTipologia()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Tipologia = "ESCR";
        var r = await repo.GetCostesAsync(filtro);

        r.TotalEjecuciones.Should().Be(2);
        r.CosteTotalEur.Should().Be(0.05m);
    }

    [Fact]
    public async Task GetCostesAsync_SinEjecuciones_DevuelveCeros()
    {
        await using var context = CreateContext();
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetCostesAsync(Filtro(Base, Base.AddDays(1)));

        r.TotalEjecuciones.Should().Be(0);
        r.CosteTotalEur.Should().Be(0m);
        r.CosteMedioEur.Should().Be(0m);
        r.PorTipologia.Should().BeEmpty();
        r.Serie.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_ProyectaCosteYMarcaDeEstimado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (items, _) = await repo.GetPagedAsync(Filtro(Base, Base.AddDays(10)), page: 1, pageSize: 25);

        var real = items.Single(i => i.Id == 1);
        real.CosteIAEur.Should().Be(0.25m);
        real.CosteEstimado.Should().BeFalse();

        var estimada = items.Single(i => i.Id == 3);
        estimada.CosteIAEur.Should().Be(0.30m);
        estimada.CosteEstimado.Should().BeTrue();

        items.Single(i => i.Id == 5).CosteIAEur.Should().BeNull();
    }

    private static EjecucionFiltro Filtro(DateTime desde, DateTime hasta) =>
        new() { Desde = desde, Hasta = hasta };

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"ejecuciones-costes-tests-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }

    private static void Seed(DocumentIADbContext context)
    {
        context.Documentos.Add(new DocumentoEntity { Id = 1, NombreArchivo = "doc.pdf", SHA256 = "a" });

        context.DocumentoEjecuciones.AddRange(
            // Medidas
            Ejecucion(1, Base, "NOTS", "gpt-4o-mini", coste: 0.25m, layout: 0.20m, clasif: 0.05m, tokens: 2000, estimado: false),
            Ejecucion(2, Base.AddDays(2), "ESCR", "gpt-4o-mini", coste: 0.05m, layout: null, clasif: 0.05m, tokens: 1000, estimado: false),
            // Estimadas por el relleno retroactivo
            Ejecucion(3, Base.AddDays(2), "NOTS", "gpt-4o-mini", coste: 0.30m, layout: 0.30m, clasif: null, tokens: null, estimado: true),
            Ejecucion(4, Base.AddDays(4), "ESCR", "gpt-5-mini", coste: 0.20m, layout: 0.20m, clasif: null, tokens: null, estimado: true),
            // Sin coste
            Ejecucion(5, Base.AddDays(6), "NOTS", null, coste: null, layout: null, clasif: null, tokens: null, estimado: false));

        context.SaveChanges();
    }

    private static DocumentoEjecucionEntity Ejecucion(
        int id, DateTime fecha, string tipologia, string? modelo,
        decimal? coste, decimal? layout, decimal? clasif, int? tokens, bool estimado) =>
        new()
        {
            Id = id,
            DocumentoId = 1,
            EjecucionGuid = $"{id:D8}-0000-0000-0000-000000000000",
            FechaEjecucion = fecha,
            Tipologia = tipologia,
            ModeloClasificacion = modelo,
            EstadoFinal = "OK",
            ConfianzaGlobal = 0.9,
            DuracionTotalMs = 1000,
            CosteIAEur = coste,
            CosteLayoutEur = layout,
            CosteClasificacionEur = clasif,
            TokensIA = tokens,
            CosteEstimado = estimado
        };
}
