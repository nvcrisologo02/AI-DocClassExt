#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class PromptPlantillaHashTests
{
    [Fact]
    public void Calcular_SinPrompt_DevuelveNull()
    {
        PromptPlantillaHash.Calcular(null).Should().BeNull();
    }

    [Fact]
    public void Calcular_MismaPlantillaYModelo_MismoHash()
    {
        var a = new PromptInstrucciones { SystemPrompt = "s", UserPromptTemplate = "u {{CONTENT}}", ModelKey = "m" };
        var b = new PromptInstrucciones { SystemPrompt = "s", UserPromptTemplate = "u {{CONTENT}}", ModelKey = "m", Temperature = 0.3 };

        PromptPlantillaHash.Calcular(a).Should().Be(PromptPlantillaHash.Calcular(b)).And.HaveLength(16);
    }

    [Fact]
    public void Calcular_PlantillaOModeloDistintos_HashDistinto()
    {
        var a = new PromptInstrucciones { SystemPrompt = "s", UserPromptTemplate = "u", ModelKey = "m" };
        var b = new PromptInstrucciones { SystemPrompt = "s", UserPromptTemplate = "u2", ModelKey = "m" };
        var c = new PromptInstrucciones { SystemPrompt = "s", UserPromptTemplate = "u", ModelKey = "m2" };

        PromptPlantillaHash.Calcular(a).Should().NotBe(PromptPlantillaHash.Calcular(b));
        PromptPlantillaHash.Calcular(a).Should().NotBe(PromptPlantillaHash.Calcular(c));
    }
}
