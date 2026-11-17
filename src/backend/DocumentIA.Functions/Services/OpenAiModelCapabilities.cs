using System.Text.RegularExpressions;
using Azure.AI.OpenAI.Chat;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Capacidades por familia de modelo Azure OpenAI. Los modelos de razonamiento
/// (familia gpt-5 y o-series) rechazan el parámetro temperature con HTTP 400:
/// solo debe enviarse a los modelos clásicos (gpt-4x, gpt-35). El SDK actual admite
/// 'reasoning_effort' y 'max_completion_tokens' (este último solo cuando el cliente de Azure
/// lo emite, lo que exige activar SetNewMaxCompletionTokensPropertyEnabled; si no, sale como
/// 'max_tokens'): la sobrecarga de cinco parámetros de
/// ConfigureChatOptions los envía a los modelos de razonamiento; la de cuatro se
/// conserva sin cambios para la extracción.
/// </summary>
public static partial class OpenAiModelCapabilities
{
    // Familias de razonamiento por prefijo del deployment: gpt-5*, o1/o3/o4...
    [GeneratedRegex(@"^(gpt-5|o\d+)([-._]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReasoningFamilyRegex();

    /// <summary>
    /// Indica si el deployment admite el parámetro temperature. Ante un nombre
    /// vacío o desconocido se asume que sí (comportamiento previo).
    /// </summary>
    public static bool SupportsTemperature(string? deploymentName)
    {
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return true;
        }

        return !ReasoningFamilyRegex().IsMatch(deploymentName.Trim());
    }

    /// <summary>
    /// Asigna temperature en las opciones solo si el modelo lo admite.
    /// </summary>
    public static void ApplyTemperature(ChatCompletionOptions options, string? deploymentName, double temperature)
    {
        if (SupportsTemperature(deploymentName))
        {
            options.Temperature = (float)temperature;
        }
    }

    /// <summary>
    /// Configura temperature y límite de salida según la familia del modelo.
    /// Sobrecarga conservada para la extracción: en modelos de razonamiento no se
    /// envía ni temperature ni límite de salida.
    /// </summary>
    public static void ConfigureChatOptions(ChatCompletionOptions options, string? deploymentName, double temperature, int? maxOutputTokens)
    {
        ApplyTemperature(options, deploymentName, temperature);

        if (SupportsTemperature(deploymentName) && maxOutputTokens is > 0)
        {
            options.MaxOutputTokenCount = maxOutputTokens;
        }
    }

    /// <summary>
    /// Interpreta el esfuerzo de razonamiento configurado (none, minimal, low, medium,
    /// high; sin distinguir mayúsculas ni espacios). Devuelve false para nulo, vacío
    /// o un valor desconocido.
    /// </summary>
    public static bool TryParseReasoningEffort(string? value, out ChatReasoningEffortLevel level)
    {
        var normalizado = value?.Trim().ToLowerInvariant();
        if (normalizado is "none" or "minimal" or "low" or "medium" or "high")
        {
            level = new ChatReasoningEffortLevel(normalizado);
            return true;
        }

        level = default;
        return false;
    }

    /// <summary>
    /// Avisa en el log cuando el esfuerzo de razonamiento está informado pero no es válido
    /// (en ese caso no se envía). Nulo o vacío no genera aviso.
    /// </summary>
    public static void WarnIfInvalidReasoningEffort(ILogger logger, string? reasoningEffort, string modelKey)
    {
        if (!string.IsNullOrWhiteSpace(reasoningEffort) && !TryParseReasoningEffort(reasoningEffort, out _))
        {
            logger.LogWarning("ReasoningEffort '{ReasoningEffort}' no válido en el modelo {ModelKey}; no se envía.", reasoningEffort, modelKey);
        }
    }

    /// <summary>
    /// Configura temperature, límite de salida y esfuerzo de razonamiento. Los modelos
    /// clásicos reciben temperature y límite; los de razonamiento reciben el límite
    /// (como 'max_completion_tokens', activando la propiedad nueva en el cliente de Azure
    /// con SetNewMaxCompletionTokensPropertyEnabled) y el esfuerzo, si es válido, y nunca temperature.
    /// </summary>
    public static void ConfigureChatOptions(ChatCompletionOptions options, string? deploymentName, double temperature, int? maxOutputTokens, string? reasoningEffort)
    {
        ApplyTemperature(options, deploymentName, temperature);

        if (maxOutputTokens is > 0)
        {
            options.MaxOutputTokenCount = maxOutputTokens;

            if (!SupportsTemperature(deploymentName))
            {
                // El cliente de Azure serializa el límite como 'max_tokens' salvo que se active la propiedad nueva.
                options.SetNewMaxCompletionTokensPropertyEnabled();
            }
        }

        if (!SupportsTemperature(deploymentName) && TryParseReasoningEffort(reasoningEffort, out var level))
        {
            options.ReasoningEffortLevel = level;
        }
    }
}
