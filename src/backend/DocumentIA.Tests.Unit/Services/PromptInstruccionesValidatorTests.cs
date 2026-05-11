using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class PromptInstruccionesValidatorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PromptInstruccionesValidator _validator;

    public PromptInstruccionesValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"documentia-prompt-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var registryPath = Path.Combine(_tempDir, "prompt.models.json");
        File.WriteAllText(
            registryPath,
            """
            {
              "models": [
                {
                  "key": "default.prompt",
                  "provider": "azure-openai",
                  "endpoint": "https://example.openai.azure.com",
                  "apiKey": "test-key",
                  "authMode": "ApiKey",
                  "deploymentName": "gpt-4o-mini",
                  "timeoutSeconds": 60
                }
              ]
            }
            """);

        var loader = new PromptModelRegistryLoader(registryPath);
        _validator = new PromptInstruccionesValidator(loader);
    }

    [Fact]
    public void TryValidate_NullPrompt_ReturnsTrue()
    {
        var ok = _validator.TryValidate(null, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_ValidPrompt_ReturnsTrue()
    {
        var ok = _validator.TryValidate(new PromptInstrucciones
        {
            SystemPrompt = "Eres un analista",
            UserPromptTemplate = "Resume {contenido}",
            ModelKey = "default.prompt",
            Temperature = 0.2,
            MaxTokens = 1000,
            ContentMode = "markdown"
        }, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_SystemPromptTooLong_ReturnsFalse()
    {
        var tooLong = new string('a', 5001);

        var ok = _validator.TryValidate(new PromptInstrucciones
        {
            SystemPrompt = tooLong
        }, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("systemPrompt");
    }

    [Fact]
    public void TryValidate_UserPromptTemplateTooLong_ReturnsFalse()
    {
        var tooLong = new string('b', 5001);

        var ok = _validator.TryValidate(new PromptInstrucciones
        {
            UserPromptTemplate = tooLong
        }, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("userPromptTemplate");
    }

    [Fact]
    public void TryValidate_TemperatureOutOfRange_ReturnsFalse()
    {
        var ok = _validator.TryValidate(new PromptInstrucciones
        {
            Temperature = 2.1
        }, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("temperature");
    }

    [Fact]
    public void TryValidate_MaxTokensOutOfRange_ReturnsFalse()
    {
        var ok = _validator.TryValidate(new PromptInstrucciones
        {
            MaxTokens = 10
        }, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("maxTokens");
    }

    [Fact]
    public void TryValidate_ContentModeInvalid_ReturnsFalse()
    {
        var ok = _validator.TryValidate(new PromptInstrucciones
        {
            ContentMode = "pdf"
        }, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("contentMode");
    }

    [Fact]
    public void TryValidate_ModelKeyUnknown_ReturnsFalse()
    {
        var ok = _validator.TryValidate(new PromptInstrucciones
        {
            ModelKey = "missing.model"
        }, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("modelKey");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
