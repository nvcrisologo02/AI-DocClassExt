// DocumentIA.Core/Validation/Rules/CatastralReferenceValidator.cs
using System.Text.RegularExpressions;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de referencias catastrales espanolas
    /// Formato: 20 caracteres (14 numericos/alfanumericos + 4 letras + 2 caracteres)
    /// Ejemplo: 1234567AB1234S0001ZX
    /// </summary>
    public class CatastralReferenceValidator : ValidationRuleBase
    {
        private static readonly string CATASTRAL_PATTERN = @"^[0-9]{7}[A-Z]{2}[0-9]{4}[A-Z]{1}[0-9]{4}[A-Z]{2}$";

        public override string RuleName => "CatastralReferenceValidator";

        public override ValidationResult Validate(string fieldName, object value, Dictionary<string, object> context = null)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return CreateSuccessResult(fieldName);
            }

            string catastralRef = value.ToString().ToUpper().Trim().Replace(" ", "");

            if (catastralRef.Length != 20)
            {
                return CreateFailureResult(fieldName,
                    $"Referencia catastral '{catastralRef}' debe tener 20 caracteres (tiene {catastralRef.Length})",
                    "Verificar la referencia catastral completa");
            }

            if (!Regex.IsMatch(catastralRef, CATASTRAL_PATTERN))
            {
                return CreateFailureResult(fieldName,
                    $"Referencia catastral '{catastralRef}' no cumple el formato requerido",
                    "Formato: 7 digitos + 2 letras + 4 digitos + 1 letra + 4 digitos + 2 letras");
            }

            // Validacion de digitos de control (opcional, simplificado)
            if (!ValidateControlDigits(catastralRef))
            {
                Severity = ValidationSeverity.Warning; // Bajamos a warning
                return CreateFailureResult(fieldName,
                    $"Referencia catastral '{catastralRef}' puede tener digitos de control incorrectos",
                    "Verificar con el Catastro si la referencia es correcta");
            }

            return CreateSuccessResult(fieldName);
        }

        private bool ValidateControlDigits(string catastralRef)
        {
            // Implementacion simplificada
            // En produccion, usar algoritmo oficial del Catastro
            // Por ahora retornamos true para no bloquear
            return true;
        }
    }
}
