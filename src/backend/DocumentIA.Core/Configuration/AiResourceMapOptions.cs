namespace DocumentIA.Core.Configuration;

/// <summary>Sección "AI" de configuración: mapa alias lógico → recurso físico por entorno.</summary>
public sealed class AiResourceMapOptions
{
    public const string SectionName = "AI";

    /// <summary>Clave: alias (openai_primary, cu_primary, cu_secondary, di). App Setting: AI__Resources__&lt;alias&gt;__Endpoint.</summary>
    public Dictionary<string, AiResourceEntry> Resources { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AiResourceEntry
{
    public string Endpoint { get; set; } = string.Empty;
}
