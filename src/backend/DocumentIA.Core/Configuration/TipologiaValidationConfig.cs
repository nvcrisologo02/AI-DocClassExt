// DocumentIA.Core/Configuration/TipologiaValidationConfig.cs
namespace DocumentIA.Core.Configuration
{
    public class TipologiaValidationConfig
    {
        public string TipologiaId { get; set; } = string.Empty;
        public string TipologiaNombre { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<FieldValidationConfig> Fields { get; set; } = new List<FieldValidationConfig>();

        public TipologiaValidationConfig()
        {
            Fields = new List<FieldValidationConfig>();
        }
    }

    public class FieldValidationConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // decimal, string, date, etc
        public bool Required { get; set; }
        public List<ValidationRuleConfig> Rules { get; set; } = new List<ValidationRuleConfig>();

        public FieldValidationConfig()
        {
            Rules = new List<ValidationRuleConfig>();
        }
    }

    public class ValidationRuleConfig
    {
        public string RuleType { get; set; } = string.Empty; // range, pattern, date, nif, catastral
        public string Severity { get; set; } = string.Empty; // Error, Warning, Info
        public Dictionary<string, object?> Parameters { get; set; } = new Dictionary<string, object?>();

        public ValidationRuleConfig()
        {
            Parameters = new Dictionary<string, object?>();
        }
    }
}
