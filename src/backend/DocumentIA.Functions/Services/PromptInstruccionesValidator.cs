using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Services;

public sealed class PromptInstruccionesValidator
{
    private const int MaxPromptLength = 50000;
    private const double MinTemperature = 0.0;
    private const double MaxTemperature = 2.0;
    private const int MinMaxTokens = 100;
    private const int MaxMaxTokens = 16384;

    private readonly PromptModelRegistryLoader _promptModelRegistryLoader;

    public PromptInstruccionesValidator(PromptModelRegistryLoader promptModelRegistryLoader)
    {
        _promptModelRegistryLoader = promptModelRegistryLoader;
    }

    public bool TryValidate(PromptInstrucciones? instrucciones, out string? error)
    {
        error = null;

        if (instrucciones is null)
        {
            return true;
        }

        if (instrucciones.SystemPrompt is not null && instrucciones.SystemPrompt.Length > MaxPromptLength)
        {
            error = $"instrucciones.prompt.systemPrompt excede el máximo de {MaxPromptLength} caracteres.";
            return false;
        }

        if (instrucciones.UserPromptTemplate is not null && instrucciones.UserPromptTemplate.Length > MaxPromptLength)
        {
            error = $"instrucciones.prompt.userPromptTemplate excede el máximo de {MaxPromptLength} caracteres.";
            return false;
        }

        if (instrucciones.Temperature.HasValue &&
            (instrucciones.Temperature.Value < MinTemperature || instrucciones.Temperature.Value > MaxTemperature))
        {
            error = $"instrucciones.prompt.temperature debe estar entre {MinTemperature:0.0} y {MaxTemperature:0.0}.";
            return false;
        }

        if (instrucciones.MaxTokens.HasValue &&
            (instrucciones.MaxTokens.Value < MinMaxTokens || instrucciones.MaxTokens.Value > MaxMaxTokens))
        {
            error = $"instrucciones.prompt.maxTokens debe estar entre {MinMaxTokens} y {MaxMaxTokens}.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(instrucciones.ContentMode) &&
            !string.Equals(instrucciones.ContentMode, "markdown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(instrucciones.ContentMode, "vision", StringComparison.OrdinalIgnoreCase))
        {
            error = "instrucciones.prompt.contentMode debe ser 'markdown' o 'vision'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(instrucciones.ModelKey))
        {
            try
            {
                _promptModelRegistryLoader.GetModel(instrucciones.ModelKey);
            }
            catch (Exception ex)
            {
                error = $"instrucciones.prompt.modelKey inválido: {ex.Message}";
                return false;
            }
        }

        return true;
    }
}
