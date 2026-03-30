// DocumentIA.Core/Validation/Rules/BooleanValidator.cs
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de booleanos - verifica que el valor sea un booleano válido
    /// </summary>
    public class BooleanValidator : ValidationRuleBase
    {
        public override string RuleName => "BooleanValidator";

        public override ValidationResult Validate(string fieldName, object? value, Dictionary<string, object?>? context = null)
        {
            if (value == null)
            {
                return CreateSuccessResult(fieldName);
            }

            // Si ya es bool, es válido
            if (value is bool)
            {
                return CreateSuccessResult(fieldName);
            }

            // Intentar convertir desde string
            var valueString = value.ToString();

            if (string.IsNullOrWhiteSpace(valueString))
            {
                return CreateSuccessResult(fieldName);
            }

            if (bool.TryParse(valueString, out var result))
            {
                return CreateSuccessResult(fieldName);
            }

            // Aceptar variantes comunes
            var lowerValue = valueString.ToLowerInvariant().Trim();
            if (lowerValue == "1" || lowerValue == "0" || 
                lowerValue == "si" || lowerValue == "sí" || lowerValue == "yes" ||
                lowerValue == "no" || lowerValue == "verdadero" || lowerValue == "falso")
            {
                return CreateSuccessResult(fieldName);
            }

            return CreateFailureResult(fieldName,
                $"El valor '{value}' no es un booleano válido",
                "Proporcionar true/false o un valor booleano válido (1/0, sí/no, true/false)");
        }
    }
}
