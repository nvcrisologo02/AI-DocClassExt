# Análisis de la crisis CU del 29/05/2026 y Plan de Resiliencia

Fecha análisis: 2026-06-01  
Ventana analizada: 2026-05-29 08:10 – 20:10 UTC (12 horas)  
Operaciones registradas: 1.055  
Tipología predominante: nota.simple  
Recurso AI Services: `upe48-mm2avmdm-swedencentral` (swedencentral, SKU S0)  
Modelo de despliegue OpenAI: **GlobalStandard en TPM** (pago por token, sin PTUs)  
Cuotas observadas (captura 2026-06-01): `gpt-4.1` 150K TPM · `gpt-4.1-mini` 250K TPM · `gpt-4o` 250K TPM · `gpt-4o-mini` 150K Standard · `text-embedding-3-large` 150K TPM

Estado operativo vigente (post-implementación):

- `host.json`: `maxConcurrentActivityFunctions=4`, `maxConcurrentOrchestratorFunctions=4`.
- `Extraction:AzureContentUnderstanding`: `MaxConcurrentCalls=4`, `HardTimeoutSeconds=90`, `EnableCircuitBreaker=true`, `CircuitBreakerFailureThreshold=5`, `CircuitBreakerOpenSeconds=45`, `MaxRetries=3`, `InitialRetryDelayMs=500`.
- Selección de modelo CU con `modelKey` y `secondaryModelKey` (round-robin + failover por circuit breaker).

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

Ver sección 9 — implementación realizada el 2026-06-01.

---

## 9. Estrategia multi-analyzer: pool de analyzers mismo recurso

> **Decisión de diseño (2026-06-01):** Como paso previo a añadir un segundo recurso Azure AI Services,
> se implementa primero un pool de analyzers dentro del mismo recurso.
> Si no hay mejora, se escala al multi-recurso (sección 5.2.3).

### 9.1 Enfoque adoptado

El campo `AnalyzerId` de `ExtractionModelConfig` pasa a soportar **múltiples IDs separados por `;`**:

```json
{
  "Key": "nota.simple.1_5.azure-cu",
  "Provider": "azure-content-understanding",
  "AnalyzerId": "CU_NS_1.5_0;CU_NS_1.5_0_b",
  "Endpoint": "https://upe48-mm2avmdm-swedencentral.services.ai.azure.com/",
  ...
}
```

El sistema selecciona el analyzer mediante **round-robin atómico** (`Interlocked.Increment`) en cada
llamada a `ObtenerDatosAsync`. Con N analyzers, la carga se distribuye 1/N por analyzer.

**Hipótesis de mejora:** Azure CU puede manejar múltiples analyzers con colas de procesamiento
independientes internamente. Si hay saturación por analyzer, distribuir entre 2-3 reducirá la
latencia efectiva aunque compartan el mismo recurso/cuota.

**Limitación conocida:** Si la degradación es por límite de capacidad del recurso completo (como
el 29/05), este enfoque no ayudará y habrá que pasar al multi-recurso.

---

### 9.2 Cambios implementados

| Archivo | Cambio |
|---|---|
| `DocumentIA.Core/Configuration/TipologiaValidationConfig.cs` | Propiedad `AnalyzerIds` en `ExtractionModelConfig` — parsea `;` |
| `DocumentIA.Functions/Services/AzureContentUnderstandingProvider.cs` | Campo `_analyzerRoundRobinCounter` + selección en `ObtenerDatosAsync` |
| `DocumentIA.Functions/Services/AzureContentUnderstandingProvider.cs` | Parámetro `analyzerId` en `TrackCuMetrics` → nuevo atributo `AnalyzerId` en métricas App Insights |
| `DocumentIA.Tests.Unit/Configuration/ExtractionModelConfigTests.cs` | 9 tests: parsing de IDs, distribución round-robin con 2 y 3 analyzers |

**Retrocompatibilidad total:** si `AnalyzerId` contiene un solo ID (configuración actual), el
comportamiento es idéntico al anterior — el path rápido usa `model.AnalyzerId` directamente.

