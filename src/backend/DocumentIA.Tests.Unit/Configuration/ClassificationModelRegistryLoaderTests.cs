#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class ClassificationModelRegistryLoaderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public ClassificationModelRegistryLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ClassificationModelRegistryLoaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _registryPath = Path.Combine(_tempDirectory, "models.json");
    }

    [Fact]
    public void Load_ValidRegistry_ReturnsModels()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""default.azure-di"",
                    ""provider"": ""azure-document-intelligence"",
                    ""classifierId"": ""classifier-demo"",
                    ""apiVersion"": ""2024-11-30""
                }
            ]
        }");

        var loader = new ClassificationModelRegistryLoader(_registryPath);

        var registry = loader.Load();

        registry.Models.Should().ContainSingle();
        registry.Models[0].ClassifierId.Should().Be("classifier-demo");
    }

    [Fact]
    public void GetModel_ExistingKey_ReturnsMatchingModel()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""default.azure-di"",
                    ""provider"": ""azure-document-intelligence"",
                    ""classifierId"": ""classifier-demo""
                }
            ]
        }");

        var loader = new ClassificationModelRegistryLoader(_registryPath);

        var model = loader.GetModel("default.azure-di");

        model.Provider.Should().Be("azure-document-intelligence");
        model.ClassifierId.Should().Be("classifier-demo");
    }

    [Fact]
    public void GetModel_UnknownKey_ThrowsKeyNotFoundException()
    {
        File.WriteAllText(_registryPath, @"{ ""models"": [] }");

        var loader = new ClassificationModelRegistryLoader(_registryPath);

        var action = () => loader.GetModel("missing");

        action.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetDefaultModel_ReturnsModelMarkedAsDefault()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""classification.gpt4o-mini-fallback"",
                    ""provider"": ""azure-openai"",
                    ""useAsFallback"": true,
                    ""deploymentName"": ""gpt-4o-mini""
                },
                {
                    ""key"": ""default.azure-di"",
                    ""provider"": ""azure-document-intelligence"",
                    ""isDefault"": true,
                    ""classifierId"": ""classifier-demo""
                }
            ]
        }");

        var loader = new ClassificationModelRegistryLoader(_registryPath);

        var model = loader.GetDefaultModel("azure-document-intelligence");

        model.Key.Should().Be("default.azure-di");
        model.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void GetFallbackModel_ReturnsModelMarkedForFallback()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""default.azure-di"",
                    ""provider"": ""azure-document-intelligence"",
                    ""isDefault"": true,
                    ""classifierId"": ""classifier-demo""
                },
                {
                    ""key"": ""classification.gpt4o-mini-fallback"",
                    ""provider"": ""azure-openai"",
                    ""useAsFallback"": true,
                    ""deploymentName"": ""gpt-4o-mini"",
                    ""fallbackThreshold"": 0.6
                }
            ]
        }");

        var loader = new ClassificationModelRegistryLoader(_registryPath);

        var model = loader.GetFallbackModel();

        model.Key.Should().Be("classification.gpt4o-mini-fallback");
        model.UseAsFallback.Should().BeTrue();
        model.FallbackThreshold.Should().Be(0.6);
    }

    private static AiEndpointResolver ResolverDePrueba() => new(new Dictionary<string, AiResourceEntry>
    {
        ["di"] = new() { Endpoint = "https://di-dev.example/" },
    });

    [Fact]
    public void Load_ModeloConAliasSinEndpoint_ResuelveEndpointDesdeElAlias()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""default.azure-di"", ""provider"": ""azure-document-intelligence"",
                  ""classifierId"": ""classifier-demo"", ""resourceAlias"": ""di"" }
            ]
        }");

        var loader = new ClassificationModelRegistryLoader(_registryPath, ResolverDePrueba());

        loader.GetModel("default.azure-di").Endpoint.Should().Be("https://di-dev.example/");
    }

    [Fact]
    public void Load_ModeloConEndpointExplicitoYAlias_ConservaElExplicito()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""default.azure-di"", ""provider"": ""azure-document-intelligence"",
                  ""endpoint"": ""https://explicito.example/"", ""resourceAlias"": ""di"" }
            ]
        }");

        var loader = new ClassificationModelRegistryLoader(_registryPath, ResolverDePrueba());

        loader.GetModel("default.azure-di").Endpoint.Should().Be("https://explicito.example/");
    }

    [Fact]
    public void Load_ModeloConAliasNoMapeado_LanzaAlCargarElRegistro()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""x"", ""provider"": ""azure-openai"", ""resourceAlias"": ""openai_primary"" }
            ]
        }");

        var loader = new ClassificationModelRegistryLoader(_registryPath, ResolverDePrueba());

        var act = () => loader.Load();

        act.Should().Throw<InvalidOperationException>().WithMessage("*openai_primary*");
    }

    [Fact]
    public void Load_SinResolvedor_ConservaComportamientoActual()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""default.azure-di"", ""provider"": ""azure-document-intelligence"",
                  ""endpoint"": ""https://pro.example/"" }
            ]
        }");

        var loader = new ClassificationModelRegistryLoader(_registryPath);

        loader.GetModel("default.azure-di").Endpoint.Should().Be("https://pro.example/");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
