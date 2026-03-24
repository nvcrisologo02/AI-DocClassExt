// DocumentIA.Core/Configuration/TipologiaValidationConfig.cs
namespace DocumentIA.Core.Configuration
{
    public class TipologiaValidationConfig
    {
        public string TipologiaId { get; set; } = string.Empty;
        public string TipologiaNombre { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        // Matricula utilizada para GDC upload checks for this tipologia.
        // If empty, the global default from configuration will be used.
        public string TipologiaMGDCMatricula { get; set; } = string.Empty;
        // GDC taxonomy fields — mandatory for document create in SINTWS.
        // GdcTipoDocumento: código del tipo de documento en catálogo GDC (ej. "NOTS", "CERT", "ESIN").
        public string GdcTipoDocumento { get; set; } = string.Empty;
        // GdcSubtipoDocumento: código del subtipo de documento en catálogo GDC (opcional, ej. "NOTS01").
        public string GdcSubtipoDocumento { get; set; } = string.Empty;
        // GdcSerie: serie documental GDC (ej. "AI09", "AI05"). Confirmar con Sistemas el valor exacto.
        public string GdcSerie { get; set; } = string.Empty;
        public TipologiaExtractionConfig Extraction { get; set; } = new();
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

    public class TipologiaExtractionConfig
    {
        public bool Enabled { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ModelKey { get; set; } = string.Empty;
        public bool AutoMapUnmappedFields { get; set; } = true;
        public List<ExtractionFieldMappingConfig> FieldMappings { get; set; } = new List<ExtractionFieldMappingConfig>();
    }

    public class ExtractionFieldMappingConfig
    {
        public string TargetField { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
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

    public class ExtractionModelRegistry
    {
        public List<ExtractionModelConfig> Models { get; set; } = new List<ExtractionModelConfig>();
    }

    public class ExtractionModelConfig
    {
        public string Key { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string AnalyzerId { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string ProcessingLocation { get; set; } = string.Empty;
        public string InputRange { get; set; } = string.Empty;
    }
}
