// DocumentIA.Core/Validation/Rules/ArrayValidator.cs
using DocumentIA.Core.Validation.Models;
using DocumentIA.Core.Configuration;
using System.Text.Json;

namespace DocumentIA.Core.Validation.Rules
{
    /// <summary>
    /// Validador de arrays - valida los items dentro de un array según la configuración especificada
    /// </summary>
    public class ArrayValidator : ValidationRuleBase
    {
        private readonly ItemsConfig _itemsConfig;
        private readonly Func<ItemsConfig, ValidationEngine>? _engineBuilder;

        public override string RuleName => "ArrayValidator";

        /// <summary>
        /// Constructor para ArrayValidator
        /// </summary>
        /// <param name="itemsConfig">Configuración de los items del array</param>
        /// <param name="engineBuilder">Función para construir el ValidationEngine para validar items anidados</param>
        public ArrayValidator(ItemsConfig itemsConfig, Func<ItemsConfig, ValidationEngine>? engineBuilder = null)
        {
            _itemsConfig = itemsConfig ?? throw new ArgumentNullException(nameof(itemsConfig));
            _engineBuilder = engineBuilder;
        }

        public override ValidationResult Validate(string fieldName, object? value, Dictionary<string, object?>? context = null)
        {
            if (value == null)
            {
                return CreateSuccessResult(fieldName);
            }

            List<object> items = new();

            // Manejo de diferentes tipos de entrada
            if (value is JsonElement jsonArray)
            {
                // Si es un JsonElement array
                if (jsonArray.ValueKind == JsonValueKind.Array)
                {
                    items = jsonArray.EnumerateArray().Cast<object>().ToList();
                }
                else if (jsonArray.ValueKind == JsonValueKind.String)
                {
                    // Si es un string, intentar parsear como JSON
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<object>>(jsonArray.GetString() ?? "[]");
                        items = parsed ?? new List<object>();
                    }
                    catch
                    {
                        // Si no se puede parsear, no es array válido
                        return CreateFailureResult(fieldName,
                            $"El valor no es una colección JSON válida",
                            "Proporcionar un array JSON válido");
                    }
                }
                else
                {
                    return CreateFailureResult(fieldName,
                        $"El valor no es una colección válida (JsonElement tipo: {jsonArray.ValueKind})",
                        "Proporcionar un array válido");
                }
            }
            else if (value is string strValue)
            {
                // Si es un string, intentar parsear como JSON
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<object>>(strValue);
                    items = parsed ?? new List<object>();
                }
                catch
                {
                    return CreateFailureResult(fieldName,
                        $"El valor no es una colección JSON válida",
                        "Proporcionar un array JSON válido");
                }
            }
            else if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                // Si es IEnumerable válido
                items = enumerable.Cast<object>().ToList();
            }
            else
            {
                // Si es un objeto individual, envolverlo en array para validacion
                items = new List<object> { value };
            }

            if (!items.Any())
            {
                return CreateSuccessResult(fieldName);
            }

            // Si tenemos información de propiedades anidadas, validarlas
            if (_itemsConfig.Type == "object" && _itemsConfig.Properties?.Any() == true)
            {
                return ValidateObjectArray(fieldName, items);
            }

            return CreateSuccessResult(fieldName);
        }

        private ValidationResult ValidateObjectArray(string fieldName, List<object> items)
        {
            var failedIndices = new List<(int index, List<string> errors)>();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemErrors = new List<string>();

                // Convertir item a diccionario si es posible
                if (item is System.Text.Json.JsonElement jsonElement)
                {
                    itemErrors.AddRange(ValidateJsonElementItem(jsonElement, i));
                }
                else if (item is Dictionary<string, object?> dict)
                {
                    itemErrors.AddRange(ValidateDictionaryItem(dict, i));
                }
                else
                {
                    itemErrors.Add($"Item en índice {i} no es un formato válido");
                }

                if (itemErrors.Any())
                {
                    failedIndices.Add((i, itemErrors));
                }
            }

            if (!failedIndices.Any())
            {
                return CreateSuccessResult(fieldName);
            }

            var failureMessage = string.Join("; ", failedIndices
                .Select(f => $"Índice {f.index}: {string.Join(", ", f.errors)}"));

            var result = CreateFailureResult(fieldName,
                $"El array '{fieldName}' contiene {failedIndices.Count} item(s) inválido(s)",
                $"Revisar los siguientes items: {failureMessage}");

            result.Metadata["ItemIndicesWithErrors"] = failedIndices.Select(f => f.index).ToList();
            result.Metadata["ItemErrors"] = failedIndices.ToDictionary(f => f.index, f => f.errors);

            return result;
        }

        private List<string> ValidateJsonElementItem(System.Text.Json.JsonElement jsonElement, int itemIndex)
        {
            var errors = new List<string>();

            if (_itemsConfig.Properties == null)
                return errors;

            foreach (var prop in _itemsConfig.Properties)
            {
                if (jsonElement.TryGetProperty(prop.Name, out var propValue))
                {
                    errors.AddRange(ValidateProperty(prop, propValue, itemIndex));
                }
                else if (prop.Required)
                {
                    errors.Add($"Propiedad requerida '{prop.Name}' no encontrada");
                }
            }

            return errors;
        }

        private List<string> ValidateDictionaryItem(Dictionary<string, object?> dict, int itemIndex)
        {
            var errors = new List<string>();

            if (_itemsConfig.Properties == null)
                return errors;

            foreach (var prop in _itemsConfig.Properties)
            {
                if (dict.TryGetValue(prop.Name, out var propValue))
                {
                    errors.AddRange(ValidatePropertyValue(prop, propValue, itemIndex));
                }
                else if (prop.Required)
                {
                    errors.Add($"Propiedad requerida '{prop.Name}' no encontrada");
                }
            }

            return errors;
        }

        private List<string> ValidateProperty(FieldValidationConfig prop, System.Text.Json.JsonElement propValue, int itemIndex)
        {
            var errors = new List<string>();

            if (propValue.ValueKind == System.Text.Json.JsonValueKind.Null)
            {
                if (prop.Required)
                {
                    errors.Add($"Propiedad '{prop.Name}' es nula");
                }
                return errors;
            }

            // Validar según tipo
            object? value = propValue.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => propValue.GetString(),
                System.Text.Json.JsonValueKind.Number => propValue.TryGetDecimal(out var dec) ? dec : propValue.GetDouble(),
                System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => propValue.GetBoolean(),
                _ => propValue.GetRawText()
            };

            errors.AddRange(ValidatePropertyValue(prop, value, itemIndex));
            return errors;
        }

        private List<string> ValidatePropertyValue(FieldValidationConfig prop, object? propValue, int itemIndex)
        {
            var errors = new List<string>();

            // Validar reglas de la propiedad
            foreach (var rule in prop.Rules)
            {
                var validator = CreateValidatorForRule(rule);
                if (validator != null)
                {
                    var result = validator.Validate(prop.Name, propValue);
                    if (!result.IsValid)
                    {
                        errors.Add($"{prop.Name}: {result.Message}");
                    }
                }
            }

            return errors;
        }

        private IValidationRule? CreateValidatorForRule(ValidationRuleConfig ruleConfig)
        {
            try
            {
                return ruleConfig.RuleType.ToLower() switch
                {
                    "range" => new RangeValidator(
                        min: GetParameter<decimal?>(ruleConfig.Parameters, "min"),
                        max: GetParameter<decimal?>(ruleConfig.Parameters, "max")
                    ),
                    "minlength" => new LengthValidator(
                        minLength: GetParameter<int?>(ruleConfig.Parameters, "value")
                    ),
                    "maxlength" => new LengthValidator(
                        maxLength: GetParameter<int?>(ruleConfig.Parameters, "value")
                    ),
                    "date" => new DateFormatValidator(
                        acceptedFormats: GetParameter<string[]>(ruleConfig.Parameters, "formats"),
                        allowFutureDates: GetParameter<bool>(ruleConfig.Parameters, "allowFuture", true),
                        allowPastDates: GetParameter<bool>(ruleConfig.Parameters, "allowPast", true)
                    ),
                    "enum" => new EnumValidator(
                        values: GetParameter<List<string>>(ruleConfig.Parameters, "values", new List<string>()),
                        caseSensitive: GetParameter<bool>(ruleConfig.Parameters, "caseSensitive", false)
                    ),
                    "regex" => new RegexValidator(
                        pattern: GetParameter<string>(ruleConfig.Parameters, "pattern", "")
                    ),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private T GetParameter<T>(Dictionary<string, object?>? parameters, string key, T defaultValue = default!)
        {
            if (parameters != null && parameters.TryGetValue(key, out var value))
            {
                if (value is null)
                    return defaultValue;

                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                    return parsed is null ? defaultValue : parsed;
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }

            return defaultValue;
        }
    }
}
