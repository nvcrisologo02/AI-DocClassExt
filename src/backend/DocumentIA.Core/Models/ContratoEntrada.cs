namespace DocumentIA.Core.Models;

public class ContratoEntrada
{
    public Instrucciones Instrucciones { get; set; } = new();
    public Documento Documento { get; set; } = new();
    public Trazabilidad Trazabilidad { get; set; } = new();
}

public class Instrucciones
{
    public string ExpectedType { get; set; } = string.Empty;
    public bool SkipDuplicateCheck { get; set; }
    public bool ForceReprocess { get; set; }
    /// <summary>
    /// Si true, se ejecuta solo clasificación y se omiten extracción/validación/asset resolver.
    /// Incompatible con ExpectedType.
    /// </summary>
    public bool ClassificationOnly { get; set; }
    /// <summary>
    /// Override para ejecutar Integrar cuando ClassificationOnly=true.
    /// null = comportamiento por defecto (false).
    /// </summary>
    public bool? ExecuteIntegrarWhenClassificationOnly { get; set; }
    // Controla si se sube el documento al GDC. Si no se especifica (null), se usa el valor por defecto
    // configurado en la tipología detectada (tipologiaConfig.SkipGDCUpload).
    // true = omitir subida; false = forzar subida; null = respetar config de tipología.
    public bool? SkipGDCUpload { get; set; }
    public ConfiguracionIA Classification { get; set; } = new();
    public ConfiguracionIA Extraction { get; set; } = new();
    /// <summary>
    /// Configuración de AssetResolver para esta petición.
    /// null = usar config de tipología; si se informa, sus valores tienen precedencia.
    /// </summary>
    public AssetResolverInstrucciones? AssetResolver { get; set; }
    /// <summary>
    /// Override opcional de prompt para esta petición.
    /// Si no se informa, se mantiene la configuración de prompt de la tipología.
    /// </summary>
    public PromptInstrucciones? Prompt { get; set; }
}

/// <summary>
/// Instrucciones opcionales para sobrescribir la configuración de prompt por petición.
/// Todos los campos son opcionales para mantener compatibilidad hacia atrás.
/// </summary>
public class PromptInstrucciones
{
    public string? SystemPrompt { get; set; }
    public string? UserPromptTemplate { get; set; }
    public string? ModelKey { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public string? ContentMode { get; set; }
}

/// <summary>
/// Instrucciones de AssetResolver por petición. Permite activar/desactivar y especificar
/// campos de búsqueda y columnas solicitadas con precedencia sobre la tipología.
/// </summary>
public class AssetResolverInstrucciones
{
    /// <summary>
    /// true = ejecutar AssetResolver; false = omitir; null = respetar config de tipología.
    /// </summary>
    public bool? Enabled { get; set; }
    /// <summary>
    /// Campos de cruce explícitos. Si se informan, tienen precedencia sobre la auto-detección
    /// por aliases configurados en la tipología.
    /// </summary>
    public CamposBusquedaActivo? CamposBusqueda { get; set; }
    /// <summary>
    /// Columnas de DM_POSICION_AAII_TB a devolver además de las obligatorias (ID_ACTIVO_SAREB, FCH_CIERRE).
    /// null = usar las definidas en tipología; lista vacía = solo obligatorias.
    /// </summary>
    public List<string>? CamposSolicitados { get; set; }
}

public class CamposBusquedaActivo
{
    public string? Idufir { get; set; }
    public string? ReferenciaCatastral { get; set; }
}

public class ConfiguracionIA
{
    public string Provider { get; set; } = "auto"; // auto | azure-document-intelligence | mock
    public string Model { get; set; } = "auto"; // DI | GPT | auto
    /// <summary>
    /// Umbral de confianza para esta etapa (legado, aplica a completitud y confianza si no se informan los específicos).
    /// null = usar el valor configurado en la tipología o en el servidor.
    /// </summary>
    public double? Umbral { get; set; } = null;
    /// <summary>
    /// Umbral de completitud de extracción CU (ratio de campos esperados presentes) para esta petición.
    /// Tiene precedencia sobre tipología y sobre Umbral. null = usar tipología o global.
    /// Rango [0.0–1.0].
    /// </summary>
    public double? UmbralCompletitud { get; set; } = null;
    /// <summary>
    /// Umbral de confianza global de extracción CU para esta petición.
    /// Tiene precedencia sobre tipología y sobre Umbral. null = usar tipología o global.
    /// Rango [0.0–1.0].
    /// </summary>
    public double? UmbralConfianza { get; set; } = null;
}

public class Documento
{
    public string Name { get; set; } = string.Empty;
    public string? ObjectIdGDC { get; set; }
    public ContenidoDocumento Content { get; set; } = new();
}

public class ContenidoDocumento
{
    public string Base64 { get; set; } = string.Empty;
}

public class Trazabilidad
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string SubmittedBy { get; set; } = string.Empty;
    public string? IdActivo { get; set; }
    /// <summary>W3C TraceId capturado en el trigger HTTP (operation_Id de App Insights). Propagado al output para facilitar correlación en Insights.</summary>
    public string? OperationId { get; set; }
}

public class ObtenerUltimaEjecucionDuplicadoInput
{
    public string SHA256 { get; set; } = string.Empty;
    public bool ClassificationOnly { get; set; }
}
