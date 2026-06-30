# 8. Checklists de Revision Azure y Azure DevOps — DocumentIA MVP

> Ultima actualizacion: 2026-04-06  
> Proyecto: AI DocClassExt — SAREB

---

## Checklist A — Revision Azure/DevOps para despliegue inicial

Usar cuando se despliega el sistema por primera vez en un entorno nuevo o se reconstruye desde cero.
El foco es revisar plataforma Azure y cadena de despliegue en Azure DevOps.

---

### BLOQUE 1 — Azure DevOps: Service Connection

| # | Tarea | Detalle | OK |
|---|-------|---------|-----|
| 1.1 | Crear Service Connection ARM `AI DocClassExt` | Project Settings → Service Connections → Azure Resource Manager → scope: `SRBRGDOCSAIPROD` | ☐ |
| 1.2 | Dar permisos al SP del SC sobre el Resource Group | `Contributor` + `Key Vault Secrets User` | ☐ |
| 1.3 | Verificar que `azure-pipelines.yml` apunta al SC correcto | Variable `AZURE_SERVICE_CONNECTION` = `AI DocClassExt` | ☐ |

---

### BLOQUE 2 — Recursos Azure (verificar o crear)

| # | Recurso | Nombre | Región | Estado esperado | OK |
|---|---------|--------|--------|-----------------|----|
| 3.1 | Resource Group | `SRBRGDOCSAIPROD` | West Europe | Existente | ☐ |
| 3.2 | Storage Account (Durable hub) | `srbstgproapppdocai` | West Europe | Existente | ☐ |
| 3.3 | Storage Account (documentos) | `srbstgprodocai` | West Europe | Existente | ☐ |
| 3.4 | Function App | `srbappprodocai` | West Europe | Existente (**.NET 10 Isolated**) | ☐ |
| 3.5 | Document Intelligence | `srbdiprodocai` | West Europe | Existente | ☐ |
| 3.6 | Azure OpenAI + Content Understanding | `upe48-mm2avmdm` | Sweden Central | Verificar deployment `gpt-4o-mini` activo | ☐ |
| 3.7 | Application Insights | `srbappiprodocai` | West Europe | Existente | ☐ |
| 3.8 | Key Vault | `srbkvprodocai` | West Europe | Existente | ☐ |
| 3.9 | **Azure SQL Server + Database** | `srbsqlprodocai` / `DocumentIA` | West Europe | Existente (Operativo) — verificado 2026-04-30 (ver `docs/auxiliares/auditorias/09_AUDITORIA_CONFIGURACION_2026-04-30.md`) | ☑ |
| 3.10 | Web App Admin | `srbwebadminprodocai` | West Europe | Existente — verificado 2026-04-30 (doc 09) | ☑ |
| 3.11 | App Service AssetResolver | `srbwebpluginassetresolver` | West Europe | Existente — verificado 2026-04-30 (doc 09) | ☑ |

> **Requisito Application Insights (obligatorio):** las tres apps (`srbappprodocai`, `srbwebadminprodocai`, `srbwebpluginassetresolver`) deben tener `APPLICATIONINSIGHTS_CONNECTION_STRING` asociado a `srbappiprodocai`. El contrato de configuracion (`scripts/config/azure-appsettings-contract.json`) exige esta clave para las tres y el stage `ValidateConfiguration` del pipeline **fallara** si falta. La asociacion de App Insights al AssetResolver faltaba historicamente; verificar que esta presente (Portal → App Service → Application Insights → On, o app setting manual).

---

### BLOQUE 3 — Managed Identity + RBAC

