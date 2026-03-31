namespace DocumentIA.Functions.Services;

public class AzureDocumentIntelligenceClassificationSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>
    /// "ApiKey" (default) or "DefaultAzureCredential" (Managed Identity).
    /// </summary>
    public string AuthMode { get; set; } = "ApiKey";
    public string ApiVersion { get; set; } = "2024-11-30";
    public int PollIntervalMs { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 120;
}
