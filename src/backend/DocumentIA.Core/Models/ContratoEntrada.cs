using System.Text.Json.Serialization;

namespace DocumentIA.Core.Models;

public class ContratoEntrada
{
    /// <summary>
    /// Instrucciones funcionales y técnicas que gobiernan clasificación, extracción e integración.
    /// </summary>
    public Instrucciones Instrucciones { get; set; } = new();

    /// <summary>
    /// Metadatos y contenido del documento a procesar.
    /// </summary>
    public Documento Documento { get; set; } = new();

    /// <summary>
    /// Información de correlación y auditoría de la petición.
    /// </summary>
    public Trazabilidad Trazabilidad { get; set; } = new();
}

public class Instrucciones
{
    /// <summary>
    /// Tipo documental esperado (hint del caller).
    /// Si se informa, puede influir en validaciones y decisiones de clasificación.
    /// </summary>
    public string ExpectedType { get; set; } = string.Empty;

    /// <summary>
    /// Activa modo "solo clasificación".
    /// En este modo se prioriza identificar tipología/familia y se limita o evita la extracción completa
    /// según la configuración y el tamaño del documento.
    /// </summary>
    public bool ClassificationOnly { get; set; }

    /// <summary>
    /// Override para ejecutar integración incluso en modo <see cref="ClassificationOnly"/>.
    /// true = forzar integración; false = no integrar; null = usar comportamiento por defecto.
    /// </summary>
    public bool? ExecuteIntegrarWhenClassificationOnly { get; set; }

    /// <summary>
    /// Máximo de páginas permitido para aplicar modo <see cref="ClassificationOnly"/>.
    /// Si el documento supera este valor, el pipeline puede cambiar a flujo completo o fallback definido.
    /// </summary>
    public int MaxPagesForClassificationOnly { get; set; }

    /// <summary>
    /// Omite la validación de duplicados antes de procesar.
    /// Útil para reintentos controlados o pruebas; aumenta riesgo de reprocesado de documentos ya tratados.
    /// </summary>
    public bool SkipDuplicateCheck { get; set; }

    /// <summary>
    /// Fuerza reproceso aunque existan marcas/estados previos de procesamiento.
    /// </summary>
    public bool ForceReprocess { get; set; }

    /// <summary>
    /// Fuerza la generación del resumen por defecto cuando aplique lógica de resumen.
    /// </summary>
    [JsonConverter(typeof(NullToFalseBooleanConverter))]
    public bool ForzarResumenPorDefecto { get; set; }

    /// <summary>
    /// Controla la subida del documento al GDC.
    /// true = omitir subida; false = forzar subida; null = respetar la configuración de tipología.
    /// </summary>
    public bool? SkipGDCUpload { get; set; }

    /// <summary>
    /// Configuración de IA para la etapa de clasificación.
    /// Permite seleccionar proveedor/modelo y umbrales específicos para esta fase.
    /// </summary>
    public ConfiguracionIA Classification { get; set; } = new();

    /// <summary>
    /// Configuración de IA para la etapa de extracción.
    /// Sus umbrales pueden influir en el umbral efectivo aplicado en runtime.
    /// </summary>
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

    /// <summary>
    /// Cuando es true, omite la validación de límite de páginas y procesa el documento completo.
    /// El llamador es responsable del coste adicional. Se registra traza de auditoría obligatoria.
    /// </summary>
    public bool ForzarProcesadoSinLimitePaginas { get; set; }
}

/// <summary>
/// Instrucciones opcionales para sobrescribir la configuración de prompt por petición.
/// Todos los campos son opcionales para mantener compatibilidad hacia atrás.
/// </summary>
public class PromptInstrucciones
{
    /// <summary>
    /// Prompt de sistema para guiar el comportamiento global del modelo.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Plantilla de prompt de usuario aplicada a la petición.
    /// </summary>
    public string? UserPromptTemplate { get; set; }

    /// <summary>
    /// Clave lógica del modelo a utilizar en la configuración del sistema.
    /// </summary>
    public string? ModelKey { get; set; }

