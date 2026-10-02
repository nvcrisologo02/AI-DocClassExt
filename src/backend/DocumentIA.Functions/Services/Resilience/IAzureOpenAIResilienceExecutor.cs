namespace DocumentIA.Functions.Services.Resilience;

/// <summary>
/// Ejecuta una operación contra Azure OpenAI aplicando reintento in-call ante 429/5xx
/// (respetando Retry-After) y circuit breaker con cooldown por circuitKey.
/// </summary>
public interface IAzureOpenAIResilienceExecutor
{
    Task<T> ExecuteAsync<T>(
        string circuitKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una sola tentativa: respeta el circuito (lanza RateLimitExhaustedException si esta
    /// abierto), registra el exito en el circuito y deja propagar la excepcion original sin
    /// reintentar. Cuentan como fallo del circuito la ClientResultException con estado
    /// reintentable (429/5xx) y la HttpRequestException. Los timeouts por cancelacion NO se
    /// cuentan: el ejecutor recibe un unico token y no distingue un temporizador propio del
    /// llamante de una cancelacion externa. Para llamadas que no deben esperar a un
    /// Retry-After, como los embeddings del clasificador A (AB#100779).
    /// </summary>
    Task<T> ExecuteOnceAsync<T>(
        string circuitKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
