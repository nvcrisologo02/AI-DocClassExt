using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services
{
    /// <summary>
    /// Wrapper that adds retry with exponential backoff and a simple circuit breaker
    /// around an inner IGdcService implementation.
    /// </summary>
    public class ResilientGdcService : IGdcService
    {
        private readonly IGdcService inner;
        private readonly GdcSettings settings;
        private readonly ILogger<ResilientGdcService> logger;

        private int consecutiveFailures = 0;
        private DateTime? circuitOpenedAt = null;

        public ResilientGdcService(IGdcService inner, IOptions<GdcSettings> options, ILogger<ResilientGdcService> logger)
        {
            this.inner = inner;
            this.settings = options.Value;
            this.logger = logger;
        }

        private bool IsCircuitOpen()
        {
            if (circuitOpenedAt == null) return false;
            var elapsed = (DateTime.UtcNow - circuitOpenedAt.Value).TotalMilliseconds;
            if (elapsed > settings.CircuitBreakerDurationMs)
            {
                // move to half-open
                circuitOpenedAt = null;
                consecutiveFailures = 0;
                logger.LogInformation("GDC circuit breaker transitioning to half-open (trial)");
                return false;
            }
            return true;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        {
            if (IsCircuitOpen())
            {
                throw new InvalidOperationException("GDC circuit breaker abierto");
            }

            int attempt = 0;
            Exception? lastEx = null;

            while (attempt <= settings.MaxRetries)
            {
                try
                {
                    attempt++;
                    var res = await operation(cancellationToken);
                    // success -> reset failures
                    consecutiveFailures = 0;
                    return res;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    lastEx = ex;
                    consecutiveFailures++;
                    logger.LogWarning(ex, "GDC attempt {Attempt} failed", attempt);

                    if (consecutiveFailures >= settings.CircuitBreakerFailures)
                    {
                        circuitOpenedAt = DateTime.UtcNow;
                        logger.LogError("GDC circuit breaker opened after {Failures} consecutive failures", consecutiveFailures);
                        throw new InvalidOperationException("GDC circuit breaker opened", ex);
                    }

                    if (attempt > settings.MaxRetries)
                    {
                        break;
                    }

                    var delay = settings.InitialDelayMs;
                    if (settings.ExponentialBackoff)
                    {
                        delay = settings.InitialDelayMs * (int)Math.Pow(2, attempt - 1);
                    }

                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw lastEx ?? new Exception("GDC: unknown failure");
        }

        public Task<(bool Exists, string? ObjectId)> ConsultarDocumentoAsync(string idActivo, string md5, string matricula, CancellationToken cancellationToken = default)
        {
            return ExecuteWithRetryAsync(ct => inner.ConsultarDocumentoAsync(idActivo, md5, matricula, ct), cancellationToken);
        }

        public Task<ResultadoGDC> SubirDocumentoAsync(SubirGDCInput input, CancellationToken cancellationToken = default)
        {
            return ExecuteWithRetryAsync(ct => inner.SubirDocumentoAsync(input, ct), cancellationToken);
        }
    }
}
