using System.Text.Json.Serialization;

namespace DocumentIA.Core.Models;

public class ContratoSalida
{
    /// <summary>
    /// Identificación documental resultante del procesamiento (tipología, fecha, páginas, etc.).
    /// </summary>
    public Identificacion Identificacion { get; set; } = new();

    /// <summary>
    /// Metadatos de integridad y huellas del documento procesado.
    /// </summary>
    public Integridad Integridad { get; set; } = new();

    /// <summary>
    /// Diccionario final de campos extraídos normalizados.
    /// </summary>
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();

    /// <summary>
    /// Detalle técnico de la ejecución (clasificación, extracción, integración y trazas).
    /// </summary>
    public DetalleEjecucion DetalleEjecucion { get; set; } = new();

    /// <summary>
    /// Resultado agregado de calidad/estado del proceso.
    /// </summary>
    public ResultadoFinal Resultado { get; set; } = new();
    /// <summary>
    /// Resultado del prompt libre definido en la configuración de la tipología.
    /// Null cuando la tipología no tiene prompt habilitado.
    /// </summary>
    //public PromptResultado? ResultadoPrompt { get; set; }
}

public class Identificacion
{
    /// <summary>
    /// Nombre del documento procesado.
    /// </summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>
    /// Identificador único lógico de la ejecución/documento en salida.
    /// </summary>
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>
    /// Tipología final detectada.
    /// </summary>
    public string Tipologia { get; set; } = string.Empty;

    /// <summary>
    /// Familia funcional de la tipología detectada.
    /// </summary>
    public string TipologiaFamilia { get; set; } = string.Empty;

    /// <summary>
    /// Versión de la tipología usada en la ejecución.
    /// </summary>
    public string TipologiaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Marca temporal UTC del procesamiento.
    /// </summary>
    public DateTime FechaProceso { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Número de páginas del documento.
    /// </summary>
    public int Paginas { get; set; }

    /// <summary>
    /// Código TDN nivel 1 detectado (si aplica).
    /// </summary>
    public string? Tdn1 { get; set; }

    /// <summary>
    /// Código TDN nivel 2 detectado (si aplica).
    /// </summary>
    public string? Tdn2 { get; set; }

    /// <summary>
    /// Matrícula/identificador registral detectado (si aplica).
    /// </summary>
    public string? Matricula { get; set; }
    /// <summary>
    /// Propuesta de tipología libre cuando GPT no pudo mapear a un código de catálogo.
    /// Solo se informa cuando Tipologia="Desconocido" (tipología virtual).
    /// </summary>
    public string? PropuestaTipologia { get; set; }
}

public class Integridad
{
    /// <summary>
    /// Huella CRC32 del documento procesado.
    /// </summary>
    public string CRC32 { get; set; } = string.Empty;

    /// <summary>
    /// Huella SHA-256 del documento procesado.
    /// </summary>
    public string SHA256 { get; set; } = string.Empty;

    /// <summary>
    /// Huella MD5 del documento procesado.
    /// </summary>
    public string MD5 { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño del documento en bytes.
    /// </summary>
    public long TamanoBytes { get; set; }

    /// <summary>
    /// Ruta completa en blob (container/path) para relacionar documento lógico con almacenamiento físico.
    /// </summary>
    public string? RutaBlobStorage { get; set; }

    /// <summary>
    /// Identificador de gestor documental de destino/origen (si aplica).
    /// </summary>
    public string? GestorDocumental { get; set; }

    /// <summary>
    /// IdActivo final tras integración/enriquecimiento.
    /// </summary>
    public string? IdActivo { get; set; }

    /// <summary>
    /// IdActivo recibido originalmente en entrada.
    /// </summary>
    public string? IdActivoEntrada { get; set; }

    /// <summary>
    /// Indica si el IdActivo final difiere del informado en entrada.
    /// </summary>
    public bool IdActivoCambiado { get; set; }
}

public class DetalleEjecucion
{
    /// <summary>ID de la instancia de orquestación Durable Functions. Permite localizar la ejecución en Azure Portal y mediante polling.</summary>
    public string? InstanceId { get; set; }
    /// <summary>operation_Id de Application Insights (W3C TraceId). Usar en KQL: union traces,requests | where operation_Id == OperationId.</summary>
    public string? OperationId { get; set; }
    /// <summary>
    /// Indica si la petición se ejecutó en modo solo clasificación.
    /// </summary>
    public bool ClassificationOnly { get; set; }
    /// <summary>Nivel de clasificación usado en la petición (TDN1, TDN1/TDN2, etc.). Forma parte de la clave de deduplicación cuando se informa.</summary>
    public string? NivelClasificacion { get; set; }
    /// <summary>
    /// Tipología ejecutada en runtime (puede diferir de la esperada en entrada).
    /// </summary>
    public string RunTipologia { get; set; } = string.Empty;