---

### 9.3 Plan de aprovisionamiento en Azure

#### Paso 1 — Crear el segundo analyzer en el mismo recurso

```powershell
# Exportar el analyzer actual
$endpoint  = "https://upe48-mm2avmdm-swedencentral.services.ai.azure.com"
$apiKey    = "<ApiKey del recurso>"
$srcId     = "CU_NS_1.5_0"
$dstId     = "CU_NS_1.5_0_b"
$apiVer    = "2025-11-01"

$headers = @{
    "Ocp-Apim-Subscription-Key" = $apiKey
    "Content-Type"              = "application/json"
}

# Exportar esquema del analyzer original
$schema = Invoke-RestMethod `
    -Uri "$endpoint/contentunderstanding/analyzers/${srcId}?api-version=$apiVer" `
    -Headers $headers `
    -Method GET

# Eliminar campos de solo-lectura antes de recrear
$body = $schema | Select-Object -ExcludeProperty analyzerId, createdAt, lastModifiedAt, status `
    | ConvertTo-Json -Depth 20

# Crear el analyzer duplicado con el nuevo ID (mismo esquema, mismo recurso)
Invoke-RestMethod `
    -Uri "$endpoint/contentunderstanding/analyzers/${dstId}?api-version=$apiVer" `
    -Headers $headers `
    -Method PUT `
    -Body $body

Write-Host "Analyzer '$dstId' creado. Verificando..."

# Verificar estado (esperar a que quede en estado 'ready')
do {
    Start-Sleep -Seconds 2
    $status = Invoke-RestMethod `
        -Uri "$endpoint/contentunderstanding/analyzers/${dstId}?api-version=$apiVer" `
        -Headers $headers -Method GET
    Write-Host "  Estado: $($status.status)"
} while ($status.status -eq "creating")

Write-Host "Listo: $($status.status)"
```

#### Paso 2 — Actualizar la configuración en BD

```sql
-- Verificar la fila actual
SELECT Id, [Key], AnalyzerId = JSON_VALUE(ConfiguracionJson, '$.AnalyzerId'), ConfiguracionJson
FROM TipologiaModelEntity
WHERE [Key] = 'nota.simple.1_5.azure-cu' AND Tipo = 'Extraccion' AND Activo = 1;

-- Actualizar el campo AnalyzerId dentro del JSON (SQL Server JSON_MODIFY)
UPDATE TipologiaModelEntity
SET ConfiguracionJson = JSON_MODIFY(ConfiguracionJson, '$.AnalyzerId', 'CU_NS_1.5_0;CU_NS_1.5_0_b')
WHERE [Key] = 'nota.simple.1_5.azure-cu' AND Tipo = 'Extraccion' AND Activo = 1;

-- Confirmar cambio
SELECT JSON_VALUE(ConfiguracionJson, '$.AnalyzerId') AS AnalyzerId
FROM TipologiaModelEntity
WHERE [Key] = 'nota.simple.1_5.azure-cu' AND Tipo = 'Extraccion' AND Activo = 1;
```

#### Paso 3 — Invalidar caché (opcional)

La caché de modelos expira cada 5 minutos. Para efecto inmediato sin reiniciar la Function App,
se puede reiniciar la instancia activa desde Azure Portal → Function App → Restart.

---

### 9.4 Configuración de número de analyzers

El número de analyzers a usar se controla **exclusivamente desde BD** — no requiere cambio de código:

| Caso | Valor de `AnalyzerId` en BD |
|---|---|
| 1 analyzer (actual, sin cambio) | `CU_NS_1.5_0` |
| 2 analyzers | `CU_NS_1.5_0;CU_NS_1.5_0_b` |
| 3 analyzers | `CU_NS_1.5_0;CU_NS_1.5_0_b;CU_NS_1.5_0_c` |
| Rollback a 1 | Eliminar todo después del primer `;` |

**Para añadir un analyzer adicional:** repetir el Paso 1 con un nuevo `$dstId` y añadir el ID
al campo en BD separado por `;`.

