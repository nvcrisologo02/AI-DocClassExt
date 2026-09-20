namespace DocumentIA.Core.Models;

/// <summary>
/// Entrada de PersistirActivity.
/// </summary>
public class PersistirInput
{
    public ContratoSalida Salida { get; set; } = new();

    /// <summary>Solicitante de esta ejecucion, tomado de entrada.Trazabilidad.SubmittedBy. Null si no se informo.</summary>
    public string? SubmittedBy { get; set; }

    /// <summary>
    /// Informado solo cuando la peticion se sirve reutilizando el contrato de otra
    /// ejecucion. Cuando es null, la persistencia es la de siempre (AB#100258).
    /// </summary>
    public ReutilizacionInput? Reutilizacion { get; set; }
}

/// <summary>
/// Lo que el contrato reutilizado no puede aportar por si mismo. El InstanceId y el
/// OperationId no viajan aqui: el orquestador ya los fija con los valores reales de esta
/// llamada sobre el contrato antes de persistir.
/// </summary>
public class ReutilizacionInput
{
    /// <summary>EjecucionGuid de la fila cuyo contrato se devolvio.</summary>
    public string EjecucionOriginalGuid { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 del documento. Explicito porque la rama de dedup por MD5 de GDC corre antes
    /// de NormalizarActivity y un contrato antiguo puede traer el suyo vacio.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;
}
