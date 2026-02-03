// DocumentIA.Core/Validation/IValidationRule.cs
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation
{
    /// <summary>
    /// Interfaz base para todas las reglas de validacion
    /// </summary>
    public interface IValidationRule
    {
        string RuleName { get; }
        ValidationSeverity Severity { get; }
        
        /// <summary>
        /// Ejecuta la validacion sobre el valor proporcionado
        /// </summary>
        /// <param name="fieldName">Nombre del campo a validar</param>
        /// <param name="value">Valor del campo</param>
        /// <param name="context">Contexto adicional (documento completo, metadata, etc)</param>
        /// <returns>Resultado de la validacion</returns>
        ValidationResult Validate(string fieldName, object? value, Dictionary<string, object?>? context = null);
    }

    /// <summary>
    /// Clase base abstracta para facilitar implementacion de reglas
    /// </summary>
    public abstract class ValidationRuleBase : IValidationRule
    {
        public abstract string RuleName { get; }
        public virtual ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

        public abstract ValidationResult Validate(string fieldName, object? value, Dictionary<string, object?>? context = null);

        protected ValidationResult CreateSuccessResult(string fieldName)
        {
            return new ValidationResult
            {
                IsValid = true,
                FieldName = fieldName,
                Severity = Severity
            };
        }

        protected ValidationResult CreateFailureResult(string fieldName, string message, string? suggestion = null)
        {
            return new ValidationResult
            {
                IsValid = false,
                FieldName = fieldName,
                Message = message,
                SuggestionString = suggestion,
                Severity = Severity
            };
        }
    }
}
