# Plan de Implementación: CU Limiter, Backoff Adaptativo y Telemetría Granular

**Fecha:** 2026-05-28  
**Rama de implementación:** `feature/99671-cu-limiter-backoff-telemetry`  
**Estado:** Implementado localmente y verificado  
**ADO Work Items:** Feature [#99671](https://sareb.visualstudio.com/baf56a97-5b96-43d7-952c-86d74d2a4af1/_workitems/edit/99671) → PBI [#99672](https://sareb.visualstudio.com/baf56a97-5b96-43d7-952c-86d74d2a4af1/_workitems/edit/99672), [#99673](https://sareb.visualstudio.com/baf56a97-5b96-43d7-952c-86d74d2a4af1/_workitems/edit/99673), [#99674](https://sareb.visualstudio.com/baf56a97-5b96-43d7-952c-86d74d2a4af1/_workitems/edit/99674)  
**Análisis base:** [18_ANALISIS_RENDIMIENTO_CONTENT_UNDERSTANDING_2026-05-28.md](../18_ANALISIS_RENDIMIENTO_CONTENT_UNDERSTANDING_2026-05-28.md)

> Estado documental: este documento describe el plan original y se conserva como histórico. La referencia operativa vigente está en `docs/plans/20_ANALISIS_CRISIS_CU_20260529_Y_PLAN_RESILIENCIA.md` y en manuales de operación/despliegue.

> Valores vigentes tras implementación y endurecimiento posterior: `maxConcurrentActivityFunctions=4`, `maxConcurrentOrchestratorFunctions=4`, `Extraction:AzureContentUnderstanding:MaxConcurrentCalls=4`, `HardTimeoutSeconds=90`, `EnableCircuitBreaker=true`, `CircuitBreakerFailureThreshold=5`, `CircuitBreakerOpenSeconds=45`.

---

## 1. Problema raíz

Con colas de 10 documentos y `maxConcurrentActivityFunctions=10` en `host.json`, se lanzan hasta 10 llamadas simultáneas a `ContentUnderstandingClient.AnalyzeBinary`. El servicio responde con degradación progresiva de latencia (p95 ~248 s, max ~538 s) y errores `InternalServerError` sin 429 explícito.

Agravante: el SDK de Azure Core aplica por defecto **3 reintentos exponenciales silenciosos** en el cliente CU porque `CreateClient` no pasa `ContentUnderstandingClientOptions`. Sin corregir esto, añadir un bucle de retry propio podría generar hasta **3 × 3 = 9 llamadas HTTP reales** por intento.

---

## 2. Contexto técnico verificado

| Aspecto | Estado actual |
|---|---|
| `AzureContentUnderstandingProvider` en DI | `AddSingleton` → campo `SemaphoreSlim` seguro como instancia |
| Try/catch alrededor de `AnalyzeBinaryAsync` | No existe; excepciones suben sin filtro |
| Telemetría de tiempos | Solo `TiemposMs["analysis"]` (cubre todo: binary + espera + parseo) |
| Referencia de retry manual | `ResilientGdcService.cs` (MaxRetries=3, InitialDelayMs=200, backoff exponencial + circuit breaker) |
| `TelemetryClient` en DI | Sí, disponible (usado en `PersistirActivity.cs`) |
| Patrón configuración | `IOptions<T>` + `services.Configure<T>(config.GetSection(...))` en `Program.cs` |

---

## 3. Archivos afectados

| Archivo | Cambio |
|---|---|
| `src/backend/DocumentIA.Core/Configuration/AzureContentUnderstandingOptions.cs` | **NUEVO** — clase de opciones |
| `src/backend/DocumentIA.Functions/Program.cs` | Registrar `IOptions<AzureContentUnderstandingOptions>` |
| `src/backend/DocumentIA.Functions/Services/AzureContentUnderstandingProvider.cs` | Cambios principales (limiter, retry, telemetría) |
| `src/backend/DocumentIA.Functions/local.settings.template.json` | Añadir 3 claves nuevas |

---

## 3.1 Estado de implementación

| PBI | Estado técnico | Nota |
|---|---|---|
| #99672 Limiter de concurrencia | Implementado | `SemaphoreSlim` singleton configurable por `MaxConcurrentCalls` |
| #99674 Backoff exponencial + jitter | Implementado | Retry del SDK desactivado con `ContentUnderstandingClientOptions.Retry.MaxRetries = 0`; `RequestContent` se recrea por intento |
| #99673 Telemetría granular | Implementado | `TiemposMs` y App Insights emiten `prepare`, `limiterWaitMs`, `analysis`, `parse`, `attempts` |

La compilación de `DocumentIA.Functions` finalizó correctamente. La suite backend `dotnet test .\src\backend\DocumentIA.sln` finalizó con **678 tests**: **668 correctos**, **0 fallidos**, **10 omitidos** por requerir frontend E2E activo. Persisten únicamente advertencias ya existentes de obsolescencia en configuración GDC y una advertencia nullable previa en tests.

Validación adicional de configuración:

- `local.settings.json` vigente contiene `Extraction:AzureContentUnderstanding:MaxConcurrentCalls=4`, `Extraction:AzureContentUnderstanding:HardTimeoutSeconds=90`, `Extraction:AzureContentUnderstanding:EnableCircuitBreaker=true`, `Extraction:AzureContentUnderstanding:CircuitBreakerFailureThreshold=5`, `Extraction:AzureContentUnderstanding:CircuitBreakerOpenSeconds=45`, `Extraction:AzureContentUnderstanding:MaxRetries=3` y `Extraction:AzureContentUnderstanding:InitialRetryDelayMs=500`.
- `local.settings.template.json` contiene las claves equivalentes con doble guion bajo para entornos basados en variables.
- `azure-pipelines.yml` aplica las tres App Settings en producción y las incluye en la lista de settings obligatorias tras el despliegue.
- `azure-pipelines-functions.yml` incluye un paso `AzureCLI@2` posterior al ZIP deploy para aplicar las mismas tres App Settings si se usa ese pipeline alternativo.

---

## 4. Fases de implementación

> Importante: las subsecciones y snippets de esta sección describen la propuesta original de 2026-05-28. Pueden contener valores de partida (`2`, `3` o `10`) que ya no representan la configuración vigente.

### Fase 1 — Limiter (SemaphoreSlim) · PBI #99672

**Objetivo:** limitar a N llamadas simultáneas a CU, configurable por app settings.

**Pasos:**

1. Crear `AzureContentUnderstandingOptions.cs`:
   ```csharp
   public class AzureContentUnderstandingOptions
   {
       public int MaxConcurrentCalls    { get; set; } = 2;
       public int MaxRetries            { get; set; } = 3;
       public int InitialRetryDelayMs   { get; set; } = 500;
   }
   ```

2. Registrar en `Program.cs`:
   ```csharp
   services.Configure<AzureContentUnderstandingOptions>(
       context.Configuration.GetSection("Extraction__AzureContentUnderstanding"));
   ```

3. Inyectar en el constructor del provider:
   ```csharp
   public AzureContentUnderstandingProvider(
       ...,
       IOptions<AzureContentUnderstandingOptions> cuOptions,
       TelemetryClient telemetryClient)
   ```

4. Añadir campo inicializado en constructor:
   ```csharp
   private readonly SemaphoreSlim _cuLimiter;
   // En constructor:
   _cuLimiter = new SemaphoreSlim(cuOptions.Value.MaxConcurrentCalls, cuOptions.Value.MaxConcurrentCalls);
   ```

5. Envolver llamada:
   ```csharp
   await _cuLimiter.WaitAsync(ct);
   try
   {
       // bucle retry + AnalyzeBinaryAsync
   }
   finally
   {
       _cuLimiter.Release();
   }
   ```

6. Añadir al `local.settings.template.json`:
   ```json
   "Extraction__AzureContentUnderstanding__MaxConcurrentCalls": "2",
   "Extraction__AzureContentUnderstanding__MaxRetries": "3",
   "Extraction__AzureContentUnderstanding__InitialRetryDelayMs": "500"
   ```

---

### Fase 2 — Retry/Backoff exponencial + jitter · PBI #99674

**Objetivo:** reintentar ante errores transitorios desactivando primero el retry del SDK.

**Paso crítico previo — deshabilitar retry del SDK:**

`CreateClient` actualmente no pasa `ContentUnderstandingClientOptions`, lo que activa los 3 reintentos por defecto de Azure.Core. Debe convertirse en método no-estático:

```csharp
// ANTES (static, sin opciones)
private static ContentUnderstandingClient CreateClient(...) { ... }

// DESPUÉS (no-static, Retry.MaxRetries=0)
private ContentUnderstandingClient CreateClient(...)
{
    var clientOpts = new ContentUnderstandingClientOptions();
    clientOpts.Retry.MaxRetries = 0;
    return new ContentUnderstandingClient(new Uri(endpoint), credential, clientOpts);
}
```

**Helpers a añadir:**

```csharp
private static bool IsTransientCuError(Exception ex) =>
    ex is RequestFailedException rfe &&
    (rfe.Status is 429 or 500 or 502 or 503 or 504
     || rfe.Message.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase));

private TimeSpan ComputeRetryDelay(Exception ex, int attempt)
{
    if (ex is RequestFailedException rfe &&
        rfe.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var ra) == true &&
        int.TryParse(ra, out var seconds))
        return TimeSpan.FromSeconds(seconds);

    var baseMs = _options.InitialRetryDelayMs * Math.Pow(2, attempt - 1);
    var jitter  = baseMs * (0.8 + Random.Shared.NextDouble() * 0.4); // ±20%
    return TimeSpan.FromMilliseconds(jitter);
}
```

**Bucle retry dentro del guard del limiter:**

```csharp
Exception lastEx = null;
for (int attempt = 1; attempt <= _options.MaxRetries; attempt++)
{
    try
    {
        // ... AnalyzeBinaryAsync ...
        break; // éxito
    }
    catch (Exception ex) when (IsTransientCuError(ex) && attempt < _options.MaxRetries)
    {
        lastEx = ex;
        await Task.Delay(ComputeRetryDelay(ex, attempt), ct);
    }
}
if (lastEx != null) throw lastEx; // último intento fallido
```

---

### Fase 3 — Telemetría granular · PBI #99673

**Objetivo:** visibilidad por sub-fase en Application Insights.

**Stopwatch único → 4 mediciones:**

| Clave `TiemposMs` | Métrica App Insights | Qué mide |
|---|---|---|
| `"prepare"` | `CU.PrepareMs` | Descarga blob + carga config modelo |
| `"limiterWaitMs"` | `CU.LimiterWaitMs` | Espera por el SemaphoreSlim |
| `"analysis"` | `CU.AnalysisMs` | Duración real de `AnalyzeBinaryAsync` |
| `"parse"` | `CU.ParseMs` | Mapeo JSON → campos (ResultMapper) |

**Emisión de telemetría:**

```csharp
_telemetryClient.TrackMetric("CU.LimiterWaitMs", limiterWaitMs, new Dictionary<string, string> { ["Tipologia"] = tipologia });
_telemetryClient.TrackMetric("CU.AnalysisMs",    analysisMs,    new Dictionary<string, string> { ["Tipologia"] = tipologia });
_telemetryClient.TrackMetric("CU.ParseMs",       parseMs,       new Dictionary<string, string> { ["Tipologia"] = tipologia });
_telemetryClient.TrackMetric("CU.Attempts",      attempts,      new Dictionary<string, string> { ["Tipologia"] = tipologia });

// En cada catch de error transitorio:
_telemetryClient.TrackEvent("CU.TransientError", new Dictionary<string, string>
{
    ["attempt"]    = attempt.ToString(),
    ["statusCode"] = (ex as RequestFailedException)?.Status.ToString() ?? "unknown",
    ["tipologia"]  = tipologia
});
```

---

## 5. Valores de configuración recomendados

| Setting | Desarrollo local | Producción inicial |
|---|---|---|
| `MaxConcurrentCalls` | `2` | `2` (ajustar según métricas) |
| `MaxRetries` | `3` | `3` |
| `InitialRetryDelayMs` | `500` | `500` |

> Con `MaxConcurrentCalls=2` y colas de 10 actividades, el resto esperarán en el SemaphoreSlim. Ajustar en producción según `CU.LimiterWaitMs` vs `CU.AnalysisMs` en App Insights.

---

## 6. Fuera de alcance

- No se modifica el orquestador Durable ni `host.json` (`maxConcurrentActivityFunctions`).
- No se implementa `WaitUntil.Started` + polling manual (mayor complejidad, fuera de scope).
- `models.json` no recibe nuevos campos; la configuración va en app settings / `IOptions`.

---

## 7. Criterios de verificación

1. `dotnet build .\src\backend\DocumentIA.Functions\DocumentIA.Functions.csproj` sin errores.
2. `dotnet test .\src\backend\DocumentIA.sln` sin fallos (668 correctos, 10 omitidos por E2E frontend).
3. Ejecutar lote local de 10 docs; verificar en logs: `LimiterWaitMs`, `attempts`, sub-tiempos por doc.
4. En producción: filtrar en App Insights `customMetrics` por `CU.LimiterWaitMs`, `CU.Attempts`, `CU.AnalysisMs`.
5. Comparar p95 de `analysis` antes/después con misma carga de 150 notas simples.
