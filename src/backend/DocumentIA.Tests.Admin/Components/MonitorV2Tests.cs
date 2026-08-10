using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace DocumentIA.Tests.Admin.Components;

public class MonitorMatrizTests : TestContext
{
    private static List<MatrizCeldaDto> Matriz() =>
    [
        new() { EstadoProceso = "OK", Calidad = "OK", Total = 22140 },
        new() { EstadoProceso = "OK", Calidad = "REVISION", Total = 2980 },
        new() { EstadoProceso = "OK", Calidad = "ERROR", Total = 658 },
        new() { EstadoProceso = "VALIDACION_CON_ERRORES", Calidad = "REVISION", Total = 121 }
    ];

    // Los estados salen de los datos: un estado nuevo del orquestador debe
    // aparecer sin tocar la interfaz, que es justo lo que fallaba antes.
    [Fact]
    public void Estados_SeTomanDeLosDatosYNoDeUnaListaFija()
    {
        var cut = RenderComponent<MonitorMatriz>(p => p.Add(c => c.Celdas, Matriz()));

        cut.Markup.Should().Contain("VALIDACION_CON_ERRORES");
    }

    [Fact]
    public void Celda_AlPulsarlaEmiteElCruceDeProcesoYCalidad()
    {
        (string Proceso, string Calidad)? emitido = null;
        var cut = RenderComponent<MonitorMatriz>(p => p
            .Add(c => c.Celdas, Matriz())
            .Add(c => c.CeldaSeleccionada, (( string, string) v) => emitido = v));

        // La celda del cuadrante que antes era invisible: completado + revision.
        cut.FindAll("button").First(b => b.TextContent.Trim() == "2980").Click();

        emitido.Should().Be(("OK", "REVISION"));
    }

    [Fact]
    public void CeldaSinDatos_NoOfreceNavegacion()
    {
        var cut = RenderComponent<MonitorMatriz>(p => p.Add(c => c.Celdas, Matriz()));

        // VALIDACION_CON_ERRORES no tiene celda de calidad OK.
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "0");
    }

    [Fact]
    public void SinCeldas_MuestraMensajeDePeriodoVacio()
    {
        var cut = RenderComponent<MonitorMatriz>(p => p.Add(c => c.Celdas, []));

        cut.Markup.Should().Contain("Sin datos en el período");
    }

    [Fact]
    public void TotalDeFila_SumaLasTresCalidades()
    {
        var cut = RenderComponent<MonitorMatriz>(p => p.Add(c => c.Celdas, Matriz()));

        cut.Markup.Should().Contain("25778", "22140 + 2980 + 658");
    }
}

public class MonitorHistogramaTests : TestContext
{
    private static List<HistogramaBinDto> Bins() =>
    [
        new() { Desde = 0.60, Hasta = 0.65, Total = 186 },
        new() { Desde = 0.70, Hasta = 0.75, Total = 486 },
        new() { Desde = 0.90, Hasta = 0.95, Total = 8940 },
        new() { Desde = 0.95, Hasta = 1.01, Total = 0 }
    ];

    [Fact]
    public void Barra_AlPulsarlaEmiteElTramo()
    {
        (double Desde, double Hasta)? emitido = null;
        var cut = RenderComponent<MonitorHistograma>(p => p
            .Add(c => c.Bins, Bins())
            .Add(c => c.TramoSeleccionado, ((double, double) v) => emitido = v));

        cut.FindAll("button")[1].Click();

        emitido.Should().Be((0.70, 0.75));
    }

    [Fact]
    public void TramoVacio_NoEsNavegable()
    {
        var cut = RenderComponent<MonitorHistograma>(p => p.Add(c => c.Bins, Bins()));

        cut.FindAll("button").Last().HasAttribute("disabled").Should().BeTrue(
            "un tramo sin ejecuciones no lleva a ninguna parte");
    }

    [Fact]
    public void SinDatos_MuestraMensajeDePeriodoVacio()
    {
        var cut = RenderComponent<MonitorHistograma>(p => p.Add(c => c.Bins, []));

        cut.Markup.Should().Contain("Sin datos en el período");
    }
}

