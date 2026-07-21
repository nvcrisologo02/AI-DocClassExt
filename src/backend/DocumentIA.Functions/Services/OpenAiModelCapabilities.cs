using System.Text.RegularExpressions;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Capacidades por familia de modelo Azure OpenAI. Los modelos de razonamiento
/// (familia gpt-5 y o-series) rechazan el parámetro temperature con HTTP 400:
/// solo debe enviarse a los modelos clásicos (gpt-4x, gpt-35).
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
    /// Para modelos de razonamiento no se envía ninguno de los dos: rechazan
    /// temperature != default y el SDK estable serializa el límite como
    /// 'max_tokens', que estos modelos rechazan (exigen 'max_completion_tokens').
    /// </summary>
    public static void ConfigureChatOptions(ChatCompletionOptions options, string? deploymentName, double temperature, int? maxOutputTokens)
    {
        ApplyTemperature(options, deploymentName, temperature);

        if (SupportsTemperature(deploymentName) && maxOutputTokens is > 0)
        {
            options.MaxOutputTokenCount = maxOutputTokens;
        }
    }
}
