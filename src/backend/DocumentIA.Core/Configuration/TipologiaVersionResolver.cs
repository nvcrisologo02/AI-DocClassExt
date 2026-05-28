using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

public class TipologiaVersionResolver : ITipologiaVersionResolver
{
    private readonly IMemoryCache? _cache;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly Func<IEnumerable<TipologiaEntity>>? _entityFactory;

    public TipologiaVersionResolver(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    internal TipologiaVersionResolver(Func<IEnumerable<TipologiaEntity>> entityFactory)
    {
        _entityFactory = entityFactory;
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

            // Compatibilidad con entradas híbridas provenientes de algunos clientes/modelos,
            // por ejemplo: "IBI_1.1@1.1" (technicalKey@version).
            if (index.ByTechnicalKey.TryGetValue(family, out var technicalMatch))
            {
                if (string.Equals(technicalMatch.Version, version, StringComparison.OrdinalIgnoreCase))
                {
                    return technicalMatch with { RequestedValue = normalizedInput };
                }

                throw new KeyNotFoundException(
                    $"La version '{version}' no coincide con la tipologia tecnica '{family}' (version esperada '{technicalMatch.Version}').");
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
                return BuildIndexFromEntities(GetPublishedTipologiasFromDatabase());
            })!;
        }

        if (_entityFactory is null)
        {
            throw new InvalidOperationException("TipologiaVersionResolver no esta correctamente configurado.");
        }

        return BuildIndexFromEntities(_entityFactory());
    }

    private IEnumerable<TipologiaEntity> GetPublishedTipologiasFromDatabase()
    {
        using var scope = _scopeFactory!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();

        return repository.GetAllPublishedAsync()
            .GetAwaiter()
            .GetResult();
    }

    private ResolverIndex BuildIndexFromEntities(IEnumerable<TipologiaEntity> sourceEntities)
    {
        var byTechnicalKey = new Dictionary<string, ResolvedTipologia>(StringComparer.OrdinalIgnoreCase);
        var byFamily = new Dictionary<string, List<ResolvedTipologia>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tipologia in sourceEntities)
        {
            if (!tipologia.Activa || tipologia.Estado != EstadoTipologia.Published)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
            {
                continue;
            }

            var technicalKey = tipologia.Codigo;
            if (string.IsNullOrWhiteSpace(technicalKey))
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
            var resolved = new ResolvedTipologia(
                RequestedValue: technicalKey,
                TipologiaId: config.TipologiaId.Trim(),
                Version: config.Version.Trim(),
                TechnicalKey: technicalKey,
                IsDefault: config.IsDefault,
                SkipGDCUpload: config.ResolvedSkipGDCUpload,
                PromptEnabled: config.PromptConfig?.Enabled == true,
                ExtractionEnabled: config.Extraction.Enabled,
                ConfidenceConfig: config.ConfidenceConfig,
                ExtractionProvider: config.Extraction.Provider ?? string.Empty,
                AssetResolverEnabled: config.AssetResolver?.Enabled == true,
                AssetResolverCamposSolicitados: config.AssetResolver?.CamposSolicitados,
                AssetResolverModoCombinacionCriterios: config.AssetResolver?.ModoCombinacionCriterios ?? "OR",
                AssetResolverMapeoIdufir: config.AssetResolver?.MapeoIdufir,
                AssetResolverMapeoReferenciaCatastral: config.AssetResolver?.MapeoReferenciaCatastral,
                AssetResolverBusquedaIdufirHabilitada: config.AssetResolver?.BusquedaIdufirHabilitada ?? true,
                AssetResolverBusquedaReferenciaCatastralHabilitada: config.AssetResolver?.BusquedaReferenciaCatastralHabilitada ?? true,
                AssetResolverBusquedaDireccionHabilitada: config.AssetResolver?.BusquedaDireccionHabilitada == true,
                AssetResolverMapeoDireccionCompleta: config.AssetResolver?.MapeoDireccionCompleta,
                AssetResolverMapeoDireccionNombreVia: config.AssetResolver?.MapeoDireccionNombreVia,
                AssetResolverMapeoDireccionNumero: config.AssetResolver?.MapeoDireccionNumero,
                AssetResolverMapeoDireccionMunicipio: config.AssetResolver?.MapeoDireccionMunicipio,
                AssetResolverMapeoDireccionCodigoPostal: config.AssetResolver?.MapeoDireccionCodigoPostal,
                AssetResolverUmbralScoreDireccion: config.AssetResolver?.UmbralScoreDireccion ?? 0.75,
                TipologiaNombre: config.TipologiaNombre ?? string.Empty,
                TipologiaMGDCMatricula: config.ResolvedMatricula ?? string.Empty,
                GdcTipoDocumento: config.ResolvedGdcTipo ?? string.Empty,
                GdcSubtipoDocumento: config.ResolvedGdcSubtipo ?? string.Empty,
                GdcSerie: config.ResolvedGdcSerie ?? string.Empty,
                Tdn1: config.ResolvedTdn1 ?? string.Empty,
                Tdn2: config.ResolvedTdn2 ?? string.Empty,
                GptDescripcion: config.ResolvedGptDescripcion ?? string.Empty,
                PromptHasDefinition: HasPromptDefinition(config.PromptConfig));

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

    private static bool HasPromptDefinition(PromptConfig? promptConfig)
    {
        return promptConfig is not null &&
            (!string.IsNullOrWhiteSpace(promptConfig.SystemPrompt) ||
             !string.IsNullOrWhiteSpace(promptConfig.UserPromptTemplate));
    }
}