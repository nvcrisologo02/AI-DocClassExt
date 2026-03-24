namespace DocumentIA.Core.Configuration
{
    /// <summary>
    /// Settings for GDC (Gestor Documental) integration and upload behavior.
    /// Bound from configuration section 'GDC'.
    /// </summary>
    public class GdcSettings
    {
        public string Endpoint { get; set; } = string.Empty;

        // Default matricula to use when a tipologia doesn't define its own
        public string DefaultMatricula { get; set; } = "OTROS_DOCUMENTOS";

        // HTTP/SOAP timeout in seconds
        public int TimeoutSeconds { get; set; } = 30;

        // Identity credentials for GDC SOAP authentication (passed as arg0 in every operation)
        public string ApplicationId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string NominalUser { get; set; } = string.Empty;

        // GDC entity typeId used in create and searchEntities (confirm with Sistemas if different)
        public string DocumentTypeId { get; set; } = "document";

        // GDC field name for binary file content in create operation (confirm with Sistemas)
        public string ContentFieldName { get; set; } = "content";

        // origen_documento field value — mandatory since SINTWS v4.0 (2020); identifies the source system
        public string OrigenDocumento { get; set; } = string.Empty;

        // clase_expediente value for the entity expediente field (OTCS folder class, e.g. "AI", "AAII")
        // Used to place uploaded documents in the correct activo folder in OTCS
        public string ClaseExpediente { get; set; } = string.Empty;

        // Optional: DocRepository for searchEntities (name + id of the GDC repository to search)
        // Leave empty to search across all repositories (server default when repo is not found)
        public string RepositoryId { get; set; } = string.Empty;
        public string RepositoryName { get; set; } = string.Empty;

        // Mandatory GDC document fields — global to all uploads from this application
        // servicer: código del servicer (ej. "9999" en DEV)
        public string Servicer { get; set; } = string.Empty;
        // entidad_origen: código de la entidad/servicer origen del documento (normalmente igual a Servicer)
        public string EntidadOrigen { get; set; } = string.Empty;
        // proceso_carga: código del proceso de carga (ej. "PC01", "CKP1")
        public string ProcesoCarga { get; set; } = string.Empty;
        // tipo_expediente: tipo de expediente GDC ("AI" = Activo Inmobiliario, "AF" = Activo Financiero)
        public string TipoExpediente { get; set; } = "AI";
        // publico: "verdadero" o "falso" (string, no boolean nativo SOAP)
        public string Publico { get; set; } = "verdadero";

        // HTTP Basic Auth credentials for the GDC transport (separate from SOAP Identity)
        public string HttpBasicUsername { get; set; } = string.Empty;
        public string HttpBasicPassword { get; set; } = string.Empty;

        // Retry behavior
        public int MaxRetries { get; set; } = 3;
        public int InitialDelayMs { get; set; } = 200;
        // Use exponential backoff for retries
        public bool ExponentialBackoff { get; set; } = true;

        // Circuit breaker: number of consecutive failures to open the breaker
        public int CircuitBreakerFailures { get; set; } = 5;

        // Duration in ms for which the circuit stays open before a trial request
        public int CircuitBreakerDurationMs { get; set; } = 30000;
    }
}
