namespace DocumentIA.Core.Models;

public class PrepararDocumentoClasificacionResultado
{
    public string DocumentoBase64Clasif { get; set; } = string.Empty;
    public int TotalPaginas { get; set; }
    public int CharsTextoNativo { get; set; }
    public int PaginasIncluidas { get; set; }
    public bool RecorteAplicado { get; set; }
}