---

### 9.5 Plan de pruebas y criterios de eficacia

#### 9.5.1 Prueba de distribución (pre-producción)

Verificar que el round-robin funciona antes de activar en producción:

```powershell
# Consulta App Insights tras 30-50 clasificaciones:
# Verificar que AnalyzerId aparece como dimensión y ambos IDs tienen ~50% de llamadas
# KQL en Application Insights:
# customMetrics
# | where name == "CU.AnalysisMs"
# | extend analyzerId = tostring(customDimensions.AnalyzerId)
# | summarize count(), avg(value), percentile(value, 50), percentile(value, 95)
#     by analyzerId
# | order by count_ desc
```

Criterio de éxito: cada analyzer recibe entre 40%-60% de las llamadas.

#### 9.5.2 Métricas de referencia (baseline)

Extraer p50/p95 de `CU.AnalysisMs` en una ventana sin carga extrema (estado normal):

```powershell
# KQL baseline (últimas 2 horas en estado normal):
# customMetrics
# | where name == "CU.AnalysisMs" and timestamp > ago(2h)
# | summarize p50=percentile(value, 50), p95=percentile(value, 95), n=count()
```

Registrar los valores aquí como baseline:

| Métrica | Baseline (sin pool) | Objetivo con pool |
|---|---|---|
| `CU.AnalysisMs` p50 | _medir_ | ≤ baseline |
| `CU.AnalysisMs` p95 | _medir_ | ≤ baseline × 0.8 |
| `CU.LimiterWaitMs` p95 | _medir_ | ≤ baseline |
| Throughput (docs/hora) | _medir_ | ≥ baseline × 1.2 |

#### 9.5.3 Prueba de carga controlada

Ejecutar un lote de ≥ 50 documentos y comparar:

```powershell
# Lanzar lote de prueba (usar el smoke test existente o API directamente)
# Luego consultar en App Insights:

# customMetrics
# | where name in ("CU.AnalysisMs", "CU.LimiterWaitMs") and timestamp > ago(1h)
# | extend analyzerId = tostring(customDimensions.AnalyzerId)
# | summarize p50=percentile(value, 50), p95=percentile(value, 95), n=count()
#     by name, analyzerId
# | order by name, analyzerId
```

#### 9.5.4 Criterio de decisión Go/No-Go

| Resultado | Decisión |
|---|---|
| p95 `CU.AnalysisMs` baja ≥ 20% respecto al baseline | ✅ Estrategia válida — mantener y escalar a 3 analyzers |
| Sin mejora apreciable (< 5%) bajo carga normal | ⚠️ Neutral — mantener por si acaso, preparar multi-recurso |
| Sin mejora durante un episodio de degradación | ❌ Confirma que el problema es a nivel de recurso — activar Plan B: multi-recurso (sección 5.2.3) |

---

### 9.7 Resultados del experimento multi-analyzer (2026-06-01) — CONCLUIDO

> **Veredicto: hipótesis refutada. Experimento revertido.**

#### 9.7.1 Configuración del experimento

| Parámetro | Valor |
|---|---|
| Analyzers | `CU_NS_1.5_0` + `CU_NS_1.5_0b` (mismo recurso, misma región) |
| Documentos | 40 |
| Concurrencia | `MaxConcurrentCalls=4`, 4 colas Service Bus |
| Ventana | 08:50:10 – 08:56:14 UTC |
| Duración total | **6 min 4 seg** |

#### 9.7.2 Distribución del round-robin

| Analyzer | Llamadas | % |
|---|---|---|
| `CU_NS_1.5_0` | 20 | 50% |
| `CU_NS_1.5_0b` | 20 | 50% |

Reparto perfectamente equilibrado. El mecanismo `Interlocked.Increment` funciona correctamente.

#### 9.7.3 Latencias por analyzer