> ⚠️ **PRERREQUISITO OBLIGATORIO (antes de ejecutar el pipeline).** El Service Principal del service connection puede desplegar pero **NO tiene permiso para crear role assignments** (`Microsoft.Authorization/roleAssignments/write`). Por tanto, la asignación del rol **`Key Vault Secrets User`** a las managed identities de las apps (Function App, Admin, AssetResolver) se hace **una vez por entorno** con un rol elevado (Owner / User Access Administrator / RBAC Administrator, vía PIM), usando:
>
> ```powershell
> az login                                  # cuenta con permiso RBAC (activa PIM si procede)
> pwsh ./scripts/configuration/assign-keyvault-rbac.ps1 -TargetEnvironment dev   # o pre / prod
> ```
>
> El script habilita la System-Assigned Managed Identity de las 3 apps y les asigna `Key Vault Secrets User` sobre el Key Vault (idempotente). Tras ejecutarlo, el paso *"Ensure ... Key Vault RBAC"* del pipeline **solo verifica** que el rol existe (variable `manageKeyVaultRbac=false`, por defecto). Si la variable se pone a `true`, el pipeline intentará crear la asignación (solo funciona si el SP tiene permiso RBAC).
>
> **Comprobar** (solo lectura, no requiere PIM): confirma el estado antes o después del pipeline.
>
> ```powershell
> pwsh ./scripts/configuration/verify-keyvault-rbac.ps1 -TargetEnvironment dev   # o pre / prod
> ```
>
> Devuelve `[ OK ]` por cada app (Function App, Admin, AssetResolver) y código de salida 0 si todas tienen el rol; `[MISSING]`/`[MI OFF]` y código 1 si falta en alguna.

| # | Tarea | Comando / Portal | OK |
|---|-------|------------------|----|
| 4.0 | **Pre-asignar RBAC de Key Vault a las MI (Function App + Admin + AssetResolver)** | `pwsh ./scripts/configuration/assign-keyvault-rbac.ps1 -TargetEnvironment <env>` (rol elevado/PIM) | ☐ |
| 4.1 | Activar System Managed Identity en `srbappprodocai` | Lo hace 4.0; o Portal → Function App → Identity → System assigned: **On** | ☐ |
| 4.2 | KV `srbkvprodocai`: rol `Key Vault Secrets User` a la MI de Function App, Admin y AssetResolver | Lo hace 4.0; o Portal → KV → Access control (IAM) | ☐ |
| 4.3 | Storage `srbstgprodocai`: rol `Storage Blob Data Contributor` a la MI | Portal → Storage → IAM | ☐ |
| 4.4 | Storage `srbstgproapppdocai`: roles `Blob Data Owner` + `Queue Data Contributor` + `Table Data Contributor` a la MI | Portal → Storage → IAM | ☐ |
| 4.5 | Azure SQL (cuando esté listo): `CREATE USER [srbappprodocai] FROM EXTERNAL PROVIDER` + `db_datareader` / `db_datawriter` | SSMS / sqlcmd en Azure SQL | ☐ |
| 4.6 | Document Intelligence: `Cognitive Services User` a la MI (si AuthMode=MI) | Portal → DI → IAM | ☐ |
| 4.7 | Azure OpenAI: `Cognitive Services User` a la MI (si AuthMode=MI) | Portal → OpenAI → IAM | ☐ |

> **Nota:** `assign-keyvault-rbac.ps1` cubre solo el rol `Key Vault Secrets User` (causa del fallo del pipeline). Los roles de Storage/SQL/DI/OpenAI (4.3–4.7) siguen siendo manuales si aplican.

---

### BLOQUE 4 — Key Vault: cargar secretos

| # | Tarea | Script | OK |
|---|-------|--------|----|
| 5.1 | Login Azure CLI con cuenta que tenga acceso a KV | `az login` | ☐ |
| 5.2 | Activar rol PIM si necesario | Ver tareas VS Code: "Activate PIM role (user - device code)" | ☐ |
| 5.3 | Verificar elegibilidad PIM | Ver tareas VS Code: "List PIM eligible assignments (user)" | ☐ |
| 5.4 | Verificar permisos sobre el RG | `.\scripts\configuration\check-azure-permissions.ps1` | ☐ |
| 5.5 | Cargar todos los secretos en Key Vault | `.\scripts\configuration\set-keyvault-secrets.ps1 -SubscriptionId <ID>` | ☐ |
| 5.6 | Verificar que los secretos existen en KV | `.\scripts\deployment\verify-prod-prereqs.ps1` | ☐ |

Secretos requeridos en KV:

