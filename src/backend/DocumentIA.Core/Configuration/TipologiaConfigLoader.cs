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
            });

            return config;
        }

        /// <summary>
        /// Construye el motor de validacion a partir de la configuracion JSON
        /// </summary>
        public ValidationEngine BuildValidationEngine(string tipologiaId)
        {
            var config = LoadConfig(tipologiaId);
            var engine = new ValidationEngine();

            foreach (var fieldConfig in config.Fields)
            {
                // Agregar validador de campo requerido
                if (fieldConfig.Required)
                {
                    engine.AddRule(fieldConfig.Name, new Validation.Rules.RequiredFieldValidator());
                }

                // Agregar reglas especificas
                foreach (var ruleConfig in fieldConfig.Rules)
                {
                    IValidationRule rule = CreateRuleFromConfig(ruleConfig);
                    engine.AddRule(fieldConfig.Name, rule);
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
                _ => throw new NotSupportedException($"Tipo de regla '{ruleConfig.RuleType}' no soportado")
            };

            // Configurar severidad
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

        private T GetParameter<T>(Dictionary<string, object> parameters, string key, T defaultValue = default)
        {
            if (parameters.ContainsKey(key))
            {
                var value = parameters[key];
                
                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                
                return (T)Convert.ChangeType(value, typeof(T));
            }

            return defaultValue;
        }
    }
}
