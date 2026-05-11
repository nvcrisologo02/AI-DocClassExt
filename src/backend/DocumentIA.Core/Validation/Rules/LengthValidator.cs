// DocumentIA.Core/Validation/Rules/LengthValidator.cs
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de longitud de strings - verifica minLength y maxLength
    /// </summary>
    public class LengthValidator : ValidationRuleBase
    {
        private readonly int? _minLength;
        private readonly int? _maxLength;

        public override string RuleName => "LengthValidator";

        public LengthValidator(int? minLength = null, int? maxLength = null)
        {
            _minLength = minLength;
            _maxLength = maxLength;
        }

        public override ValidationResult Validate(string fieldName, object? value, Dictionary<string, object?>? context = null)
        {
            if (value == null)
            {
                return CreateSuccessResult(fieldName);
            }

            var valueString = value.ToString();

            if (string.IsNullOrEmpty(valueString))
            {
                if (_minLength.HasValue && _minLength.Value > 0)
                {
                    return CreateFailureResult(fieldName,
                        $"El campo está vacío pero requiere mínimo {_minLength.Value} caracteres",
                        $"Proporcionar al menos {_minLength.Value} caracteres");
                }
                return CreateSuccessResult(fieldName);
            }

            int length = valueString.Length;

            if (_minLength.HasValue && length < _minLength.Value)
            {
                return CreateFailureResult(fieldName,
                    $"La longitud {length} es menor que el mínimo permitido ({_minLength.Value})",
                    $"Debe tener al menos {_minLength.Value} caracteres");
            }

            if (_maxLength.HasValue && length > _maxLength.Value)
            {
                return CreateFailureResult(fieldName,
                    $"La longitud {length} excede el máximo permitido ({_maxLength.Value})",
                    $"No debe exceder {_maxLength.Value} caracteres");
            }

            return CreateSuccessResult(fieldName);
        }
    }
}
