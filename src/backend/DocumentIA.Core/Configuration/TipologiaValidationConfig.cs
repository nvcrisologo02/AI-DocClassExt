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
        // SkipGDCUpload: si true, la subida a GDC se omite por defecto para esta tipología.
        // Puede sobreescribirse explícitamente en cada petición vía Instrucciones.SkipGDCUpload.
        public bool SkipGDCUpload { get; set; } = false;
        /// <summary>
        /// Descripción optimizada para el prompt de clasificación GPT.
        /// Si está vacía, se usa TipologiaNombre como fallback en el prompt.
        /// </summary>
        public string GptDescripcion { get; set; } = string.Empty;
        public TipologiaExtractionConfig Extraction { get; set; } = new();
        /// <summary>
        /// Configuración del prompt libre. Cuando está habilitado, se ejecuta el prompt contra
        /// el modelo indicado y el resultado se devuelve en ResultadoPrompt de la salida.
        /// Puede coexistir con Extraction (flujo secuencial) o usarse como único paso.
        /// </summary>
        public PromptConfig? PromptConfig { get; set; }
        /// <summary>
        /// Configuración de umbrales y pesos para el cálculo de confianza.
        /// Si es null, se usan los defaults de ConfidenceConfig.
        /// </summary>
        public ConfidenceConfig? ConfidenceConfig { get; set; }
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
        public bool Enabled { get; set; } = true;
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

    /// <summary>
    /// Umbrales y pesos para el cálculo de confianza agregada.
    /// Los defaults (0.5/0.3/0.2 pesos; 0.85/0.70 umbrales) se aplican cuando este objeto no se define en el JSON.
    /// </summary>
    public class ConfidenceConfig
    {
        /// <summary>Umbral de confianza de clasificación DI por debajo del cual se activa fallback GPT.
        /// También se usa como umbral BAJA_CONFIANZA_CLASIFICACION cuando no hay umbral en la petición.</summary>
        public double ClasifUmbralFallback { get; set; } = 0.85;
        /// <summary>Ratio mínimo de campos obtenidos/esperados para considerar la extracción CU suficiente.
        /// Si CU no supera este ratio, se activa el fallback GPT de extracción.
        /// null = usar config.MinFieldsRatio (GptFallbackExtraerSettings) como último recurso.</summary>
        public double? ExtracUmbralFallback { get; set; } = null;
        /// <summary>Umbral mínimo de completitud (ratio de campos esperados presentes) para considerar CU suficiente.
        /// Si no se informa, usa ExtracUmbralFallback y, en último término, config.MinFieldsRatio.</summary>
        public double? ExtracUmbralFallbackCompletitud { get; set; } = null;
        /// <summary>Umbral mínimo de confianza global de extracción CU para considerar CU suficiente.
        /// Si no se informa, usa ExtracUmbralFallback y, en último término, config.MinFieldsRatio.</summary>
        public double? ExtracUmbralFallbackConfianza { get; set; } = null;
        /// <summary>Peso del promedio de confianza de campos en el cálculo CU.</summary>
        public double ExtracWeightCampos { get; set; } = 0.5;
        /// <summary>Peso del ratio de campos requeridos presentes en el cálculo CU.</summary>
        public double ExtracWeightRequeridos { get; set; } = 0.3;
        /// <summary>Peso de la penalización por warnings en el cálculo CU.</summary>
        public double ExtracWeightWarnings { get; set; } = 0.2;
        /// <summary>Confianza global mínima para estado OK.</summary>
        public double UmbralOK { get; set; } = 0.85;
        /// <summary>Confianza global mínima para estado REVISION (por debajo es ERROR).</summary>
        public double UmbralRevision { get; set; } = 0.70;
    }

    public class ExtractionModelRegistry
    {
        public List<ExtractionModelConfig> Models { get; set; } = new List<ExtractionModelConfig>();
    }

    public class ExtractionModelConfig
    {
        public string Key { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool UseAsFallback { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string AuthMode { get; set; } = "ApiKey";
        public string AnalyzerId { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string ProcessingLocation { get; set; } = string.Empty;
        public string InputRange { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-11-30";
        public int TimeoutSeconds { get; set; } = 60;
        public int PollIntervalMs { get; set; } = 1000;
        public double MinFieldsRatio { get; set; } = 0.5;
        public double Temperature { get; set; } = 0.0;
        public int MaxTokens { get; set; } = 2000;
    }
}
