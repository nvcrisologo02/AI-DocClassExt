using System.Text.Json;

namespace DocumentIA.Core.Configuration;

public class PromptModelRegistryLoader
{
    private readonly string _registryFilePath;
    private PromptModelRegistry? _cachedRegistry;

    public PromptModelRegistryLoader(string registryFilePath)
    {
        _registryFilePath = registryFilePath;
    }

    public PromptModelRegistry Load()
    {
        if (_cachedRegistry is not null)
        {
            return _cachedRegistry;
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
