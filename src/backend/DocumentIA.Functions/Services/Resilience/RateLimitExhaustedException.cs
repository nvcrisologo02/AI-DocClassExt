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

    /// <summary>
    /// Consumo de IA ya realizado antes de agotar los reintentos. Una peticion que
    /// completa la fase 1 y agota la 2 con 429 ha pagado la primera llamada: sin
    /// esto ese gasto desapareceria y se volveria a pagar al reintentar (AB#100227).
    /// </summary>
    public List<DocumentIA.Core.Models.ConsumoIA> ConsumosParciales { get; } = new();
}
