using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
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
                return CreateSuccessResult(fieldName);
            }

            string nifValue = value.ToString().ToUpper().Trim();

            if (Regex.IsMatch(nifValue, NIF_PATTERN))
            {
                if (IsValidNif(nifValue))
                {
                    return CreateSuccessResult(fieldName);
                }
                return CreateFailureResult(fieldName, 
                    string.Format("NIF '{0}' tiene letra de control incorrecta", nifValue),
                    "Verificar el numero y letra del NIF");
            }

            if (Regex.IsMatch(nifValue, CIF_PATTERN))
            {
                if (IsValidCif(nifValue))
                {
                    return CreateSuccessResult(fieldName);
                }
                return CreateFailureResult(fieldName,
                    string.Format("CIF '{0}' tiene digito de control incorrecto", nifValue),
                    "Verificar el CIF con la organizacion emisora");
            }

            if (Regex.IsMatch(nifValue, NIE_PATTERN))
            {
                if (IsValidNie(nifValue))
                {
                    return CreateSuccessResult(fieldName);
                }
                return CreateFailureResult(fieldName,
                    string.Format("NIE '{0}' tiene letra de control incorrecta", nifValue),
                    "Verificar el numero y letra del NIE");
            }

            return CreateFailureResult(fieldName,
                string.Format("Formato de NIF/CIF/NIE '{0}' no valido", nifValue),
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
            // Convertir primera letra a numero
            char firstChar = nie[0];
            int replacementDigit = firstChar switch
            {
                'X' => 0,
                'Y' => 1,
                'Z' => 2,
                _ => -1
            };

            if (replacementDigit == -1) return false;

            // Construir el numero completo: digito de reemplazo + 7 digitos del NIE
            string fullNumber = replacementDigit.ToString() + nie.Substring(1, 7);
            int number = int.Parse(fullNumber);
            char expectedLetter = NIF_LETTERS[number % 23];
            char actualLetter = nie[8];

            return expectedLetter == actualLetter;
        }

        private bool IsValidCif(string cif)
        {
            string digits = cif.Substring(1, 7);
            char control = cif[8];

            int sumA = 0;
            int sumB = 0;

            for (int i = 0; i < 7; i++)
            {
                int digit = int.Parse(digits[i].ToString());
                
                if (i % 2 == 0)
                {
                    int doubled = digit * 2;
                    sumA += doubled / 10 + doubled % 10;
                }
                else
                {
                    sumB += digit;
                }
            }

            int totalSum = sumA + sumB;
            int unitDigit = totalSum % 10;
            int controlDigit = unitDigit == 0 ? 0 : 10 - unitDigit;

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
