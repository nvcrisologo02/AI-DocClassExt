namespace DocumentIA.Core.Models;

public class PrepararDocumentoClasificacionInput
{
    public string DocumentoBase64 { get; set; } = string.Empty;
    public string NombreDocumento { get; set; } = string.Empty;
    public int? MaxPaginasClasificacion { get; set; }
}
