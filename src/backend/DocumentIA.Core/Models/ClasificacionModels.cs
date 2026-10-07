namespace DocumentIA.Core.Models;

public class ClasificacionInput
{
    public ContratoEntrada Entrada { get; set; } = new();
    public Dictionary<string, object> DatosNormalizados { get; set; } = new();
    /// <summary>
    /// Umbral de fallback efectivo resuelto por el orquestador antes de llamar a ClasificarActivity.
    /// Cadena: instrucciones.Classification.Umbral ?? tipología.ClasifUmbralFallback ?? config.FallbackThreshold.
    /// null = usar config.FallbackThreshold directamente en el proveedor.
    /// </summary>
    public double? UmbralFallbackEfectivo { get; set; }
    /// <summary>
    /// Ruta en blob del documento a clasificar (recorte u original). Los proveedores que
    /// necesitan el binario lo resuelven por SAS o descarga dentro de la activity (AB#100814).
    /// </summary>
    public string? BlobPathClasificacion { get; set; }

    /// <summary>
    /// Legado: base64 del recorte. El orquestador ya no lo rellena; solo se lee cuando
    /// BlobPathClasificacion viene vacío (instancias en vuelo). Retirar tras v1.0.0.
    /// </summary>
    [Obsolete("El documento viaja por BlobPathClasificacion (AB#100814). Solo lectura legada.")]
    public string? DocumentoBase64Override { get; set; }
    public int CharsTextoNativo { get; set; }
    public int TotalPaginas { get; set; }
    public bool GenerarResumenPorDefecto { get; set; }

    /// <summary>
    /// true = ignorar Instrucciones.RestriccionTipologias en esta llamada. Lo activa el router
    /// para la pasada única de "propuesta libre" cuando el resultado restringido es Desconocido.
    /// </summary>
    public bool OmitirRestriccionTipologias { get; set; }

    /// <summary>
    /// Por que no hay markdown en DatosNormalizados, cuando el resolutor no lo trajo (AB#100880).
    /// La guarda de contenido la propaga en FallbackRazon y en ResultadoClasificacion.CausaSinContenido.
    /// </summary>
    public CausaSinContenido? CausaSinMarkdown { get; set; }
}

/// <summary>Entrada de ClasificarEmbeddingsActivity (AB#100779). La construye el orquestador en el Paso 3.0.</summary>
public class ClasificarEmbeddingsInput
{
    /// <summary>Texto del documento ya con espacios colapsados y recortado a MaxCharsPorDefecto por el orquestador; el proveedor aplica ademas el MaxChars configurado.</summary>
    public string? Texto { get; set; }
    /// <summary>La peticion trae ExpectedType resoluble: el caller manda y A solo persiste.</summary>
    public bool ExpectedTypeInformado { get; set; }
    /// <summary>Codigos de restriccionTipologias ya normalizados por el trigger. Nulo o vacio: sin restriccion.</summary>
    public List<string>? RestriccionCodigos { get; set; }
    public string? NivelClasificacion { get; set; }
    /// <summary>InstanceId de la orquestacion, correlacion de la telemetria.</summary>
    public string? InstanceId { get; set; }
}
