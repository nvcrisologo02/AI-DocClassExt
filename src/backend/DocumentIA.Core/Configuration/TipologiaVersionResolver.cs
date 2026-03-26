using System.Text.Json;

namespace DocumentIA.Core.Configuration;

public class TipologiaVersionResolver : ITipologiaVersionResolver
{
    private const string ValidationSuffix = ".validation.json";
    private readonly string _configBasePath;
    private readonly Lazy<ResolverIndex> _index;

    public TipologiaVersionResolver(string configBasePath)
    {
        _configBasePath = configBasePath;
        _index = new Lazy<ResolverIndex>(BuildIndex);
    }

    public ResolvedTipologia Resolve(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("La tipologia a resolver no puede estar vacia.", nameof(input));
        }

        var normalizedInput = input.Trim();
        var index = _index.Value;

        if (index.ByTechnicalKey.TryGetValue(normalizedInput, out var directMatch))
        {
            return directMatch with { RequestedValue = normalizedInput };
        }

        var separatorIndex = normalizedInput.IndexOf('@');
        if (separatorIndex >= 0)
        {
            var family = normalizedInput[..separatorIndex].Trim();
            var version = normalizedInput[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(version))
            {
                throw new InvalidOperationException(
                    $"El formato de tipologia '{normalizedInput}' es invalido. Use 'familia' o 'familia@version'.");
            }

            if (!index.ByFamily.TryGetValue(family, out var entries))
            {
                throw new KeyNotFoundException($"No existe la tipologia '{family}'.");
            }

            var versionMatch = entries.FirstOrDefault(entry => string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase));
            if (versionMatch is null)
            {
                throw new KeyNotFoundException($"La version '{version}' no existe para la tipologia '{family}'.");
            }

            return versionMatch with { RequestedValue = normalizedInput };
        }

        if (!index.ByFamily.TryGetValue(normalizedInput, out var familyEntries))
        {
            throw new KeyNotFoundException($"No existe la tipologia '{normalizedInput}'.");
        }

        var defaults = familyEntries.Where(entry => entry.IsDefault).ToList();
        if (defaults.Count == 1)
        {
            return defaults[0] with { RequestedValue = normalizedInput };
        }

        if (defaults.Count > 1)
        {
            throw new InvalidOperationException(
                $"La tipologia '{normalizedInput}' tiene multiples versiones marcadas como default.");
        }

        if (familyEntries.Count == 1)
        {
            return familyEntries[0] with { RequestedValue = normalizedInput };
        }

        throw new InvalidOperationException(
            $"La tipologia '{normalizedInput}' no tiene ninguna version default configurada.");
    }

    public IReadOnlyCollection<string> GetVersions(string tipologiaId)
    {
        if (string.IsNullOrWhiteSpace(tipologiaId))
        {
            return Array.Empty<string>();
        }

        var normalizedTipologiaId = tipologiaId.Trim();
        if (!_index.Value.ByFamily.TryGetValue(normalizedTipologiaId, out var entries))
        {
            return Array.Empty<string>();
        }

        return entries
            .Select(entry => entry.Version)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private ResolverIndex BuildIndex()
    {
        if (!Directory.Exists(_configBasePath))
        {
            throw new DirectoryNotFoundException($"No existe el directorio de tipologias: {_configBasePath}");
        }

        var byTechnicalKey = new Dictionary<string, ResolvedTipologia>(StringComparer.OrdinalIgnoreCase);
        var byFamily = new Dictionary<string, List<ResolvedTipologia>>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in Directory.EnumerateFiles(_configBasePath, $"*{ValidationSuffix}"))
        {
            var technicalKey = Path.GetFileName(filePath)[..^ValidationSuffix.Length];
            var jsonContent = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException($"Configuracion invalida en {filePath}");

            if (string.IsNullOrWhiteSpace(config.TipologiaId))
            {
                throw new InvalidDataException($"La configuracion '{technicalKey}' no define tipologiaId.");
            }

            if (string.IsNullOrWhiteSpace(config.Version))
            {
                throw new InvalidDataException($"La configuracion '{technicalKey}' no define version.");
            }

            var resolved = new ResolvedTipologia(
                RequestedValue: technicalKey,
                TipologiaId: config.TipologiaId.Trim(),
                Version: config.Version.Trim(),
                TechnicalKey: technicalKey,
                IsDefault: config.IsDefault,
                SkipGDCUpload: config.SkipGDCUpload,
                PromptEnabled: config.PromptConfig?.Enabled == true,
                ExtractionEnabled: config.Extraction.Enabled,
                ConfidenceConfig: config.ConfidenceConfig);

            byTechnicalKey[technicalKey] = resolved;

            if (!byFamily.TryGetValue(resolved.TipologiaId, out var familyEntries))
            {
                familyEntries = new List<ResolvedTipologia>();
                byFamily[resolved.TipologiaId] = familyEntries;
            }

            familyEntries.Add(resolved);
        }

        return new ResolverIndex(byTechnicalKey, byFamily);
    }

    private sealed record ResolverIndex(
        Dictionary<string, ResolvedTipologia> ByTechnicalKey,
        Dictionary<string, List<ResolvedTipologia>> ByFamily);
}