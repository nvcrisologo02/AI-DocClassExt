using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

public class TipologiaVersionResolver : ITipologiaVersionResolver
{
    private const string ValidationSuffix = ".validation.json";
    private readonly string? _configBasePath;
    private readonly IMemoryCache? _cache;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly Lazy<ResolverIndex>? _index;

    public TipologiaVersionResolver(string configBasePath)
    {
        _configBasePath = configBasePath;
        _index = new Lazy<ResolverIndex>(BuildIndex);
    }

    public TipologiaVersionResolver(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public ResolvedTipologia Resolve(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("La tipologia a resolver no puede estar vacia.", nameof(input));
        }

        var normalizedInput = input.Trim();
        var index = GetIndex();

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
        var index = GetIndex();
        if (!index.ByFamily.TryGetValue(normalizedTipologiaId, out var entries))
        {
            return Array.Empty<string>();
        }

        return entries
            .Select(entry => entry.Version)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private ResolverIndex GetIndex()
    {
        if (_cache is not null && _scopeFactory is not null)
        {
            return _cache.GetOrCreate("tipologias:snapshot", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return BuildIndexFromDatabase();
            })!;
        }

        if (_index is null)
        {
            throw new InvalidOperationException("TipologiaVersionResolver no esta correctamente configurado.");
        }

        return _index.Value;
    }

    private ResolverIndex BuildIndex()
    {
        if (_configBasePath is null)
        {
            throw new InvalidOperationException("No se ha configurado ruta base de tipologias.");
        }

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

    private ResolverIndex BuildIndexFromDatabase()
    {
        using var scope = _scopeFactory!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();

        var publishedTipologias = repository.GetAllPublishedAsync()
            .GetAwaiter()
            .GetResult();

        var byTechnicalKey = new Dictionary<string, ResolvedTipologia>(StringComparer.OrdinalIgnoreCase);
        var byFamily = new Dictionary<string, List<ResolvedTipologia>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tipologia in publishedTipologias)
        {
            if (!tipologia.Activa || tipologia.Estado != EstadoTipologia.Published)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
            {
                continue;
            }

            var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(tipologia.ConfiguracionJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config is null || string.IsNullOrWhiteSpace(config.TipologiaId) || string.IsNullOrWhiteSpace(config.Version))
            {
                continue;
            }

            var technicalKey = tipologia.Codigo;
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

            if (!byFamily.TryGetValue(resolved.TipologiaId, out var entries))
            {
                entries = new List<ResolvedTipologia>();
                byFamily[resolved.TipologiaId] = entries;
            }

            entries.Add(resolved);
        }

        return new ResolverIndex(byTechnicalKey, byFamily);
    }

    private sealed record ResolverIndex(
        Dictionary<string, ResolvedTipologia> ByTechnicalKey,
        Dictionary<string, List<ResolvedTipologia>> ByFamily);
}