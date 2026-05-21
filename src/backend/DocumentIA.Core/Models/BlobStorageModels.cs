namespace DocumentIA.Core.Models;

public class SubirBlobInput
{
    public string ContenidoBase64 { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string Contenedor { get; set; } = "documents";
    /// <summary>
    /// Si está informado, el documento ya fue subido al blob en el trigger.
    /// SubirBlobActivity retornará este path sin realizar ninguna operación.
    /// </summary>
    public string? BlobPath { get; set; }
}
