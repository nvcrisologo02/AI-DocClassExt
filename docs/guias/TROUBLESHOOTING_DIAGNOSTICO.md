# Troubleshooting & Diagnóstico — DocumentIA

**Última actualización:** 2026-07-13  
**Público objetivo:** Operadores, DevOps, Soporte 2º nivel

---

## 1. Diagnóstico Rápido — Decision Tree

```
¿Está el servicio levantado?
├─ NO → Ver sección 4.1 (Health checks)
└─ SÍ
    ├─ ¿Llega la petición al API?
    │  ├─ NO → Firewall / Networking (ver 4.2)
    │  └─ SÍ
    │      ├─ ¿Retorna HTTP 4xx?
    │      │  └─ Validar payload (schema, tipología, campos requeridos)
    │      └─ ¿Retorna HTTP 5xx?
    │         └─ Ver logs en AppInsights (sección 3.1)
    │
    ├─ ¿Inicia la orquestación?
    │  ├─ NO → Function App issue (logs en AppInsights)
    │  └─ SÍ
    │      ├─ ¿Completa con éxito (runtimeStatus = Completed)?
    │      │  ├─ SÍ → Revisar resultado y confianza (sección 2.1)
    │      │  └─ NO → Ver estado en customStatus (sección 2.2)
    │      │
    │      ├─ ¿runtimeStatus = Running?
    │      │  ├─ >10 min? → Timeout de activity (ver 2.3)
    │      │  └─ Normal → Esperar o revisar logs
    │      │
    │      ├─ ¿runtimeStatus = Failed?
    │      │  └─ Revisar excepción y activity fallida (sección 3.2)
    │      │
    │      └─ ¿runtimeStatus = Terminated?
    │         └─ Orquestación cancelada (revisar instancia en Durable Monitor)
    │
    └─ Si el resultado llegó pero está mal → Ver casos prácticos (sección 2)
```

---

## 2. Casos Prácticos Comunes

### CASO 1: Clasificación devuelve confianza muy baja (< 0.3)

**Síntomas:**
- Resultado completa sin error
- `Identificacion.Tipologia` detectada, pero `ConfianzaGlobal` está por debajo del umbral configurado
- `EstadoCalidad = "REVISION"` o `"ERROR"`
- `ConfianzaClasificacion` baja (ej., 0.25)

**Causas posibles:**
1. Documento ambiguo o de baja calidad
2. Fallback GPT no está habilitado o falló
3. Todos los providers devuelven confianza baja
4. Umbral de tipología muy estricto

**Diagnóstico:**

1. **Revisar el documento:**
   ```powershell
   # Obtener metadata del documento procesado
   $operationId = "guid-from-response"
   $kql = @"
   customEvents
   | where name == "DocumentIngestedSuccessfully"
   | where tostring(customDimensions["OperationId"]) == "$operationId"
   | project timestamp, Documento=customDimensions["DocumentName"], 
             Confianza=tostring(customDimensions["ConfianzaGlobal"]),
             Tipologia=customDimensions["Tipologia"],
             ProveedorClasif=customDimensions["ProveedorClasificacion"]
   "@ 
   # Ejecutar en Log Analytics (app insights)
   ```

2. **Revisar scores de cada provider:**
   ```kusto
   traces
   | where timestamp > ago(1h)
   | where operation_Id == "operationId-aqui"
   | where message contains "Confianza" or message contains "Confidence"
   | project timestamp, provider=customDimensions["provider"], 
             confidence=tostring(customDimensions["confidence"]),
             message
   | order by timestamp asc
   ```

3. **Revisar configuración de tipología:**
   - ¿`confidenceConfig.clasifUmbralFallback` está muy alto (> 0.9)?
   - ¿`Classification.GptFallback.Enabled` es `false`?

**Solución paso a paso:**

1. **Si es un documento legítimo de baja confianza:**
   - Bajar `umbralOK` en `confidenceConfig` de la tipología
   - O bajar `umbralFallbackCompletitud`/`umbralConfianza` para este caso

2. **Si debería haber fallback GPT:**
   - Verificar `Classification__GptFallback__Enabled=true` en app settings
   - Verificar `Classification__GptFallback__Endpoint` y `ApiKey` en Key Vault
   - Revisar logs: ¿hay exception en la llamada a GPT?

3. **Si la tipología es muy restrictiva:**
   - Revisar `confidenceConfig` en la configuración de tipología
   - Ajustar pesos de confianza si es necesario

---

### CASO 2: Activity timeout en orquestación

**Síntomas:**
- `runtimeStatus = "Running"` después de > 5 minutos
- `customStatus.actividadActual` congelada en una actividad
- Activity nunca pasa a estado `Completed`
- Posible `"Timeout"` en `DetalleEjecucion.Seguimiento.Actividades[x].Mensaje`

**Causas posibles:**
1. Azure Content Understanding muy lento (> 90 segundos)
2. Plugin REST/SOAP colgado indefinidamente
3. SQL Server no responde
4. Resource contention o saturación

**Diagnóstico:**

1. **Identificar qué activity está colgada:**
   ```powershell
   $instanceId = "instance-id-from-orchestration"
   
   # Desde Desktop App o via API status endpoint
   $statusUri = "https://func-app.azurewebsites.net/runtime/webhooks/durableTask/instances/$instanceId"
   $status = Invoke-RestMethod -Uri $statusUri -Method Get -Headers @{
       "x-functions-key" = "function-key"
   }
   
   $currentActivity = $status.customStatus.actividadActual
   Write-Host "Activity congelada: $currentActivity"
   ```

