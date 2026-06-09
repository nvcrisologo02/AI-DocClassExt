using DocumentIA.Core.Caching;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Caching;

public class CacheInvalidationServiceTests
{
    [Fact]
    public async Task InvalidateAllAsync_ClearsEntireCache()
    {
        // Arrange
        var cache = CreateMockCache();
        var service = new CacheInvalidationService(cache.Object, Mock.Of<ILogger<CacheInvalidationService>>());

        // Act
        await service.InvalidateAllAsync();

        // Assert
        cache.Verify(c => c.ClearAsync(), Times.Once);
    }

    [Fact]
    public async Task InvalidateTipologiaAsync_RemovesSpecificCacheKey()
    {
        // Arrange
        var cache = CreateMockCache();
        var service = new CacheInvalidationService(cache.Object, Mock.Of<ILogger<CacheInvalidationService>>());

        // Act
        await service.InvalidateTipologiaAsync(tipologiaId: 123, tipologiaVersion: "1.0", tipologiaUpdateTicks: 12345);

        // Assert
        cache.Verify(c => c.RemoveAsync("tipologia-validation-config:123:1.0:12345"), Times.Once);
    }

    [Fact]
    public async Task InvalidatePluginConfigAsync_LogsInvalidation()
    {
        // Arrange
        var cache = CreateMockCache();
        var logger = new Mock<ILogger<CacheInvalidationService>>();
        var service = new CacheInvalidationService(cache.Object, logger.Object);

        // Act
        await service.InvalidatePluginConfigAsync(tipologiaId: 456);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalidating plugin config")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Exactly(2) // Two patterns for file and DB configs
        );
    }

    private static Mock<IConfigurationCache> CreateMockCache()
    {
        return new Mock<IConfigurationCache>();
    }
}
