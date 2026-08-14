using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;
using Microsoft.JSInterop;

namespace DocumentIA.Tests.Admin.Components;

// El grafico dibuja llamando a JS desde OnAfterRenderAsync. Si esa llamada lanza,
// la excepcion escapa del ciclo de render y termina el circuito de Blazor Server:
// el usuario ve la pagina en error en vez de un grafico ausente. Es un fallo
// esperable, no excepcional: el circuito puede cerrarse entre el render y la
// llamada (corte de WebSocket), y ApexCharts puede fallar con datos inesperados.
public class MonitorGraficosTests : TestContext
{
    private static List<SeriePuntoDto> SerieDeUnDia() =>
    [
        new() { Fecha = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc), Total = 3, Error = 1 }
    ];

    [Fact]
    public void CircuitoCerradoAlCrearElGrafico_NoPropagaLaExcepcion()
    {
        JSInterop.Setup<bool>("documentIaCharts.crear", _ => true)
            .SetException(new JSDisconnectedException("El circuito ya no esta disponible"));

        var render = () => RenderComponent<MonitorGraficos>(p => p.Add(c => c.Serie, SerieDeUnDia()));

        render.Should().NotThrow(
            "un circuito ya cerrado no debe convertirse en una pagina en error");
    }

    [Fact]
    public void ErrorDeLaLibreriaDeGraficos_NoPropagaLaExcepcion()
    {
        JSInterop.Setup<bool>("documentIaCharts.crear", _ => true)
            .SetException(new JSException("ApexCharts no pudo dibujar la serie"));

        var render = () => RenderComponent<MonitorGraficos>(p => p.Add(c => c.Serie, SerieDeUnDia()));

        render.Should().NotThrow(
            "un fallo dibujando el grafico no debe impedir ver el resto del Monitor");
    }

    [Fact]
    public void ErrorAlActualizar_NoPropagaLaExcepcion()
    {
        JSInterop.Setup<bool>("documentIaCharts.crear", _ => true).SetResult(true);
        JSInterop.SetupVoid("documentIaCharts.actualizar", _ => true)
            .SetException(new JSDisconnectedException("El circuito ya no esta disponible"));

        var cut = RenderComponent<MonitorGraficos>(p => p.Add(c => c.Serie, SerieDeUnDia()));

        var actualizar = () => cut.SetParametersAndRender(p => p.Add(c => c.Serie,
        [
            new SeriePuntoDto { Fecha = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc), Total = 9, Error = 2 }
        ]));

        actualizar.Should().NotThrow(
            "el auto-refresco actualiza el grafico sin intervencion del usuario: un fallo ahi no debe tumbar la pagina");
    }
}
