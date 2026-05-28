namespace DocumentIA.Core.Configuration;

public class AzureContentUnderstandingOptions
{
    public int MaxConcurrentCalls { get; set; } = 2;

    public int MaxRetries { get; set; } = 3;

    public int InitialRetryDelayMs { get; set; } = 500;
}