    /// <summary>
    /// Resultado detallado de la etapa de clasificación.
    /// </summary>
    public ResultadoClasificacion Clasificacion { get; set; } = new();

    /// <summary>
    /// Resultado detallado de la etapa de extracción.
    /// </summary>
    public ResultadoExtraccion Extraccion { get; set; } = new();

    /// <summary>
    /// Información del postproceso (normalización, validaciones e inconsistencias).
    /// </summary>
    public InformacionPostproceso Postproceso { get; set; } = new();

    /// <summary>
    /// Resultado de la etapa de integración/plugins.
    /// </summary>
    public ResultadoIntegracion Integracion { get; set; } = new();

    /// <summary>
    /// Resultado de la interacción con el Gestor Documental (GDC).
    /// </summary>
    public ResultadoGDC GDC { get; set; } = new();

    /// <summary>
    /// Resultado de la consulta AssetResolver (DM_POSICION_AAII_TB).
    /// Null cuando la actividad no está habilitada para la tipología/petición.
    /// </summary>
    public ResultadoAssetResolver? AssetResolver { get; set; }

    /// <summary>
    /// Seguimiento estructurado de actividades para monitorización en tiempo real y post-ejecución.
    /// </summary>
    public SeguimientoOrquestacion Seguimiento { get; set; } = new();
    /// <summary>Información de ejecución del prompt libre (cuando la tipología lo tiene habilitado).</summary>
    public ResultadoPromptEjecucion? Prompt { get; set; }

    // NUEVO: Trazabilidad de recorte, markdown y modelo LLM
    /// <summary>Indica si se aplicó recorte de páginas para clasificación.</summary>
    public bool RecorteAplicado { get; set; }
    /// <summary>Número de páginas incluidas tras recorte (si aplica).</summary>
    public int PaginasIncluidas { get; set; }
    /// <summary>Indica si se generó markdown en algún paso del flujo.</summary>
    public bool MarkdownGenerado { get; set; }
    /// <summary>Origen del markdown generado ("Clasificacion", "Extraccion", "Fallback", etc.).</summary>
    public string? OrigenMarkdown { get; set; }
    /// <summary>Modelo LLM usado en clasificación o prompt (si aplica).</summary>
    public string? ModeloLLMUsado { get; set; }
    /// <summary>Motivo de error en la resolución de tipología (si aplica).</summary>
    public string? MotivoErrorTipologia { get; set; }
}

public class ResultadoPromptEjecucion
{
    /// <summary>
    /// Modelo empleado para ejecutar el prompt libre.
    /// </summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>
    /// Duración de ejecución del prompt en milisegundos.
    /// </summary>
    public int TiempoMs { get; set; }

    /// <summary>
    /// true cuando el prompt se resolvió en llamada combinada con fallback de extracción.
    /// </summary>
    public bool CombinedWithFallback { get; set; }

    /// <summary>
    /// Mensaje de error de la ejecución del prompt, si existe.
    /// </summary>
    public string? Error { get; set; }
}

public class SeguimientoOrquestacion
{
    /// <summary>
    /// Versión del esquema de seguimiento.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Estado agregado de la orquestación (Pending, Running, Completed, Failed, etc.).
    /// </summary>
    public string Estado { get; set; } = "Pending";

    /// <summary>
    /// Nombre de la actividad en curso.
    /// </summary>
    public string ActividadActual { get; set; } = string.Empty;

    /// <summary>
    /// Número total de actividades planificadas.
    /// </summary>
    public int ActividadesTotales { get; set; }

    /// <summary>
    /// Lista de nombres de actividades completadas.
    /// </summary>
    public List<string> ActividadesCompletadas { get; set; } = new();

    /// <summary>
    /// Duración total acumulada de la ejecución en milisegundos.
    /// </summary>
    public int DuracionTotalMs { get; set; }

    /// <summary>
    /// Timeline detallado por actividad.
    /// </summary>
    public List<TrazaActividad> Actividades { get; set; } = new();
}

public class TrazaActividad
{
    /// <summary>
    /// Nombre de la actividad de orquestación.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Estado de la actividad: Pending, Running, Completed, Failed, Skipped o Timeout.
    /// </summary>
    public string Estado { get; set; } = "Pending"; // Pending | Running | Completed | Failed | Skipped | Timeout

    /// <summary>
    /// Fecha/hora UTC de inicio de la actividad.
    /// </summary>
    public DateTime InicioUtc { get; set; }

    /// <summary>
    /// Fecha/hora UTC de fin de la actividad, si terminó.
    /// </summary>
    public DateTime? FinUtc { get; set; }

    /// <summary>
    /// Duración de la actividad en milisegundos.
    /// </summary>
    public int DuracionMs { get; set; }