| Secreto KV | Descripcion |
|-----------|-------------|
| `AzureWebJobsStorage` | Connection string Storage Durable hub |
| `AzureStorageConnectionString` | Connection string Storage documentos |
| `SqlConnectionString` | Connection string Azure SQL (o Docker temporal) |
| `user-ods-dwh` | Connection string ODS DWH usada por `ConnectionStrings__AssetResolverDb` del AssetResolver |
| `AssetResolverApiKey` | API key compartida entre Function App (`AssetResolver__ApiKey`) y Web App del AssetResolver (`ApiKey`) |
| `Extraction--AzureContentUnderstanding--ApiKey` | API Key Content Understanding |
| `Extraction--GptFallback--ApiKey` | API Key Azure OpenAI (extraccion fallback) |
| `Classification--AzureDocumentIntelligence--ApiKey` | API Key Document Intelligence |
| `Classification--GptFallback--ApiKey` | API Key Azure OpenAI (clasificacion fallback) |
| `GDC--Username` | Usuario servicio GDC |
| `GDC--Password` | Password servicio GDC |
| `GDC--HttpBasicUsername` | Usuario HTTP Basic GDC |
| `GDC--HttpBasicPassword` | Password HTTP Basic GDC |

---

### BLOQUE 5 — App Settings de la Function App

| # | Tarea | Script | OK |
|---|-------|--------|----|
| 6.1 | Aplicar todos los App Settings (infraestructura + AI + GDC) | `.\scripts\configuration\set-app-settings.ps1` | ☐ |
| 6.2 | Aplicar referencias Key Vault en settings de secretos | `.\scripts\configuration\set-functionapp-keyvault-references.ps1 -SubscriptionId <ID>` | ☐ |
| 6.3 | Verificar que la Function App tiene `RunDatabaseMigrationsOnStartup=false` (valor real aplicado por el pipeline en cada deploy). Las migraciones **no** se aplican en runtime ni por pipeline: se aplican manualmente con script idempotente (ver Bloque 7) | `az functionapp config appsettings list --name srbappprodocai --resource-group SRBRGDOCSAIPROD` | ☐ |
| 6.4 | Verificar `host.json`: `hubName=DocumentIAHub`, `maxConcurrentActivityFunctions=4`, `maxConcurrentOrchestratorFunctions=4` | `src/backend/DocumentIA.Functions/host.json` | ☐ |

Settings clave a confirmar (ver detalle completo en `docs/auxiliares/migracion-deployment/04_MANUAL_EXPLOTACION.md` § 4.4):

| Setting | Valor esperado |
|---------|---------------|
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `Extraction__DefaultProvider` | `azure-content-understanding` |
| `Classification__DefaultProvider` | `azure-document-intelligence` |
| `Extraction__AzureContentUnderstanding__Endpoint` | `https://upe48-mm2avmdm-swedencentral.services.ai.azure.com/` |
| `Extraction__AzureContentUnderstanding__MaxConcurrentCalls` | `4` (baseline operativo actual; ajustar según carga) |
| `Extraction__AzureContentUnderstanding__HardTimeoutSeconds` | `90` (timeout duro por intento CU) |
| `Extraction__AzureContentUnderstanding__EnableCircuitBreaker` | `true` |
| `Extraction__AzureContentUnderstanding__CircuitBreakerFailureThreshold` | `5` |
| `Extraction__AzureContentUnderstanding__CircuitBreakerOpenSeconds` | `45` |
| `Extraction__AzureContentUnderstanding__MaxRetries` | `3` (reintentos con backoff exponencial) |
| `Extraction__AzureContentUnderstanding__InitialRetryDelayMs` | `500` (ms base para backoff; delay real = base × 2^(intento-1)) |
| `Classification__AzureDocumentIntelligence__Endpoint` | `https://srbdiprodocai.cognitiveservices.azure.com/` |
| `Extraction__GptFallback__DeploymentName` | `gpt-4o-mini` |
| `Classification__GptFallback__DeploymentName` | `gpt-4o-mini` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connection string `srbappiprodocai` |
| `AssetResolver__BaseUrl` | `https://srbwebpluginassetresolver.azurewebsites.net/` |
| `AssetResolver__ApiKey` | Key Vault reference a `AssetResolverApiKey` |

---

