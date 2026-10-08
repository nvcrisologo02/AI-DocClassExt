using System.Text.Json.Serialization;

namespace DocumentIA.Core.Models;

/// <summary>
/// Que necesita una actividad del markdown del documento: el documento entero o un
/// minimo de paginas. Es lo que declara cada paso del pipeline, en lugar de "el base64
/// que tengo a mano" (AB#100245).
/// </summary>
public sealed record NecesidadMarkdown(bool DocumentoCompleto, int PaginasMinimas)
{
    public static NecesidadMarkdown Completo() => new(true, 0);
    public static NecesidadMarkdown Paginas(int n) => new(false, Math.Max(1, n));
}

/// <summary>De donde salio el markdown de una resolucion.</summary>
public enum FuenteMarkdown
{
    Ninguna,
    Caller,
    CacheEjecucion,
    BaseDatos,
    Layout,
    Clasificador,
    Extraccion,
    Normalizacion
}

/// <summary>
/// Por que una resolucion no trajo texto (AB#100880). Solo <see cref="DocumentoSinTexto"/> significa
/// que el documento esta vacio; el resto son fallos al obtener el texto de un documento que puede
/// tenerlo, y el orquestador no debe cerrarlos como SIN_CONTENIDO_DOCUMENTO.
/// </summary>
public enum MotivoSinContenido
{
    /// <summary>Layout respondio y el documento no tiene texto.</summary>
    DocumentoSinTexto,
    /// <summary>Sin BlobPath ni base64: no se pudo llamar a Layout.</summary>
    SinFuente,
    /// <summary>Document Intelligence rechaza el formato del documento (HTTP 415).</summary>
    FormatoNoSoportado,
    /// <summary>Layout no respondio en el tiempo configurado (o el cliente HTTP agoto el suyo).</summary>
    LayoutTimeout,
    /// <summary>Cualquier otro fallo al llamar a Layout; <see cref="CausaSinContenido.CodigoHttp"/> lleva el codigo si lo hubo.</summary>
    LayoutError
}

/// <summary>Causa por la que una resolucion de markdown no trajo texto. Viaja con el resultado (AB#100880).</summary>
public sealed class CausaSinContenido
{
    private const int MaxDetalle = 300;

    public MotivoSinContenido Motivo { get; set; }

    /// <summary>Tipo de excepcion y mensaje, recortado. Solo para el operador.</summary>
    public string? Detalle { get; set; }

    /// <summary>Codigo HTTP de la respuesta de Layout, si lo hubo.</summary>
    public int? CodigoHttp { get; set; }

    /// <summary>Timeout, 429 o 5xx: merece reintento, no un error definitivo.</summary>
    [JsonIgnore]
    public bool EsTransitoria => Motivo == MotivoSinContenido.LayoutTimeout
        || (Motivo == MotivoSinContenido.LayoutError && CodigoHttp is 429 or 500 or 502 or 503 or 504);

    /// <summary>Layout respondio y no habia texto: el unico caso de documento vacio de verdad.</summary>
    [JsonIgnore]
    public bool EsDocumentoSinTexto => Motivo == MotivoSinContenido.DocumentoSinTexto;

    /// <summary>Mensaje para el cliente y el operador cuando el fallo es de obtencion del texto.</summary>
    public string MensajeObtencionFallida() => $"No se pudo obtener el texto del documento: {Describir()}";

    public string Describir()
    {
        var texto = Motivo.ToString();
        if (CodigoHttp.HasValue)
        {
            texto += $" (HTTP {CodigoHttp.Value})";
        }

        if (!string.IsNullOrWhiteSpace(Detalle))
        {
            texto += $": {Detalle}";
        }

        return texto;
    }

    public static CausaSinContenido DesdeExcepcion(Exception ex)
    {
        var detalle = Recortar($"{ex.GetType().Name}: {ex.Message}");

        return ex switch
        {
            TimeoutException => new CausaSinContenido { Motivo = MotivoSinContenido.LayoutTimeout, Detalle = detalle },
            OperationCanceledException => new CausaSinContenido { Motivo = MotivoSinContenido.LayoutTimeout, Detalle = detalle },
            LayoutRequestException { CodigoHttp: 415 } => new CausaSinContenido { Motivo = MotivoSinContenido.FormatoNoSoportado, CodigoHttp = 415, Detalle = detalle },
            LayoutRequestException layout => new CausaSinContenido { Motivo = MotivoSinContenido.LayoutError, CodigoHttp = layout.CodigoHttp, Detalle = detalle },
            HttpRequestException http => new CausaSinContenido
            {
                Motivo = MotivoSinContenido.LayoutError,
                CodigoHttp = http.StatusCode.HasValue ? (int)http.StatusCode.Value : null,
                Detalle = detalle
            },
            _ => new CausaSinContenido { Motivo = MotivoSinContenido.LayoutError, Detalle = detalle }
        };
    }

