using Microsoft.Extensions.Options;

namespace DocumentIA.Core.Configuration;

public interface IAiEndpointResolver
{
    /// <summary>Endpoint explícito si existe; si no, el del alias; vacío si no hay ninguno; excepción si el alias no está mapeado.</summary>
    string Resolve(string? explicitEndpoint, string? resourceAlias, string modelKey);
}

public sealed class AiEndpointResolver : IAiEndpointResolver
{
    public static readonly AiEndpointResolver Empty = new(new Dictionary<string, AiResourceEntry>());

    private readonly Dictionary<string, AiResourceEntry> _resources;

    public AiEndpointResolver(IOptions<AiResourceMapOptions> options)
        : this(options.Value.Resources) { }

    public AiEndpointResolver(IReadOnlyDictionary<string, AiResourceEntry> resources)
    {
        _resources = new Dictionary<string, AiResourceEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (alias, entry) in resources)
        {
            _resources[alias] = entry;
        }
    }

    public string Resolve(string? explicitEndpoint, string? resourceAlias, string modelKey)
    {
        if (!string.IsNullOrWhiteSpace(explicitEndpoint))
        {
            return explicitEndpoint;
        }

        if (string.IsNullOrWhiteSpace(resourceAlias))
        {
            return string.Empty;
        }

        if (_resources.TryGetValue(resourceAlias, out var entry) && !string.IsNullOrWhiteSpace(entry.Endpoint))
        {
            return entry.Endpoint;
        }

        throw new InvalidOperationException(
            $"El modelo '{modelKey}' referencia el alias de recurso '{resourceAlias}' y no existe el App Setting " +
            $"AI__Resources__{resourceAlias}__Endpoint en este entorno.");
    }
}
