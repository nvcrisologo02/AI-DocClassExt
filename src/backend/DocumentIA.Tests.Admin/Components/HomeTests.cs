using Bunit;
using DocumentIA.Admin.Components.Pages;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

// Inicio con cuatro accesos (AB#101127).
public class HomeTests : TestContext
{
    [Fact]
    public void MuestraLasCuatroTarjetasDeAcceso()
    {
        var cut = RenderComponent<Home>();

        var accesos = cut.FindAll("a.sareb-acceso")
            .Select(a => (a.GetAttribute("href"), a.QuerySelector(".sareb-acceso-titulo")!.TextContent.Trim()))
            .ToList();

        accesos.Should().Equal(
            ("tipologias", "Tipologías"),
            ("monitor-v2", "Monitor"),
            ("costes", "Cuadro de costes"),
            ("configuracion-consulta", "Configuración"));
    }

    [Fact]
    public void MuestraElTituloYLaDescripcion()
    {
        var cut = RenderComponent<Home>();

        cut.Find("h1").TextContent.Trim().Should().Be("DocumentIA");
        cut.Markup.Should().Contain("Administración de la clasificación documental: configuración, seguimiento y costes.");
    }
}