    /// <summary>
    /// Mensaje descriptivo de estado/error de la actividad.
    /// </summary>
    public string? Mensaje { get; set; }

    /// <summary>
    /// Indica si durante esta actividad se activó fallback.
    /// </summary>
    public bool FallbackActivado { get; set; }

    /// <summary>
    /// Motivo del fallback cuando aplica.
    /// </summary>
    public string? FallbackRazon { get; set; }
}

public class ResultadoClasificacion
{
    /// <summary>
    /// Modelo utilizado para clasificación.
    /// </summary>
    public string Modelo { get; set; } = string.Empty;
    /// <summary>Confianza final usada en ConfianzaGlobal (DI primario, o GPT si hubo fallback).</summary>
    public double Confianza { get; set; }
    /// <summary>Confianza bruta de Azure Document Intelligence (0 si no se ejecutó).</summary>
    public double ConfianzaDI { get; set; }
    /// <summary>Confianza reportada por GPT fallback (0 si no se activó).</summary>
    public double ConfianzaGPT { get; set; }
    /// <summary>Proveedor que produjo la clasificación final: "DocumentIntelligence" | "GPT4oMini".</summary>
    public string ProveedorClasif { get; set; } = string.Empty;
    /// <summary>
    /// Indica si se utilizó fallback LLM para clasificación.
    /// </summary>
    public bool FallbackLLM { get; set; }
    /// <summary>
    /// Motivo por el que se activó o se informó fallback.
    /// </summary>
    public string? FallbackRazon { get; set; }
    /// <summary>
    /// Umbral de fallback de clasificación aplicado en esta ejecución.
    /// </summary>
    public double? UmbralFallbackAplicado { get; set; }
    /// <summary>
    /// Tipología detectada por el proveedor ganador.
    /// </summary>
    public string? TipologiaDetectada { get; set; }
    /// <summary>
    /// Indica si la clasificación quedó parcial/incompleta.
    /// </summary>
    public bool ClasificacionParcial { get; set; }
    /// <summary>
    /// Propuesta libre de tipología cuando no existe mapeo final de catálogo.
    /// </summary>
    public string PropuestaTipologia { get; set; } = string.Empty;

