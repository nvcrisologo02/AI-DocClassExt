using DocumentIA.Admin.Models;
using System.Text.RegularExpressions;

namespace DocumentIA.Admin.Components.Prompts;

/// <summary>Validador de placeholders en contenido de prompts.</summary>
public class PromptPlaceholderValidator
{
    /// <summary>Valida que el contenido cumpla con reglas de longitud y placeholders.</summary>
    public static List<string> ValidateContent(string content, string promptKey)
    {
        var errors = new List<string>();

        // Validar longitud
        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add("El contenido del prompt no puede estar vacío.");
            return errors;
        }

        if (content.Length < 10)
            errors.Add("El contenido debe tener al menos 10 caracteres.");

        if (content.Length > 16000)
            errors.Add("El contenido no puede exceder 16000 caracteres.");

        // Validar que no hay placeholders inválidos
        var invalidPlaceholders = FindInvalidPlaceholders(content);
        foreach (var invalid in invalidPlaceholders)
        {
            errors.Add($"Placeholder inválido o desconocido: '{invalid}'");
        }

        return errors;
    }

    /// <summary>Encuentra todos los placeholders presentes en el contenido.</summary>
    public static List<string> FindPlaceholders(string content)
    {
        var found = new List<string>();
        foreach (var placeholder in PromptPlaceholders.AllPlaceholders)
        {
            if (content.Contains(placeholder, StringComparison.Ordinal))
                found.Add(placeholder);
        }
        return found;
    }

    /// <summary>Encuentra placeholders que no están en la lista de permitidos.</summary>
    private static List<string> FindInvalidPlaceholders(string content)
    {
        var invalidPlaceholders = new List<string>();
        var pattern = @"\{[A-Z_]+\}";

        var regex = new Regex(pattern);
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            var placeholder = match.Value;
            if (!PromptPlaceholders.AllPlaceholders.Contains(placeholder, StringComparer.Ordinal))
            {
                invalidPlaceholders.Add(placeholder);
            }
        }

        return invalidPlaceholders;
    }

    /// <summary>Calcula el estado de validación para mostrar feedback visual.</summary>
    public static ValidationState GetValidationState(string content, string promptKey)
    {
        var errors = ValidateContent(content, promptKey);
        var found = FindPlaceholders(content);
        var required = PromptKeyRules.GetRequiredPlaceholders(promptKey);
        var missingRequired = required.Where(r => !found.Contains(r)).ToList();

        return new ValidationState
        {
            IsValid = errors.Count == 0,
            HasErrors = errors.Count > 0,
            Errors = errors,
            FoundPlaceholders = found,
            RequiredPlaceholders = required,
            MissingRequired = missingRequired,
            CharacterCount = content?.Length ?? 0,
            IsLengthValid = content?.Length >= 10 && content?.Length <= 16000
        };
    }

    /// <summary>Estado de validación de un prompt.</summary>
    public class ValidationState
    {
        public bool IsValid { get; set; }
        public bool HasErrors { get; set; }
        public List<string> Errors { get; set; } = [];
        public List<string> FoundPlaceholders { get; set; } = [];
        public List<string> RequiredPlaceholders { get; set; } = [];
        public List<string> MissingRequired { get; set; } = [];
        public int CharacterCount { get; set; }
        public bool IsLengthValid { get; set; }
    }
}
