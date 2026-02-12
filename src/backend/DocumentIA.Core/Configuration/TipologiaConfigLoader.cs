// DocumentIA.Core/Configuration/TipologiaConfigLoader.cs
using System.Text.Json;
using DocumentIA.Core.Validation;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Configuration
{
    /// <summary>
    /// Cargador de configuraciones de tipologias desde archivos JSON
    /// </summary>
    public class TipologiaConfigLoader
    {
        private readonly string _configBasePath;

        public TipologiaConfigLoader(string configBasePath = "config/tipologias")
        {
            _configBasePath = configBasePath;
        }

        /// <summary>
        /// Carga la configuracion de validacion para una tipologia especifica
        /// </summary>
        public TipologiaValidationConfig LoadConfig(string tipologiaId)
        {
            string configPath = Path.Combine(_configBasePath, $"{tipologiaId}.validation.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"No se encontro configuracion para tipologia '{tipologiaId}' en {configPath}");
            }

            string jsonContent = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException($"Configuracion invalida para tipologia '{tipologiaId}' en {configPath}");

            return config;
        }

        /// <summary>
        /// Construye el motor de validacion a partir de la configuracion JSON
        /// </summary>
        public ValidationEngine BuildValidationEngine(string tipologiaId)
        {
            var config = LoadConfig(tipologiaId);
            var engine = new ValidationEngine();

            var fieldConfigs = config.Fields ?? new List<FieldValidationConfig>();

            foreach (var fieldConfig in fieldConfigs)
            {
                // Agregar validador de campo requerido
                if (fieldConfig.Required)
                {
                    var requiredValidator = new Validation.Rules.RequiredFieldValidator
                    {
                        Severity = ValidationSeverity.Warning
                    };
                    engine.AddRule(fieldConfig.Name, requiredValidator);
                }

                // Validar según el tipo de campo
                var fieldType = fieldConfig.Type.ToLower();

                if (fieldType == "boolean")
                {
                    var booleanValidator = new Validation.Rules.BooleanValidator
                    {
                        Severity = ValidationSeverity.Warning
                    };
                    engine.AddRule(fieldConfig.Name, booleanValidator);
                }
                else if (fieldType == "array")
                {
                    if (fieldConfig.Items != null)
                    {
                        var arrayValidator = new Validation.Rules.ArrayValidator(fieldConfig.Items)
                        {
                            Severity = ValidationSeverity.Warning
                        };
                        engine.AddRule(fieldConfig.Name, arrayValidator);
                    }
                }
                else
                {
                    // Agregar reglas especificas para campos simples
                    var ruleConfigs = fieldConfig.Rules ?? new List<ValidationRuleConfig>();

                    foreach (var ruleConfig in ruleConfigs)
                    {
                        IValidationRule rule = CreateRuleFromConfig(ruleConfig);
                        engine.AddRule(fieldConfig.Name, rule);
                    }
                }
            }

            return engine;
        }

        private IValidationRule CreateRuleFromConfig(ValidationRuleConfig ruleConfig)
        {
            IValidationRule rule = ruleConfig.RuleType.ToLower() switch
            {
                "range" => new Validation.Rules.RangeValidator(
                    min: GetParameter<decimal?>(ruleConfig.Parameters, "min"),
                    max: GetParameter<decimal?>(ruleConfig.Parameters, "max")
                ),
                "nif" => new Validation.Rules.NifValidator(),
                "catastral" => new Validation.Rules.CatastralReferenceValidator(),
                "date" => new Validation.Rules.DateFormatValidator(
                    acceptedFormats: GetParameter<string[]>(ruleConfig.Parameters, "formats"),
                    allowFutureDates: GetParameter<bool>(ruleConfig.Parameters, "allowFuture", true),
                    allowPastDates: GetParameter<bool>(ruleConfig.Parameters, "allowPast", true)
                ),
                "address" => new Validation.Rules.AddressValidator(
                    minLength: GetParameter<int>(ruleConfig.Parameters, "minLength", 5),
                    maxLength: GetParameter<int>(ruleConfig.Parameters, "maxLength", 260),
                    requireStreetNumber: GetParameter<bool>(ruleConfig.Parameters, "requireStreetNumber", true),
                    requireMunicipality: GetParameter<bool>(ruleConfig.Parameters, "requireMunicipality", false),
                    requireProvince: GetParameter<bool>(ruleConfig.Parameters, "requireProvince", false)
                ),
                "enum" => new Validation.Rules.EnumValidator(
                    values: GetParameter<List<string>>(ruleConfig.Parameters, "values", new List<string>()),
                    caseSensitive: GetParameter<bool>(ruleConfig.Parameters, "caseSensitive", false)
                ),
                "regex" => new Validation.Rules.RegexValidator(
                    pattern: GetParameter<string>(ruleConfig.Parameters, "pattern", "")
                ),
                "minlength" => new Validation.Rules.LengthValidator(
                    minLength: GetParameter<int?>(ruleConfig.Parameters, "value")
                ),
                "maxlength" => new Validation.Rules.LengthValidator(
                    maxLength: GetParameter<int?>(ruleConfig.Parameters, "value")
                ),
                _ => throw new NotSupportedException($"Tipo de regla '{ruleConfig.RuleType}' no soportado")
            };

            // Por defecto degradamos las reglas a Warning para no generar errores bloqueantes.
            if (rule is ValidationRuleBase defaultRuleBase)
            {
                defaultRuleBase.Severity = ValidationSeverity.Warning;
            }

            // Configurar severidad explicita si existe en JSON.
            if (!string.IsNullOrEmpty(ruleConfig.Severity))
            {
                if (Enum.TryParse<ValidationSeverity>(ruleConfig.Severity, true, out var severity))
                {
                    if (rule is ValidationRuleBase ruleBase)
                    {
                        ruleBase.Severity = severity;
                    }
                }
            }

            return rule;
        }

        private T GetParameter<T>(Dictionary<string, object?>? parameters, string key, T defaultValue = default!)
        {
            if (parameters != null && parameters.TryGetValue(key, out var value))
            {
                if (value is null)
                {
                    return defaultValue;
                }

                if (value is JsonElement jsonElement)
                {
                    var parsed = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                    return parsed is null ? defaultValue : parsed;
                }
                
                return (T)Convert.ChangeType(value, typeof(T));
            }

            return defaultValue;
        }
    }
}
