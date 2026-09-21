#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class LayoutModelRegistryLoaderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public LayoutModelRegistryLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"LayoutModelRegistryLoaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _registryPath = Path.Combine(_tempDirectory, "models.json");
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
                { ""key"": ""layout.prebuilt-layout"", ""provider"": ""azure-document-intelligence-layout"",
                  ""resourceAlias"": ""di"" }
            ]
        }");

        var loader = new LayoutModelRegistryLoader(_registryPath, ResolverDePrueba());

        loader.GetDefaultModel().Endpoint.Should().Be("https://di-dev.example/");
    }

    [Fact]
    public void Load_ModeloConEndpointExplicitoYAlias_ConservaElExplicito()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""layout.prebuilt-layout"", ""provider"": ""azure-document-intelligence-layout"",
                  ""endpoint"": ""https://explicito.example/"", ""resourceAlias"": ""di"" }
            ]
        }");

        var loader = new LayoutModelRegistryLoader(_registryPath, ResolverDePrueba());

        loader.GetDefaultModel().Endpoint.Should().Be("https://explicito.example/");
    }

    [Fact]
    public void Load_ModeloConAliasNoMapeado_LanzaAlCargarElRegistro()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""x"", ""provider"": ""azure-openai"", ""resourceAlias"": ""openai_primary"" }
            ]
        }");

        var loader = new LayoutModelRegistryLoader(_registryPath, ResolverDePrueba());

        var act = () => loader.Load();

        act.Should().Throw<InvalidOperationException>().WithMessage("*openai_primary*");
    }

    [Fact]
    public void Load_SinResolvedor_ConservaComportamientoActual()
    {
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                { ""key"": ""layout.prebuilt-layout"", ""provider"": ""azure-document-intelligence-layout"",
                  ""endpoint"": ""https://pro.example/"" }
            ]
        }");

        var loader = new LayoutModelRegistryLoader(_registryPath);

        loader.GetDefaultModel().Endpoint.Should().Be("https://pro.example/");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
