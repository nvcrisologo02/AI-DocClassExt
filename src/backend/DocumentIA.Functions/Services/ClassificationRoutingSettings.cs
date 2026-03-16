namespace DocumentIA.Functions.Services;

public class ClassificationRoutingSettings
{
    public string DefaultProvider { get; set; } = "azure-document-intelligence";
    public string DefaultModelKey { get; set; } = string.Empty;
}
