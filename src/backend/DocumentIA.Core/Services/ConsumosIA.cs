using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Fusion de consumos entre proveedores encadenados.
///
/// Los proveedores hoja registran su propio consumo, pero los que los componen
/// (routers de clasificacion y extraccion, clasificador hibrido) devuelven el
/// resultado de uno solo y descartan los demas. Sin fusionar, el gasto de todo lo
/// descartado desaparece justo en los caminos que mas cuestan: cadena de
/// proveedores, fallback y restriccion de tipologias.
/// </summary>
public static class ConsumosIA
{
    /// <summary>
    /// Vuelca <paramref name="origen"/> en <paramref name="destino"/> sin duplicar
    /// los consumos que ya estan (comparacion por referencia, porque el mismo objeto
    /// puede llegar por dos vias).
    /// </summary>
    /// <param name="marcarDescartados">
    /// true cuando el resultado de esas llamadas no es el que se devuelve. La llamada
    /// se pago igual, asi que cuenta en el total, pero queda marcada para poder
    /// separar el gasto util del descartado.
    /// </param>
    public static void Fusionar(
        List<ConsumoIA>? destino,
        IEnumerable<ConsumoIA>? origen,
        bool marcarDescartados = false)
    {
        if (destino is null || origen is null)
        {
            return;
        }

        foreach (var consumo in origen)
        {
            if (consumo is null || ContienePorReferencia(destino, consumo))
            {
                continue;
            }

            if (marcarDescartados)
            {
                consumo.Descartado = true;
            }

            destino.Add(consumo);
        }
    }

    /// <summary>
    /// Fusiona en el resultado elegido los consumos de todos los candidatos evaluados.
    /// El propio resultado elegido se omite para no duplicar, y el resto entra marcado
    /// como descartado.
    /// </summary>
    public static void FusionarEvaluados(
        List<ConsumoIA>? destino,
        IEnumerable<IReadOnlyList<ConsumoIA>?> consumosEvaluados)
    {
        if (destino is null)
        {
            return;
        }

        foreach (var consumos in consumosEvaluados)
        {
            Fusionar(destino, consumos, marcarDescartados: true);
        }
    }

    private static bool ContienePorReferencia(List<ConsumoIA> destino, ConsumoIA consumo)
    {
        foreach (var existente in destino)
        {
            if (ReferenceEquals(existente, consumo))
            {
                return true;
            }
        }

        return false;
    }
}
