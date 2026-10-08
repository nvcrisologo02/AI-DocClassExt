namespace DocumentIA.Core.Models;

public class PrepararDocumentoClasificacionResultado
{
    /// <summary>
    /// Ruta del documento que debe usar la clasificación: el recorte en documents-clasif si se
    /// aplicó, o la ruta del documento original si no (AB#100814).
    /// </summary>
    public string? BlobPathClasificacion { get; set; }

    /// <summary>
    /// Legado: base64 del recorte. El código actual lo deja a null; solo lo leen las instancias
    /// serializadas antes de AB#100814. Retirar en la release siguiente a v1.0.0.
    /// </summary>
    [Obsolete("El recorte viaja por BlobPathClasificacion (AB#100814). Solo lectura legada.")]
    public string? DocumentoBase64Clasif { get; set; }

    public int TotalPaginas { get; set; }
    public int CharsTextoNativo { get; set; }
    public int PaginasIncluidas { get; set; }
    public bool RecorteAplicado { get; set; }
}
