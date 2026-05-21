namespace DocumentIA.Core.Models;

public class ClasificacionInput
{
    public ContratoEntrada Entrada { get; set; } = new();
    public Dictionary<string, object> DatosNormalizados { get; set; } = new();
    /// <summary>
    /// Umbral de fallback efectivo resuelto por el orquestador antes de llamar a ClasificarActivity.
    /// Cadena: instrucciones.Classification.Umbral ?? tipología.ClasifUmbralFallback ?? config.FallbackThreshold.
    /// null = usar config.FallbackThreshold directamente en el proveedor.
    /// </summary>
    public double? UmbralFallbackEfectivo { get; set; }
    public string? DocumentoBase64Override { get; set; }
    public int CharsTextoNativo { get; set; }
    public int TotalPaginas { get; set; }
}
