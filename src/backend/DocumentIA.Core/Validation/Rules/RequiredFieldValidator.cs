// DocumentIA.Core/Validation/Rules/RequiredFieldValidator.cs
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de campos obligatorios
    /// </summary>
    public class RequiredFieldValidator : ValidationRuleBase
    {
        public override string RuleName => "RequiredFieldValidator";

        public override ValidationResult Validate(string fieldName, object? value, Dictionary<string, object?>? context = null)
        {
            if (value == null || 
                (value is string str && string.IsNullOrWhiteSpace(str)))
            {
                return CreateFailureResult(fieldName,
                    $"El campo '{fieldName}' es obligatorio y no tiene valor",
                    "Proporcionar un valor para este campo");
            }

            return CreateSuccessResult(fieldName);
        }
    }
}
