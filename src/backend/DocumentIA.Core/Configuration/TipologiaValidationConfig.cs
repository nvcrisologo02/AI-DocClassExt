// DocumentIA.Core/Configuration/TipologiaValidationConfig.cs
namespace DocumentIA.Core.Configuration
{
    public class TipologiaValidationConfig
    {
        public string TipologiaId { get; set; } = string.Empty;
        public string TipologiaNombre { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("gdc")]
        public GdcConfig? Gdc { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("classification")]
        public ClassificationTdnConfig? Classification { get; set; }
        // Matricula utilizada para GDC upload checks for this tipologia.
        // If empty, the global default from configuration will be used.
        [Obsolete("Use Gdc.Matricula")]
        public string TipologiaMGDCMatricula { get; set; } = string.Empty;
        // GDC taxonomy fields — mandatory for document create in SINTWS.
        // GdcTipoDocumento: código del tipo de documento en catálogo GDC (ej. "NOTS", "CERT", "ESIN").
        [Obsolete("Use Gdc.TipoDocumento")]
        public string GdcTipoDocumento { get; set; } = string.Empty;
        // GdcSubtipoDocumento: código del subtipo de documento en catálogo GDC (opcional, ej. "NOTS01").
        [Obsolete("Use Gdc.SubtipoDocumento")]
        public string GdcSubtipoDocumento { get; set; } = string.Empty;
        // GdcSerie: serie documental GDC (ej. "AI09", "AI05"). Confirmar con Sistemas el valor exacto.
        [Obsolete("Use Gdc.Serie")]
        public string GdcSerie { get; set; } = string.Empty;
        // TDN jerárquico opcional asociado a la tipología.
        [Obsolete("Use Classification.Tdn1")]
        public string Tdn1 { get; set; } = string.Empty;
        [Obsolete("Use Classification.Tdn2")]
        public string Tdn2 { get; set; } = string.Empty;
        // SkipGDCUpload: si true, la subida a GDC se omite por defecto para esta tipología.
        // Puede sobreescribirse explícitamente en cada petición vía Instrucciones.SkipGDCUpload.
        [Obsolete("Use Gdc.SkipUpload")]
        public bool SkipGDCUpload { get; set; } = false;
        /// <summary>
        /// Descripción optimizada para el prompt de clasificación GPT.
        /// Si está vacía, se usa TipologiaNombre como fallback en el prompt.
        /// </summary>
        [Obsolete("Use Classification.GptDescripcion")]
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
        /// <summary>
        /// Configuración de AssetResolver para esta tipología.
        /// Si es null o Enabled=false, la actividad de obtención de activo no se ejecuta (salvo override por instrucciones).
        /// </summary>
        public TipologiaAssetResolverConfig? AssetResolver { get; set; }
        /// <summary>
        /// Política específica para clasificación documental jerárquica (TDN1 -> TDN2 -> Matrícula).
        /// Se usa como contrato de configuración y no altera el flujo si no se consume explícitamente.
        /// </summary>
        public ClassificationPolicyConfig? ClassificationPolicy { get; set; }
        /// <summary>
        /// Catálogo jerárquico de clases para clasificación documental.
        /// Permite definir familias y subtipos versionados en configuración.
        /// </summary>
        public TdnClassificationCatalogConfig? ClassificationCatalog { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("retentionPolicy")]
        public RetentionPolicyConfig? RetentionPolicy { get; set; }
        /// <summary>
        /// Máximo de páginas del documento para permitir extracción completa.
        /// Si el documento supera este valor, el pipeline se detiene con PAGINAS_EXCEDIDAS.
        /// 0 o ausente = sin límite para esta tipología.
        /// </summary>
        public int MaxPaginasDocumento { get; set; } = 0;
        public List<FieldValidationConfig> Fields { get; set; } = new List<FieldValidationConfig>();

        #pragma warning disable CS0618
        [System.Text.Json.Serialization.JsonIgnore]
        public bool ResolvedSkipGDCUpload => Gdc?.SkipUpload ?? SkipGDCUpload;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ResolvedMatricula => Gdc?.Matricula ?? TipologiaMGDCMatricula;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ResolvedGdcTipo => Gdc?.TipoDocumento ?? GdcTipoDocumento;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ResolvedGdcSubtipo => Gdc?.SubtipoDocumento ?? GdcSubtipoDocumento;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ResolvedGdcSerie => Gdc?.Serie ?? GdcSerie;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ResolvedTdn1 => Classification?.Tdn1 ?? Tdn1;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ResolvedTdn2 => Classification?.Tdn2 ?? Tdn2;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ResolvedGptDescripcion => Classification?.GptDescripcion ?? GptDescripcion;
        [System.Text.Json.Serialization.JsonIgnore]
        public bool ResolvedEnableRules => Classification?.EnableRules ?? true;
        #pragma warning restore CS0618