2. **Revisar logs de la activity:**
   ```kusto
   traces
   | where operation_Id == "operation-id"
   | where operation_Name contains "Activity"  // ej., "ClasificarActivity", "ExtraerActivity"
   | project timestamp, operation_Name, message, severityLevel
   | order by timestamp desc
   | take 50
   ```

3. **Revisar métricas de CU (si está en ExtraerActivity):**
   ```kusto
   customMetrics
   | where timestamp > ago(30m)
   | where name in ("CU.AnalysisMs", "CU.LimiterWaitMs")
   | where tostring(customDimensions["Tipologia"]) == "tipologia-aqui"
   | summarize p95_analysis=percentile(value, 95), 
             p95_wait=percentile(value, 95) 
             by name
   ```

**Solución paso a paso:**

1. **Si es ExtraerActivity (CU):**
   - Verificar que `MaxConcurrentCalls` no sea muy bajo (default: 4)
   - Aumentar `HardTimeoutSeconds` si documentos complejos necesitan más tiempo
   - Revisar si hay circuit breaker abierto: ¿`EnableCircuitBreaker=true` y hay muchas fallas?

2. **Si es plugin (IntegrarActivity):**
   - Revisar `retryPolicy.timeoutSeconds` en configuración de plugin
   - Verificar que el endpoint del plugin esté accesible
   - Aumentar timeout del plugin si es necesario

3. **Si es GDC (SubirGDCActivity):**
   - Default timeout es 120s; verificar `GDC__TimeoutSeconds`
   - Verificar conectividad a GDC (firewall, endpoint)
   - Revisar si SOAP está respondiendo lentamente

4. **Opción nuclear:** Cancelar la orquestación y reintentar:
   ```powershell
   # Terminar instancia
   az functionapp durable orchestration terminate `
     --instance-id "instance-id" `
     --resource-group "SRBRGDOCSAIPROD" `
     --name "srbappprodocai"
   ```

---

### CASO 3: Plugin falla repetidamente (ej., Azure CU 429)

**Síntomas:**
- `ExtraerActivity` retorna error `429 Too Many Requests` o `503 Service Unavailable`
- `ModelKey` marcado en circuit breaker
- Otros documentos de la misma tipología también fallan
- Métricas de CU muestran `Attempts > 1` repetidamente

**Causas posibles:**
1. Rate limiting de Azure Content Understanding activado
2. Cuota de CU agotada
3. Token de autenticación expirado
4. Endpoint CU saturado

**Diagnóstico:**

1. **Revisar pattern de errores en AppInsights:**
   ```kusto
   customEvents
   | where timestamp > ago(6h)
   | where name == "CU.TransientError" or name == "CU.CircuitOpen"
   | extend statusCode = tostring(customDimensions["statusCode"]),
           attempt = tostring(customDimensions["attempt"]),
           tipologia = tostring(customDimensions["Tipologia"])
   | summarize ErrorCount=count() by bin(timestamp, 5m), statusCode, tipologia
   | order by timestamp desc
   ```

2. **Revisar cuota y uso de CU:**
   ```powershell
   # En Azure Portal: ir a la resource CU → Metrics
   # Buscar métrica "Total Requests" vs "Throttled Requests"
   # Si Throttled > 0, hay rate limiting
   ```

3. **Verificar token de autenticación:**
   ```powershell
   # Si AuthMode=ApiKey, validar que la clave en Key Vault no esté expirada
   $kvName = "srbkvprodocai"
   $secretName = "Extraction--AzureContentUnderstanding--ApiKey"
   az keyvault secret show --vault-name $kvName --name $secretName `
     --query "attributes.expires" -o tsv
   ```

**Solución paso a paso:**

1. **Reducir concurrencia temporal:**
   ```powershell
   az functionapp config appsettings set `
     --resource-group "SRBRGDOCSAIPROD" `
     --name "srbappprodocai" `
     --settings "Extraction__AzureContentUnderstanding__MaxConcurrentCalls=2"
   ```

2. **Aumentar backoff de reintentos:**
   ```powershell
   # Cambiar InitialRetryDelayMs a 1500 (default 500)
   az functionapp config appsettings set `
     --resource-group "SRBRGDOCSAIPROD" `
     --name "srbappprodocai" `
     --settings "Extraction__AzureContentUnderstanding__InitialRetryDelayMs=1500"
   ```

3. **Habilitar secondary model (si existe):**
   - Si tipología tiene `secondaryModelKey`, el sistema hace round-robin automáticamente
   - Verificar en base de datos: tabla `ModeloConfigs`, campo `ConfiguracionJson` → `secondaryModelKey`

4. **Escalar Azure CU:**
   - Si la cuota está agotada, contactar a Azure support
   - Opción: usar GPT fallback como proveedor principal temporalmente

---

### CASO 4: Documento rechazado como duplicado (debería procesarse)

**Síntomas:**
- Resultado devuelve `ReutilizadaPorDuplicado = true`
- `DetalleEjecucion.Seguimiento.Actividades` contiene `VerificarDuplicadoActivity: Completed`
- Documento no fue procesado, solo retorna resultado anterior

