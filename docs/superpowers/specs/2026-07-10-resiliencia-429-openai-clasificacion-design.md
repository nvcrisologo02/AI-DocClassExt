# Diseño — Resiliencia 429 (rate limit) en clasificación GPT y prompts

- **Fecha**: 2026-07-10
- **Estado**: Aprobado (pendiente de plan de implementación)
- **Ámbito**: `DocumentIA.Functions` (+ `DocumentIA.Core` para opciones de configuración)

## 1. Contexto y problema

Hoy el camino de clasificación por GPT (`GptClasificarDataProvider`) y el de prompts
(`OpenAIPromptDataProvider`) crean el `AzureOpenAIClient` con opciones por defecto y **no**
tienen manejo dedicado de errores `429 Too Many Requests`:

- El único reintento es el implícito del SDK (por defecto, no configurado ni ajustable en el código).
- No hay circuit breaker, ni cooldown, ni lectura del header `Retry-After`.
- En clasificación, `CompleteChatAsync` no captura la excepción: un 429 que agote los reintentos
  del SDK se propaga hasta `ClasificarActivity` → el `catch` del orquestador hace `throw;`
  ([`DocumentProcessOrchestrator.cs:851`](../../../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs))
  → **la orquestación falla con el mensaje del 429 como resultado final**.

En cambio, la extracción con Content Understanding
([`AzureContentUnderstandingProvider.cs`](../../../src/backend/DocumentIA.Functions/Services/AzureContentUnderstandingProvider.cs))
sí implementa el patrón completo: bucle de reintento manual con `Retry-After`, detección de
429/5xx reintentables, y circuit breaker con cooldown y failover a modelo alterno.

**Objetivo**: dar a la clasificación GPT (y a los prompts) un mecanismo de reintento con
cooldown ante 429 y evitar que un error de cuota termine como resultado final del documento.

## 2. Decisiones (acordadas)

| Decisión | Elección |
|---|---|
| Empaquetado | **Componente reutilizable** (executor/policy propio) — port del patrón CU |
| Consumidores | Clasificación GPT (`GptClasificarDataProvider`) **y** prompts (`OpenAIPromptDataProvider`) |
| Reintento | **In-call respetando `Retry-After`** (backoff exponencial + N intentos) |
| Circuit breaker | **Sí**, con cooldown (patrón CU) |
| Failover a modelo alterno | **No** (fuera de alcance) |
| Retry de Durable Functions | **No** (fuera de alcance) |
| Desenlace al agotar cuota | **Estado retriable diferenciado** (`PENDIENTE_REINTENTO`), salida limpia sin stacktrace |

## 3. Arquitectura

### 3.1 Componente reutilizable

Ubicación (espejo del patrón CU existente):

- `src/backend/DocumentIA.Functions/Services/Resilience/IAzureOpenAIResilienceExecutor.cs`
- `src/backend/DocumentIA.Functions/Services/Resilience/AzureOpenAIResilienceExecutor.cs`
- `src/backend/DocumentIA.Functions/Services/Resilience/RateLimitExhaustedException.cs`
- `src/backend/DocumentIA.Core/Configuration/AzureOpenAIResilienceOptions.cs`
  (espejo de `AzureContentUnderstandingOptions`)

Interfaz genérica (reutilizable por ambos providers):

```csharp
public interface IAzureOpenAIResilienceExecutor
{
    Task<T> ExecuteAsync<T>(
        string circuitKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
```

Comportamiento de `ExecuteAsync` (port directo de `AzureContentUnderstandingProvider`):

1. Si el circuito de `circuitKey` está abierto (`OpenUntilUtc` en el futuro) → lanza
   `RateLimitExhaustedException` de inmediato (fail-fast durante cooldown).
2. Bucle de `1..MaxRetries+1` intentos:
   - Éxito → registra éxito en circuito, devuelve resultado.
   - `catch ClientResultException ex` con `ex.Status ∈ {429, 500, 502, 503, 504}` → registra
     fallo; si es el último intento lanza `RateLimitExhaustedException`; si no, espera `delay`
     y reintenta.
   - Excepción no reintentable (p.ej. 400) → se relanza tal cual, **sin** contar al circuito.
