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
}
