namespace DocumentIA.Core.Models;

public class ObtenerUltimaEjecucionDuplicadoInput
{
    public string SHA256 { get; set; } = string.Empty;

    /// <summary>Modo pedido. En modo estricto ya va dentro de la clave; se conserva para la regla anterior.</summary>
    public bool ClassificationOnly { get; set; }

    /// <summary>Nivel pedido. Idem.</summary>
    public string? NivelClasificacion { get; set; }

    /// <summary>Clave de intencion de la peticion (AB#100930). Null solo si no pudo calcularse.</summary>
    public string? ClaveIntencion { get; set; }

    /// <summary>Necesidad de paginas de la peticion (spec, seccion 10): documento entero...</summary>
    public bool DocumentoCompleto { get; set; }

    /// <summary>...o al menos estas paginas.</summary>
    public int PaginasMinimas { get; set; }

    /// <summary>Instrucciones.Extraction.Model cuando no es "auto": manda sobre la tipologia al comparar el modelo de extraccion.</summary>
    public string? ExtraccionModelKeyPedido { get; set; }

    public NecesidadMarkdown Necesidad()
        => DocumentoCompleto ? NecesidadMarkdown.Completo() : NecesidadMarkdown.Paginas(PaginasMinimas);
}