public class MonitorLecturasTests : TestContext
{
    [Fact]
    public void Proceso_MuestraTodosLosEstadosIncluidosLosNoContempladosAntes()
    {
        var cut = RenderComponent<MonitorLecturas>(p => p
            .Add(c => c.Proceso,
            [
                new AgregadoGrupoDto { Grupo = "OK", Total = 25778 },
                new AgregadoGrupoDto { Grupo = "VALIDACION_CON_ERRORES", Total = 268 },
                new AgregadoGrupoDto { Grupo = "PAGINAS_EXCEDIDAS", Total = 2 }
            ]));

        cut.Markup.Should().Contain("VALIDACION_CON_ERRORES").And.Contain("PAGINAS_EXCEDIDAS");
    }

    // El ancho se interpola dentro de un atributo style. Con la cultura espanola
    // un double sale con coma ("width:99,7%"), que el navegador descarta por
    // invalida: la barra desaparecia y solo quedaba la leyenda.
    [Fact]
    public void Barras_ElAnchoUsaPuntoDecimalParaQueElNavegadorLoAcepte()
    {
        var cut = RenderComponent<MonitorLecturas>(p => p
            .Add(c => c.Proceso,
            [
                new AgregadoGrupoDto { Grupo = "OK", Total = 1722 },
                new AgregadoGrupoDto { Grupo = "VALIDACION_CON_ERRORES", Total = 5 }
            ]));

        var anchos = Regex.Matches(cut.Markup, @"width:([^;%]+)%")
            .Select(m => m.Groups[1].Value).ToList();

        anchos.Should().NotBeEmpty();
        anchos.Should().OnlyContain(a => !a.Contains(","),
            "una coma decimal invalida la regla CSS y la barra no se pinta");
    }

    [Fact]
    public void Barras_ElAnchoNoArrastraDecimalesInterminables()
    {
        var cut = RenderComponent<MonitorLecturas>(p => p
            .Add(c => c.CalidadOk, 1672)
            .Add(c => c.CalidadRevision, 4)
            .Add(c => c.CalidadError, 51));

        var anchos = Regex.Matches(cut.Markup, @"width:([\d.]+)%")
            .Select(m => m.Groups[1].Value).ToList();

        anchos.Should().NotBeEmpty();
        anchos.Should().OnlyContain(a => !a.Contains(".") || a.Split(new[]{'.'})[1].Length <= 2);
    }

    [Fact]
    public void Calidad_AlPulsarUnTramoEmiteSuClave()
    {
        string? emitido = null;
        var cut = RenderComponent<MonitorLecturas>(p => p
            .Add(c => c.CalidadOk, 100)
            .Add(c => c.CalidadRevision, 30)
            .Add(c => c.CalidadError, 5)
            .Add(c => c.CalidadSeleccionada, (string v) => emitido = v));

        // Los tres primeros botones son los tramos de proceso (vacio) o calidad.
        cut.FindAll("button")[1].Click();

        emitido.Should().Be("REVISION");
    }
}

// La pagina completa: mismo blindaje que la original, porque comparte el mismo
// riesgo de tumbar el circuito si un fallo del backend escapa del componente.
public class MonitorV2PaginaTests : TestContext
{
    public MonitorV2PaginaTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private void RegistrarBackend(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        Services.AddSingleton(new MonitorService(
            new HttpClient(new DocumentIA.Tests.Admin.Helpers.StubHttpMessageHandler(responder))
            {
                BaseAddress = new Uri("http://localhost/api/")
            }));

    [Fact]
    public void BackendQueNoRespondeATiempo_MuestraElErrorSinTumbarLaPagina()
    {
        RegistrarBackend(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout", new TimeoutException()));

        var cut = RenderComponent<DocumentIA.Admin.Components.Pages.MonitorV2>();

        cut.Find(".alert-danger").TextContent.Should().NotBeEmpty();
    }

    [Fact]
    public void BackendConJsonInesperado_NoTumbaLaPagina()
    {
        RegistrarBackend(_ => DocumentIA.Tests.Admin.Helpers.StubHttpMessageHandler.Json("[]"));

        var render = () => RenderComponent<DocumentIA.Admin.Components.Pages.MonitorV2>();

        render.Should().NotThrow();
    }
}
