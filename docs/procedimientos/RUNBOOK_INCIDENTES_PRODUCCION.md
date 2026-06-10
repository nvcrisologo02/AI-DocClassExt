# Runbook: Incidentes en Producción — DocumentIA

## 0. GUÍA RÁPIDA (Primeros 5 minutos)

### Acciones Inmediatas
1. Abrir Azure Portal
2. Navegar a Application Insights dashboard
3. Verificar "Health Status" en Workbook "DocumentIA Overview"
4. Si es rojo: Escalar a lead dev

### Escalation Matrix
- Lead Dev: Oncall rotation (ver TEAM_MATRIX)
- Arquitecto: Si es architectural issue
- Microsoft Support: Si es problema de Azure service

---

## 1. CLASIFICACIÓN LENTA (Latency > 30 sec)

### Síntomas
- Users reportan "documento tarda demasiado"
- P99 latency en AppInsights > 30s
- Algunos documentos ok, otros lentos

### Diagnóstico (10 min)

**Paso 1: Verificar actividad lenta**
```kql
customMetrics
| where name == "ActivityDuration"
| where tostring(customDimensions["Activity"]) in ("ClassifyActivity", "NormalizeActivity", "ExtractActivity")
| summarize avg_duration=avg(value), max_duration=max(value) by Activity=tostring(customDimensions["Activity"])
| order by avg_duration desc
```

**Paso 2: Verificar plugin latency**
```kql
customMetrics
| where name == "ProviderCallDuration"
| summarize avg_latency=avg(value), calls=dcount(tostring(customDimensions["Provider"])) by Provider=tostring(customDimensions["Provider"])
| order by avg_latency desc
```

### Causas Comunes & Soluciones

| Causa | Síntoma | Solución Inmediata | Solución Permanente |
|-------|--------|-------------------|-------------------|
| **CU throttled (429)** | ClassifyActivity lento | Esperar 1-2 min, auto-retry | Aumentar CU tier o batch calls |
| **DB connection pool exhausted** | PersistActivity lento | Restart Functions | Aumentar pool size en connection string |
| **Large document** | NormalizeActivity lento | Esperar completion | Increase activity timeout |
| **Network lag** | Todos lentos | Check VPN/network | Review network topology |

### Si CU está throttled (429):
1. Ver `docs/referencias/THIRD_PARTY_SLAS.md` para contacto CU
2. Escalar a Microsoft support si es SLA violation
3. Temporary: Reduce concurrent calls (config host.json)
4. Permanent: Upgrade CU tier

### Si DB conexiones agotadas:
1. SSH a Function App (Kudu)
2. Run: `dotnet trace ps` → buscar open connections
3. Si Pool agotada:
   - Restart all Function App instances
   - Aumentar connectionstring pool size (actual: 100, aumentar a 200)
4. Monitorear próximas 30 min para recurrencia

### Si documento muy grande:
1. Verificar size: `Get-AzStorageBlob -Container ... | where Name -eq $docName`
2. Si > 100MB: Known limitation, document in TROUBLESHOOTING
3. Aumentar timeout en host.json (current: 5 min, aumentar a 10 min)

---

## 2. ERROR RATE SPIKE (> 5% failures)

### Síntomas
- AppInsights muestra error rate jump
- Usuarios reportan failures aleatorios
- Algunos documentos fallan, otros ok

### Diagnóstico (5 min)

```kql
exceptions
| summarize error_count=dcount(itemId), by problemId, exceptionType
| order by error_count desc
| limit 10
```

### Causas Comunes

| Error | Causa Probable | Fix |
|-------|---|---|
| `TimeoutException` en ClassifyActivity | Plugin lento o network | Aumentar timeout (ver sección anterior) |
| `StorageException` | Blob access denied o no existe | Verificar permisos, recrear blob si necesario |
| `SqlException` "connection timeout" | DB overloaded o networking issue | Restart Functions, check DB performance |
| `NullReferenceException` en Extract | Formato inesperado en documento | Add to test suite, fix in code |
| `JsonSerializationException` | Provider response changed | Check provider API changes |

### Resolución por Tipo

**Para TimeoutException:**
```kql
traces
| where message like "ActivityTimeout%"
| summarize count() by Activity=tostring(customDimensions["Activity"])
```
→ Aumentar timeout en host.json para ese activity

**Para StorageException:**
```powershell
# Verificar blob existe
Get-AzStorageBlob -Container "documentos" -Blob "problematic-blob" -Context $ctx
# Si no existe, re-upload
```

**Para SqlException:**
1. Check SQL Server CPU/DTU en Azure Portal
2. Si > 80% DTU: Upgrade SQL tier
3. Check connection strings, verify firewall rules

---

## 3. CIRCUIT BREAKER ABIERTO (Plugin no responde)

### Síntomas
- Classifications fallando con "circuit breaker open"
- No se llama al provider por X horas
- Manual override necesario para recuperar

### Diagnóstico (2 min)

```kql
customEvents
| where name == "CircuitBreakerStateChange"
| order by timestamp desc
| limit 20
```

### Causas

- Provider completamente caído (CU, OpenAI outage)
- Rate limit agresivo (429 responses)
- Auth token expirado
- Network connectivity issue

### Resolución

**Para CU outage:**
1. Verificar Azure status: https://status.azure.com
2. Si CU down: Fallback a GPT automático (código lo hace)
3. Si persiste > 30 min: Abrir Microsoft support ticket
4. Monitor via: `docs/referencias/THIRD_PARTY_SLAS.md`

