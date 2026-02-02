// DocumentIA.Core/Validation/Rules/NifValidator.cs
using System.Text.RegularExpressions;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de NIF/CIF/NIE espanoles
    /// </summary>
    public class NifValidator : ValidationRuleBase
    {
        private static readonly string NIF_PATTERN = @"^[0-9]{8}[A-Z]$";
        private static readonly string CIF_PATTERN = @"^[ABCDEFGHJNPQRSUVW][0-9]{7}[0-9A-J]$";
        private static readonly string NIE_PATTERN = @"^[XYZ][0-9]{7}[A-Z]$";
        private static readonly string NIF_LETTERS = "TRWAGMYFPDXBNJZSQVHLCKE";

        public override string RuleName => "NifCifNieValidator";

        public override ValidationResult Validate(string fieldName, object value, Dictionary<string, object> context = null)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return CreateSuccessResult(fieldName); // Campo opcional
            }

            string nifValue = value.ToString().ToUpper().Trim();

            // Validar NIF
            if (Regex.IsMatch(nifValue, NIF_PATTERN))
            {
                if (IsValidNif(nifValue))
                {
                    return CreateSuccessResult(fieldName);
                }
                return CreateFailureResult(fieldName, 
                    $"NIF '{nifValue}' tiene letra de control incorrecta",
                    "Verificar el numero y letra del NIF");
            }

            // Validar CIF
            if (Regex.IsMatch(nifValue, CIF_PATTERN))
            {
                if (IsValidCif(nifValue))
                {
                    return CreateSuccessResult(fieldName);
                }
                return CreateFailureResult(fieldName,
                    $"CIF '{nifValue}' tiene digito de control incorrecto",
                    "Verificar el CIF con la organizacion emisora");
            }

            // Validar NIE
            if (Regex.IsMatch(nifValue, NIE_PATTERN))
            {
                if (IsValidNie(nifValue))
                {
                    return CreateSuccessResult(fieldName);
                }
                return CreateFailureResult(fieldName,
                    $"NIE '{nifValue}' tiene letra de control incorrecta",
                    "Verificar el numero y letra del NIE");
            }

            return CreateFailureResult(fieldName,
                $"Formato de NIF/CIF/NIE '{nifValue}' no valido",
                "Debe ser NIF (12345678A), CIF (A12345678) o NIE (X1234567A)");
        }

        private bool IsValidNif(string nif)
        {
            int number = int.Parse(nif.Substring(0, 8));
            char letter = nif[8];
            return NIF_LETTERS[number % 23] == letter;
        }

        private bool IsValidNie(string nie)
        {
            // Reemplazar primera letra por numero
            char firstChar = nie[0];
            int replacement = firstChar switch
            {
                'X' => 0,
                'Y' => 1,
                'Z' => 2,
                _ => -1
            };

            if (replacement == -1) return false;

            string nifEquivalent = replacement + nie.Substring(1, 7) + nie[8];
            return IsValidNif(nifEquivalent);
        }

        private bool IsValidCif(string cif)
        {
            // Algoritmo de validacion de CIF
            char letter = cif[0];
            string digits = cif.Substring(1, 7);
            char control = cif[8];

            int sumA = 0;
            int sumB = 0;

            for (int i = 0; i < 7; i++)
            {
                int digit = int.Parse(digits[i].ToString());
                
                if (i % 2 == 0) // Posiciones impares (0-indexed)
                {
                    int doubled = digit * 2;
                    sumA += doubled / 10 + doubled % 10;
                }
                else // Posiciones pares
                {
                    sumB += digit;
                }
            }

            int totalSum = sumA + sumB;
            int unitDigit = totalSum % 10;
            int controlDigit = unitDigit == 0 ? 0 : 10 - unitDigit;

            // Dependiendo del tipo de organizacion, puede ser numero o letra
            if (char.IsDigit(control))
            {
                return int.Parse(control.ToString()) == controlDigit;
            }
            else
            {
                char controlLetter = (char)('A' + controlDigit);
                return control == controlLetter;
            }
        }
    }
}