| Métrica | `CU_NS_1.5_0` | `CU_NS_1.5_0b` | Diferencia |
|---|---|---|---|
| `CU.AnalysisMs` p50 | 26.2s | 28.5s | +9% |
| `CU.AnalysisMs` p95 | 63.2s | 69.1s | +9% |
| `CU.AnalysisMs` avg | 34.5s | 34.7s | < 1% |
| `CU.LimiterWaitMs` p95 | **0ms** | **0ms** | — |
| `CU.Attempts` | 1 (todos) | 1 (todos) | — |

Las latencias de ambos analyzers son **estadísticamente idénticas** (diferencia < 10%, dentro del ruido del servicio). Ninguno tiene ventaja sobre el otro porque comparten el mismo pool de inferencia del recurso.

#### 9.7.4 Throughput

| Métrica | Valor |
|---|---|
| Docs/min | **~6.6** |
| p50 E2E (DocumentIA.Duracion.Total) | 29.2s |
| p95 E2E | 69.9s |

#### 9.7.5 Implicaciones del modelo de despliegue TPM (GlobalStandard)

La captura del portal Azure (2026-06-01) confirma que los despliegues OpenAI del recurso son **GlobalStandard en TPM**, no PTU. Esto tiene implicaciones directas para entender la saturación del 29/05 y las opciones de mejora:

**GlobalStandard + TPM**:
- El límite no es de PTUs reservados sino de **tokens por minuto (TPM)**. El recurso comparte la capacidad regional de Azure con otros tenants.
- El throttling se produce cuando el pool global de la región (`swedencentral`) está bajo presión — independiente de la carga propia.
- No se puede "comprar más capacidad" aumentando PTUs porque no hay PTUs en este modelo. Para más throughput garantizado habría que migrar a **Provisioned Throughput (PTU)**.
- Los episodios de saturación como el del 29/05 son consecuencia directa de compartir capacidad regional con otros clientes de Azure — es inherente al modelo GlobalStandard.

**¿Ayudan múltiples analyzers en el mismo recurso?**
No. Confirmado experimentalmente: mismas latencias en ambos analyzers bajo carga normal. El cuello de botella es el pool de capacidad regional compartido del recurso AI Services, no el analyzer concreto.

**¿Ayuda un segundo recurso en la misma región?**
Potencialmente, pero no garantizado: dos recursos en `swedencentral` siguen compartiendo el mismo pool de capacidad regional de Azure. En un episodio de saturación regional (como el 29/05), ambos recursos se degradarían igual.

**¿Qué sí ayuda?**

| Opción | Efectividad | Coste/Complejidad |
|---|---|---|
| Segundo recurso en **región diferente** (westeurope, francecentral) | ✅ Alta — pools independientes | Media |
| Migrar a **PTU (Provisioned Throughput)** | ✅ Alta — capacidad reservada, sin compartir | Alta (coste fijo) |
| Circuit breaker + hard timeout (Fase 1) | ✅ Media — no evita degradación, sí limita el daño | Baja |
| Múltiples analyzers mismo recurso | ❌ Ninguna — comparten pool | N/A |

**Nota sobre PTU**: con PTU no se paga por token sino por PTU/hora (coste fijo reservado). La capacidad está garantizada — no hay throttling por saturación regional. Para el volumen actual (~40 docs en 6 min = ~400 docs/hora en punta) habría que dimensionar los PTUs necesarios con la herramienta de sizing de Azure Foundry.

#### 9.7.6 Estado del experimento

- **Código revertido** (2026-06-01): `TipologiaValidationConfig.cs`, `AzureContentUnderstandingProvider.cs`, `ExtractionModelConfigTests.cs` eliminados.
- **BD restaurada**: `AnalyzerId` vuelto a valor único `CU_NS_1.5_0`.
- **Analyzer `CU_NS_1.5_0b`**: puede eliminarse del recurso o mantenerse para pruebas futuras.
- **Próximo paso recomendado**: implementar Fase 1 (hard timeout + queue depth limit) como protección inmediata ante la próxima degradación regional.

---

### 9.6 Plan B: multi-recurso (activación condicional)

Si el pool de analyzers no muestra mejora durante un episodio real:

