# Análisis de la crisis CU del 29/05/2026 y Plan de Resiliencia

Fecha análisis: 2026-06-01  
Ventana analizada: 2026-05-29 08:10 – 20:10 UTC (12 horas)  
Operaciones registradas: 1.055  
Tipología predominante: nota.simple  
Recurso AI Services: `upe48-mm2avmdm-swedencentral` (swedencentral, SKU S0)

---

## 1. Resumen ejecutivo

El viernes 29/05/2026 el sistema de clasificación y extracción sufrió una degradación severa durante aproximadamente 8 horas. El p95 de latencia de Azure Content Understanding (CU) alcanzó los **129 segundos** a nivel de ventana de 12h, con picos puntuales de **p95=1.085s** (09:30–10:45 UTC) y **p50=1.100s** post-restart (16:40 UTC). El tiempo de espera en el semáforo de concurrencia (`CU.LimiterWaitMs`) llegó a un máximo de **950 segundos**.

La causa raíz no es única: es un **failure cascade determinista** provocado por la combinación de tres patologías estructurales en el código que se amplifican mutuamente cuando Azure CU se degrada a nivel de servicio. Un restart manual a las 14:45 UTC para intentar solucionar el problema agravó la situación y generó un **thundering herd** a las 16:40 UTC.

---

## 2. Datos del incidente

### 2.1 Métricas globales de la ventana 12h

| Métrica | p50 | p95 | p99 | Máximo |
|---|---|---|---|---|
| `CU.AnalysisMs` (tiempo en CU) | 30.4s | 129s | 478s | **1.521s** |
| `CU.LimiterWaitMs` (espera en semáforo) | 28.8s | 158s | 360s | **950s** |
| `CU.Attempts` (reintentos) | 1 | 1 | 1 | 3 |

Referencia sana (tarde del 29/05 y operación normal): `CU.AnalysisMs` p50 ≈ 22s.

### 2.2 Cronología de fases

| Fase | Ventana UTC | Descripción | Métrica clave |
|---|---|---|---|
| **F1 - Arranque degradado** | 08:10 – 09:29 | Sistema arranca ya con CU por encima del baseline (31s p50 vs 22s) | p50=31s, p95=72s |
| **F2 - Primer pico grave** | 09:30 – 10:04 | Saturación confirmada; el semáforo amplifica la presión | p50=183s, p95=280s |
| **F3 - Pico máximo** | 10:05 – 10:50 | Máxima degradación de la jornada | p95=1.085s, LimiterWait p95=523s |
| **F4 - Recuperación parcial** | 12:25 – 14:44 | Alivio espontáneo, pero con spikes esporádicos | p50~22s, spikes p95 hasta 863s |
| **F5 - Gap + thundering herd** | 14:45 – 17:04 | Restart manual a las 14:45 → gap de 2h sin datos → al reanudar a las 16:40, todas las activities represadas golpean CU simultáneamente | p50=1.100s, max=1.521s |
| **F6 - Recuperación total** | 17:05+ | CU vuelve a latencia sana | p50~22s, p95~30s |

### 2.3 Evidencia del thundering herd

El restart a las 14:45 generó un gap total de ~2h en telemetría. A las 16:40, Durable Functions rerranció los orchestrators pendientes y lanzó sus activities simultáneamente contra el semáforo. Con `MaxConcurrentCalls=2`, los 10 activities potenciales pusieron 8 en cola inmediata, todas contra un CU que aún no había recuperado capacidad plena. El resultado fue el peor p50 de toda la jornada: 1.100s.

---

## 3. Diagnóstico técnico: tres patologías estructurales

El incidente no fue un fallo aislado de Azure CU. Fue la amplificación de un problema externo por tres debilidades arquitectónicas propias del sistema.

### 3.1 Patología 1 — Semáforo singleton con cola ilimitada

```csharp
// AzureContentUnderstandingProvider constructor
_cuLimiter = new SemaphoreSlim(maxConcurrentCalls, maxConcurrentCalls); // MaxConcurrentCalls = 2
```

