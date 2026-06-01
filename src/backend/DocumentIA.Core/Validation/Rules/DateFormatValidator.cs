// DocumentIA.Core/Validation/Rules/DateFormatValidator.cs
using System.Globalization;
using System.Text;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de formatos de fecha
    /// </summary>
    public class DateFormatValidator : ValidationRuleBase
    {
        private readonly string[] _acceptedFormats;
        private static readonly string[] SupplementalFormats =
        {
            "dd/MM/yy",
            "d/M/yy",
            "d/M/yyyy",
            "dd-MM-yy",
            "d-M-yy",
            "d-M-yyyy",
            "d 'de' MMMM 'de' yyyy",
            "dd 'de' MMMM 'de' yyyy"
        };

        private static readonly CultureInfo[] ParseCultures =
        {
            CultureInfo.InvariantCulture,
            new("es-ES")
        };
        private readonly bool _allowFutureDates;
        private readonly bool _allowPastDates;

        public override string RuleName => "DateFormatValidator";

        public DateFormatValidator(
            string[]? acceptedFormats = null, 
            bool allowFutureDates = true, 
            bool allowPastDates = true)
        {
            var baseFormats = acceptedFormats ?? new[] 
            { 
                "dd/MM/yyyy", 
                "yyyy-MM-dd", 
                "dd-MM-yyyy",
                "yyyy/MM/dd"
            };

            _acceptedFormats = (acceptedFormats is null
                    ? baseFormats.Concat(SupplementalFormats)
                    : baseFormats)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            _allowFutureDates = allowFutureDates;
            _allowPastDates = allowPastDates;
        }

        public override ValidationResult Validate(string fieldName, object? value, Dictionary<string, object?>? context = null)
        {
            if (value == null)
            {
                return CreateSuccessResult(fieldName);
            }

            var dateString = value.ToString();
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return CreateSuccessResult(fieldName);
            }

            dateString = dateString.Trim();

            if (!TryParseDate(dateString, out DateTime parsedDate))
            {
                return CreateFailureResult(fieldName,
                    $"Formato de fecha '{dateString}' no valido",
                    $"Formatos aceptados: {string.Join(", ", _acceptedFormats)}");
            }

            if (!_allowFutureDates && parsedDate > DateTime.Now)
            {
                return CreateFailureResult(fieldName,
                    $"La fecha {dateString} es futura y no esta permitida",
                    "Proporcionar una fecha actual o pasada");
            }

            if (!_allowPastDates && parsedDate < DateTime.Now.Date)
            {
                return CreateFailureResult(fieldName,
                    $"La fecha {dateString} es pasada y no esta permitida",
                    "Proporcionar una fecha actual o futura");
            }

            return CreateSuccessResult(fieldName);
        }

        private bool TryParseDate(string dateString, out DateTime parsedDate)
        {
            var candidates = new[]
            {
                dateString,
                dateString.ToLowerInvariant(),
                RemoveDiacritics(dateString),
                RemoveDiacritics(dateString).ToLowerInvariant()
            }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

            foreach (var candidate in candidates)
            {
                foreach (var culture in ParseCultures)
                {
                    if (DateTime.TryParseExact(
                        candidate,
                        _acceptedFormats,
                        culture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out parsedDate))
                    {
                        return true;
                    }
                }
            }

            parsedDate = default;
            return false;
        }

        private static string RemoveDiacritics(string input)
        {
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
