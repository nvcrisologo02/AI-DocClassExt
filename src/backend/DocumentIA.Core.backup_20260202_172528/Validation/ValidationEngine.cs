// DocumentIA.Core/Validation/ValidationEngine.cs
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation
{
    /// <summary>
    /// Motor principal de validacion que ejecuta reglas sobre documentos extraidos
    /// </summary>
    public class ValidationEngine
    {
        private readonly Dictionary<string, List<IValidationRule>> _fieldRules;

        public ValidationEngine()
        {
            _fieldRules = new Dictionary<string, List<IValidationRule>>();
        }

        /// <summary>
        /// Agrega una regla de validacion para un campo especifico
        /// </summary>
        public ValidationEngine AddRule(string fieldName, IValidationRule rule)
        {
            if (!_fieldRules.ContainsKey(fieldName))
            {
                _fieldRules[fieldName] = new List<IValidationRule>();
            }

            _fieldRules[fieldName].Add(rule);
            return this; // Fluent interface
        }

        /// <summary>
        /// Valida todos los campos configurados contra el objeto de datos extraidos
        /// </summary>
        public ValidationReport ValidateDocument(Dictionary<string, object> extractedData, Dictionary<string, object> context = null)
        {
            var report = new ValidationReport();

            foreach (var fieldConfig in _fieldRules)
            {
                string fieldName = fieldConfig.Key;
                List<IValidationRule> rules = fieldConfig.Value;

                // Obtener valor del campo
                object fieldValue = extractedData.ContainsKey(fieldName) 
                    ? extractedData[fieldName] 
                    : null;

                // Ejecutar todas las reglas para este campo
                foreach (var rule in rules)
                {
                    var result = rule.Validate(fieldName, fieldValue, context);
                    
                    if (!result.IsValid)
                    {
                        report.AddResult(result);
                        
                        // Si es error critico, detener validacion de este campo
                        if (result.Severity == ValidationSeverity.Error)
                        {
                            break;
                        }
                    }
                }
            }

            return report;
        }
    }
}
