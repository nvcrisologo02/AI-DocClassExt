// DocumentIA.Core/Validation/Rules/EnumValidator.cs
using DocumentIA.Core.Validation.Models;
using System.Globalization;
using System.Text;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de enumeraciones - verifica que el valor esté en una lista de valores permitidos
    /// </summary>
    public class EnumValidator : ValidationRuleBase
    {
        private readonly List<string> _allowedValues;
        private readonly HashSet<string> _allowedValuesNormalized;
        private readonly bool _caseSensitive;

        public override string RuleName => "EnumValidator";

        public EnumValidator(List<string> values, bool caseSensitive = false)
        {
            _allowedValues = values ?? throw new ArgumentNullException(nameof(values));
            _caseSensitive = caseSensitive;
            _allowedValuesNormalized = _allowedValues
                .Select(NormalizeForComparison)
                .ToHashSet(StringComparer.Ordinal);
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
                : _allowedValuesNormalized.Contains(NormalizeForComparison(valueString));

            if (!isValid)
            {
                var allowedValuesStr = string.Join(", ", _allowedValues.Select(v => $"'{v}'"));
                return CreateFailureResult(fieldName,
                    $"El valor '{valueString}' no es válido para este campo",
                    $"Use uno de los siguientes valores: {allowedValuesStr}");
            }

            return CreateSuccessResult(fieldName);
        }

        private static string NormalizeForComparison(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var normalized = trimmed.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }
    }
}