    /// <summary>Texto extraído por DI durante la clasificación. El orquestador lo usa para propagar a DatosNormalizados["Markdown"] y luego lo limpia antes de incluirlo en la respuesta.</summary>
    public string? ContentExtraido { get; set; }
    /// <summary>
    /// URI de evidencia del proveedor (si aplica).
    /// </summary>
    public string? EvidenceUri { get; set; }
    /// <summary>
    /// Versión del clasificador/modelo reportada por el proveedor.
    /// </summary>
    public string? ClassifierVersion { get; set; }
    /// <summary>
    /// Páginas procesadas durante clasificación.
    /// </summary>
    public int PagesProcessed { get; set; }
    /// <summary>
    /// Nombre lógico del clasificador usado.
    /// </summary>
    public string? Clasificador { get; set; }
    /// <summary>
    /// Detalle de candidatos/proveedores evaluados y descartes.
    /// </summary>
    public List<PropuestaProveedor> DetalleProveedores { get; set; } = new();
    /// <summary>
    /// Resultado de prompt libre en ejecución combinada con fallback.
    /// </summary>
    public string? ResultadoPromptCombinado { get; set; }
    /// <summary>
    /// Resumen resultante de ejecución combinada.
    /// </summary>
    public string? ResumenCombinado { get; set; }
}

public class PropuestaProveedor
{
    /// <summary>
    /// Proveedor evaluado en clasificación.
    /// </summary>
    public string Proveedor { get; set; } = string.Empty;
    /// <summary>
    /// Tipología propuesta por ese proveedor.
    /// </summary>
    public string? Tipologia { get; set; }
    /// <summary>
    /// Confianza reportada por ese proveedor.
    /// </summary>
    public double Confianza { get; set; }
    /// <summary>
    /// Motivo de descarte frente al proveedor final.
    /// </summary>
    public string? MotivoDescarte { get; set; }
}

public class ResultadoExtraccion
{
    /// <summary>
    /// Modelo usado en extracción.
    /// </summary>
    public string Modelo { get; set; } = string.Empty;
    /// <summary>
    /// Model key efectivo resuelto para la llamada de extracción.
    /// </summary>
    public string? ModelKeyEfectivo { get; set; }
    /// <summary>
    /// Endpoint efectivo usado por el proveedor de extracción.
    /// </summary>
    public string? EndpointEfectivo { get; set; }
    /// <summary>
    /// Processing location efectiva de la llamada de extracción.
    /// </summary>
    public string? ProcessingLocationEfectiva { get; set; }
    /// <summary>Confianza calculada para la extracción (0-1). 0 cuando la extracción está deshabilitada.</summary>
    public double ConfianzaExtraccion { get; set; }
    /// <summary>Proveedor que realizó la extracción: "AzureContentUnderstanding" | "DICustom" | "GPT4oMini".</summary>
    public string ProveedorExtrac { get; set; } = string.Empty;
    /// <summary>
    /// Indica si se habilitó layout/document parsing de apoyo.
    /// </summary>
    public bool LayoutEnabled { get; set; }
    /// <summary>
    /// Indica si se ejecutó fallback de extracción.
    /// </summary>
    public bool FallbackUsado { get; set; }
    /// <summary>
    /// Motivo del fallback de extracción.
    /// </summary>
    public string? FallbackRazon { get; set; }
    /// <summary>Confianzas individuales reportadas por el proveedor de extracción por campo normalizado.</summary>
    public Dictionary<string, double> ConfianzaPorCampo { get; set; } = new();
    /// <summary>Campos cuya confianza de extracción quedó por debajo del umbral efectivo de extracción.</summary>
    public List<string> CamposConDuda { get; set; } = new();
    /// <summary>
    /// Duración por sub-etapa de extracción (ms).
    /// </summary>
    public Dictionary<string, int> TiemposMs { get; set; } = new();
}

public class InformacionPostproceso
{
    /// <summary>
    /// Normalizaciones aplicadas durante postproceso.
    /// </summary>
    public List<string> Normalizaciones { get; set; } = new();
    /// <summary>
    /// Markdown final usado en postproceso/validación, si existe.
    /// </summary>
    public string? Markdown { get; set; }
    /// <summary>
    /// Validaciones ejecutadas en postproceso.
    /// </summary>
    public List<string> Validaciones { get; set; } = new();
    /// <summary>
    /// Inconsistencias detectadas por reglas de negocio.
    /// </summary>
    public List<string> Inconsistencias { get; set; } = new();
    /// <summary>Confianza de validación calculada por el motor de reglas (1 - errores/reglasRequeridas).</summary>
    public double ConfianzaValidacion { get; set; }
}

public class IntegrarInput
{
    /// <summary>
    /// Tipología para la cual se ejecuta la integración.
    /// </summary>
    public string Tipologia { get; set; } = string.Empty;
    /// <summary>
    /// Identificador lógico del documento en el proceso.
    /// </summary>
    public string DocumentoId { get; set; } = string.Empty;
    /// <summary>
    /// Datos extraídos que se entregan a la cadena de plugins.
    /// </summary>
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    /// <summary>
    /// Metadatos de contexto para integración.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    /// <summary>
    /// IdActivo de trazabilidad de entrada. Puede estar vacío si el plugin de enriquecimiento lo retorna.
    /// </summary>
    public string? IdActivo { get; set; }
}

public class ResultadoIntegracion
{
    /// <summary>
    /// Tipología integrada.
    /// </summary>
    public string Tipologia { get; set; } = string.Empty;
    /// <summary>
    /// Estado agregado de integración: OK, REVISION o ERROR.
    /// </summary>
    public string Estado { get; set; } = "OK"; // OK | REVISION | ERROR
    /// <summary>
    /// Mensaje resumen del resultado de integración.
    /// </summary>
    public string Mensaje { get; set; } = string.Empty;
    /// <summary>
    /// Marca temporal del resultado de integración.
    /// </summary>
    public DateTime Timestamp { get; set; }
    /// <summary>
    /// Resultado individual de ejecución por plugin.
    /// </summary>
    public List<PluginExecutionResult> Plugins { get; set; } = new();
    /// <summary>
    /// Datos antes del enriquecimiento de plugins.
    /// </summary>
    public Dictionary<string, object> DatosOriginales { get; set; } = new();
    /// <summary>
    /// Datos finales tras ejecución de plugins.
    /// </summary>
    public Dictionary<string, object> DatosFinales { get; set; } = new();
    /// <summary>
    /// IdActivo recibido en la entrada de integración, antes de cualquier enriquecimiento.
    /// </summary>
    public string? IdActivoEntrada { get; set; }
    /// <summary>
    /// IdActivo resuelto tras la integración: el devuelto por un plugin (clave "idActivo" en DatosFinales)
    /// o en su defecto el que venía en la entrada. Null si ningún plugin lo proporcionó y no vino en entrada.
    /// </summary>
    public string? IdActivoResuelto { get; set; }
    /// <summary>
    /// True cuando el IdActivo informado en entrada existe y el resuelto tras integración no coincide.
    /// </summary>
    public bool IdActivoCambiado { get; set; }
}

public class PluginExecutionResult
{
    /// <summary>
    /// Clave del plugin ejecutado.
    /// </summary>
    public string PluginKey { get; set; } = string.Empty;
    /// <summary>
    /// Prioridad de ejecución del plugin.
    /// </summary>
    public int Priority { get; set; }
    /// <summary>
    /// true si la ejecución del plugin terminó correctamente.
    /// </summary>
    public bool Success { get; set; }
    /// <summary>
    /// Mensaje de resultado devuelto por el plugin.
    /// </summary>
    public string Mensaje { get; set; } = string.Empty;
    /// <summary>
    /// Código de estado funcional/técnico del plugin.
    /// </summary>
    public int StatusCode { get; set; }
    /// <summary>
    /// Duración del plugin en milisegundos.
    /// </summary>
    public int DurationMs { get; set; }
    /// <summary>
    /// Detalle de error cuando el plugin falla.
    /// </summary>
    public string? Error { get; set; }
    /// <summary>
    /// Datos enriquecidos aportados por el plugin (si aplica).
    /// </summary>
    public Dictionary<string, object>? DatosEnriquecidos { get; set; }
}

public class ResultadoFinal
{
    /// <summary>
    /// Estado final global del proceso.
    /// </summary>
    public string Estado { get; set; } = "OK";
    /// <summary>Detalle del motivo cuando Estado = ERROR.</summary>
    public string? MensajeError { get; set; }
    /// <summary>
    /// Confianza global final (normalmente mínimo entre clasificación, extracción y validación).
    /// </summary>
    public double ConfianzaGlobal { get; set; }
    /// <summary>Estado de calidad basado en umbrales de confianza: OK | REVISION | ERROR.</summary>
    public string EstadoCalidad { get; set; } = string.Empty;
    /// <summary>Componentes del MIN usado para ConfianzaGlobal.</summary>
    public double ConfianzaClasificacion { get; set; }

