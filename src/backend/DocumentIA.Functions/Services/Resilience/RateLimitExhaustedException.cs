namespace DocumentIA.Functions.Services.Resilience;

/// <summary>
/// Señala que las llamadas a Azure OpenAI agotaron los reintentos por rate limit (429/5xx),
/// o que el circuito estaba abierto durante el cooldown.
/// </summary>
public class RateLimitExhaustedException : Exception
{
    public RateLimitExhaustedException(string message) : base(message) { }
    public RateLimitExhaustedException(string message, Exception? innerException)
        : base(message, innerException) { }
}
