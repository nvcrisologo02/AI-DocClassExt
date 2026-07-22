using DocumentIA.Admin.Services;
using FluentAssertions;

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
}