    /// <summary>
    /// Componente de confianza de extracción usado en el cálculo de confianza global.
    /// </summary>
    public double ConfianzaExtraccion { get; set; }

    /// <summary>
    /// Componente de confianza de validación usado en el cálculo de confianza global.
    /// </summary>
    public double ConfianzaValidacion { get; set; }
    /// <summary>
    /// Indica si se devolvió una respuesta reutilizada por deduplicación.
    /// </summary>
    public bool ReutilizadaPorDuplicado { get; set; }
    /// <summary>
    /// Mensaje explicativo de la reutilización por duplicado.
    /// </summary>
    public string? MensajeReutilizacion { get; set; }
}

/// <summary>
/// Input necesarios para subir un documento al GDC
/// </summary>
public class SubirGDCInput
{
    /// <summary>
    /// Identificador de activo para alta documental en GDC.
    /// </summary>
    public string IdActivo { get; set; } = string.Empty;
    /// <summary>
    /// Matrícula asociada al activo/documento.
    /// </summary>
    public string Matricula { get; set; } = string.Empty;
    /// <summary>
    /// Contenido del documento en Base64 (modo inline).
    /// </summary>
    public string ContenidoBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Ruta container/path en blob para escenarios blob-first (alternativa a ContenidoBase64).
    /// </summary>
    public string? BlobPath { get; set; }
    /// <summary>
    /// Nombre físico del archivo a subir.
    /// </summary>
    public string NombreArchivo { get; set; } = string.Empty;
    /// <summary>
    /// Huella SHA-256 del archivo.
    /// </summary>
    public string SHA256 { get; set; } = string.Empty;
    /// <summary>
    /// Huella MD5 del archivo.
    /// </summary>
    public string MD5 { get; set; } = string.Empty;
    /// <summary>
    /// CorrelationId de trazabilidad de la operación.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Código de tipo de documento GDC (ej. NOTS). Obligatorio en alta SINTWS.
    /// </summary>
    public string TipoDocumento { get; set; } = string.Empty;

    /// <summary>
    /// Código de subtipo de documento GDC (ej. NOTS01). Opcional en alta SINTWS.
    /// </summary>
    public string SubtipoDocumento { get; set; } = string.Empty;

    /// <summary>
    /// Serie documental GDC (ej. AI09). Obligatoria en alta SINTWS.
    /// </summary>
    public string Serie { get; set; } = string.Empty;

    /// <summary>
    /// Nombre lógico/documental. Si está vacío, se utiliza NombreArchivo.
    /// </summary>
    public string NombreDocumento { get; set; } = string.Empty;
}

public class ResultadoGDC
{
    /// <summary>
    /// true cuando la operación en GDC finaliza correctamente.
    /// </summary>
    public bool Exitoso { get; set; }
    /// <summary>
    /// ObjectId resultante en GDC.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;
    /// <summary>
    /// Mensaje de resultado de la operación GDC.
    /// </summary>
    public string Mensaje { get; set; } = string.Empty;
    /// <summary>
    /// Número de intentos realizados.
    /// </summary>
    public int Intentos { get; set; }
    /// <summary>
    /// Duración total de la operación en milisegundos.
    /// </summary>
    public int DuracionMs { get; set; }
    /// <summary>
    /// Error técnico detallado (si existe).
    /// </summary>
    public string ErrorDetalle { get; set; } = string.Empty;
    /// <summary>
    /// true cuando el documento ya existía previamente en GDC.
    /// </summary>
    public bool YaExistia { get; set; }

