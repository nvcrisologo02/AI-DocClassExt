using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Decorador que agrega resiliencia a cualquier plugin
    /// Implementa retry con exponential backoff y circuit breaker basico
    /// </summary>
    public class ResilientPlugin : IIntegrationPlugin
    {
        private readonly IIntegrationPlugin innerPlugin;
        private readonly RetryPolicy retryPolicy;
        private readonly ILogger logger;
        private int consecutiveFailures = 0;
        private DateTime? circuitOpenedAt = null;
        private const int CircuitBreakerThreshold = 5;
        private const int CircuitBreakerResetMinutes = 5;

        public string PluginName => innerPlugin.PluginName + "-Resilient";
        public string Version => innerPlugin.Version;

        public ResilientPlugin(IIntegrationPlugin innerPlugin, RetryPolicy retryPolicy, ILogger logger)
        {
            this.innerPlugin = innerPlugin;
            this.retryPolicy = retryPolicy;
            this.logger = logger;
        }

        public Task InitializeAsync(Dictionary<string, object> configuration)
        {
            return innerPlugin.InitializeAsync(configuration);
        }

        public async Task<IntegrationResult> ExecuteAsync(Dictionary<string, object> data)
        {
            // Verificar circuit breaker
            if (IsCircuitOpen())
            {
                logger.LogWarning("Circuit breaker abierto para {PluginName}. Rechazando solicitud.", PluginName);
                return new IntegrationResult
                {
                    Success = false,
                    Status = "ERROR",
                    Message = "Servicio temporalmente no disponible (circuit breaker abierto)",
                    Errors = new List<string> { "El circuito esta abierto debido a multiples fallos consecutivos" },
                    Metadata = new Dictionary<string, object>
                    {
                        ["circuitBreakerOpen"] = true,
                        ["consecutiveFailures"] = consecutiveFailures
                    }
                };
            }

            int attempt = 0;
            IntegrationResult? lastResult = null;

            while (attempt <= retryPolicy.MaxRetries)
            {
                attempt++;

                try
                {
                    logger.LogInformation("Ejecutando {PluginName} - Intento {Attempt}/{MaxAttempts}",
                        PluginName, attempt, retryPolicy.MaxRetries + 1);

                    lastResult = await innerPlugin.ExecuteAsync(data);

                    if (lastResult.Success)
                    {
                        // Reset circuit breaker en caso de exito
                        consecutiveFailures = 0;
                        circuitOpenedAt = null;
                        return lastResult;
                    }

                    // Verificar si debemos reintentar segun el status code
                    bool shouldRetry = ShouldRetry(lastResult, attempt);

                    if (!shouldRetry || attempt > retryPolicy.MaxRetries)
                    {
                        RegisterFailure();
                        return lastResult;
                    }

                    // Calcular delay antes del siguiente reintento
                    int delayMs = CalculateDelay(attempt);
                    logger.LogWarning("Intento {Attempt} fallo. Reintentando en {Delay}ms...", attempt, delayMs);
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Excepcion en intento {Attempt} de {PluginName}", attempt, PluginName);

                    if (attempt > retryPolicy.MaxRetries)
                    {
                        RegisterFailure();
                        throw new PluginException(PluginName, "Maximo de reintentos alcanzado", ex, true);
                    }

                    int delayMs = CalculateDelay(attempt);
                    await Task.Delay(delayMs);
                }
            }

            RegisterFailure();
            return lastResult ?? new IntegrationResult
            {
                Success = false,
                Status = "ERROR",
                Message = "Todos los intentos fallaron"
            };
        }

        public async Task<bool> HealthCheckAsync()
        {
            if (IsCircuitOpen())
                return false;

            try
            {
                return await innerPlugin.HealthCheckAsync();
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldRetry(IntegrationResult result, int attempt)
        {
            if (attempt > retryPolicy.MaxRetries)
                return false;

            // Retry si el status code esta en la lista de codigos recuperables
            if (retryPolicy.RetryOnStatusCodes.Contains(result.StatusCode))
                return true;

            // Retry si es un error transient
            if (result.Metadata.ContainsKey("isTransient") && (bool)result.Metadata["isTransient"])
                return true;

            return false;
        }

        private int CalculateDelay(int attempt)
        {
            if (!retryPolicy.ExponentialBackoff)
                return retryPolicy.InitialDelayMs;

            // Exponential backoff: delay = initialDelay * 2^(attempt-1)
            return retryPolicy.InitialDelayMs * (int)Math.Pow(2, attempt - 1);
        }

        private bool IsCircuitOpen()
        {
            if (consecutiveFailures < CircuitBreakerThreshold)
                return false;

            if (circuitOpenedAt == null)
            {
                circuitOpenedAt = DateTime.UtcNow;
                logger.LogError("Circuit breaker ABIERTO para {PluginName} debido a {Failures} fallos consecutivos",
                    PluginName, consecutiveFailures);
                return true;
            }

            // Verificar si ya paso el tiempo de reset
            var minutesSinceOpen = (DateTime.UtcNow - circuitOpenedAt.Value).TotalMinutes;
            if (minutesSinceOpen >= CircuitBreakerResetMinutes)
            {
                logger.LogInformation("Circuit breaker SEMIABIERTO para {PluginName}. Intentando reset...", PluginName);
                consecutiveFailures = CircuitBreakerThreshold - 1; // Semi-open state
                circuitOpenedAt = null;
                return false;
            }

            return true;
        }

        private void RegisterFailure()
        {
            consecutiveFailures++;
            logger.LogWarning("Fallo registrado para {PluginName}. Fallos consecutivos: {Count}",
                PluginName, consecutiveFailures);
        }
    }
}
