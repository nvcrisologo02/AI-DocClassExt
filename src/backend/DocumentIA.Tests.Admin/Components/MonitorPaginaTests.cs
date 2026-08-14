using System.Net;
using Bunit;
using DocumentIA.Admin.Services;
using MonitorPagina = DocumentIA.Admin.Components.Pages.Monitor;
using DocumentIA.Tests.Admin.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Tests.Admin.Components;

// La pagina del Monitor carga sola al abrirse y se recarga sola cada 30 s. Ningun
// fallo del backend en esas rutas debe escapar del componente: en Blazor Server una
// excepcion no capturada termina el circuito y el usuario ve la pantalla en error
// ("Server returned an error on close") en vez del mensaje de fallo de la pagina.
public class MonitorPaginaTests : TestContext
{
    public MonitorPaginaTests()
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

    [Fact]
    public void BackendQueNoRespondeATiempo_MuestraElErrorEnLaPaginaSinTumbarla()
    {
        RegistrarBackend(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout", new TimeoutException()));

        var cut = RenderComponent<MonitorPagina>();

        // Sin traducir el timeout, la excepcion escapa de la carga inicial y deja la
        // pagina muda: sin datos y sin explicacion de por que.
        cut.Find(".alert-danger").TextContent.Should().NotBeEmpty(
            "un timeout del backend debe degradarse a un mensaje de error visible");
    }

    [Fact]
    public void BackendQueDevuelve500_MuestraElErrorEnLaPaginaSinTumbarla()
    {
        RegistrarBackend(_ => StubHttpMessageHandler.Json("", HttpStatusCode.InternalServerError));

        var cut = RenderComponent<MonitorPagina>();

        cut.Find(".alert-danger").TextContent.Should().NotBeEmpty(
            "el fallo del backend debe explicarse en la propia pagina");
    }

    [Fact]
    public void BackendQueDevuelveJsonConFormaInesperada_NoTumbaLaPagina()
    {
        // Admin y Functions se despliegan por separado: durante una ventana de
        // despliegue el contrato puede no coincidir.
        RegistrarBackend(_ => StubHttpMessageHandler.Json("[]"));

        var render = () => RenderComponent<MonitorPagina>();

        render.Should().NotThrow();
    }
}
