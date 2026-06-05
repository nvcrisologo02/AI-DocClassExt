using DocumentIA.Core.Configuration;

namespace DocumentIA.Functions.Services.Abstractions;

public interface IGptJsonResponseParser
{
    /// <summary>
    /// Parse OpenAI JSON response into typed GptExtractionResponse.
    /// </summary>
    GptExtractionResponse Parse(string jsonText, TipologiaValidationConfig config);
}

public class GptExtractionResponse
{
    public Dictionary<string, object> CamposExtraidos { get; set; } = new();
    public Dictionary<string, double>? ConfianzaPorCampo { get; set; }
    public double? ConfianzaExtraccionGpt { get; set; }
    public string? Resumen { get; set; }
    public string? ResultadoPrompt { get; set; }
}
