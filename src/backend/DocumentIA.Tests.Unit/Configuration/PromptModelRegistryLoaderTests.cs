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

    private static AiEndpointResolver ResolverDePrueba() => new(new Dictionary<string, AiResourceEntry>
    {
        ["openai_primary"] = new() { Endpoint = "https://openai-dev.example/" },
    });

    [Fact]
    public void Load_ModeloConAliasSinEndpoint_ResuelveEndpointDesdeElAlias()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""prompt.gpt5-mini"", ""provider"": ""azure-openai"",
                  ""deploymentName"": ""gpt-5-mini"", ""resourceAlias"": ""openai_primary"" }
            ]
        }");

        var loader = new PromptModelRegistryLoader(_registryPath, ResolverDePrueba());

        loader.GetModel("prompt.gpt5-mini").Endpoint.Should().Be("https://openai-dev.example/");
    }

    [Fact]
    public void Load_ModeloConEndpointExplicitoYAlias_ConservaElExplicito()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""prompt.gpt5-mini"", ""provider"": ""azure-openai"",
                  ""endpoint"": ""https://explicito.example/"", ""resourceAlias"": ""openai_primary"" }
            ]
        }");

        var loader = new PromptModelRegistryLoader(_registryPath, ResolverDePrueba());

        loader.GetModel("prompt.gpt5-mini").Endpoint.Should().Be("https://explicito.example/");
    }

    [Fact]
    public void Load_ModeloConAliasNoMapeado_LanzaAlCargarElRegistro()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""x"", ""provider"": ""azure-document-intelligence"", ""resourceAlias"": ""di"" }
            ]
        }");

        var loader = new PromptModelRegistryLoader(_registryPath, ResolverDePrueba());

        var act = () => loader.Load();

        act.Should().Throw<InvalidOperationException>().WithMessage("*di*");
    }

    [Fact]
    public void Load_SinResolvedor_ConservaComportamientoActual()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""prompt.gpt5-mini"", ""provider"": ""azure-openai"",
                  ""endpoint"": ""https://pro.example/"" }
            ]
        }");

        var loader = new PromptModelRegistryLoader(_registryPath);

        loader.GetModel("prompt.gpt5-mini").Endpoint.Should().Be("https://pro.example/");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
