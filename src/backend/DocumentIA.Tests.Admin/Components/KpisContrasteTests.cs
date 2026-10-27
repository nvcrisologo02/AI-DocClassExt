using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

// Las cifras de los KPI en turquesa (2,8:1) o naranja (2,1:1) no llegan al 3:1
// de texto grande. La cifra va en verde corporativo y el estado lo marca una
// barra lateral del color de la paleta (AB#101124).
public class KpisContrasteTests : TestContext
{
    private static void SinCifrasEnColorDeEstado(IRenderedFragment cut) =>
        cut.FindAll(".mv2-kpi-value")
            .Where(v => v.ClassList.Any(c => c is "text-success" or "text-warning" or "text-danger"))
            .Should().BeEmpty();

    [Fact]
    public void CostesKpis_MarcaElEstadoSinColorearLaCifra()
    {
        var costes = new CostesResumenDto { TotalEjecuciones = 4, ConCosteReal = 3, ConCosteEstimado = 1, CosteTotalEur = 1m };

        var cut = RenderComponent<CostesKpis>(p => p.Add(c => c.Costes, costes));

        SinCifrasEnColorDeEstado(cut);
        cut.FindAll(".mv2-kpi-value.sareb-kpi-ok").Should().HaveCount(1);
        cut.FindAll(".mv2-kpi-value.sareb-kpi-revision").Should().HaveCount(1);
    }

    [Fact]
    public void MonitorCalidadKpis_MarcaElEstadoSinColorearLaCifra()
    {
        var agregados = new DashboardAgregadosDto { TotalEjecuciones = 10, Ok = 6, Revision = 3, Error = 1 };

        var cut = RenderComponent<MonitorCalidadKpis>(p => p.Add(c => c.Agregados, agregados));

        SinCifrasEnColorDeEstado(cut);
        cut.FindAll(".mv2-kpi-value.sareb-kpi-ok").Should().HaveCount(2);
        cut.FindAll(".mv2-kpi-value.sareb-kpi-revision").Should().HaveCount(1);
        cut.FindAll(".mv2-kpi-value.sareb-kpi-error").Should().HaveCount(1);
    }
}
