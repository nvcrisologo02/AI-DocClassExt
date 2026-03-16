using System.Text.Json;

namespace DocumentIA.Core.Configuration;

public class ClassificationModelRegistryLoader
{
    private readonly string _registryFilePath;
    private ClassificationModelRegistry? _cachedRegistry;

    public ClassificationModelRegistryLoader(string registryFilePath)
    {
        _registryFilePath = registryFilePath;
    }

    public ClassificationModelRegistry Load()
    {
        if (_cachedRegistry is not null)
        {
            return _cachedRegistry;
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
}

public class ClassificationModelRegistry
{
    public List<ClassificationModelConfig> Models { get; set; } = new();
}

public class ClassificationModelConfig
{
    public string Key { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ClassifierId { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
}