        public TipologiaValidationConfig()
        {
            Fields = new List<FieldValidationConfig>();
        }
    }

    public class GdcConfig
    {
        [System.Text.Json.Serialization.JsonPropertyName("skipUpload")]
        public bool SkipUpload { get; set; } = false;
        [System.Text.Json.Serialization.JsonPropertyName("matricula")]
        public string Matricula { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("tipoDocumento")]
        public string TipoDocumento { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("subtipoDocumento")]
        public string SubtipoDocumento { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("serie")]
        public string Serie { get; set; } = string.Empty;
    }

    public class ClassificationTdnConfig
    {
        [System.Text.Json.Serialization.JsonPropertyName("tdn1")]
        public string Tdn1 { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("tdn2")]
        public string Tdn2 { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("gptDescripcion")]
        public string GptDescripcion { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("enableRules")]
        public bool EnableRules { get; set; } = true;
    }

    public class ClassificationPolicyConfig
    {
        /// <summary>Umbral para aceptar sin fallback.</summary>
        public double AcceptWithoutFallbackThreshold { get; set; } = 0.85;
        /// <summary>Delta mínimo entre top1 y top2 para evitar ambigüedad.</summary>
        public double Top1Top2AmbiguityDelta { get; set; } = 0.15;
        /// <summary>Número de páginas por defecto a clasificar.</summary>
        public int DefaultPagesToClassify { get; set; } = 3;
        /// <summary>Número máximo de páginas cuando se escala por ambigüedad.</summary>
        public int EscalatedPagesToClassify { get; set; } = 5;
        /// <summary>Si true, el backend aplica límite de páginas aunque el cliente no recorte.</summary>
        public bool EnforceBackendPageLimit { get; set; } = true;
        /// <summary>Timeout del fallback GPT en segundos.</summary>
        public int FallbackTimeoutSeconds { get; set; } = 8;
        /// <summary>Número máximo de reintentos del fallback GPT.</summary>
        public int FallbackMaxRetries { get; set; } = 1;
        /// <summary>Backoff entre reintentos de fallback GPT en milisegundos.</summary>
        public int FallbackBackoffMs { get; set; } = 500;
        /// <summary>Objetivo máximo de fallback rate.</summary>
        public double FallbackRateTarget { get; set; } = 0.25;
        /// <summary>Umbral de alerta de fallback rate.</summary>
        public double FallbackRateAlertThreshold { get; set; } = 0.20;
        /// <summary>Objetivo de coste por documento en EUR.</summary>
        public double CostTargetEuroPerDocument { get; set; } = 0.10;
        /// <summary>Objetivo de latencia p95 en segundos.</summary>
        public int LatencyP95TargetSeconds { get; set; } = 90;
        /// <summary>Estados de negocio válidos para clasificación documental.</summary>
        public List<string> ValidBusinessStates { get; set; } = new() { "UNKNOWN", "OUT_OF_SCOPE", "NEEDS_REVIEW" };
        /// <summary>Si true, ERROR se reserva para fallos técnicos.</summary>
        public bool TechnicalErrorOnly { get; set; } = true;
        /// <summary>Si true, evita reproceso por cambio de versión de clasificador cuando el SHA coincide.</summary>
        public bool SkipReprocessWhenShaMatches { get; set; } = true;
    }

    public class RetentionPolicyConfig
    {
        [System.Text.Json.Serialization.JsonPropertyName("blobRetentionDays")]
        public int BlobRetentionDays { get; set; } = -1;
    }

    public class TdnClassificationCatalogConfig
    {
        /// <summary>Versión semántica del catálogo de clasificación documental.</summary>
        public string CatalogVersion { get; set; } = "1.0";
        /// <summary>Familias TDN1 con sus subtipos TDN2 y matrícula asociada.</summary>
        public List<TdnFamilyConfig> Families { get; set; } = new();
    }

    public class TdnFamilyConfig
    {
        public string Tdn1Code { get; set; } = string.Empty;
        public string Tdn1Name { get; set; } = string.Empty;
        public List<TdnSubtypeConfig> Subtypes { get; set; } = new();
    }

    public class TdnSubtypeConfig
    {
        public string Tdn2Code { get; set; } = string.Empty;
        public string Tdn2Name { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;
    }