3. `delay = min( max(Retry-After, backoff exponencial), MaxRetryDelaySeconds )`. El `Retry-After`
   se lee de `ex.GetRawResponse().Headers` (segundos; si viene como fecha HTTP se ignora y se usa
   backoff).
4. Alcanzado `CircuitBreakerFailureThreshold` fallos consecutivos → el circuito abre por
   `CircuitBreakerOpenSeconds` (cooldown), y se resetea al primer éxito posterior.

Estado de circuito: `ConcurrentDictionary<string, CircuitState>` keyed por `circuitKey`. Los
providers pasan `"{endpoint}|{deployment}"`, de modo que clasificación y prompts que apuntan al
**mismo recurso Azure OpenAI comparten circuito** (misma bolsa de cuota). `CircuitState` idéntico
al de CU: `{ ConsecutiveFailures, OpenUntilUtc, SyncRoot }`.

Se inyecta como **singleton** (mantiene el estado de circuito) en `Program.cs`.

> **Nota SDK**: el SDK es `Azure.AI.OpenAI` v2 (`OpenAI.Chat`, basado en System.ClientModel),
> por lo que se captura `ClientResultException` (no `RequestFailedException` como CU). Las firmas
> exactas (`ClientResultException.Status`, `GetRawResponse().Headers`, `ClientRetryPolicy`) se
> verifican con la skill `microsoft-code-reference` durante la implementación.

### 3.2 Integración en los providers

- **Desactivar el retry del SDK** para controlarlo nosotros: al crear el `AzureOpenAIClient` en
  [`GptClasificarDataProvider.CreateChatClient`](../../../src/backend/DocumentIA.Functions/Services/GptClasificarDataProvider.cs)
  y en [`OpenAIPromptDataProvider.GetOrCreateClient`](../../../src/backend/DocumentIA.Functions/Services/OpenAIPromptDataProvider.cs),
  pasar `AzureOpenAIClientOptions { RetryPolicy = new ClientRetryPolicy(maxRetries: 0) }`.
- **Envolver las llamadas**:
  - Clasificación — `CompleteChatAsync`:
    `await _resilience.ExecuteAsync(circuitKey, ct => chatClient.CompleteChatAsync(msgs, opts, ct), cts.Token)`.
  - Prompts — las dos llamadas a `chatClient.CompleteChatAsync` (resultado y JSON): igual.
- `circuitKey = "{endpoint}|{deployment}"` en ambos.

### 3.3 Propagación del estado retriable (sutileza Durable)

Si `RateLimitExhaustedException` se propagara desde la actividad, Durable la marshalla como
`TaskFailedException` en el orquestador (se pierde el tipo). Para evitar detección frágil por
mensaje, se usa una **señal por dato, determinista**:

1. El executor lanza `RateLimitExhaustedException` al agotar reintentos / con circuito abierto.
2. `GptClasificarDataProvider` la propaga hasta
   [`ClasificarActivity`](../../../src/backend/DocumentIA.Functions/Activities/ClasificarActivity.cs),
   que **la captura** y devuelve un `ResultadoClasificacion` limpio con un flag nuevo:

   ```csharp
   catch (RateLimitExhaustedException)
   {
       return new ResultadoClasificacion {
           RateLimitExcedido = true,          // campo nuevo en el modelo
           FallbackRazon = "rate_limit_exhausted",
           TipologiaDetectada = "Desconocido",
           Confianza = 0
       };
   }
   ```

