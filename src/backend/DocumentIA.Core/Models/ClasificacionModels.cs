namespace DocumentIA.Core.Models;

public class ClasificacionInput
{
    public ContratoEntrada Entrada { get; set; } = new();
    public Dictionary<string, object> DatosNormalizados { get; set; } = new();
}