**Causas posibles:**
1. SHA256 del documento coincide con uno anterior
2. Detección de duplicado funcionando correctamente, pero cliente quiere forzar reprocess
3. Hash collision (muy raro, pero posible)

**Diagnóstico:**

1. **Obtener SHA256 del documento actual:**
   ```powershell
   $base64Doc = "... content.Base64 ..."
   $bytes = [Convert]::FromBase64String($base64Doc)
   $sha256 = ([System.Security.Cryptography.HashAlgorithm]::Create("SHA256")).
             ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") } | Join-String
   Write-Host "SHA256 del documento: $sha256"
   ```

2. **Buscar en base de datos si existe:**
   ```sql
   SELECT Documento, SHA256, FechaProceso, Estado
   FROM Documentos
   WHERE SHA256 = 'sha256-aqui'
   ORDER BY FechaProceso DESC
   ```

3. **Revisar si es falso positivo:**
   - Comparar documentos byte a byte (¿son idénticos o solo similares?)
   - Si son similares pero no idénticos, hash colls muy raro; revisar SQL

**Solución paso a paso:**

1. **Si debería reprocessarse (incluir `forceReprocess = true`):**
   ```json
   {
     "documento": { "name": "...", "content": { "base64": "..." } },
     "instrucciones": {
       "forceReprocess": true
     }
   }
   ```

2. **Si fue un falso positivo (hash collis):**
   - Investigar el documento anterior en BD
   - Evaluar si necesita reprocess manual

3. **Si se desea eliminar el duplicado anterior:**
   ```powershell
   # Buscar documento en BD y marcarlo como obsoleto (cambiar Estado)
   # NO eliminar físicamente salvo instrucción explícita
   $sql = @"
   UPDATE Documentos
   SET Estado = 'Obsoleto', FechaModificacion = GETUTCDATE()
   WHERE SHA256 = '$sha256'
   "@
   ```

---

### CASO 5: Error de acceso a Azure Storage

**Síntomas:**
- `SubirBlobActivity` o similar retorna error `AuthenticationFailed` / `Forbidden`
- SAS token expirado o credenciales inválidas
- RBAC roles insuficientes
- Mensajes como: `"Access denied to container 'documents'"`

**Diagnóstico:**

1. **Verificar RBAC de Managed Identity:**
   ```powershell
   $principalId = (az functionapp identity show `
     --resource-group "SRBRGDOCSAIPROD" `
     --name "srbappprodocai" `
     --query "principalId" -o tsv)
   
   # Listar role assignments en Storage Account
   az role assignment list `
     --scope "/subscriptions/sub-id/resourceGroups/SRBRGDOCSAIPROD/providers/Microsoft.Storage/storageAccounts/srbstgprodocai" `
     --query "[?principalId=='$principalId']" -o table
   ```

2. **Verificar Key Vault references:**
   ```powershell
   # Verificar que `AzureStorageConnectionString` esté resuelto
   az functionapp config appsettings list `
     --resource-group "SRBRGDOCSAIPROD" `
     --name "srbappprodocai" `
     | Select-String "AzureStorageConnectionString" -A 2
   
   # Debería mostrar estado "Resolved"
   ```

3. **Revisar logs:**
   ```kusto
   traces
   | where timestamp > ago(1h)
   | where severityLevel >= 2  // Warnings + Errors
   | where message contains "Blob" or message contains "Storage"
   | project timestamp, message, customDimensions
   ```

**Solución paso a paso:**

1. **Si faltan RBAC roles:**
   ```powershell
   $principalId = (az functionapp identity show `
     --resource-group "SRBRGDOCSAIPROD" `
     --name "srbappprodocai" `
     --query "principalId" -o tsv)
   
   # Asignar rol "Storage Blob Data Contributor"
   az role assignment create `
     --role "Storage Blob Data Contributor" `
     --assignee-object-id $principalId `
     --scope "/subscriptions/.../resourceGroups/SRBRGDOCSAIPROD/providers/Microsoft.Storage/storageAccounts/srbstgprodocai"
   ```

2. **Si connection string está expirada:**
   - Regenerar en Azure Storage → Access keys
   - Actualizar en Key Vault

3. **Verificar firewall de Storage:**
   - Portal → Storage Account → Networking
   - ¿Virtual network rules permiten la Function App?
   - ¿"Allow access from" está en "Selected networks"?

---

### CASO 6: Base de datos connection timeout

**Síntomas:**
- `PersistirActivity` falla con `"timeout expired"` o `"connection pool exhausted"`
- `ValidarActivity` o `ResolverTipologiaActivity` no obtiene configuración de BD
- Múltiples documentos fallan simultáneamente

**Diagnóstico:**

1. **Verificar connectivity básica:**
   ```powershell
   $sqlServer = "srbsqlprodocai.database.windows.net"
   $database = "DocumentIA"
   
   # Test DNS resolution
   [System.Net.Dns]::GetHostAddresses($sqlServer)
   
   # Test port 1433
   Test-NetConnection -ComputerName $sqlServer -Port 1433 -InformationLevel Verbose
   ```

2. **Verificar connection pool:**
   ```kusto
   customMetrics
   | where name contains "DbPool" or name contains "Connection"
   | summarize PoolSize=max(value), Active=avg(value) by name
   ```

