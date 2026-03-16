namespace DocumentIA.Functions.Services;

public class AzureContentUnderstandingSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "ApiKey";
    public string DefaultProcessingLocation { get; set; } = "global";
}