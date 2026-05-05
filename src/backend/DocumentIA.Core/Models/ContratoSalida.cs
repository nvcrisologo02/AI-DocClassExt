using System.Text.Json.Serialization;

namespace DocumentIA.Core.Models;

public class ContratoSalida
{
    public Identificacion Identificacion { get; set; } = new();
    public Integridad Integridad { get; set; } = new();
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    public DetalleEjecucion DetalleEjecucion { get; set; } = new();
    public ResultadoFinal Resultado { get; set; } = new();
    /// <summary>
    /// Resultado del prompt libre definido en la configuración de la tipología.
    /// Null cuando la tipología no tiene prompt habilitado.
    /// </summary>
    //public PromptResultado? ResultadoPrompt { get; set; }
}

public class Identificacion
{
    public string Documento { get; set; } = string.Empty;
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public string Tipologia { get; set; } = string.Empty;
    public string TipologiaFamilia { get; set; } = string.Empty;
    public string TipologiaVersion { get; set; } = string.Empty;
    public DateTime FechaProceso { get; set; } = DateTime.UtcNow;
    public int Paginas { get; set; }
}

public class Integridad
{
    public string CRC32 { get; set; } = string.Empty;
    public string SHA256 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    // Ruta completa en blob (container/path) para relacionar documento logico con almacenamiento fisico
    public string? RutaBlobStorage { get; set; }
    public string? GestorDocumental { get; set; }
    public string? IdActivo { get; set; }
    public string? IdActivoEntrada { get; set; }
    public bool IdActivoCambiado { get; set; }
}

public class DetalleEjecucion
{
    /// <summary>ID de la instancia de orquestación Durable Functions. Permite localizar la ejecución en Azure Portal y mediante polling.</summary>
    public string? InstanceId { get; set; }
    /// <summary>operation_Id de Application Insights (W3C TraceId). Usar en KQL: union traces,requests | where operation_Id == OperationId.</summary>
    public string? OperationId { get; set; }
    public string RunTipologia { get; set; } = string.Empty;
    public ResultadoClasificacion Clasificacion { get; set; } = new();
    public ResultadoExtraccion Extraccion { get; set; } = new();
    public InformacionPostproceso Postproceso { get; set; } = new();
    public ResultadoIntegracion Integracion { get; set; } = new();
    // Resultado de la interacción con el Gestor Documental (GDC)
    public ResultadoGDC GDC { get; set; } = new();
    // Resultado de la consulta AssetResolver (DM_POSICION_AAII_TB).
    // Null cuando la actividad no está habilitada para la tipología/petición.
    public ResultadoAssetResolver? AssetResolver { get; set; }
    // Seguimiento estructurado de actividades para monitorización en tiempo real y post-ejecución.
    public SeguimientoOrquestacion Seguimiento { get; set; } = new();
    /// <summary>Información de ejecución del prompt libre (cuando la tipología lo tiene habilitado).</summary>
    public ResultadoPromptEjecucion? Prompt { get; set; }
}

public class ResultadoPromptEjecucion
{
    public string Modelo { get; set; } = string.Empty;
    public int TiempoMs { get; set; }
    public bool CombinedWithFallback { get; set; }
    public string? Error { get; set; }
}

public class SeguimientoOrquestacion
{
    public string Version { get; set; } = "1.0";
    public string Estado { get; set; } = "Pending";
    public string ActividadActual { get; set; } = string.Empty;
    public int ActividadesTotales { get; set; }
    public List<string> ActividadesCompletadas { get; set; } = new();
    public int DuracionTotalMs { get; set; }
    public List<TrazaActividad> Actividades { get; set; } = new();
}

public class TrazaActividad
{
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = "Pending"; // Pending | Running | Completed | Failed | Skipped | Timeout
    public DateTime InicioUtc { get; set; }
    public DateTime? FinUtc { get; set; }
    public int DuracionMs { get; set; }
    public string? Mensaje { get; set; }
    public bool FallbackActivado { get; set; }
    public string? FallbackRazon { get; set; }
}

