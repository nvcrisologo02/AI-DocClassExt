# 8. Checklists de Revision Azure y Azure DevOps — DocumentIA

> Última revisión: 2026-11-25 (AB#101553, validación TLS del GDC contra la CA de SAREB; pendiente de despliegue, DEV primero). Revisiones previas: 2026-11-25 (AB#101450, retirada de los app settings heredados sin efecto), 2026-11-22 (AB#101447, contraste con `azure-pipelines*.yml`, `infra/ai/pipeline-variables.yml`, `infra/ai/deployments.*.json`, `scripts/**`, GUIA_EASYAUTH_ADMIN.md e INFRAESTRUCTURA_REAL_DESPLEGADA.md). Hay una service connection por entorno (`AI DocClassExt DEV|PRE|PRO`) y los nombres de recursos de los bloques 2 a 8 son los de PRO; los de DEV y PRE están en la tabla de inventario de INFRAESTRUCTURA_REAL_DESPLEGADA.md. Las menciones a gpt-4o-mini de este documento describen PRE y PRO; en DEV el modelo generativo es gpt-5.6-luna desde el 2026-11-20 (ADR-004). Los app settings `Classification__GptFallback__*`, `Classification__AzureDocumentIntelligence__*`, `Classification__DefaultModelKey`, `Extraction__GptFallback__*` y `Extraction__AzureContentUnderstanding__*` (salvo los siete de resiliencia) siguen en el contrato y en el pipeline pero no se leen en runtime: la conexión, el modelo y los umbrales de IA salen de `ModeloConfigs` por alias e identidad administrada. La numeración de las filas (3.x en el Bloque 2, 4.x en el Bloque 3…) es un desfase heredado que no se renumera. El estado de recursos, RBAC, App Insights y restricciones de red se comprobó en Azure el 2026-11-23 (AB#101449).
>
> Ultima actualizacion anterior: 2026-04-06  
> Proyecto: AI DocClassExt — SAREB

---

## Checklist A — Revision Azure/DevOps para despliegue inicial

Usar cuando se despliega el sistema por primera vez en un entorno nuevo o se reconstruye desde cero.
El foco es revisar plataforma Azure y cadena de despliegue en Azure DevOps.

---

### BLOQUE 1 — Azure DevOps: Service Connection

| # | Tarea | Detalle | OK |
|---|-------|---------|-----|
| 1.1 | Crear una Service Connection ARM por entorno: `AI DocClassExt DEV`, `AI DocClassExt PRE`, `AI DocClassExt PRO` | Project Settings → Service Connections → Azure Resource Manager → scope: el resource group del entorno (`SRBRGDEVDOCSAI`, `SRBRGPREDOCSAI`, `SRBRGDOCSAIPROD`). Para el pipeline de promoción de IA (832) existe además la service connection WIF `AI DocClassExt Promocion IA` (ver RELEASE_MANAGEMENT §2.4) | ☐ |
| 1.2 | Dar permisos al SP del SC sobre el Resource Group | `Contributor` + `Key Vault Secrets User` | ☐ |
| 1.3 | Verificar que `azure-pipelines.yml` apunta al SC correcto | Variable `AZURE_SERVICE_CONNECTION` = `AI DocClassExt DEV` / `PRE` / `PRO` según `targetEnvironment` (se resuelve en el propio YAML) | ☐ |

---

### BLOQUE 2 — Recursos Azure (verificar o crear)

| # | Recurso | Nombre | Región | Estado esperado | OK |
|---|---------|--------|--------|-----------------|----|
| 3.1 | Resource Group | `SRBRGDOCSAIPROD` | West Europe | Existente | ☐ |
| 3.2 | Storage Account (Durable hub) | `srbstgproapppdocai` | West Europe | Existente | ☐ |
| 3.3 | Storage Account (documentos) | `srbstgprodocai` | West Europe | Existente | ☐ |
| 3.4 | Function App | `srbappprodocai` | West Europe | Existente (**.NET 10 Isolated**) | ☐ |
| 3.5 | Document Intelligence | `srbdiprodocai` | West Europe | Existente | ☐ |
| 3.6 | Azure OpenAI + Content Understanding | `upe48-mm2avmdm` (PRO; cuenta secundaria de CU `srbaisrv-westeurope`) | Sweden Central | Verificar deployment `gpt-4o-mini` (modelo gpt-4.1-mini) activo en PRE y PRO; en DEV el generativo es `gpt-5.6-luna` desde el 2026-11-20 (ADR-004) | ☐ |
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
| 4.6 | Document Intelligence: `Cognitive Services User` a la MI | Obligatorio en los tres entornos: el acceso es por alias e identidad desde la release v1.0.0 (2026-10-08). Portal → DI → IAM | ☐ |
| 4.7 | Azure OpenAI y cuentas AIServices (CU): `Cognitive Services User` y `Azure AI User` (Foundry) a la MI | Obligatorio en los tres entornos (ver `docs/releases/v1.0.0/release.md`, sección Configuración). Portal → recurso → IAM | ☐ |

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
| `FunctionsAdminApiFunctionKey` | Function key que usa el Admin para llamar a la Function App (exigido por el contrato; faltaba en esta tabla) |
| `GDC--Username` | Usuario servicio GDC (no exigido por el contrato; lo comprueba `verify-prod-prereqs.ps1`) |
| `GDC--Password` | Password servicio GDC (no exigido por el contrato; lo comprueba `verify-prod-prereqs.ps1`) |

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

Settings clave a confirmar (ver el inventario completo en `docs/infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md`):

| Setting | Valor esperado |
|---------|---------------|
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `Extraction__DefaultProvider` | `azure-content-understanding` |
| `Classification__DefaultProvider` | `azure-document-intelligence` |
| `Extraction__AzureContentUnderstanding__MaxConcurrentCalls` | `4` (baseline operativo actual; ajustar según carga) |
| `Extraction__AzureContentUnderstanding__HardTimeoutSeconds` | `90` (timeout duro por intento CU) |
| `Extraction__AzureContentUnderstanding__EnableCircuitBreaker` | `true` |
| `Extraction__AzureContentUnderstanding__CircuitBreakerFailureThreshold` | `5` |
| `Extraction__AzureContentUnderstanding__CircuitBreakerOpenSeconds` | `45` |
| `Extraction__AzureContentUnderstanding__MaxRetries` | `3` (reintentos con backoff exponencial) |
| `Extraction__AzureContentUnderstanding__InitialRetryDelayMs` | `500` (ms base para backoff; delay real = base × 2^(intento-1)) |
| `PromptDefaults__ModelKey` | Obligatorio y no vacío (sin él la resolución falla): `default.gpt4o-mini` en pre y pro (deployment `gpt-4o-mini` = gpt-4.1-mini hasta la siguiente release), `prompt.gpt56-luna` solo en dev (gpt-5.6-luna desde el 2026-11-20, ADR-004). Lo fija la variable de pipeline `promptDefaultsModelKey` (802) / `PROMPT_DEFAULTS_MODEL_KEY` (799); `ensure-app-settings` no pisa un valor ya existente (AB#99938) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connection string `srbappiprodocai` |
| `AssetResolver__BaseUrl` | `https://srbwebpluginassetresolver.azurewebsites.net/` |
| `AssetResolver__ApiKey` | Key Vault reference a `AssetResolverApiKey` |
| `GDC__BypassSslValidation` | `false` en `azure-pipelines.yml` y en `set-app-settings.ps1` desde AB#101553 (pendiente de despliegue: DEV primero; hasta entonces los tres entornos siguen con `true`); el handler valida contra `config/certs/sareb-ca-chain.pem`. `true` solo como emergencia manual. El YAML fija 83 app settings; el inventario completo está en INFRAESTRUCTURA_REAL_DESPLEGADA.md |
| `EnvironmentName` | `Production` (dev: `Development` · pre: `Preproduction`). Alimenta el campo `environment` de `GET management/configuration` con prioridad sobre `AZURE_FUNCTIONS_ENVIRONMENT`/`DOTNET_ENVIRONMENT` (no usar estos como fuente principal: en un Function App desplegado cambian el comportamiento del host — fallback a BD InMemory si valen `Development`). Lo fija la variable de pipeline `ENVIRONMENT_NAME` en **ambos** `azure-pipelines.yml` y `azure-pipelines-admin.yml` |

> Los 27 app settings heredados sin efecto (`Classification__GptFallback__*`, `Classification__AzureDocumentIntelligence__*`, `Classification__DefaultModelKey`, `Extraction__GptFallback__*` y la conexión de `Extraction__AzureContentUnderstanding__*`) se retiraron en AB#101450: el pipeline los borra en cada despliegue con `scripts/configuration/remove-legacy-app-settings.ps1` y el contrato los prohíbe. Sus cuatro secretos `*--ApiKey` se borran tras el despliegue con `scripts/configuration/remove-legacy-keyvault-secrets.ps1 -TargetEnvironment <env>`.

---

### BLOQUE 6 — Build y deploy inicial (pipeline)

| # | Tarea | Detalle | OK |
|---|-------|---------|-----|
| 7.1 | Verificar modo de ejecucion del pipeline | `trigger: none` y `pr: none` en los siete YAML de la raíz (`azure-pipelines.yml` es el pipeline 799): el despliegue es **manual** ("Run pipeline" en AzDO) con el parametro `targetEnvironment`. No hay disparo automatico por merge | ☐ |
| 7.1a | Verificar que `azure-pipelines-admin.yml` resuelve `AZURE_ASSET_RESOLVER_WEB_APP_NAME` por entorno | Antes estaba fijo al assetresolver de PRODUCCION y la validacion de app settings fallaba en dev con un error criptico (AB#99997); ahora hay una variable por entorno igual que en `azure-pipelines.yml` | ☐ |
| 7.1b | Confirmar el step "Ensure Functions environment name" del pipeline de Admin | Asegura el app setting `EnvironmentName` en el Function App tambien al desplegar el Admin (idempotente vía `ensure-app-settings.ps1`, no pisa valores existentes); asi el aviso de entorno del Admin funciona aunque solo se despliegue el Admin | ☐ |
| 7.2 | Confirmar que el Stage Build ejecuta restore/build/test/publish | AzDO Run logs | ☐ |
| 7.3 | Confirmar que el Stage DeployFunctions ejecuta zipDeploy | Task `AzureFunctionApp@2` en run. El stage tiene dos jobs, `DeployFunctionsJob` (Functions) y `DeployAdminJob` (Admin); el stage `RunMigrations` aparece omitido (`condition: false`) a propósito: las migraciones van por el pipeline 807 | ☐ |
| 7.4 | Confirmar que se aplica modo Key Vault en app settings | Task `AzureCLI@2` post-deploy | ☐ |
| 7.5 | Confirmar Stage `ValidateConfiguration` (valida contrato de App Settings) | Task `Validate App Settings contract` en run. El smoke test del endpoint NO forma parte del pipeline; se ejecuta a mano post-deploy (Bloque 8, tarea 9.2). Si `validate-azure-appsettings-contract.ps1` no puede leer los settings de una app (no existe en el RG o sin permisos), falla con un mensaje explicito indicando la app y el resource group, en vez del error generico de PowerShell "The property 'value' cannot be found" | ☐ |
| 7.6 | Verificar estado final del run | `Succeeded` en Build y DeployFunctions | ☐ |

> **Rama para encolar el pipeline:** tanto `azure-pipelines.yml` como `azure-pipelines-admin.yml` son de lanzamiento manual (`trigger: none`) y deben encolarse seleccionando la rama **`develop`** en "Run pipeline". La rama por defecto (`master`) no recibe los cambios de `ENVIRONMENT_NAME` / `AZURE_ASSET_RESOLVER_WEB_APP_NAME` hasta el siguiente merge a `master`.

---

### BLOQUE 7 — Migraciones de base de datos

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| 8.1 | ~~Si `RunDatabaseMigrationsOnStartup=true`: verificar que el primer arranque aplica migraciones automaticamente~~ No se usa: el pipeline fija `false` en cada despliegue y la vía vigente es el pipeline 807 (el código aún lee el setting) | ☐ |
| 8.2 | Si se aplican de forma manual (recomendado en prod): generar script SQL idempotente | `dotnet ef migrations script --idempotent -p src/backend/DocumentIA.Data -s src/backend/DocumentIA.Functions -o migrations.sql` | ☐ |
| 8.3 | Aplicar migrations con el pipeline **Migrations-BD** (`azure-pipelines-migrations.yml`), parámetro `targetEnvironment=prod`; revisar el artefacto `MigrationsScript` y aprobar el stage `Apply` (Generate en agente hosted; Apply en el pool privado `docia-mdp-private`) | Run del pipeline en ADO | ☐ |
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

  > Vía estándar: pipeline **Migrations-BD**. El comando sqlcmd anterior queda solo como contingencia manual documentada.

#### Paso 2 — Cargar/replicar los datos de configuración entre entornos

> **Solo para un entorno vacío (carga inicial).** Este procedimiento conserva los `Id` y, en un entorno con datos, reescribiría filas distintas (en `Tipologias` los `Id` 2224-2231 difieren entre DEV y PRE). Para una release, la configuración se aplica con los seeds propios y el diff por clave natural: RELEASE_MANAGEMENT.md, regla fija 4 y pasos 2.3 y 4.3.

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
- `-Mirror` borra en destino lo que no exista en origen (orden inverso de FK) y evita colisiones de clave única (`Codigo`/`Key` con distinto Id). No usar con destino PRE ni PRO (regla fija 4 de RELEASE_MANAGEMENT).
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
| 9.2b | Smoke E2E funcional post-deploy (clasificación y extracción) | `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile smoke`. Valida pipeline completo de ingest (clasificación, extracción, resumen, contrato) con corpus sintético marcado `submittedBy=e2e-postdeploy`. Reporte y cobertura en `tests/e2e-postdeploy/artifacts/`. Perfil `full` (45-90 min) bajo demanda en releases mayores. Ver `tests/e2e-postdeploy/README.md` | ☐ |
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
| B2.1 | Commit y push a rama de trabajo + PR a `develop` | Flujo controlado con aprobacion | ☐ |
| B2.2 | Lanzar el pipeline manualmente ("Run pipeline") con `targetEnvironment` correcto | El pipeline es `trigger: none`; no se dispara por merge a `develop` | ☐ |
| B2.3 | Verificar Stage **Build** completo | restore/build/test/publish sin errores | ☐ |
| B2.4 | Verificar Stage **DeployFunctions** completo | zipDeploy + AzureCLI settings + validacion de claves CU (el smoke test es manual, post-deploy) | ☐ |
| B2.5 | Revisar evidencias del run | logs, artifacts, duracion, warnings | ☐ |
| B2.6 | Si aplica Admin, revisar el job **`DeployAdminJob`** | El Admin se despliega en el job `DeployAdminJob` del stage DeployFunctions del 799 (y solo el Admin con el pipeline 800, stage `DeployAdmin`) | ☐ |

---

### B3 — Deploy manual (sin pipeline)

Usar solo si el pipeline no esta disponible o hay urgencia.

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| B3.1 | Activar PIM si necesario | Ver tareas VS Code: "Activate PIM role (user - device code)" | ☐ |
| B3.2 | Obtener credenciales Kudu | `az functionapp deployment list-publishing-credentials --resource-group SRBRGDOCSAIPROD --name srbappprodocai` (sustituir RG y Function App por los del entorno) | ☐ |
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
| B5.3 | Aplicar en Azure SQL con el pipeline **Migrations-BD** (`targetEnvironment` según entorno); en local usar `dotnet ef database update` | Run del pipeline en ADO / CLI local | ☐ |
| B5.4 | Verificar tablas y columnas nuevas | `sqlcmd ... -Q "SELECT name FROM sys.tables"` o `.\scripts\database\Query-Tipologias.ps1` | ☐ |

> Para **entorno limpio** (BD vacía) o para **promocionar datos de configuración** entre entornos (dev→pre→prod), ver el procedimiento completo paso a paso en **BLOQUE 7b** (migraciones de esquema + carga de datos con `replicate-config-data.ps1`).

> ✅ **Resuelto (revisión 2026-11-22):** PRO está al día de migraciones desde el 2026-10-08 (807 `prod` run 79691: 37/37, `Pendientes: 0`; ver `docs/releases/v1.0.0/release.md`, tabla «Runs de pipeline»). Texto histórico: la migración `20260805100351_AgregarSubmittedByEjecucion` (columna `SubmittedBy` en `DocumentoEjecuciones`) estaba aplicada en **dev** pero **no en PRO** a 2026-08-05. Aplicarla con este procedimiento en el próximo despliegue, **antes** de desplegar las Functions que la usan.

> ⚠️ **Costes de IA (2026-09-07; en PRO ya aplicado con v1.0.0, sigue vigente para entornos nuevos):** la funcionalidad necesita **dos migraciones** (`20260907075128_AddCostesIAToEjecuciones` y `20260907103326_AddCostesPorActividadYEstimado`, siete columnas anulables en `DocumentoEjecuciones`) **más el catálogo de tarifas**, que no viaja en las migraciones: es una fila de `ModeloConfigs` que se carga con `scripts/migrations/costes-ia/01-seed-tarifas-ia.sql`. Sin el catálogo el sistema no falla, pero toda ejecución sale con coste nulo y `tarifasCompletas=false`. Aplicar el catálogo **después** de las migraciones y **antes** de dar por buena la verificación. Ver [MANUAL_COSTES_IA.md](manuales/MANUAL_COSTES_IA.md).

> ⚠️ **Orden de despliegue Functions → Admin (2026-08-05; lo respeta el 799, que ejecuta el job de Functions antes que el del Admin):** los endpoints `/management/ejecuciones*` cambiaron de contrato (listado paginado `{items,total,page,pageSize}`, detalle por `{guid}/detalle`, agregados con filtro). El Admin actual depende de ellos: desplegar **primero la Function App y después el Admin**; en orden inverso, el Monitor quedará roto durante la ventana de despliegue.

---

### B6 — Verificacion post-deploy (siempre)

| # | Tarea | Script / Comando | OK |
|---|-------|------------------|----|
| B6.1 | Humo: /api/tipologias responde HTTP 200 | `.\scripts\testing\smoke-test-functions.ps1 -HostName srbappprodocai.azurewebsites.net` | ☐ |
| B6.2 | Revisar Application Insights durante 5-10 min post-deploy | Failures, exceptions, duraciones normales | ☐ |
| B6.3 | Verificar estado de secretos y referencias KV | `verify-prod-prereqs.ps1` sin errores y settings `Resolved` | ☐ |
| B6.4 | Verificar estado de recursos criticos Azure | Function App, Storage, SQL y DI en estado saludable | ☐ |
| B6.5 | Costes de IA: el bloque llega con importes al pedirlo y no aparece si no se pide | `pwsh .\scripts\testing\test-costes-ia.ps1 -Environment <env>` | ☐ |

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
| `scripts\testing\test-costes-ia.ps1 -Environment <env>` | Verificar el coste de IA por ejecucion: calculo, tarifas completas, desglose que cuadra y visibilidad condicionada a `incluirCostes` | Tras desplegar o tras tocar el catalogo de tarifas |
| `scripts\migrations\costes-ia\01-seed-tarifas-ia.sql` | Cargar o actualizar el catalogo de tarifas de IA (fila `tarifas.ia` de `ModeloConfigs`) | Tras las migraciones de costes y al cambiar precios |
| `scripts\migrations\costes-ia\02-tarifas-cera16v2-gpt41-cached.sql` | Anadir al catalogo las tarifas de `CERA16_v2` y `gpt-4.1-cached` (AB#100860) | Tras el 01, una vez por entorno |
| `scripts\database\backfill-costes-estimados.ps1` | Estimar el coste de ejecuciones anteriores a la medicion, marcandolas como estimadas | Una vez por entorno, tras cargar el catalogo |
| `tests/e2e-postdeploy/run-e2e-postdeploy.ps1` | Smoke E2E funcional post-deploy (perfiles smoke/full, cobertura funcional) | Manual, tras 9.2 |
| `scripts\database\Query-Tipologias.ps1` / `sqlcmd` | Verificar conectividad BD y estado tablas | Post migraciones |
| `scripts\database\replicate-config-data.ps1` | Replicar datos de **configuracion** (modelos/providers, tipologias, catalogos TDN1/TDN2, plugins, prompts) entre entornos. Modos `Export`/`Apply`/`Copy`, idempotente (MERGE+IDENTITY_INSERT) | Al promocionar configuracion dev→pre→prod |
| `scripts\legacy\list-analyzers.ps1` | Listar modelos disponibles en Document Intelligence | Diagnostico de clasificacion |
| `scripts\configuration\check-azure-permissions.ps1` | Verificar permisos del SP / usuario sobre el RG | Antes del primer deploy |
| `scripts\configuration\assign-keyvault-rbac.ps1 -TargetEnvironment <env>` | **Prerrequisito RBAC**: habilita las MI de Function App/Admin/AssetResolver y les asigna `Key Vault Secrets User` (rol elevado/PIM) | Una vez por entorno, antes del pipeline |
| `scripts\configuration\verify-keyvault-rbac.ps1 -TargetEnvironment <env>` | Comprobar (solo lectura) que las MI tienen `Key Vault Secrets User` | Antes/después del pipeline o ante fallo de RBAC |

> **Nota:** `compile-all-plugins.ps1` y `check-database.ps1` referenciados en versiones anteriores **ya no existen**. Para compilar plugins de enriquecimiento ver [docs/manuales/MANUAL_PLUGINS.md](manuales/MANUAL_PLUGINS.md), Paso 2 (compilación manual: `dotnet build src/enrichments/SarebEnrichments/SarebEnrichments.csproj -c Release` y copia de la DLL a `plugins/`; los scripts `scripts/compile-*-plugins.ps1` se retiraron en el commit `ab365619`); para la BD usar `scripts\database\Query-Tipologias.ps1`, `scripts\database\diagnose-assetresolver-sql.ps1` o `sqlcmd`.

> `set-keyvault-secrets.ps1` no contiene valores secretos versionados. Los valores deben aportarse mediante variables `DOCIA_SECRET_*` o mediante `-PreferLocalSettings -LocalSettingsPath <ruta local no versionada>`.

---

## Notas de estado (histórico 2026-04-06)

> Estado actual: ver INFRAESTRUCTURA_REAL_DESPLEGADA.md. Admin y AssetResolver se despliegan con el 799 (y por separado con 800 y 801) en los tres entornos; el RBAC de Key Vault de las MI se asigna una vez por entorno con `assign-keyvault-rbac.ps1`. La fila «Parcialmente configurado» del RBAC es de abril de 2026; a 2026-11-23 las tres identidades (Functions, Admin y AssetResolver) tienen Key Vault Secrets User sobre el Key Vault de su entorno en DEV, PRE y PRO.

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

## BLOQUE 9 — Verificar App Insights post-despliegue

Usar tras cualquier despliegue a produccion para confirmar que la observabilidad esta operativa. (Antes figuraba como un segundo «BLOQUE 6»; las filas conservan su numeración 6.x.) Los nombres son de PRO; en DEV y PRE: `srbappidevdocai` / `srbappipredocai`. Estado a 2026-11-23: App Insights con retención de 90 días en los tres entornos, sin consultas guardadas compartidas en PRO y sin Availability Tests; Live Metrics es una vista en vivo sin estado que comprobar.

| # | Tarea | Detalle | OK |
|---|-------|---------|----|
| 6.1 | `APPLICATIONINSIGHTS_CONNECTION_STRING` presente en App Settings de **`srbappprodocai`** (Functions) | Portal → `srbappprodocai` → Configuration → Application Settings → buscar clave | ☐ |
| 6.1a | `APPLICATIONINSIGHTS_CONNECTION_STRING` presente en **`srbwebadminprodocai`** (Admin) | Portal → Admin → Configuration / Application Insights → On | ☐ |
| 6.1b | `APPLICATIONINSIGHTS_CONNECTION_STRING` presente en **`srbwebpluginassetresolver`** (AssetResolver) — *requisito obligatorio: faltaba historicamente y rompe el stage `ValidateConfiguration`* | Portal → AssetResolver App Service → Application Insights → On (o app setting manual a `srbappiprodocai`) | ☐ |
| 6.2 | Live Metrics accesible y reactivo | `srbappiprodocai` → Live Metrics → debe mostrar servidor activo en < 5 s tras invocar cualquier endpoint | ☐ |
| 6.3 | Exceptions aparecen en Failures | Provocar un error controlado (parametro invalido) → verificar en App Insights → Failures en < 2 min | ☐ |
| 6.4 | Durable Functions Monitor accesible | Portal → Function App → Durable Functions → listar instancias de las ultimas 24 h sin error de acceso | ☐ |
| 6.5 | Log Analytics workspace vinculado y queries disponibles | `srbappiprodocai` → Logs → ejecutar la query guardada `DocumentIA-Q1` sin error de workspace (la referencia anterior a «sec. 4.12.4 de Manual Explotacion» no existe en `docs/`; las consultas vigentes están en [OBSERVABILIDAD_KQL.md](observabilidad/OBSERVABILIDAD_KQL.md)) | ☐ |
| 6.6 | Retencion de datos ≥ 30 dias | `srbappiprodocai` → Usage and estimated costs → Data Retention: verificar ≥ 30 dias | ☐ |

> **Referencia**: ver [MONITOREO_ALERTAS_REAL.md](observabilidad/MONITOREO_ALERTAS_REAL.md) y [OBSERVABILIDAD_KQL.md](observabilidad/OBSERVABILIDAD_KQL.md).  
> Queries KQL base guardadas en Log Analytics como `DocumentIA-Q1` … `DocumentIA-Q4`.

---

## Seguridad del Admin — autenticacion (EasyAuth activo en DEV, PRE y PRO)

Estado 2026-11-22: App Service Authentication (EasyAuth) activo en el Admin de DEV, PRE y PRO (DEV 2026-08-06; PRE y PRO 2026-09-04; detalle en [guias/GUIA_EASYAUTH_ADMIN.md](guias/GUIA_EASYAUTH_ADMIN.md)). Restricciones de acceso de red a 2026-11-23: «Allow all», `publicNetworkAccess` habilitado y sin private endpoint en el Admin de DEV, PRE y PRO, igual que el 2026-07-31; la integracion VNet del Admin de prod es de salida y no limita el acceso entrante.

| # | Tarea | Detalle | OK |
|---|-------|---------|----|
| S.1 | Activar App Service Authentication (EasyAuth) en el Admin | App registration single-tenant + grupo de seguridad con los usuarios autorizados y "Assignment required = Yes". Solicitud al equipo de identidad redactada en [guias/SOLICITUD_APP_REGISTRATION_ADMIN.md](guias/SOLICITUD_APP_REGISTRATION_ADMIN.md); configuracion del App Service en [guias/GUIA_EASYAUTH_ADMIN.md](guias/GUIA_EASYAUTH_ADMIN.md). **Estado: activada en los tres entornos (DEV 2026-08-06; PRE y PRO 2026-09-04), ver la guia.** | ☑ |
| S.2 | (Medida transitoria, opcional) Restringir el acceso de red al Admin | Limita desde donde se accede, no quien; el sitio `scm` tiene reglas propias. Ver [guias/GUIA_RESTRICCION_ACCESO_ADMIN.md](guias/GUIA_RESTRICCION_ACCESO_ADMIN.md) | ☐ |
| S.3 | Confirmar que el modo solo lectura automatico sigue como salvaguarda (con EasyAuth activo no se dispara salvo sesion anonima) | Sin usuario autenticado, el Admin rechaza toda escritura con "Modo solo lectura: no hay un usuario autenticado..."; las consultas siguen funcionando. No aplica en desarrollo local (localhost) | ☑ |

---

## Referencias

| Documento | Contenido |
|-----------|-----------|
| [01_ARQUITECTURA_SISTEMA.md](01_ARQUITECTURA_SISTEMA.md) | Arquitectura completa y diagrama de despliegue |
| [procedimientos/RELEASE_MANAGEMENT.md](procedimientos/RELEASE_MANAGEMENT.md) | Runbook de release DEV → PRE → PRO, catálogo de pipelines y vuelta atrás |
| [infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md](infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md) | Recursos, app settings y secretos por entorno |
| [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md) | Configuracion tecnica detallada |
| [../azure-pipelines.yml](../azure-pipelines.yml) | Pipeline 799: Build + RunMigrations (deshabilitado) + DeployFunctions (jobs Functions y Admin) + DeployAssetResolver + ValidateConfiguration |
| [procedimientos/DESPLIEGUE_IA.md](procedimientos/DESPLIEGUE_IA.md) | Promocion de artefactos de IA por entornos (pipeline 832) |
| [../azure-pipelines-admin.yml](../azure-pipelines-admin.yml) | Pipeline hotfix del Admin: BuildAdmin + DeployAdmin (incluye RBAC KV, settings Functions API, `EnvironmentName` y validacion de contrato) |
| [guias/GUIA_EASYAUTH_ADMIN.md](guias/GUIA_EASYAUTH_ADMIN.md) | Activar App Service Authentication (EasyAuth) en el Admin |
| [guias/GUIA_RESTRICCION_ACCESO_ADMIN.md](guias/GUIA_RESTRICCION_ACCESO_ADMIN.md) | Restringir el acceso de red al Admin |
| [guias/SOLICITUD_APP_REGISTRATION_ADMIN.md](guias/SOLICITUD_APP_REGISTRATION_ADMIN.md) | Solicitud a identidad: app registration + grupo de seguridad para EasyAuth |