    public ResultadoGDC()
    {
        Exitoso = false;
        ObjectId = string.Empty;
        Mensaje = string.Empty;
        Intentos = 0;
        DuracionMs = 0;
        ErrorDetalle = string.Empty;
        YaExistia = false;
    }
}

public class GdcDocumentoMetadatos
{
    /// <summary>
    /// true si la consulta de metadatos en GDC fue satisfactoria.
    /// </summary>
    public bool Exitoso { get; set; }
    /// <summary>
    /// ObjectId del documento en GDC.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;
    /// <summary>
    /// MD5 almacenado del documento en GDC.
    /// </summary>
    public string MD5 { get; set; } = string.Empty;
    /// <summary>
    /// Nombre de archivo informado por GDC.
    /// </summary>
    public string NombreArchivo { get; set; } = string.Empty;
    /// <summary>
    /// Mensaje funcional de la operación de metadatos.
    /// </summary>
    public string Mensaje { get; set; } = string.Empty;
    /// <summary>
    /// Error técnico detallado de la consulta de metadatos.
    /// </summary>
    public string ErrorDetalle { get; set; } = string.Empty;
}

public class ObtenerDocumentoGDCResult
{
    /// <summary>
    /// Documento descargado de GDC en Base64.
    /// </summary>
    public string Base64 { get; set; } = string.Empty;
    /// <summary>
    /// Nombre de archivo recuperado desde GDC.
    /// </summary>
    public string NombreArchivo { get; set; } = string.Empty;
    /// <summary>
    /// Huella MD5 recuperada desde GDC.
    /// </summary>
    public string MD5 { get; set; } = string.Empty;

    /// <summary>
    /// Ruta container/path en blob cuando se persiste el documento descargado de GDC.
    /// </summary>
    public string? BlobPath { get; set; }

