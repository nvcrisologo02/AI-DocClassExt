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
