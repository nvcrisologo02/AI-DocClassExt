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

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}