3. **Revisar logs de SQL:**
   ```sql
   -- En Azure Portal → SQL Database → Query editor (o SSMS)
   SELECT 
     name, 
     COUNT(*) as connections,
     status
   FROM sys.dm_exec_connections
   GROUP BY name, status
   ORDER BY COUNT(*) DESC
   ```

**Solución paso a paso:**

1. **Aumentar tamaño del connection pool:**
   ```powershell
   # En código: appsettings.json o connection string
   # "Connection Timeout=30;Max Pool Size=100;"
   
   az functionapp config appsettings set `
     --resource-group "SRBRGDOCSAIPROD" `
     --name "srbappprodocai" `
     --settings "SqlConnectionStringOptions=Max Pool Size=100"
   ```

2. **Verificar firewall de SQL:**
   ```powershell
   # Portal → SQL Server → Firewall rules
   # ¿Está abierto a IP de Function App?
   
   # Permitir Azure services
   az sql server firewall-rule create `
     --resource-group "SRBRGDOCSAIPROD" `
     --server "srbsqlprodocai" `
     --name "AllowAzureServices" `
     --start-ip-address "0.0.0.0" `
     --end-ip-address "0.0.0.0"
   ```

3. **Escalar SQL:**
   - Si la carga es muy alta, aumentar DTU o vCores
   - Portal → SQL Database → Scale → ajustar tier

---

### CASO 7: Documentos en `PENDIENTE_REINTENTO` / latencia alta en clasificación GPT (429 Azure OpenAI)

**Síntomas:**
- Documentos terminan con `Estado = "PENDIENTE_REINTENTO"` y `EstadoCalidad = "ERROR"`
- `MensajeError = "Clasificación pospuesta: cuota de Azure OpenAI agotada (429). Reintentar más tarde."`
- Confianzas de clasificación a 0
- Latencias altas en `ClasificarActivity` o en ejecución de prompts bajo carga
- Prompts (enriquecimiento) devuelven `PromptResultado.Error` con prefijo `rate_limit_exhausted:` (no bloqueante, no escala el documento)

**Causas posibles:**
1. Rate limiting (429) de Azure OpenAI por cuota/TPM del deployment agotada
2. Picos de carga concurrente sobre el mismo endpoint/deployment (clasificación GPT y prompts comparten la misma cuota cuando apuntan al mismo recurso)
3. Circuit breaker abierto tras varios fallos consecutivos: el cooldown está activo y las llamadas se rechazan rápido sin reintentar

**Diagnóstico:**

1. **Revisar reintentos y apertura de circuito en AppInsights:**
   ```kusto
   customEvents
   | where name in ("AOAI.RateLimitRetry", "AOAI.CircuitOpen", "AOAI.CircuitRejected", "AOAI.CircuitClosed")
   | where timestamp > ago(6h)
   | project timestamp, name, circuitKey=tostring(customDimensions["circuitKey"]),
             attempt=tostring(customDimensions["attempt"]),
             delayMs=tostring(customDimensions["delayMs"]),
             statusCode=tostring(customDimensions["statusCode"])
   | order by timestamp desc
   ```
   Ver el catálogo completo de queries en [OBSERVABILIDAD_KQL.md](../observabilidad/OBSERVABILIDAD_KQL.md).

2. **Diferenciar el estado del documento:**
   - `PENDIENTE_REINTENTO` → cuota de Azure OpenAI agotada tras reintentos/cooldown; estado limpio y retriable, no es un fallo del documento
   - `NO_CLASIFICADO` → documento genuinamente no clasificable; no confundir con rate limit
   - `SIN_CONTENIDO_DOCUMENTO` → se pidió prompt o resumen y no se pudo leer el documento por ninguna vía; no es un problema de clasificación ni de cuota

3. **Revisar cuota y uso del deployment en Azure Portal:**
   - Azure OpenAI resource → deployment usado por clasificación/prompts → Metrics → comparar `Rate Limit Requests` / uso de TPM vs límite asignado

**Solución paso a paso:**

1. **Reprocesar los documentos en `PENDIENTE_REINTENTO`** cuando la cuota se haya recuperado
2. **Revisar y, si procede, ampliar la cuota/TPM** del deployment de Azure OpenAI en Azure Portal (o repartir carga entre deployments)
3. **Ajustar la configuración `AzureOpenAIResilience`** (ver manual de configuración): `MaxRetries`, `InitialRetryDelayMs`, `MaxRetryDelaySeconds`, `CircuitBreakerFailureThreshold`, `CircuitBreakerOpenSeconds`
4. **Rollback si es necesario:** `MaxRetries: 0` + `EnableCircuitBreaker: false` desactiva el reintento/circuito y vuelve al comportamiento anterior (error crudo del SDK ante 429)
5. Los prompts con `rate_limit_exhausted:` no requieren acción sobre el documento (degradado no bloqueante); revisar solo si el volumen es alto y afecta a la calidad del enriquecimiento

---

### CASO 8: Réplica de analizador CU a West Europe falla — `"has not granted the necessary permissions"`

**Síntomas:**

- Al replicar un analizador de Content Understanding de Sweden Central → West Europe con `scripts/deployment/copy-cu-analyzer.ps1` (cross-resource), el paso `:copy` falla con:
  ```
  NotFound / ModelNotFound: The resource '.../upe48-mm2avmdm-swedencentral' has not granted
  the necessary permissions for the source analyzer '<id>' to copy to the target resource.
  ```
