using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

/// <summary>
/// Una reutilizacion por duplicado no es una ejecucion de IA: tiene que verse cuando se
/// pide y no contaminar ninguna cifra cuando no (AB#100258).
/// </summary>
public class DocumentoEjecucionRepositoryReutilizadasTests
{
    private static readonly DateTime Base = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPagedAsync_Should_ExcluirLasReutilizadasPorDefecto()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (items, total) = await repo.GetPagedAsync(Filtro(), 1, 25);

        total.Should().Be(2);
        items.Should().OnlyContain(e => !e.ReutilizadaPorDuplicado);
    }

    [Fact]
    public async Task GetPagedAsync_Should_IncluirlasCuandoSePiden()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro();
        filtro.Reutilizadas = FiltroReutilizadas.Incluir;

        var (_, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(3);
    }

    [Fact]
    public async Task GetPagedAsync_Should_DevolverSoloLasReutilizadasCuandoSePideSolo()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro();
        filtro.Reutilizadas = FiltroReutilizadas.Solo;

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(1);
        items.Should().OnlyContain(e => e.ReutilizadaPorDuplicado);
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_NoContarLasReutilizadasEnLasCategorias()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var agregados = await repo.GetAgregadosAsync(Filtro());

        agregados.TotalEjecuciones.Should().Be(2);
        agregados.Ok.Should().Be(2);
    }

    [Fact]
    public async Task GetCostesAsync_Should_NoSumarElCosteDeLasReutilizadas()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var costes = await repo.GetCostesAsync(Filtro());

        costes.TotalEjecuciones.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_Should_ProyectarLaMarcaYElOriginal()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro();
        filtro.Reutilizadas = FiltroReutilizadas.Solo;

        var (items, _) = await repo.GetPagedAsync(filtro, 1, 25);

        items.Single().ReutilizadaPorDuplicado.Should().BeTrue();
        items.Single().EjecucionOriginalId.Should().Be(2);
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_ContarLasReutilizadasYElCosteEvitado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var agregados = await repo.GetAgregadosAsync(Filtro());

        agregados.Reutilizadas.Should().Be(1);
        agregados.CosteEvitadoEur.Should().Be(0.07m);
    }

    [Fact]
    public async Task GetReutilizacionesAsync_Should_DevolverLasQueApuntanAEsaEjecucion()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var reutilizaciones = await repo.GetReutilizacionesAsync(2);

        reutilizaciones.Should().ContainSingle()
            .Which.EjecucionGuid.Should().Be("33333333-3333-3333-3333-333333333333");
    }

    [Fact]
    public async Task GetReutilizacionesAsync_Should_DevolverVacioSiNadieLaReutilizo()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var reutilizaciones = await repo.GetReutilizacionesAsync(1);

        reutilizaciones.Should().BeEmpty();
    }

    private static EjecucionFiltro Filtro() =>
        new() { Desde = Base, Hasta = Base.AddDays(10) };

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"ejecuciones-reutilizadas-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }

    private static void Seed(DocumentIADbContext context)
    {
        context.Documentos.Add(new DocumentoEntity { Id = 1, NombreArchivo = "doc.pdf", SHA256 = "sha-1" });

        context.DocumentoEjecuciones.AddRange(
            new DocumentoEjecucionEntity
            {
                Id = 1,
                DocumentoId = 1,
                EjecucionGuid = "11111111-1111-1111-1111-111111111111",
                FechaEjecucion = Base,
                EstadoFinal = "OK",
                Tipologia = "NOTS",
                ConfianzaGlobal = 0.9,
                DuracionTotalMs = 1000,
                CosteIAEur = 0.05m
            },
            new DocumentoEjecucionEntity
            {
                Id = 2,
                DocumentoId = 1,
                EjecucionGuid = "22222222-2222-2222-2222-222222222222",
                FechaEjecucion = Base.AddDays(1),
                EstadoFinal = "OK",
                Tipologia = "NOTS",
                ConfianzaGlobal = 0.9,
                DuracionTotalMs = 1200,
                CosteIAEur = 0.07m
            },
            // La reutilizacion copia estado y tipologia del original, pero no tiene
            // contrato ni coste propios: su duracion es la de servir la respuesta.
            new DocumentoEjecucionEntity
            {
                Id = 3,
                DocumentoId = 1,
                EjecucionGuid = "33333333-3333-3333-3333-333333333333",
                FechaEjecucion = Base.AddDays(2),
                EstadoFinal = "OK",
                Tipologia = "NOTS",
                ConfianzaGlobal = 0.9,
                DuracionTotalMs = 40,
                ReutilizadaPorDuplicado = true,
                EjecucionOriginalId = 2
            });

        context.SaveChanges();
    }
}
