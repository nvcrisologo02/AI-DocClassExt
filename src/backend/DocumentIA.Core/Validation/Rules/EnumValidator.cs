// DocumentIA.Core/Validation/Rules/EnumValidator.cs
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de enumeraciones - verifica que el valor esté en una lista de valores permitidos
    /// </summary>
    public class EnumValidator : ValidationRuleBase
    {
        private readonly List<string> _allowedValues;
        private readonly bool _caseSensitive;

        public override string RuleName => "EnumValidator";

        public EnumValidator(List<string> values, bool caseSensitive = false)
        {
            _allowedValues = values ?? throw new ArgumentNullException(nameof(values));
            _caseSensitive = caseSensitive;
        }

        public override ValidationResult Validate(string fieldName, object? value, Dictionary<string, object?>? context = null)
        {
            if (value == null)
            {
                return CreateSuccessResult(fieldName);
            }

            var valueString = value.ToString();

            if (string.IsNullOrWhiteSpace(valueString))
            {
                return CreateSuccessResult(fieldName);
            }

            var isValid = _caseSensitive
                ? _allowedValues.Contains(valueString)
                : _allowedValues.Any(v => v.Equals(valueString, StringComparison.OrdinalIgnoreCase));

            if (!isValid)
            {
                var allowedValuesStr = string.Join(", ", _allowedValues.Select(v => $"'{v}'"));
                return CreateFailureResult(fieldName,
                    $"El valor '{valueString}' no es válido para este campo",
                    $"Use uno de los siguientes valores: {allowedValuesStr}");
            }

            return CreateSuccessResult(fieldName);
        }
    }
}