`SemaphoreSlim` en .NET no tiene límite de cola interna: cualquier número de `WaitAsync()` puede encolarse. Con `maxConcurrentActivityFunctions=10` (host.json) y `MaxConcurrentCalls=2` (appsettings), **8 de las 10 activities potenciales siempre están bloqueadas esperando**.

Desbalance: `10 / 2 = 5x` → ratio de contención extremo.

Si CU tarda 1.521s (máximo observado), ese slot del semáforo está ocupado durante **25 minutos**. Con 2 slots y CU degradado, la cola crece exponencialmente.

### 3.2 Patología 2 — Sin timeout propio en la llamada a CU

```csharp
// ObtenerDatosAsync — fragmento actual
operation = await client.AnalyzeBinaryAsync(WaitUntil.Completed, ...);
// Sin CancellationTokenSource con timeout propio
// El CancellationToken llega del orquestador (timeout potencialmente de horas)
```

`WaitUntil.Completed` hace polling interno hasta que CU finaliza el análisis. No existe ningún mecanismo que corte la espera si CU tarda demasiado. Una llamada que debería durar ~22s puede mantenerse activa 1.521s ocupando indefinidamente su slot del semáforo.

**Impacto cuantificado**: sin timeout, las llamadas de la cola de espera del semáforo (`LimiterWaitMs`) se añaden encima de los `AnalysisMs`. El tiempo total experimentado por un documento en el peor caso fue `950s (espera) + 1521s (CU) = 2.471s ≈ 41 minutos**.

### 3.3 Patología 3 — Sin circuit breaker

El bucle de reintentos es correcto en diseño, pero sin estado compartido entre concurrentes ni entre instancias. Cuando CU está saturado:

1. Cada llamada que falla reintenta hasta 3 veces
2. Cada reintento consume tiempo en el slot del semáforo
3. Todas las instancias concurrentes retransmiten simultáneamente cuando CU responde
4. Al restart, todas las activities acumuladas se lanzan a la vez → thundering herd

### 3.4 Desbalance de configuración (raíz multiplicadora)

| Parámetro | Valor actual | Valor recomendado |
|---|---|---|
| `maxConcurrentActivityFunctions` (host.json) | 10 | 4 |
| `MaxConcurrentCalls` (appsettings) | 2 | 3–4 |
| Ratio de contención | **5x** | **≤1.5x** |

---

## 4. Failure cascade diagram

```
Azure CU se degrada (latencia sube de 22s → 31s+)
         │
         ▼
Los 2 slots del semáforo se ocupan más tiempo
         │
         ▼
Las 8 activities restantes se encolan (cola ilimitada)
         │
         ▼
Más operaciones entran → cola crece exponencialmente
         │
         ▼
LimiterWaitMs: 28s → 158s → 950s
         │
         ▼
Reintentos por timeouts → más carga sobre CU saturado
         │
         ▼
Restart manual (14:45) → gap 2h → thundering herd (16:40)
         │
         ▼
