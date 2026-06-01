#nullable enable
using System.Reflection;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Services;

public class AzureContentUnderstandingProviderCircuitBreakerTests
{
    [Fact]
    public void ResolveModelKeyWithCircuit_WhenPrimaryCircuitIsOpen_FailsOverToSecondary()
    {
        var sut = CreateProvider(new AzureContentUnderstandingOptions
        {
            EnableCircuitBreaker = true,
            CircuitBreakerFailureThreshold = 1,
            CircuitBreakerOpenSeconds = 60
        });

        InvokePrivate(sut, "RegisterCircuitFailure", "primary-cu", "nota.simple_bal", "test");

        var extractionConfig = new TipologiaExtractionConfig
        {
            Enabled = true,
            ModelKey = "primary-cu",
            SecondaryModelKey = "secondary-cu"
        };

        var resolved = (string)InvokePrivate(
            sut,
            "ResolveModelKeyWithCircuit",
            "primary-cu",
            extractionConfig,
            "nota.simple_bal")!;

        resolved.Should().Be("secondary-cu");
    }

    [Fact]
    public void ResolveModelKeyWithCircuit_WhenCircuitBreakerDisabled_KeepsPreferredModelKey()
    {
        var sut = CreateProvider(new AzureContentUnderstandingOptions
        {
            EnableCircuitBreaker = false,
            CircuitBreakerFailureThreshold = 1,
            CircuitBreakerOpenSeconds = 60
        });

        InvokePrivate(sut, "RegisterCircuitFailure", "primary-cu", "nota.simple_bal", "test");

        var extractionConfig = new TipologiaExtractionConfig
        {
            Enabled = true,
            ModelKey = "primary-cu",
            SecondaryModelKey = "secondary-cu"
        };

        var resolved = (string)InvokePrivate(
            sut,
            "ResolveModelKeyWithCircuit",
            "primary-cu",
            extractionConfig,
            "nota.simple_bal")!;

        resolved.Should().Be("primary-cu");
    }

    [Fact]
    public void IsCircuitOpen_AfterCooldown_ReturnsFalse()
    {
        var sut = CreateProvider(new AzureContentUnderstandingOptions
        {
            EnableCircuitBreaker = true,
            CircuitBreakerFailureThreshold = 1,
            CircuitBreakerOpenSeconds = 1
        });

        InvokePrivate(sut, "RegisterCircuitFailure", "primary-cu", "nota.simple_bal", "test");

        var openNow = (bool)InvokePrivate(sut, "IsCircuitOpen", "primary-cu", "nota.simple_bal")!;
        openNow.Should().BeTrue();

        Thread.Sleep(1200);

        var openAfterCooldown = (bool)InvokePrivate(sut, "IsCircuitOpen", "primary-cu", "nota.simple_bal")!;
        openAfterCooldown.Should().BeFalse();
    }

    private static AzureContentUnderstandingProvider CreateProvider(AzureContentUnderstandingOptions options)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DocumentIA.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var extractionRegistryPath = Path.Combine(tempDir, "extraction.models.json");
        File.WriteAllText(
            extractionRegistryPath,
            "{\"models\":[{\"key\":\"primary-cu\",\"provider\":\"azure-content-understanding\",\"endpoint\":\"https://example.cu.azure.com\",\"apiKey\":\"test\",\"authMode\":\"ApiKey\",\"analyzerId\":\"analyzer\"}]}");

        var extractionLoader = new ExtractionModelRegistryLoader(extractionRegistryPath);
        var tipologiaLoader = CreateTipologiaConfigLoader();

        return new AzureContentUnderstandingProvider(
            Mock.Of<ILogger<AzureContentUnderstandingProvider>>(),
            tipologiaLoader,
            extractionLoader,
            new ContentUnderstandingResultMapper(),
            Mock.Of<IBlobStorageService>(),
            Options.Create(options),
            new TelemetryClient(TelemetryConfiguration.CreateDefault()));
    }

    private static TipologiaConfigLoader CreateTipologiaConfigLoader()
    {
        var provider = new Mock<IServiceProvider>();
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new TipologiaConfigLoader(
            new MemoryCache(new MemoryCacheOptions()),
            scopeFactory.Object);
    }

    private static object? InvokePrivate(object instance, string methodName, params object?[] args)
    {
        var method = instance
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró método privado '{methodName}'.");

        return method.Invoke(instance, args);
    }
}