public class ResultadoClasificacion
{
    public string Modelo { get; set; } = string.Empty;
    /// <summary>Confianza final usada en ConfianzaGlobal (DI primario, o GPT si hubo fallback).</summary>
    public double Confianza { get; set; }
    /// <summary>Confianza bruta de Azure Document Intelligence (0 si no se ejecutó).</summary>
    public double ConfianzaDI { get; set; }
    /// <summary>Confianza reportada por GPT fallback (0 si no se activó).</summary>
    public double ConfianzaGPT { get; set; }
    /// <summary>Proveedor que produjo la clasificación final: "DocumentIntelligence" | "GPT4oMini".</summary>
    public string ProveedorClasif { get; set; } = string.Empty;
    public bool FallbackLLM { get; set; }
    public string? FallbackRazon { get; set; }
    public double? UmbralFallbackAplicado { get; set; }
    public string? TipologiaDetectada { get; set; }

    /// <summary>Texto extraído por DI durante la clasificación. El orquestador lo usa para propagar a DatosNormalizados["Markdown"] y luego lo limpia antes de incluirlo en la respuesta.</summary>
    public string? ContentExtraido { get; set; }
}

public class ResultadoExtraccion
{
    public string Modelo { get; set; } = string.Empty;
    /// <summary>Confianza calculada para la extracción (0-1). 0 cuando la extracción está deshabilitada.</summary>
    public double ConfianzaExtraccion { get; set; }
    /// <summary>Proveedor que realizó la extracción: "AzureContentUnderstanding" | "DICustom" | "GPT4oMini".</summary>
    public string ProveedorExtrac { get; set; } = string.Empty;
    public bool LayoutEnabled { get; set; }
    public bool FallbackUsado { get; set; }
    public string? FallbackRazon { get; set; }
    /// <summary>Confianzas individuales reportadas por el proveedor de extracción por campo normalizado.</summary>
    public Dictionary<string, double> ConfianzaPorCampo { get; set; } = new();
    /// <summary>Campos cuya confianza de extracción quedó por debajo del umbral efectivo de extracción.</summary>
    public List<string> CamposConDuda { get; set; } = new();
    public Dictionary<string, int> TiemposMs { get; set; } = new();
}

public class InformacionPostproceso
{
    public List<string> Normalizaciones { get; set; } = new();
    public string? Markdown { get; set; }
    public List<string> Validaciones { get; set; } = new();
    public List<string> Inconsistencias { get; set; } = new();
    /// <summary>Confianza de validación calculada por el motor de reglas (1 - errores/reglasRequeridas).</summary>
    public double ConfianzaValidacion { get; set; }
}

public class IntegrarInput
{
    public string Tipologia { get; set; } = string.Empty;
    public string DocumentoId { get; set; } = string.Empty;
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    /// <summary>
    /// IdActivo de trazabilidad de entrada. Puede estar vacío si el plugin de enriquecimiento lo retorna.
    /// </summary>
    public string? IdActivo { get; set; }
}

