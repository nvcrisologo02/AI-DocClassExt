# Monitoreo y Alertas — Configuración REAL

**Estado:** ✅ Verificado en código (2026-06-10); alertas de Azure Monitor y action group verificados en Azure (2026-08-04, AB#99083)  
**Fuente:** `src/backend/DocumentIA.Functions/` + `azure-pipelines*.yml` + `scripts/observability/create-monitor-alerts.ps1`  
**Producción:** SRBRGDOCSAIPROD

---

## 📋 Resumen Ejecutivo

### Application Insights
- **Component:** `srbappiprodocai`
- **Connection String (Variable):** `InstrumentationKey=c57c6166-c5ac-4584-a4d6-13fbb37860c4;IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.com/;ApplicationId=19eeb2fb-8544-41c5-931a-b0983a26e92e`
- **Estado:** ✅ Habilitado en producción
- **Sampling:** ✅ Habilitado (20 events/sec, excludes "Request" type)
- **Live Metrics:** ✅ Habilitado

### Eventos Telemetría Registrados
| Evento | Propiedad Clave | Métrica | Origen |
|--------|---|---|---|
| `DocumentProcessed` | Tipologia, EstadoFinal, UseFallbackLLM, NombreDocumento | DuracionTotalMs, DuracionExtraccionMs, DuracionClasificacionMs | `PersistirActivity.cs` |
| `CU.CircuitOpen` | tipologia, modelKey, reason, failureThreshold | openUntilUtc | `AzureContentUnderstandingProvider.cs` |
| `CU.CircuitClosed` | tipologia, modelKey, reason | - | `AzureContentUnderstandingProvider.cs` |
| `CU.CircuitFailover` | tipologia, primaryModelKey, fallbackModelKey | - | `AzureContentUnderstandingProvider.cs` |
| `CU.CircuitRejected` | tipologia, modelKey | - | `AzureContentUnderstandingProvider.cs` |
| `CU.TransientError` | attempt, statusCode, tipologia | - | `AzureContentUnderstandingProvider.cs` |
| `CU.HardTimeout` | tipologia, modelKey, attempt, hardTimeoutSeconds | - | `AzureContentUnderstandingProvider.cs` |
| `Prompt.Trace` | provider, operation, tipologia, modelKey, deployment, systemPromptSha256, userPromptSha256 | systemPromptLength, userPromptLength | `PromptTraceTelemetryService.cs` |
| `BlobCleanupCycle` | source | blobs_procesados, blobs_eliminados, blobs_error, bytes_liberados | `BlobCleanupTimerTrigger.cs` |

---

## 📊 Métricas Reales Registradas

### 1. CU (Content Understanding) — Subfases
```
CU.PrepareMs       → tiempo preparación request (ms)
CU.LimiterWaitMs   → tiempo espera en queue local (ms)
CU.AnalysisMs      → tiempo análisis en Azure CU (ms)
CU.ParseMs         → tiempo parsing resultado (ms)
CU.Attempts        → número intentos (con retries)
```

**Propiedades asociadas:** `Tipologia`, `ModelKey`

**Umbrales interpretados del código:**
- Si `CU.LimiterWaitMs` > 10 segundos → backpressure local (cola saturada)
- Si `CU.AnalysisMs` > 60 segundos → Azure CU lento o saturado
- Si `CU.Attempts` ≥ 4 → múltiples retries (revisar timeout o disponibilidad CU)

---

### 2. Duraciones por Actividad de Orquestación
```
DocumentIA.Duracion.Total          → ms totales documento
DocumentIA.Duracion.Clasificacion  → ms clasificación
DocumentIA.Duracion.Extraccion     → ms extracción
DocumentIA.Duracion.Validacion     → ms validación
DocumentIA.Duracion.GDC            → ms integración GDC
DocumentIA.Duracion.Integracion    → ms post-procesamiento
DocumentIA.Duracion.Persistencia   → ms guardado BD
```

**Propiedades asociadas:** `Tipologia`

**Contexto:** Registradas al finalizar cada documento en `PersistirActivity.cs`

---

### 3. Limpieza de Blobs
```
BlobCleanupCycle (evento)
├─ blobs_procesados    → cantidad revisadas
├─ blobs_eliminados     → cantidad eliminadas
├─ blobs_no_encontrados → fallos de acceso
├─ blobs_error          → excepciones durante eliminación
└─ bytes_liberados      → espacio liberado
```

**Programación:** Timer trigger CRON = `"0 0 3 * * *"` (3 AM UTC diario)

---

### 4. Prompts (Tracing Opcional)
```
Prompt.Trace (evento)
├─ provider              → "gpt" | "di" | ...
├─ operation             → "extraction" | "classification" | ...
├─ tipologia             → tipología documento
├─ modelKey              → modelo específico
├─ deployment            → deployment OpenAI (ej: "gpt-4o-mini")
├─ systemPromptSha256    → hash sistema (privacidad)
├─ userPromptSha256      → hash usuario (privacidad)
├─ systemPromptSnippet   → (si IncludePromptText=true) primeros 20KB
└─ userPromptSnippet     → (si IncludePromptText=true) primeros 20KB
```

**Status en producción:**
- `PromptTracing__Enabled` = `true` ✅
- `PromptTracing__IncludePromptText` = `true` ✅ (no privacidad restringida)
- `PromptTracing__MaxPromptTextChars` = `20000` caracteres

---

## ⚙️ Configuración de Logging

### host.json (Durable Functions)
```json
{
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20,
        "excludedTypes": "Request"
      },
      "enableLiveMetricsFilters": true
    },
    "logLevel": {
      "default": "Warning",
      "Host.Results": "Information",
      "Function": "Information",
      "Host.Aggregator": "Warning",
      "Microsoft.Azure.Functions.Worker": "Warning",
      "Microsoft.DurableTask": "Warning"
    }
  }
}
```

**Efectos:**
- ✅ Logging de resultados = `Information` (errores + completados)
- ✅ Logging de función = `Information` (todo)
- ⚠️ Microsoft logs = `Warning` (solo errores serios)
- 🟡 Durable Tasks = `Warning` (no verbose)
- 🟡 Sampling = 20 events/sec (algunos eventos pueden omitirse en picos)

---

## 🔴 Alertas Implementadas EN CÓDIGO

### Circuit Breaker — Azure Content Understanding

```csharp
// Configuración en producción (pipeline):
Extraction__AzureContentUnderstanding__EnableCircuitBreaker = true
Extraction__AzureContentUnderstanding__CircuitBreakerFailureThreshold = 5
Extraction__AzureContentUnderstanding__CircuitBreakerOpenSeconds = 45
```

**Comportamiento:**
1. Si 5 requests consecutivos a CU fallan → circuit abierto
2. Durante 45 segundos, rechaza nuevos requests (rápida respuesta sin timeout)
3. Emite evento `CU.CircuitOpen` con razón y timestamp apertura
4. Al timeout expirar, reintenta automáticamente (evento `CU.CircuitClosed`)

**Causa raíz posible:**
- Azure CU endpoint down o rate-limited
- Network connectivity issues
- Timeout de 90 segundos excedido

---

### Retry Policy — Azure Content Understanding

```csharp
// Configuración en producción:
Extraction__AzureContentUnderstanding__MaxRetries = 3
Extraction__AzureContentUnderstanding__InitialRetryDelayMs = 500
```

**Patrón:** Exponential backoff (500ms → 1000ms → 2000ms)

**Evento registrado:** `CU.TransientError` con:
- `attempt` = número de intento (1, 2, 3)
- `statusCode` = HTTP status si aplica
- `tipologia` = tipo documento

---

### Hard Timeout — Azure Content Understanding

```csharp
// Configuración en producción:
Extraction__AzureContentUnderstanding__HardTimeoutSeconds = 90
```

**Comportamiento:**
- Si CU tarda > 90 segundos → cancela request y falla actividad
- Emite evento `CU.HardTimeout` con:
  - `tipologia`, `modelKey`, `attempt`, `hardTimeoutSeconds`

**Típicamente causa:** CU saturado, API lento o red inestable

---

### Circuit Breaker — GDC (Document Management System)

```csharp
// Configuración en código (GdcSettings.cs):
CircuitBreakerFailures = 5
CircuitBreakerDurationMs = 30000 (30 seg)
ExponentialBackoff = true
```

**Evento:** Implícito en `ResilientPlugin.cs` (sin TrackEvent explícito, solo logs)

**Razón:** GDC es legacy SOAP sobre HTTP Basic Auth + SSL bypass — muy propenso a fallos

---

## 📈 Workbooks y Consultas KQL Validadas

### Workbook Existente
**Ruta:** `docs/observabilidad/workbooks/documentia-cu-performance.workbook.json`

**Dashboards implementados:**
1. **Resumen CU (últimas 24h)** → eventos, promedio, P95, máximo por métrica
2. **P95 por subfase** → timechart de preparación, espera, análisis, parse
3. **Diagnóstico rápido** → compara espera vs análisis para identificar cuello de botella
4. **Top operaciones esperando** → lista docID con mayor LimiterWaitMs

**Estado:** ✅ Queries validadas, schema comprobado

---

### Script de Exportación
**Ruta:** `scripts/reports/export-cu-performance-insights.ps1`

**Uso:**
```powershell
# Exporta reportes de CU últimas 24h
.\export-cu-performance-insights.ps1 `
  -SubscriptionId "..." `
  -ResourceGroup "SRBRGDOCSAIPROD" `
  -AppInsightsName "srbappiprodocai" `
  -OutputDir ".\artifacts\reports\cu-performance"

# Rango específico
.\export-cu-performance-insights.ps1 `
  -SubscriptionId "..." `
  -ResourceGroup "SRBRGDOCSAIPROD" `
  -StartTime "2026-06-09T08:00:00Z" `
  -EndTime "2026-06-09T20:00:00Z"
```

**Salidas:** JSON + CSV con 4 queries pre-construidas

---

## 🔔 Alertas Implementadas en Azure Monitor (AB#99083, 2026-08-04)

**Estado:** ✅ 5 scheduled query rules activas sobre `srbappiprodocai` + action group de correo asociado.

| Regla | Condición | Ventana / Frecuencia | Sev |
|-------|-----------|----------------------|-----|
| `srbalerterrprodocai` | % de `DocumentProcessed` con `EstadoFinal` ∈ (Error, ERROR, Fallido) > 10% (mín. 5 docs) | 5 min / 5 min | 2 |
| `srbalertlatprodocai` | p95 de `DocumentIA.Duracion.Total` > 120 s | 15 min / 15 min | 2 |
| `srbalertfbkprodocai` | % de `DocumentProcessed` con `UseFallbackLLM=true` > 20% (mín. 5 docs) | 30 min / 15 min | 3 |
| `srbalertexcprodocai` | > 10 excepciones (cubre fallos GDC mientras no exista evento específico) | 5 min / 5 min | 2 |
| `srbalertidleprodocai` | 0 requests en horario laboral (L-V, evaluada 11:00-18:00 Europe/Madrid) | 180 min / 60 min | 2 |

Además existen 2 metric alerts previas de plataforma: `srbalertcpuprodocai` (CPU) y `srbalertmemprodocai` (memoria).

**Criterios de diseño** (difieren del plan original 7.3.4 por alinearse a la telemetría real):
- El criterio de error es `EstadoFinal in (Error, ERROR, Fallido)` — no `!= "OK"` — para no contar REVISION como fallo.
- Las alertas de ratio exigen un mínimo de 5 documentos por ventana para evitar falsos positivos con volumen bajo.
- No existen los eventos `GdcUploadFailed` / `GptFallbackUsed` en el código; el fallback se mide con la dimensión `UseFallbackLLM` de `DocumentProcessed`.
- La alerta de inactividad se amplió de 60 a 180 min (2026-08-14): el filtro horario de la query aplica al momento de evaluación, no a la ventana, así que se evalúa desde las 11:00 para que las 3 h previas caigan enteras en jornada (8:00-18:00) y no salte de madrugada/primera hora sin tráfico. Trade-off: la última detección posible del día es ~17:xx; un tramo de silencio iniciado después de las 15:00 no alerta ese día.

**Gestión (script idempotente):** `scripts/observability/create-monitor-alerts.ps1` crea o actualiza las 5 reglas. Re-ejecutable sin riesgo; parámetro `-ActionGroupId` para asociar el action group.

### Action group de avisos (correo)

- **Recurso:** `srbagoperprodocai` (short name `docaiops`), RG `SRBRGDOCSAIPROD`, ubicación Global.
- **Receptores actuales:** `ignacio.varas@sareb.es` (common alert schema activado).
- Asociado a las 5 scheduled query rules (no a las metric alerts de CPU/memoria).

### Cómo dar de alta (o quitar) correos de aviso

Los destinatarios se gestionan **solo en el action group**; las alertas no se tocan.

**Opción A — Azure Portal (recomendada para operación):**
1. Portal → Azure Monitor → **Alerts** → **Action groups** → `srbagoperprodocai` → **Edit**.
2. En **Notifications**, añadir una fila *Email/SMS message/Push/Voice* → tipo **Email**, con nombre identificativo y la dirección.
3. Marcar *Enable the common alert schema* → **Yes**. Guardar.
4. El nuevo destinatario recibe un correo de confirmación de `azure-noreply@microsoft.com` (si la dirección es nueva para Azure, con validación OTP que debe completar antes de recibir avisos). Vigilar la cuarentena del filtro corporativo.
5. (Opcional) Probar con **Test** sobre el action group (sample type *Log alert V2*).

**Opción B — CLI:** el comando `az monitor action-group` falla tras el proxy corporativo (TLS); usar `az rest` contra ARM con el JSON completo de receptores (PUT reemplaza la lista entera — incluir siempre los receptores existentes):

```bash
az rest --method put \
  --url "https://management.azure.com/subscriptions/<subId>/resourceGroups/SRBRGDOCSAIPROD/providers/microsoft.insights/actionGroups/srbagoperprodocai?api-version=2023-01-01" \
  --body '{ "location": "Global", "properties": { "groupShortName": "docaiops", "enabled": true,
      "emailReceivers": [
        { "name": "ignacio", "emailAddress": "ignacio.varas@sareb.es", "useCommonAlertSchema": true },
        { "name": "nuevo",   "emailAddress": "nueva.persona@sareb.es", "useCommonAlertSchema": true }
      ] } }'
```

**Límites del servicio:** máx. 100 correos/hora por dirección y región (Azure suspende y avisa si se supera); hasta 1.000 receptores de email por action group.

---

## 🔧 Cómo Acceder a Monitoreo Real

### 1. Azure Portal → Application Insights
```
Recurso: srbappiprodocai
Ruta: Portal → "SRBRGDOCSAIPROD" RG → "Application Insights srbappiprodocai"
```

**Secciones útiles:**
- **Performance** → Duraciones P95/P99, dependencias
- **Failures** → Errores por tipo, stack traces
- **Logs** → KQL queries custom
- **Live Metrics** → Dashboards en vivo

### 2. Workbook Integrado
```
Acceso: Azure Portal → Application Insights "srbappiprodocai" 
       → "Workbooks" → Crear desde "Documentia CU Performance" (si importado)
```

### 3. Queries Manuales KQL
```kusto
// Últimas 24h — eventos CU
customMetrics
| where timestamp > ago(24h)
| where name startswith "CU."
| summarize eventos=count(), avg_ms=round(avg(value), 1), p95_ms=round(percentile(value, 95), 1) by name
| order by name asc

// Circuit breaker status
customEvents
| where timestamp > ago(24h)
| where name in ("CU.CircuitOpen", "CU.CircuitClosed")
| project timestamp, name, tostring(customDimensions["reason"]), tostring(customDimensions["openUntilUtc"])
| order by timestamp desc
```

### 4. Script PowerShell
```powershell
# Exporta reportes con queries pre-construidas
cd C:\temp\MVP\documento-ia-clasificacion-mvp
.\scripts\reports\export-cu-performance-insights.ps1 `
  -SubscriptionId "YOUR_SUBSCRIPTION_ID" `
  -ResourceGroup "SRBRGDOCSAIPROD" `
  -AppInsightsName "srbappiprodocai"
```

---

## 📝 Configuración por App (Referencia Rápida)

### Functions App (srbappprodocai)
| Setting | Valor | Propósito |
|---------|-------|----------|
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | `InstrumentationKey=c57c6166...` | Conexión AppInsights |
| `Extraction__AzureContentUnderstanding__MaxConcurrentCalls` | `4` | Limiter local (no queue) |
| `Extraction__AzureContentUnderstanding__HardTimeoutSeconds` | `90` | Timeout máximo CU |
| `Extraction__AzureContentUnderstanding__EnableCircuitBreaker` | `true` | Protección ante fallos |
| `Extraction__AzureContentUnderstanding__CircuitBreakerFailureThreshold` | `5` | Fallos antes de abrir circuito |
| `Extraction__AzureContentUnderstanding__CircuitBreakerOpenSeconds` | `45` | Duración abierto |
| `PromptTracing__Enabled` | `true` | Registrar prompts a AppInsights |
| `PromptTracing__IncludePromptText` | `true` | Incluir full prompt (no hash solo) |
| `PromptTracing__MaxPromptTextChars` | `20000` | Truncar si > 20KB |

---

## 🚨 Cómo Diagnosticar Problemas

### Problema: CU lento (P95 > 60 seg)
```kusto
customMetrics
| where timestamp > ago(1h)
| where name == "CU.AnalysisMs"
| summarize max_ms=max(value), p95_ms=percentile(value, 95), count=count() by tostring(customDimensions["Tipologia"])
| order by p95_ms desc
```
**Acciones:**
- ¿Es toda tipología lenta o una específica?
- Si específica → revisar tamaño promedio documento
- Si todas → contactar Azure CU support

### Problema: Circuit breaker abierto
```kusto
customEvents
| where timestamp > ago(24h)
| where name == "CU.CircuitOpen"
| project timestamp, reason=tostring(customDimensions["reason"]), tipologia=tostring(customDimensions["tipologia"])
| order by timestamp desc
```
**Acciones:**
- Verificar status Azure CU endpoint
- Revisar network connectivity desde Functions
- Si persistente > 2h → escalar a Azure support

### Problema: Blobs no se limpian
```kusto
customEvents
| where timestamp > ago(24h)
| where name == "BlobCleanupCycle"
| project timestamp, 
          procesados=todint(customMeasurements["blobs_procesados"]),
          eliminados=todint(customMeasurements["blobs_eliminados"]),
          errores=todint(customMeasurements["blobs_error"])
| order by timestamp desc
```
**Acciones:**
- Si `errores > 0` → revisar Key Vault access permissions
- Si `procesados == 0` → revisar CRON schedule (3 AM UTC)
- Si datos obsoletos (> 3 días) → revisión manual

---

## 📞 Escalation

**Las 5 alertas de Azure Monitor notifican por correo al action group `srbagoperprodocai`; a partir del aviso, aplicar esta priorización:**

1. **P1 (Crítico):** CU circuit abierto > 30 min → contactar Azure CU support
2. **P2 (Alto):** P95 CU > 120 seg → revisar workbook + diagnosticar tipología
3. **P3 (Medio):** Blob cleanup errors → revisar permisos KeyVault
4. **P4 (Bajo):** Latencia GDC > 30 seg → revisar legacy system status

---

## 📚 Archivos Relacionados

| Archivo | Propósito |
|---------|----------|
| `src/backend/DocumentIA.Functions/Services/ApplicationInsightsTelemetryService.cs` | Wrapper TelemetryClient (TrackEvent, TrackMetric) |
| `src/backend/DocumentIA.Functions/Services/PromptTraceTelemetryService.cs` | Registra prompts con privacidad (SHA256 + truncate) |
| `src/backend/DocumentIA.Functions/host.json` | Logging config AppInsights (sampling, levels) |
| `src/backend/DocumentIA.Functions/appsettings.json` | Local dev config (override en pipeline) |
| `docs/observabilidad/workbooks/documentia-cu-performance.workbook.json` | Dashboard interactivo AppInsights |
| `scripts/reports/export-cu-performance-insights.ps1` | Script exporta KQL queries a CSV/JSON |
| `scripts/observability/create-monitor-alerts.ps1` | Crea/actualiza las 5 alert rules de Azure Monitor (idempotente, `-ActionGroupId` opcional) |
| `azure-pipelines.yml` / `azure-pipelines-functions.yml` | Config resiliencia (circuit breaker, retries, timeout) |

---

## ✅ Validación

- ✅ Events `CU.*` verificados en `AzureContentUnderstandingProvider.cs`
- ✅ Metrics `DocumentIA.Duracion.*` verificados en `PersistirActivity.cs`
- ✅ Event `Prompt.Trace` verificado en `PromptTraceTelemetryService.cs`
- ✅ Circuit breaker thresholds verificados en pipeline YAML
- ✅ Workbook queries verificadas en JSON schema
- ✅ Connection string verificada en `set-app-settings.ps1`
- ✅ 5 scheduled query rules y action group verificados en Azure (`az monitor scheduled-query list`, `actions.actionGroups`)
- ✅ Queries de alerta validadas contra datos reales de App Insights (370 `DocumentProcessed` en 7 días, p95 duración 29,6 s)

**Última verificación:** 2026-08-04 (alertas en Azure); 2026-06-10 (código fuente)