    public class FieldValidationConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // decimal, string, date, array, object, etc
        public bool Required { get; set; }
        public bool AvoidConfidence { get; set; } = false;
        public string Description { get; set; } = string.Empty;
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
        public string? SecondaryModelKey { get; set; }
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
        [System.Text.Json.Serialization.JsonPropertyName("clasifUmbralFallback")]
        public double ClasifUmbralFallback { get; set; } = 0.85;
        /// <summary>Ratio mínimo de campos obtenidos/esperados para considerar la extracción CU suficiente.
        /// Si CU no supera este ratio, se activa el fallback GPT de extracción.
        /// null = usar config.MinFieldsRatio (GptFallbackExtraerSettings) como último recurso.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("extracUmbralFallback")]
        public double? ExtracUmbralFallback { get; set; } = null;
        /// <summary>Umbral mínimo de completitud (ratio de campos esperados presentes) para considerar CU suficiente.
        /// Si no se informa, usa ExtracUmbralFallback y, en último término, config.MinFieldsRatio.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("extracUmbralFallbackCompletitud")]
        public double? ExtracUmbralFallbackCompletitud { get; set; } = null;
        /// <summary>Umbral mínimo de confianza global de extracción CU para considerar CU suficiente.
        /// Si no se informa, usa ExtracUmbralFallback y, en último término, config.MinFieldsRatio.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("extracUmbralFallbackConfianza")]
        public double? ExtracUmbralFallbackConfianza { get; set; } = null;
        /// <summary>Peso del promedio de confianza de campos en el cálculo CU.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("extracWeightCampos")]
        public double ExtracWeightCampos { get; set; } = 0.5;
        /// <summary>Peso del ratio de campos requeridos presentes en el cálculo CU.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("extracWeightRequeridos")]
        public double ExtracWeightRequeridos { get; set; } = 0.25;
        /// <summary>Peso de la penalización por warnings en el cálculo CU.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("extracWeightWarnings")]
        public double ExtracWeightWarnings { get; set; } = 0.15;
        /// <summary>Confianza global mínima para estado OK.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("umbralOK")]
        public double UmbralOK { get; set; } = 0.85;
        /// <summary>Confianza global mínima para estado REVISION (por debajo es ERROR).</summary>
        [System.Text.Json.Serialization.JsonPropertyName("umbralRevision")]
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

    /// <summary>
    /// Configuración de AssetResolver por tipología.
    /// Controla si se ejecuta la consulta a DM_POSICION_AAII_TB y qué campos se devuelven.
    /// </summary>
    public class TipologiaAssetResolverConfig
    {
        /// <summary>Si true, la actividad ObtenerActivo se ejecuta por defecto para esta tipología.</summary>
        public bool Enabled { get; set; } = false;
        /// <summary>
        /// Columnas de DM_POSICION_AAII_TB a devolver además de las obligatorias (ID_ACTIVO_SAREB, FCH_CIERRE).
        /// null = solo obligatorias.
        /// </summary>
        public List<string>? CamposSolicitados { get; set; }
        /// <summary>
        /// Posibles nombres de campo extraído que mapean al IDUFIR de la tabla (columna ID_IDUFIR).
        /// </summary>
        public List<string> MapeoIdufir { get; set; } = new();
        /// <summary>
        /// Posibles nombres de campo extraído que mapean a la referencia catastral (columna ID_REF_CATAST).
        /// </summary>
        public List<string> MapeoReferenciaCatastral { get; set; } = new();

        /// <summary>
        /// Modo de combinación entre criterios resueltos de búsqueda.
        /// Valores admitidos: AND, OR. Default: OR.
        /// </summary>
        public string ModoCombinacionCriterios { get; set; } = "OR";

        // ── Búsqueda por dirección como criterio adicional ──

        /// <summary>Si true, se incluye IDUFIR como criterio de búsqueda (se auto-detecta por aliases si no hay override). Default: true.</summary>
        public bool BusquedaIdufirHabilitada { get; set; } = true;
        /// <summary>Si true, se incluye ReferenciaCatastral como criterio de búsqueda. Default: true.</summary>
        public bool BusquedaReferenciaCatastralHabilitada { get; set; } = true;
        /// <summary>Si true, permite búsqueda fuzzy por dirección como criterio adicional.</summary>
        public bool BusquedaDireccionHabilitada { get; set; } = false;
        /// <summary>Aliases para auto-detectar una dirección libre/completa en datos extraídos.</summary>
        public List<string> MapeoDireccionCompleta { get; set; } = new();
        /// <summary>Aliases para auto-detectar nombre de vía en datos extraídos.</summary>
        public List<string> MapeoDireccionNombreVia { get; set; } = new();
        /// <summary>Aliases para auto-detectar número de vía en datos extraídos.</summary>
        public List<string> MapeoDireccionNumero { get; set; } = new();
        /// <summary>Aliases para auto-detectar municipio en datos extraídos.</summary>
        public List<string> MapeoDireccionMunicipio { get; set; } = new();
        /// <summary>Aliases para auto-detectar código postal en datos extraídos.</summary>
        public List<string> MapeoDireccionCodigoPostal { get; set; } = new();
        /// <summary>Umbral mínimo de score para aceptar un match por dirección (0.0–1.0, default 0.75).</summary>
        public double UmbralScoreDireccion { get; set; } = 0.75;
    }
}
