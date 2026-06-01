// DocumentIA.Core/Configuration/TipologiaConfigLoader.cs
using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Core.Validation;
using DocumentIA.Core.Validation.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration
{
    /// <summary>
    /// Cargador de configuraciones de tipologias desde archivos JSON
    /// </summary>
    public class TipologiaConfigLoader
    {
        private readonly IMemoryCache? _cache;
        private readonly IServiceScopeFactory? _scopeFactory;

        public TipologiaConfigLoader(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Carga la configuracion de validacion para una tipologia especifica
        /// </summary>
        public TipologiaValidationConfig LoadConfig(string tipologiaId)
        {
            if (_cache is null || _scopeFactory is null)
            {
                throw new InvalidOperationException("TipologiaConfigLoader no esta correctamente configurado.");
            }

            return _cache.GetOrCreate($"tipologia:config:{tipologiaId}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return LoadConfigFromDatabase(tipologiaId);
            })!;
        }

        private TipologiaValidationConfig LoadConfigFromDatabase(string tipologiaId)
        {
            using var scope = _scopeFactory!.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();

            var tipologia = repository.GetByCodigoAsync(tipologiaId)
                .GetAwaiter()
                .GetResult();

            if (tipologia is null || !tipologia.Activa || tipologia.Estado != EstadoTipologia.Published)
            {
                throw new FileNotFoundException($"No se encontro configuracion publicada para tipologia '{tipologiaId}' en base de datos.");
            }

            if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
            {
                throw new InvalidDataException($"La tipologia '{tipologiaId}' no tiene ConfiguracionJson en base de datos.");
            }

            return JsonSerializer.Deserialize<TipologiaValidationConfig>(tipologia.ConfiguracionJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException($"Configuracion invalida para tipologia '{tipologiaId}' en base de datos.");
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
                    engine.AddRule(fieldConfig.Name, new Validation.Rules.RequiredFieldValidator());
                }

                // Validar según el tipo de campo
                var fieldType = fieldConfig.Type.ToLower();

                if (fieldType == "boolean")
                {
                    engine.AddRule(fieldConfig.Name, new Validation.Rules.BooleanValidator());
                }
                else if (fieldType == "array")
                {
                    if (fieldConfig.Items != null)
                    {
                        var arrayValidator = new Validation.Rules.ArrayValidator(fieldConfig.Items);
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
                    requirePostalCode: GetParameter<bool>(ruleConfig.Parameters, "requirePostalCode", true),
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