- El paso `grantCopyAuthorization` responde `200` (parece OK) pero el copy falla igualmente.

**Causa (verificado 2026-07-17):**

- **La Copy API cross-resource NO funciona en este entorno.** El `grantCopyAuthorization` devuelve un cuerpo **sin el campo `source`** (stub), el `:copy` no valida la autorización, y el flujo con token (`:getCopyAuthorization`) da `404`.
- **No es (solo) permisos.** Se descartó por orden: casing del RG, propagación (reintentos), analizadores referenciados, permisos del usuario (tiene `Cognitive Services User` en ambos), e incluso tras **conceder `Cognitive Services User` a la identidad administrada del destino sobre el origen** (el requisito del *pull* cross-resource) el error persiste. Probable limitación de servicio con analizadores **project-scoped de Foundry**.

**Solución (método operativo):**

1. **Replicar reconstruyendo/reentrenando** en el destino, no copiando:
   ```powershell
   ./scripts/deployment/recreate-cu-analyzer.ps1 `
       -SourceAnalyzerId <id> -ResourceGroup SRBRGDOCSAIPROD -SyncDefaults
   ```
   Hace `PUT create-or-replace` en el destino y reentrena desde el blob de datos etiquetados (`knowledgeSources`).
2. **Prerrequisito:** la identidad administrada del destino (`srbaisrv-westeurope`) necesita **`Storage Blob Data Reader`** sobre `srbstgproapppdocai` (ya concedido). Si el build acaba en `failed`, revisar ese rol.
3. **Validar** el analizador reconstruido con documentos de prueba antes de repuntar `modelKey` en `ModeloConfigs`: es equivalente funcional, no un snapshot bit a bit.
4. Si se necesita el snapshot exacto vía Copy API, **abrir caso de soporte Azure** con la evidencia (grant sin `source`, `:getCopyAuthorization` → 404).

> Detalle completo: `docs/guias/GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING.md` §12 (aviso al inicio + §12.2b).

---

### Caso: ejecución en `SIN_CONTENIDO_DOCUMENTO`

**Síntoma:** el documento cierra en `SIN_CONTENIDO_DOCUMENTO`, sin `Resumen` ni `ResultadoPrompt`, con la actividad `Prompt` en `Failed`.

**Qué significa:** se pidió un prompt o un resumen y el sistema no consiguió texto del documento por ninguna vía (markdown de extracción, layout pre-clasificación ni extracción bajo demanda). **Es un fallo deliberado**: antes de existir esta guarda se llamaba al modelo con el contenido vacío y respondía cosas como *"No has incluido el documento"*, que se persistían como resumen válido con confianza 1.0.

**Diagnóstico por orden de probabilidad:**

1. **Documento ilegible o corrupto.** Comprobar `detalleEjecucion.markdownGenerado = false` y buscar errores de Document Intelligence:
   ```kql
   traces
   | where operation_Id == "<operationId>"
   | where message has "InvalidContent" or message has "no se pudo obtener markdown"
   ```
2. **Document Intelligence no disponible o sin permisos.** Buscar `401`, `403` o `InvalidRequest` en la misma traza. En dev/pre revisar `DocumentIntelligence__UseInlineContent` (ver `03_DISENO_TECNICO_DETALLADO.md`).
3. **Tipología sin extracción ni layout con `expectedType` informado.** Es el caso que cubre la extracción bajo demanda; si aun así falla, el problema está en la llamada a layout, no en la configuración de la tipología.

**Qué NO es:** no es un problema de clasificación (`NO_CLASIFICADO`) ni de cuota de Azure OpenAI (`PENDIENTE_REINTENTO`). El modelo ni siquiera llegó a invocarse.

### Caso: el coste de IA sale a cero o incompleto

**Síntoma:** la salida trae `detalleEjecucion.costes` con `costeTotalEur` a cero, o con `tarifasCompletas` en falso y modelos listados en `modelosSinTarifa`. En el Admin, la sección `/costes` muestra ejecuciones sin importe.

**Qué significa:** el cálculo no falla nunca una ejecución. Un coste ausente es un dato que falta, no un error de procesamiento. Las causas, por orden de probabilidad:

1. **Falta el catálogo de tarifas en ese entorno.** Es el caso más común tras un despliegue nuevo. Comprobar que existe la fila:
   ```sql
   SELECT ModelKey, LEN(ConfiguracionJson) AS Tamano, Activo
   FROM ModeloConfigs WHERE Tipo = 4;
   ```
   Si no devuelve nada, cargar `scripts/migrations/costes-ia/01-seed-tarifas-ia.sql`.
2. **El modelo consumido no está en el catálogo.** El propio bloque lo dice en `modelosSinTarifa`. Ocurre al dar de alta un deployment o un analyzer nuevo sin añadir su línea de precio. La tarifa se resuelve por **modelo físico** (nombre de deployment, analyzer, classifier o `prebuilt-layout`), no por la fila de configuración que lo invoca.
3. **La tarifa existe pero su fecha de vigencia es posterior a la ejecución.** Cada línea lleva `vigenteDesde`; una ejecución se tarifa con lo que se facturaba entonces. Si se dio de alta el precio hoy, las ejecuciones de ayer siguen sin tarifa. Es el comportamiento correcto: añadir una línea con vigencia anterior si se quiere cubrirlas.
4. **Caché del catálogo.** Se recarga cada cinco minutos. Tras editar las tarifas hay que esperar antes de dar por malo el resultado.
5. **La ejecución no consumió IA.** Una reutilización por duplicado llega a cero, marcada con `reutilizadaPorDuplicado`, y el coste de la ejecución original va aparte. Una clasificación resuelta por reglas del clasificador híbrido tampoco gasta IA. Ninguno de los dos es un fallo.
6. **JSON del catálogo inválido.** El cargador es tolerante: ante un JSON que no parsea deja el catálogo vacío en silencio, sin romper nada. Un catálogo entero sin efecto, con la fila presente en base de datos, apunta aquí. Validar el JSON antes de guardarlo desde el Admin.

**Verificación:** `pwsh .\scripts\testing\test-costes-ia.ps1 -Environment <env>` comprueba de una pasada el cálculo, las tarifas completas, el cuadre del desglose y la visibilidad condicionada al parámetro.

**Qué NO es:** que la suma de un entorno no cuadre con la factura de su grupo de recursos no es un defecto. Los tres entornos consumen la misma cuenta de IA de producción, así que la factura agrega ejecuciones que viven en otras bases de datos. Ver [MANUAL_COSTES_IA.md](../manuales/MANUAL_COSTES_IA.md).

---

## 3. Debugging Profundo

### 3.1 Seguimiento de logs en Application Insights

**¿Dónde buscar traces?**

1. **Por Orchestration Instance ID:**
   ```kusto
   traces
   | where operation_Id == "instance-id-aqui"
   | order by timestamp asc
   | project timestamp, message, severityLevel, operation_Name
   ```

2. **Por Documento:**
   ```kusto
   traces
   | where customDimensions["DocumentName"] == "documento.pdf"
   | order by timestamp asc
   ```

3. **Por Tipología:**
   ```kusto
   traces
   | where customDimensions["Tipologia"] == "nota-simple"
   | where timestamp > ago(24h)
   | summarize Count=count(), Errors=countif(severityLevel >= 3) by operation_Name
   ```

**Filtrar por severity:**
- Level 0-1: Verbose/Info (ruido, ignorar normalmente)
- Level 2: Warning (revisar si hay patrón)
- Level 3+: Error (crítico, investigar)

### 3.2 customStatus & Timeline

El orquestador publica `customStatus` en tiempo real. Estructura:

```json
{
  "version": "1.0",
  "estado": "Running",
  "actividadActual": "ClasificarActivity",
  "actividadesTotales": 8,
  "actividadesCompletadas": ["ObtenerMetadatosGDC", "VerificarDuplicado"],
  "duracionTotalMs": 5234,
  "actividades": [
    {
      "nombre": "Clasificar",
      "estado": "Running",
      "duracionMs": 3100,
      "mensaje": null,
      "fallbackActivado": false
    },
    {
      "nombre": "Extraer",
      "estado": "Pending",
      "duracionMs": 0,
      "mensaje": null,
      "fallbackActivado": false
    }
  ],
  "mensaje": null
}
```

**Cómo leerlo:**
1. `estado = "Running"` → Pipeline en marcha
2. `actividadActual` → Activity actual (debería avanzar cada 30s)
3. Si `duracionMs` > timeout esperado de activity → investigar

### 3.3 Local Debugging

**Setup inicial:**

```powershell
# Activar virtual environment Python (para tests)
& .\.venv\Scripts\Activate.ps1

