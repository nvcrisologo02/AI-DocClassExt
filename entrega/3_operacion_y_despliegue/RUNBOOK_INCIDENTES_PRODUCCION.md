# Runbook de Incidentes — Producción SRBRGDOCSAIPROD

**Aplicable a:** SRBRGDOCSAIPROD (Production Only)  
**Fuente:** Código verificado en Activities + Providers

---

## Tabla de Contenidos

1. [SLAs & Severidad](#slas--severidad)
2. [Incidentes Comunes](#incidentes-comunes)
3. [Árbol de Diagnóstico](#árbol-de-diagnóstico)
4. [Procedimientos por Incidente](#procedimientos-por-incidente)
5. [Escalation Path](#escalation-path)
6. [Post-Incident Review](#post-incident-review)

---

## SLAs & Severidad

### Tabla de severidades

| Severidad | P95 Latencia | Disponibilidad | Tiempo de respuesta (SLA) | Nivel que atiende |
|-----------|---|---|---|---|
| **P1 — Crítico** | > 180s o no responde | < 90% | ≤ 15 min | Tier 1 → Tier 2 → Tier 3 + Soporte Azure |
| **P2 — Alto** | 60-180s | 90-95% | ≤ 30 min | Tier 1 → Tier 2 |
| **P3 — Medio** | 30-60s | 95-99% | ≤ 2h | Tier 1 |
| **P4 — Bajo** | < 30s o degradación | > 99% | ≤ 1 día | Tier 1 (registro y seguimiento ordinario) |

### Matriz de escalado

| Nivel | Rol | Entra en acción cuando | Acciones principales | Escala al siguiente nivel si |
|-------|-----|------------------------|----------------------|------------------------------|
| **Tier 1** | Operación de guardia (on-call) | Toda incidencia; primer diagnóstico | Verificación rápida, clasificación de severidad, workaround temporal, registro del incidente | No resuelta en 15 min · severidad P1/P2 · requiere acceso Azure o cambios de código |
| **Tier 2** | Responsable técnico (Tech Lead) | Escalado desde Tier 1 | Diagnóstico profundo (KQL), revisión de logs, ajuste de app settings, coordinación con soporte Azure | Requiere cambio de código o release · infraestructura Azure caída (CU/DI/SQL) · > 30 min sin resolución |
| **Tier 3** | Dirección técnica + Soporte Azure | Escalado desde Tier 2 | Decisión de release/hotfix, gestión con soporte Azure, comunicación a dirección | — |

---

## Incidentes Comunes

### Azure Content Understanding (CU) Circuit Breaker / Timeout
**Patrón:** Múltiples fallos consecutivos en extracción, `TaskCanceledException` en WaitForCompletionAsync

**Síntomas:**
-  Documentos fallan en extraction phase con `Hard timeout en Azure Content Understanding`
-  AppInsights: evento `CU.CircuitOpen` o `TaskCanceledException`
-  P95 extraction > 90s o CU tarda > 90s (default timeout)
-  Métrica `CU.CircuitRejected` > 0
- `GDC health probe timeout` log (indica problema de red más amplio)
-  Retry 2/3, 3/3 fallando consecutivamente

**Causas Raíz Posibles:**
- Azure CU endpoint down o rate-limited
- **Network connectivity issue (Functions ↔ CU or CU ↔ GDC)** — indicado por GDC probe timeout
- CU timeout (90s) excedido repetidamente (típico si Azure CU está lento o sobrecargado)
- DoS o spike inesperado de volumen
- Firewall/NSG bloqueando requests a CU endpoint
- Azure CU circuit breaker open (threshold 5 errores consecutivos)

**Verificación Rápida (1 min):**
```kusto
customEvents
| where timestamp > ago(10m)
| where name in ("CU.CircuitOpen", "CU.CircuitRejected")
| project timestamp, reason=tostring(customDimensions["reason"]), tipologia=tostring(customDimensions["tipologia"])
| order by timestamp desc
| take 20
```

**Diagnóstico Profundo:**
```kusto
// 1. Comparar antes/después apertura
customMetrics
| where timestamp between (ago(1h) .. now())
| where name == "CU.AnalysisMs"
| summarize p95_ms=percentile(value, 95), eventos=count() by bin(timestamp, 5m), tostring(customDimensions["Tipologia"])
| order by timestamp desc

// 2. Ver intentos y retries
customMetrics
| where timestamp > ago(30m)
| where name == "CU.Attempts"
| summarize avg_attempts=avg(value), max_attempts=max(value) by tostring(customDimensions["Tipologia"])
```

**Acciones:**
1. **Inmediato (≤ 5 min):**
   - Revisar log `GDC health probe timeout` en últimas 2h:
     ```kusto
     traces
     | where timestamp > ago(2h)
     | where message contains "GDC health probe timeout"
     | summarize count=count(), sample_message=any(message) by bin(timestamp, 10m)
     ```
     - Si > 0 → problema de red más amplio, afecta múltiples servicios
   - Verificar status Azure CU endpoint: https://status.azure.com/
   - Test conexión CU desde Functions: `Test-NetConnection -ComputerName "CU_ENDPOINT" -Port 443`
   - Revisar Azure KeyVault: ¿Key Vault access revoked?

2. **Si GDC probe timeout o test-netconnection falla:**
   - **Probable:** Firewall/NSG bloqueando outbound a Azure services
   - Revisar NSG rules de Functions app (RBAC, subnets, service endpoints)
   - Verificar que Azure services endpoints habilitados (CU, GDC, KeyVault)
   - Contactar network/infrastructure team

3. **Si es timeout sin conectividad issue:**
   - Azure CU está lento (pero respondiendo)
   - Esperar 45-90 segundos (circuit breaker abre 45s)
   - Monitor `CU.CircuitClosed` event → si aparece, issue resuelto
   - Si no se recupera en 2 min → **P2 escalation: contactar Azure CU support**

4. **Si es KeyVault:**
   - Verificar RBAC: Functions app tiene "Key Vault Secrets User"?
   - Check Key Vault firewall: ¿está permitido Azure Functions?
   - Renovar connection string si expiró

5. **Temporal workaround (mientras Azure responde):**
   - Trigger manual del script de fallback GPT (si enabled):
     ```json
     PUT /api/classification/force-fallback?tipologia=XXX
     ```
   - Revisar `Extraction__GptFallback__Enabled` en app settings
   - Aumentar `Extraction__AzureContentUnderstanding__TimeoutSeconds` desde 90 a 180 (si es timeout genuino de Azure CU)

**Escalation:**
- Si GDC probe timeout > 5 min → **P2 — Network/Infrastructure issue** → Tech Lead + Network team
- Si circuit abierto > 30 min → **P2** → Tech Lead + Azure Support
- Si circuit abierto > 2h → **P1** → CTO + Priority Azure Support

---

### Database Connection Timeout o Unavailable
**Patrón:** Documentos fallan en persistencia, Durable Functions timeout

**Síntomas:**
-  Activities fallan con `SqlConnectionString` error
-  AppInsights: `Microsoft.EntityFrameworkCore` errors
-  P95 "Persistencia" > 30s
-  Durable Functions orchestration cancela después retry timeout

**Causas Raíz Posibles:**
- SQL Server outage o mantenimiento
- Connection pool exhausted (> max connections)
- Network ACL bloqueando Functions ↔ SQL
- DNS resolution failure

**Verificación Rápida (1 min):**
```powershell
# Conectar con identidad managed
$connectionString = (az functionapp config appsettings list `
  --resource-group SRBRGDOCSAIPROD `
  --name srbappprodocai `
  --query "[?name=='SqlConnectionString'].value" -o tsv)

# Test via sqlcmd
sqlcmd -S "YOUR_SQL_SERVER.database.windows.net" -d "DocumentIA" -U "user@domain" -P "password" -Q "SELECT 1"
```

**Diagnóstico Profundo:**
```kusto
// Errores de base de datos últimas 2h
traces
| where timestamp > ago(2h)
| where severityLevel >= 2  // Error or higher
| where message contains "SqlConnection" or message contains "EntityFrameworkCore"
| summarize count=count(), sample_message=any(message) by bin(timestamp, 10m)
| order by timestamp desc
```

**Acciones:**
1. **Inmediato (≤ 5 min):**
   - Verificar Azure SQL Server status: `az sql server show --name YOUR_SERVER`
   - Check network connectivity: `Test-NetConnection -ComputerName YOUR_SQL_SERVER -Port 1433`
   - Revisar SQL Server firewall rules (¿permite Azure Functions IP?)

2. **Si es connection pool:**
   - Aumentar timeout en appsettings: `Connection Timeout=120` (default 30)
   - Reducir `maxConcurrentActivityFunctions` temporalmente (del 4 al 2)
   - Monitorear recuperación

3. **Si es SQL outage:**
   - Contactar Azure SQL Support
   - Revisar maintenance windows (user puede tener backup)
   - Opción failover: ¿hay geo-replica?

4. **Temporal workaround:**
   - Pausar Functions app: `az functionapp stop --resource-group SRBRGDOCSAIPROD --name srbappprodocai`
   - Esperar 5 min, reiniciar: `az functionapp start`

**Escalation:**
- Si DB unavailable > 15 min → P1 → CTO + Microsoft Support
- Si lenta (> 30s P95) > 1h → P2 → Tech Lead

---

### GDC Integration (Document Management System) Failures
**Patrón:** Documentos clasificados pero no integran con GDC

**Síntomas:**
-  Extracción y clasificación OK
-  Falla en `IntegrarActivity`
-  Event `plugin critico X fallo. Deteniendo cadena`
-  AppInsights: evento de `IntegrarActivity` con error

**Causas Raíz Posibles:**
- GDC HTTP Basic Auth credentials inválidas
- GDC endpoint down (legacy SOAP service)
- SSL certificate validation (GDC bypass SSL?)
- Request format cambió (GDC API incompatible)

**Verificación Rápida (2 min):**
```powershell
# Test GDC connectivity
$gdcEndpoint = "https://srbwidd03.sareb.srb:8090/sintws/IDocService"
$username = (az keyvault secret show --vault-name srbkvprodocai --name "GDC--HttpBasicUsername" --query "value" -o tsv)
$password = (az keyvault secret show --vault-name srbkvprodocai --name "GDC--HttpBasicPassword" --query "value" -o tsv)

$auth = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("$username`:$password"))
$headers = @{ "Authorization" = "Basic $auth" }

try {
  $response = Invoke-WebRequest -Uri "$gdcEndpoint/GetMetadata" -Headers $headers -SkipCertificateCheck -TimeoutSec 10
  Write-Output "GDC respondiendo: $($response.StatusCode)"
} catch {
  Write-Error "Error GDC: $($_.Exception.Message)"
}
```

**Diagnóstico Profundo:**
```kusto
// Errores en IntegrarActivity últimas 4h
traces
| where timestamp > ago(4h)
| where message contains "IntegrarActivity" or message contains "GDC" or message contains "plugin"
| summarize count=count(), sample_error=any(message) by bin(timestamp, 30m)
| order by timestamp desc

// Causas específicas
traces
| where timestamp > ago(4h)
| where severityLevel >= 2
| where message contains "GDC" or message contains "plugin critico"
| project timestamp, message
| order by timestamp desc
```

**Acciones:**
1. **Inmediato (≤ 5 min):**
   - Verificar credenciales KeyVault:
     ```powershell
     az keyvault secret show --vault-name srbkvprodocai --name "GDC--HttpBasicUsername"
     az keyvault secret show --vault-name srbkvprodocai --name "GDC--HttpBasicPassword"
     ```
   - Test connectivity (ver script arriba)

2. **Si es credential issue:**
   - Contactar GDC Admin → verificar password cambió
   - Actualizar KeyVault secrets (si cambiaron)
   - Restart Functions app para recargar secrets

3. **Si es GDC down:**
   - Contactar GDC support
   - Verificar maintenance windows
   - Revisar app setting `GDC__Endpoint` — ¿correcto?

4. **Si es SSL certificate:**
   - GDC tiene certificado autofirmado → revisar `GDC__BypassSslValidation=true` en appsettings
   - Si falta, agregar setting y redeploy

5. **Temporal workaround:**
   - Deshabilitar plugin GDC en orchestration (marcar como optional)
   - Enqueue documentos en cola para retry manual

**Escalation:**
- Si GDC down > 1h → Contactar GDC support (no es issue de DocumentIA)
- Si credential issue no resuelve → P2 → Tech Lead + SecOps (para rotation)

---

### Durable Functions Orchestration Timeout o Stuck
**Patrón:** Documentos quedan "Running" indefinidamente

**Síntomas:**
-  Documentos en estado "Running" > 1h
-  AppInsights: orchestration timeout logs
-  Durable Functions history muestra "Pending" activities
-  No hay eventos de "DocumentProcessed" en últimas 2h

**Causas Raíz Posibles:**
- Activity function deadlock (espera que nunca llega)
- Orchestrator bloqueado (no puede ejecutar siguiente activity)
- Durable Storage (AzureWebJobsStorage) inaccesible
- Concurrency limit reached: `maxConcurrentActivityFunctions=4` (cola infinita)

**Verificación Rápida (1 min):**
```powershell
# Ver instancias en ejecución
$orchestrationState = az functionapp function list --resource-group SRBRGDOCSAIPROD --name srbappprodocai --query "[?name=='OrquestadorActivity'].{Name:name, Status:status}" -o json | ConvertFrom-Json

# Si hay muchas "Running" => possible deadlock
Write-Output "Instancias Running: $($orchestrationState | Where-Object {$_.Status -eq 'Running'} | Measure-Object | Select-Object -ExpandProperty Count)"
```

**Diagnóstico Profundo:**
```kusto
// Documentos stuck en "Running"
customEvents
| where timestamp > ago(2h)
| where name == "DocumentProcessed" and customDimensions["EstadoFinal"] == "Running"
| summarize stuck_docs=count() by tostring(customDimensions["Tipologia"])

// Ver activities que más tiempo tardan
customMetrics
| where timestamp > ago(2h)
| where name startswith "DocumentIA.Duracion."
| where value > 300000  // > 5 minutos
| project timestamp, actividad=name, duracion_ms=value, tipologia=tostring(customDimensions["Tipologia"])
| order by timestamp desc
| take 50
```

**Acciones:**
1. **Inmediato (≤ 5 min):**
   - Verificar AzureWebJobsStorage connectivity:
     ```powershell
     $storageConn = az keyvault secret show --vault-name srbkvprodocai --name "AzureWebJobsStorage" --query "value" -o tsv
     # Test conexión
     ```
   - Revisar Application Insights logs (últimas 30 min)

2. **Si es storage issue:**
   - Verificar storage account status
   - Check firewall rules (Functions puede acceder?)
   - Reiniciar Functions app

3. **Si es deadlock en activity:**
   - Identificar qué activity está stuck (ver Durable Functions UI en portal)
   - Revisar logs de esa actividad específica
   - Si es CU → verificar circuit breaker (puede estar open)
   - Si es GDC → verificar conectividad GDC

4. **Si es concurrency maxed out:**
   - Aumentar `maxConcurrentActivityFunctions` de 4 a 8 (temporary)
   - Monitorear si se recuperan stuck instances
   - Investigar qué activity causa backlog (probablemente CU timeout)

5. **Force termination (último recurso):**
   ```powershell
   # Terminar instancia stuck (CUIDADO: pierde datos en-flight)
   az functionapp stop --resource-group SRBRGDOCSAIPROD --name srbappprodocai
   Start-Sleep -Seconds 30
   az functionapp start --resource-group SRBRGDOCSAIPROD --name srbappprodocai
   ```

**Escalation:**
- Si documentos stuck > 30 min → P2 → Tech Lead
- Si > 2h → P1 → CTO + Azure Support

---

### Document File Corruption en Azure Document Intelligence (DI)
**Patrón:** Documento procesa N veces OK, pero falla en ExtraerMarkdownLayoutActivity con error "file is corrupted"

**Síntomas:**
-  `ExtraerMarkdownLayoutActivity` falla con HTTP 400
-  Error: `"The file is corrupted or format is unsupported. Refer to documentation for the list of supported formats."`
-  En `AzureDocumentIntelligenceLayoutMarkdownProvider.cs:82`
-  Mensaje: "Error iniciando DI layout. Status=400"
-  El mismo documento procesa N veces sin problema (indica corrupto en esta instancia específica)

**Causas Raíz Posibles:**
- Base64 del documento está **incompleto o corrupto** en tránsito
- PDF tiene sectores corruptos (descarga interrumpida, transmisión fallida)
- Encoding/decoding base64 incorrecto en el trigger
- Blob Storage descargó documento incompleto
- Documento original descargado de GDC está corrupto

**Verificación Rápida (2 min):**
```kusto
// Ver frecuencia de este error
traces
| where timestamp > ago(1h)
| where message contains "file is corrupted" or message contains "format is unsupported"
| summarize count=count(), tipologias=dcount(tostring(customDimensions["Tipologia"])) by bin(timestamp, 10m)
| order by timestamp desc
```

**Diagnóstico Profundo:**
```kusto
// Documentos específicos con este error
customEvents
| where timestamp > ago(2h)
| where name == "ExtraerMarkdownLayoutActivity_Failed"
| where customDimensions["error"] contains "file is corrupted"
| project timestamp, correlationId=customDimensions["correlationId"], documento=customDimensions["nombre_documento"], tipologia=customDimensions["tipologia"]
| order by timestamp desc
| take 20
```

**Acciones:**
1. **Inmediato (≤ 5 min):**
   - Obtener `correlationId` del log
   - Revisar AppInsights para ese `correlationId`
   - Buscar en qué step falló: ¿en descargar de Blob? ¿en base64 encode? ¿en DI?

2. **Si es descarga de Blob incompleta:**
   - Verificar tamaño del blob en Storage:
     ```powershell
     $blob = Get-AzStorageBlob -Container "contenedor" -Blob "nombre.pdf"
     $blob.Length  # Comparar con tamaño esperado
     ```
   - Si < tamaño esperado → descarga interrumpida
   - Reintenta descarga del archivo original

3. **Si es base64 corrupto:**
   - En el trigger (IngestAPITrigger), verificar que la codificación base64 es correcta:
     ```csharp
     //  Correcto
     string base64 = Convert.ToBase64String(documentBytes);

     //  Si es incompleto o mal formado
     byte[] decodificado = Convert.FromBase64String(base64);
     int tamaño_original = decodificado.Length;
     // Si tamaño es inconsistente con el esperado → base64 corrupto
     ```
   - Revalidar que el documento llega completo al trigger

4. **Si es PDF corrupto en origen:**
   - Descargar el archivo desde GDC
   - Validar con comando local:
     ```powershell
     # Test PDF integrity
     Add-Type -AssemblyName System.Drawing
     $pdf = [System.Drawing.Image]::FromFile("C:\documento.pdf")
     Write-Output "PDF válido: $($pdf.Width)x$($pdf.Height)"
     ```
   - Si falla → documento corrupto en source, solicitar re-descarga a GDC

5. **Workaround temporal:**
   - Si documento es crítico pero base64 corrupto en tránsito:
     - Descargar nuevo del GDC
     - Re-enviar a processing queue con nuevo base64
   - Si falla de nuevo → problema en origen, contactar GDC admin

**Escalation:**
- Si ocurre en múltiples documentos → P2 → Network/Blob Storage issue
- Si es aislado → P3 → Validar origen, reintentar
- Si es patrón de GDC downloads → contactar GDC admin

---

### DurableSerializationException en Activities
**Patrón:** Documentos fallan en serialización Durable Functions

**Síntomas:**
-  Activity falla con `DurableSerializationException`
-  AppInsights: `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` error
-  Error en `DurableFunctionExecutor.Activity.cs:39`
-  Stacktrace menciona "RunActivityAsync" o "OutputBindingsMiddleware"

**Causas Raíz Posibles:**
- `Dictionary<string, object>` contiene tipos no JSON-serializables
- Versioning mismatch en modelos (cambios en propiedades)
- Tipos complejos sin constructores sin parámetros
- Valores `null` o estruturas circulares en diccionarios

**Verificación Rápida (1 min):**
```kusto
traces
| where timestamp > ago(1h)
| where message contains "DurableSerializationException" or message contains "OutputBindingsMiddleware"
| summarize count=count(), sample=any(message) by bin(timestamp, 10m)
| order by timestamp desc
```

**Diagnóstico Profundo:**
```kusto
// Ver qué activities fallan con serialización
traces
| where timestamp > ago(2h)
| where severityLevel >= 2
| where message contains "DurableFunctionExecutor" or message contains "OutputBindings"
| project timestamp, activity=extract("Activity: (\\w+)", 1, message), error=message
| distinct activity
```

**Acciones:**
1. **Inmediato (≤ 5 min):**
   - Identificar qué activity falla: ¿ExtraerActivity? ¿ClasificacionActivity?
   - Revisar qué tipología/documento causa el error
   - Buscar en AppInsights el documento específico para ver el payload

2. **Si es ExtraerActivity:**
   - Revisar `DatosNormalizados` y `DatosExtraidos` (Dictionary<string, object>)
   - El objeto problemático probablemente contiene un tipo complejo:
     ```csharp
     //  Problemático
     Dictionary<string, object> datos = new()
     {
         ["field"] = someComplexObject,  // ← Culpable
         ["array"] = new[] { 1, 2, 3 }   // ← Arrays pueden fallar
     };

     //  Solución
     Dictionary<string, string> datos = new()
     {
         ["field"] = JsonSerializer.Serialize(complexObject),
         ["array"] = JsonSerializer.Serialize(new[] { 1, 2, 3 })
     };
     ```

3. **Fix técnico (corto plazo):**
   - Cambiar `Dictionary<string, object>` a `Dictionary<string, string>`
   - Serializar valores complejos a JSON antes de pasar a Durable Functions
   - En la activity, deserializar si es necesario

4. **Fix técnico (largo plazo):**
   - Implementar `JsonConverter` personalizado para `Dictionary<string, object>`
   - O refactorizar modelo para usar tipos explícitos (no `object`)
   - Actualizar versión Durable SDK (>= 1.14.1 si hay fix)

5. **Workaround temporal:**
   - Filtrar valores no serializables antes de pasar a activity
   - Si es tipología específica, marcar como "no procesable" temporalmente

**Escalation:**
- Si ocurre en múltiples activities → P2 → Refactor modelo requerido
- Si es tipología específica → P3 → Revisar input de esa tipología

---

### High Latency on Classification or Extraction
**Patrón:** P95 latencia > 60 segundos (pero sin errores)

**Síntomas:**
-  Documentos procesados correctamente
-  P95 "Total" > 60s o P95 "Clasificacion" > 30s
-  Workbook "Diagnóstico Rápido" muestra Wait > 10s o Analysis > 60s
-  User reports "lento"

**Causas Raíz Posibles:**
- Azure CU o DI saturado (pero no failing)
- Network latency increase
- Local queue backpressure (todos los 4 concurrent slots ocupados)
- Large document (> 100 páginas)
- Tipología específica es computacionalmente cara

**Verificación Rápida (2 min):**
```kusto
// P95 latencias por subfase
customMetrics
| where timestamp > ago(1h)
| where name in ("CU.PrepareMs", "CU.LimiterWaitMs", "CU.AnalysisMs", "CU.ParseMs")
| summarize p95_ms=percentile(value, 95), max_ms=max(value) by name
| order by p95_ms desc

// Si LimiterWaitMs > 10000 => backpressure local
// Si AnalysisMs > 60000 => Azure CU lento
```

**Diagnóstico Profundo:**
```kusto
// Latencias por tipología
customMetrics
| where timestamp > ago(1h)
| where name == "DocumentIA.Duracion.Extraccion"
| summarize p50_ms=percentile(value, 50), p95_ms=percentile(value, 95), p99_ms=percentile(value, 99) by tostring(customDimensions["Tipologia"])
| order by p95_ms desc

// Volumetría
customEvents
| where timestamp > ago(1h)
| where name == "DocumentProcessed"
| summarize docs=count(), avg_total_ms=avg(todouble(customDimensions["DuracionTotalMs"])) by tostring(customDimensions["Tipologia"])
| order by avg_total_ms desc
```

**Acciones:**
1. **If local queue backpressure (LimiterWaitMs > 10s):**
   - Aumentar `Extraction__AzureContentUnderstanding__MaxConcurrentCalls` (de 4 a 6-8)
   - Aumentar `maxConcurrentActivityFunctions` (de 4 a 8)
   - Monitor recovery

2. **If Azure CU/DI slow (AnalysisMs > 60s):**
   - Contact Azure support → capacity planning
   - Temporary: reduce document size or split large docs
   - Check if specific tipología es inherently slower

3. **If large document:**
   - Review `Pipeline__MaxPaginasDocumento` setting
   - If > 500 pages, consider splitting
   - GPT fallback might be faster for very large docs

4. **If specific tipología slow:**
   - Investigate classification rules (maybe too complex?)
   - Consider dedicated DI model for that tipología
   - Registrar para revisión de rendimiento de esa tipología

**Escalation:**
- If P95 > 180s consistently → P3 → performance tuning
- If P95 increases over time → capacity planning needed

---

## Árbol de Diagnóstico

```
┌─ Documentos NO procesan (fallan)
│  ├─ Extraction error?
│  │  ├─ CU circuit abierto? → Incident #1
│  │  ├─ CU timeout? → Review timeout setting
│  │  ├─ Network issue? → Check connectivity
│  │  └─ DurableSerializationException? → Incident #6
│  │
│  ├─ Layout extraction (markdown) error?
│  │  ├─ File corrupted/unsupported? → Incident #5
│  │  ├─ DI endpoint down? → Check Azure DI status
│  │  └─ Network issue? → Check connectivity
│  │
│  ├─ Classification error?
│  │  ├─ DI endpoint down? → Check Azure DI status
│  │  ├─ Rule engine error? → Check logs
│  │  ├─ DurableSerializationException? → Incident #6
│  │  └─ GPT fallback error? → Check OpenAI quota
│  │
│  └─ Integration error?
│     ├─ GDC down? → Incident #3
│     ├─ DurableSerializationException? → Incident #6
│     ├─ Plugin critico fallo? → Check plugin logs
│     └─ Database error? → Incident #2
│
├─ Documentos quedan "Running" (stuck)
│  └─ Incident #4 (Orchestration Timeout)
│
└─ Documentos procesan pero LENTO (P95 > 60s)
   └─ Incident #7 (High Latency)
```

---

## Escalation Path

### Tier 1 — On-Call Engineer
**Responsabilidad:** Diagnóstico inicial, acciones inmediatas, mitigación temporal

**Acciones:**
- Ejecutar "Verificación Rápida" de arriba
- Identificar severidad (P1-P4)
- Implementar workaround temporal si aplica
- Registrar el incidente en el sistema de tickets

**Criterios de escalation a Tier 2:**
- Issue no resuelta en 15 min
- Severidad P1 o P2
- Requiere acceso Azure principal o cambios código

---

### Tier 2 — Tech Lead
**Responsabilidad:** Investigación profunda, cambios código/config, escalation Tier 3

**Acciones:**
- Ejecutar "Diagnóstico Profundo" (KQL queries)
- Revisar application logs en Application Insights
- Hacer cambios a app settings si necesario
- Coordinate con Azure support si aplica

**Criterios de escalation a Tier 3:**
- Issue requiere cambio código o IR
- Issue es Azure infrastructure (CU, DI, SQL down)
- > 30 min sin resolución

---

### Tier 3 — CTO / Architecture
**Responsabilidad:** Strategic decisions, external escalations, post-mortem

**Acciones:**
- Contactar Azure support (P1/P2)
- Authorize emergency deploys si necesario
- Lead post-incident review
- Approve changes a architecture

---

## Post-Incident Review

**Trigger:** Cualquier incident P1 o P2

**Dentro de 24h después resolución:**

```markdown
# Post-Incident Review — [Incident ID]

## Summary
- **What happened:** [1-2 sentence resumen]
- **When:** [Tiempo start/end, duración total]
- **Impact:** [Docs afectados, uptime loss, user impact]
- **Severity:** [P1/P2/P3/P4]

## Root Cause
[Análisis de qué causó el incident]

## Timeline
- T+0min: [Detección o alerta]
- T+5min: [Acción initial]
- T+10min: [Diagnóstico]
- T+20min: [Resolución]

## Resolution
[Qué se hizo para arreglarlo]

## Prevention
[Qué cambios previenen este incident en futuro]
- Action 1: [Responsable, fecha target]
- Action 2: [Responsable, fecha target]

## Lessons Learned
[Qué aprendimos, mejoras en runbook]
```

---

## Contactos de Escalation

| Rol | Contacto | Disponibilidad |
|-----|----------|----------------|
| **On-Call Engineer** | Responsable de operaciones de guardia | 24/7 |
| **Tech Lead** | Responsable técnico | En horario |
| **Dirección técnica / operaciones** | Escalado de decisión | En emergencias |
| **Azure Support** | Microsoft (support.microsoft.com) | Según contrato de soporte |
| **GDC Admin** | Administración del sistema GDC | En horario |

---

## Apéndice: Quick Commands

### Acceso a Recursos
```powershell
# Connect to Azure
az login

# Switch subscription
az account set --subscription "YOUR_SUBSCRIPTION_ID"

# Get app settings
az functionapp config appsettings list --resource-group SRBRGDOCSAIPROD --name srbappprodocai

# Get secrets from KeyVault
az keyvault secret show --vault-name srbkvprodocai --name "NOMBRE_SECRET"

# Restart Functions app
az functionapp restart --resource-group SRBRGDOCSAIPROD --name srbappprodocai

# Stop Functions app
az functionapp stop --resource-group SRBRGDOCSAIPROD --name srbappprodocai

# Start Functions app
az functionapp start --resource-group SRBRGDOCSAIPROD --name srbappprodocai
```

### AppInsights KQL Quick Access
```kusto
// All errors last 24h
traces
| where timestamp > ago(24h)
| where severityLevel >= 2
| summarize count=count() by bin(timestamp, 1h)
| order by timestamp desc

// Circuit breaker events
customEvents
| where name startswith "CU.Circuit"
| project timestamp, name, reason=customDimensions["reason"]
| order by timestamp desc
```

---

## Validación

- Incidents mapeados desde código (IntegrarActivity.cs, AzureContentUnderstandingProvider.cs)
- Procedimientos basados en la configuración actual (CU timeout 90s, retry 3x, circuit threshold 5)
- Escalation path con tiers y contactos genéricos por rol
- Post-incident review template incluida