**Para auth token expirado:**
1. Verificar en Key Vault:
   ```powershell
   Get-AzKeyVaultSecret -VaultName "documentia-kv" -Name "CU-ApiKey" | Select-Object @{N="Expires";E={$_.Expires}}
   ```
2. Si expirado: Regenerate key, update Key Vault
3. Restart Functions para reload secret

**Para manual recovery:**
1. En database, encontrar documento stuck:
   ```sql
   SELECT TOP 10 DocumentoId, EstadoEjecucion, FechaInicio 
   FROM DocumentoEjecuciones 
   WHERE EstadoEjecucion = 'CircuitBreakerOpen' 
   ORDER BY FechaInicio DESC
   ```
2. Cambiar estado a "PendingRetry" para re-process
3. Monitorear siguientes 10 min

---

## 4. DOCUMENTOS STUCK (En queue por horas)

### Síntomas
- Documentos en estado "Queued" o "Processing" > 2 horas
- No hay errores, pero tampoco avanza
- Reintento manual no ayuda

### Diagnóstico (5 min)

```kql
customEvents
| where name == "DurableOrchestrationStatus"
| where customDimensions["Status"] == "Running"
| where timestamp < ago(2h)
| summarize count() by DocumentoId=tostring(customDimensions["DocumentoId"])
```

### Causas Comunes

- Activity en infinite loop (revisión de código)
- Deadlock en DB (rare pero posible)
- Out of memory en Function instance
- Durable Functions runtime issue

### Soluciones

**Para infinite loop:**
1. Restart Functions instance (Kudu → Restart)
2. Documento se reprocessará desde checkpoint
3. Fix código, deploy fix, re-process

**Para DB deadlock:**
1. Kill blocker query:
   ```sql
   EXEC sp_who2
   -- Buscar BLOCKED>0
   -- KILL <spid> para killer process
   ```
2. Retry documento

**Para out of memory:**
1. Check memory usage: Azure Portal → Function App → Monitor → Memory
2. Si consistently > 80%:
   - Upgrade to Premium Plan (más memory por instance)
   - Reducir maxConcurrentActivityFunctions en host.json
3. Restart instances

**Para Durable Functions runtime:**
1. Restart all instances:
   ```powershell
   # Via portal: Function App → Restart
   # Via CLI: az functionapp restart --name ... --resource-group ...
   ```
2. Monitor próximas 2 horas

---

## 5. MEMORY LEAK (Process memory crece)

### Síntomas
- Instance memory steadily increases
- P99 latency degrades over time
- Performance improves after restart

### Diagnóstico (5 min)

En Application Insights:
```kql
performanceCounters
| where name == "Process Private Bytes"
| summarize avg_memory=avg(value), max_memory=max(value) by bin(timestamp, 5m)
| order by timestamp desc
| render timechart
```

Si gráfico muestra trend upward → Memory leak probado

### Causas

- Static field holding references in plugin
- Event handler not unsubscribed
- Cache growing unbounded
- HttpClient not disposed

### Soluciones

**Inmediatas:**
1. Increase autoscale trigger (reduce time between restarts)
2. Reduce maxConcurrentActivityFunctions to lower memory usage
3. Deploy null-coalescing cache cleanup (temporary patch)

**Permanentes:**
1. Code review: Search for static fields, event handlers, HttpClient
2. Add memory tests to CI/CD
3. Deploy memory leak fix

---

## 6. STORAGE ACCESS DENIED

### Síntomas
- Errores: "AuthorizationPermissionMismatch" o "ResourceNotFound"
- Blobs no se pueden leer/escribir
- Intermitente vs consistent

### Diagnóstico (2 min)

```powershell
# Verificar managed identity permissions
Get-AzRoleAssignment -ObjectId (Get-AzADServicePrincipal -DisplayName "documentia-functions").Id
```

### Causas

- RBAC role missing (Storage Blob Data Contributor)
- Storage account firewall blocking (if private endpoint)
- SAS token expired
- Storage account key rotated

### Soluciones

**Para RBAC issue:**
```powershell
New-AzRoleAssignment -ObjectId (Get-AzADServicePrincipal -DisplayName "documentia-functions").Id `
  -RoleDefinitionName "Storage Blob Data Contributor" `
  -Scope (Get-AzStorageAccount -Name "documentiastorage" -ResourceGroupName "..").Id
```

**Para firewall issue:**
1. Azure Portal → Storage Account → Networking
2. Si private endpoint: Verificar traffic routing
3. Si service endpoint: Verificar VNet rules
4. Temp: Add Function App IP to whitelist

**Para SAS token:**
```powershell
# Regenerate SAS
$ctx = New-AzStorageContext -StorageAccountName documentiastorage -UseConnectedAccount
New-AzStorageBlobSASToken -Container "documentos" -Blob "*" -Permission racwd -ExpiryTime (Get-Date).AddYears(1) -Context $ctx
```

---

## 7. ESCALATION PATH & CONTACTS

### SLA Response Times
- P1 (Complete outage): 30 min response, 2 hour resolution
- P2 (High impact): 2 hour response, 8 hour resolution
- P3 (Low impact): 4 hour response, 1 week resolution

### Escalation Contacts
1. **On-Call Dev:** [Via TEAM_MATRIX]
2. **Lead Architect:** [Via TEAM_MATRIX]
3. **Azure Support:** [Microsoft support contract details]
4. **Provider Escalation:** See THIRD_PARTY_SLAS.md

### Post-Incident

After resolving incident:
1. Document symptom, cause, solution in this runbook
2. Schedule retro within 48 hours
3. Update tests/monitoring to prevent recurrence
