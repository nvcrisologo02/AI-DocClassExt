namespace DocumentIA.Core.Models;

public class ObtenerUltimaEjecucionDuplicadoInput
{
    public string SHA256 { get; set; } = string.Empty;
    public bool ClassificationOnly { get; set; }
    /// <summary>
    /// Nivel de clasificación que formó parte de la clave de deduplicación.
    /// null si no se especificó (deduplicación solo por SHA256 + ClassificationOnly).
    /// </summary>
    public string? NivelClasificacion { get; set; }
}
