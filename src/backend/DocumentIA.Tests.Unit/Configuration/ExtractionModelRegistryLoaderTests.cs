#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class ExtractionModelRegistryLoaderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public ExtractionModelRegistryLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ExtractionModelRegistryLoaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _registryPath = Path.Combine(_tempDirectory, "models.json");
    }

    [Fact]
    public void Load_ValidRegistry_ReturnsModels()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""nota.simple.1_4.azure-cu"",
                    ""provider"": ""azure-content-understanding"",
                    ""analyzerId"": ""nota-simple-1-4"",
                    ""contentType"": ""application/pdf""
                }
            ]
        }");

        var loader = new ExtractionModelRegistryLoader(_registryPath);

        var registry = loader.Load();

        registry.Models.Should().ContainSingle();
        registry.Models[0].AnalyzerId.Should().Be("nota-simple-1-4");
    }

    [Fact]
    public void GetModel_ExistingKey_ReturnsMatchingModel()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""nota.simple.1_4.azure-cu"",
                    ""provider"": ""azure-content-understanding"",
                    ""analyzerId"": ""nota-simple-1-4""
                }
            ]
        }");

        var loader = new ExtractionModelRegistryLoader(_registryPath);

        var model = loader.GetModel("nota.simple.1_4.azure-cu");

        model.Provider.Should().Be("azure-content-understanding");
        model.AnalyzerId.Should().Be("nota-simple-1-4");
    }

    [Fact]
    public void GetModel_UnknownKey_ThrowsKeyNotFoundException()
    {
        File.WriteAllText(_registryPath, @"{ ""models"": [] }");

        var loader = new ExtractionModelRegistryLoader(_registryPath);

        var action = () => loader.GetModel("missing");

        action.Should().Throw<KeyNotFoundException>();
    }

    private static AiEndpointResolver ResolverDePrueba() => new(new Dictionary<string, AiResourceEntry>
    {
        ["cu_primary"] = new() { Endpoint = "https://cu-dev.example/" },
    });

    [Fact]
    public void Load_ModeloConAliasSinEndpoint_ResuelveEndpointDesdeElAlias()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""nota.simple.1_4.azure-cu"", ""provider"": ""azure-content-understanding"",
                  ""analyzerId"": ""nota-simple-1-4"", ""resourceAlias"": ""cu_primary"" }
            ]
        }");

        var loader = new ExtractionModelRegistryLoader(_registryPath, ResolverDePrueba());

        loader.GetModel("nota.simple.1_4.azure-cu").Endpoint.Should().Be("https://cu-dev.example/");
    }

    [Fact]
    public void Load_ModeloConEndpointExplicitoYAlias_ConservaElExplicito()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""nota.simple.1_4.azure-cu"", ""provider"": ""azure-content-understanding"",
                  ""endpoint"": ""https://explicito.example/"", ""resourceAlias"": ""cu_primary"" }
            ]
        }");

        var loader = new ExtractionModelRegistryLoader(_registryPath, ResolverDePrueba());

        loader.GetModel("nota.simple.1_4.azure-cu").Endpoint.Should().Be("https://explicito.example/");
    }

    [Fact]
    public void Load_ModeloConAliasNoMapeado_LanzaAlCargarElRegistro()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""x"", ""provider"": ""azure-openai"", ""resourceAlias"": ""openai_primary"" }
            ]
        }");

        var loader = new ExtractionModelRegistryLoader(_registryPath, ResolverDePrueba());

        var act = () => loader.Load();

        act.Should().Throw<InvalidOperationException>().WithMessage("*openai_primary*");
    }

    [Fact]
    public void Load_SinResolvedor_ConservaComportamientoActual()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""nota.simple.1_4.azure-cu"", ""provider"": ""azure-content-understanding"",
                  ""endpoint"": ""https://pro.example/"" }
            ]
        }");

        var loader = new ExtractionModelRegistryLoader(_registryPath);

        loader.GetModel("nota.simple.1_4.azure-cu").Endpoint.Should().Be("https://pro.example/");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
