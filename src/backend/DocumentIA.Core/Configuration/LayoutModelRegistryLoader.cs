using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

public class LayoutModelRegistryLoader
{
    private readonly string? _registryFilePath;
    private readonly IMemoryCache? _cache;
    private readonly IServiceScopeFactory? _scopeFactory;
    private LayoutModelRegistry? _cachedRegistry;

    public LayoutModelRegistryLoader(string registryFilePath)
    {
        _registryFilePath = registryFilePath;
    }

    public LayoutModelRegistryLoader(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public LayoutModelRegistry Load()
    {
        if (_cache is not null && _scopeFactory is not null)
        {
            return _cache.GetOrCreate("modelos:layout", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return LoadFromDatabase();
            })!;
        }

        if (_cachedRegistry is not null)
            return _cachedRegistry;

        if (_registryFilePath is null)
            throw new InvalidOperationException("LayoutModelRegistryLoader no esta correctamente configurado.");

        if (!File.Exists(_registryFilePath))
            throw new FileNotFoundException($"No se encontro el registro de modelos de layout en {_registryFilePath}");

        var jsonContent = File.ReadAllText(_registryFilePath);
        _cachedRegistry = JsonSerializer.Deserialize<LayoutModelRegistry>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException($"Registro de modelos de layout invalido en {_registryFilePath}");

        return _cachedRegistry;
    }

    public LayoutModelConfig GetDefaultModel()
    {
        var registry = Load();
        var model = registry.Models.FirstOrDefault(m => m.IsDefault)
            ?? (registry.Models.Count == 1 ? registry.Models[0] : null);

        return model ?? throw new KeyNotFoundException(
            "No se encontro un modelo de layout por defecto en base de datos.");
    }

    private LayoutModelRegistry LoadFromDatabase()
    {
        using var scope = _scopeFactory!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IModeloConfigRepository>();
        var models = repository.GetAllActivosByTipoAsync(TipoModelo.Layout)
            .GetAwaiter()
            .GetResult();

        var registry = new LayoutModelRegistry();
        foreach (var modelEntity in models)
        {
            var model = string.IsNullOrWhiteSpace(modelEntity.ConfiguracionJson)
                ? new LayoutModelConfig()
                : JsonSerializer.Deserialize<LayoutModelConfig>(modelEntity.ConfiguracionJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new LayoutModelConfig();

            model.Key = modelEntity.Key;
            model.Provider = modelEntity.Provider;
            registry.Models.Add(model);
        }

        return registry;
    }
}

public class LayoutModelRegistry
{
    public List<LayoutModelConfig> Models { get; set; } = new();
}

public class LayoutModelConfig
{
    public string Key { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "ApiKey";
    public string ApiVersion { get; set; } = "2024-11-30";
    public int TimeoutSeconds { get; set; } = 120;
    public int PollIntervalMs { get; set; } = 1000;
}