    /// <summary>
    /// Temperatura de generación para el modelo (si el proveedor la soporta).
    /// null = usar configuración por defecto.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Límite máximo de tokens de salida para la respuesta del modelo.
    /// null = usar configuración por defecto.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Modo de contenido del prompt/respuesta (según convención del provider).
    /// </summary>
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
    /// <summary>
    /// Valor de IDUFIR para búsqueda exacta de activo en AssetResolver.
    /// </summary>
    public string? Idufir { get; set; }

    /// <summary>
    /// Referencia catastral para búsqueda de activo en AssetResolver.
    /// </summary>
    public string? ReferenciaCatastral { get; set; }
}

public class ConfiguracionIA
{
    /// <summary>
    /// Proveedor de IA a utilizar.
    /// Valores habituales: auto, azure-document-intelligence, mock.
    /// </summary>
    public string Provider { get; set; } = "auto"; // auto | azure-document-intelligence | mock

    /// <summary>
    /// Modelo lógico para la etapa.
    /// Valores habituales: DI, GPT, auto.
    /// </summary>
    public string Model { get; set; } = "auto"; // DI | GPT | auto

    /// <summary>
    /// Nivel de clasificación jerárquica solicitado para la etapa de clasificación.
    /// Valores permitidos: TDN1, TDN1_TDN2. null = usar default global configurado.
    /// </summary>
    public string? NivelClasificacion { get; set; }
    /// <summary>
    /// Umbral de confianza para esta etapa (legado, aplica a completitud y confianza si no se informan los específicos).
    /// Se usa como base para calcular el umbral efectivo/fallback cuando no hay overrides más específicos.
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
    /// <summary>
    /// Markdown del documento pre-procesado por el caller.
    /// Si se informa, el paso 2.8 (ExtraerMarkdownLayout) se omite y se usa este valor directamente.
    /// null/vacío = el sistema extrae el markdown mediante DI Layout.
    /// </summary>
    public string? Markdown { get; set; }
}

public class Documento
{
    /// <summary>
    /// Nombre lógico del documento (incluyendo extensión cuando aplique).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Identificador del objeto en GDC cuando el documento ya está almacenado allí.
    /// </summary>
    public string? ObjectIdGDC { get; set; }

    /// <summary>
    /// Ruta container/path en Blob Storage cuando el documento se procesa en modo blob-first.
    /// </summary>
    public string? BlobPath { get; set; }

    /// <summary>
    /// SHA-256 precomputado en el trigger para evitar recálculo en actividades.
    /// </summary>
    public string? PreComputedSHA256 { get; set; }

    /// <summary>
    /// MD5 precomputado en el trigger para validaciones de integridad/compatibilidad.
    /// </summary>
    public string? PreComputedMD5 { get; set; }

    /// <summary>
    /// CRC32 precomputado en el trigger para chequeos rápidos de consistencia.
    /// </summary>
    public string? PreComputedCRC32 { get; set; }

    /// <summary>
    /// Tamaño del documento en bytes precomputado en el trigger.
    /// </summary>
    public int PreComputedTamañoBytes { get; set; }

    /// <summary>
    /// Contenido binario del documento serializado en Base64 cuando se usa modo inline.
    /// </summary>
    public ContenidoDocumento Content { get; set; } = new();
}

public class ContenidoDocumento
{
    /// <summary>
    /// Documento codificado en Base64.
    /// Vacío cuando el flujo usa rutas externas (por ejemplo BlobPath u ObjectIdGDC).
    /// </summary>
    public string Base64 { get; set; } = string.Empty;
}

public class Trazabilidad
{
    /// <summary>
    /// Identificador de correlación funcional de la petición.
    /// Se propaga entre componentes para trazabilidad extremo a extremo.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Identificador del usuario/sistema que envía la petición.
    /// </summary>
    public string SubmittedBy { get; set; } = string.Empty;

    /// <summary>
    /// Identificador de activo de negocio asociado a la petición (si aplica).
    /// </summary>
    public string? IdActivo { get; set; }

    /// <summary>W3C TraceId capturado en el trigger HTTP (operation_Id de App Insights). Propagado al output para facilitar correlación en Insights.</summary>
    public string? OperationId { get; set; }
}
