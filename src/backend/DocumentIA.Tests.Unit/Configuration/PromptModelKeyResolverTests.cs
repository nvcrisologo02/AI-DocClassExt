using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class PromptModelKeyResolverTests
{
    [Fact]
    public void Resolve_PeticionGana()
    {
        PromptModelKeyResolver.Resolve("prompt.x", "default.gpt4o-mini", "prompt.gpt56-luna")
            .Should().Be("prompt.x");
    }

    [Fact]
    public void Resolve_TipologiaSinPeticion()
    {
        PromptModelKeyResolver.Resolve(null, "prompt.y", "prompt.gpt56-luna")
            .Should().Be("prompt.y");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_TipologiaVaciaHereda(string tipologia)
    {
        PromptModelKeyResolver.Resolve(null, tipologia, "prompt.gpt56-luna")
            .Should().Be("prompt.gpt56-luna");
    }

    [Fact]
    public void Resolve_TodoVacio_Lanza()
    {
        var act = () => PromptModelKeyResolver.Resolve(null, null, "");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PromptDefaults:ModelKey*");
    }
}
