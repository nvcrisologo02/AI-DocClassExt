// DocumentIA.Core/Validation/Rules/AddressValidator.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de direcciones españolas.
    ///
    /// FUNCIONAMIENTO
    /// 1) Normaliza: trim, colapsa espacios, estandariza comas.
    /// 2) Valida caracteres permitidos.
    /// 3) Valida longitud (min/max).
    /// 4) Verifica número de portal (configurable).
    /// 5) Exige código postal español (5 dígitos).
    /// 6) Opcionalmente valida municipio y provincia contra el context.
    ///
    /// EJEMPLOS VÁLIDOS
    /// - "Calle Mayor 15, 28013 Madrid"
    /// - "Av. de Andalucía 203, 41007 Sevilla"
    /// - "Paseo del Prado 32-B, 28014 Madrid"
    ///
    /// EJEMPLOS NO VÁLIDOS
    /// - "Calle Mayor"                 // falta portal si se exige
    /// - "C@lle 12"                    // caracteres inválidos
    /// - "Calle Real 10, 999 Madrid"   // CP no válido
    /// </summary>
    public class AddressValidator : ValidationRuleBase
    {
        public override string RuleName => "AddressValidator";

        private readonly int _minLength;
        private readonly int _maxLength;
        private readonly bool _requireStreetNumber;
        private readonly bool _requireMunicipality;
        private readonly bool _requireProvince;

        private static readonly Regex StreetNumberPattern =
            new(@"\b\d{1,4}([A-Z]|-[A-Z])?\b", RegexOptions.Compiled);

        private static readonly Regex PostalCodePattern =
            new(@"\b\d{5}\b", RegexOptions.Compiled);

        // Letras con tildes/ñ, números y separadores comunes
        private static readonly string ADDRESS_CHAR_PATTERN =
            @"^[A-Z0-9ÁÉÍÓÚÜÑàáéíóúüñºª\-/.,() ]+$";

        public AddressValidator(
            int minLength = 5,
            int maxLength = 160,
            bool requireStreetNumber = true,
            bool requireMunicipality = false,
            bool requireProvince = false)
        {
            _minLength = minLength;
            _maxLength = maxLength;
            _requireStreetNumber = requireStreetNumber;
            _requireMunicipality = requireMunicipality;
            _requireProvince = requireProvince;
        }

        public override ValidationResult Validate(
            string fieldName,
            object? value,
            Dictionary<string, object?>? context = null)
        {
            // Delegar obligatoriedad en RequiredFieldValidator (consistente con tus reglas)
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return CreateSuccessResult(fieldName); // [1](https://srbo365-my.sharepoint.com/personal/ignacio_varas_sareb_es/Documents/Archivos%20de%20Microsoft%C2%A0Copilot%20Chat/RequiredFieldValidator.cs)

            var original = value.ToString()!;
            var address = NormalizeAddress(original);

            // Longitud
            if (address.Length < _minLength)
                return Fail(fieldName, address,
                    $"La dirección es demasiado corta",
                    $"Debe tener al menos {_minLength} caracteres.");

            if (address.Length > _maxLength)
                return Fail(fieldName, address,
                    $"La dirección supera la longitud permitida",
                    $"Debe tener como máximo {_maxLength} caracteres.");

            // Caracteres permitidos
            if (!Regex.IsMatch(address.ToUpperInvariant(), ADDRESS_CHAR_PATTERN))
                return Fail(fieldName, address,
                    $"La dirección contiene caracteres no permitidos.",
                    $"Usar solo letras, números, espacios, puntos, comas y guiones.");

            // Número de portal
            if (_requireStreetNumber && !StreetNumberPattern.IsMatch(address))
                return Fail(fieldName, address,
                    $"La dirección no contiene número de portal.",
                    $"Ejemplo: 'Calle Mayor 15' o 'Paseo del Prado 32-B'.");

            // Código postal (5 dígitos)
            if (!PostalCodePattern.IsMatch(address))
                return Fail(fieldName, address,
                    $"No se ha encontrado un código postal válido.",
                    $"El código postal debe tener 5 dígitos (ej.: 28013).");

            // Municipio (si se requiere y viene en context)
            if (_requireMunicipality)
            {
                var municipality = ReadContextString(context, "municipality");
                if (!string.IsNullOrWhiteSpace(municipality) &&
                    address.IndexOf(municipality, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return Fail(fieldName, address,
                        $"La dirección no contiene el municipio requerido.",
                        $"Añadir municipio: '{municipality}'.");
                }
            }

            // Provincia (si se requiere y viene en context)
            if (_requireProvince)
            {
                var province = ReadContextString(context, "province");
                if (!string.IsNullOrWhiteSpace(province) &&
                    address.IndexOf(province, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return Fail(fieldName, address,
                        $"La dirección no contiene la provincia requerida.",
                        $"Añadir provincia: '{province}'.");
                }
            }

            // Éxito
            return CreateSuccessResult(fieldName);
        }

        // ----------------- Helpers -----------------

        private static string NormalizeAddress(string input)
        {
            var a = input.Trim();
            a = Regex.Replace(a, @"\s+", " ");       // Colapsar espacios
            a = Regex.Replace(a, @"\s*,\s*", ", ");  // Estandarizar comas
            return a;
        }

        private static string? ReadContextString(Dictionary<string, object?>? context, string key)
        {
            if (context != null && context.TryGetValue(key, out var v) && v is string s)
                return s;
            return null;
        }

        // Uniformar creación de fallo con los campos de tu ValidationResult
        private ValidationResult Fail(string fieldName, string value, string message, string suggestion)
        {
            // Alineado con el estilo de tus reglas: Error por defecto; Warning/Info si lo decides
            var vr = CreateFailureResult(fieldName, message, suggestion);
            // Metadatos útiles para diagnóstico
            vr.Metadata["input"] = value;
            vr.Metadata["rule"] = RuleName;
            return vr;
        }
    }
}
