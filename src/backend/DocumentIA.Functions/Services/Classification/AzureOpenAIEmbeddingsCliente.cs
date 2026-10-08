using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using OpenAI.Embeddings;

namespace DocumentIA.Functions.Services.Classification;

/// <summary>Cliente de embeddings sobre Azure OpenAI (SDK Azure.AI.OpenAI 2.x). AB#100779.</summary>
public sealed class AzureOpenAIEmbeddingsCliente : IEmbeddingsCliente
{
    private readonly EmbeddingClient _cliente;
    private readonly string _deploymentName;

    public AzureOpenAIEmbeddingsCliente(EmbeddingClient cliente, string deploymentName)
    {
        _cliente = cliente;
        _deploymentName = deploymentName;
    }

    public async Task<RespuestaEmbedding> GenerarAsync(string texto, CancellationToken cancellationToken)
    {
        var respuesta = await _cliente.GenerateEmbeddingsAsync(new[] { texto }, cancellationToken: cancellationToken);
        var coleccion = respuesta.Value;
        if (coleccion.Count == 0)
        {
            throw new InvalidOperationException("La API de embeddings no devolvio ningun vector.");
        }

        var vector = coleccion[0].ToFloats().ToArray();
        var consumo = UsoEmbeddingsMapper.Mapear(coleccion.Usage, _deploymentName);
        return new RespuestaEmbedding(vector, consumo);
    }
}

/// <summary>
/// Un cliente por (endpoint, deployment, modo de autenticacion y, con clave, huella de la clave).
/// Los clientes del SDK son seguros para uso concurrente, asi que se comparten entre llamadas.
/// Incluir la huella de la clave hace que una rotacion en ModeloConfigs surta efecto con la
/// cache de configuracion de cinco minutos.
/// </summary>
public sealed class AzureOpenAIEmbeddingsClienteFactory : IEmbeddingsClienteFactory
{
    private readonly ConcurrentDictionary<string, IEmbeddingsCliente> _clientes = new(StringComparer.OrdinalIgnoreCase);

    public IEmbeddingsCliente Crear(EmbeddingsClasificadorConfig config)
    {
        var clave = $"{config.Endpoint}|{config.DeploymentName}|{config.AuthMode}";
        if (!UsaIdentidadGestionada(config))
        {
            clave += "|" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.ApiKey ?? string.Empty)));
        }

        return _clientes.GetOrAdd(clave, _ => Construir(config));
    }

    private static bool UsaIdentidadGestionada(EmbeddingsClasificadorConfig config) =>
        string.Equals(config.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase);

    private static IEmbeddingsCliente Construir(EmbeddingsClasificadorConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Endpoint) || string.IsNullOrWhiteSpace(config.DeploymentName))
        {
            throw new InvalidOperationException("La configuracion del clasificador por embeddings no tiene endpoint o deployment.");
        }

        var opciones = new AzureOpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };

        AzureOpenAIClient azureClient = UsaIdentidadGestionada(config)
            ? new AzureOpenAIClient(new Uri(config.Endpoint), new DefaultAzureCredential(), opciones)
            : new AzureOpenAIClient(new Uri(config.Endpoint), new AzureKeyCredential(config.ApiKey), opciones);

        return new AzureOpenAIEmbeddingsCliente(azureClient.GetEmbeddingClient(config.DeploymentName), config.DeploymentName);
    }
}
