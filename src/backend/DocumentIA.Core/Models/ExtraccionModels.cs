namespace DocumentIA.Core.Models;

public class ExtraccionInput
{
    public ContratoEntrada Entrada { get; set; } = new();
    public string Tipologia { get; set; } = string.Empty;
    public Dictionary<string, object> DatosNormalizados { get; set; } = new();
    /// <summary>
    /// Umbral de fallback efectivo resuelto por el orquestador (legado, aplica a ambos criterios si los específicos son null).
    /// Cadena: instrucciones.Extraction.Umbral ?? tipología.ExtracUmbralFallback ?? config.MinFieldsRatio.
    /// null = usar config.MinFieldsRatio directamente en el proveedor.
    /// </summary>
    public double? UmbralFallbackEfectivo { get; set; }
    /// <summary>
    /// Umbral de completitud de extracción CU resuelto por el orquestador para esta petición.
    /// Precede sobre tipología. null = usa tipología o UmbralFallbackEfectivo como fallback.
    /// </summary>
    public double? UmbralFallbackEfectivoCompletitud { get; set; }
    /// <summary>
    /// Umbral de confianza global de extracción CU resuelto por el orquestador para esta petición.
    /// Precede sobre tipología. null = usa tipología o UmbralFallbackEfectivo como fallback.
    /// </summary>
    public double? UmbralFallbackEfectivoConfianza { get; set; }
    /// <summary>
    /// Provider de extracción efectivo resuelto por el orquestador.
    /// Viene de instrucciones.Extraction.Provider si no es "auto" ni vacío; de lo contrario null (usa config de tipología).
    /// </summary>
    public string? ProviderEfectivo { get; set; }
    /// <summary>
    /// Model key de extracción efectivo resuelto por el orquestador.
    /// Viene de instrucciones.Extraction.Model si no es "auto" ni vacío; de lo contrario null (usa config de tipología).
    /// </summary>
    public string? ModelKeyEfectivo { get; set; }
    public bool GenerarResumenPorDefecto { get; set; }
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
    /// <summary>Confianza calculada para la extracción (0-1). Calculada por ConfidenceCalculator.</summary>
    public double ConfianzaExtraccion { get; set; }
    /// <summary>Proveedor que realizó la extracción: "AzureContentUnderstanding" | "DICustom" | "GPT4oMini".</summary>
    public string ProveedorExtrac { get; set; } = string.Empty;
    /// <summary>Métricas de debug del cálculo de confianza de extracción. Null si no se calculó.</summary>
    public ConfidenceMetricasExtraccion? MetricasDebug { get; set; }
    public Dictionary<string, int> TiemposMs { get; set; } = new();
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    /// <summary>
    /// Resultado del prompt libre cuando se ejecutó en modo combinado con el fallback de extracción
    /// (una única llamada LLM que realizó extracción + prompt a la vez). Null en caso contrario.
    /// </summary>
    public string? ResultadoPromptCombinado { get; set; }
    public string? ResumenCombinado { get; set; }
}

public class ConfidenceMetricasExtraccion
{
    public double PromedioConfianza { get; set; }
    public double RatioRequeridos { get; set; }
    public int CamposConConfianza { get; set; }
    public int CamposTotales { get; set; }
    public Dictionary<string, double> ConfianzaPorCampo { get; set; } = new();
    public List<string> CamposBajaConfianza { get; set; } = new();
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
    public string? ResumenCombinado { get; set; }
    public bool ForzarResumenPorDefecto { get; set; }
    /// <summary>
    /// Override opcional de prompt para esta petición.
    /// Si no se informa, se usa PromptConfig de la tipología.
    /// </summary>
    public PromptInstrucciones? Prompt { get; set; }
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
    public string? BlobPath { get; set; }
}

public class ExtraerMarkdownLayoutResultado
{
    public string Modelo { get; set; } = "prebuilt-layout";
    public string? Markdown { get; set; }
    public int Paginas { get; set; }
}
