using System.Text.Json;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Caching;

public class ConfigurationCacheBenchmarkTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Cache_AvoidsReParsing_OnlyParsesEachUniqueConfigOnce()
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

        var parseCountWithoutCache = 0;
        var parseCountWithCache = 0;

        ParseAll(testData, iterations, useCache: false, () => parseCountWithoutCache++);
        ParseAll(testData, iterations, useCache: true, () => parseCountWithCache++);

        // Sin cache: se parsea en cada iteración (iterations * configs).
        parseCountWithoutCache.Should().Be(iterations * testData.Length,
            "sin cache cada iteración vuelve a deserializar todos los JSON");

        // Con cache: cada JSON único se parsea una sola vez, sin importar las iteraciones.
        parseCountWithCache.Should().Be(testData.Length,
            "la cache debe evitar re-parsear un JSON ya deserializado");

        parseCountWithCache.Should().BeLessThan(parseCountWithoutCache,
            "la cache reduce el trabajo de parseo respecto a la ruta sin cache");
    }

    [Fact]
    public void Cache_ReturnsSameParsedResult_AsUncachedPath()
    {
        var json = CreateSampleConfig("config1");
        var cache = new Dictionary<string, TestConfig?>();

        var uncached = JsonSerializer.Deserialize<TestConfig>(json, SerializerOptions);

        cache[json] = JsonSerializer.Deserialize<TestConfig>(json, SerializerOptions);
        var cachedFirst = cache[json];
        var cachedSecond = cache.TryGetValue(json, out var hit) ? hit : null;

        cachedFirst.Should().BeSameAs(cachedSecond,
            "un segundo acceso debe devolver la misma instancia cacheada, no un re-parseo");

        cachedFirst!.Id.Should().Be(uncached!.Id);
        cachedFirst.PromptConfig!.SystemPrompt.Should().Be(uncached.PromptConfig!.SystemPrompt);
        cachedFirst.PromptConfig.UserPromptTemplate.Should().Be(uncached.PromptConfig.UserPromptTemplate);
    }

    private static void ParseAll(string[] configJsons, int iterations, bool useCache, Action onParse)
    {
        var cache = new Dictionary<string, TestConfig?>();

        for (var i = 0; i < iterations; i++)
        {
            foreach (var json in configJsons)
            {
                if (useCache && cache.ContainsKey(json))
                {
                    continue;
                }

                onParse();
                var parsed = JsonSerializer.Deserialize<TestConfig>(json, SerializerOptions);

                if (useCache)
                {
                    cache[json] = parsed;
                }
            }
        }
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