    private static string Recortar(string texto)
    {
        var plano = texto.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return plano.Length <= MaxDetalle ? plano : plano[..MaxDetalle];
    }
}

/// <summary>Markdown resuelto con su cobertura. Es la cache de la ejecucion en el orquestador.</summary>
public sealed class ResultadoMarkdown
{
    public string? Markdown { get; set; }

    /// <summary>Paginas que cubre. 0 cuando no se conoce.</summary>
    public int Paginas { get; set; }

    /// <summary>Cubre el documento entero (con independencia de si se conoce el numero de paginas).</summary>
    public bool Completo { get; set; }

    public FuenteMarkdown Fuente { get; set; } = FuenteMarkdown.Ninguna;

    /// <summary>Se escribio en BD en esta resolucion.</summary>
    public bool Persistido { get; set; }

    public List<ConsumoIA> Consumos { get; set; } = new();

    /// <summary>Por que no hay texto. Nula cuando lo hay (AB#100880).</summary>
    public CausaSinContenido? CausaSinContenido { get; set; }

    public bool TieneContenido => !string.IsNullOrWhiteSpace(Markdown);

    /// <summary>
    /// Unica regla de "sirve": completo cubre todo; parcial cubre una necesidad de N paginas
    /// si llega a N. Cubrir significa entregar tal cual, sin recortar (regla 7 de la spec).
    /// </summary>
    public bool Cubre(NecesidadMarkdown necesidad)
    {
        if (!TieneContenido)
        {
            return false;
        }

        if (Completo)
        {
            return true;
        }

        return !necesidad.DocumentoCompleto && Paginas >= necesidad.PaginasMinimas;
    }

    /// <summary>
    /// Cobertura estrictamente mayor que la de <paramref name="otro"/>. Es la regla 7 aplicada
    /// dentro de una misma ejecucion: la cobertura nunca se degrada, asi que un resultado solo
    /// sustituye al que ya habia si lo mejora. Sin contenido no mejora nada; frente a un completo
    /// no mejora nadie; un completo mejora a cualquier parcial; y entre parciales gana el que
    /// cubre mas paginas (AB#100252).
    /// </summary>
    public bool MejoraA(ResultadoMarkdown? otro)
    {
        if (!TieneContenido)
        {
            return false;
        }

        if (otro is not { TieneContenido: true })
        {
            return true;
        }

        if (otro.Completo)
        {
            return false;
        }

        return Completo || Paginas > otro.Paginas;
    }
}

/// <summary>Todo lo que el resolutor necesita saber del documento y de la peticion.</summary>
public sealed class ContextoMarkdown
{
    public string? Sha256 { get; set; }
    public string? Md5 { get; set; }
    public string? BlobPath { get; set; }

    /// <summary>Solo para el flujo legado sin blob. En blob-first va vacio.</summary>
    public string? DocumentoBase64 { get; set; }

    public string NombreDocumento { get; set; } = string.Empty;
    public string? Tipologia { get; set; }

    /// <summary>0 cuando no se conoce (Office: el recorte del paso 2.7 solo entiende PDF).</summary>
    public int TotalPaginas { get; set; }

    public bool ForceReprocess { get; set; }

    /// <summary>Instrucciones.Classification.Markdown. Gana siempre y no se persiste.</summary>
    public string? MarkdownCaller { get; set; }

    public ResultadoMarkdown? CacheEjecucion { get; set; }
}

public sealed class ObtenerMarkdownInput
{
    public NecesidadMarkdown Necesidad { get; set; } = NecesidadMarkdown.Completo();
    public ContextoMarkdown Contexto { get; set; } = new();
}

/// <summary>
/// Markdown que aporto otra actividad (clasificador DI/CU, extraccion CU) y que hay que
/// persistir con la misma regla de cobertura que el obtenido por Layout.
/// </summary>
public sealed class PersistirMarkdownInput
{
    public string? Sha256 { get; set; }
    public string? Markdown { get; set; }
    public int Paginas { get; set; }
    public bool Completo { get; set; }
    public bool Forzar { get; set; }
}
