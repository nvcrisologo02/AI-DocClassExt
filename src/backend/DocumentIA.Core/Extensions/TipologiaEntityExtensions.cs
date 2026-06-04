using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Core.Extensions;

/// <summary>
/// Extension methods para TipologiaEntity que aseguran acceso SOLO a ConfiguracionJson.
/// Propósito (AB#99738): Host NUNCA accede directamente a .PromptGPT de tabla.
/// 
/// Patrón de uso:
///   ✅ var config = tipologia.GetValidationConfig(logger);
///   ❌ var prompt = tipologia.PromptGPT;  (NUNCA USAR)
/// 
/// Status: Requerido en producción v1.4+
/// </summary>
public static class TipologiaEntityExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Obtiene TipologiaValidationConfig parseada desde ConfiguracionJson.
    /// Este es el ÚNICO método recomendado para acceder a configuración.
    /// 
    /// Retorna objeto vacío (no null) si JSON inválido.
    /// Loguea errores de parsing.
    /// </summary>
    public static TipologiaValidationConfig GetValidationConfig(
        this TipologiaEntity tipologia,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
        {
            return new TipologiaValidationConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<TipologiaValidationConfig>(
                tipologia.ConfiguracionJson,
                JsonOptions
            ) ?? new TipologiaValidationConfig();
        }
        catch (JsonException ex)
        {
            logger?.LogError(
                "Error parsing ConfiguracionJson para tipologia {Codigo}: {Error}",
                tipologia.Codigo, ex.Message
            );
            return new TipologiaValidationConfig();
        }
    }

    /// <summary>
    /// Extrae el SystemPrompt desde ConfiguracionJson.PromptConfig.
    /// NO accede a .PromptGPT de tabla.
    /// 
    /// Retorna string.Empty si no configurado.
    /// </summary>
    public static string GetSystemPrompt(this TipologiaEntity tipologia)
    {
        var config = tipologia.GetValidationConfig();
        return config?.PromptConfig?.SystemPrompt ?? string.Empty;
    }

    /// <summary>
    /// Extrae el UserPromptTemplate desde ConfiguracionJson.PromptConfig.
    /// NO accede a .PromptGPT de tabla.
    /// 
    /// Retorna string.Empty si no configurado.
    /// </summary>
    public static string GetUserPromptTemplate(this TipologiaEntity tipologia)
    {
        var config = tipologia.GetValidationConfig();
        return config?.PromptConfig?.UserPromptTemplate ?? string.Empty;
    }

    /// <summary>
    /// Obtiene la clasificación TDN1 desde ConfiguracionJson.Classification.
    /// NO accede a campo .Tdn1 deprecated de tabla.
    /// 
    /// Retorna string.Empty si no configurado.
    /// </summary>
    public static string GetTdn1(this TipologiaEntity tipologia)
    {
        var config = tipologia.GetValidationConfig();
        return config?.ResolvedTdn1 ?? string.Empty;
    }

    /// <summary>
    /// Obtiene la clasificación TDN2 desde ConfiguracionJson.Classification.
    /// 
    /// Retorna string.Empty si no configurado.
    /// </summary>
    public static string GetTdn2(this TipologiaEntity tipologia)
    {
        var config = tipologia.GetValidationConfig();
        return config?.ResolvedTdn2 ?? string.Empty;
    }

    /// <summary>
    /// Obtiene configuración GDC desde ConfiguracionJson.Gdc.
    /// 
    /// Retorna objeto vacío si no configurado.
    /// </summary>
    public static GdcConfig GetGdcConfig(this TipologiaEntity tipologia)
    {
        var config = tipologia.GetValidationConfig();
        return config?.Gdc ?? new GdcConfig();
    }

    /// <summary>
    /// Obtiene configuración de extracción desde ConfiguracionJson.Extraction.
    /// 
    /// Retorna objeto vacío si no configurado.
    /// </summary>
    public static TipologiaExtractionConfig GetExtractionConfig(this TipologiaEntity tipologia)
    {
        var config = tipologia.GetValidationConfig();
        return config?.Extraction ?? new TipologiaExtractionConfig();
    }

    /// <summary>
    /// Valida que ConfiguracionJson NO está vacío y es JSON válido.
    /// Útil para validaciones previas antes de procesamiento.
    /// </summary>
    public static bool HasValidConfiguration(this TipologiaEntity tipologia)
    {
        if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
        {
            return false;
        }

        try
        {
            JsonSerializer.Deserialize<TipologiaValidationConfig>(
                tipologia.ConfiguracionJson,
                JsonOptions
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ⚠️ DEPRECATED: Acceso a PromptGPT directamente.
    /// Este método SOLO para auditoría/debugging. NUNCA usar en lógica de negocio.
    /// 
    /// En v2.0 este método no existirá (columna eliminada).
    /// </summary>
    [Obsolete("DEPRECATED: Use GetSystemPrompt() o GetUserPromptTemplate() en su lugar. Lee desde ConfiguracionJson.", false)]
    public static string GetLegacyPromptGPT(this TipologiaEntity tipologia)
    {
        return tipologia.PromptGPT ?? string.Empty;
    }

    /// <summary>
    /// Retorna resumen de configuración para debugging.
    /// </summary>
    public static string GetConfigurationSummary(this TipologiaEntity tipologia)
    {
        var config = tipologia.GetValidationConfig();
        return $"Tipologia({tipologia.Codigo}): " +
               $"HasPrompt={!string.IsNullOrWhiteSpace(config?.PromptConfig?.SystemPrompt)}, " +
               $"Tdn1={config?.ResolvedTdn1 ?? "[none]"}, " +
               $"Tdn2={config?.ResolvedTdn2 ?? "[none]"}, " +
               $"Estado={tipologia.Estado}, " +
               $"Activa={tipologia.Activa}";
    }
}

/// <summary>
/// Extension methods para listas de TipologiaEntity.
/// Optimizaciones para operaciones en batch.
/// </summary>
public static class TipologiaEntityEnumerableExtensions
{
    /// <summary>
    /// Filtra tipologías que tienen configuración válida.
    /// Evita procesar tipologías con JSON corrupto.
    /// </summary>
    public static IEnumerable<TipologiaEntity> WithValidConfiguration(
        this IEnumerable<TipologiaEntity> tipologias)
    {
        return tipologias.Where(t => t.HasValidConfiguration());
    }

    /// <summary>
    /// Parsea configuraciones en batch de forma eficiente.
    /// Retorna tuplas (Entity, Config) para todas las tipologías.
    /// </summary>
    public static List<(TipologiaEntity Entity, TipologiaValidationConfig Config)> GetConfigurationsInBatch(
        this IEnumerable<TipologiaEntity> tipologias)
    {
        return tipologias
            .Select(t => (Entity: t, Config: t.GetValidationConfig()))
            .ToList();
    }
}
