using Bunit;
using DocumentIA.Admin.Components.Layout;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

// Barra lateral con la marca en negativo sobre verde corporativo (manual 1.4).
public class NavMenuTests : TestContext
{
    [Fact]
    public void Cabecera_MuestraElLogoNegativoYLaDenominacion()
    {
        var cut = RenderComponent<NavMenu>();

        var logo = cut.Find("img.sareb-logo");
        logo.GetAttribute("src").Should().Be("img/sareb-logo-negativo.png");
        logo.GetAttribute("alt").Should().Be("SAREB");
        cut.Find(".sareb-denominacion").TextContent.Trim().Should().Be("DocumentIA · Administración");
    }

    [Fact]
    public void Cabecera_YaNoMuestraElNombreTecnicoDelProyecto()
    {
        var cut = RenderComponent<NavMenu>();

        cut.Markup.Should().NotContain("DocumentIA.Admin");
    }

    private static List<(string Href, string Texto)> Enlaces(IRenderedComponent<NavMenu> cut) =>
        cut.FindAll("nav a.nav-link")
            .Select(a => (a.GetAttribute("href") ?? "", a.TextContent.Trim()))
            .ToList();

    [Fact]
    public void Menu_MonitorApuntaALaVersionDos()
    {
        var cut = RenderComponent<NavMenu>();

        Enlaces(cut).Should().ContainSingle(e => e.Texto == "Monitor").Which.Href.Should().Be("monitor-v2");
    }

    [Fact]
    public void Menu_IncluyeCuadroDeCostes()
    {
        var cut = RenderComponent<NavMenu>();

        Enlaces(cut).Should().ContainSingle(e => e.Texto == "Cuadro de costes").Which.Href.Should().Be("costes");
    }

    [Fact]
    public void Menu_NoEnlazaElMonitorOriginal()
    {
        var cut = RenderComponent<NavMenu>();

        Enlaces(cut).Select(e => e.Href).Should().NotContain("monitor");
    }
}
