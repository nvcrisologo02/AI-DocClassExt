using System.Globalization;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Resuelve la tarifa de un consumo y calcula su coste. Sin estado y sin
/// dependencias de infraestructura: recibe el catalogo ya cargado.
/// </summary>
public static class CalculadoraCosteIA
{
    private const decimal PorMillon = 1_000_000m;
    private const int DecimalesCoste = 6;

    /// <summary>
    /// Rellena CosteEur y TarifaAplicada del consumo. Si no hay linea aplicable
    /// los deja a nulo, sin lanzar: una tarifa que falta nunca frena una ejecucion.
    /// </summary>
    public static void Aplicar(ConsumoIA consumo, TarifaRegistry registro, DateTime fechaEjecucion)
    {
        if (consumo is null || registro is null)
        {
            return;
        }

        var tarifa = ResolverTarifa(consumo.Modelo, registro, fechaEjecucion);
        if (tarifa is null)
        {
            return;
        }

        var coste = 0m;

        // Los tokens cacheados vienen INCLUIDOS en TokensEntrada. Se restan para
        // no pagarlos dos veces y se tarifican con su precio reducido. El Min
        // protege de datos incoherentes del proveedor (cache > entrada).
        var entradaTotal = Math.Max(0, consumo.TokensEntrada ?? 0);
        var entradaCache = Math.Clamp(consumo.TokensEntradaCache ?? 0, 0, entradaTotal);
        var entradaPlena = entradaTotal - entradaCache;

        if (entradaPlena > 0 && tarifa.EurEntradaPor1M.HasValue)
        {
            coste += entradaPlena / PorMillon * tarifa.EurEntradaPor1M.Value;
        }

        if (entradaCache > 0 && tarifa.EurEntradaCachePor1M.HasValue)
        {
            coste += entradaCache / PorMillon * tarifa.EurEntradaCachePor1M.Value;
        }

        // Los tokens de razonamiento ya vienen incluidos en TokensSalida.
        if (consumo.TokensSalida > 0 && tarifa.EurSalidaPor1M.HasValue)
        {
            coste += consumo.TokensSalida.Value / PorMillon * tarifa.EurSalidaPor1M.Value;
        }

        if (consumo.TokensContextualizacion > 0 && tarifa.EurContextualizacionPor1M.HasValue)
        {
            coste += consumo.TokensContextualizacion.Value / PorMillon * tarifa.EurContextualizacionPor1M.Value;
        }

        if (consumo.Paginas > 0 && tarifa.EurPorPagina.HasValue)
        {
            coste += consumo.Paginas.Value * tarifa.EurPorPagina.Value;
        }

        consumo.CosteEur = Math.Round(coste, DecimalesCoste, MidpointRounding.AwayFromZero);
        consumo.TarifaAplicada = string.Format(
            CultureInfo.InvariantCulture,
            "{0}@{1:yyyy-MM-dd}",
            tarifa.Modelo,
            tarifa.VigenteDesde);
    }

    /// <summary>
    /// Linea de mayor VigenteDesde que no supere la fecha de la ejecucion.
    /// </summary>
    private static TarifaIA? ResolverTarifa(string? modelo, TarifaRegistry registro, DateTime fechaEjecucion)
    {
        if (string.IsNullOrWhiteSpace(modelo))
        {
            return null;
        }

        var clave = modelo.Trim();

        return registro.Tarifas
            .Where(t => string.Equals(t.Modelo?.Trim(), clave, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.VigenteDesde <= fechaEjecucion)
            .OrderByDescending(t => t.VigenteDesde)
            .FirstOrDefault();
    }

    /// <summary>
    /// Agrega una lista de consumos. Los descartados cuentan igual: se han pagado.
    /// </summary>
    public static CostesIA Agregar(IEnumerable<ConsumoIA>? consumos)
    {
        var lista = consumos?.ToList() ?? new List<ConsumoIA>();

        var sinTarifa = lista
            .Where(c => c.CosteEur is null)
            .Select(c => string.IsNullOrWhiteSpace(c.Modelo) ? "(desconocido)" : c.Modelo.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CostesIA
        {
            Consumos = lista,
            CosteTotalEur = Math.Round(
                lista.Sum(c => c.CosteEur ?? 0m),
                DecimalesCoste,
                MidpointRounding.AwayFromZero),
            // Cacheados y razonamiento no se suman: ya estan dentro de entrada y salida.
            TokensTotales = lista.Sum(c =>
                (c.TokensEntrada ?? 0) + (c.TokensSalida ?? 0) + (c.TokensContextualizacion ?? 0)),
            PaginasTotales = lista.Sum(c => c.Paginas ?? 0),
            TarifasCompletas = sinTarifa.Count == 0,
            ModelosSinTarifa = sinTarifa
        };
    }
}
