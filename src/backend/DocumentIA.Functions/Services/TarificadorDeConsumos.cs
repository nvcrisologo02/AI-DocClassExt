using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Aplica el catalogo de tarifas a los consumos de una actividad.
///
/// Vive en la actividad y no en el orquestador porque el catalogo esta en base de
/// datos y el orquestador debe seguir siendo determinista: no puede consultarla.
/// El orquestador solo acumula y suma, que si lo es.
/// </summary>
public static class TarificadorDeConsumos
{
    public static void Aplicar(
        List<ConsumoIA>? consumos,
        TarifaRegistryLoader cargador,
        ILogger logger)
    {
        if (consumos is null || consumos.Count == 0)
        {
            return;
        }

        try
        {
            var registro = cargador.Load();
            var ahora = DateTime.UtcNow;

            foreach (var consumo in consumos)
            {
                CalculadoraCosteIA.Aplicar(consumo, registro, ahora);
            }
        }
        catch (Exception ex)
        {
            // El control de costes nunca puede tumbar un procesamiento: los consumos
            // se quedan sin coste y el agregado se marcara incompleto.
            logger.LogWarning(ex, "No se pudieron tarificar los consumos de IA. Se continua sin coste.");
        }
    }
}
