using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

public class ClassificationModelRegistryLoader
{
    private readonly string? _registryFilePath;
    private readonly IMemoryCache? _cache;
    private readonly IServiceScopeFactory? _scopeFactory;
    private ClassificationModelRegistry? _cachedRegistry;

    public ClassificationModelRegistryLoader(string registryFilePath)
    {
        _registryFilePath = registryFilePath;
    }

    public ClassificationModelRegistryLoader(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public ClassificationModelRegistry Load()
    {
        if (_cache is not null && _scopeFactory is not null)
        {
            return _cache.GetOrCreate("modelos:clasificacion", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return LoadFromDatabase();
            })!;
        }

        if (_cachedRegistry is not null)
        {
            return _cachedRegistry;
        }

        if (_registryFilePath is null)
        {
            throw new InvalidOperationException("ClassificationModelRegistryLoader no esta correctamente configurado.");
        }

        if (!File.Exists(_registryFilePath))
        {
            throw new FileNotFoundException($"No se encontro el registro de modelos de clasificacion en {_registryFilePath}");
        }

        var jsonContent = File.ReadAllText(_registryFilePath);
        _cachedRegistry = JsonSerializer.Deserialize<ClassificationModelRegistry>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException($"Registro de modelos de clasificacion invalido en {_registryFilePath}");

        return _cachedRegistry;
    }

    public ClassificationModelConfig GetModel(string modelKey)
    {
        var registry = Load();
        var model = registry.Models.FirstOrDefault(m => string.Equals(m.Key, modelKey, StringComparison.OrdinalIgnoreCase));

        return model ?? throw new KeyNotFoundException($"No se encontro el modelo de clasificacion '{modelKey}' en {_registryFilePath}");
    }

    public ClassificationModelConfig GetDefaultModel(string? provider = null)
    {
        var registry = Load();
        var candidates = registry.Models
            .Where(m => string.IsNullOrWhiteSpace(provider)
                || string.Equals(m.Provider, provider, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var model = candidates.FirstOrDefault(m => m.IsDefault)
            ?? (candidates.Count == 1 ? candidates[0] : null);

        return model ?? throw new KeyNotFoundException(
            $"No se encontro un modelo de clasificacion por defecto{(string.IsNullOrWhiteSpace(provider) ? string.Empty : $" para provider '{provider}'")}.");
    }

    public ClassificationModelConfig GetFallbackModel()
    {
        var registry = Load();
        var candidates = registry.Models
            .Where(m => m.UseAsFallback)
            .ToList();

        var model = candidates.Count switch
        {
            1 => candidates[0],
            _ => null
        };

        return model ?? throw new KeyNotFoundException(
            "No se encontro un modelo de clasificacion marcado para fallback en base de datos.");
    }

    private ClassificationModelRegistry LoadFromDatabase()
    {
        using var scope = _scopeFactory!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IModeloConfigRepository>();
        var models = repository.GetAllActivosByTipoAsync(TipoModelo.Clasificacion)
            .GetAwaiter()
            .GetResult();

        var registry = new ClassificationModelRegistry();
        foreach (var modelEntity in models)
        {
            var model = string.IsNullOrWhiteSpace(modelEntity.ConfiguracionJson)
                ? new ClassificationModelConfig()
                : JsonSerializer.Deserialize<ClassificationModelConfig>(modelEntity.ConfiguracionJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new ClassificationModelConfig();

            model.Key = modelEntity.Key;
            model.Provider = modelEntity.Provider;
            registry.Models.Add(model);
        }

        return registry;
    }
}

public class ClassificationModelRegistry
{
    public List<ClassificationModelConfig> Models { get; set; } = new();
}

public class ClassificationModelConfig
{
    public string Key { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool UseAsFallback { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "ApiKey";
    public string ClassifierId { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public int PollIntervalMs { get; set; } = 1000;
    public double FallbackThreshold { get; set; } = 0.6;
    public double Temperature { get; set; } = 0.0;
    public int MaxTokens { get; set; } = 150;
}
