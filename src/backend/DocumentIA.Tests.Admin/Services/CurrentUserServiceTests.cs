using DocumentIA.Admin.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace DocumentIA.Tests.Admin.Services;

public class CurrentUserServiceTests
{
    [Fact]
    public void Resolve_ConHeaderEasyAuth_DevuelveElNombre()
    {
        CurrentUserService.Resolve("usuario@sareb.es", isDevelopment: false)
            .Should().Be("usuario@sareb.es");
    }

    [Fact]
    public void Resolve_SinHeaderEnDesarrollo_DevuelveUsuarioLocalConPrefijo()
    {
        var resolved = CurrentUserService.Resolve(null, isDevelopment: true);

        resolved.Should().StartWith("dev-").And.NotBe("dev-");
    }

    [Fact]
    public void Resolve_SinHeaderFueraDeDesarrollo_DevuelveNoAutenticado()
    {
        CurrentUserService.Resolve("  ", isDevelopment: false)
            .Should().Be("no-autenticado");
    }

    [Fact]
    public void UserName_HeaderPresenteSoloEnConstruccion_UsaElValorCapturado()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CurrentUserService.PrincipalNameHeader] = "usuario@sareb.es";
        var accessor = new FakeHttpContextAccessor(context);
        var service = new CurrentUserService(accessor, new FakeEnvironment("Production"));

        accessor.HttpContext = null; // el circuito pierde el HttpContext tras el render inicial

        service.UserName.Should().Be("usuario@sareb.es");
    }

    [Fact]
    public void UserName_SinHttpContextNunca_FueraDeDev_DevuelveNoAutenticado()
    {
        var accessor = new FakeHttpContextAccessor(null);
        var service = new CurrentUserService(accessor, new FakeEnvironment("Production"));

        service.UserName.Should().Be("no-autenticado");
    }

    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public FakeHttpContextAccessor(HttpContext? httpContext)
        {
            HttpContext = httpContext;
        }

        public HttpContext? HttpContext { get; set; }
    }

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public FakeEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
            ApplicationName = "DocumentIA.Admin.Tests";
            ContentRootPath = System.AppContext.BaseDirectory;
            WebRootPath = System.AppContext.BaseDirectory;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public string WebRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
