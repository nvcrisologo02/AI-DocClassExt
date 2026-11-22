# Runbook de Incidentes — Producción SRBRGDOCSAIPROD

> Última revisión: 2026-11-22 (AB#101447, contraste con `src/backend/DocumentIA.Functions` y `DocumentIA.Core`, `azure-pipelines.yml`, CONTRATO_API_HTTP.md §5 e INFRAESTRUCTURA_REAL_DESPLEGADA.md). Corregidos: el endpoint `force-fallback`, `OrquestadorActivity`, `ObtenerMarkdownActivity_Failed` y `GetMetadata` (no existen); `TimeoutSeconds` pasa a `HardTimeoutSeconds`; los settings `Extraction__GptFallback__*` y la conexión `Extraction__AzureContentUnderstanding__*` son heredados sin efecto (no se leen en código; la conexión, el modelo y los umbrales de IA salen de `ModeloConfigs` por alias e identidad administrada); `PENDIENTE_REINTENTO` por 429 de Azure OpenAI no se persiste (AB#100178), el de Layout sí. Los valores que fija `azure-pipelines.yml` se restablecen en cada despliegue: un cambio manual de app settings dura hasta el siguiente. Los contactos de escalado siguen sin rellenar y desde el repositorio no consta quién debe aportarlos. Los comandos usan los nombres de PRO; DEV y PRE (`SRBRGDEVDOCSAI`/`srbappdevdocai`, `SRBRGPREDOCSAI`/`srbapppredocai`) siguen el mismo patrón (ver INFRAESTRUCTURA_REAL_DESPLEGADA.md). Alertas desplegadas, permisos, secretos de Key Vault y comandos `az`: no comprobado en la revisión del 2026-11-22.

**Versión:** 1.1  
**Última actualización:** 2026-11-22  
**Aplicable a:** SRBRGDOCSAIPROD (los demás entornos siguen el mismo patrón de nombres)  
**Fuente:** Código verificado en Activities + Providers

---

## 📋 Tabla de Contenidos

1. [SLAs & Severidad](#slas--severidad)
2. [Incidentes Comunes](#incidentes-comunes)
3. [Árbol de Diagnóstico](#árbol-de-diagnóstico)
4. [Procedimientos por Incidente](#procedimientos-por-incidente)
5. [Escalation Path](#escalation-path)
6. [Post-Incident Review](#post-incident-review)

---

## SLAs & Severidad

### Definición de Severidad

| Severidad | P95 Latencia | Availability | SLA Response | Escalation |
|-----------|---|---|---|---|
| **P1 — Crítico** | > 180s o no responde | < 90% | ≤ 15 min | CTO + Azure Support |
| **P2 — Alto** | 60-180s | 90-95% | ≤ 30 min | Tech Lead + Azure Support |
| **P3 — Medio** | 30-60s | 95-99% | ≤ 2h | On-call Engineer |
| **P4 — Bajo** | < 30s o degradación | > 99% | ≤ 1 día | Backlog item |

---

## Detección Automática (alertas de Azure Monitor)

Desde 2026-08-04 (AB#99083) existen alert rules sobre `srbappiprodocai` que **notifican por correo** al action group `srbagoperprodocai`: las 5 de negocio (err, lat, fbk, exc, idle), las de memoria y OOM (2026-10-01) y las 3 de trazas de AB#100181 (`srbalertdupprodocai`, `srbalertexpprodocai`, `srbalertsctprodocai`; fuente de verdad en [MONITOREO_ALERTAS_REAL.md](../observabilidad/MONITOREO_ALERTAS_REAL.md), estado vivo en Azure no comprobado en la revisión del 2026-11-22). Al recibir un aviso, entrar por el incidente correspondiente:

| Correo de alerta | Qué significa | Entrada al runbook |
|------------------|---------------|--------------------|
| `srbalerterrprodocai` (errores > 10%) | Documentos terminando en Error/Fallido | Árbol de diagnóstico → incidentes de extracción/persistencia |
| `srbalertlatprodocai` (p95 > 120 s) | Latencia E2E excesiva | Incident #7 (High Latency) |
| `srbalertfbkprodocai` (fallback > 20%) | CU degradado, GPT asumiendo extracción | Incidente CU (circuit breaker / timeout) |
| `srbalertexcprodocai` (> 10 excepciones/5 min) | Fallo transversal (incluye GDC) | Verificación Rápida + Failures en App Insights |
| `srbalertidleprodocai` (0 requests en horario laboral) | Function App caída o sin flujo de entrada | Verificación Rápida (disponibilidad) |
| `srbalertmemprodocai` (working set por instancia > 2,25 GiB, desde 2026-10-01) | Instancia cerca del techo del heap .NET (~75 % de 3,5 GB en EP1); típico de lotes con PDF grandes | Revisar si hay un lote de Batch en curso y si aparecen `OutOfMemoryException` (AB#100814) |
| `srbalertoomprodocai` (cualquier `OutOfMemoryException`, Sev 1, desde 2026-10-01) | Una orquestación falló por memoria; el documento suele terminar OK al reintentarse | Localizar el documento por la traza `Fichero subido a blob` previa en la misma instancia y comprobar su estado en `Documentos` (AB#100814) |

Gestión de reglas y **alta de nuevos correos de aviso**: `docs/observabilidad/MONITOREO_ALERTAS_REAL.md` (script `scripts/observability/create-monitor-alerts.ps1`).

---

## Incidentes Comunes

### 1️⃣ Azure Content Understanding (CU) Circuit Breaker / Timeout
**Patrón:** Múltiples fallos consecutivos en extracción, `TaskCanceledException` en WaitForCompletionAsync

**Síntomas:**
- ❌ Documentos fallan en extraction phase con `Hard timeout en Azure Content Understanding`
- 🔴 AppInsights: evento `CU.CircuitOpen` o `TaskCanceledException`
- ⏱️ P95 extraction > 90s o CU tarda > 90s (default timeout)
- 📊 Métrica `CU.CircuitRejected` > 0
- 🟡 `GDC health probe timeout` log (indica problema de red más amplio). La traza solo se emite cuando alguien invoca `POST /api/healthcheck`: el probe tiene timeout de 30 s, sale de `SystemHealthService` y el resultado se cachea 45 s; sin polling no hay traza
- 📊 Retry 2/3, 3/3 fallando consecutivamente

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
   - **NEW:** Revisar log `GDC health probe timeout` en últimas 2h:
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
   - La autenticación de CU sale de `ModeloConfigs` por alias con identidad administrada, no de una cadena de conexión: verificar el rol de la MI sobre el recurso de IA y el alias en `ModeloConfigs`

5. **Temporal workaround (mientras Azure responde):**
   - No hay conmutador manual a GPT (el endpoint `force-fallback` que citaba este runbook no existe). El failover de CU primario a secundario es automático (`CU.CircuitFailover`) si la tipología tiene `secondaryModelKey`
   - `Extraction__GptFallback__Enabled` es un setting heredado sin efecto (no se lee en código): no sirve como remedio
   - Aumentar `Extraction__AzureContentUnderstanding__HardTimeoutSeconds` desde 90 a 180 (si es timeout genuino de Azure CU); el pipeline lo fija en cada despliegue, así que un cambio manual se pierde en el siguiente

**Escalation:**
- Si GDC probe timeout > 5 min → **P2 — Network/Infrastructure issue** → Tech Lead + Network team
- Si circuit abierto > 30 min → **P2** → Tech Lead + Azure Support
- Si circuit abierto > 2h → **P1** → CTO + Priority Azure Support

---

### 2️⃣ Database Connection Timeout o Unavailable
**Patrón:** Documentos fallan en persistencia, Durable Functions timeout

**Síntomas:**
- ❌ Activities fallan con `SqlConnectionString` error
- 📊 AppInsights: `Microsoft.EntityFrameworkCore` errors
- ⏱️ P95 "Persistencia" > 30s
- 🔴 Durable Functions orchestration cancela después retry timeout

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

# Test via sqlcmd (el valor de SqlConnectionString en PRO es una referencia a Key Vault; usar token de Entra, no contraseña en claro)
sqlcmd -S "srbsqlprodocai.database.windows.net" -d "DocumentIA" -G -Q "SELECT 1"
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
   - Aumentar `Connection Timeout=120` (default 30) dentro del secreto `SqlConnectionString` de Key Vault (el host de Functions no carga `appsettings.json`)
   - Reducir `maxConcurrentActivityFunctions` temporalmente (del 4 al 2): vive en `host.json` (`extensions.durableTask`), así que requiere redeploy; la variante por app setting `extensions__durableTask__maxConcurrentActivityFunctions` no comprobado en la revisión del 2026-11-22
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

### 3️⃣ GDC Integration (Document Management System) Failures
**Patrón:** Documentos clasificados pero no integran con GDC

**Síntomas:**
- ✅ Extracción y clasificación OK
- ❌ Falla en `IntegrarActivity`
- 📊 Event `plugin critico X fallo. Deteniendo cadena`
- 🔴 AppInsights: evento de `IntegrarActivity` con error

**Causas Raíz Posibles:**
- GDC HTTP Basic Auth credentials inválidas
- GDC endpoint down (legacy SOAP service)
- SSL certificate validation (GDC bypass SSL?)
- Request format cambió (GDC API incompatible)

**Verificación Rápida (2 min):**
```powershell
# Test GDC connectivity
# PROD apunta a la réplica srbwidp05 (primario srbwidp04); en dev el host es srbwidd03
$gdcEndpoint = "https://srbwidp05.sareb.srb:8090/sintws/IDocService"
$username = (az keyvault secret show --vault-name srbkvprodocai --name "GDC--HttpBasicUsername" --query "value" -o tsv)
$password = (az keyvault secret show --vault-name srbkvprodocai --name "GDC--HttpBasicPassword" --query "value" -o tsv)

$auth = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("$username`:$password"))
$headers = @{ "Authorization" = "Basic $auth" }

try {
  # GDC es SOAP (IDocService); no hay GET /GetMetadata. El sondeo real es consultarObjetoExiste via POST /api/healthcheck (components.gdc)
  $response = Invoke-WebRequest -Uri "https://srbappprodocai.azurewebsites.net/api/healthcheck" -Method Post -TimeoutSec 60
  Write-Output "✅ GDC respondiendo: $($response.StatusCode)"
} catch {
  Write-Error "❌ Error GDC: $($_.Exception.Message)"
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
   - GDC tiene certificado autofirmado → `GDC__BypassSslValidation=true` está en los tres entornos (`azure-pipelines.yml`) y se aplica en cada despliegue
   - Si falta, agregar setting y redeploy

5. **Temporal workaround:**
   - Deshabilitar plugin GDC en orchestration (marcar como optional)
   - Enqueue documentos en cola para retry manual

**Escalation:**
- Si GDC down > 1h → Contactar GDC support (no es issue de DocumentIA)
- Si credential issue no resuelve → P2 → Tech Lead + SecOps (para rotation)

---

### 4️⃣ Durable Functions Orchestration Timeout o Stuck
**Patrón:** Documentos quedan "Running" indefinidamente

**Síntomas:**
- ❌ Documentos en estado "Running" > 1h
- 📊 AppInsights: orchestration timeout logs
- 🔴 Durable Functions history muestra "Pending" activities
- ⏱️ No hay eventos de "DocumentProcessed" en últimas 2h

**Causas Raíz Posibles:**
- Activity function deadlock (espera que nunca llega)
- Orchestrator bloqueado (no puede ejecutar siguiente activity)
- Durable Storage (AzureWebJobsStorage) inaccesible
- Concurrency limit reached: `maxConcurrentActivityFunctions=4` (cola infinita)

**Verificación Rápida (1 min):**
```powershell
# Ver instancias en ejecución (API de Durable; `az functionapp function list` no devuelve instancias y `OrquestadorActivity` no existe: el orquestador es DocumentProcessOrchestrator)
$running = Invoke-RestMethod -Uri "https://srbappprodocai.azurewebsites.net/runtime/webhooks/durableTask/instances?runtimeStatus=Running" -Headers @{ "x-functions-key" = $functionKey }

# Si hay muchas "Running" => possible deadlock
Write-Output "Instancias Running: $(@($running).Count)"
```

**Diagnóstico Profundo:**
```kusto
// Documentos stuck en "Running": DocumentProcessed se emite al persistir, así que un documento atascado nunca lo tiene;
// usar la API de instancias Durable (runtimeStatus=Running) o el Durable Functions Monitor. Esta consulta cuenta lo que SÍ terminó:
customEvents
| where timestamp > ago(2h)
| where name == "DocumentProcessed"
| summarize docs=count() by tostring(customDimensions["EstadoFinal"]), tostring(customDimensions["Tipologia"])

// Ver fases que más tiempo tardan (sufijos: Total, Clasificacion, Extraccion, Validacion, GDC, Integracion, Persistencia)
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

### 5️⃣ Document File Corruption en Azure Document Intelligence (DI)
**Patrón:** Documento procesa N veces OK, pero falla en ObtenerMarkdownActivity (Document Intelligence Layout) con error "file is corrupted"

**Síntomas:**
- ❌ `ObtenerMarkdownActivity` falla con HTTP 400 al resolver markdown vía Layout
- 📊 Error: `"The file is corrupted or format is unsupported. Refer to documentation for the list of supported formats."`
- ⏱️ En `AzureDocumentIntelligenceLayoutMarkdownProvider.cs` (los `throw` están hoy en las líneas 104 y 111; ante `InvalidContent` con `urlSource` el proveedor reintenta con `base64Source`, así que el 400 que ve el operador es el fallo tras ambos intentos)
- 🔴 Mensaje: "Error iniciando DI layout. Status=400"
- ✅ El mismo documento procesa N veces sin problema (indica corrupto en esta instancia específica)

**Cómo se ve en la salida (desde AB#100880):** cualquier fallo al obtener el texto cierra la ejecución con `MensajeError = "No se pudo obtener el texto del documento: <motivo>..."`, persistida y visible en el Monitor. `SIN_CONTENIDO_DOCUMENTO` queda reservado al documento que de verdad no tiene texto (Layout respondió vacío) y **no** es un incidente de infraestructura.

| Motivo | Estado | Acción del operador |
|---|---|---|
| `LayoutTimeout`, `LayoutError (HTTP 429/5xx)` | `PENDIENTE_REINTENTO` | Reintentar más tarde; si persiste, revisar salud y timeout del recurso de DI |
| `LayoutError (HTTP 400)` con `InvalidContent` | `ERROR` | Este incidente: blob no descargable por DI o contenido corrupto (pasos de abajo) |
| `LayoutError (HTTP 401/403)` | `ERROR` | Credenciales o rol del recurso de DI en `ModeloConfigs`; no reclamar al remitente |
| `FormatoNoSoportado` (HTTP 415) | `ERROR` | Pedir el documento en un formato soportado |
| `SinFuente` | `ERROR` | La ejecución no tenía blob ni base64: revisar subida a blob y trigger |
| `DocumentoSinTexto` | `SIN_CONTENIDO_DOCUMENTO` | Escaneado sin OCR o página en blanco: acción del remitente, no de plataforma |

Detalle de diagnóstico por motivo en [TROUBLESHOOTING_DIAGNOSTICO.md](../guias/TROUBLESHOOTING_DIAGNOSTICO.md), caso "No se pudo obtener el texto del documento".

**Causas Raíz Posibles:**
- El blob (`BlobPath`, vía principal desde blob-first) está **incompleto o corrupto**; el base64 solo es fallback legado y el trigger lo pone a `null` tras volcar el contenido a blob
- PDF tiene sectores corruptos (descarga interrumpida, transmisión fallida)
- Encoding/decoding base64 incorrecto en el cliente (solo en la vía legada)
- Blob Storage descargó documento incompleto: comparar el tamaño del blob con el original
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
// Documentos específicos con este error (no existe el evento ObtenerMarkdownActivity_Failed; se usa la traza del MarkdownResolver)
traces
| where timestamp > ago(2h)
| where message startswith "Layout fallo para"
| where message contains "corrupted" or message contains "InvalidContent"
| project timestamp, operation_Id, message
| order by timestamp desc
| take 20
// Alternativa en BD: DocumentoEjecuciones.EstadoFinal = 'ERROR' y MensajeError que empieza por 'No se pudo obtener el texto del documento:'
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
     // ✅ Correcto
     string base64 = Convert.ToBase64String(documentBytes);
     
     // ❌ Si es incompleto o mal formado
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

### 6️⃣ DurableSerializationException en Activities
**Patrón:** Documentos fallan en serialización Durable Functions

**Síntomas:**
- ❌ Activity falla con `DurableSerializationException`
- 📊 AppInsights: `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` error
- ⏱️ Error en el executor del worker de Durable (la cita a `DurableFunctionExecutor.Activity.cs:39` es histórica; ese fichero no está en el repositorio)
- 🔴 Stacktrace menciona "RunActivityAsync" o "OutputBindingsMiddleware"

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
     // ❌ Problemático
     Dictionary<string, object> datos = new()
     {
         ["field"] = someComplexObject,  // ← Culpable
         ["array"] = new[] { 1, 2, 3 }   // ← Arrays pueden fallar
     };
     
     // ✅ Solución
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

### 7️⃣ High Latency on Classification or Extraction
**Patrón:** P95 latencia > 60 segundos (pero sin errores)

**Síntomas:**
- ✅ Documentos procesados correctamente
- ⏱️ P95 "Total" > 60s o P95 "Clasificacion" > 30s
- 📊 Workbook "Diagnóstico Rápido" muestra Wait > 10s o Analysis > 60s
- 💾 User reports "lento"

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
| summarize docs=count() by tostring(customDimensions["Tipologia"])
| join kind=leftouter (
    customMetrics
    | where timestamp > ago(1h)
    | where name == "DocumentIA.Duracion.Total"
    | summarize avg_total_ms=avg(value) by Tipologia=tostring(customDimensions["Tipologia"])
) on Tipologia
| order by avg_total_ms desc
// DocumentProcessed no lleva DuracionTotalMs: las duraciones son las métricas DocumentIA.Duracion.*
```

**Acciones:**
1. **If local queue backpressure (LimiterWaitMs > 10s):**
   - Aumentar `Extraction__AzureContentUnderstanding__MaxConcurrentCalls` (de 4 a 6-8); el pipeline lo restablece a 4 en el siguiente despliegue
   - Aumentar `maxConcurrentActivityFunctions` (de 4 a 8) en `host.json` (requiere redeploy)
   - Monitor recovery

2. **If Azure CU/DI slow (AnalysisMs > 60s):**
   - Contact Azure support → capacity planning
   - Temporary: reduce document size or split large docs
   - Check if specific tipología es inherently slower

3. **If large document:**
   - Review `Pipeline__MaxPaginasDocumento` setting
   - If > 500 pages, consider splitting
   - Que un fallback a GPT sea más rápido en documentos muy grandes no está medido (no comprobado en la revisión del 2026-11-22)

4. **If specific tipología slow:**
   - Investigate classification rules (maybe too complex?)
   - Consider dedicated DI model for that tipología
   - Add to performance improvement backlog

**Escalation:**
- If P95 > 180s consistently → P3 → performance tuning
- If P95 increases over time → capacity planning needed

---

### 8️⃣ Azure OpenAI (AOAI) Rate Limit / Cuota Agotada (429) en Clasificación GPT y Prompts
**Patrón:** Errores 429 sostenidos de Azure OpenAI; el circuit breaker abre para proteger el recurso; documentos de clasificación GPT quedan en estado retriable

> Matiz (revisión 2026-11-22): por 429, `PENDIENTE_REINTENTO` va solo en la respuesta (`Resultado.Estado`) y **no se persiste** en `DocumentoEjecuciones` (decisión AB#100178, evita una fila por reintento); tampoco genera `DocumentProcessed`. El cliente debe reenviar la petición: no hay cola interna de reintento. Solo el `PENDIENTE_REINTENTO` por fallo transitorio de Layout (incidente 5) sí se persiste.

**Impacto:** La clasificación de los documentos afectados se **pospone** (estado retriable `PENDIENTE_REINTENTO`), **no se pierden ni terminan en error crudo**. Los prompts (enriquecimiento) se degradan de forma no bloqueante. El circuit breaker protege el recurso Azure OpenAI durante el cooldown; no requiere intervención de emergencia salvo que se prolongue.

**Síntomas:**
- ❌ Documentos de clasificación GPT terminan con `Estado = "PENDIENTE_REINTENTO"` (no error crudo), `EstadoCalidad = "ERROR"`, `MensajeError = "Clasificación pospuesta: cuota de Azure OpenAI agotada (429). Reintentar más tarde."`
- 🔴 AppInsights: eventos `AOAI.CircuitOpen` y `AOAI.CircuitRejected` frecuentes
- 📊 Evento `AOAI.RateLimitRetry` en aumento (reintentos por 429/500/502/503/504)
- 🟡 Prompts devuelven `PromptResultado.Error` con prefijo `rate_limit_exhausted:` (degradado, no escala el documento a `PENDIENTE_REINTENTO`)
- ℹ️ `DetalleEjecucion.Seguimiento.Estado = "PendienteReintento"` acompaña a `Resultado.Estado = "PENDIENTE_REINTENTO"`; la instancia queda en `runtimeStatus = Completed`

**No confundir con otros estados de `Resultado.Estado`** (cierran con `runtimeStatus = Completed`, no son 429):
- `PAGINAS_EXCEDIDAS` → el documento supera `maxPages`, `EstadoCalidad = "REVISION"` (ver `docs/especificaciones/ESPECIFICACION_LIMITE_PAGINAS_DOCUMENTO.md`)
- `EXTRACCION_INCOMPLETA` → falló Content Understanding, se usó el fallback y no se obtuvo ningún campo útil
- `ERROR` → excepción capturada por el orquestador; `runtimeStatus = Failed` queda para input nulo, excepción en el preámbulo o fallo de infraestructura
- Lista completa de estados (10, cadenas literales sin enum en código): CONTRATO_API_HTTP.md §5; faltaban aquí `NO_CLASIFICADO`, `DUPLICADO` (solo con `Dedup__ModoEstricto=false`), `BAJA_CONFIANZA_CLASIFICACION`, `VALIDACION_CON_ERRORES` y `SIN_CONTENIDO_DOCUMENTO`

**Causas Raíz Posibles:**
- Cuota/TPM (tokens-por-minuto) del deployment de Azure OpenAI agotada por volumen de documentos
- Spike de volumen de clasificación GPT + prompts, que comparten el mismo recurso (`circuitKey = "{endpoint}|{deployment}"`)
- `CircuitBreakerFailureThreshold` (default 5) insuficiente para el patrón de tráfico actual
- Incidencia de servicio en Azure OpenAI (no solo cuota)

**Verificación Rápida (1 min):**
```kusto
customEvents
| where timestamp > ago(10m)
| where name in ("AOAI.CircuitOpen", "AOAI.CircuitRejected", "AOAI.RateLimitRetry")
| project timestamp, name, circuitKey=tostring(customDimensions["circuitKey"]), statusCode=tostring(customDimensions["statusCode"]), attempt=tostring(customDimensions["attempt"])
| order by timestamp desc
| take 20
```

**Diagnóstico Profundo:**
```kusto
// Documentos pospuestos por cuota agotada: el 429 no se persiste ni emite DocumentProcessed, así que se cuenta por la traza
traces
| where timestamp > ago(2h)
| where message startswith "Clasificación pospuesta por rate limit (429)"
| summarize docs=count() by bin(timestamp, 15m)
| order by timestamp desc

// Evolución de reintentos y aperturas/cierres de circuito
customEvents
| where timestamp > ago(2h)
| where name startswith "AOAI."
| summarize count=count() by name, bin(timestamp, 10m)
| order by timestamp desc
```
Ver `docs/observabilidad/OBSERVABILIDAD_KQL.md` para más queries de circuit breaker (patrón análogo al de Content Understanding, sección `CU.Circuit*`).

**Acciones:**
1. **Inmediato (≤ 5 min):**
   - Confirmar con la query de Verificación Rápida que el patrón es 429 sostenido (no otra incidencia de Azure OpenAI)
   - Revisar cuota/TPM asignada al deployment en Azure OpenAI Studio y compararla con el volumen actual de clasificación GPT + prompts
   - Confirmar si clasificación y prompts comparten `circuitKey` (mismo endpoint/deployment) — si es así, ambos se degradan a la vez

2. **Si la cuota es insuficiente para el volumen:**
   - Solicitar aumento de cuota/TPM del deployment de Azure OpenAI (Azure Portal o soporte Azure)
   - Mientras se gestiona, dejar que el circuit breaker proteja el recurso; no forzar reintentos manuales masivos

3. **Esperar recuperación:**
   - El circuito cierra automáticamente al expirar el cooldown (`CircuitBreakerOpenSeconds`, default 45s) con la siguiente llamada exitosa (evento `AOAI.CircuitClosed`)
   - No requiere reinicio de la Function app

4. **Reprocesar documentos `PENDIENTE_REINTENTO`:**
   - Verificar que ya no se generan nuevos eventos `AOAI.CircuitOpen` / `AOAI.RateLimitRetry`
   - El cliente reenvía la petición de los documentos que recibieron `PENDIENTE_REINTENTO`: no hay cola interna ni filas que reprocesar (AB#100178)

5. **Si el patrón se repite con frecuencia (tuning):**
   - Ajustar los app settings `AzureOpenAIResilience__*` (`CircuitBreakerFailureThreshold`, `CircuitBreakerOpenSeconds`, `MaxRetries`, `InitialRetryDelayMs`, `MaxRetryDelaySeconds`; el host no carga `appsettings.json` y el pipeline no los fija, así que rigen los defaults de código: reintentos 3, umbral 5, apertura 45 s) — ver `docs/manuales/MANUAL_CONFIGURACION.md`
   - Evaluar reducir concurrencia de llamadas a Azure OpenAI o distribuir el volumen en el tiempo

**Rollback:**
- Para desactivar la lógica de resiliencia 429 y volver al comportamiento previo (retry por defecto del SDK, sin estado retriable diferenciado: el error crudo del SDK ante 429 acaba como `Resultado.Estado = "ERROR"` con `runtimeStatus = Completed`, no como HTTP 429 ni como `Failed`): app settings `AzureOpenAIResilience__MaxRetries=0` y `AzureOpenAIResilience__EnableCircuitBreaker=false`

**Escalation:**
- Si `PENDIENTE_REINTENTO` persiste > 30 min tras confirmar cuota disponible → P2 → Tech Lead
- Si requiere aumento de cuota/TPM en Azure OpenAI → coordinar con Azure Support (tiempos de aprovisionamiento variables)
- Si afecta > 1h de forma sostenida al volumen de producción → P1 → CTO + Azure Support

---

### 9️⃣ Réplica de analizador CU a West Europe (Copy API cross-resource bloqueada entre las dos cuentas de PRO)

> Alcance (revisión 2026-11-22): la limitación se da entre las dos cuentas de PRO (`upe48-mm2avmdm-swedencentral` y `srbaisrv-westeurope`). La Copy API sí funciona de PRO a DEV y PRE (manifiestos `copied` del 2026-09-21 en `infra/ai/`); el procedimiento vigente es [DESPLIEGUE_IA.md](DESPLIEGUE_IA.md).

**Síntomas:**
- `scripts/deployment/copy-cu-analyzer.ps1` (cross-resource) falla en `:copy` con `ModelNotFound / "has not granted the necessary permissions"`, aunque `grantCopyAuthorization` responda `200`.

**Causa (verificado 2026-07-17):** la Copy API cross-resource de CU **no funciona en este entorno** (el grant devuelve un stub sin `source`; `:getCopyAuthorization` da `404`). No se resuelve con permisos: persiste incluso con `Cognitive Services User` de la MI del destino sobre el origen. Probable limitación con analizadores project-scoped de Foundry.

**Resolución:** replicar **reconstruyendo/reentrenando** en el destino:
```powershell
./scripts/deployment/recreate-cu-analyzer.ps1 -SourceAnalyzerId <id> -ResourceGroup SRBRGDOCSAIPROD -SyncDefaults
```
Requiere que la MI de `srbaisrv-westeurope` tenga `Storage Blob Data Reader` sobre `srbstgproapppdocai` (ya concedido). Validar con documentos de prueba antes de repuntar `modelKey` en `ModeloConfigs`. Detalle: `TROUBLESHOOTING_DIAGNOSTICO.md` CASO 8 y guía de extracción CU §12.

**Escalation:** si se requiere el snapshot exacto vía Copy API → **caso de soporte Azure** (P3) con la evidencia (grant sin `source`, token-flow 404). No bloquea producción: el secundario es failover.

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
│  │  ├─ Error del clasificador GPT (alias en ModeloConfigs)? → Check OpenAI quota
│  │  └─ Documento en PENDIENTE_REINTENTO / 429 Azure OpenAI? → Incident #8
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
- Log en ticket (Azure DevOps Work Item)

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

| Rol | Nombre | Email | On-Call |
|-----|--------|-------|---------|
| **On-Call Engineer** | TBD | TBD | ✅ Sí |
| **Tech Lead** | TBD | TBD | En horario |
| **CTO** | TBD | TBD | En emergencias |
| **Azure Support** | Microsoft | support.microsoft.com | - |
| **GDC Admin** | TBD | TBD | En horario |
| **Soporte de red (Network team)** | TBD | TBD | En horario |

> Pendiente de rellenar (revisión 2026-11-22): desde el repositorio no consta quién debe asignar los contactos de On-Call, Tech Lead, CTO, GDC Admin ni red, y el responsable de completarlos no está identificado. Hasta entonces el escalado P1/P2 no tiene destinatario nominal. No se inventan datos.

---

## Apéndice: Quick Commands

### Acceso a Recursos
```powershell
# Connect to Azure
az login

# Switch subscription
az account set --subscription "647c7246-54bc-4d31-b909-431cacf03272"   # PRO (Producción Central); DEV y PRE en INFRAESTRUCTURA_REAL_DESPLEGADA.md

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

- Incidentes contrastados con el código el 2026-11-22 (ver la nota «Última revisión» de la cabecera).
- Procedimientos basados en la configuración del pipeline (CU hard timeout 90 s, 3 reintentos, umbral de circuito 5).
- El escalado tiene niveles pero sin contactos nominales (pendiente sin responsable identificado).
- Plantilla de post-incident review incluida.

**Próximas actualizaciones:** contactos reales; refinar con los incidentes históricos
