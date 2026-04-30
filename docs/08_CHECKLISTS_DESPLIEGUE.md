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
| 3.4 | Function App | `srbappprodocai` | West Europe | Existente (.NET 8 Isolated) | ☐ |
| 3.5 | Document Intelligence | `srbdiprodocai` | West Europe | Existente | ☐ |
| 3.6 | Azure OpenAI + Content Understanding | `upe48-mm2avmdm` | Sweden Central | Verificar deployment `gpt-4o-mini` activo | ☐ |
| 3.7 | Application Insights | `srbappiprodocai` | West Europe | Existente | ☐ |
| 3.8 | Key Vault | `srbkvprodocai` | West Europe | Existente | ☐ |
| 3.9 | **Azure SQL Server + Database** | `srbsqlprodocai` / `DocumentIA` | West Europe | **PENDIENTE DE CREAR** | ☐ |
| 3.10 | Web App Admin | `srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai` | West Europe | **PENDIENTE DE CREAR** (EP5 roadmap) | ☐ |

> Mientras Azure SQL no esté disponible, la BD se sirve desde Docker SQL local.  
> Connection string temporal: `Server=<IP-MAQUINA>,1433;Database=DocumentIA;User Id=sa;Password=COMPLETAR_SQL_PASSWORD;TrustServerCertificate=True;`

---

### BLOQUE 3 — Managed Identity + RBAC

| # | Tarea | Comando / Portal | OK |
|---|-------|------------------|----|
| 4.1 | Activar System Managed Identity en `srbappprodocai` | Portal → Function App → Identity → System assigned: **On** | ☐ |
| 4.2 | KV `srbkvprodocai`: rol `Key Vault Secrets User` a la MI | Portal → KV → Access control (IAM) | ☐ |
| 4.3 | Storage `srbstgprodocai`: rol `Storage Blob Data Contributor` a la MI | Portal → Storage → IAM | ☐ |
| 4.4 | Storage `srbstgproapppdocai`: roles `Blob Data Owner` + `Queue Data Contributor` + `Table Data Contributor` a la MI | Portal → Storage → IAM | ☐ |
| 4.5 | Azure SQL (cuando esté listo): `CREATE USER [srbappprodocai] FROM EXTERNAL PROVIDER` + `db_datareader` / `db_datawriter` | SSMS / sqlcmd en Azure SQL | ☐ |
| 4.6 | Document Intelligence: `Cognitive Services User` a la MI (si AuthMode=MI) | Portal → DI → IAM | ☐ |
| 4.7 | Azure OpenAI: `Cognitive Services User` a la MI (si AuthMode=MI) | Portal → OpenAI → IAM | ☐ |

---

### BLOQUE 4 — Key Vault: cargar secretos

| # | Tarea | Script | OK |
|---|-------|--------|----|
| 5.1 | Login Azure CLI con cuenta que tenga acceso a KV | `az login` | ☐ |
| 5.2 | Activar rol PIM si necesario | `.\scripts\activate-pim.ps1 -UseUser` | ☐ |
| 5.3 | Verificar elegibilidad PIM | `.\scripts\list-pim-eligible.ps1 -UseUser` | ☐ |
| 5.4 | Verificar permisos sobre el RG | `.\scripts\check-azure-permissions.ps1` | ☐ |
| 5.5 | Cargar todos los secretos en Key Vault | `.\scripts\set-keyvault-secrets.ps1 -SubscriptionId <ID>` | ☐ |
| 5.6 | Verificar que los secretos existen en KV | `.\scripts\verify-prod-prereqs.ps1` | ☐ |

Secretos requeridos en KV:

| Secreto KV | Descripcion |
|-----------|-------------|
| `AzureWebJobsStorage` | Connection string Storage Durable hub |
| `AzureStorageConnectionString` | Connection string Storage documentos |
| `SqlConnectionString` | Connection string Azure SQL (o Docker temporal) |
| `user-ods-dwh` | Connection string ODS DWH usada por `ConnectionStrings__AssetResolverDb` del AssetResolver |
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
| 6.1 | Aplicar todos los App Settings (infraestructura + AI + GDC) | `.\scripts\set-app-settings.ps1` | ☐ |
| 6.2 | Aplicar referencias Key Vault en settings de secretos | `.\scripts\set-functionapp-keyvault-references.ps1 -SubscriptionId <ID>` | ☐ |
| 6.3 | Verificar que la Function App tiene `RunDatabaseMigrationsOnStartup=true` (primer arranque) | `az functionapp config appsettings list --name srbappprodocai --resource-group SRBRGDOCSAIPROD` | ☐ |
| 6.4 | Verificar `host.json`: `hubName=DocumentIAHub`, storage provider correcto | `src/backend/DocumentIA.Functions/host.json` | ☐ |

Settings clave a confirmar (ver detalle completo en `docs/04_MANUAL_EXPLOTACION.md` § 4.4):

