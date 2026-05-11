using System.Text.Json;
using System.Globalization;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;
using Microsoft.JSInterop;

namespace DocumentIA.Admin.Services;

public sealed class TipologiaWizardStateService
{
    private const string DraftStorageKey = "tipologia-wizard-draft";
    private const int DraftSchemaVersion = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public TipologiaWizardDraft Draft { get; private set; } = TipologiaWizardDraft.CreateDefault();
    public int CurrentStep { get; private set; } = 1;
    public int TotalSteps => 5;

    public async Task LoadDraftAsync(IJSRuntime js)
    {
        var draftJson = await js.InvokeAsync<string?>("wizardDraftInterop.get", DraftStorageKey);
        if (string.IsNullOrWhiteSpace(draftJson))
        {
            return;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<WizardDraftEnvelope>(draftJson, JsonOptions);
            if (envelope is null || envelope.SchemaVersion != DraftSchemaVersion || envelope.Draft is null)
            {
                return;
            }

            Draft = envelope.Draft;
            CurrentStep = Math.Clamp(envelope.CurrentStep, 1, TotalSteps);
            NormalizeDraft();
        }
        catch
        {
            // Ignore malformed browser draft data and keep defaults.
            Reset();
        }
    }

    public async Task SaveDraftAsync(IJSRuntime js)
    {
        var envelope = new WizardDraftEnvelope
        {
            SchemaVersion = DraftSchemaVersion,
            CurrentStep = CurrentStep,
            Draft = Draft,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        await js.InvokeVoidAsync("wizardDraftInterop.set", DraftStorageKey, json);
    }

    public async Task ClearDraftAsync(IJSRuntime js)
    {
        await js.InvokeVoidAsync("wizardDraftInterop.remove", DraftStorageKey);
        Reset();
    }

    public void Reset()
    {
        Draft = TipologiaWizardDraft.CreateDefault();
        CurrentStep = 1;
        NormalizeDraft();
    }

    public void NextStep()
    {
        CurrentStep = Math.Min(CurrentStep + 1, TotalSteps);
    }

    public void PreviousStep()
    {
        CurrentStep = Math.Max(CurrentStep - 1, 1);
    }

    public void GoToStep(int step)
    {
        CurrentStep = Math.Clamp(step, 1, TotalSteps);
    }

    public void ApplyTemplate(string templateCode)
    {
        Draft.TemplateCode = templateCode;

        switch (templateCode)
        {
            case "notasimple":
                if (string.IsNullOrWhiteSpace(Draft.Nombre))
                {
                    Draft.Nombre = "Nota simple";
                }
                if (string.IsNullOrWhiteSpace(Draft.ModeloClasificacionDI))
                {
                    Draft.ModeloClasificacionDI = "di-clasificacion-default";
                }
                if (string.IsNullOrWhiteSpace(Draft.ModeloExtraccionDI))
                {
                    Draft.EnableExtraction = true;
                    Draft.ModeloExtraccionDI = "cu-notasimple-v1";
                }
                if (string.IsNullOrWhiteSpace(Draft.GdcTipoDocumento))
                {
                    Draft.GdcTipoDocumento = "NOTS";
                }
                if (string.IsNullOrWhiteSpace(Draft.GdcSerie))
                {
                    Draft.GdcSerie = "AI09";
                }
                Draft.SkipGdcUpload = false;
                break;
            case "tasacion":
                if (string.IsNullOrWhiteSpace(Draft.Nombre))
                {
                    Draft.Nombre = "Tasacion";
                }
                if (string.IsNullOrWhiteSpace(Draft.ModeloClasificacionDI))
                {
                    Draft.ModeloClasificacionDI = "di-clasificacion-default";
                }
                if (string.IsNullOrWhiteSpace(Draft.ModeloExtraccionDI))
                {
                    Draft.EnableExtraction = true;
                    Draft.ModeloExtraccionDI = "cu-tasacion-v1";
                }
                if (string.IsNullOrWhiteSpace(Draft.GdcTipoDocumento))
                {
                    Draft.GdcTipoDocumento = "TASA";
                }
                if (string.IsNullOrWhiteSpace(Draft.GdcSerie))
                {
                    Draft.GdcSerie = "AI05";
                }
                Draft.SkipGdcUpload = false;
                break;
            case "generica":
                if (string.IsNullOrWhiteSpace(Draft.Nombre))
                {
                    Draft.Nombre = "Documento generico";
                }
                if (string.IsNullOrWhiteSpace(Draft.ModeloClasificacionDI))
                {
                    Draft.ModeloClasificacionDI = "di-clasificacion-default";
                }
                if (string.IsNullOrWhiteSpace(Draft.ModeloExtraccionDI))
                {
                    Draft.EnableExtraction = false;
                }
                Draft.SkipGdcUpload = true;
                break;
        }
    }

    public bool EnsureVersionedCodigoAndVersion(IReadOnlyCollection<TipologiaEntity> tipologias)
    {
        var suggestion = SuggestVersionedCodigoAndVersion(tipologias);
        if (!suggestion.HasChanges)
        {
            return false;
        }

        Draft.Codigo = suggestion.Codigo;
        Draft.Version = suggestion.Version;
        return true;
    }

    public TipologiaWizardVersionSuggestion SuggestVersionedCodigoAndVersion(IReadOnlyCollection<TipologiaEntity> tipologias)
    {
        var changed = false;
        var baseCodigo = Draft.Codigo.Trim();
        if (string.IsNullOrWhiteSpace(baseCodigo))
        {
            return new TipologiaWizardVersionSuggestion(Draft.Codigo, Draft.Version, false);
        }

        var suggestedCodigo = Draft.Codigo;
        var suggestedVersion = Draft.Version;

        var sameFamily = tipologias
            .Where(t => string.Equals(t.Codigo, baseCodigo, StringComparison.OrdinalIgnoreCase)
                || t.Codigo.StartsWith(baseCodigo + "-v", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (sameFamily.Length == 0)
        {
            return new TipologiaWizardVersionSuggestion(suggestedCodigo, suggestedVersion, false);
        }

        if (sameFamily.Any(t => string.Equals(t.Codigo, baseCodigo, StringComparison.OrdinalIgnoreCase)))
        {
            var suffixes = sameFamily
                .Select(t => TryExtractVersionedSuffix(baseCodigo, t.Codigo))
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .DefaultIfEmpty(1)
                .ToArray();

            var nextSuffix = suffixes.Max() + 1;
            suggestedCodigo = $"{baseCodigo}-v{nextSuffix}";
            changed = true;
        }

        var nextVersion = SuggestNextVersion(sameFamily.Select(t => t.Version));
        if (!string.Equals(nextVersion, Draft.Version, StringComparison.OrdinalIgnoreCase))
        {
            suggestedVersion = nextVersion;
            changed = true;
        }

        return new TipologiaWizardVersionSuggestion(suggestedCodigo, suggestedVersion, changed);
    }

    public async Task<bool> TryCloneFromTipologiaAsync(int id, TipologiaAdminService COMPLETAR_GDC_HTTP_BASIC_USERNAMEService)
    {
        var source = await COMPLETAR_GDC_HTTP_BASIC_USERNAMEService.GetTipologiaAsync(id);
        if (source is null)
        {
            return false;
        }

        Draft.StartMode = TipologiaWizardStartMode.Clonar;
        Draft.SourceTipologiaId = source.Id;
        Draft.Codigo = $"{source.Codigo}-copy";
        Draft.Nombre = source.Nombre;
        Draft.Version = source.Version;
        Draft.ModeloClasificacionDI = source.ModeloClasificacionDI ?? string.Empty;
        Draft.UmbralClasificacion = source.UmbralClasificacion;
        Draft.ModeloExtraccionDI = source.ModeloExtraccionDI ?? string.Empty;
        Draft.UmbralExtraccion = source.UmbralExtraccion;
        Draft.PromptGPT = source.PromptGPT ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(source.ConfiguracionJson))
        {
            try
            {
                var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(source.ConfiguracionJson, JsonOptions);
                if (config is not null)
                {
                    Draft.GdcTipoDocumento = config.GdcTipoDocumento;
                    Draft.GdcSubtipoDocumento = config.GdcSubtipoDocumento;
                    Draft.GdcSerie = config.GdcSerie;
                    Draft.TipologiaMGDCMatricula = config.TipologiaMGDCMatricula;
                    Draft.SkipGdcUpload = config.SkipGDCUpload;
                }
            }
            catch
            {
                // Ignore invalid source config and keep values already copied from entity.
            }
        }

        return true;
    }

    public string BuildConfigurationJson()
    {
        var fieldConfigs = Draft.Fields
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .Select(f => new FieldValidationConfig
            {
                Name = f.Name.Trim(),
                Type = f.Type.Trim(),
                Required = f.Required,
                Description = f.Description ?? string.Empty,
                Rules = f.Rules
                    .Where(r => !string.IsNullOrWhiteSpace(r.RuleType))
                    .Select(r => new ValidationRuleConfig
                    {
                        RuleType = r.RuleType.Trim(),
                        Severity = string.IsNullOrWhiteSpace(r.Severity) ? "Error" : r.Severity.Trim(),
                        Parameters = BuildRuleParameters(r)
                    })
                    .ToList()
            })
            .ToList();

        var fieldMappings = Draft.Fields
            .Where(f => !string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.SourcePath))
            .Select(f => new ExtractionFieldMappingConfig
            {
                TargetField = f.Name.Trim(),
                SourcePath = f.SourcePath!.Trim()
            })
            .ToList();

        var requestedAssetFields = string.IsNullOrWhiteSpace(Draft.AssetResolverCamposSolicitados)
            ? null
            : Draft.AssetResolverCamposSolicitados
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var config = new TipologiaValidationConfig
        {
            TipologiaId = Draft.Codigo,
            TipologiaNombre = Draft.Nombre,
            Version = Draft.Version,
            TipologiaMGDCMatricula = Draft.TipologiaMGDCMatricula,
            GdcTipoDocumento = Draft.GdcTipoDocumento,
            GdcSubtipoDocumento = Draft.GdcSubtipoDocumento,
            GdcSerie = Draft.GdcSerie,
            SkipGDCUpload = Draft.SkipGdcUpload,
            Extraction = new TipologiaExtractionConfig
            {
                Enabled = Draft.EnableExtraction,
                Provider = "ContentUnderstanding",
                ModelKey = Draft.EnableExtraction ? Draft.ModeloExtraccionDI : string.Empty,
                FieldMappings = fieldMappings
            },
            ConfidenceConfig = new ConfidenceConfig
            {
                ClasifUmbralFallback = Draft.UmbralClasificacion,
                ExtracUmbralFallback = Draft.UmbralExtraccion
            },
            PromptConfig = Draft.EnablePrompting
                ? new PromptConfig
                {
                    Enabled = true,
                    ModelKey = Draft.PromptModelKey,
                    SystemPrompt = "Eres un asistente experto en análisis documental.",
                    UserPromptTemplate = Draft.PromptGPT
                }
                : null,
            AssetResolver = new TipologiaAssetResolverConfig
            {
                Enabled = Draft.EnableAssetResolver,
                CamposSolicitados = requestedAssetFields
            },
            Fields = fieldConfigs
        };

        return JsonSerializer.Serialize(config, JsonOptions);
    }

    public void AddField()
    {
        Draft.Fields.Add(new TipologiaWizardFieldDraft
        {
            Name = string.Empty,
            Type = "string",
            Required = false
        });
    }

    public void RemoveFieldAt(int index)
    {
        if (index < 0 || index >= Draft.Fields.Count)
        {
            return;
        }

        Draft.Fields.RemoveAt(index);
    }

    public void AddRule(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= Draft.Fields.Count)
        {
            return;
        }

        Draft.Fields[fieldIndex].Rules.Add(new TipologiaWizardRuleDraft
        {
            RuleType = string.Empty,
            Severity = "Error"
        });
    }

