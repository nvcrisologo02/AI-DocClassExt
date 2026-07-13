using System.ClientModel;
using System.Collections.Concurrent;
using DocumentIA.Core.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services.Resilience;

public class AzureOpenAIResilienceExecutor : IAzureOpenAIResilienceExecutor
{
    private static readonly int[] RetryableStatusCodes = { 429, 500, 502, 503, 504 };

    private readonly AzureOpenAIResilienceOptions _options;
    private readonly ILogger<AzureOpenAIResilienceExecutor> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly ConcurrentDictionary<string, CircuitState> _circuits =
        new(StringComparer.OrdinalIgnoreCase);

    public AzureOpenAIResilienceExecutor(
        IOptions<AzureOpenAIResilienceOptions> options,
        ILogger<AzureOpenAIResilienceExecutor> logger,
        TelemetryClient telemetryClient)
    {
        _options = options.Value;
        _logger = logger;
        _telemetryClient = telemetryClient;
    }

    public async Task<T> ExecuteAsync<T>(
        string circuitKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (IsCircuitOpen(circuitKey))
        {
            TrackCircuit("AOAI.CircuitRejected", circuitKey);
            throw new RateLimitExhaustedException(
                $"Circuito abierto para '{circuitKey}'. Llamada rechazada durante cooldown.");
        }

        var maxAttempts = Math.Max(1, _options.MaxRetries + 1);
        ClientResultException? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await operation(cancellationToken);
                RegisterCircuitSuccess(circuitKey);
                return result;
            }
            catch (ClientResultException ex) when (IsRetryableStatus(ex.Status))
            {
                lastError = ex;
                RegisterCircuitFailure(circuitKey);

                if (attempt >= maxAttempts || IsCircuitOpen(circuitKey))
                {
                    break;
                }

                var delay = ComputeDelay(attempt, ex);
                TrackRetry(circuitKey, attempt, delay, ex.Status);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new RateLimitExhaustedException(
            $"Reintentos agotados para '{circuitKey}' (status={lastError?.Status}).",
            lastError);
    }

    private TimeSpan ComputeDelay(int attempt, ClientResultException ex)
    {
        var backoff = TimeSpan.FromMilliseconds(
            _options.InitialRetryDelayMs * Math.Pow(2, attempt - 1));
        var retryAfter = TryGetRetryAfter(ex);
        var delay = retryAfter is not null && retryAfter.Value > backoff ? retryAfter.Value : backoff;
        var cap = TimeSpan.FromSeconds(Math.Max(1, _options.MaxRetryDelaySeconds));
        return delay > cap ? cap : delay;
    }

    private static TimeSpan? TryGetRetryAfter(ClientResultException ex)
    {
        var response = ex.GetRawResponse();
        if (response is null)
        {
            return null;
        }

        if (!response.Headers.TryGetValue("Retry-After", out var value))
        {
            return null;
        }

        var seconds = ParseRetryAfterSeconds(value);
        return seconds is null ? null : TimeSpan.FromSeconds(seconds.Value);
    }

    internal static int? ParseRetryAfterSeconds(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        return int.TryParse(headerValue.Trim(), out var seconds) && seconds >= 0
            ? seconds
            : null;
    }

    internal static bool IsRetryableStatus(int status)
        => Array.IndexOf(RetryableStatusCodes, status) >= 0;

    private bool IsCircuitOpen(string circuitKey)
    {
        if (!_options.EnableCircuitBreaker)
        {
            return false;
        }

        var circuit = _circuits.GetOrAdd(circuitKey, _ => new CircuitState());
        lock (circuit.SyncRoot)
        {
            if (!circuit.OpenUntilUtc.HasValue)
            {
                return false;
            }

            if (DateTimeOffset.UtcNow >= circuit.OpenUntilUtc.Value)
            {
                circuit.OpenUntilUtc = null;
                circuit.ConsecutiveFailures = 0;
                TrackCircuit("AOAI.CircuitClosed", circuitKey);
                return false;
            }

            return true;
        }
    }

    private void RegisterCircuitFailure(string circuitKey)
    {
        if (!_options.EnableCircuitBreaker)
        {
            return;
        }

        var threshold = Math.Max(1, _options.CircuitBreakerFailureThreshold);
        var openSeconds = Math.Max(1, _options.CircuitBreakerOpenSeconds);
        var circuit = _circuits.GetOrAdd(circuitKey, _ => new CircuitState());
        lock (circuit.SyncRoot)
        {
            circuit.ConsecutiveFailures++;
            if (circuit.ConsecutiveFailures < threshold)
            {
                return;
            }

            circuit.OpenUntilUtc = DateTimeOffset.UtcNow.AddSeconds(openSeconds);
            circuit.ConsecutiveFailures = 0;
            TrackCircuit("AOAI.CircuitOpen", circuitKey);
        }
    }

    private void RegisterCircuitSuccess(string circuitKey)
    {
        if (!_options.EnableCircuitBreaker)
        {
            return;
        }

        var circuit = _circuits.GetOrAdd(circuitKey, _ => new CircuitState());
        lock (circuit.SyncRoot)
        {
            circuit.ConsecutiveFailures = 0;
            circuit.OpenUntilUtc = null;
        }
    }

    private void TrackRetry(string circuitKey, int attempt, TimeSpan delay, int status)
    {
        _logger.LogWarning(
            "Azure OpenAI rate-limit (status={Status}) en '{CircuitKey}'. Reintento {Attempt} en {DelayMs}ms.",
            status, circuitKey, attempt, (int)delay.TotalMilliseconds);

        _telemetryClient.TrackEvent("AOAI.RateLimitRetry", new Dictionary<string, string>
        {
            ["circuitKey"] = circuitKey,
            ["attempt"] = attempt.ToString(),
            ["delayMs"] = ((int)delay.TotalMilliseconds).ToString(),
            ["statusCode"] = status.ToString()
        });
    }

    private void TrackCircuit(string eventName, string circuitKey)
    {
        _telemetryClient.TrackEvent(eventName, new Dictionary<string, string>
        {
            ["circuitKey"] = circuitKey
        });
    }

    private sealed class CircuitState
    {
        public int ConsecutiveFailures;
        public DateTimeOffset? OpenUntilUtc;
        public readonly object SyncRoot = new();
    }
}