# Instalar Azure Functions Core Tools
npm install -g azure-functions-core-tools@4

# Activar Azurite (local storage) en otra terminal
azurite --silent --location .\__blobstorage__

# En la primera terminal, arrancar Functions
cd src/backend/DocumentIA.Functions
func start --build

# En tercera terminal, ejecutar smoke tests
.\tests\api-tests\test-ingest-notasimple1-4-classify.ps1 -HostUrl "http://localhost:7071"
```

**Breakpoints en VS Code:**

1. Abrir `.vscode/launch.json`
2. Verificar que esté la configuración de Functions:
   ```json
   {
     "name": "Attach to Functions",
     "type": "coreclr",
     "request": "attach",
     "processId": "${command:pickProcess}",
     "preLaunchTask": "func: host start",
     "postDebugTask": "func: host stop"
   }
   ```
3. Presionar F5 o `Debug → Start Debugging`
4. Poner breakpoints en activity functions (ej., `ClasificarActivity.cs`)

**Mock servers locales:**

```powershell
# Arrancar mock servers (REST/SOAP para plugins)
cd scripts\Mock\ Servers
.\start-mock-servers.ps1

# En otra terminal, test
curl http://localhost:8080/

# Parar
.\stop-mock-servers.ps1
```

### 3.4 Performance Profiling

**Identificar cuellos de botella:**

```kusto
customMetrics
| where timestamp > ago(24h)
| where name in ("CU.PrepareMs", "CU.LimiterWaitMs", "CU.AnalysisMs", "CU.ParseMs")
| extend tipologia = tostring(customDimensions["Tipologia"])
| summarize
    p50=percentile(value, 50),
    p95=percentile(value, 95),
    p99=percentile(value, 99)
  by name, tipologia
