using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

public class EnvironmentBannerStateTests
{
    [Fact]
    public void Resolve_SinCargar_MuestraEstadoDeConexion()
    {
        var state = EnvironmentBannerState.Resolve(loaded: false, backendUnreachable: false, environment: null);

        state.Label.Should().Contain("Conectando");
        state.CssClass.Should().Contain("bg-light");
    }

    [Fact]
    public void Resolve_BackendInaccesible_Advierte()
    {
        var state = EnvironmentBannerState.Resolve(loaded: true, backendUnreachable: true, environment: null);

        state.Label.Should().Contain("no accesible");
        state.CssClass.Should().Contain("bg-warning");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    public void Resolve_Produccion_AvisaEnRojo(string environment)
    {
        var state = EnvironmentBannerState.Resolve(loaded: true, backendUnreachable: false, environment);

        state.Label.Should().Contain("PRODUCCIÓN");
        state.CssClass.Should().Contain("bg-danger");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    [InlineData("unknown")]
    public void Resolve_EntornoSinIdentificar_AdvierteNoPareceNormal(string? environment)
    {
        var state = EnvironmentBannerState.Resolve(loaded: true, backendUnreachable: false, environment);

        state.Label.Should().Contain("sin identificar");
        state.CssClass.Should().Contain("bg-warning");
        state.CssClass.Should().NotContain("bg-info", "un entorno desconocido no puede presentarse como estado normal");
    }

    [Fact]
    public void Resolve_EntornoConocidoNoProductivo_MuestraElNombre()
    {
        var state = EnvironmentBannerState.Resolve(loaded: true, backendUnreachable: false, environment: "Development");

        state.Label.Should().Be("Entorno backend: Development");
        state.CssClass.Should().Contain("bg-info");
    }
}
