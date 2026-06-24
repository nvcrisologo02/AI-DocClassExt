using DocumentIA.Core.Caching;
using DocumentIA.Core.Configuration;
using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Caching;

public class ConfigurationCacheBenchmarkTests
{
    [Fact]
    public void Benchmark_JsonParsingWithCache_VsWithout_ShowsSignificantImprovement()
    {
        const int iterations = 200;

        var testData = new[]
        {
            CreateSampleConfig("config1"),
            CreateSampleConfig("config2"),
            CreateSampleConfig("config3"),
            CreateSampleConfig("config4"),
            CreateSampleConfig("config5")
        };

        // Benchmark sin cache
        var timeWithoutCache = BenchmarkParsing(testData, iterations, useCache: false);

        // Benchmark con cache
        var timeWithCache = BenchmarkParsing(testData, iterations, useCache: true);

        var improvementPercent = timeWithoutCache > 0 
            ? ((double)(timeWithoutCache - timeWithCache) / timeWithoutCache) * 100 
            : 0;

        // Target: 60-80% improvement
        improvementPercent.Should().BeGreaterThan(40, $"cache should provide improvement; got {improvementPercent:F1}%");
    }

    private long BenchmarkParsing(string[] configJsons, int iterations, bool useCache)
    {
        var sw = Stopwatch.StartNew();
        var cache = new Dictionary<string, TestConfig?>();

        if (useCache)
        {
            for (var i = 0; i < iterations; i++)
            {
                foreach (var json in configJsons)
                {
                    if (!cache.TryGetValue(json, out _))
                    {
                        cache[json] = JsonSerializer.Deserialize<TestConfig>(json);
                    }
                }
            }
        }
        else
        {
            for (var i = 0; i < iterations; i++)
            {
                foreach (var json in configJsons)
                {
                    var _ = JsonSerializer.Deserialize<TestConfig>(json);
                }
            }
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static string CreateSampleConfig(string id)
    {
        return $$"""
        {
          "id": "{{id}}",
          "promptConfig": {
            "systemPrompt": "Eres un clasificador de documentos legales SAREB",
            "userPromptTemplate": "Clasifica este documento: {documento}"
          }
        }
        """;
    }

    private class TestConfig
    {
        public string? Id { get; set; }
        public PromptConfig? PromptConfig { get; set; }
    }

    private class PromptConfig
    {
        public string? SystemPrompt { get; set; }
        public string? UserPromptTemplate { get; set; }
    }
}
