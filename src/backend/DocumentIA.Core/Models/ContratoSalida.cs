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
    public PromptResultado? ResultadoPrompt { get; set; }
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
    public string RunTipologia { get; set; } = string.Empty;
    public ResultadoClasificacion Clasificacion { get; set; } = new();
    public ResultadoExtraccion Extraccion { get; set; } = new();
    public InformacionPostproceso Postproceso { get; set; } = new();
    public ResultadoIntegracion Integracion { get; set; } = new();
    // Resultado de la interacción con el Gestor Documental (GDC)
    public ResultadoGDC GDC { get; set; } = new();
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
    public string? TipologiaDetectada { get; set; }

    /// <summary>Texto extraído por DI durante la clasificación. Campo transitorio, no se serializa en la respuesta.</summary>
    [JsonIgnore]
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
    public Dictionary<string, int> TiemposMs { get; set; } = new();
}

public class InformacionPostproceso
{
    public List<string> Normalizaciones { get; set; } = new();
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
