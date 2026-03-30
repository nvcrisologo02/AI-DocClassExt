using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

public class ExtractionModelRegistryLoader
{
    private readonly string? _registryFilePath;
    private readonly IMemoryCache? _cache;
    private readonly IServiceScopeFactory? _scopeFactory;
    private ExtractionModelRegistry? _cachedRegistry;

    public ExtractionModelRegistryLoader(string registryFilePath)
    {
        _registryFilePath = registryFilePath;
    }

    public ExtractionModelRegistryLoader(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public ExtractionModelRegistry Load()
    {
        if (_cache is not null && _scopeFactory is not null)
        {
            return _cache.GetOrCreate("modelos:extraccion", entry =>
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
            throw new InvalidOperationException("ExtractionModelRegistryLoader no esta correctamente configurado.");
        }

        if (!File.Exists(_registryFilePath))
        {
            throw new FileNotFoundException($"No se encontro el registro de modelos de extraccion en {_registryFilePath}");
        }

        var jsonContent = File.ReadAllText(_registryFilePath);
        _cachedRegistry = JsonSerializer.Deserialize<ExtractionModelRegistry>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException($"Registro de modelos de extraccion invalido en {_registryFilePath}");

        return _cachedRegistry;
    }

    public ExtractionModelConfig GetModel(string modelKey)
    {
        var registry = Load();
        var model = registry.Models.FirstOrDefault(m => string.Equals(m.Key, modelKey, StringComparison.OrdinalIgnoreCase));

        return model ?? throw new KeyNotFoundException($"No se encontro el modelo de extraccion '{modelKey}' en {_registryFilePath}");
    }

    private ExtractionModelRegistry LoadFromDatabase()
    {
        using var scope = _scopeFactory!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IModeloConfigRepository>();
        var models = repository.GetAllActivosByTipoAsync(TipoModelo.Extraccion)
            .GetAwaiter()
            .GetResult();

        var registry = new ExtractionModelRegistry();
        foreach (var modelEntity in models)
        {
            var model = string.IsNullOrWhiteSpace(modelEntity.ConfiguracionJson)
                ? new ExtractionModelConfig()
                : JsonSerializer.Deserialize<ExtractionModelConfig>(modelEntity.ConfiguracionJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new ExtractionModelConfig();

            model.Key = modelEntity.Key;
            model.Provider = modelEntity.Provider;
            registry.Models.Add(model);
        }

        return registry;
    }
}