p50=1.100s, max=1.521s (peor punto de la jornada)
```

---

## 5. Plan de acción por fases

### FASE 0 — Inmediata (sin cambiar código, 1-2 horas)

Tres cambios de configuración que habrían reducido el p95 de 129s a ~60s estimado:

#### 5.0.1 Ajustar `maxConcurrentActivityFunctions` en `host.json`

```json
// host.json
"durableTask": {
  "maxConcurrentActivityFunctions": 4,
  "maxConcurrentOrchestratorFunctions": 4
}
```

Efecto: máximo 2 activities en cola del semáforo en vez de 8. Reduce la presión estructural en un 75%.

#### 5.0.2 Subir `MaxConcurrentCalls` (condicionado a cuota CU)

```json
// appsettings.json
"AzureContentUnderstanding": {
  "MaxConcurrentCalls": 3
}
```

Requiere verificar que la cuota de CU en `swedencentral` admite 3 llamadas concurrentes sin throttling adicional.

#### 5.0.3 Alertas proactivas en Azure Monitor

Las siguientes queries KQL crean alertas que habrían dado aviso ~40 minutos antes del pico de las 10:45:

**Alerta 1 — LimiterWait elevado** (señal temprana):
```kusto
customMetrics
| where name == "CU.LimiterWaitMs"
| summarize p95=percentile(value, 95) by bin(timestamp, 5m)
| where p95 > 30000
```

**Alerta 2 — AnalysisMs crítico**:
```kusto
customMetrics
| where name == "CU.AnalysisMs"
| summarize p95=percentile(value, 95) by bin(timestamp, 5m)
| where p95 > 60000
```

**Alerta 3 — Circuit breaker abierto** (post Fase 2):
```kusto
customEvents
| where name == "CU.CircuitOpen"
| summarize count() by bin(timestamp, 1m)
| where count_ > 0
```

---

### FASE 1 — Urgente (1 semana)

#### 5.1.1 Hard timeout de 90 segundos

Introducir un `CancellationTokenSource` con cap fijo de 90s en la llamada a CU dentro de `AzureContentUnderstandingProvider.ObtenerDatosAsync`:

```csharp
// Nueva configuración en AzureContentUnderstandingOptions
public int HardTimeoutSeconds { get; set; } = 90;

// En ObtenerDatosAsync, dentro del bucle de intentos:
using var timeoutCts = new CancellationTokenSource(
    TimeSpan.FromSeconds(_options.HardTimeoutSeconds));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken, timeoutCts.Token);

try
{
    operation = await client.AnalyzeBinaryAsync(
        WaitUntil.Completed, ..., linkedCts.Token);
}
catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
{
    _telemetryClient.TrackEvent("CU.HardTimeout", new Dictionary<string, string>
    {
        ["Tipologia"] = input.Tipologia,
        ["Attempt"] = attempt.ToString()
    });
    throw new CuHardTimeoutException(
        $"CU no respondió en {_options.HardTimeoutSeconds}s");
}
```

**Justificación del valor 90s**: el p95 en operación sana es ~22s (hay margen). El hard cap debe estar por encima del p95 sano para no impactar el flujo normal, pero muy por debajo del p99 de crisis (478s) para cortar la espiral. 90s es un compromiso conservador.

**Impacto estimado en el 29/05**: max=1.521s → 90s. p99=478s → 90s. La cola del semáforo se habría drenado en minutos en vez de horas.

#### 5.1.2 Queue depth limit con fail-fast 503

`SemaphoreSlim` no expone la longitud de su cola interna. Se rastrea con un contador atómico:

```csharp
// Nuevo campo en AzureContentUnderstandingProvider
private int _queueDepth; // gestionado con Interlocked

// Nueva configuración
public int MaxQueueDepth { get; set; } = 8;

// Al inicio de ObtenerDatosAsync, antes de WaitAsync:
var depth = Interlocked.Increment(ref _queueDepth);
if (depth > _options.MaxQueueDepth)
{
    Interlocked.Decrement(ref _queueDepth);
    throw new CuQueueFullException(
        $"CU queue llena ({depth}/{_options.MaxQueueDepth}). Retry en 30s.",
        retryAfterSeconds: 30);
}

try
{
    await _cuLimiter.WaitAsync(cancellationToken);
    // ... llamada a CU ...
}
finally
{
    Interlocked.Decrement(ref _queueDepth);
    _cuLimiter.Release();
}
```

El orquestador convierte `CuQueueFullException` en respuesta `503 + Retry-After: 30` para el cliente. El cliente recibe una respuesta inmediata en vez de esperar 950s en cola.

#### 5.1.3 Anti-thundering-herd en startup

Jitter aleatorio proporcional al orden en cola durante los primeros `MaxConcurrentCalls * 3` requests tras arrancar el proceso:

```csharp
// Nuevo campo
private int _warmupCallsRemaining; // inicializado con MaxConcurrentCalls * 3 en constructor