| order by p99 desc
```

**Si `LimiterWaitMs` es alto:** Backpressure, aumentar `MaxConcurrentCalls`  
**Si `AnalysisMs` es alto:** Azure CU lento, revisar payload o cuota  
**Si `ParseMs` es alto:** Parsing de resultado lento (raro, revisar documento)

---

## 4. Herramientas de Diagnóstico

### 4.1 KQL Queries (guardadas en Log Analytics)

**Q1: Últimas clasificaciones fallidas (últimas 24h)**
```kusto
customEvents
| where name == "ClassificationFailed"
| where timestamp > ago(24h)
| project timestamp, Documento=customDimensions["DocumentName"],
          Tipologia=customDimensions["Tipologia"],
          Confianza=tostring(customDimensions["Confidence"]),
          Razon=customDimensions["Reason"]
| order by timestamp desc
| take 50
```

**Q2: Promedio de confianza por tipología**
```kusto
customMetrics
| where name == "DocumentProcessed"
| where timestamp > ago(7d)
| extend tipologia = tostring(customDimensions["Tipologia"])
| summarize
    Total=count(),
    ConfianzaPromedio=avg(value),
    ConfianzaMin=min(value),
    ConfianzaMax=max(value),
    ConfianzaP95=percentile(value, 95)
  by tipologia
| order by ConfianzaPromedio asc
```

**Q3: Duración por actividad (últimas 24h)**
```kusto
customMetrics
| where name startswith "Activity."
| where timestamp > ago(24h)
| extend activity=replace_regex(name, @"Activity\.", "")
| summarize
    Llamadas=count(),
    DuracionPromedio=avg(value),
    P95=percentile(value, 95),
    Max=max(value)
  by activity
| order by P95 desc
```

**Q4: Errores por proveedor (últimas 6h)**
```kusto
customEvents
| where name contains "Error" or severityLevel >= 3
| where timestamp > ago(6h)
| extend provider=tostring(customDimensions["Provider"])
| summarize ErrorCount=count() by provider, customDimensions["ErrorCode"]
| order by ErrorCount desc
```

**Q5: Circuit breaker eventos (CU)**
```kusto
customEvents
| where name in ("CU.CircuitOpen", "CU.CircuitClosed", "CU.CircuitFailover")
| where timestamp > ago(24h)
| project timestamp, name, ModelKey=customDimensions["ModelKey"], Razon=customDimensions["Reason"]
| order by timestamp desc
```

### 4.2 PowerShell Scripts de Health Check

**check-service-health.ps1:**
```powershell
param(
    [string]$ResourceGroup = "SRBRGDOCSAIPROD",
    [string]$FunctionApp = "srbappprodocai",
    [string]$ApiUrl = "https://srbappprodocai.azurewebsites.net"
)

Write-Host "`n=== DocumentIA Health Check ===" -ForegroundColor Cyan

# 1. Function App status
Write-Host "`n[1/5] Function App status..." -ForegroundColor Yellow
$funcStatus = az functionapp show -g $ResourceGroup -n $FunctionApp --query "state" -o tsv
Write-Host "State: $funcStatus" -ForegroundColor $(if ($funcStatus -eq "Running") { "Green" } else { "Red" })

