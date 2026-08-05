namespace DocumentIA.Core.Models;

/// <summary>
/// Input interno (serializacion Durable Functions orquestador -> actividad) de PersistirActivity.
/// No es el ContratoSalida externo: envuelve datos adicionales de orquestacion que no viajan en el contrato.
/// </summary>
public class PersistirInput
{
    public ContratoSalida Salida { get; set; } = new();

    /// <summary>Solicitante de esta ejecucion, tomado de entrada.Trazabilidad.SubmittedBy. Null si no se informo.</summary>
    public string? SubmittedBy { get; set; }
}
