using System.Text.Json;

namespace DocumentIA.Core.Configuration;

public class ExtractionModelRegistryLoader
{
    private readonly string _registryFilePath;
    private ExtractionModelRegistry? _cachedRegistry;

    public ExtractionModelRegistryLoader(string registryFilePath)
    {
        _registryFilePath = registryFilePath;
    }

    public ExtractionModelRegistry Load()
    {
        if (_cachedRegistry is not null)
        {
            return _cachedRegistry;
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
}