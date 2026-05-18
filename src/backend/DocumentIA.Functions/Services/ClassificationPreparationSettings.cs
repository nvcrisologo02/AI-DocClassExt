namespace DocumentIA.Functions.Services;

public class ClassificationPreparationSettings
{
    public bool Enabled { get; set; } = true;
    public int MaxPaginasClasificacionDefault { get; set; } = 3;
    public Dictionary<string, int> OverridesPorFamilia { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> OverridesPorTipologia { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
