namespace DocumentIA.Core.Models;

public class PrepararDocumentoClasificacionInput
{
    public string DocumentoBase64 { get; set; } = string.Empty;
    public string NombreDocumento { get; set; } = string.Empty;
    public int? MaxPaginasClasificacion { get; set; }
    /// <summary>
    /// Ruta en blob storage del documento original.
    /// Si está informado, la actividad descarga desde blob en lugar de usar DocumentoBase64.
    /// </summary>
    public string? BlobPath { get; set; }
}
