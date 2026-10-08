using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Services.Classification;

/// <summary>Vector del documento y consumo de la llamada (nulo si la API no devolvio usage). AB#100779.</summary>
public sealed record RespuestaEmbedding(float[] Vector, ConsumoIA? Consumo);

/// <summary>Una llamada de embeddings sobre un deployment concreto. Lanza ClientResultException ante 429/5xx.</summary>
public interface IEmbeddingsCliente
{
    Task<RespuestaEmbedding> GenerarAsync(string texto, CancellationToken cancellationToken);
}

/// <summary>Crea (y reutiliza) el cliente para la configuracion vigente: endpoint, deployment y autenticacion.</summary>
public interface IEmbeddingsClienteFactory
{
    IEmbeddingsCliente Crear(EmbeddingsClasificadorConfig config);
}