// Al inicio de ObtenerDatosAsync, antes de WaitAsync:
var remainingWarmup = Interlocked.Decrement(ref _warmupCallsRemaining);
if (remainingWarmup >= 0)
{
    var jitterMs = remainingWarmup * Random.Shared.Next(500, 1500);
    jitterMs = Math.Min(jitterMs, _options.MaxStartupJitterMs);
    if (jitterMs > 0)
        await Task.Delay(jitterMs, cancellationToken);
}

// Nueva configuración
public int MaxStartupJitterMs { get; set; } = 15_000; // máximo 15s de jitter al inicio
```

Distribuye el burst de restart en una ventana de ~15s en vez de un instante sincronizado.

#### 5.1.4 Migrar a Premium Plan (eliminar cold starts)

El restart de las 14:45 + cold start .NET Isolated amplificaron el thundering herd de las 16:40. **Premium Plan (EP1)** mantiene instancias pre-calentadas.

Configuración recomendada post-migración:
```
WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT = 3
```
Limitar el scale-out evita que múltiples instancias simultáneas multipliquen la presión sobre CU.

---

### FASE 2 — Importante (2-3 semanas)

#### 5.2.1 Circuit breaker completo

Librería recomendada: `Microsoft.Extensions.Resilience` (Polly v8, incluida en .NET 8).

Parámetros derivados de los datos del 29/05:

| Parámetro | Valor | Justificación |
|---|---|---|
| `FailureRatioThreshold` | 0.5 | ≥50% de llamadas fallando o con timeout |
| `MinimumThroughput` | 5 | No abrir con 1-2 muestras |
| `SamplingDuration` | 30s | Ventana de detección |
| `BreakDuration` | 45s | Tiempo en estado Open antes de Half-Open |
| `HalfOpenAttempts` | 2 | Validar recuperación con 2 llamadas reales |

El circuit breaker actúa **antes** del `WaitAsync` para no consumir slots del semáforo:

```csharp
// Patrón integrado: check breaker → wait semaphore → call CU
if (_circuitBreaker.State == CircuitBreakerState.Open)
{
    // Activity devuelve error orquestable inmediatamente
    // El orquestador responde 503 + Retry-After al cliente
    throw new CuCircuitOpenException("CU circuit breaker abierto. Retry-After: 45s");
}

