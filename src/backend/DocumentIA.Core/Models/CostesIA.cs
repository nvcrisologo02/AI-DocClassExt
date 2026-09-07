namespace DocumentIA.Core.Models;

/// <summary>
/// Nombres canonicos de proveedor en ConsumoIA.Proveedor. Viven aqui, sin
/// dependencias de SDK, para que los use tanto Core como Functions.
/// </summary>
public static class ProveedoresIA
{
    /// <summary>
    /// Tokens facturados por un deployment de Azure OpenAI. Tambien los que
    /// Content Understanding consume de su deployment de Foundry conectado.
    /// </summary>
    public const string AzureOpenAI = "AzureOpenAI";

    /// <summary>Paginas facturadas por Document Intelligence.</summary>
    public const string DocumentIntelligence = "DocumentIntelligence";

    /// <summary>Paginas y contextualizacion facturadas por Content Understanding.</summary>
    public const string ContentUnderstanding = "ContentUnderstanding";
}

/// <summary>
/// Actividades del pipeline en ConsumoIA.Actividad.
/// </summary>
public static class ActividadesIA
{
    public const string Clasificar = "Clasificar";
    public const string Extraer = "Extraer";
    public const string Prompt = "Prompt";
    public const string Layout = "Layout";
}

/// <summary>
/// Consumo de una unica llamada a un servicio de IA. Una ejecucion genera tantos
/// consumos como llamadas haga, incluidas las de proveedores cuyo resultado se
/// descarto despues: esas tambien se han pagado.
/// </summary>
public class ConsumoIA
{
    /// <summary>Actividad del pipeline: Clasificar, Extraer, Prompt o Layout.</summary>
    public string Actividad { get; set; } = string.Empty;

    /// <summary>Punto concreto de consumo, por ejemplo "classification.phase1".</summary>
    public string Operacion { get; set; } = string.Empty;

    /// <summary>AzureOpenAI, DocumentIntelligence o ContentUnderstanding.</summary>
    public string Proveedor { get; set; } = string.Empty;

    /// <summary>
    /// Modelo fisico consumido: nombre de deployment, identificador de clasificador,
    /// "prebuilt-layout" o identificador de analyzer. Es la clave de tarifa.
    /// </summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>Tokens de entrada totales tal como los devuelve la API. Incluye los cacheados.</summary>
    public int? TokensEntrada { get; set; }

    /// <summary>Tokens de entrada servidos desde cache. Subconjunto de TokensEntrada, no un sumando.</summary>
    public int? TokensEntradaCache { get; set; }

    /// <summary>Tokens de salida totales. Incluye los de razonamiento.</summary>
    public int? TokensSalida { get; set; }

    /// <summary>Tokens de razonamiento. Subconjunto de TokensSalida, informativo.</summary>
    public int? TokensRazonamiento { get; set; }

    /// <summary>Tokens de contextualizacion facturados por Content Understanding.</summary>
    public int? TokensContextualizacion { get; set; }

    /// <summary>Paginas facturadas por los servicios que facturan por pagina.</summary>
    public int? Paginas { get; set; }

    /// <summary>Coste en euros. Null cuando el modelo no tiene tarifa en el catalogo.</summary>
    public decimal? CosteEur { get; set; }

    /// <summary>Linea de tarifa aplicada, con su vigencia: "gpt-5-mini@2026-07-21".</summary>
    public string? TarifaAplicada { get; set; }

    /// <summary>
    /// El resultado de esta llamada se descarto (proveedor no satisfactorio, fallback).
    /// El coste se contabiliza igual.
    /// </summary>
    public bool Descartado { get; set; }
}

/// <summary>
/// Agregado de consumo de servicios de IA de una ejecucion. Solo servicios de IA:
/// no incluye storage, computo ni ninguna otra infraestructura.
/// </summary>
public class CostesIA
{
    /// <summary>Version del esquema del bloque.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Detalle por llamada.</summary>
    public List<ConsumoIA> Consumos { get; set; } = new();

    /// <summary>Suma de los costes con tarifa conocida.</summary>
    public decimal CosteTotalEur { get; set; }

    /// <summary>
    /// Suma de entrada, salida y contextualizacion. No suma cacheados ni razonamiento,
    /// que ya estan contenidos en entrada y salida respectivamente.
    /// </summary>
    public int TokensTotales { get; set; }

    /// <summary>Suma de paginas facturadas.</summary>
    public int PaginasTotales { get; set; }

    /// <summary>False si algun consumo quedo sin tarifa: el total es incompleto.</summary>
    public bool TarifasCompletas { get; set; } = true;

    /// <summary>Modelos consumidos que no tienen linea de tarifa aplicable.</summary>
    public List<string> ModelosSinTarifa { get; set; } = new();

    /// <summary>True cuando la ejecucion reutilizo un resultado previo y no gasto IA.</summary>
    public bool ReutilizadaPorDuplicado { get; set; }

    /// <summary>Coste de la ejecucion original reutilizada, informativo. No suma al total.</summary>
    public decimal? CosteEjecucionOriginalEur { get; set; }
}
