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
    public string? ModelKeyEfectivo { get; set; }
    public string? EndpointEfectivo { get; set; }
    public string? ProcessingLocationEfectiva { get; set; }
    public bool LayoutEnabled { get; set; }
    public string? OperationId { get; set; }
    public int Paginas { get; set; }
    public bool FallbackUsado { get; set; }
    public string? FallbackRazon { get; set; }
    public string? MarkdownExtraido { get; set; }
    /// <summary>Confianza calculada para la extracción (0-1). Calculada por ConfidenceCalculator.</summary>
    public double ConfianzaExtraccion { get; set; }
    /// <summary>
    /// True cuando la llamada GPT (fallback o directa) agotó su propio TimeoutSeconds y se devolvió
    /// un resultado controlado (sin datos) en lugar de lanzar (AB#100130). Señaliza al orquestador
    /// que la extracción NO se realizó (no que se realizó con confianza 0): debe excluirse del
    /// cálculo de ConfianzaGlobal igual que cuando Extraction.Enabled=false, en vez de forzar
    /// ConfianzaGlobal=0 y por tanto EstadoCalidad="ERROR" de forma artificial.
    /// </summary>
    public bool ExtraccionTimeoutPropio { get; set; }
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

    /// <summary>
    /// Consumo de servicios de IA de esta llamada. El proveedor lo rellena y el
    /// orquestador lo acumula en DetalleEjecucion.Costes. Vacia cuando el paso no
    /// consumio IA.
    /// </summary>
    public List<ConsumoIA> Consumos { get; set; } = new();
}

public class ConfidenceMetricasExtraccion
{
    public double PromedioConfianza { get; set; }
    public double RatioRequeridos { get; set; }
    public int CamposConConfianza { get; set; }
    public int CamposTotales { get; set; }
    public Dictionary<string, double> ConfianzaPorCampo { get; set; } = new();
    public List<string> CamposBajaConfianza { get; set; } = new();
    public List<string>? CamposExcluidosConfianza { get; set; }
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

    /// <summary>
    /// Primeras N paginas a analizar. null = documento entero. Solo tiene efecto en PDF y TIFF;
    /// en el resto de formatos se analiza el documento entero (AB#100249).
    /// </summary>
    public int? PaginasSolicitadas { get; set; }
}

public class ExtraerMarkdownLayoutResultado
{
    public string Modelo { get; set; } = "prebuilt-layout";
    public string? Markdown { get; set; }
    public int Paginas { get; set; }

    /// <summary>
    /// El analisis se restringio de verdad a las primeras N paginas. False cuando no se
    /// pidio recorte o cuando el formato no lo admite y se analizo el documento entero
    /// (AB#100249).
    /// </summary>
    public bool RangoAplicado { get; set; }

    /// <summary>
    /// Consumo de servicios de IA de esta llamada. El proveedor lo rellena y el
    /// orquestador lo acumula en DetalleEjecucion.Costes. Vacia cuando el paso no
    /// consumio IA.
    /// </summary>
    public List<ConsumoIA> Consumos { get; set; } = new();
}

/// <summary>
/// Input para recuperar markdown ya persistido en BD (Documentos.NormalizacionMarkdownCompressed)
/// como respaldo cuando la extraccion de markdown DI Layout previa a clasificacion falla o no
/// devuelve contenido util.
/// </summary>
public class RecuperarMarkdownPersistidoInput
{
    public string? Sha256 { get; set; }
    public string? Md5 { get; set; }
    public string NombreDocumento { get; set; } = string.Empty;
}

public class RecuperarMarkdownPersistidoResultado
{
    public bool Encontrado { get; set; }
    public string? Markdown { get; set; }
    public int? DocumentoId { get; set; }
}
