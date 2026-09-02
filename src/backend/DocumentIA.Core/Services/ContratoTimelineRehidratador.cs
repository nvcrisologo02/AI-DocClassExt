using System.Collections.Generic;
using System.Text.Json;
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Vuelve a unir el timeline de actividades al contrato deserializado. Desde AB#100166 el
/// contrato se persiste sin Seguimiento.Actividades (viven en la columna ActivityTimelineJson,
/// que consume el listado del Monitor); los lectores que reconstruyen el contrato completo
/// deben recomponerlo para que su salida sea identica a la de antes.
/// </summary>
public static class ContratoTimelineRehidratador
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Rellena <c>salida.DetalleEjecucion.Seguimiento.Actividades</c> desde el JSON de la columna
    /// cuando el contrato viene podado. Si el contrato ya trae timeline (filas historicas) no se
    /// toca. Tolerante a JSON invalido: no lanza.
    /// </summary>
    public static void Rehidratar(ContratoSalida? salida, string? activityTimelineJson)
    {
        if (salida?.DetalleEjecucion?.Seguimiento is null)
        {
            return;
        }

        if (salida.DetalleEjecucion.Seguimiento.Actividades.Count > 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(activityTimelineJson))
        {
            return;
        }

        try
        {
            var actividades = JsonSerializer.Deserialize<List<TrazaActividad>>(activityTimelineJson, Opciones);
            if (actividades is { Count: > 0 })
            {
                salida.DetalleEjecucion.Seguimiento.Actividades = actividades;
            }
        }
        catch (JsonException)
        {
            // Timeline ilegible: se deja el contrato como esta, igual que hace el detalle de Admin.
        }
    }
}
