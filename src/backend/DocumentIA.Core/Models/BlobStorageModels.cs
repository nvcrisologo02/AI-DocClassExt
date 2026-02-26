namespace DocumentIA.Core.Models;

public class SubirBlobInput
{
    public string ContenidoBase64 { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string Contenedor { get; set; } = "documents";
}