    public void RemoveRule(int fieldIndex, int ruleIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= Draft.Fields.Count)
        {
            return;
        }

        var rules = Draft.Fields[fieldIndex].Rules;
        if (ruleIndex < 0 || ruleIndex >= rules.Count)
        {
            return;
        }

        rules.RemoveAt(ruleIndex);
    }

    private static Dictionary<string, object?> BuildRuleParameters(TipologiaWizardRuleDraft rule)
    {
        var p = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        switch (rule.RuleType.ToLowerInvariant())
        {
            case "range":
                if (!string.IsNullOrWhiteSpace(rule.RangeMin) &&
                    decimal.TryParse(rule.RangeMin, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rMin))
                    p["min"] = rMin;
                if (!string.IsNullOrWhiteSpace(rule.RangeMax) &&
                    decimal.TryParse(rule.RangeMax, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rMax))
                    p["max"] = rMax;
                break;
            case "regex":
                if (!string.IsNullOrWhiteSpace(rule.RegexPattern))
                    p["pattern"] = rule.RegexPattern.Trim();
                break;
            case "enum":
                var enumVals = rule.EnumValues
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                if (enumVals.Count > 0)
                    p["values"] = enumVals;
                p["caseSensitive"] = rule.EnumCaseSensitive;
                break;
            case "date":
                var fmts = rule.DateFormats
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToArray();
                if (fmts.Length > 0)
                    p["formats"] = fmts;
                p["allowFuture"] = rule.DateAllowFuture;
                p["allowPast"] = rule.DateAllowPast;
                break;
            case "minlength":
                if (!string.IsNullOrWhiteSpace(rule.LengthValue) &&
                    int.TryParse(rule.LengthValue, out var minLen))
                    p["value"] = minLen;
                break;
            case "maxlength":
                if (!string.IsNullOrWhiteSpace(rule.LengthValue) &&
                    int.TryParse(rule.LengthValue, out var maxLen))
                    p["value"] = maxLen;
                break;
        }
        return p;
    }

    private static Dictionary<string, object?> ParseRuleParameters(string? raw)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        var lines = raw
            .Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            result[parts[0]] = ParseScalar(parts[1]);
        }

        return result;
    }

    private static object? ParseScalar(string value)
    {
        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        return value;
    }

    private void NormalizeDraft()
    {
        Draft.Fields ??= new List<TipologiaWizardFieldDraft>();
        foreach (var field in Draft.Fields)
        {
            field.Name ??= string.Empty;
            field.Type = string.IsNullOrWhiteSpace(field.Type) ? "string" : field.Type;
            field.Description ??= string.Empty;
            field.SourcePath ??= string.Empty;
            field.Rules ??= new List<TipologiaWizardRuleDraft>();

            foreach (var rule in field.Rules)
            {
                rule.RuleType ??= string.Empty;
                rule.Severity = string.IsNullOrWhiteSpace(rule.Severity) ? "Error" : rule.Severity;
                rule.RangeMin ??= string.Empty;
                rule.RangeMax ??= string.Empty;
                rule.RegexPattern ??= string.Empty;
                rule.EnumValues ??= string.Empty;
                rule.DateFormats ??= string.Empty;
                rule.LengthValue ??= string.Empty;
            }
        }

        Draft.Codigo ??= string.Empty;
        Draft.Nombre ??= string.Empty;
        Draft.Version = string.IsNullOrWhiteSpace(Draft.Version) ? "1.0.0" : Draft.Version;
        Draft.ModeloClasificacionDI ??= string.Empty;
        Draft.ModeloExtraccionDI ??= string.Empty;
        Draft.PromptModelKey ??= string.Empty;
        Draft.PromptGPT ??= string.Empty;
        Draft.AssetResolverCamposSolicitados ??= string.Empty;
        Draft.GdcTipoDocumento ??= string.Empty;
        Draft.GdcSubtipoDocumento ??= string.Empty;
        Draft.GdcSerie ??= string.Empty;
        Draft.TipologiaMGDCMatricula ??= string.Empty;
    }

    private static int? TryExtractVersionedSuffix(string baseCodigo, string currentCode)
    {
        if (!currentCode.StartsWith(baseCodigo + "-v", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw = currentCode[(baseCodigo.Length + 2)..];
        return int.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static string SuggestNextVersion(IEnumerable<string?> versions)
    {
        var parsed = versions
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(TryParseVersion)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();

        if (parsed.Length == 0)
        {
            return "1.0.0";
        }

        var max = parsed
            .OrderBy(v => v.Major)
            .ThenBy(v => v.Minor)
            .ThenBy(v => v.Patch)
            .Last();

        if (max.HasPatch)
        {
            return $"{max.Major}.{max.Minor}.{max.Patch + 1}";
        }

        return $"{max.Major}.{max.Minor + 1}";
    }

    private static VersionParts? TryParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var main = value.Split('-', 2)[0].Split('+', 2)[0];
        var parts = main.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3)
        {
            return null;
        }

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            return null;
        }

        var hasPatch = parts.Length == 3;
        var patch = 0;
        if (hasPatch && !int.TryParse(parts[2], out patch))
        {
            return null;
        }

        return new VersionParts(major, minor, patch, hasPatch);
    }

    private readonly record struct VersionParts(int Major, int Minor, int Patch, bool HasPatch);

    private sealed class WizardDraftEnvelope
    {
        public int SchemaVersion { get; set; }
        public int CurrentStep { get; set; }
        public TipologiaWizardDraft? Draft { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}

public sealed class TipologiaWizardDraft
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";

    public TipologiaWizardStartMode StartMode { get; set; } = TipologiaWizardStartMode.DesdeCero;
    public int? SourceTipologiaId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;

    public string ModeloClasificacionDI { get; set; } = string.Empty;
    public double UmbralClasificacion { get; set; } = 0.85;
    public bool EnableExtraction { get; set; } = true;
    public string ModeloExtraccionDI { get; set; } = string.Empty;
    public double UmbralExtraccion { get; set; } = 0.80;
    public bool EnablePrompting { get; set; }
    public string PromptModelKey { get; set; } = string.Empty;
    public string PromptGPT { get; set; } = string.Empty;
    public bool EnableAssetResolver { get; set; }
    public string AssetResolverCamposSolicitados { get; set; } = string.Empty;
    public List<TipologiaWizardFieldDraft> Fields { get; set; } = new();

    public string TipologiaMGDCMatricula { get; set; } = string.Empty;
    public string GdcTipoDocumento { get; set; } = string.Empty;
    public string GdcSubtipoDocumento { get; set; } = string.Empty;
    public string GdcSerie { get; set; } = string.Empty;
    public bool SkipGdcUpload { get; set; }

    public static TipologiaWizardDraft CreateDefault() => new();
}

public sealed class TipologiaWizardFieldDraft
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public List<TipologiaWizardRuleDraft> Rules { get; set; } = new();
}

public sealed class TipologiaWizardRuleDraft
{
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";

    // range
    public string RangeMin { get; set; } = string.Empty;
    public string RangeMax { get; set; } = string.Empty;

    // regex
    public string RegexPattern { get; set; } = string.Empty;

    // enum
    public string EnumValues { get; set; } = string.Empty; // comma-separated
    public bool EnumCaseSensitive { get; set; } = false;

    // date
    public string DateFormats { get; set; } = string.Empty; // semicolon-separated, empty = defaults
    public bool DateAllowFuture { get; set; } = true;
    public bool DateAllowPast { get; set; } = true;

    // minlength / maxlength
    public string LengthValue { get; set; } = string.Empty;
}

public sealed record TipologiaWizardVersionSuggestion(string Codigo, string Version, bool HasChanges);

public enum TipologiaWizardStartMode
{
    DesdeCero = 0,
    Clonar = 1,
    Plantilla = 2
}
