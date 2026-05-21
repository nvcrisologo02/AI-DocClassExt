using System.Globalization;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services.Classification;

public interface ITipologiaClassificationProfileProvider
{
    IReadOnlyList<TipologiaClassificationProfile> GetProfiles();
}

public sealed class DbTipologiaClassificationProfileProvider : ITipologiaClassificationProfileProvider
{
    private const string CacheKey = "classification.v1_4.tipologias";
    private readonly ILogger<DbTipologiaClassificationProfileProvider> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly HybridTdnOptions _options;
    private readonly IReadOnlyList<TipologiaClassificationProfile> _profiles;

    public DbTipologiaClassificationProfileProvider(
        ILogger<DbTipologiaClassificationProfileProvider> logger,
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        IOptions<HybridTdnOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _options = options.Value;
        _profiles = Array.Empty<TipologiaClassificationProfile>();
    }

    public IReadOnlyList<TipologiaClassificationProfile> GetProfiles()
    {
        if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<TipologiaClassificationProfile>? cached)
            && cached is not null)
        {
            return cached;
        }

        var loaded = LoadProfilesFromDatabase();
        _memoryCache.Set(
            CacheKey,
            loaded,
            TimeSpan.FromSeconds(Math.Max(30, _options.RulesCacheSeconds)));

        return loaded;
    }

    private IReadOnlyList<TipologiaClassificationProfile> LoadProfilesFromDatabase()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentIADbContext>();

        var tipologias = db.Tipologias
            .AsNoTracking()
            .Where(t => t.Activa && t.Estado == EstadoTipologia.Published)
            .OrderByDescending(t => t.FechaActualizacion ?? t.FechaCreacion)
            .ToList();

        var profiles = new List<TipologiaClassificationProfile>();
        foreach (var tipologia in tipologias)
        {
            var profile = TryBuildProfile(tipologia);
            if (profile is not null)
            {
                profiles.Add(profile);
            }
        }

        if (profiles.Count == 0)
        {
            _logger.LogWarning("No hay tipologías de clasificación v1.4 en BBDD (Tipologias.ConfiguracionJson). Se usará fallback legacy.");
            return _profiles;
        }

        _logger.LogInformation("Cargadas {Count} tipologías de clasificación v1.4 desde BBDD.", profiles.Count);
        return profiles;
    }

    private static TipologiaClassificationProfile? TryBuildProfile(TipologiaEntity tipologia)
    {
        if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
        {
            return null;
        }

        try
        {
            var json = tipologia.ConfiguracionJson;
            var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            using var jsonDoc = JsonDocument.Parse(json);
            if (jsonDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            TipologiaClassificationDefinition? definition = JsonSerializer.Deserialize<TipologiaClassificationDefinition>(json, deserializeOptions);
            if (definition?.ClassificationConfig is null)
            {
                if (!jsonDoc.RootElement.TryGetProperty("classificationConfig", out var classificationConfigNode) ||
                    classificationConfigNode.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var config = JsonSerializer.Deserialize<TipologiaClassificationConfig>(classificationConfigNode.GetRawText(), deserializeOptions);
                if (config is null)
                {
                    return null;
                }

                definition = new TipologiaClassificationDefinition
                {
                    Codigo = tipologia.Codigo,
                    VersionPropuesta = tipologia.Version,
                    ClassificationConfig = config
                };
            }

            if (string.IsNullOrWhiteSpace(definition.Codigo))
            {
                definition.Codigo = tipologia.Codigo;
            }

            return TipologiaClassificationProfile.FromDefinition(definition);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class TipologiaClassificationDefinition
{
    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("versionPropuesta")]
    public string VersionPropuesta { get; set; } = string.Empty;

    [JsonPropertyName("classificationConfig")]
    public TipologiaClassificationConfig? ClassificationConfig { get; set; }
}

public sealed class TipologiaClassificationConfig
{
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("allowAsFallback")]
    public bool AllowAsFallback { get; set; }

    [JsonPropertyName("pagesToInspect")]
    public int PagesToInspect { get; set; } = 5;

    [JsonPropertyName("strongSignals")]
    public List<string> StrongSignals { get; set; } = new();

    [JsonPropertyName("positiveSignals")]
    public List<string> PositiveSignals { get; set; } = new();

    [JsonPropertyName("negativeSignals")]
    public List<string> NegativeSignals { get; set; } = new();

    [JsonPropertyName("outOfScopeSignals")]
    public List<string> OutOfScopeSignals { get; set; } = new();

    [JsonPropertyName("minimumSignalScore")]
    public double MinimumSignalScore { get; set; }

    [JsonPropertyName("minimumMarginOverSecond")]
    public double MinimumMarginOverSecond { get; set; }

    [JsonPropertyName("metadataBonus")]
    public List<MetadataBonusDefinition> MetadataBonus { get; set; } = new();

    [JsonPropertyName("immediateClassificationTriggers")]
    public ImmediateClassificationTriggersDefinition? ImmediateClassificationTriggers { get; set; }
}

public sealed class MetadataBonusDefinition
{
    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }
}

public sealed class ImmediateClassificationTriggersDefinition
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "firstPage";

    [JsonPropertyName("maxCharsToScan")]
    public int MaxCharsToScan { get; set; } = 1000;

    [JsonPropertyName("triggers")]
    public List<string> Triggers { get; set; } = new();

    [JsonPropertyName("resultScore")]
    public double ResultScore { get; set; } = 1.0;

    [JsonPropertyName("vetoedByOutOfScope")]
    public bool VetoedByOutOfScope { get; set; } = true;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class TipologiaClassificationProfile
{
    public string Codigo { get; init; } = string.Empty;
    public int Priority { get; init; }
    public bool AllowAsFallback { get; init; }
    public int PagesToInspect { get; init; } = 5;
    public double MinimumSignalScore { get; init; }
    public double MinimumMarginOverSecond { get; init; }

    public IReadOnlyList<string> StrongSignals { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PositiveSignals { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NegativeSignals { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OutOfScopeSignals { get; init; } = Array.Empty<string>();
    public IReadOnlyList<MetadataBonusRule> MetadataBonus { get; init; } = Array.Empty<MetadataBonusRule>();
    public ImmediateClassificationTriggersProfile? ImmediateClassificationTriggers { get; init; }

    public static TipologiaClassificationProfile FromDefinition(TipologiaClassificationDefinition definition)
    {
        var config = definition.ClassificationConfig!;
        return new TipologiaClassificationProfile
        {
            Codigo = definition.Codigo.Trim(),
            Priority = config.Priority,
            AllowAsFallback = config.AllowAsFallback,
            PagesToInspect = config.PagesToInspect > 0 ? config.PagesToInspect : 5,
            MinimumSignalScore = config.MinimumSignalScore,
            MinimumMarginOverSecond = config.MinimumMarginOverSecond,
            StrongSignals = SignalNormalization.NormalizeSignals(config.StrongSignals),
            PositiveSignals = SignalNormalization.NormalizeSignals(config.PositiveSignals),
            NegativeSignals = SignalNormalization.NormalizeSignals(config.NegativeSignals),
            OutOfScopeSignals = SignalNormalization.NormalizeSignals(config.OutOfScopeSignals),
            MetadataBonus = config.MetadataBonus
                .Select(MetadataBonusRule.Parse)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList(),
            ImmediateClassificationTriggers = ImmediateClassificationTriggersProfile.FromDefinition(config.ImmediateClassificationTriggers)
        };
    }
}

public sealed class ImmediateClassificationTriggersProfile
{
    public bool Enabled { get; init; }
    public string Scope { get; init; } = "firstPage";
    public int MaxCharsToScan { get; init; } = 1000;
    public IReadOnlyList<string> Triggers { get; init; } = Array.Empty<string>();
    public double ResultScore { get; init; } = 1.0;
    public bool VetoedByOutOfScope { get; init; } = true;
    public string? Description { get; init; }

    public static ImmediateClassificationTriggersProfile? FromDefinition(ImmediateClassificationTriggersDefinition? definition)
    {
        if (definition is null || !definition.Enabled)
        {
            return null;
        }

        return new ImmediateClassificationTriggersProfile
        {
            Enabled = true,
            Scope = string.IsNullOrWhiteSpace(definition.Scope) ? "firstPage" : definition.Scope.Trim(),
            MaxCharsToScan = definition.MaxCharsToScan > 0 ? definition.MaxCharsToScan : 600,
            Triggers = SignalNormalization.NormalizeSignals(definition.Triggers),
            ResultScore = definition.ResultScore > 0 ? definition.ResultScore : 1.0,
            VetoedByOutOfScope = definition.VetoedByOutOfScope,
            Description = definition.Description
        };
    }
}

internal static class SignalNormalization
{
    public static IReadOnlyList<string> NormalizeSignals(IEnumerable<string> signals)
    {
        return signals
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

public sealed class MetadataBonusRule
{
    private readonly string _operatorTotal;
    private readonly int _valueTotal;
    private readonly string _operatorChars;
    private readonly int _valueChars;

    private MetadataBonusRule(string operatorTotal, int valueTotal, string operatorChars, int valueChars, double score)
    {
        _operatorTotal = operatorTotal;
        _valueTotal = valueTotal;
        _operatorChars = operatorChars;
        _valueChars = valueChars;
        Score = score;
    }

    public double Score { get; }

    public static MetadataBonusRule? Parse(MetadataBonusDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Condition))
        {
            return null;
        }

        // Soportamos condiciones del tipo: TotalPaginas < 8 AND CharsTextoNativo < 300
        var parts = definition.Condition
            .Split("AND", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            return null;
        }

        if (!TryParsePredicate(parts[0], "TotalPaginas", out var opTotal, out var valTotal))
        {
            return null;
        }

        if (!TryParsePredicate(parts[1], "CharsTextoNativo", out var opChars, out var valChars))
        {
            return null;
        }

        return new MetadataBonusRule(opTotal, valTotal, opChars, valChars, definition.Score);
    }

    public bool Matches(int totalPaginas, int charsTextoNativo)
    {
        return Evaluate(totalPaginas, _operatorTotal, _valueTotal)
            && Evaluate(charsTextoNativo, _operatorChars, _valueChars);
    }

    private static bool TryParsePredicate(string predicate, string expectedField, out string comparisonOperator, out int value)
    {
        comparisonOperator = string.Empty;
        value = 0;

        var cleaned = predicate.Trim();
        var operators = new[] { "<=", ">=", "<", ">", "=" };
        var selectedOperator = operators.FirstOrDefault(cleaned.Contains);

        if (selectedOperator is null)
        {
            return false;
        }

        var tokens = cleaned.Split(selectedOperator, StringSplitOptions.TrimEntries);
        if (tokens.Length != 2)
        {
            return false;
        }

        if (!tokens[0].Equals(expectedField, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        comparisonOperator = selectedOperator;
        return true;
    }

    private static bool Evaluate(int candidate, string comparisonOperator, int expected)
    {
        return comparisonOperator switch
        {
            "<" => candidate < expected,
            "<=" => candidate <= expected,
            ">" => candidate > expected,
            ">=" => candidate >= expected,
            "=" => candidate == expected,
            _ => false
        };
    }
}
