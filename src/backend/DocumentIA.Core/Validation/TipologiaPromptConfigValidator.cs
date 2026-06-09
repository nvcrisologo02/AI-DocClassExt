using System.Text.Json;
using DocumentIA.Core.Caching;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Core.Validation;

/// <summary>
/// Validator para detectar inconsistencias entre PromptGPT (campo tabla) y ConfiguracionJson.PromptConfig.
/// 
/// Propósito (AB#99734):
/// - Identificar tipologías con prompts desincronizados
/// - Prevenir corrupción de datos durante transiciones Fase 1 → Fase 2
/// - Facilitar migración automática en Fase 2
/// 
/// Status: Deprecated - Solo para validación/auditoría durante transición v1.4 → v2.0
/// </summary>
public class TipologiaPromptConfigValidator
{
    private readonly ILogger<TipologiaPromptConfigValidator> _logger;
    private readonly IConfigurationCache? _configCache;

    public TipologiaPromptConfigValidator(
        ILogger<TipologiaPromptConfigValidator> logger,
        IConfigurationCache? configCache = null)
    {
        _logger = logger;
        _configCache = configCache;
    }

    /// <summary>
    /// Valida una tipología individual para detectar inconsistencias de prompts.
    /// </summary>
    /// <returns>
    /// ValidationResult con:
    /// - IsConsistent: true si PromptGPT == ConfiguracionJson.PromptConfig
    /// - Warnings: lista de inconsistencias encontradas
    /// - SuggestedAction: próximo paso recomendado
    /// </returns>
    public TipologiaPromptValidationResult ValidateSingle(TipologiaEntity tipologia)
    {
        var result = new TipologiaPromptValidationResult
        {
            TipologiaId = tipologia.Id,
            Codigo = tipologia.Codigo,
            IsConsistent = true,
            Warnings = new List<string>(),
            #pragma warning disable CS0618 // Type or member is obsolete
            HasPromptGPT = !string.IsNullOrWhiteSpace(tipologia.PromptGPT),
            #pragma warning restore CS0618
            HasConfigPrompt = false
        };

        // Parse ConfiguracionJson
        TipologiaValidationConfig? config = null;
        if (!string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
        {
            var cacheKey = $"tipologia-validation-config:{tipologia.Id}:{tipologia.Version}:{tipologia.FechaActualizacion?.Ticks ?? 0}";
            try
            {
                config = _configCache is not null
                    ? _configCache.GetAsync<TipologiaValidationConfig>(cacheKey).GetAwaiter().GetResult()
                    : null;

                if (config is null)
                {
                    config = JsonSerializer.Deserialize<TipologiaValidationConfig>(
                        tipologia.ConfiguracionJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (config is not null && _configCache is not null)
                    {
                        _ = _configCache.SetAsync(cacheKey, config);
                    }
                }

                result.HasConfigPrompt = config?.PromptConfig != null &&
                    (!string.IsNullOrWhiteSpace(config.PromptConfig.SystemPrompt) ||
                     !string.IsNullOrWhiteSpace(config.PromptConfig.UserPromptTemplate));
            }
            catch (JsonException ex)
            {
                result.IsConsistent = false;
                result.Warnings.Add($"ERROR: ConfiguracionJson JSON inválido: {ex.Message}");
                result.SuggestedAction = "REPAIR: Revisar y validar ConfiguracionJson manualmente";
                _logger.LogError("TipologiaPromptValidator: JSON inválido en tipologia {Codigo}: {Error}", tipologia.Codigo, ex.Message);
                return result;
            }
        }

        // Verificar inconsistencias
        #pragma warning disable CS0618 // Type or member is obsolete
        var promptGPTNorm = NormalizePrompt(tipologia.PromptGPT ?? string.Empty);
        #pragma warning restore CS0618
        var configPromptNorm = NormalizePrompt(config?.PromptConfig?.SystemPrompt ?? string.Empty);

        if (result.HasPromptGPT && result.HasConfigPrompt && promptGPTNorm != configPromptNorm)
        {
            result.IsConsistent = false;
            result.Warnings.Add(
                $"MISMATCH: PromptGPT (tabla) difiere de ConfiguracionJson.PromptConfig\n" +
                $"  Table PromptGPT: {TruncateForDisplay(promptGPTNorm)}\n" +
                $"  Config Prompt: {TruncateForDisplay(configPromptNorm)}"
            );
            result.SuggestedAction = "MIGRATE: Usar ConfiguracionJson como fuente (ejecutar TipologiaPromptMigration)";
        }
        else if (result.HasPromptGPT && !result.HasConfigPrompt)
        {
            result.IsConsistent = false;
            result.Warnings.Add(
                "WARNING: PromptGPT existe en tabla pero NO en ConfiguracionJson.\n" +
                "  → Prompt en tabla será perdido en v2.0 si no se migra"
            );
            result.SuggestedAction = "ACTION: Copiar PromptGPT → ConfiguracionJson.PromptConfig";
        }
        else if (!result.HasPromptGPT && result.HasConfigPrompt)
        {
            result.IsConsistent = true; // OK, ya usa ConfiguracionJson
            result.Warnings.Add("OK: Ya usa ConfiguracionJson.PromptConfig (sin PromptGPT)");
            result.SuggestedAction = "READY: Tipología lista para v2.0";
        }
        else if (!result.HasPromptGPT && !result.HasConfigPrompt)
        {
            result.IsConsistent = true;
            result.Warnings.Add("OK: Sin prompt configurado (sin riesgo de pérdida)");
            result.SuggestedAction = "READY: Tipología lista para v2.0";
        }

        return result;
    }

    /// <summary>
    /// Valida todas las tipologías y retorna un resumen de estado.
    /// </summary>
    public TipologiaPromptValidationSummary ValidateAll(IEnumerable<TipologiaEntity> tipologias)
    {
        var results = tipologias.Select(t => ValidateSingle(t)).ToList();

        var summary = new TipologiaPromptValidationSummary
        {
            TotalTipologias = results.Count,
            Consistent = results.Count(r => r.IsConsistent),
            Inconsistent = results.Count(r => !r.IsConsistent),
            RequiresAction = results.Where(r => !r.IsConsistent).ToList(),
            AllReady = results.All(r => r.IsConsistent),
            ValidationTimestamp = DateTime.UtcNow
        };

        // Log resumen
        _logger.LogInformation(
            "TipologiaPromptValidator Summary: Total={Total}, Consistent={Consistent}, Inconsistent={Inconsistent}",
            summary.TotalTipologias, summary.Consistent, summary.Inconsistent
        );

        foreach (var issue in summary.RequiresAction)
        {
            _logger.LogWarning(
                "TipologiaPromptValidator Issue: {Codigo} - {Action}",
                issue.Codigo, issue.SuggestedAction
            );
        }

        return summary;
    }

    /// <summary>
    /// Normaliza prompts para comparación (trim, lowercase parcial).
    /// </summary>
    private string NormalizePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return string.Empty;

        return prompt.Trim();
    }

    /// <summary>
    /// Trunca prompts para display en logs (primeros 100 chars).
    /// </summary>
    private string TruncateForDisplay(string text, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "[EMPTY]";

        return text.Length > maxLength
            ? text[..maxLength] + "..."
            : text;
    }
}

/// <summary>
/// Resultado de validación de una tipología individual.
/// </summary>
public class TipologiaPromptValidationResult
{
    public int TipologiaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public bool IsConsistent { get; set; }
    public List<string> Warnings { get; set; } = new();
    public bool HasPromptGPT { get; set; }
    public bool HasConfigPrompt { get; set; }
    public string SuggestedAction { get; set; } = "NO ACTION";
}

/// <summary>
/// Resumen de validación para todas las tipologías.
/// </summary>
public class TipologiaPromptValidationSummary
{
    public int TotalTipologias { get; set; }
    public int Consistent { get; set; }
    public int Inconsistent { get; set; }
    public List<TipologiaPromptValidationResult> RequiresAction { get; set; } = new();
    public bool AllReady { get; set; }
    public DateTime ValidationTimestamp { get; set; }

    /// <summary>
    /// Retorna porcentaje de tipologías listas para v2.0.
    /// </summary>
    public double ReadinessPercentage =>
        TotalTipologias == 0 ? 100.0 : (Consistent * 100.0) / TotalTipologias;
}
