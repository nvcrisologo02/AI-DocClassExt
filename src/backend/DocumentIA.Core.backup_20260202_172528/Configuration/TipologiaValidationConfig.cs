// DocumentIA.Core/Configuration/TipologiaValidationConfig.cs
namespace DocumentIA.Core.Configuration
{
    public class TipologiaValidationConfig
    {
        public string TipologiaId { get; set; }
        public string TipologiaNombre { get; set; }
        public string Version { get; set; }
        public List<FieldValidationConfig> Fields { get; set; }

        public TipologiaValidationConfig()
        {
            Fields = new List<FieldValidationConfig>();
        }
    }

    public class FieldValidationConfig
    {
        public string Name { get; set; }
        public string Type { get; set; } // decimal, string, date, etc
        public bool Required { get; set; }
        public List<ValidationRuleConfig> Rules { get; set; }

        public FieldValidationConfig()
        {
            Rules = new List<ValidationRuleConfig>();
        }
    }

    public class ValidationRuleConfig
    {
        public string RuleType { get; set; } // range, pattern, date, nif, catastral
        public string Severity { get; set; } // Error, Warning, Info
        public Dictionary<string, object> Parameters { get; set; }

        public ValidationRuleConfig()
        {
            Parameters = new Dictionary<string, object>();
        }
    }
}
