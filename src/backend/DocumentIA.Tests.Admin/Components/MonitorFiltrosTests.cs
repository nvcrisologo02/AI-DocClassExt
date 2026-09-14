using AngleSharp.Dom;
using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

// Rango de fechas fijo en los filtros compartidos por Monitor y Costes (AB#100284).
public class MonitorFiltrosTests : TestContext
{
    private IRenderedComponent<MonitorFiltros> Render(MonitorFiltroDto filtro, List<MonitorFiltroDto> emitidos) =>
        RenderComponent<MonitorFiltros>(p => p
            .Add(c => c.Filtro, filtro)
            .Add(c => c.FiltroCambiado, f => emitidos.Add(f)));

    private static IElement SelectRango(IRenderedComponent<MonitorFiltros> cut) =>
        cut.FindAll("select").First(s => s.QuerySelectorAll("option").Any(o => o.GetAttribute("value") == "custom"));

    [Fact]
    public void Rango_OfreceLaOpcionPersonalizado()
    {
        var cut = Render(new MonitorFiltroDto(), []);

        var textos = SelectRango(cut).QuerySelectorAll("option").Select(o => o.TextContent.Trim());

        textos.Should().Contain("Personalizado");
    }

    [Fact]
    public void ConRangoRelativo_NoMuestraLasFechas()
    {
        var cut = Render(new MonitorFiltroDto { RangoDias = 30 }, []);

        cut.FindAll("input[type=date]").Should().BeEmpty();
    }

    // El caso de uso principal es un mes natural: al pasar a Personalizado las fechas
    // salen precargadas con el mes en curso para que el usuario solo tenga que ajustar.
    [Fact]
    public void ElegirPersonalizado_EmiteElMesEnCursoEnHoraPeninsular()
    {
        var emitidos = new List<MonitorFiltroDto>();
        var cut = Render(new MonitorFiltroDto { RangoDias = 7 }, emitidos);

        SelectRango(cut).Change("custom");

        var hoy = DateOnly.FromDateTime(HoraEspana.Desde(DateTime.UtcNow));
        emitidos.Should().ContainSingle();
        emitidos[0].Desde.Should().Be(new DateOnly(hoy.Year, hoy.Month, 1));
        emitidos[0].Hasta.Should().Be(hoy);
        emitidos[0].EsRangoFijo.Should().BeTrue();
    }

    [Fact]
    public void ConRangoFijo_MuestraLasFechasYSeleccionaPersonalizado()
    {
        var filtro = new MonitorFiltroDto { Desde = new DateOnly(2026, 8, 1), Hasta = new DateOnly(2026, 8, 31) };
        var cut = Render(filtro, []);

        var fechas = cut.FindAll("input[type=date]").Select(i => i.GetAttribute("value")).ToList();

        fechas.Should().Equal("2026-08-01", "2026-08-31");
        SelectRango(cut).GetAttribute("value").Should().Be("custom");
    }

    [Fact]
    public void CambiarDesde_EmiteLaNuevaFechaConservandoHasta()
    {
        var emitidos = new List<MonitorFiltroDto>();
        var filtro = new MonitorFiltroDto { Desde = new DateOnly(2026, 8, 1), Hasta = new DateOnly(2026, 8, 31) };
        var cut = Render(filtro, emitidos);

        cut.FindAll("input[type=date]")[0].Change("2026-07-01");

        emitidos.Should().ContainSingle();
        emitidos[0].Desde.Should().Be(new DateOnly(2026, 7, 1));
        emitidos[0].Hasta.Should().Be(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void CambiarHasta_EmiteLaNuevaFechaConservandoDesde()
    {
        var emitidos = new List<MonitorFiltroDto>();
        var filtro = new MonitorFiltroDto { Desde = new DateOnly(2026, 8, 1), Hasta = new DateOnly(2026, 8, 31) };
        var cut = Render(filtro, emitidos);

        cut.FindAll("input[type=date]")[1].Change("2026-09-15");

        emitidos.Should().ContainSingle();
        emitidos[0].Desde.Should().Be(new DateOnly(2026, 8, 1));
        emitidos[0].Hasta.Should().Be(new DateOnly(2026, 9, 15));
    }

    // Un valor vacio (el usuario borra la fecha en el navegador) no debe romper el
    // filtro: se queda la ventana relativa hasta que vuelva a haber dos fechas.
    [Fact]
    public void BorrarUnaFecha_DejaEseExtremoVacio()
    {
        var emitidos = new List<MonitorFiltroDto>();
        var filtro = new MonitorFiltroDto { Desde = new DateOnly(2026, 8, 1), Hasta = new DateOnly(2026, 8, 31) };
        var cut = Render(filtro, emitidos);

        cut.FindAll("input[type=date]")[1].Change("");

        emitidos.Should().ContainSingle();
        emitidos[0].Hasta.Should().BeNull();
        emitidos[0].EsRangoFijo.Should().BeFalse();
    }

    [Fact]
    public void VolverAUnPreset_LimpiaLasFechasYFijaRangoDias()
    {
        var emitidos = new List<MonitorFiltroDto>();
        var filtro = new MonitorFiltroDto { Desde = new DateOnly(2026, 8, 1), Hasta = new DateOnly(2026, 8, 31) };
        var cut = Render(filtro, emitidos);

        SelectRango(cut).Change("30");

        emitidos.Should().ContainSingle();
        emitidos[0].Desde.Should().BeNull();
        emitidos[0].Hasta.Should().BeNull();
        emitidos[0].RangoDias.Should().Be(30);
    }
}
