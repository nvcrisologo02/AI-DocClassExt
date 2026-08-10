using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

// Agregados que alimentan Monitor v2: reparto por calidad, cruce con el estado
// de proceso e histograma de confianza.
public class DocumentoEjecucionRepositoryCalidadTests
{
    private static readonly DateTime Base = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAgregados_RepartePorCalidadSegunLaConfianza()
    {
        await using var ctx = CreateContext();
        Seed(ctx,
            (0.95, "OK"), (0.88, "OK"),                 // fiables
            (0.80, "OK"), (0.72, "OK"),                 // revision
            (0.55, "VALIDACION_CON_ERRORES"));          // insuficiente
        var repo = new DocumentoEjecucionRepository(ctx);

        var r = await repo.GetAgregadosAsync(Filtro());

        r.CalidadOk.Should().Be(2);
        r.CalidadRevision.Should().Be(2);
        r.CalidadError.Should().Be(1);
    }

    // El desglose por estado de proceso no puede perder filas: los tres contadores
    // por EstadoFinal dejaban fuera cualquier estado no contemplado.
    [Fact]
    public async Task GetAgregados_PorEstadoProceso_IncluyeTodosLosEstados()
    {
        await using var ctx = CreateContext();
        Seed(ctx,
            (0.95, "OK"), (0.90, "OK"),
            (0.60, "VALIDACION_CON_ERRORES"),
            (0.10, "PAGINAS_EXCEDIDAS"));
        var repo = new DocumentoEjecucionRepository(ctx);

        var r = await repo.GetAgregadosAsync(Filtro());

        r.PorEstadoProceso.Sum(g => g.Total).Should().Be(4,
            "el desglose por estado debe sumar el total, sin filas huerfanas");
        r.PorEstadoProceso.Select(g => g.Grupo).Should()
            .Contain(["OK", "VALIDACION_CON_ERRORES", "PAGINAS_EXCEDIDAS"]);
    }

    [Fact]
    public async Task GetAgregados_MatrizCruzaProcesoYCalidad()
    {
        await using var ctx = CreateContext();
        Seed(ctx,
            (0.95, "OK"),                        // OK x OK
            (0.78, "OK"), (0.75, "OK"),          // OK x REVISION  <- el cuadrante invisible
            (0.55, "VALIDACION_CON_ERRORES"));   // VALIDACION x ERROR
        var repo = new DocumentoEjecucionRepository(ctx);

        var r = await repo.GetAgregadosAsync(Filtro());

        r.Matriz.Should().Contain(c =>
            c.EstadoProceso == "OK" && c.Calidad == CalidadEjecucion.Revision && c.Total == 2);
        r.Matriz.Should().Contain(c =>
            c.EstadoProceso == "OK" && c.Calidad == CalidadEjecucion.Ok && c.Total == 1);
        r.Matriz.Sum(c => c.Total).Should().Be(4, "la matriz debe cubrir todas las ejecuciones");
    }

    [Fact]
    public async Task GetAgregados_HistogramaAgrupaLaConfianzaEnTramos()
    {
        await using var ctx = CreateContext();
        Seed(ctx, (0.91, "OK"), (0.93, "OK"), (0.72, "OK"), (0.42, "OK"));
        var repo = new DocumentoEjecucionRepository(ctx);

        var r = await repo.GetAgregadosAsync(Filtro());

        r.Histograma.Sum(b => b.Total).Should().Be(4, "ningun documento puede quedar fuera del histograma");
        r.Histograma.Should().Contain(b => b.Desde <= 0.91 && b.Hasta > 0.91 && b.Total == 2);
        r.Histograma.Should().BeInAscendingOrder(b => b.Desde);
    }

    [Fact]
    public async Task GetAgregados_SinEjecuciones_DevuelveColeccionesVacias()
    {
        await using var ctx = CreateContext();
        var repo = new DocumentoEjecucionRepository(ctx);

        var r = await repo.GetAgregadosAsync(Filtro());

        r.TotalEjecuciones.Should().Be(0);
        r.Matriz.Should().BeEmpty();
        r.PorEstadoProceso.Should().BeEmpty();
        r.CalidadOk.Should().Be(0);
    }

