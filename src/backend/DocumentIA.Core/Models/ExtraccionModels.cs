namespace DocumentIA.Core.Models;

public class ExtraccionInput
{
    public ContratoEntrada Entrada { get; set; } = new();
    public string Tipologia { get; set; } = string.Empty;
    public Dictionary<string, object> DatosNormalizados { get; set; } = new();
}

public class ExtraccionResultado
{
    public string Proveedor { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public bool LayoutEnabled { get; set; }
    public string? OperationId { get; set; }
    public int Paginas { get; set; }
    public bool FallbackUsado { get; set; }
    public string? FallbackRazon { get; set; }
    public string? MarkdownExtraido { get; set; }
    public Dictionary<string, int> TiemposMs { get; set; } = new();
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    /// <summary>
    /// Resultado del prompt libre cuando se ejecutó en modo combinado con el fallback de extracción
    /// (una única llamada LLM que realizó extracción + prompt a la vez). Null en caso contrario.
    /// </summary>
    public string? ResultadoPromptCombinado { get; set; }
}

/// <summary>
/// Input para la actividad de ejecución del prompt libre de tipología.
/// Puede contener el markdown ya extraído (modo markdown) o los bytes del documento (modo vision).
/// Si ResultadoPromptCombinado no es null, la actividad reutiliza ese resultado sin llamar al LLM.
/// </summary>
public class PromptActivityInput
{
    public string Tipologia { get; set; } = string.Empty;
    /// <summary>Markdown extraído en el paso de extracción previo (si existe).</summary>
    public string? MarkdownExtraido { get; set; }
    /// <summary>Documento en base64 para modo vision (cuando no hay markdown disponible).</summary>
    public string? DocumentoBase64 { get; set; }
    public string? ContentType { get; set; }
    /// <summary>
    /// Campos ya extraídos. Se usan para resolver los placeholders {campo:NombreCampo} del template.
    /// Solo están disponibles en flujo secuencial (extracción primero, prompt después).
    /// </summary>
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    /// <summary>
    /// Cuando viene con valor (modo combinado con fallback), la actividad devuelve este resultado
    /// directamente sin realizar ninguna llamada adicional al LLM.
    /// </summary>
    public string? ResultadoPromptCombinado { get; set; }
}

/// <summary>
/// Input para extraer markdown con DI prebuilt-layout antes del prompt
/// en escenarios en los que no se ejecuta extracción de negocio.
/// </summary>
public class ExtraerMarkdownLayoutInput
{
    public string Tipologia { get; set; } = string.Empty;
    public string DocumentoBase64 { get; set; } = string.Empty;
    public string NombreDocumento { get; set; } = string.Empty;
}

public class ExtraerMarkdownLayoutResultado
{
    public string Modelo { get; set; } = "prebuilt-layout";
    public string? Markdown { get; set; }
    public int Paginas { get; set; }
}
