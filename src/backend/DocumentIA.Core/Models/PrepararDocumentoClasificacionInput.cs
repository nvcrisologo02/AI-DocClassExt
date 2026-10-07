namespace DocumentIA.Core.Models;

public class PrepararDocumentoClasificacionInput
{
    public string NombreDocumento { get; set; } = string.Empty;
    public int? MaxPaginasClasificacion { get; set; }

    /// <summary>
    /// Ruta en blob storage del documento original (<contenedor>/yyyy/MM/<sha256>.<ext>).
    /// Obligatoria: desde el blob-first el documento nunca viaja en base64 (AB#100814).
    /// </summary>
    public string? BlobPath { get; set; }
}