    // El filtro debe aplicarse tambien a los agregados nuevos, no solo a los antiguos.
    [Fact]
    public async Task GetAgregados_RespetaElFiltroDeFechas()
    {
        await using var ctx = CreateContext();
        Seed(ctx, (0.95, "OK"), (0.95, "OK"));
        ctx.DocumentoEjecuciones.Add(Entidad(99, 0.30, "OK", Base.AddDays(40)));
        ctx.SaveChanges();
        var repo = new DocumentoEjecucionRepository(ctx);

        var r = await repo.GetAgregadosAsync(Filtro());

        r.TotalEjecuciones.Should().Be(2);
        r.CalidadError.Should().Be(0, "la ejecucion fuera de rango no debe contarse");
    }

    // ── filtros que alimentan el drill-down desde la matriz ──────────────────

    [Fact]
    public async Task GetPaged_FiltraPorCalidad()
    {
        await using var ctx = CreateContext();
        Seed(ctx, (0.95, "OK"), (0.78, "OK"), (0.75, "OK"), (0.40, "OK"));
        var repo = new DocumentoEjecucionRepository(ctx);

        var f = Filtro();
        f.Calidad = CalidadEjecucion.Revision;
        var (items, total) = await repo.GetPagedAsync(f, 1, 25);

        total.Should().Be(2);
        items.Should().OnlyContain(e => e.ConfianzaGlobal >= 0.70 && e.ConfianzaGlobal < 0.85);
    }

    [Fact]
    public async Task GetPaged_FiltraPorEstadoDeProcesoExacto()
    {
        await using var ctx = CreateContext();
        Seed(ctx, (0.95, "OK"), (0.60, "VALIDACION_CON_ERRORES"), (0.55, "VALIDACION_CON_ERRORES"));
        var repo = new DocumentoEjecucionRepository(ctx);

        var f = Filtro();
        f.EstadoProceso = "VALIDACION_CON_ERRORES";
        var (_, total) = await repo.GetPagedAsync(f, 1, 25);

        total.Should().Be(2, "el estado de proceso se filtra por valor exacto, no por las categorias historicas");
    }

    // Al pulsar una barra del histograma se filtra por su tramo concreto.
    [Fact]
    public async Task GetPaged_FiltraPorTramoDeConfianza()
    {
        await using var ctx = CreateContext();
        Seed(ctx, (0.91, "OK"), (0.93, "OK"), (0.72, "OK"));
        var repo = new DocumentoEjecucionRepository(ctx);

        var f = Filtro();
        f.ConfianzaMin = 0.90;
        f.ConfianzaMax = 0.95;
        var (_, total) = await repo.GetPagedAsync(f, 1, 25);

        total.Should().Be(2);
    }

    // La combinacion es lo que hace navegable una celda de la matriz.
    [Fact]
    public async Task GetPaged_CombinaEstadoDeProcesoYCalidad()
    {
        await using var ctx = CreateContext();
        Seed(ctx, (0.95, "OK"), (0.78, "OK"), (0.78, "VALIDACION_CON_ERRORES"));
        var repo = new DocumentoEjecucionRepository(ctx);

        var f = Filtro();
        f.EstadoProceso = "OK";
        f.Calidad = CalidadEjecucion.Revision;
        var (_, total) = await repo.GetPagedAsync(f, 1, 25);

        total.Should().Be(1, "solo la ejecucion que cumple ambas condiciones");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static EjecucionFiltro Filtro() => new() { Desde = Base, Hasta = Base.AddDays(30) };

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"calidad-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }

    // El listado hace Include del documento, asi que las ejecuciones necesitan uno
    // asociado: sin el, GetPagedAsync devuelve la pagina vacia aunque el total cuadre.
    private static void Seed(DocumentIADbContext ctx, params (double Conf, string Estado)[] filas)
    {
        AsegurarDocumento(ctx);
        var id = 1;
        foreach (var (conf, estado) in filas)
        {
            ctx.DocumentoEjecuciones.Add(Entidad(id, conf, estado, Base.AddHours(id)));
            id++;
        }
        ctx.SaveChanges();
    }

    private static void AsegurarDocumento(DocumentIADbContext ctx)
    {
        if (!ctx.Documentos.Any())
        {
            ctx.Documentos.Add(new DocumentoEntity
            {
                Id = 1, NombreArchivo = "nota-simple.pdf", SHA256 = "a", SubmittedBy = "srv_batch"
            });
        }
    }

    private static DocumentoEjecucionEntity Entidad(int id, double conf, string estado, DateTime fecha) => new()
    {
        Id = id,
        DocumentoId = 1,
        EjecucionGuid = Guid.NewGuid().ToString(),
        FechaEjecucion = fecha,
        Tipologia = "TDN1-NOTA",
        EstadoFinal = estado,
        ConfianzaGlobal = conf,
        DuracionTotalMs = 1000
    };
}