3. En el orquestador, justo tras la llamada a la actividad, un short-circuit **antes** del flujo
   normal (mismo patrón que la rama "no tipología"):

   ```csharp
   if (resultadoClasificacion.RateLimitExcedido)
   {
       salida.Resultado.Estado = "PENDIENTE_REINTENTO";
       salida.Resultado.MensajeError =
           "Clasificación pospuesta: cuota de Azure OpenAI agotada (429). Reintentar más tarde.";
       salida.Resultado.EstadoCalidad = "ERROR";
       salida.DetalleEjecucion.Clasificacion = resultadoClasificacion;
       FinalizarSeguimiento("PendienteReintento", ...);
       return salida;   // salida limpia, sin stacktrace 429
   }
   ```

Cambios de modelo: añadir `bool RateLimitExcedido` a `ResultadoClasificacion`. Literal de estado
propuesto: `"PENDIENTE_REINTENTO"` (convive con el ya existente `"NO_CLASIFICADO"`).

> **Validación obligatoria en el plan** (no asumir): confirmar que `Estado="PENDIENTE_REINTENTO"`
> se persiste bien en `DocumentoEjecucion.EstadoFinal` y que el frontend/consumidores lo renderizan
> sin romper (hoy esperan OK/REVISION/ERROR/NO_CLASIFICADO). Si algún consumidor valida contra un
> set cerrado, se amplía ahí.

### 3.4 Prompts (no bloqueante)

`OpenAIPromptDataProvider` ya captura excepción y devuelve `PromptResultado.Error`. Decisión: el
prompt **no** escala el documento a `PENDIENTE_REINTENTO` (es enriquecimiento, no crítico). Se
beneficia del retry+cooldown del executor; al agotarse devuelve su `Error` como hoy pero con marca
distinguible (prefijo `rate_limit_exhausted:`) + telemetría.

## 4. Configuración

`AzureOpenAIResilienceOptions` en `appsettings.json`, sección `"AzureOpenAIResilience"`:

```json
{
  "EnableCircuitBreaker": true,
  "CircuitBreakerFailureThreshold": 5,
  "CircuitBreakerOpenSeconds": 45,
  "MaxRetries": 3,
  "InitialRetryDelayMs": 500,
  "MaxRetryDelaySeconds": 60
}
```

Rollback instantáneo: `MaxRetries: 0` + `EnableCircuitBreaker: false` deja el comportamiento actual.

## 5. Telemetría

Vía `TelemetryClient` (como CU): eventos `AOAI.RateLimitRetry` (circuitKey, intento, delayMs,
statusCode), `AOAI.CircuitOpen` / `AOAI.CircuitClosed` / `AOAI.CircuitRejected`. Habilita alertas
KQL sobre cuota.

## 6. Testing

Espejo de `AzureContentUnderstandingProviderCircuitBreakerTests`:

- **Executor (unit)**: 429 con `Retry-After` → respeta el delay; N fallos → abre circuito;
  circuito abierto → fail-fast; éxito tras reintento → cierra circuito; no-reintentable (400) →
  relanza sin tocar circuito.
- **`ClasificarActivity` (unit)**: `RateLimitExhaustedException` → `ResultadoClasificacion.RateLimitExcedido=true` (sin throw).
- **Orquestador (unit/rama)**: `RateLimitExcedido` → `Estado="PENDIENTE_REINTENTO"`, salida limpia.
- **Regresión**: los tests existentes de clasificación GPT y de prompts siguen verdes.

## 7. Fuera de alcance

- Failover a modelo alterno (`UseAsFallback` / `GetFallbackModel()`).
- Retry de Durable Functions sobre `ClasificarActivity`.
- Extensión a `GptFallbackExtraerDataProvider` (el componente queda listo para aplicarlo después).

## 8. Trazabilidad

- Feature ADO: `AB#99893` — "Resiliencia 429 (rate limit) en clasificación GPT y prompts".
- Tasks: `AB#99894` (opciones config), `AB#99895` (executor), `AB#99896` (integración clasificación GPT),
  `AB#99897` (flag + ClasificarActivity), `AB#99898` (short-circuit orquestador),
  `AB#99899` (persistencia/UI del estado), `AB#99900` (integración prompts).
- Cada commit referencia el `AB#` de su task correspondiente.
