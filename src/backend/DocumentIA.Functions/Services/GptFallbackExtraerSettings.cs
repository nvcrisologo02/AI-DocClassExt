namespace DocumentIA.Functions.Services;

public class GptFallbackExtraerSettings
{
    public bool Enabled { get; set; } = false;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "ApiKey";
    public string DeploymentName { get; set; } = string.Empty;
    public double MinFieldsRatio { get; set; } = 0.5;
    public double Temperature { get; set; } = 0.0;
    public int MaxTokens { get; set; } = 2000;
    public int TimeoutSeconds { get; set; } = 60;
}
