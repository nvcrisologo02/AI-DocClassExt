namespace DocumentIA.Functions.Services;

using DocumentIA.Core.Services;

public class ClassificationRoutingSettings
{
    /// <summary>
    /// Valor legacy. Se mantiene para compatibilidad cuando no hay flows definidos.
    /// </summary>
    public string DefaultProvider { get; set; } = "hybrid-tdn";
    /// <summary>
    /// Flujo por defecto cuando la instruccion usa Provider=auto.
    /// </summary>
    public string DefaultFlow { get; set; } = "hybrid-tdn";
    /// <summary>
    /// Flujos configurables de clasificacion. Key=nombre del flujo, Value=secuencia de providers.
    /// </summary>
    public Dictionary<string, ClassificationFlowSettings> Flows { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Indica si debe ejecutarse el fallback global cuando ningun provider del flujo es satisfactorio.
    /// </summary>
    public bool UseGlobalFallback { get; set; } = true;
    /// <summary>
    /// Provider de fallback global por defecto (ej: gpt, di, mock, hybrid-tdn).
    /// </summary>
    public string GlobalFallbackProvider { get; set; } = "gpt";
    public string DefaultModelKey { get; set; } = string.Empty;
    /// <summary>
    /// Nivel jerárquico de clasificación usado cuando la petición no informa NivelClasificacion.
    /// Valores permitidos: TDN1, TDN1_TDN2.
    /// </summary>
    public string NivelClasificacionDefault { get; set; } = ClassificationLevelResolver.DefaultLevel;
}

public class ClassificationFlowSettings
{
    public List<string> Providers { get; set; } = new();
}