| Setting | Valor esperado |
|---------|---------------|
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `Extraction__DefaultProvider` | `azure-content-understanding` |
| `Classification__DefaultProvider` | `azure-document-intelligence` |
| `Extraction__AzureContentUnderstanding__Endpoint` | `https://upe48-mm2avmdm.cognitiveservices.azure.com/` |
| `Classification__AzureDocumentIntelligence__Endpoint` | `https://srbdiprodocai.cognitiveservices.azure.com/` |
| `Extraction__GptFallback__DeploymentName` | `gpt-4o-mini` |
| `Classification__GptFallback__DeploymentName` | `gpt-4o-mini` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connection string `srbappiprodocai` |

---

### BLOQUE 6 — Build y deploy inicial (pipeline)

| # | Tarea | Detalle | OK |
|---|-------|---------|-----|
| 7.1 | Verificar trigger y rama de despliegue | `trigger.branches.include: [main]` en `azure-pipelines.yml` | ☐ |
| 7.2 | Confirmar que el Stage Build ejecuta restore/build/test/publish | AzDO Run logs | ☐ |
| 7.3 | Confirmar que el Stage DeployFunctions ejecuta zipDeploy | Task `AzureFunctionApp@2` en run | ☐ |
| 7.4 | Confirmar que se aplica modo Key Vault en app settings | Task `AzureCLI@2` post-deploy | ☐ |
| 7.5 | Confirmar resolución de hostname y smoke test | Tasks `Resolve Function App host name` + `Smoke test Functions endpoint` | ☐ |
| 7.6 | Verificar estado final del run | `Succeeded` en Build y DeployFunctions | ☐ |

---

### BLOQUE 7 — Migraciones de base de datos

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| 8.1 | Si `RunDatabaseMigrationsOnStartup=true`: verificar que el primer arranque aplica migraciones automaticamente | Logs de Function App en AppInsights o Kudu | ☐ |
| 8.2 | Si se aplican de forma manual (recomendado en prod): generar script SQL idempotente | `dotnet ef migrations script --idempotent -p src/backend/DocumentIA.Data -s src/backend/DocumentIA.Functions -o migrations.sql` | ☐ |
| 8.3 | Aplicar `migrations.sql` contra Azure SQL via sqlcmd o Azure Portal Query Editor | `sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -i migrations.sql` | ☐ |
| 8.4 | Una vez aplicadas las migraciones, cambiar `RunDatabaseMigrationsOnStartup=false` | `az functionapp config appsettings set --settings RunDatabaseMigrationsOnStartup=false ...` | ☐ |
| 8.5 | Verificar conectividad y tablas creadas | `.\scripts\check-database.ps1` | ☐ |

---

### BLOQUE 8 — Verificacion post-deploy

| # | Tarea | Script / Comando | OK |
|---|-------|------------------|----|
| 9.1 | Verificar todos los prerrequisitos de prod OK | `.\scripts\verify-prod-prereqs.ps1` | ☐ |
| 9.2 | Smoke test del endpoint `/api/tipologias` | `.\scripts\smoke-test-functions.ps1 -HostName srbappprodocai.azurewebsites.net` | ☐ |
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
| B2.2 | Verificar run de pipeline disparado por merge a `main` | Trigger OK en AzDO | ☐ |
| B2.3 | Verificar Stage **Build** completo | restore/build/test/publish sin errores | ☐ |
| B2.4 | Verificar Stage **DeployFunctions** completo | zipDeploy + AzureCLI settings + smoke test | ☐ |
| B2.5 | Revisar evidencias del run | logs, artifacts, duracion, warnings | ☐ |
| B2.6 | Si aplica Admin, habilitar y revisar Stage **DeployAdmin** | `condition: succeeded()` cuando exista `srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai` | ☐ |

---

### B3 — Deploy manual (sin pipeline)

Usar solo si el pipeline no esta disponible o hay urgencia.

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| B3.1 | Activar PIM si necesario | `.\scripts\activate-pim.ps1 -UseUser` | ☐ |
| B3.2 | Obtener credenciales Kudu | `az functionapp deployment list-publishing-credentials --resource-group SRBRGDOCSAIPROD --name srbappprodocai` | ☐ |
| B3.3 | Ejecutar deploy-manual.ps1 | `.\scripts\deploy-manual.ps1 -KuduUser '$srbappprodocai' -KuduPassword '<PASS>'`  | ☐ |
| B3.4 | Alternativa Cloud Shell | `.\scripts\deploy-manual.ps1` → subir `publish/functions.zip` → `az functionapp deploy ...` | ☐ |

---

### B4 — Cambios de configuracion / secretos

| # | Tarea | Script | OK |
|---|-------|--------|----|
| B4.1 | Actualizar secreto en Key Vault | `.\scripts\set-keyvault-secrets.ps1 -SubscriptionId <ID>` | ☐ |
| B4.2 | Si es un nuevo setting no secreto: actualizar `set-app-settings.ps1` y re-ejecutar | `.\scripts\set-app-settings.ps1` | ☐ |
| B4.3 | Forzar restart de la Function App para tomar cambios | `az functionapp restart --name srbappprodocai --resource-group SRBRGDOCSAIPROD` | ☐ |
| B4.4 | Verificar que las referencias KV estan en estado `Resolved` | Azure Portal → Function App → Configuration → Application Settings | ☐ |

