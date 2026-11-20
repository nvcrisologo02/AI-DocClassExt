#nullable enable

using System.Text.Json;
using DocumentIA.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace DocumentIA.Tests.Unit.Configuration;

/// <summary>
/// Deserialización de la propiedad ReasoningEffort en las configuraciones de modelo,
/// con las mismas opciones que usan los loaders.
/// </summary>
public class ModelConfigReasoningEffortTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void ClassificationModelConfig_SinReasoningEffort_DeserializaNulo()
    {
        var json = """{"Key":"classification.gpt5-mini","DeploymentName":"gpt-5-mini","MaxTokens":2000}""";

        var config = JsonSerializer.Deserialize<ClassificationModelConfig>(json, Opciones);

        config.Should().NotBeNull();
        config!.ReasoningEffort.Should().BeNull();
        config.MaxTokens.Should().Be(2000);
    }

    [Fact]
    public void ClassificationModelConfig_ConReasoningEffort_DeserializaValor()
    {
        var json = """{"Key":"classification.gpt56-luna","DeploymentName":"gpt-5.6-luna","MaxTokens":2000,"ReasoningEffort":"none"}""";

        var config = JsonSerializer.Deserialize<ClassificationModelConfig>(json, Opciones);

        config!.ReasoningEffort.Should().Be("none");
    }

    [Fact]
    public void ClassificationModelConfig_ReasoningEffortEnMinusculas_DeserializaValor()
    {
        var json = """{"Key":"classification.gpt56-luna","reasoningEffort":"low"}""";

        var config = JsonSerializer.Deserialize<ClassificationModelConfig>(json, Opciones);

        config!.ReasoningEffort.Should().Be("low");
    }

    [Fact]
    public void PromptModelConfig_SinReasoningEffort_DeserializaNulo()
    {
        var json = """{"Key":"prompt.gpt5-mini","DeploymentName":"gpt-5-mini","TimeoutSeconds":60}""";

        var config = JsonSerializer.Deserialize<PromptModelConfig>(json, Opciones);

        config.Should().NotBeNull();
        config!.ReasoningEffort.Should().BeNull();
        config.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void PromptModelConfig_ConReasoningEffort_DeserializaValor()
    {
        var json = """{"Key":"prompt.gpt56-luna","DeploymentName":"gpt-5.6-luna","ReasoningEffort":"none"}""";

        var config = JsonSerializer.Deserialize<PromptModelConfig>(json, Opciones);

        config!.ReasoningEffort.Should().Be("none");
    }

    [Fact]
    public void PromptModelConfig_ReasoningEffortEnMinusculas_DeserializaValor()
    {
        var json = """{"Key":"prompt.gpt56-luna","reasoningEffort":"low"}""";

        var config = JsonSerializer.Deserialize<PromptModelConfig>(json, Opciones);

        config!.ReasoningEffort.Should().Be("low");
    }
}