### BLOQUE 6 — Build y deploy inicial (pipeline)

| # | Tarea | Detalle | OK |
|---|-------|---------|-----|
| 7.1 | Verificar modo de ejecucion del pipeline | `trigger: none` y `pr: none` en `azure-pipelines.yml`: el despliegue es **manual** ("Run pipeline" en AzDO) con el parametro `targetEnvironment`. No hay disparo automatico por merge | ☐ |
| 7.2 | Confirmar que el Stage Build ejecuta restore/build/test/publish | AzDO Run logs | ☐ |
| 7.3 | Confirmar que el Stage DeployFunctions ejecuta zipDeploy | Task `AzureFunctionApp@2` en run | ☐ |
| 7.4 | Confirmar que se aplica modo Key Vault en app settings | Task `AzureCLI@2` post-deploy | ☐ |
| 7.5 | Confirmar Stage `ValidateConfiguration` (valida contrato de App Settings) | Task `Validate App Settings contract` en run. El smoke test del endpoint NO forma parte del pipeline; se ejecuta a mano post-deploy (Bloque 8, tarea 9.2) | ☐ |
| 7.6 | Verificar estado final del run | `Succeeded` en Build y DeployFunctions | ☐ |

---

### BLOQUE 7 — Migraciones de base de datos

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| 8.1 | Si `RunDatabaseMigrationsOnStartup=true`: verificar que el primer arranque aplica migraciones automaticamente | Logs de Function App en AppInsights o Kudu | ☐ |
| 8.2 | Si se aplican de forma manual (recomendado en prod): generar script SQL idempotente | `dotnet ef migrations script --idempotent -p src/backend/DocumentIA.Data -s src/backend/DocumentIA.Functions -o migrations.sql` | ☐ |
| 8.3 | Aplicar `migrations.sql` contra Azure SQL via sqlcmd o Azure Portal Query Editor | `sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -i migrations.sql` | ☐ |
| 8.4 | Una vez aplicadas las migraciones, cambiar `RunDatabaseMigrationsOnStartup=false` | `az functionapp config appsettings set --settings RunDatabaseMigrationsOnStartup=false ...` | ☐ |
| 8.5 | Verificar conectividad y tablas creadas | `sqlcmd ... -Q "SELECT name FROM sys.tables"` o `.\scripts\database\Query-Tipologias.ps1` | ☐ |

---

### BLOQUE 7b — Esquema + datos de configuración en un entorno nuevo/limpio (procedimiento probado)

Usar cuando la base de datos del entorno está **vacía** (sin tablas) y hay que dejarla operativa: primero el **esquema** (migraciones EF Core) y después los **datos de configuración** (modelos/providers, tipologías, catálogos TDN1/TDN2, plugins, prompts). Autenticación con tu usuario **Entra ID** (`az login`), sin contraseñas.

> ⚠️ **Orden obligatorio:** las migraciones van SIEMPRE antes de la carga de datos. Si intentas cargar config sobre una BD vacía obtendrás `Cannot find the object "dbo.ModeloConfigs"`.

#### Paso 1 — Crear/actualizar el esquema (migraciones EF Core)

El factory de diseño (`DocumentIA.Data/Context/DocumentIADbContextFactory.cs`) resuelve la conexión desde la **variable de entorno** `SqlConnectionString` (o `ConnectionStrings__DocumentIA`) e **ignora `--connection`**. Por eso se fija la conexión por env var:

```powershell
az login   # tu usuario con permisos en el entorno destino

# Apuntar al entorno destino (ejemplo DEV) con auth Entra ID
$env:SqlConnectionString = "Server=tcp:srbsqldevdocai.database.windows.net,1433;Database=DocumentIA;Authentication=Active Directory Default;Encrypt=True;"

dotnet ef database update `
  --project src/backend/DocumentIA.Data `
  --startup-project src/backend/DocumentIA.Functions
```