1. Crear nuevo recurso Azure AI Services en diferente región (westeurope recomendado por latencia).
2. Exportar y recrear los analyzers en el nuevo recurso (scripts de sección 5.2.3).
3. Añadir segunda fila en `TipologiaModelEntity` con el nuevo `Endpoint` + `ApiKey`.
4. Implementar `GetModelsForProvider()` en `ExtractionModelRegistryLoader` + selección por key
   en el provider (ya documentado en sección 5.2.3, Opción A).

El cambio de enfoque (mismo recurso → multi-recurso) requiere ~3h de trabajo incluyendo
el aprovisionamiento Azure.

---

_Sección añadida: 2026-06-01 — Implementación activa en rama de trabajo._

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
| **Fase 0** | `maxConcurrentActivityFunctions=4`, `MaxConcurrentCalls=4`, alertas | p95 estimado: 129s → ~60s |
| **+ Fase 1** | Timeout 90s, queue limit, jitter, Premium Plan | p99: 478s → 90s. Max: 1.521s → 90s. Thundering herd eliminado |
| **+ Fase 2** | Circuit breaker, adaptive rate, endpoint secundario, priorización batch | Saturación de CU → failover automático. Ciclo vicioso interrumpido en origen |
| **+ Fase 3** | Back-pressure batch, scheduling, APIM | Prevención estructural para escenario de carga masiva |

---

## 7. Configuración efectiva post-implementación

```json
// appsettings.json — AzureContentUnderstanding section
{
  "MaxConcurrentCalls": 4,
  "HardTimeoutSeconds": 90,
  "EnableCircuitBreaker": true,
  "CircuitBreakerFailureThreshold": 5,
  "CircuitBreakerOpenSeconds": 45,
  "MaxRetries": 3,
  "InitialRetryDelayMs": 500
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

---

## 21. Actualizacion 2026-06-01: replica multi-region y balanceo CU

### 21.1 Cambios ejecutados

Durante la sesion del 01/06/2026 se completo la estrategia de resiliencia basada en **replica de analyzer en segunda region** y **balanceo de carga por model key**:

1. Se replico el analyzer `CU_NS_1.5_0` desde `swedencentral` a `westeurope`.
2. Se dio de alta en BD el modelo secundario `nota.simple.1_5.azure-cu-we` (activo) apuntando al endpoint de West Europe.
3. Se amplio la config de tipologia con `extraction.secondaryModelKey`.
4. Se implemento round-robin en `AzureContentUnderstandingProvider`:
   - slot 0: `extraction.modelKey` (principal)
   - slot 1: `extraction.secondaryModelKey` (secundario)
5. Se anadio trazabilidad en telemetria App Insights con dimension `ModelKey` en `CU.AnalysisMs`.
6. Se creo tipologia de prueba `nota.simple_bal` para validar reparto sin tocar `nota.simple` productiva.

### 21.2 Verificacion de resultado

En la ejecucion de validacion finalizada el 01/06/2026:

- Total llamadas `CU.AnalysisMs` para `nota.simple_bal`: **50**
- Reparto por `ModelKey`:
  - `nota.simple.1_5.azure-cu` (Sweden): **25**
  - `nota.simple.1_5.azure-cu-we` (West Europe): **25**
- Resultado: **50/50 exacto**
- Estado de ejecuciones persistidas en `DocumentoEjecuciones`: **50 OK / 0 NoOK**

Nota importante: la secuencia temporal no siempre alterna visualmente 1-1 porque las llamadas finalizan en paralelo y con latencias distintas. El criterio canonico de validacion es el conteo por `ModelKey` en App Insights.

### 21.3 Decision tecnica

Queda **adoptada** la estrategia multi-region por model key para CU en tipologias criticas, al ser la unica que separa pools de capacidad entre regiones Azure.

Queda **descartada** como estrategia principal la multiplicacion de analyzers en el mismo recurso para resiliencia, ya que no separa cuota/capacidad del recurso.

---

## 22. Manual operativo: replicar analyzers CU entre recursos/regiones

Este procedimiento crea una copia funcional del analyzer en otro recurso AI Services sin reentrenamiento.

### 22.1 Prerrequisitos

- `az login` con permisos sobre ambos recursos.
- API version CU valida (ejemplo: `2025-11-01`).
- Endpoint origen y destino accesibles.
- Mismo `AnalyzerId` permitido en destino (o uno alternativo si ya existe).

### 22.2 Paso 1 - Exportar analyzer del recurso origen

```powershell
$token = (az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv)
$sourceEndpoint = "https://upe48-mm2avmdm-swedencentral.services.ai.azure.com"
$analyzerId = "CU_NS_1.5_0"
$apiVersion = "2025-11-01"

