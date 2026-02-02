// DocumentIA.Core/Validation/Rules/DateFormatValidator.cs
using System.Globalization;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de formatos de fecha
    /// </summary>
    public class DateFormatValidator : ValidationRuleBase
    {
        private readonly string[] _acceptedFormats;
        private readonly bool _allowFutureDates;
        private readonly bool _allowPastDates;

        public override string RuleName => "DateFormatValidator";

        public DateFormatValidator(
            string[] acceptedFormats = null, 
            bool allowFutureDates = true, 
            bool allowPastDates = true)
        {
            _acceptedFormats = acceptedFormats ?? new[] 
            { 
                "dd/MM/yyyy", 
                "yyyy-MM-dd", 
                "dd-MM-yyyy",
                "yyyy/MM/dd"
            };
            _allowFutureDates = allowFutureDates;
            _allowPastDates = allowPastDates;
        }

        public override ValidationResult Validate(string fieldName, object value, Dictionary<string, object> context = null)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return CreateSuccessResult(fieldName);
            }

            string dateString = value.ToString().Trim();

            if (!DateTime.TryParseExact(dateString, _acceptedFormats, 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
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
    }
}