- Requiere **.NET 10 SDK** (compila el startup `DocumentIA.Functions`, net10) y `dotnet-ef` (8.x). Aplica todas las migraciones + datos seed (catálogos, prompts iniciales, tipología `tasacion`).
- Necesitas **`db_owner`/`db_ddladmin`** en la BD destino.
- **Alternativa** (mezcla net10/EF8, o sin SDK en la máquina de aplicación): generar script idempotente y aplicarlo con `sqlcmd`:
  ```powershell
  dotnet ef migrations script --idempotent `
    --project src/backend/DocumentIA.Data --startup-project src/backend/DocumentIA.Functions `
    -o .\artifacts\db-config\schema-migrations.sql
  sqlcmd -S srbsqldevdocai.database.windows.net -d DocumentIA -G -C -i .\artifacts\db-config\schema-migrations.sql
  ```

#### Paso 2 — Cargar/replicar los datos de configuración entre entornos

Con [scripts/database/replicate-config-data.ps1](../scripts/database/replicate-config-data.ps1). Ejemplo **PROD → DEV** (PROD es origen de solo lectura; DEV el destino):

```powershell
# 2a) Exportar desde PROD (solo lectura, genera un .sql idempotente para revisar)
pwsh ./scripts/database/replicate-config-data.ps1 -Mode Export -EntraAuth `
  -SourceConnectionString "Server=tcp:srbsqlprodocai.database.windows.net,1433;Database=DocumentIA;Encrypt=True;" `
  -OutputFile .\artifacts\db-config\seed-from-prod.sql

# 2b) Revisar el .sql y aplicarlo a DEV (-Mirror deja DEV como espejo de PROD)
pwsh ./scripts/database/replicate-config-data.ps1 -Mode Apply -EntraAuth -Mirror `
  -TargetConnectionString "Server=tcp:srbsqldevdocai.database.windows.net,1433;Database=DocumentIA;Encrypt=True;" `
  -InputFile .\artifacts\db-config\seed-from-prod.sql
```

- Tablas replicadas: `ModeloConfigs` (modelos+providers), `PromptTemplates`, `Tipologias`, `CatalogoTdn1`, `CatalogoTdn2`, `PluginTipologiaConfigs`. **No** toca datos operativos (documentos, ejecuciones, auditoría, validaciones).
- Con `-EntraAuth` las cadenas solo llevan `Server`/`Database`/`Encrypt` (sin credenciales); el script obtiene un token de Entra ID válido para todo el tenant.
- Idempotente: `MERGE` por PK con `IDENTITY_INSERT` (conserva Ids → mantiene la FK `CatalogoTdn2 → CatalogoTdn1`). Reejecutable.
- `-Mirror` borra en destino lo que no exista en origen (orden inverso de FK) y evita colisiones de clave única (`Codigo`/`Key` con distinto Id). Para destino = PROD, omitir `-Mirror` salvo intención explícita.
- Modo directo sin fichero intermedio: `-Mode Copy` pasando ambas connection strings.
- Para otros entornos cambiar los nombres de servidor: DEV `srbsqldevdocai` · PRE `srbsqlpredocai` · PROD `srbsqlprodocai`.

#### Paso 3 — Verificar

```powershell
.\scripts\database\Query-Tipologias.ps1     # o: sqlcmd ... -Q "SELECT COUNT(*) FROM Tipologias"
```

---

### BLOQUE 8 — Verificacion post-deploy

| # | Tarea | Script / Comando | OK |
|---|-------|------------------|----|
| 9.1 | Verificar todos los prerrequisitos de prod OK | `.\scripts\deployment\verify-prod-prereqs.ps1` | ☐ |
| 9.2 | Smoke test del endpoint `/api/tipologias` | `.\scripts\testing\smoke-test-functions.ps1 -HostName srbappprodocai.azurewebsites.net` | ☐ |
| 9.3 | Verificar estado de slots/settings en Function App | Azure Portal → Configuration (sin valores vacios/errores) | ☐ |
| 9.4 | Verificar conectividad de secretos Key Vault references | Estado `Resolved` en App Settings | ☐ |
| 9.5 | Verificar salud en Application Insights | Exceptions/failures sin picos post-deploy | ☐ |
| 9.6 | Verificar disponibilidad Storage + SQL desde prerrequisitos | `verify-prod-prereqs.ps1` sin errores | ☐ |

---

---

## Checklist B — Revision Azure/DevOps para futuras modificaciones

