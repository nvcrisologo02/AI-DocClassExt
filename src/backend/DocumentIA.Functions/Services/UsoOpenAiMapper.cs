using DocumentIA.Core.Models;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Traduce el bloque de uso que devuelve el SDK de OpenAI a un consumo del contrato.
///
/// Vive en Functions y no en Core porque depende del SDK de OpenAI, que Core no
/// referencia. Los nombres canonicos de proveedor y actividad si estan en Core
/// (ProveedoresIA, ActividadesIA) para poder compartirlos.
/// </summary>
public static class UsoOpenAiMapper
{
    /// <summary>
    /// Construye el consumo de una llamada. Un uso nulo produce igualmente un
    /// consumo, sin cifras: interesa saber que la llamada se hizo y se pago.
    /// </summary>
    public static ConsumoIA Mapear(ChatTokenUsage? uso, string actividad, string operacion, string? modelo)
    {
        var consumo = new ConsumoIA
        {
            Actividad = actividad,
            Operacion = operacion,
            Proveedor = ProveedoresIA.AzureOpenAI,
            Modelo = modelo ?? string.Empty
        };

        if (uso is null)
        {
            return consumo;
        }

        // InputTokenCount ya incluye los cacheados; se guardan tal cual y es la
        // calculadora quien resta. OutputTokenCount ya incluye el razonamiento.
        consumo.TokensEntrada = uso.InputTokenCount;
        consumo.TokensSalida = uso.OutputTokenCount;
        consumo.TokensEntradaCache = uso.InputTokenDetails?.CachedTokenCount;
        consumo.TokensRazonamiento = uso.OutputTokenDetails?.ReasoningTokenCount;

        return consumo;
    }
}
