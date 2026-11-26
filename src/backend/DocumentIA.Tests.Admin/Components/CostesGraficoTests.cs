using System.Text.Json;
using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

// Grafico de coste diario segun la guia de estilo 6.6 (AB#101126).
public class CostesGraficoTests : TestContext
{
    private static List<CosteSeriePuntoDto> SerieDeUnDia() =>
    [
        new() { Fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), CosteEur = 1.25m }
    ];

    [Fact]
    public void Opciones_UsanElVerdeCorporativo()
    {
        JSInterop.Setup<bool>("documentIaCharts.crear", _ => true).SetResult(true);

        RenderComponent<CostesGrafico>(p => p.Add(c => c.Serie, SerieDeUnDia()));

        var opciones = JsonSerializer.SerializeToElement(JSInterop.Invocations["documentIaCharts.crear"].Single().Arguments[1]);
        opciones.GetProperty("colors").EnumerateArray().Select(c => c.GetString()).Should().Equal(SarebPaleta.Verde);
    }

    // La rejilla tambien sale de SarebPaleta: el interop JS no guarda ningun color.
    [Fact]
    public void Opciones_LlevanLaRejillaConElGrisDeLaPaleta()
    {
        JSInterop.Setup<bool>("documentIaCharts.crear", _ => true).SetResult(true);

        RenderComponent<CostesGrafico>(p => p.Add(c => c.Serie, SerieDeUnDia()));

        var opciones = JsonSerializer.SerializeToElement(JSInterop.Invocations["documentIaCharts.crear"].Single().Arguments[1]);
        opciones.TryGetProperty("grid", out var rejilla).Should().BeTrue();
        rejilla.GetProperty("borderColor").GetString().Should().Be(SarebPaleta.GrisClaro);
    }

    [Fact]
    public void Tarjeta_MuestraTituloConPeriodoUnidadYFuente()
    {
        JSInterop.Setup<bool>("documentIaCharts.crear", _ => true).SetResult(true);

        var cut = RenderComponent<CostesGrafico>(p => p
            .Add(c => c.Serie, SerieDeUnDia())
            .Add(c => c.Periodo, "mes en curso"));

        cut.Find(".sareb-grafico-titulo").TextContent.Should().Be("coste de IA por día · mes en curso");
        cut.Find(".sareb-grafico-unidad").TextContent.Should().Be("€");
        cut.Find(".sareb-grafico-fuente").TextContent.Should().StartWith("Fuente:");
    }
}
