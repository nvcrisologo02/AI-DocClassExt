#nullable enable
using System.Text.RegularExpressions;
using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class PromptCacheKeyBuilderTests
{
    [Fact]
    public void Build_MismoSystemPrompt_MismaClave()
    {
        var a = PromptCacheKeyBuilder.Build("documentia-clasif", "phase1", "Eres un clasificador.");
        var b = PromptCacheKeyBuilder.Build("documentia-clasif", "phase1", "Eres un clasificador.");

        a.Should().Be(b);
        Regex.IsMatch(a, "^documentia-clasif:phase1:[0-9a-f]{12}$").Should().BeTrue();
    }

    [Fact]
    public void Build_SystemPromptDistinto_ClaveDistinta()
    {
        var a = PromptCacheKeyBuilder.Build("documentia-clasif", "phase1", "Eres un clasificador.");
        var b = PromptCacheKeyBuilder.Build("documentia-clasif", "phase1", "Eres un clasificador,");

        a.Should().NotBe(b);
    }
}