Usar para cualquier actualización sobre un entorno ya operativo con foco en control de cambios Azure/DevOps.

---

### B1 — Alcance y riesgo del cambio

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| B1.1 | Clasificar el cambio | Infra Azure / Pipeline / App Settings / Secretos / Schema BD | ☐ |
| B1.2 | Validar impacto en seguridad | Cambios de roles MI, KV, SC, permisos SP | ☐ |
| B1.3 | Validar impacto en disponibilidad | Restart de Function App, ventanas de mantenimiento | ☐ |
| B1.4 | Si hay cambios de schema, planificar migracion idempotente | `dotnet ef migrations script --idempotent ...` | ☐ |

---

### B2 — Deploy via pipeline (opcion recomendada)

| # | Tarea | Detalle | OK |
|---|-------|---------|-----|
| B2.1 | Commit y push a rama de trabajo + PR a `main` | Flujo controlado con aprobacion | ☐ |
| B2.2 | Lanzar el pipeline manualmente ("Run pipeline") con `targetEnvironment` correcto | El pipeline es `trigger: none`; no se dispara por merge a `main` | ☐ |
| B2.3 | Verificar Stage **Build** completo | restore/build/test/publish sin errores | ☐ |
| B2.4 | Verificar Stage **DeployFunctions** completo | zipDeploy + AzureCLI settings + validacion de claves CU (el smoke test es manual, post-deploy) | ☐ |
| B2.5 | Revisar evidencias del run | logs, artifacts, duracion, warnings | ☐ |
| B2.6 | Si aplica Admin, habilitar y revisar Stage **DeployAdmin** | `condition: succeeded()` cuando exista `srbwebadminprodocai` | ☐ |

---

### B3 — Deploy manual (sin pipeline)

Usar solo si el pipeline no esta disponible o hay urgencia.

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| B3.1 | Activar PIM si necesario | Ver tareas VS Code: "Activate PIM role (user - device code)" | ☐ |
| B3.2 | Obtener credenciales Kudu | `az functionapp deployment list-publishing-credentials --resource-group SRBRGDOCSAIPROD --name srbappprodocai` | ☐ |
| B3.3 | Ejecutar deploy-manual.ps1 | `.\scripts\deployment\deploy-manual.ps1 -KuduUser '$srbappprodocai' -KuduPassword '<PASS>'`  | ☐ |
| B3.4 | Alternativa Cloud Shell | `.\scripts\deployment\deploy-manual.ps1` → subir `publish/functions.zip` → `az functionapp deploy ...` | ☐ |

---

### B4 — Cambios de configuracion / secretos

| # | Tarea | Script | OK |
|---|-------|--------|----|
| B4.1 | Actualizar secreto en Key Vault | `.\scripts\configuration\set-keyvault-secrets.ps1 -SubscriptionId <ID>` | ☐ |
| B4.2 | Si es un nuevo setting no secreto: actualizar `set-app-settings.ps1` y re-ejecutar | `.\scripts\configuration\set-app-settings.ps1` | ☐ |
| B4.3 | Forzar restart de la Function App para tomar cambios | `az functionapp restart --name srbappprodocai --resource-group SRBRGDOCSAIPROD` | ☐ |
| B4.4 | Verificar que las referencias KV estan en estado `Resolved` | Azure Portal → Function App → Configuration → Application Settings | ☐ |

---

### B5 — Migraciones de BD

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| B5.1 | Generar script idempotente | `dotnet ef migrations script --idempotent -p src/backend/DocumentIA.Data -s src/backend/DocumentIA.Functions -o migrations.sql` | ☐ |
| B5.2 | Revisar `migrations.sql` antes de aplicar | Especialmente: columnas ADD, tablas CREATE, indices | ☐ |
| B5.3 | Aplicar en Azure SQL (o Docker si es local) | `sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -i migrations.sql` | ☐ |
| B5.4 | Verificar tablas y columnas nuevas | `sqlcmd ... -Q "SELECT name FROM sys.tables"` o `.\scripts\database\Query-Tipologias.ps1` | ☐ |

