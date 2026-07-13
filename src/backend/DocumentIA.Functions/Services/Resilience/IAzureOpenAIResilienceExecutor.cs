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
}
