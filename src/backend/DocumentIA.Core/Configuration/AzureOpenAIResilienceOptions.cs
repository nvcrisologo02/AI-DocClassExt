namespace DocumentIA.Core.Configuration;

/// <summary>
/// Opciones de resiliencia para llamadas a Azure OpenAI (clasificación GPT y prompts):
/// reintento in-call ante 429/5xx respetando Retry-After, y circuit breaker con cooldown.
/// </summary>
public class AzureOpenAIResilienceOptions
{
    /// <summary>Habilita el circuit breaker. Si false, solo aplica el reintento in-call.</summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>Fallos consecutivos (por circuitKey) que abren el circuito.</summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>Segundos que el circuito permanece abierto (cooldown) tras abrirse.</summary>
    public int CircuitBreakerOpenSeconds { get; set; } = 45;

    /// <summary>Reintentos adicionales tras el primer intento. 0 = sin reintentos.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Delay base del backoff exponencial en milisegundos.</summary>
    public int InitialRetryDelayMs { get; set; } = 500;

    /// <summary>Tope máximo del delay entre reintentos en segundos (cap del Retry-After/backoff).</summary>
    public int MaxRetryDelaySeconds { get; set; } = 60;
}