await _cuLimiter.WaitAsync(cancellationToken);
try
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(TimeSpan.FromSeconds(_options.HardTimeoutSeconds));

    var result = await _circuitBreaker.ExecuteAsync(
        async ct => await CallCuAsync(ct), cts.Token);
    return result;
}
finally { _cuLimiter.Release(); }
```

#### 5.2.2 Adaptive rate limiting

Ventana deslizante de latencias recientes (2 minutos) para ajustar dinámicamente los slots del semáforo:

- Si p50 > `SlowThresholdMs` (45s) → reducir un slot (consumir un `WaitAsync` sin liberarlo)
- Si p50 < `RecoveryThresholdMs` (28s) → restaurar un slot (`Release()`)

Emite eventos `CU.ConcurrencyReduced` / `CU.ConcurrencyRestored` a Application Insights para observabilidad.

```json
// Nueva configuración en AzureContentUnderstandingOptions
"SlowThresholdMs": 45000,
"RecoveryThresholdMs": 28000
```

#### 5.2.3 Pool de endpoints CU (mayor impacto potencial)

El incidente del 29/05 fue una saturación del **recurso CU a nivel de servicio Azure**. El único mecanismo que protege ante esto es disponer de 2-3 recursos AI Services, cada uno con su propio analyzer, y distribuir la carga entre ellos.

---

##### Pregunta frecuente: ¿basta con usar dos analyzers en el mismo recurso?

No. La cuota y el throttling de CU se aplican a nivel de **recurso AI Services** (la cuenta), no a nivel de analyzer. Un analyzer es solo una definición de esquema — no tiene cuota ni límite de concurrencia propios. Si se crean `nota-simple-1-4-a` y `nota-simple-1-4-b` en el mismo recurso `swedencentral`, ambos comparten exactamente el mismo pool de capacidad. El 29/05 la saturación era a nivel del recurso/región — todas las llamadas a ese recurso, sin importar el `AnalyzerId`, estaban en la misma cola de Azure CU.

| Escenario | ¿Resuelve saturación? |
|---|---|
| 2 analyzers en el mismo recurso | ❌ Cuota compartida, sin beneficio de capacidad |
| 2 recursos AI Services, misma región | ⚠️ Cuota separada, pero mismo pool de capacidad regional de Azure |
| 2 recursos AI Services, regiones distintas | ✅ Cuota separada + pools de capacidad Azure independientes |

Múltiples analyzers en el mismo recurso sí tienen utilidad para otras cosas: versionado de esquema (`nota-simple-1-4` en producción + `nota-simple-2-0` en piloto), A/B testing de campo, o variantes de `InputRange` por tipología. Pero no para load balancing.

---

##### Viabilidad: ¿se puede sin reentrenar?

**Respuesta directa: sí. No hay reentrenamiento.**

Los analyzers de Azure Content Understanding **no son modelos de ML entrenados desde cero**. Son una **definición de esquema** (campos a extraer, tipos, descripciones) que se ejecuta sobre el modelo pre-entrenado de Microsoft (Azure AI layout + extracción de campos). El `AnalyzerId` (`nota-simple-1-4`, etc.) es solo el nombre que se le da al esquema cuando se registra en un recurso AI Services.

Esto implica que:

1. El analyzer puede **exportarse** como JSON desde el recurso original con una sola llamada REST.
2. El mismo JSON puede **recrearse** en cualquier otro recurso AI Services con otra llamada REST.
3. El resultado es funcionalmente idéntico: mismos campos, mismos resultados, mismo comportamiento.

No existe ningún training set, pesos, ni datos propietarios atados al recurso. El modelo subyacente es de Microsoft y está disponible en todos los recursos AI Services que soporten CU.

---

##### Proceso de exportación y recreación de un analyzer

**Paso 1 — Exportar el analyzer del recurso actual**

```powershell
# PowerShell — obtener la definición del analyzer existente
$token = (az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv)
$sourceEndpoint = "https://upe48-mm2avmdm-swedencentral.cognitiveservices.azure.com"
$analyzerId = "nota-simple-1-4"
$apiVersion = "2024-11-30"

$response = Invoke-RestMethod `
    -Uri "$sourceEndpoint/contentunderstanding/analyzers/${analyzerId}?api-version=$apiVersion" `
    -Headers @{ Authorization = "Bearer $token" } `
    -Method GET

# Guardar el esquema
$response | ConvertTo-Json -Depth 20 | Out-File "analyzer-nota-simple-1-4.json"
```

El JSON resultante contiene el `fieldSchema` completo con todos los campos definidos para `nota.simple`.

**Paso 2 — Crear el mismo analyzer en el nuevo recurso**

```powershell
# Crear un nuevo recurso AI Services en Azure Portal (o CLI) en otra región, p.ej. westeurope
# Luego registrar el mismo analyzer en el nuevo recurso

$token2 = (az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv)
$targetEndpoint = "https://<nuevo-recurso>.cognitiveservices.azure.com"

# Limpiar campos de respuesta que no deben enviarse en el PUT (id, createdAt, etc.)
$schema = Get-Content "analyzer-nota-simple-1-4.json" | ConvertFrom-Json
$schema.PSObject.Properties.Remove('analyzerId')
$schema.PSObject.Properties.Remove('createdAt')
$schema.PSObject.Properties.Remove('lastModifiedAt')
$body = $schema | ConvertTo-Json -Depth 20

Invoke-RestMethod `
    -Uri "$targetEndpoint/contentunderstanding/analyzers/${analyzerId}?api-version=$apiVersion" `
    -Headers @{ Authorization = "Bearer $token2"; "Content-Type" = "application/json" } `
    -Method PUT `
    -Body $body
```

