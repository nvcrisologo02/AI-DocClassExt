using System.Net;
using DocumentIA.Admin.Services;
using DocumentIA.Tests.Admin.Helpers;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

public class MonitorServiceTests
{
    private static MonitorService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
        return new MonitorService(client);
    }

    [Fact]
    public async Task GetEjecucionDetalleAsync_GuidInexistente404_DevuelveNull()
    {
        var service = CreateService(_ => StubHttpMessageHandler.Json("", HttpStatusCode.NotFound));

        var detalle = await service.GetEjecucionDetalleAsync(Guid.NewGuid().ToString());

        detalle.Should().BeNull("un 404 significa que no existe ninguna ejecucion con ese identificador, no un fallo de comunicacion");
    }

    [Fact]
    public async Task GetEjecucionDetalleAsync_ErrorServidor500_LanzaInvalidOperationException()
    {
        var service = CreateService(_ => StubHttpMessageHandler.Json("", HttpStatusCode.InternalServerError));

        var accion = async () => await service.GetEjecucionDetalleAsync(Guid.NewGuid().ToString());

        await accion.Should().ThrowAsync<InvalidOperationException>("un fallo de comunicacion distinto de 404 debe seguir reportandose como error tecnico");
    }
}
