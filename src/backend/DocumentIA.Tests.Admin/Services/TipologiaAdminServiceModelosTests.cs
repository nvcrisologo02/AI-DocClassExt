using System.Net;
using DocumentIA.Admin.Services;
using DocumentIA.Tests.Admin.Helpers;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

public class TipologiaAdminServiceModelosTests
{
    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public string UserName => "test-user";

        public bool IsAuthenticated => true;
    }

    private static TipologiaAdminService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
        return new TipologiaAdminService(client, new FakeCurrentUser());
    }

    private static HttpResponseMessage RouteModelos(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/modelos/layout"))
        {
            return StubHttpMessageHandler.Json(
                """[{"id":5,"tipo":"Layout","key":"di-layout-default","provider":"DI","configuracionJson":"{}","activo":true}]""");
        }

        if (path.Contains("/modelos/"))
        {
            return StubHttpMessageHandler.Json("[]");
        }

        return StubHttpMessageHandler.Json("null", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetModeloByIdAsync_ModeloTipoLayout_SeResuelvePorId()
    {
        var service = CreateService(RouteModelos);

        var modelo = await service.GetModeloByIdAsync(5);

        modelo.Should().NotBeNull("un modelo de tipo Layout debe poder editarse por id");
        modelo!.Key.Should().Be("di-layout-default");
    }

    [Fact]
    public async Task GetModeloByIdAsync_IdInexistente_DevuelveNull()
    {
        var service = CreateService(RouteModelos);

        var modelo = await service.GetModeloByIdAsync(999);

        modelo.Should().BeNull();
    }
}