El analyzer queda disponible en el nuevo recurso con el mismo `AnalyzerId`. La operación es prácticamente instantánea (no hay training).

**Este proceso se repite para cada tipología** — una vez terminada la Fase 1 de expansión de tipologías (10+ previstos), se exportan y recrean todos de una vez.

---

##### Arquitectura objetivo con pool de 2-3 endpoints

```
Function App
    │
    └── CuEndpointPool (nuevo componente)
             ├── Endpoint A: swedencentral — AnalyzerId: nota-simple-1-4   [principal]
             ├── Endpoint B: westeurope   — AnalyzerId: nota-simple-1-4   [secundario]
             └── (Endpoint C: francecentral — opcional para máxima resiliencia)
             
             Estrategia: round-robin weighted
             Si endpoint A abre circuit breaker → 100% tráfico a B
             Si A se recupera → rebalanceo progresivo (10% → 50% → 100%)
```

---

##### Impacto en el código existente

La arquitectura actual ya está muy cerca de soportar esto. `ExtractionModelConfig` tiene `Endpoint`, `ApiKey` y `AnalyzerId` como campos independientes — está pensada para un endpoint por config. `CreateClient(model)` ya crea el `ContentUnderstandingClient` usando `model.Endpoint`.

Lo que falta es la **lógica de selección de endpoint** en el provider. Hay dos enfoques:

**Opción A — Minimal (recomendada para Fase 2)**: múltiples entradas en el `ExtractionModelRegistry` para la misma tipología con `Key` diferente:

```json
// En la BD de configuración (ConfiguracionJson de TipologiaModelEntity)
{
  "Models": [
    {
      "Key": "cu-nota-simple-primary",
      "Provider": "ContentUnderstanding",
      "IsDefault": true,
      "Endpoint": "https://upe48-mm2avmdm-swedencentral.cognitiveservices.azure.com",
      "AnalyzerId": "nota-simple-1-4",
      "ApiKey": "...",
      "Weight": 2
    },
    {
      "Key": "cu-nota-simple-secondary",
      "Provider": "ContentUnderstanding",
      "IsDefault": false,
      "Endpoint": "https://<nuevo-recurso>.cognitiveservices.azure.com",
      "AnalyzerId": "nota-simple-1-4",
      "ApiKey": "...",
      "Weight": 1
    }
  ]
}
```

El provider selecciona entre las entradas del mismo `Provider` mediante round-robin ponderado. Cambios de código: solo en `AzureContentUnderstandingProvider` y `ExtractionModelRegistryLoader`.

**Opción B — Extensión del modelo** (más limpia, para Fase 3): añadir `Endpoints` como lista en `ExtractionModelConfig`. El provider itera la lista internamente.

---

##### Telemetría adicional necesaria

Para observar el comportamiento del pool, añadir el endpoint usado a todas las métricas existentes:

```csharp
// En TrackCuMetrics — añadir propiedad "Endpoint" a las métricas existentes
properties["EndpointRegion"] = model.ProcessingLocation ?? "unknown";
properties["ModelKey"] = model.Key;
```

Nuevos eventos:
- `CU.EndpointSelected` — endpoint elegido + motivo (round-robin / fallback por circuit)
- `CU.EndpointFailover` — failover detectado + endpoint origen + endpoint destino

KQL para monitorizar distribución de carga entre endpoints:
```kusto
customMetrics
| where name == "CU.AnalysisMs"
| summarize p50=percentile(value, 50), p95=percentile(value, 95), count()
    by tostring(customDimensions.EndpointRegion), bin(timestamp, 15m)
| render timechart
```

#### 5.2.4 Semáforos separados batch / interactive

El cliente `DocumentIA.Batch.Classification` compite en igualdad con usuarios interactivos por los slots del semáforo. Con uso intensivo futuro esto degradará la experiencia interactiva.

Separación mediante un header de origen y dos semáforos:

