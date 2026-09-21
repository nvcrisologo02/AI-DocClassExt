#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class AiEndpointResolverTests
{
    private static AiEndpointResolver Resolver() => new(new Dictionary<string, AiResourceEntry>
    {
        ["cu_primary"] = new() { Endpoint = "https://cu-primary.example/" },
        ["di"] = new() { Endpoint = "https://di.example/" },
    });

    [Fact]
    public void Resolve_ConEndpointExplicito_DevuelveElExplicito()
    {
        Resolver().Resolve("https://explicito.example/", "cu_primary", "m1")
            .Should().Be("https://explicito.example/");
    }

    [Fact]
    public void Resolve_SinEndpointConAliasMapeado_DevuelveElDelAlias()
    {
        Resolver().Resolve(null, "cu_primary", "m1").Should().Be("https://cu-primary.example/");
        Resolver().Resolve("", "CU_PRIMARY", "m1").Should().Be("https://cu-primary.example/");
    }

    [Fact]
    public void Resolve_SinEndpointNiAlias_DevuelveVacio()
    {
        Resolver().Resolve(null, null, "m1").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_AliasSinMapa_LanzaConMensajeUtil()
    {
        var act = () => Resolver().Resolve(null, "openai_primary", "classification.gpt5-mini");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*classification.gpt5-mini*openai_primary*AI__Resources__openai_primary__Endpoint*");
    }

    [Fact]
    public void Empty_SoloRespetaEndpointsExplicitos()
    {
        AiEndpointResolver.Empty.Resolve("https://x.example/", null, "m1").Should().Be("https://x.example/");
        AiEndpointResolver.Empty.Resolve(null, null, "m1").Should().BeEmpty();
    }
}
