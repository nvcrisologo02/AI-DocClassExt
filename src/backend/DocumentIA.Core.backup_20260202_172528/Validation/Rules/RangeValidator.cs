// DocumentIA.Core/Validation/Rules/RangeValidator.cs
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de rangos numericos
    /// </summary>
    public class RangeValidator : ValidationRuleBase
    {
        private readonly decimal? _min;
        private readonly decimal? _max;

        public override string RuleName => "RangeValidator";

        public RangeValidator(decimal? min = null, decimal? max = null)
        {
            _min = min;
            _max = max;
        }

        public override ValidationResult Validate(string fieldName, object value, Dictionary<string, object> context = null)
        {
            if (value == null)
            {
                return CreateSuccessResult(fieldName);
            }

            if (!decimal.TryParse(value.ToString(), out decimal numericValue))
            {
                return CreateFailureResult(fieldName,
                    $"El valor '{value}' no es numerico",
                    "Proporcionar un valor numerico valido");
            }

            if (_min.HasValue && numericValue < _min.Value)
            {
                return CreateFailureResult(fieldName,
                    $"El valor {numericValue} es menor que el minimo permitido ({_min.Value})",
                    $"El valor debe ser mayor o igual a {_min.Value}");
            }

            if (_max.HasValue && numericValue > _max.Value)
            {
                return CreateFailureResult(fieldName,
                    $"El valor {numericValue} es mayor que el maximo permitido ({_max.Value})",
                    $"El valor debe ser menor o igual a {_max.Value}");
            }

            return CreateSuccessResult(fieldName);
        }
    }
}