# 2. HTTP endpoint
Write-Host "`n[2/5] HTTP endpoint..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/api/health" -ErrorAction Stop
    Write-Host "✓ API responding" -ForegroundColor Green
} catch {
    Write-Host "✗ API not responding: $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Database connectivity
Write-Host "`n[3/5] Database connectivity..." -ForegroundColor Yellow
$sqlServer = az sql server show -g $ResourceGroup --query "fullyQualifiedDomainName" -o tsv
Write-Host "Testing $sqlServer..."
$connTest = Test-NetConnection -ComputerName $sqlServer -Port 1433 -InformationLevel Quiet
Write-Host "Port 1433: $(if ($connTest) { 'Open' } else { 'Closed' })" -ForegroundColor $(if ($connTest) { "Green" } else { "Red" })

# 4. Storage connectivity
Write-Host "`n[4/5] Storage connectivity..." -ForegroundColor Yellow
$storageAccount = "srbstgprodocai"
$storageKey = az storage account keys list -g $ResourceGroup -n $storageAccount --query "[0].value" -o tsv
$context = New-AzStorageContext -StorageAccountName $storageAccount -StorageAccountKey $storageKey
$containers = Get-AzStorageContainer -Context $context -ErrorAction SilentlyContinue
Write-Host "Containers found: $($containers.Count)" -ForegroundColor Green

# 5. AppInsights
Write-Host "`n[5/5] Application Insights..." -ForegroundColor Yellow
$appInsights = az monitor app-insights component show -g $ResourceGroup -n "srbappiprodocai" --query "appId" -o tsv
Write-Host "AppId: $appInsights" -ForegroundColor Green

Write-Host "`n=== Check Complete ===" -ForegroundColor Cyan
```

### 4.3 REST Client Testing

**health.http:**
```http
GET https://srbappprodocai.azurewebsites.net/api/health
x-functions-key: {{functionKey}}

###

GET https://srbappprodocai.azurewebsites.net/runtime/webhooks/durableTask/instances/{{orchestrationId}}
x-functions-key: {{functionKey}}

###

POST https://srbappprodocai.azurewebsites.net/api/documents/classify
x-functions-key: {{functionKey}}
Content-Type: application/json

{
  "documento": {
    "name": "test.pdf",
    "content": {
      "base64": "JVBERi0xLjQKJeLj..."
    }
  },
  "instrucciones": {
    "classification": {
      "umbral": 0.6
    }
  }
}
```

---

## 5. Escalation Path

| Issue Type | Responsable | Tickets/Logs a Adjuntar | SLA |
|---|---|---|---|
| **HTTP 5xx / Function crash** | Dev Backend | - Exceptions en AppInsights traces<br>- Orchestration Instance ID<br>- Documento (Base64 si < 1MB) | 1h |
| **Timeout > 10 min** | Dev Backend + CU Team | - customStatus timeline<br>- Logs de activity<br>- Métricas CU (P95 ms) | 4h |
| **Rate limiting CU (429)** | CU Team / Azure Support | - Timestamp de error<br>- Cuota actual vs límite<br>- Pattern de requests | 2h |
| **Rate limiting Azure OpenAI (429) / `PENDIENTE_REINTENTO`** | Dev Backend / Azure Support | - Eventos `AOAI.RateLimitRetry` / `AOAI.CircuitOpen`<br>- `circuitKey` afectado<br>- Cuota/TPM del deployment | 2h |
| **Storage / Blob access denied** | Infra / RBAC | - Error exact + timestamp<br>- Logs Azure Storage<br>- Role assignments | 2h |
| **SQL connection timeout** | Database Admin | - SQL logs<br>- Connection pool stats<br>- DTU / CPU usage | 4h |
| **Documento duplicado por error** | Dev Backend | - Documento<br>- SHA256<br>- BD audit trail | Normal |
| **Baja confianza sistemática** | ML / Classification | - Muestras de tipología<br>- Historial de confianzas<br>- Configuración de umbral | 1 día |
| **Plugin falla** | Plugin Owner | - Logs del plugin<br>- Requests/responses<br>- Configuración de retry | 4h |

**Logs obligatorios en ticket:**
- Operation ID / Instance ID
- Timestamp exact (UTC)
- Nombre de documento
- Tipología
- Error message / exception
- KQL query utilizada

---

## 6. FAQ — Preguntas Comunes

**¿Cómo veo el resultado de un documento ya procesado?**
```powershell
# Opción 1: Por instance ID
$instanceId = "..."
$statusUri = "https://func-app.azurewebsites.net/runtime/webhooks/durableTask/instances/$instanceId"
$status = Invoke-RestMethod -Uri $statusUri -Method Get -Headers @{"x-functions-key"="key"}
$status.output | ConvertTo-Json

# Opción 2: Desde BD
SELECT DocumentoId, SHA256, Resultado FROM Documentos WHERE SHA256='...'
```

**¿Cómo fuerzo reprocesamiento?**
```json
{
  "instrucciones": {
    "forceReprocess": true,
    "classification": { "umbral": 0.5 }
  }
}
```

**¿Cuál es el timeout máximo?**
- Activity individual: configurable, default ~300s (5 min)
- Orchestration total: ~7 días (limit Durable Functions)
- SubirGDC: 120s
- CU hard timeout: 90s

**¿Cómo veo si CU circuit breaker está abierto?**
```kusto
customEvents
| where name == "CU.CircuitOpen"
| where timestamp > ago(1h)
| distinct ModelKey, Razon
```

**¿Cómo aumento concurrent requests?**
```powershell
az functionapp config appsettings set \
  --resource-group SRBRGDOCSAIPROD \
  --name srbappprodocai \
  --settings \
    "Extraction__AzureContentUnderstanding__MaxConcurrentCalls=8" \
    "Extraction__AzureContentUnderstanding__InitialRetryDelayMs=1000"
```

**¿Cómo me entero de un outage?**
- Status Page de Azure (https://status.azure.com)
- Alerts en Portal de SQL / CU / OpenAI
- Durable Monitor: ver si hay instancias terminadas sin razón

---

## 7. Referencias

| Documento | Contenido |
|---|---|
| [01_ARQUITECTURA_SISTEMA.md](../01_ARQUITECTURA_SISTEMA.md) | Diagrama de despliegue, componentes principales |
| [03_DISENO_TECNICO_DETALLADO.md](../03_DISENO_TECNICO_DETALLADO.md) | Configuración detallada, endpoints |
| [05_MANUAL_USO_CONFIGURACION.md](../05_MANUAL_USO_CONFIGURACION.md) | App settings, secretos, tipologías |
| [especificaciones/CONFIANZA_AGREGADA.md](../referencias/CONFIANZA_AGREGADA.md) | Cálculo de confianza, umbrales |
| [manuales/MANUAL_COSTES_IA.md](../manuales/MANUAL_COSTES_IA.md) | Coste de IA: qué se mide, catálogo de tarifas, sección de Admin |
| [observabilidad/CU_RENDIMIENTO_INSIGHTS.md](../observabilidad/CU_RENDIMIENTO_INSIGHTS.md) | KQL queries para CU performance |

---

**Historial de cambios:**

| Fecha | Cambio |
|---|---|
| 2026-06-10 | Versión inicial con 6 casos, KQL queries, scripts PowerShell |
| 2026-07-13 | Añadido CASO 7: rate limiting (429) de Azure OpenAI en clasificación GPT/prompts y estado `PENDIENTE_REINTENTO` |
| 2026-09-07 | Añadido el caso de coste de IA a cero o incompleto (catálogo de tarifas, vigencia, caché) |