    /// <summary>
    /// SHA-256 precalculado para evitar recálculo en actividades posteriores.
    /// </summary>
    public string? PreComputedSHA256 { get; set; }
    /// <summary>
    /// MD5 precalculado para evitar recálculo en actividades posteriores.
    /// </summary>
    public string? PreComputedMD5 { get; set; }
    /// <summary>
    /// Tamaño precalculado en bytes para evitar relectura del contenido.
    /// </summary>
    public int PreComputedTamañoBytes { get; set; }
}

public class VerificarDuplicadoMd5Result
{
    /// <summary>
    /// true si existe un documento con el mismo MD5.
    /// </summary>
    public bool Existe { get; set; }
    /// <summary>
    /// SHA-256 asociado al duplicado encontrado.
    /// </summary>
    public string SHA256 { get; set; } = string.Empty;
}

/// <summary>
/// Resultado de la actividad ObtenerActivo que consulta DM_POSICION_AAII_TB vía AssetResolver.
/// </summary>
public class ResultadoAssetResolver
{
    /// <summary>
    /// true cuando la actividad fue ejecutada.
    /// </summary>
    public bool Ejecutado { get; set; }
    /// <summary>
    /// true cuando la ejecución terminó sin errores.
    /// </summary>
    public bool Exitoso { get; set; }
    /// <summary>Número de activos encontrados.</summary>
    public int Count { get; set; }
    /// <summary>Criterios de búsqueda utilizados.</summary>
    public CriteriosBusquedaActivo CriteriosUsados { get; set; } = new();
    /// <summary>Array de activos encontrados (puede contener más de uno si la búsqueda por ReferenciaCatastral devuelve varios).</summary>
    public List<ActivoEncontrado> Activos { get; set; } = new();
    /// <summary>Campos solicitados que no existen como columna en DM_POSICION_AAII_TB.</summary>
    public List<string> CamposConError { get; set; } = new();
    /// <summary>
    /// Mensaje resumen de la ejecución.
    /// </summary>
    public string Mensaje { get; set; } = string.Empty;
    /// <summary>
    /// Duración total de la búsqueda en milisegundos.
    /// </summary>
    public int DuracionMs { get; set; }
    /// <summary>
    /// Error técnico devuelto por AssetResolver, si aplica.
    /// </summary>
    public string? Error { get; set; }
}

public class CriteriosBusquedaActivo
{
    /// <summary>
    /// Valor de IDUFIR utilizado como criterio de búsqueda.
    /// </summary>
    public string? Idufir { get; set; }
    /// <summary>
    /// Referencia catastral utilizada como criterio de búsqueda.
    /// </summary>
    public string? ReferenciaCatastral { get; set; }
    /// <summary>
    /// Modo de combinación de criterios (OR/AND).
    /// </summary>
    public string ModoCombinacionCriterios { get; set; } = "OR";
    /// <summary>Detalle del criterio de dirección utilizado (solo si se usó búsqueda fuzzy).</summary>
    public DireccionCriterioActivo? Direccion { get; set; }
    /// <summary>Detalle del criterio de dirección tipificada utilizado (solo si se informó en request).</summary>
    public DireccionTipificadaCriterioActivo? DireccionTipificada { get; set; }
}

/// <summary>
/// Detalle del criterio de búsqueda por dirección utilizado en AssetResolver.
/// </summary>
public class DireccionCriterioActivo
{
    /// <summary>
    /// Dirección completa usada para matching fuzzy.
    /// </summary>
    public string? DireccionCompleta { get; set; }
    /// <summary>
    /// Nombre de vía utilizado en la búsqueda.
    /// </summary>
    public string? NombreVia { get; set; }
    /// <summary>
    /// Número de vía utilizado en la búsqueda.
    /// </summary>
    public string? Numero { get; set; }
    /// <summary>
    /// Municipio utilizado en la búsqueda.
    /// </summary>
    public string? Municipio { get; set; }
    /// <summary>
    /// Código postal utilizado en la búsqueda.
    /// </summary>
    public string? CodigoPostal { get; set; }
    /// <summary>
    /// Cadena normalizada usada en el cálculo de similitud.
    /// </summary>
    public string? DireccionNormalizada { get; set; }
    /// <summary>
    /// Score final de similitud para el match seleccionado.
    /// </summary>
    public double Score { get; set; }
    /// <summary>
    /// Número de candidatos evaluados en el proceso fuzzy.
    /// </summary>
    public int CandidatosEvaluados { get; set; }
    /// <summary>
    /// Explicación resumida del criterio/resultado de matching.
    /// </summary>
    public string? Razon { get; set; }
}

/// <summary>
/// Detalle del criterio de búsqueda por dirección tipificada utilizado en AssetResolver.
/// </summary>
public class DireccionTipificadaCriterioActivo
{
    /// <summary>
    /// País del criterio tipificado.
    /// </summary>
    public string? Pais { get; set; }
    /// <summary>
    /// Provincia del criterio tipificado.
    /// </summary>
    public string? Provincia { get; set; }
    /// <summary>
    /// Comunidad autónoma del criterio tipificado.
    /// </summary>
    public string? ComunidadAutonoma { get; set; }
    /// <summary>
    /// Municipio del criterio tipificado.
    /// </summary>
    public string? Municipio { get; set; }
    /// <summary>
    /// Población del criterio tipificado.
    /// </summary>
    public string? Poblacion { get; set; }
    /// <summary>
    /// Tipo de vía del criterio tipificado.
    /// </summary>
    public string? TipoVia { get; set; }
    /// <summary>
    /// Calle del criterio tipificado.
    /// </summary>
    public string? Calle { get; set; }
    /// <summary>
    /// Número del criterio tipificado.
    /// </summary>
    public string? Numero { get; set; }
    /// <summary>
    /// Bloque del criterio tipificado.
    /// </summary>
    public string? Bloque { get; set; }
    /// <summary>
    /// Puerta del criterio tipificado.
    /// </summary>
    public string? Puerta { get; set; }
    /// <summary>
    /// Código postal del criterio tipificado.
    /// </summary>
    public string? CodigoPostal { get; set; }
    /// <summary>
    /// Planta del criterio tipificado.
    /// </summary>
    public string? Planta { get; set; }
    /// <summary>
    /// Número de candidatos evaluados para este criterio.
    /// </summary>
    public int CandidatosEvaluados { get; set; }
    /// <summary>
    /// Explicación del resultado de matching tipificado.
    /// </summary>
    public string? Razon { get; set; }
}

/// <summary>
/// Un activo encontrado en DM_POSICION_AAII_TB.
/// Siempre incluye IdActivo y FchCierre (campos obligatorios); el resto depende de CamposSolicitados.
/// </summary>
public class ActivoEncontrado
{
    /// <summary>ID_ACTIVO_SAREB (obligatorio).</summary>
    public string IdActivo { get; set; } = string.Empty;
    /// <summary>FCH_CIERRE (obligatorio).</summary>
    public DateTime? FchCierre { get; set; }
    /// <summary>Campos adicionales solicitados con su valor.</summary>
    public Dictionary<string, object?> CamposSolicitados { get; set; } = new();
}

/// <summary>
/// Input para la actividad ObtenerActivoActivity.
/// </summary>
public class ObtenerActivoInput
{
    /// <summary>
    /// CorrelationId de la ejecución para trazabilidad.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
    /// <summary>
    /// Tipología en la que se ejecuta la búsqueda de activo.
    /// </summary>
    public string Tipologia { get; set; } = string.Empty;
    /// <summary>
    /// Datos extraídos base para auto-detección de criterios.
    /// </summary>
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    /// <summary>Columnas adicionales a devolver; null = solo obligatorias.</summary>
    public List<string>? CamposSolicitados { get; set; }
    /// <summary>Valor de IDUFIR explícito desde instrucciones (tiene precedencia sobre auto-detección).</summary>
    public string? IdufirOverride { get; set; }
    /// <summary>Valor de ReferenciaCatastral explícito desde instrucciones.</summary>
    public string? ReferenciaCatastralOverride { get; set; }
    /// <summary>Aliases para auto-detectar IDUFIR en DatosExtraidos (desde config tipología).</summary>
    public List<string> MapeoIdufir { get; set; } = new();
    /// <summary>Aliases para auto-detectar ReferenciaCatastral en DatosExtraidos (desde config tipología).</summary>
    public List<string> MapeoReferenciaCatastral { get; set; } = new();