---

### B5 — Migraciones de BD

| # | Tarea | Comando | OK |
|---|-------|---------|-----|
| B5.1 | Generar script idempotente | `dotnet ef migrations script --idempotent -p src/backend/DocumentIA.Data -s src/backend/DocumentIA.Functions -o migrations.sql` | ☐ |
| B5.2 | Revisar `migrations.sql` antes de aplicar | Especialmente: columnas ADD, tablas CREATE, indices | ☐ |
| B5.3 | Aplicar en Azure SQL (o Docker si es local) | `sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -i migrations.sql` | ☐ |
| B5.4 | Verificar tablas y columnas nuevas | `.\scripts\check-database.ps1` | ☐ |

---

### B6 — Verificacion post-deploy (siempre)

| # | Tarea | Script / Comando | OK |
|---|-------|------------------|----|
| B6.1 | Humo: /api/tipologias responde HTTP 200 | `.\scripts\smoke-test-functions.ps1 -HostName srbappprodocai.azurewebsites.net` | ☐ |
| B6.2 | Revisar Application Insights durante 5-10 min post-deploy | Failures, exceptions, duraciones normales | ☐ |
| B6.3 | Verificar estado de secretos y referencias KV | `verify-prod-prereqs.ps1` sin errores y settings `Resolved` | ☐ |
| B6.4 | Verificar estado de recursos criticos Azure | Function App, Storage, SQL y DI en estado saludable | ☐ |

---

## Referencia rapida de scripts de despliegue

| Script | Uso | Cuando ejecutar |
|--------|-----|-----------------|
| `scripts\activate-pim.ps1 -UseUser` | Activar rol PIM | Antes de cualquier operacion Azure CLI que requiera Contributor |
| `scripts\verify-prod-prereqs.ps1` | Verificar KV, secretos, Function App, Storage | Antes del deploy y post-deploy |
| `scripts\set-keyvault-secrets.ps1 -SubscriptionId <ID>` | Cargar / actualizar secretos en KV | Primer deploy o rotacion de claves |
| `scripts\set-functionapp-keyvault-references.ps1 -SubscriptionId <ID>` | Aplicar modo KV en App Settings | Primer deploy o tras reset de settings |
| `scripts\set-app-settings.ps1` | Aplicar todos los App Settings no-secretos | Primer deploy o cambio de configuracion no-secreta |
| `scripts\compile-all-plugins.ps1` | Compilar SarebEnrichments.dll | Cuando cambien los plugins de enriquecimiento |
| `scripts\deploy-manual.ps1` | Build + zip + deploy via Kudu | Deploy manual sin pipeline |
| `scripts\smoke-test-functions.ps1 -HostName <host>` | Verificar endpoint `/api/tipologias` | Post cada deploy |
| `scripts\check-database.ps1` | Verificar conectividad BD y estado tablas | Post migraciones |
| `scripts\list-analyzers.ps1` | Listar modelos disponibles en Document Intelligence | Diagnostico de clasificacion |
| `scripts\check-azure-permissions.ps1` | Verificar permisos del SP / usuario sobre el RG | Antes del primer deploy |

---

## Notas de estado actual (2026-04-06)

| Item | Estado |
|------|--------|
| Azure SQL `srbsqlprodocai` | **Pendiente de crear** — usando Docker SQL local temporalmente |
| Web App Admin `srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai` | **Pendiente de crear** — Stage DeployAdmin desactivado (`condition: false`) |
| Stage Deploy Admin en pipeline | Desactivado hasta que exista `srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai` |
| Key Vault `srbkvprodocai` | Existente — secretos pendientes de cargar con `set-keyvault-secrets.ps1` |
| Managed Identity RBAC | Parcialmente configurado — revisar Bloque 3 |
| Function App App Settings | Configurados en FASE 3 (2026-03-31) |
| Migraciones EF Core en Docker SQL | Aplicadas y verificadas |

---

## BLOQUE 6 — Verificar App Insights post-despliegue

Usar tras cualquier despliegue a produccion para confirmar que la observabilidad esta operativa.

| # | Tarea | Detalle | OK |
|---|-------|---------|----|
| 6.1 | `APPLICATIONINSIGHTS_CONNECTION_STRING` presente en App Settings | Portal → `srbappprodocai` → Configuration → Application Settings → buscar clave | ☐ |
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
| [04_MANUAL_EXPLOTACION.md](04_MANUAL_EXPLOTACION.md) | Procedimientos paso a paso, variables de entorno, scripts |
| [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md) | Configuracion tecnica detallada |
| [07_ROADMAP_PENDIENTES.md](07_ROADMAP_PENDIENTES.md) | Pendientes: Azure SQL, KV completo, Admin Web App |
| [../scripts/README-activate-pim.md](../scripts/README-activate-pim.md) | Guia de activacion PIM |
| [../azure-pipelines.yml](../azure-pipelines.yml) | Pipeline CI/CD: Build + DeployFunctions + DeployAdmin |
