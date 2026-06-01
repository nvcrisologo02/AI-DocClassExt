namespace DocumentIA.Core.Configuration;

public class AzureContentUnderstandingOptions
{
    public int MaxConcurrentCalls { get; set; } = 2;

    public int HardTimeoutSeconds { get; set; } = 90;

    public bool EnableCircuitBreaker { get; set; } = true;

    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    public int CircuitBreakerOpenSeconds { get; set; } = 45;

    public int MaxRetries { get; set; } = 3;

    public int InitialRetryDelayMs { get; set; } = 500;
}