$source = Invoke-RestMethod \
  -Uri "$sourceEndpoint/contentunderstanding/analyzers/${analyzerId}?api-version=$apiVersion" \
  -Headers @{ Authorization = "Bearer $token" } \
  -Method GET

$source | ConvertTo-Json -Depth 30 | Set-Content ".\\analyzer-export.json"
```

### 22.3 Paso 2 - Limpiar campos read-only del export

```powershell
$schema = Get-Content ".\\analyzer-export.json" -Raw | ConvertFrom-Json
$null = $schema.PSObject.Properties.Remove("analyzerId")
$null = $schema.PSObject.Properties.Remove("createdAt")
$null = $schema.PSObject.Properties.Remove("lastModifiedAt")
$null = $schema.PSObject.Properties.Remove("status")

$body = $schema | ConvertTo-Json -Depth 30
```

### 22.4 Paso 3 - Crear analyzer en recurso destino

```powershell
$token2 = (az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv)
$targetEndpoint = "https://srbaisrv-westeurope.services.ai.azure.com"
$targetAnalyzerId = "CU_NS_1.5_0"

Invoke-RestMethod \
  -Uri "$targetEndpoint/contentunderstanding/analyzers/${targetAnalyzerId}?api-version=$apiVersion" \
  -Headers @{ Authorization = "Bearer $token2"; "Content-Type" = "application/json" } \
  -Method PUT \
  -Body $body
```

### 22.5 Paso 4 - Validar estado `ready`

```powershell
do {
  Start-Sleep -Seconds 2
  $st = Invoke-RestMethod \
    -Uri "$targetEndpoint/contentunderstanding/analyzers/${targetAnalyzerId}?api-version=$apiVersion" \
    -Headers @{ Authorization = "Bearer $token2" } \
    -Method GET
  Write-Host "Estado: $($st.status)"
} while ($st.status -eq "creating")