    /// <summary>
    /// Modo de combinación de criterios de búsqueda (OR/AND).
    /// </summary>
    public string ModoCombinacionCriterios { get; set; } = "OR";

    // ── Búsqueda por dirección como criterio adicional ──

    /// <summary>Si true, se incluye IDUFIR como criterio de búsqueda. Default: true.</summary>
    public bool BusquedaIdufirHabilitada { get; set; } = true;
    /// <summary>Si true, se incluye ReferenciaCatastral como criterio de búsqueda. Default: true.</summary>
    public bool BusquedaReferenciaCatastralHabilitada { get; set; } = true;
    /// <summary>Si true, habilita la búsqueda por dirección como un criterio adicional.</summary>
    public bool BusquedaDireccionHabilitada { get; set; } = false;
    /// <summary>Si true, habilita búsqueda por dirección tipificada enviada en el request.</summary>
    public bool BusquedaDireccionTipificadaHabilitada { get; set; } = false;
    /// <summary>Campos explícitos de dirección tipificada para búsqueda (AND por campo informado).</summary>
    public DireccionTipificadaInputActivo? DireccionTipificada { get; set; }
    /// <summary>Aliases para auto-detectar una dirección libre/completa en DatosExtraidos.</summary>
    public List<string> MapeoDireccionCompleta { get; set; } = new();
    /// <summary>Aliases para auto-detectar nombre de vía en DatosExtraidos.</summary>
    public List<string> MapeoDireccionNombreVia { get; set; } = new();
    /// <summary>Aliases para auto-detectar número de vía en DatosExtraidos.</summary>
    public List<string> MapeoDireccionNumero { get; set; } = new();
    /// <summary>Aliases para auto-detectar municipio en DatosExtraidos.</summary>
    public List<string> MapeoDireccionMunicipio { get; set; } = new();
    /// <summary>Aliases para auto-detectar código postal en DatosExtraidos.</summary>
    public List<string> MapeoDireccionCodigoPostal { get; set; } = new();
    /// <summary>Umbral mínimo de score para aceptar un match por dirección (0.0–1.0, default 0.75).</summary>
    public double UmbralScoreDireccion { get; set; } = 0.75;
}

/// <summary>
/// Dirección tipificada enviada explícitamente al AssetResolver.
/// </summary>
public class DireccionTipificadaInputActivo
{
    /// <summary>
    /// País informado en la request.
    /// </summary>
    public string? Pais { get; set; }
    /// <summary>
    /// Provincia informada en la request.
    /// </summary>
    public string? Provincia { get; set; }
    /// <summary>
    /// Comunidad autónoma informada en la request.
    /// </summary>
    public string? ComunidadAutonoma { get; set; }
    /// <summary>
    /// Municipio informado en la request.
    /// </summary>
    public string? Municipio { get; set; }
    /// <summary>
    /// Población informada en la request.
    /// </summary>
    public string? Poblacion { get; set; }
    /// <summary>
    /// Tipo de vía informado en la request.
    /// </summary>
    public string? TipoVia { get; set; }
    /// <summary>
    /// Calle informada en la request.
    /// </summary>
    public string? Calle { get; set; }
    /// <summary>
    /// Número informado en la request.
    /// </summary>
    public string? Numero { get; set; }
    /// <summary>
    /// Bloque informado en la request.
    /// </summary>
    public string? Bloque { get; set; }
    /// <summary>
    /// Puerta informada en la request.
    /// </summary>
    public string? Puerta { get; set; }
    /// <summary>
    /// Código postal informado en la request.
    /// </summary>
    public string? CodigoPostal { get; set; }
    /// <summary>
    /// Planta informada en la request.
    /// </summary>
    public string? Planta { get; set; }
}
