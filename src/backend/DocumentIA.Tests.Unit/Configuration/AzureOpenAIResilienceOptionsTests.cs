using DocumentIA.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace DocumentIA.Tests.Unit.Configuration;

public class AzureOpenAIResilienceOptionsTests
{
    [Fact]
    public void Defaults_AreSafeAndConservative()
    {
        var options = new AzureOpenAIResilienceOptions();

        options.EnableCircuitBreaker.Should().BeTrue();
        options.CircuitBreakerFailureThreshold.Should().Be(5);
        options.CircuitBreakerOpenSeconds.Should().Be(45);
        options.MaxRetries.Should().Be(3);
        options.InitialRetryDelayMs.Should().Be(500);
        options.MaxRetryDelaySeconds.Should().Be(60);
    }
}