> Para **entorno limpio** (BD vacía) o para **promocionar datos de configuración** entre entornos (dev→pre→prod), ver el procedimiento completo paso a paso en **BLOQUE 7b** (migraciones de esquema + carga de datos con `replicate-config-data.ps1`).

---

### B6 — Verificacion post-deploy (siempre)

| # | Tarea | Script / Comando | OK |
|---|-------|------------------|----|
| B6.1 | Humo: /api/tipologias responde HTTP 200 | `.\scripts\testing\smoke-test-functions.ps1 -HostName srbappprodocai.azurewebsites.net` | ☐ |
| B6.2 | Revisar Application Insights durante 5-10 min post-deploy | Failures, exceptions, duraciones normales | ☐ |
| B6.3 | Verificar estado de secretos y referencias KV | `verify-prod-prereqs.ps1` sin errores y settings `Resolved` | ☐ |
| B6.4 | Verificar estado de recursos criticos Azure | Function App, Storage, SQL y DI en estado saludable | ☐ |

---

## Referencia rapida de scripts de despliegue

| Script | Uso | Cuando ejecutar |
|--------|-----|-----------------|
| Ver tareas VS Code | Activar rol PIM | Antes de cualquier operacion Azure CLI que requiera Contributor |
| `scripts\deployment\verify-prod-prereqs.ps1` | Verificar KV, secretos, Function App, Storage | Antes del deploy y post-deploy |
| `scripts\configuration\set-keyvault-secrets.ps1 -SubscriptionId <ID>` | Cargar / actualizar secretos en KV | Primer deploy o rotacion de claves |
| `scripts\configuration\set-functionapp-keyvault-references.ps1 -SubscriptionId <ID>` | Aplicar modo KV en App Settings | Primer deploy o tras reset de settings |
| `scripts\configuration\set-app-settings.ps1` | Aplicar todos los App Settings no-secretos | Primer deploy o cambio de configuracion no-secreta |
| `scripts\deployment\deploy-manual.ps1` | Build + zip + deploy via Kudu | Deploy manual sin pipeline |
| `scripts\testing\smoke-test-functions.ps1 -HostName <host>` | Verificar endpoint `/api/tipologias` (requiere PowerShell 7 / `pwsh`) | Post cada deploy |
| `scripts\database\Query-Tipologias.ps1` / `sqlcmd` | Verificar conectividad BD y estado tablas | Post migraciones |
| `scripts\database\replicate-config-data.ps1` | Replicar datos de **configuracion** (modelos/providers, tipologias, catalogos TDN1/TDN2, plugins, prompts) entre entornos. Modos `Export`/`Apply`/`Copy`, idempotente (MERGE+IDENTITY_INSERT) | Al promocionar configuracion dev→pre→prod |
| `scripts\legacy\list-analyzers.ps1` | Listar modelos disponibles en Document Intelligence | Diagnostico de clasificacion |
| `scripts\configuration\check-azure-permissions.ps1` | Verificar permisos del SP / usuario sobre el RG | Antes del primer deploy |
| `scripts\configuration\assign-keyvault-rbac.ps1 -TargetEnvironment <env>` | **Prerrequisito RBAC**: habilita las MI de Function App/Admin/AssetResolver y les asigna `Key Vault Secrets User` (rol elevado/PIM) | Una vez por entorno, antes del pipeline |
| `scripts\configuration\verify-keyvault-rbac.ps1 -TargetEnvironment <env>` | Comprobar (solo lectura) que las MI tienen `Key Vault Secrets User` | Antes/después del pipeline o ante fallo de RBAC |

> **Nota:** `compile-all-plugins.ps1` y `check-database.ps1` referenciados en versiones anteriores **ya no existen**. Para compilar plugins de enriquecimiento ver [docs/manuales/MANUAL_PLUGINS.md](../manuales/MANUAL_PLUGINS.md); para la BD usar `scripts\database\Query-Tipologias.ps1`, `scripts\database\diagnose-assetresolver-sql.ps1` o `sqlcmd`.

> `set-keyvault-secrets.ps1` no contiene valores secretos versionados. Los valores deben aportarse mediante variables `DOCIA_SECRET_*` o mediante `-PreferLocalSettings -LocalSettingsPath <ruta local no versionada>`.

