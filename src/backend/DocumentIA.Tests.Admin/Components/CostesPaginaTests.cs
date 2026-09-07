using System.Net;
using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using CostesPagina = DocumentIA.Admin.Components.Pages.Costes;
using DocumentIA.Tests.Admin.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Tests.Admin.Components;

/// <summary>
/// AB#100239: la seccion de costes carga sola al abrirse. Ningun fallo del backend
/// debe escapar del componente, y lo estimado debe presentarse siempre separado de
/// lo medido.
/// </summary>
public class CostesPaginaTests : TestContext
{
    public CostesPaginaTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private void RegistrarBackend(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        Services.AddSingleton(new MonitorService(new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://localhost/api/")
        }));
    }

    private const string CostesJson = """
    {
      "totalEjecuciones": 120, "periodoDias": 7,
      "conCosteReal": 100, "conCosteEstimado": 15, "sinCoste": 5,
      "incluyeEstimados": false, "ejecucionesConImporte": 100,
      "costeTotalEur": 12.5, "costeMedioEur": 0.125, "tokensTotales": 250000,
      "layoutEur": 11.0, "clasificacionEur": 1.5, "extraccionEur": 0, "promptEur": 0,
      "porTipologia": [ { "grupo": "NOTS", "total": 80, "conImporte": 70, "costeEur": 9.0, "costeMedioEur": 0.1286 } ],
      "porModelo": [], "serie": []
    }
    """;

    private const string ListadoJson = """
    { "items": [
        { "id": 1, "ejecucionGuid": "11111111-1111-1111-1111-111111111111", "fechaEjecucion": "2026-09-01T10:00:00Z",
          "tipologia": "NOTS", "tipoFlujo": "Clasificacion", "estadoFinal": "OK", "confianzaGlobal": 0.9,
          "duracionTotalMs": 1000, "costeIAEur": 0.1234, "costeEstimado": false },
        { "id": 2, "ejecucionGuid": "22222222-2222-2222-2222-222222222222", "fechaEjecucion": "2026-09-01T11:00:00Z",
          "tipologia": "NOTS", "tipoFlujo": "Clasificacion", "estadoFinal": "OK", "confianzaGlobal": 0.9,
          "duracionTotalMs": 1000, "costeIAEur": 0.3, "costeEstimado": true }
      ], "total": 2, "page": 1, "pageSize": 25 }
    """;

    private static HttpResponseMessage Responder(HttpRequestMessage req)
    {
        var ruta = req.RequestUri!.AbsolutePath;
        if (ruta.EndsWith("/costes")) return StubHttpMessageHandler.Json(CostesJson);
        if (ruta.EndsWith("/agregados")) return StubHttpMessageHandler.Json("""{"porTipologia":[]}""");
        if (ruta.EndsWith("/ejecuciones")) return StubHttpMessageHandler.Json(ListadoJson);
        return StubHttpMessageHandler.Json("{}", HttpStatusCode.NotFound);
    }

    [Fact]
    public void ConDatos_MuestraElTotalYSeparaMedidoDeEstimado()
    {
        RegistrarBackend(Responder);

        var cut = RenderComponent<CostesPagina>();

        cut.WaitForAssertion(() =>
        {
            var texto = cut.Markup;
            texto.Should().Contain("12,50 €", "el total del periodo encabeza la pagina");
            texto.Should().Contain("solo medido", "por defecto lo estimado no entra en el importe");
            // Recuentos por origen visibles aunque lo estimado no sume.
            var kpis = cut.FindComponent<CostesKpis>();
            kpis.Markup.Should().Contain("100").And.Contain("15").And.Contain("5");
        });
    }

    [Fact]
    public void Listado_MarcaLasEjecucionesConCosteEstimado()
    {
        RegistrarBackend(Responder);

        var cut = RenderComponent<CostesPagina>();

        cut.WaitForAssertion(() =>
        {
            var tabla = cut.FindComponent<MonitorTabla>();
            tabla.Instance.MostrarCoste.Should().BeTrue();
            tabla.Markup.Should().Contain("0,1234 €");
            // La estimada lleva marca visible; la medida no.
            tabla.Markup.Should().Contain("0,3000 € ≈");
            tabla.Markup.Should().NotContain("0,1234 € ≈");
        });
    }

    [Fact]
    public void Desglose_MuestraLasCuatroActividades()
    {
        RegistrarBackend(Responder);

        var cut = RenderComponent<CostesPagina>();

        cut.WaitForAssertion(() =>
        {
            var desglose = cut.FindComponent<CostesDesglose>();
            desglose.Markup.Should().Contain("Layout").And.Contain("Clasificación")
                .And.Contain("Extracción").And.Contain("Prompt");
            desglose.Markup.Should().Contain("11,00 €");
        });
    }

    [Fact]
    public void BackendQueDevuelve500_MuestraElErrorSinTumbarLaPagina()
    {
        RegistrarBackend(_ => StubHttpMessageHandler.Json("", HttpStatusCode.InternalServerError));

        var cut = RenderComponent<CostesPagina>();

        cut.WaitForAssertion(() =>
            cut.Find(".alert-danger").TextContent.Should().NotBeEmpty());
    }

    [Fact]
    public void BackendQueNoRespondeATiempo_MuestraElErrorSinTumbarLaPagina()
    {
        RegistrarBackend(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout", new TimeoutException()));

        var cut = RenderComponent<CostesPagina>();

        cut.WaitForAssertion(() =>
            cut.Find(".alert-danger").TextContent.Should().NotBeEmpty());
    }

    [Fact]
    public void SinAgregados_ElListadoSigueVisible()
    {
        // Un fallo en los agregados no debe impedir ver la tabla, igual que en el Monitor.
        RegistrarBackend(req =>
        {
            var ruta = req.RequestUri!.AbsolutePath;
            if (ruta.EndsWith("/costes")) return StubHttpMessageHandler.Json("", HttpStatusCode.InternalServerError);
            return Responder(req);
        });

        var cut = RenderComponent<CostesPagina>();

        cut.WaitForAssertion(() =>
        {
            cut.FindComponent<MonitorTabla>().Markup.Should().Contain("0,1234 €");
            cut.FindAll(".alert-danger").Should().BeEmpty();
        });
    }
}