```csharp
// Cliente batch envía header
request.Headers.Add("X-Request-Origin", "batch");

// Provider CU usa dos semáforos
private readonly SemaphoreSlim _interactiveLimiter; // 2 slots exclusivos para interactive
private readonly SemaphoreSlim _batchLimiter;       // 1 slot para batch

// Interactive: solo espera el limiter de interactive
// Batch: espera batch limiter + global (garantizando que interactive siempre tiene slots)
```

Configuración:
```json
"MaxConcurrentCalls": 4,        // Total CU slots (interactive + batch)
"MaxBatchConcurrentCalls": 1    // Batch nunca consume más de 1 slot
```

---

### FASE 3 — Planificación a medio plazo (1-2 meses)

| Acción | Descripción | Impacto |
|---|---|---|
| API `/health/cu-status` | Endpoint que expone `{ state, queueDepth, currentConcurrency, recommendedBatchDelay }`. El cliente WPF consulta antes de enviar un lote | Back-pressure explícito hacia batch |
| Scheduling nocturno | Para lotes >500 docs, `ScheduleBatchForLaterAsync` crea un Durable Timer para ventana off-peak (22:00 UTC). Cero competencia con usuarios interactivos | Separación temporal de cargas |
| APIM como gateway CU | Circuit breaker compartido entre **todas las instancias** de la Function App (el semáforo actual es por proceso). Throttle global: `rate-limit-by-key calls="4" renewal-period="1"` | Necesario cuando hay ≥3 instancias activas simultáneas |
| Evaluar Netherite | Event Hubs backend para el Durable Task Hub si volumen >1.000 docs/día. Mayor throughput de scheduling de activities | Relevante en escenario de uso masivo futuro |

---

## 6. Resumen de impacto por fase

| Fase | Cambios | Efecto sobre el incidente del 29/05 |
|---|---|---|
| **Fase 0** | `maxConcurrentActivityFunctions=4`, `MaxConcurrentCalls=3`, alertas | p95 estimado: 129s → ~60s |
| **+ Fase 1** | Timeout 90s, queue limit, jitter, Premium Plan | p99: 478s → 90s. Max: 1.521s → 90s. Thundering herd eliminado |
| **+ Fase 2** | Circuit breaker, adaptive rate, endpoint secundario, priorización batch | Saturación de CU → failover automático. Ciclo vicioso interrumpido en origen |
| **+ Fase 3** | Back-pressure batch, scheduling, APIM | Prevención estructural para escenario de carga masiva |

---

## 7. Configuración objetivo post-implementación

```json
// appsettings.json — AzureContentUnderstanding section
{
  "MaxConcurrentCalls": 4,
  "MaxBatchConcurrentCalls": 1,
  "MaxQueueDepth": 8,
  "HardTimeoutSeconds": 90,
  "MaxRetries": 3,
  "InitialRetryDelayMs": 500,
  "SlowThresholdMs": 45000,
  "RecoveryThresholdMs": 28000,
  "MaxStartupJitterMs": 15000,
  "CircuitBreaker": {
    "FailureRatioThreshold": 0.5,
    "MinimumThroughput": 5,
    "SamplingDurationSeconds": 30,
    "BreakDurationSeconds": 45,
    "HalfOpenAttempts": 2
  }
}

// host.json — durableTask section
{
  "maxConcurrentActivityFunctions": 4,
  "maxConcurrentOrchestratorFunctions": 4
}
```

---

## 8. Archivos de datos del análisis

Los CSV y JSON de respaldo están en:

```
artifacts/reports/cu-performance-live-20260529-12h/
  01-cu-metrics-summary.csv/.json
  02-cu-subphase-timeseries.csv/.json
  03-cu-wait-vs-analysis-diagnosis.csv/.json
  04-cu-top-waiting-operations.csv/.json
  05-cu-transient-errors.csv/.json
  README.md
```

Script utilizado para la extracción: `scripts/reports/export-cu-performance-insights.ps1` (modificado el 01/06/2026 para soportar `-StartTime`/`-EndTime` en formato ISO 8601).