---

## Notas de estado actual (2026-04-06)

| Item | Estado |
|------|--------|
| Azure SQL `srbsqlprodocai` | Existente y operativo en produccion |
| Web App Admin `srbwebadminprodocai` | Existente y desplegado via pipeline |
| Web App AssetResolver `srbwebpluginassetresolver` | Existente y desplegado via pipeline |
| Stage Deploy Admin en pipeline | Activo |
| Stage Deploy AssetResolver en pipeline | Activo |
| Key Vault `srbkvprodocai` | Existente, usado como fuente de secretos por referencia Key Vault |
| Managed Identity RBAC | Parcialmente configurado — revisar Bloque 3 |
| Function App App Settings | Alineados con contrato canonico `scripts/config/azure-appsettings-contract.json` |
| Migraciones EF Core en runtime | Desactivadas por defecto (`RunDatabaseMigrationsOnStartup=false`) |

---

## BLOQUE 6 — Verificar App Insights post-despliegue

Usar tras cualquier despliegue a produccion para confirmar que la observabilidad esta operativa.

| # | Tarea | Detalle | OK |
|---|-------|---------|----|
| 6.1 | `APPLICATIONINSIGHTS_CONNECTION_STRING` presente en App Settings de **`srbappprodocai`** (Functions) | Portal → `srbappprodocai` → Configuration → Application Settings → buscar clave | ☐ |
| 6.1a | `APPLICATIONINSIGHTS_CONNECTION_STRING` presente en **`srbwebadminprodocai`** (Admin) | Portal → Admin → Configuration / Application Insights → On | ☐ |
| 6.1b | `APPLICATIONINSIGHTS_CONNECTION_STRING` presente en **`srbwebpluginassetresolver`** (AssetResolver) — *requisito obligatorio: faltaba historicamente y rompe el stage `ValidateConfiguration`* | Portal → AssetResolver App Service → Application Insights → On (o app setting manual a `srbappiprodocai`) | ☐ |
| 6.2 | Live Metrics accesible y reactivo | `srbappiprodocai` → Live Metrics → debe mostrar servidor activo en < 5 s tras invocar cualquier endpoint | ☐ |
| 6.3 | Exceptions aparecen en Failures | Provocar un error controlado (parametro invalido) → verificar en App Insights → Failures en < 2 min | ☐ |
| 6.4 | Durable Functions Monitor accesible | Portal → Function App → Durable Functions → listar instancias de las ultimas 24 h sin error de acceso | ☐ |
| 6.5 | Log Analytics workspace vinculado y queries disponibles | `srbappiprodocai` → Logs → ejecutar Q1 (ver sec. 4.12.4 de Manual Explotacion) sin error de workspace | ☐ |
| 6.6 | Retencion de datos ≥ 30 dias | `srbappiprodocai` → Usage and estimated costs → Data Retention: verificar ≥ 30 dias | ☐ |

> **Referencia**: ver seccion `4.12 Monitorizacion y Observabilidad en Portal Azure` del [04_MANUAL_EXPLOTACION.md](04_MANUAL_EXPLOTACION.md).  
> Queries KQL base guardadas en Log Analytics como `DocumentIA-Q1` … `DocumentIA-Q4`.

---

## Referencias

| Documento | Contenido |
|-----------|-----------|
| [01_ARQUITECTURA_SISTEMA.md](01_ARQUITECTURA_SISTEMA.md) | Arquitectura completa y diagrama de despliegue |
| [auxiliares/migracion-deployment/04_MANUAL_EXPLOTACION.md](auxiliares/migracion-deployment/04_MANUAL_EXPLOTACION.md) | Procedimientos paso a paso, variables de entorno, scripts |
| [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md) | Configuracion tecnica detallada |
| [auxiliares/planes/11_PLAN_CONFIGURACION_LIMPIA.md](auxiliares/planes/11_PLAN_CONFIGURACION_LIMPIA.md) | Plan de remediacion y gobierno de configuracion |
| [../azure-pipelines.yml](../azure-pipelines.yml) | Pipeline CI/CD: Build + DeployFunctions + DeployAdmin + DeployAssetResolver + ValidateConfiguration |
