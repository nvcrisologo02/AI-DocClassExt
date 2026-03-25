namespace DocumentIA.Core.Models;

public class ExtraccionInput
{
    public ContratoEntrada Entrada { get; set; } = new();
    public string Tipologia { get; set; } = string.Empty;
    public Dictionary<string, object> DatosNormalizados { get; set; } = new();
}

public class ExtraccionResultado
{
    public string Proveedor { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public bool LayoutEnabled { get; set; }
    public string? OperationId { get; set; }
    public int Paginas { get; set; }
    public Dictionary<string, int> TiemposMs { get; set; } = new();
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
}