using System.Collections.Generic;
using DocumentIA.Core.Validation.Models;

namespace DocumentIA.Core.Validation
{
    public class ValidationEngine
    {
        private readonly Dictionary<string, List<IValidationRule>> _fieldRules;

        public ValidationEngine()
        {
            _fieldRules = new Dictionary<string, List<IValidationRule>>();
        }

        public ValidationEngine AddRule(string fieldName, IValidationRule rule)
        {
            if (!_fieldRules.ContainsKey(fieldName))
            {
                _fieldRules[fieldName] = new List<IValidationRule>();
            }

            _fieldRules[fieldName].Add(rule);
            return this;
        }

        public ValidationReport ValidateDocument(Dictionary<string, object> extractedData, Dictionary<string, object> context = null)
        {
            var report = new ValidationReport();

            foreach (var fieldConfig in _fieldRules)
            {
                string fieldName = fieldConfig.Key;
                List<IValidationRule> rules = fieldConfig.Value;

                object fieldValue = extractedData.ContainsKey(fieldName) 
                    ? extractedData[fieldName] 
                    : null;

                foreach (var rule in rules)
                {
                    var result = rule.Validate(fieldName, fieldValue, context);
                    
                    // IMPORTANTE: Solo agregar resultados INVALIDOS al reporte
                    if (!result.IsValid)
                    {
                        report.AddResult(result);
                        
                        // Si es error critico, parar validacion de este campo
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
