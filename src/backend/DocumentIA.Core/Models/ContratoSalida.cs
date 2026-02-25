namespace DocumentIA.Core.Models;

public class ContratoSalida
{
    public Identificacion Identificacion { get; set; } = new();
    public Integridad Integridad { get; set; } = new();
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    public DetalleEjecucion DetalleEjecucion { get; set; } = new();
    public ResultadoFinal Resultado { get; set; } = new();
}

public class Identificacion
{
    public string Documento { get; set; } = string.Empty;
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public string Tipologia { get; set; } = string.Empty;
    public DateTime FechaProceso { get; set; } = DateTime.UtcNow;
    public int Paginas { get; set; }
}

public class Integridad
{
    public string CRC32 { get; set; } = string.Empty;
    public string SHA256 { get; set; } = string.Empty;
    // Ruta completa en blob (container/path) para relacionar documento logico con almacenamiento fisico
    public string? RutaBlobStorage { get; set; }
    public string? GestorDocumental { get; set; }
    public string? IdActivo { get; set; }
}

public class DetalleEjecucion
{
    public string RunTipologia { get; set; } = string.Empty;
    public ResultadoClasificacion Clasificacion { get; set; } = new();
    public ResultadoExtraccion Extraccion { get; set; } = new();
    public InformacionPostproceso Postproceso { get; set; } = new();
    public ResultadoIntegracion Integracion { get; set; } = new();
}

public class ResultadoClasificacion
{
    public string Modelo { get; set; } = string.Empty;
    public double Confianza { get; set; }
    public bool FallbackLLM { get; set; }
    public string? TipologiaDetectada { get; set; }
}

public class ResultadoExtraccion
{
    public string Modelo { get; set; } = string.Empty;
    public bool LayoutEnabled { get; set; }
    public Dictionary<string, int> TiemposMs { get; set; } = new();
}

public class InformacionPostproceso
{
    public List<string> Normalizaciones { get; set; } = new();
    public List<string> Validaciones { get; set; } = new();
    public List<string> Inconsistencias { get; set; } = new();
}

public class IntegrarInput
{
    public string Tipologia { get; set; } = string.Empty;
    public string DocumentoId { get; set; } = string.Empty;
    public Dictionary<string, object> DatosExtraidos { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
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
}
