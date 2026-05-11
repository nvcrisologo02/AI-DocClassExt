using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

public class PromptModelRegistryLoader
{
    private readonly string? _registryFilePath;
    private readonly IMemoryCache? _cache;
    private readonly IServiceScopeFactory? _scopeFactory;
    private PromptModelRegistry? _cachedRegistry;

    public PromptModelRegistryLoader(string registryFilePath)
    {
        _registryFilePath = registryFilePath;
    }

    public PromptModelRegistryLoader(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public PromptModelRegistry Load()
    {
        if (_cache is not null && _scopeFactory is not null)
        {
            return _cache.GetOrCreate("modelos:prompt", entry =>
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
            throw new InvalidOperationException("PromptModelRegistryLoader no esta correctamente configurado.");
        }

        if (!File.Exists(_registryFilePath))
        {
            throw new FileNotFoundException($"No se encontro el registro de modelos de prompt en {_registryFilePath}");
        }

        var jsonContent = File.ReadAllText(_registryFilePath);
        _cachedRegistry = JsonSerializer.Deserialize<PromptModelRegistry>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException($"Registro de modelos de prompt invalido en {_registryFilePath}");

        return _cachedRegistry;
    }

    public PromptModelConfig GetModel(string modelKey)
    {
        var registry = Load();
        var model = registry.Models.FirstOrDefault(m => string.Equals(m.Key, modelKey, StringComparison.OrdinalIgnoreCase));

        return model ?? throw new KeyNotFoundException($"No se encontro el modelo de prompt '{modelKey}' en {_registryFilePath}");
    }

    private PromptModelRegistry LoadFromDatabase()
    {
        using var scope = _scopeFactory!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IModeloConfigRepository>();
        var models = repository.GetAllActivosByTipoAsync(TipoModelo.Prompt)
            .GetAwaiter()
            .GetResult();

        var registry = new PromptModelRegistry();
        foreach (var modelEntity in models)
        {
            var model = string.IsNullOrWhiteSpace(modelEntity.ConfiguracionJson)
                ? new PromptModelConfig()
                : JsonSerializer.Deserialize<PromptModelConfig>(modelEntity.ConfiguracionJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new PromptModelConfig();

            model.Key = modelEntity.Key;
            model.Provider = modelEntity.Provider;
            registry.Models.Add(model);
        }

        return registry;
    }
}

public class PromptModelRegistry
{
    public List<PromptModelConfig> Models { get; set; } = new();
}

public class PromptModelConfig
{
    public string Key { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "ApiKey";
    public string DeploymentName { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;
}
