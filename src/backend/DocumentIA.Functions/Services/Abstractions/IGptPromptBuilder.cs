using DocumentIA.Core.Configuration;

namespace DocumentIA.Functions.Services.Abstractions;

public interface IGptPromptBuilder
{
    /// <summary>
    /// Construir system prompt para extracción GPT.
    /// </summary>
    string BuildSystemPrompt(PromptMode mode, PromptConfig? resumeConfig, PromptConfig? customConfig);
    
    /// <summary>
    /// Construir user prompt con contexto de document.
    /// </summary>
    string BuildUserPrompt(TipologiaValidationConfig tipologia, string? contentMarker, string? markdownContent);
    
    /// <summary>
    /// Construir catálogo de campos esperados con hints de validación.
    /// </summary>
    string BuildFieldCatalog(TipologiaValidationConfig tipologia);
}

public enum PromptMode
{
    Extraction,
    ExtractionWithFallback,
    Classification
}
