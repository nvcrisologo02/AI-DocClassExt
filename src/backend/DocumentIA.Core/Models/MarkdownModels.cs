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
