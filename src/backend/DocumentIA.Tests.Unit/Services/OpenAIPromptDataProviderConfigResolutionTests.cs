using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class OpenAIPromptDataProviderConfigResolutionTests
{
    [Fact]
    public void ResolvePromptConfig_NoOverride_ReturnsTipologiaConfig()
    {
        var tipologia = new PromptConfig
        {
            Enabled = true,
            ModelKey = "tipologia.model",
            SystemPrompt = "sys",
            UserPromptTemplate = "template",
            MaxTokens = 1200,
            Temperature = 0.1,
            ContentMode = "markdown"
        };

        var result = OpenAIPromptDataProvider.ResolvePromptConfig(tipologia, null);

        result.Should().NotBeNull();
        result!.ModelKey.Should().Be("tipologia.model");
        result.SystemPrompt.Should().Be("sys");
        result.UserPromptTemplate.Should().Be("template");
        result.MaxTokens.Should().Be(1200);
        result.Temperature.Should().Be(0.1);
        result.ContentMode.Should().Be("markdown");
    }

    [Fact]
    public void ResolvePromptConfig_WithFullOverride_PrioritizesRequest()
    {
        var tipologia = new PromptConfig
        {
            Enabled = true,
            ModelKey = "tipologia.model",
            SystemPrompt = "tipologia.sys",
            UserPromptTemplate = "tipologia.template",
            MaxTokens = 1200,
            Temperature = 0.1,
            ContentMode = "markdown"
        };

        var result = OpenAIPromptDataProvider.ResolvePromptConfig(tipologia, new PromptInstrucciones
        {
            ModelKey = "request.model",
            SystemPrompt = "request.sys",
            UserPromptTemplate = "request.template",
            MaxTokens = 1800,
            Temperature = 0.3,
            ContentMode = "vision"
        });

        result.Should().NotBeNull();
        result!.ModelKey.Should().Be("request.model");
        result.SystemPrompt.Should().Be("request.sys");
        result.UserPromptTemplate.Should().Be("request.template");
        result.MaxTokens.Should().Be(1800);
        result.Temperature.Should().Be(0.3);
        result.ContentMode.Should().Be("vision");
    }

    [Fact]
    public void ResolvePromptConfig_WithPartialOverride_UsesFallbackForMissingFields()
    {
        var tipologia = new PromptConfig
        {
            Enabled = true,
            ModelKey = "tipologia.model",
            SystemPrompt = "tipologia.sys",
            UserPromptTemplate = "tipologia.template",
            MaxTokens = 1400,
            Temperature = 0.2,
            ContentMode = "markdown"
        };

        var result = OpenAIPromptDataProvider.ResolvePromptConfig(tipologia, new PromptInstrucciones
        {
            UserPromptTemplate = "request.template"
        });

        result.Should().NotBeNull();
        result!.ModelKey.Should().Be("tipologia.model");
        result.SystemPrompt.Should().Be("tipologia.sys");
        result.UserPromptTemplate.Should().Be("request.template");
        result.MaxTokens.Should().Be(1400);
        result.Temperature.Should().Be(0.2);
        result.ContentMode.Should().Be("markdown");
    }

    [Fact]
    public void ResolvePromptConfig_EnabledTipologiaWithEmptyPrompt_UsesDefaultPromptDefinition()
    {
        var tipologia = new PromptConfig
        {
            Enabled = true,
            ModelKey = "default.gpt4o-mini",
            SystemPrompt = string.Empty,
            UserPromptTemplate = string.Empty,
            MaxTokens = 0,
            Temperature = 0.0,
            ContentMode = string.Empty
        };

        var defaults = new PromptConfig
        {
            Enabled = true,
            ModelKey = "default.gpt4o-mini",
            SystemPrompt = "default.sys",
            UserPromptTemplate = "default.template {contenido}",
            MaxTokens = 1600,
            Temperature = 0.0,
            ContentMode = "markdown"
        };

        var result = OpenAIPromptDataProvider.ResolvePromptConfig(tipologia, null, defaults);

        result.Should().NotBeNull();
        result!.Enabled.Should().BeTrue();
        result.SystemPrompt.Should().Be("default.sys");
        result.UserPromptTemplate.Should().Be("default.template {contenido}");
        result.MaxTokens.Should().Be(1600);
        result.ContentMode.Should().Be("markdown");
    }

    [Fact]
    public void ResolvePromptConfig_NullTipologiaConfig_NullRequest_ReturnsNull()
    {
        var result = OpenAIPromptDataProvider.ResolvePromptConfig(null, null);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolvePromptConfig_NullTipologiaConfig_WithRequestPrompt_CreatesAdHocConfig()
    {
        // Tipología sin PromptConfig pero la petición trae instrucciones completas (prompt ad-hoc)
        var result = OpenAIPromptDataProvider.ResolvePromptConfig(null, new PromptInstrucciones
        {
            ModelKey = "adhoc.model",
            SystemPrompt = "adhoc.sys",
            UserPromptTemplate = "adhoc.template {contenido}",
            MaxTokens = 1500,
            Temperature = 0.5,
            ContentMode = "vision"
        });

        result.Should().NotBeNull();
        result!.Enabled.Should().BeTrue();
        result.ModelKey.Should().Be("adhoc.model");
        result.SystemPrompt.Should().Be("adhoc.sys");
        result.UserPromptTemplate.Should().Be("adhoc.template {contenido}");
        result.MaxTokens.Should().Be(1500);
        result.Temperature.Should().Be(0.5);
        result.ContentMode.Should().Be("vision");
    }

    [Fact]
    public void ResolvePromptConfig_NullTipologiaConfig_RequestWithDefaults_UsesSystemDefaults()
    {
        // Request mínimo: solo ModelKey y UserPromptTemplate; los demás usan defaults del sistema
        var result = OpenAIPromptDataProvider.ResolvePromptConfig(null, new PromptInstrucciones
        {
            ModelKey = "adhoc.model",
            UserPromptTemplate = "Resume: {contenido}"
        });

        result.Should().NotBeNull();
        result!.Enabled.Should().BeTrue();
        result.ModelKey.Should().Be("adhoc.model");
        result.UserPromptTemplate.Should().Be("Resume: {contenido}");
        result.MaxTokens.Should().Be(2000);
        result.Temperature.Should().Be(0.0);
        result.ContentMode.Should().Be("markdown");
    }
}
