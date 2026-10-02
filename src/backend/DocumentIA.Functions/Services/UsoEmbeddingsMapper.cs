using DocumentIA.Core.Models;
using OpenAI.Embeddings;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Traduce el bloque de uso de una llamada de embeddings a un consumo del contrato.
/// Solo hay tokens de entrada; no reutiliza UsoOpenAiMapper porque el tipo de uso es
/// distinto y no tiene cache ni razonamiento. AB#100779.
/// </summary>
public static class UsoEmbeddingsMapper
{
    public const string Operacion = "classification.embeddings";

    /// <summary>Sin bloque de uso no se registra consumo: la spec solo contabiliza lo que la API declara.</summary>
    public static ConsumoIA? Mapear(EmbeddingTokenUsage? uso, string? modelo)
    {
        if (uso is null)
        {
            return null;
        }

        return new ConsumoIA
        {
            Actividad = ActividadesIA.Clasificar,
            Operacion = Operacion,
            Proveedor = ProveedoresIA.AzureOpenAI,
            Modelo = modelo ?? string.Empty,
            TokensEntrada = uso.InputTokenCount,
            TokensSalida = 0,
            Descartado = false
        };
    }
}
