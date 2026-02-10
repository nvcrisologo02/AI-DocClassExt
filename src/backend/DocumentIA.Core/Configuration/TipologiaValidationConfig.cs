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
        public string Type { get; set; } = string.Empty; // decimal, string, date, array, object, etc
        public bool Required { get; set; }
        public List<ValidationRuleConfig> Rules { get; set; } = new List<ValidationRuleConfig>();
        
        // Para soportar arrays y objetos anidados
        public ItemsConfig? Items { get; set; }

        public FieldValidationConfig()
        {
            Rules = new List<ValidationRuleConfig>();
        }
    }

    /// <summary>
    /// Configuración para items dentro de arrays
    /// </summary>
    public class ItemsConfig
    {
        public string Type { get; set; } = string.Empty; // object, string, decimal, etc
        public List<FieldValidationConfig>? Properties { get; set; }

        public ItemsConfig()
        {
            Properties = new List<FieldValidationConfig>();
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
