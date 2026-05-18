namespace DocumentIA.Core.Models;

public class ObtenerUltimaEjecucionDuplicadoInput
{
    public string SHA256 { get; set; } = string.Empty;
    public bool ClassificationOnly { get; set; }
}