public class ResultadoIntegracion
{
    public string Tipologia { get; set; } = string.Empty;
    public string Estado { get; set; } = "OK"; // OK | REVISION | ERROR
    public string Mensaje { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<PluginExecutionResult> Plugins { get; set; } = new();
    public Dictionary<string, object> DatosOriginales { get; set; } = new();
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
    public string PluginKey { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool Success { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object>? DatosEnriquecidos { get; set; }
}

public class ResultadoFinal
{
    public string Estado { get; set; } = "OK";
    /// <summary>Detalle del motivo cuando Estado = ERROR.</summary>
    public string? MensajeError { get; set; }
    public double ConfianzaGlobal { get; set; }
    /// <summary>Estado de calidad basado en umbrales de confianza: OK | REVISION | ERROR.</summary>
    public string EstadoCalidad { get; set; } = string.Empty;
    /// <summary>Componentes del MIN usado para ConfianzaGlobal.</summary>
    public double ConfianzaClasificacion { get; set; }
    public double ConfianzaExtraccion { get; set; }
    public double ConfianzaValidacion { get; set; }
    public bool ReutilizadaPorDuplicado { get; set; }
    public string? MensajeReutilizacion { get; set; }
}

/// <summary>
/// Input necesarios para subir un documento al GDC
/// </summary>
public class SubirGDCInput
{
    public string IdActivo { get; set; } = string.Empty;
    public string Matricula { get; set; } = string.Empty;
    public string ContenidoBase64 { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string SHA256 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    // GDC taxonomy fields — resolved by SubirGDCActivity from tipología config before calling GdcService.
    // TipoDocumento: código tipo de documento GDC (ej. "NOTS"). Mandatory in SINTWS create.
    public string TipoDocumento { get; set; } = string.Empty;
    // SubtipoDocumento: código subtipo de documento GDC (ej. "NOTS01"). Optional in SINTWS create.
    public string SubtipoDocumento { get; set; } = string.Empty;
    // Serie: serie documental GDC (ej. "AI09"). Mandatory in SINTWS create.
    public string Serie { get; set; } = string.Empty;
    // NombreDocumento: nombre lógico del documento (display name). When empty, NombreArchivo is used.
    public string NombreDocumento { get; set; } = string.Empty;
}

public class ResultadoGDC
{
    public bool Exitoso { get; set; }
    public string ObjectId { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public int Intentos { get; set; }
    public int DuracionMs { get; set; }
    public string ErrorDetalle { get; set; } = string.Empty;
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
    public bool Exitoso { get; set; }
    public string ObjectId { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string ErrorDetalle { get; set; } = string.Empty;
}

public class ObtenerDocumentoGDCResult
{
    public string Base64 { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
}

public class VerificarDuplicadoMd5Result
{
    public bool Existe { get; set; }
    public string SHA256 { get; set; } = string.Empty;
}

/// <summary>
/// Resultado de la actividad ObtenerActivo que consulta DM_POSICION_AAII_TB vía AssetResolver.
/// </summary>
public class ResultadoAssetResolver
{
    public bool Ejecutado { get; set; }
    public bool Exitoso { get; set; }
    /// <summary>Número de activos encontrados.</summary>
    public int Count { get; set; }
    /// <summary>Criterios de búsqueda utilizados.</summary>
    public CriteriosBusquedaActivo CriteriosUsados { get; set; } = new();
    /// <summary>Array de activos encontrados (puede contener más de uno si la búsqueda por ReferenciaCatastral devuelve varios).</summary>
    public List<ActivoEncontrado> Activos { get; set; } = new();
    /// <summary>Campos solicitados que no existen como columna en DM_POSICION_AAII_TB.</summary>
    public List<string> CamposConError { get; set; } = new();
    public string Mensaje { get; set; } = string.Empty;
    public int DuracionMs { get; set; }
    public string? Error { get; set; }
}

public class CriteriosBusquedaActivo
{
    public string? Idufir { get; set; }
    public string? ReferenciaCatastral { get; set; }
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
    public string? DireccionCompleta { get; set; }
    public string? NombreVia { get; set; }
    public string? Numero { get; set; }
    public string? Municipio { get; set; }
    public string? CodigoPostal { get; set; }
    public string? DireccionNormalizada { get; set; }
    public double Score { get; set; }
    public int CandidatosEvaluados { get; set; }
    public string? Razon { get; set; }
}

/// <summary>
/// Detalle del criterio de búsqueda por dirección tipificada utilizado en AssetResolver.
/// </summary>
public class DireccionTipificadaCriterioActivo
{
    public string? Pais { get; set; }
    public string? Provincia { get; set; }
    public string? ComunidadAutonoma { get; set; }
    public string? Municipio { get; set; }
    public string? Poblacion { get; set; }
    public string? TipoVia { get; set; }
    public string? Calle { get; set; }
    public string? Numero { get; set; }
    public string? Bloque { get; set; }
    public string? Puerta { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Planta { get; set; }
    public int CandidatosEvaluados { get; set; }
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
    public string CorrelationId { get; set; } = string.Empty;
    public string Tipologia { get; set; } = string.Empty;
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
    public string? Pais { get; set; }
    public string? Provincia { get; set; }
    public string? ComunidadAutonoma { get; set; }
    public string? Municipio { get; set; }
    public string? Poblacion { get; set; }
    public string? TipoVia { get; set; }
    public string? Calle { get; set; }
    public string? Numero { get; set; }
    public string? Bloque { get; set; }
    public string? Puerta { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Planta { get; set; }
}
