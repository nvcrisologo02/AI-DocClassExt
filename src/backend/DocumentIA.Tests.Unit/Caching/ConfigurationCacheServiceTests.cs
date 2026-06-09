using DocumentIA.Core.Caching;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Caching;

public class ConfigurationCacheServiceTests
{
    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNullAndCountsMiss()
    {
        var sut = CreateSut();

        var value = await sut.GetAsync<TestPayload>("missing");

        value.Should().BeNull();
        var stats = sut.GetStats();
        stats.MissCount.Should().Be(1);
        stats.HitCount.Should().Be(0);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsValueAndCountsHit()
    {
        var sut = CreateSut();

        await sut.SetAsync("k1", new TestPayload { Name = "ok" });
        var value = await sut.GetAsync<TestPayload>("k1");

        value.Should().NotBeNull();
        value!.Name.Should().Be("ok");
        var stats = sut.GetStats();
        stats.HitCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WhenEntryExpired_ReturnsNull()
    {
        var sut = CreateSut();

        await sut.SetAsync("k1", new TestPayload { Name = "ttl" }, TimeSpan.FromMilliseconds(60));
        await Task.Delay(120);

        var value = await sut.GetAsync<TestPayload>("k1");

        value.Should().BeNull();
        sut.Exists("k1").Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_WhenCapacityReached_EvictsOldest()
    {
        var sut = CreateSut(maxEntries: 100);

        for (var i = 0; i < 110; i++)
        {
            await sut.SetAsync($"k{i}", new TestPayload { Name = $"v{i}" });
        }

        var oldest = await sut.GetAsync<TestPayload>("k0");
        var newest = await sut.GetAsync<TestPayload>("k109");

        oldest.Should().BeNull();
        newest.Should().NotBeNull();
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        var sut = CreateSut();

        await sut.SetAsync("k1", new TestPayload { Name = "a" });
        await sut.SetAsync("k2", new TestPayload { Name = "b" });
        await sut.ClearAsync();

        sut.Exists("k1").Should().BeFalse();
        sut.Exists("k2").Should().BeFalse();
        sut.GetStats().ItemCount.Should().Be(0);
    }

    private static ConfigurationCacheService CreateSut(int maxEntries = 1000)
    {
        var configData = new Dictionary<string, string?>
        {
            ["CacheConfiguration:Enabled"] = "true",
            ["CacheConfiguration:TTL:Minutes"] = "5",
            ["CacheConfiguration:MaxEntries"] = maxEntries.ToString()
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return new ConfigurationCacheService(
            configuration,
            Mock.Of<ILogger<ConfigurationCacheService>>());
    }

    private sealed class TestPayload
    {
        public string Name { get; set; } = string.Empty;
    }
}
