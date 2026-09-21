#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Tests.Unit.Configuration;

/// <summary>
/// Prueba la ruta real Configuration -> IOptions -> DI -> resolvedor, replicando exactamente
/// el registro que hace Program.cs (AB#100305). Las demas pruebas de AiEndpointResolver
/// construyen el resolvedor desde un diccionario literal y no cubren el binding real.
/// </summary>
public class AiEndpointResolverBindingTests
{
    private static IAiEndpointResolver BuildResolverFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Resources:di:Endpoint"] = "https://di.example/",
                ["AI:Resources:cu_primary:Endpoint"] = "",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<AiResourceMapOptions>(configuration.GetSection(AiResourceMapOptions.SectionName));
        services.AddSingleton<IAiEndpointResolver, AiEndpointResolver>();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IAiEndpointResolver>();
    }

    [Fact]
    public void Resolve_AliasMapeadoBusquedaCaseInsensitive_DevuelveElEndpointDelBinding()
    {
        var resolver = BuildResolverFromConfiguration();

        resolver.Resolve(null, "DI", "k").Should().Be("https://di.example/");
    }

    [Fact]
    public void Resolve_ConEndpointExplicitoYAliasMapeado_DevuelveElExplicito()
    {
        var resolver = BuildResolverFromConfiguration();

        resolver.Resolve("https://x.example/", "di", "k").Should().Be("https://x.example/");
    }

    [Fact]
    public void Resolve_AliasConAppSettingVacio_CuentaComoNoMapeadoYLanza()
    {
        var resolver = BuildResolverFromConfiguration();

        var act = () => resolver.Resolve(null, "cu_primary", "k");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_SinEndpointNiAlias_DevuelveVacio()
    {
        var resolver = BuildResolverFromConfiguration();

        resolver.Resolve(null, null, "k").Should().BeEmpty();
    }
}
