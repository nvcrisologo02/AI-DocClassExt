// DocumentIA.Core/Validation/Rules/RegexValidator.cs
using System.Text.RegularExpressions;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador basado en expresiones regulares - verifica que el valor coincida con el patrón especificado
    /// </summary>
    public class RegexValidator : ValidationRuleBase
    {
        private readonly string _pattern;
        private readonly Regex _regex;

        public override string RuleName => "RegexValidator";

        public RegexValidator(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("El patrón de expresión regular no puede estar vacío", nameof(pattern));
            }

            _pattern = pattern;
            try
            {
                _regex = new Regex(pattern, RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"El patrón de expresión regular es inválido: {pattern}", nameof(pattern), ex);
            }
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

            if (!_regex.IsMatch(valueString))
            {
                return CreateFailureResult(fieldName,
                    $"El valor '{valueString}' no cumple con el formato requerido",
                    $"El valor debe cumplir con el patrón: {_pattern}");
            }

            return CreateSuccessResult(fieldName);
        }
    }
}