if ($st.status -ne "ready") {
  throw "Analyzer no quedo ready. Estado final: $($st.status)"
}
```

### 22.6 Paso 5 - Registrar modelo en BD para el nuevo endpoint

Registrar una nueva fila activa en `ModeloConfigs` (tipo extraccion) con:

- `Key` nuevo (ejemplo `nota.simple.1_5.azure-cu-we`)
- `Endpoint` del recurso destino
- `AnalyzerId` replicado
- mismo `Provider`
- `Activo = 1`

### 22.7 Paso 6 - Activar balanceo en tipologia

Actualizar configuracion de tipologia:

- `extraction.modelKey = <modelo principal>`
- `extraction.secondaryModelKey = <modelo secundario>`

Con esto, el provider aplica round-robin automatico 50/50 (si ambos modelos estan activos y resolubles).

### 22.8 Checklist de validacion post-cambio

1. `GET analyzer` en destino devuelve `status=ready`.
2. `ModeloConfigs` contiene ambas keys activas (principal y secundaria).
3. Tipologia objetivo publica incluye `secondaryModelKey`.
4. App Insights (`CU.AnalysisMs`) muestra llamadas en ambos `ModelKey`.
5. Reparto esperado en prueba controlada: cada model key entre 40%-60%.
6. Ejecuciones en `DocumentoEjecuciones` finalizan sin aumento anomalo de errores.

---

## 23. Trazabilidad ADO (creado 2026-06-01)

Se crea un paquete de trabajo en Azure DevOps para ejecutar la fase de resiliencia post-validacion multi-region:

- Feature (padre): **99702** - `[CU-RESILIENCIA] Fase 2 post-validación de balanceo multi-región`
- PBI hijo: **99704** - Persistir trazabilidad de extraccion (`ModelKey` + `Endpoint/Region` efectiva)
- PBI hijo: **99706** - Hard timeout de CU + telemetria `CU.HardTimeout`
- PBI hijo: **99703** - Ajuste de concurrencia durable + CU y calibracion operativa
- PBI hijo: **99705** - Circuit breaker por `ModelKey`/region con failover

Estado ADO actualizado (2026-06-01, tras validacion tecnica):

- **99702**: `In Progress`
- **99704**: `Committed`
- **99706**: `Committed`
- **99703**: `Committed`
- **99705**: `Committed`

Evidencia registrada en los WI:

- Build `DocumentIA.Functions`: OK
- `dotnet test` (`DocumentIA.Tests.Unit`): 652 tests, 0 fallos

Actualizacion adicional de calidad (2026-06-01):

- Corregidos tests de `TipologiasAdminFunction` para nueva firma con `IMemoryCache`.
- Añadidos tests `AzureContentUnderstandingOptionsTests` para defaults y overrides de resiliencia CU.
- Añadidos tests `AzureContentUnderstandingProviderCircuitBreakerTests`:
  - failover a secondary con circuito primary abierto
  - circuito deshabilitado mantiene model key preferido
  - cierre de circuito tras cooldown

Validacion funcional en entorno (App Insights, ultimas 24h):

- Eventos `CU.CircuitOpen`, `CU.CircuitClosed`, `CU.CircuitFailover`, `CU.CircuitRejected`, `CU.HardTimeout`: **0**
- `CU.AnalysisMs` para `nota.simple_bal` mantiene reparto por `ModelKey`:
  - `nota.simple.1_5.azure-cu`: 25 llamadas
  - `nota.simple.1_5.azure-cu-we`: 25 llamadas

Interpretacion: el circuit breaker esta desplegado y cubierto por tests, pero no se ha activado en produccion en la ventana analizada (comportamiento esperado en ausencia de degradacion).

### 23.1 Estado de implementacion en esta sesion

Implementado:

1. **Trazabilidad funcional de extraccion (99704)**
  - Se anaden en salida de extraccion los campos efectivos:
    - `ModelKeyEfectivo`
    - `EndpointEfectivo`
    - `ProcessingLocationEfectiva`
  - Se propagan a `DetalleEjecucion.Extraccion`.

2. **Hard timeout CU (99706)**
  - Nuevo setting: `HardTimeoutSeconds` en `AzureContentUnderstandingOptions`.
  - Timeout duro por intento en llamada `AnalyzeBinaryAsync` usando `RequestContext.CancellationToken`.
  - Evento de telemetria: `CU.HardTimeout` con tipologia, modelKey, intento y timeout.

3. **Ajuste base de concurrencia (99703)**
  - `host.json`: `maxConcurrentActivityFunctions=4`, `maxConcurrentOrchestratorFunctions=4`.
  - `appsettings.json`: `Extraction:AzureContentUnderstanding:MaxConcurrentCalls=4`.
  - `appsettings.json` y `local.settings.json`: `HardTimeoutSeconds=90`.

4. **Circuit breaker por model key/region (99705)**
  - Implementado en `AzureContentUnderstandingProvider` con estado por `ModelKey`.
  - Telemetria de ciclo de vida:
    - `CU.CircuitOpen`
    - `CU.CircuitClosed`
    - `CU.CircuitFailover`
    - `CU.CircuitRejected`
  - Failover automatico entre `modelKey` principal/secundario cuando el circuito del seleccionado esta abierto.

Pendiente en siguiente iteracion:

- Validacion funcional en entorno y ajuste fino de umbrales de circuit breaker.

