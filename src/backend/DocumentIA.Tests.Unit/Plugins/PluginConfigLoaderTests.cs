using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Plugins.Integration;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Plugins;

public class PluginConfigLoaderTests : IDisposable
{
    private readonly string _tempDirectory;

    public PluginConfigLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PluginConfigLoaderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task LoadConfigAsync_FileMode_WhenFileDoesNotExist_ReturnsEmptyConfig()
    {
        var logger = Mock.Of<ILogger<PluginConfigLoader>>();
        var sut = new PluginConfigLoader(_tempDirectory, logger);

        var result = await sut.LoadConfigAsync("nota-simple");

        result.TipologiaId.Should().Be("nota-simple");
        result.Plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadConfigAsync_FileMode_WhenCalledTwice_UsesInMemoryDictionaryCache()
    {
        var filePath = Path.Combine(_tempDirectory, "nota-simple.plugins.json");
        File.WriteAllText(filePath, """
        {
          "tipologiaId": "nota-simple",
          "plugins": [
            { "pluginKey": "gdc", "pluginType": "soap", "enabled": true, "priority": 1 }
          ]
        }
        """);

        var logger = Mock.Of<ILogger<PluginConfigLoader>>();
        var sut = new PluginConfigLoader(_tempDirectory, logger);

        var first = await sut.LoadConfigAsync("nota-simple");

        File.WriteAllText(filePath, """
        {
          "tipologiaId": "nota-simple",
          "plugins": [
            { "pluginKey": "otro", "pluginType": "rest", "enabled": true, "priority": 2 }
          ]
        }
        """);

        var second = await sut.LoadConfigAsync("nota-simple");

        first.Plugins.Should().ContainSingle(p => p.PluginKey == "gdc");
        second.Plugins.Should().ContainSingle(p => p.PluginKey == "gdc");
    }

    [Fact]
    public async Task LoadConfigAsync_DatabaseMode_WhenPublishedConfigExists_ReturnsDeserializedConfig()
    {
        var repo = new Mock<IPluginTipologiaConfigRepository>();
        repo.Setup(x => x.GetPublishedByTipologiaCodigoAsync("nota-simple"))
            .ReturnsAsync(new PluginTipologiaConfigEntity
            {
                TipologiaCodigo = "nota-simple",
                Estado = EstadoPluginConfig.Published,
                ConfiguracionJson = """
                {
                  "plugins": [
                    { "pluginKey": "gdc", "pluginType": "soap", "enabled": true, "priority": 1 }
                  ]
                }
                """
            });

        using var services = BuildServices(repo.Object);
        var memoryCache = services.GetRequiredService<IMemoryCache>();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var logger = services.GetRequiredService<ILogger<PluginConfigLoader>>();

        var sut = new PluginConfigLoader(memoryCache, scopeFactory, logger);

        var result = await sut.LoadConfigAsync("nota-simple");

        result.TipologiaId.Should().Be("nota-simple");
        result.Plugins.Should().ContainSingle(p => p.PluginKey == "gdc");
    }

    [Fact]
    public async Task LoadConfigAsync_DatabaseMode_WhenNoPublishedConfig_ReturnsEmptyConfig()
    {
        var repo = new Mock<IPluginTipologiaConfigRepository>();
        repo.Setup(x => x.GetPublishedByTipologiaCodigoAsync("nota-simple"))
            .ReturnsAsync((PluginTipologiaConfigEntity?)null);

        using var services = BuildServices(repo.Object);
        var sut = new PluginConfigLoader(
            services.GetRequiredService<IMemoryCache>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<PluginConfigLoader>>());

        var result = await sut.LoadConfigAsync("nota-simple");

        result.TipologiaId.Should().Be("nota-simple");
        result.Plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadConfigAsync_DatabaseMode_WhenCalledTwice_UsesMemoryCache()
    {
        var repo = new Mock<IPluginTipologiaConfigRepository>();
        repo.Setup(x => x.GetPublishedByTipologiaCodigoAsync("nota-simple"))
            .ReturnsAsync(new PluginTipologiaConfigEntity
            {
                TipologiaCodigo = "nota-simple",
                Estado = EstadoPluginConfig.Published,
                ConfiguracionJson = """
                {
                  "tipologiaId": "nota-simple",
                  "plugins": []
                }
                """
            });

        using var services = BuildServices(repo.Object);
        var sut = new PluginConfigLoader(
            services.GetRequiredService<IMemoryCache>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<PluginConfigLoader>>());

        var _ = await sut.LoadConfigAsync("nota-simple");
        var __ = await sut.LoadConfigAsync("nota-simple");

        repo.Verify(x => x.GetPublishedByTipologiaCodigoAsync("nota-simple"), Times.Once);
    }

    [Fact]
    public async Task LoadConfigAsync_DatabaseMode_WhenJsonIsInvalid_ThrowsPluginException()
    {
        var repo = new Mock<IPluginTipologiaConfigRepository>();
        repo.Setup(x => x.GetPublishedByTipologiaCodigoAsync("nota-simple"))
            .ReturnsAsync(new PluginTipologiaConfigEntity
            {
                TipologiaCodigo = "nota-simple",
                Estado = EstadoPluginConfig.Published,
                ConfiguracionJson = "{ invalid json }"
            });

        using var services = BuildServices(repo.Object);
        var sut = new PluginConfigLoader(
            services.GetRequiredService<IMemoryCache>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<PluginConfigLoader>>());

        var action = async () => await sut.LoadConfigAsync("nota-simple");

        await action.Should().ThrowAsync<PluginException>();
    }

    private static ServiceProvider BuildServices(IPluginTipologiaConfigRepository repository)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddMemoryCache();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(repository);
        return serviceCollection.BuildServiceProvider();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
