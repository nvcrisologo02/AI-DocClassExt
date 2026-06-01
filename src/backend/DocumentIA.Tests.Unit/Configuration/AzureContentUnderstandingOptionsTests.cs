#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class AzureContentUnderstandingOptionsTests
{
    [Fact]
    public void Defaults_ShouldMatchResilienceBaseline()
    {
        var options = new AzureContentUnderstandingOptions();

        options.MaxConcurrentCalls.Should().Be(2);
        options.HardTimeoutSeconds.Should().Be(90);
        options.EnableCircuitBreaker.Should().BeTrue();
        options.CircuitBreakerFailureThreshold.Should().Be(5);
        options.CircuitBreakerOpenSeconds.Should().Be(45);
        options.MaxRetries.Should().Be(3);
        options.InitialRetryDelayMs.Should().Be(500);
    }

    [Fact]
    public void CustomValues_ShouldBeApplied()
    {
        var options = new AzureContentUnderstandingOptions
        {
            MaxConcurrentCalls = 4,
            HardTimeoutSeconds = 120,
            EnableCircuitBreaker = false,
            CircuitBreakerFailureThreshold = 7,
            CircuitBreakerOpenSeconds = 60,
            MaxRetries = 4,
            InitialRetryDelayMs = 750
        };

        options.MaxConcurrentCalls.Should().Be(4);
        options.HardTimeoutSeconds.Should().Be(120);
        options.EnableCircuitBreaker.Should().BeFalse();
        options.CircuitBreakerFailureThreshold.Should().Be(7);
        options.CircuitBreakerOpenSeconds.Should().Be(60);
        options.MaxRetries.Should().Be(4);
        options.InitialRetryDelayMs.Should().Be(750);
    }
}
