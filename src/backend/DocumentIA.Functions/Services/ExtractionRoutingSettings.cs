namespace DocumentIA.Functions.Services;

public class ExtractionRoutingSettings
{
    public string DefaultProvider { get; set; } = "mock";
    public InitialMarkdownSettings InitialMarkdown { get; set; } = new();
}

public class InitialMarkdownSettings
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "none"; // none | di-layout
}