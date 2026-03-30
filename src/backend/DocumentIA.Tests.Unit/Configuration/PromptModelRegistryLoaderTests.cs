#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class PromptModelRegistryLoaderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public PromptModelRegistryLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PromptModelRegistryLoaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _registryPath = Path.Combine(_tempDirectory, "models.json");
    }

    [Fact]
    public void Load_ValidRegistry_ReturnsModels()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""default.gpt4o-mini"",
                    ""provider"": ""azure-openai"",
                    ""endpoint"": ""https://example.openai.azure.com"",
                    ""apiKey"": ""test"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""gpt-4o-mini"",
                    ""timeoutSeconds"": 60
                }
            ]
        }");

        var loader = new PromptModelRegistryLoader(_registryPath);

        var registry = loader.Load();

        registry.Models.Should().ContainSingle();
        registry.Models[0].DeploymentName.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void GetModel_ExistingKey_ReturnsMatchingModel()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""default.gpt4o-mini"",
                    ""provider"": ""azure-openai"",
                    ""endpoint"": ""https://example.openai.azure.com"",
                    ""apiKey"": ""test"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""gpt-4o-mini"",
                    ""timeoutSeconds"": 60
                }
            ]
        }");

        var loader = new PromptModelRegistryLoader(_registryPath);

        var model = loader.GetModel("default.gpt4o-mini");

        model.Provider.Should().Be("azure-openai");
        model.DeploymentName.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void GetModel_UnknownKey_ThrowsKeyNotFoundException()
    {
        File.WriteAllText(_registryPath, @"{ ""models"": [] }");

        var loader = new PromptModelRegistryLoader(_registryPath);

        var action = () => loader.GetModel("missing");

        action.Should().Throw<KeyNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
