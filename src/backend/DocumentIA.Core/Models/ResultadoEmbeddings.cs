using System.Text.Json.Serialization;

namespace DocumentIA.Core.Models;

/// <summary>Modos del clasificador por embeddings en ModeloConfigs (AB#100779).</summary>
public static class ModosEmbeddings
{
    public const string Off = "off";
    public const string Sombra = "sombra";
    public const string Hibrido = "hibrido";

    /// <summary>Cualquier valor que no sea sombra o hibrido (incluido nulo o vacio) es off.</summary>
    public static string Normalizar(string? modo)
    {
        var m = modo?.Trim().ToLowerInvariant();
        return m switch
        {
            Sombra => Sombra,
            Hibrido => Hibrido,
            _ => Off
        };
    }
}

public static class DecisionesEmbeddings
{
    public const string Omitido = "Omitido";
    public const string DerivarGpt = "DerivarGpt";
    public const string Contesta = "Contesta";
}

public static class MotivosEmbeddings
{
    public const string Off = "off";
    public const string Sombra = "sombra";
    public const string SinTexto = "sin_texto";
    public const string Umbral = "umbral";
    public const string ConfianzaBaja = "confianza_baja";
    public const string ExpectedType = "expected_type";
    public const string Cobertura = "cobertura";
    public const string ParCompartido = "par_compartido";
    public const string SinCalibracion = "sin_calibracion";
    public const string MasaInsuficiente = "masa_insuficiente";
    public const string ConfianzaCondicionadaBaja = "confianza_condicionada_baja";
    public const string SinTipologia = "sin_tipologia";
    public const string CircuitoAbierto = "circuito_abierto";
    public const string Error = "error";
}

/// <summary>Quien contesto la clasificacion: DetalleEjecucion.Clasificacion.RamaClasificacion.</summary>
public static class RamasClasificacion
{
    public const string Gpt = "gpt";
    public const string Embeddings = "embeddings";
    public const string ExpectedType = "expectedtype";
}

/// <summary>Un TDN1 con su probabilidad, para el Top3 persistido.</summary>
public class ProbabilidadTdn1
{
    public string Tdn1 { get; set; } = string.Empty;
    public double Probabilidad { get; set; }
}

/// <summary>Cifras del modo restringido. Se persisten en cualquier modo distinto de off.</summary>
public class RestringidoEmbeddings
{
    /// <summary>Suma de probabilidades de los pares permitidos.</summary>
    public double Masa { get; set; }
    /// <summary>Probabilidad de la mejor permitida dividida por la masa.</summary>
    public double ConfianzaCondicionada { get; set; }
    /// <summary>Puerta que ha cerrado el paso: cobertura, par_compartido o sin_calibracion. Nulo si ninguna.</summary>
    public string? Puerta { get; set; }
    /// <summary>Tipologia que A habria devuelto sin restriccion (codigo del catalogo o "TDN1/TDN2" si no mapea).</summary>
    public string? PrediccionSinRestringir { get; set; }
}

/// <summary>
/// Bloque ResultadoClasificacion.Embeddings: lo que calculo el clasificador A en esta
/// ejecucion y que decidio el decisor. Ausente con modo off o sin texto. AB#100779.
/// </summary>
public class ResultadoEmbeddings
{
    public const string Proveedor = "Embeddings";

    public string? VersionModelo { get; set; }
    /// <summary>Modo configurado para peticiones sin restriccion.</summary>
    public string Modo { get; set; } = ModosEmbeddings.Off;
    /// <summary>Modo configurado para peticiones restringidas. Nulo si la peticion no traia restriccion.</summary>
    public string? ModoRestringido { get; set; }
    public string? Tdn1 { get; set; }
    public string? Tdn2 { get; set; }
    /// <summary>Codigo de tipologia del catalogo que corresponde al par predicho. Nulo si no mapea.</summary>
    public string? Tipologia { get; set; }
    public double Confianza { get; set; }
    public List<ProbabilidadTdn1> Top3 { get; set; } = new();
    public string Decision { get; set; } = DecisionesEmbeddings.Omitido;
    public string? Motivo { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RestringidoEmbeddings? Restringido { get; set; }
    public long LatenciaMs { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
    public string? Deployment { get; set; }

    /// <summary>
    /// Consumo de la llamada de embeddings. Lo rellena la activity y el orquestador lo
    /// acumula en DetalleEjecucion.Costes y lo pone a nulo: asi no se persiste dos veces.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ConsumoIA>? Consumos { get; set; }
}
