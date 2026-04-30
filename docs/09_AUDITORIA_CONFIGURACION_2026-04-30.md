# 09. Auditoria de Configuracion - 2026-04-30

## Objetivo

Recoger el diagnostico de configuracion del ecosistema DocumentIA MVP y dejar un plan revisable para una fase posterior de correccion y alineacion.

La auditoria original se ejecuto en modo lectura. Este documento no aplica cambios sobre recursos, pipelines, secretos, bases de datos ni configuraciones runtime.

## Alcance Revisado

- Entornos locales y configuracion de desarrollo.
- Infraestructura y servicios en Azure.
- Pipelines CI/CD, despliegue y automatizacion.
- Documentacion tecnica y operativa.
- App Settings, Key Vault references y configuracion dinamica prevista en SQL.
- Configuracion de tipologias, modelos, plugins y servicios externos.

## Mapa Actual De Configuracion

### Fuentes Locales

| Fuente | Uso observado | Estado |
|---|---|---|
| `.env.example` | Plantilla historica de variables Azure/AI | Desalineada con nombres actuales |
| `docker-compose.yml` | SQL Server local + Azurite | Activa para desarrollo local |
| `.vscode/tasks.json` | Build, publish, Functions host, Admin, Desktop, AssetResolver, PIM | Activa en VS Code |
| `src/backend/DocumentIA.Functions/appsettings.json` | Defaults de Functions | Parcialmente superado por App Settings Azure |
| `src/frontend/DocumentIA.Admin/appsettings.json` | URL y Function Key de Functions para Admin | Contiene secreto literal |
| `src/plugins/DocumentIA.AssetResolver/appsettings.json` | Defaults de AssetResolver | Usa referencias Key Vault en produccion |
| `src/backend/DocumentIA.Functions/config/**` | Seeds de modelos, tipologias y plugins | Activo en arranque si BD no contiene datos |
| `scripts/**` | Despliegue, pruebas, Key Vault, PIM, smoke tests | Mixto: algunos activos, otros historicos |
| `publish/`, `bin/`, `scripts/seeds/` | Artefactos generados o snapshots | Contienen duplicados y secretos |

### Fuentes En Azure

| Recurso | Nombre | Estado observado |
|---|---|---|
| Suscripcion | Produccion Central | Default, enabled |
| Resource Group | `SRBRGDOCSAIPROD` | Activo |
| Function App | `srbappprodocai` | Running, Linux, SystemAssigned MI |
| Admin Web App | `srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai` | Running, Linux, SystemAssigned MI |
| AssetResolver Web App | `srbwebpluginassetresolver` | Running, Linux, SystemAssigned MI |
| Key Vault | `srbkvprodocai` | RBAC enabled, public access enabled |
| SQL Server / DB | `srbsqlprodocai` / `DocumentIA` | Ready / Online, public network disabled |
| Storage documentos | `srbstgprodocai` | Public network disabled, default action deny |
| Storage Functions | `srbstgproapppdocai` | Public network enabled, default action allow |
| Application Insights | `srbappiprodocai` | Existente |
| Document Intelligence | `srbdiprodocai` | S0, public access enabled |
| OpenAI / AI Services | `srboaiprodocai`, `upe48-mm2avmdm-swedencentral` | S0, public access enabled |

### CI/CD

| Fuente | Estado |
|---|---|
| `azure-pipelines.yml` | Pipeline principal activo en Azure DevOps, trigger en `main` |
| Pipeline ADO `AI DocClassExt` id `799` | Ultimo run revisado `20260430.2` / `78011`, succeeded |
| `.github/workflows/infrastructure.yml` | Probablemente obsoleto o roto; apunta a infraestructura inexistente |
| Stage `RunMigrations` | Definido pero deshabilitado con `condition: false` |

## Inventario De Ficheros De Configuracion Y Uso

Esta seccion responde especificamente a que ficheros de configuracion existen, cuales parecen ser fuente activa y cuales son candidatos a obsoletos, duplicados o generados. No todo esta correcto: hay varias familias de configuracion que conviene limpiar o alinear.

### Fuentes Activas O Canonicas

| Fichero / familia | Entorno | Uso real observado | Diagnostico |
|---|---|---|---|
| `azure-pipelines.yml` | Azure DevOps / prod | Pipeline principal de build, test y deploy | Activo, pero con desalineaciones de settings |
| `.vscode/tasks.json` | Dev local | Tareas de build, Functions host, Admin, Desktop, AssetResolver, PIM | Activo para VS Code |
| `docker-compose.yml` | Dev local | SQL Server local + Azurite | Activo para desarrollo, con password dev-only |
| `src/backend/DocumentIA.Functions/host.json` | Dev/prod | Runtime Durable Functions, hub, concurrencia, logging | Activo y debe tratarse como canonico |
| `src/backend/DocumentIA.Functions/appsettings.json` | Dev/prod fallback | Defaults de Functions; superado por App Settings Azure en prod | Activo como fallback, no fuente unica de verdad |
| `src/backend/DocumentIA.Functions/local.settings.json` | Dev local | Configuracion local real, ignorada por Git | Activo local; contiene valores sensibles/literales y claves `_dev` auxiliares |
| `src/backend/DocumentIA.Functions/config/classification/models.json` | Seed / runtime inicial | Seed de `ModeloConfigs` para clasificacion | Activo si BD no tiene datos; contiene API keys literales y debe sanearse |
| `src/backend/DocumentIA.Functions/config/extraction/models.json` | Seed / runtime inicial | Seed de `ModeloConfigs` para extraccion | Activo si BD no tiene datos; contiene API keys literales y debe sanearse |
| `src/backend/DocumentIA.Functions/config/prompt/models.json` | Seed / runtime inicial | Seed de `ModeloConfigs` para prompts | Activo si BD no tiene datos; contiene API key literal y debe sanearse |
| `src/backend/DocumentIA.Functions/config/layout/models.json` | Seed / runtime inicial | Seed de modelos layout | Activo si BD no tiene datos |
| `src/backend/DocumentIA.Functions/config/tipologias/*.validation.json` | Seed / runtime inicial | Configuracion de tipologias/versiones | Activo si BD no tiene datos; pendiente contrastar con SQL real |
| `src/backend/DocumentIA.Functions/config/tipologias/*.plugins.json` | Seed / runtime inicial | Configuracion de plugins por tipologia | Activo si BD no tiene datos; pendiente contrastar con SQL real |
| `src/frontend/DocumentIA.Admin/appsettings.json` | Dev/prod publish | URL y Function Key para llamar Functions | Activo, pero contiene secreto literal |
| `src/frontend/DocumentIA.Admin/appsettings.Development.json` | Dev local | Overrides locales Admin | Activo local; FunctionKey vacia |
| `src/plugins/DocumentIA.AssetResolver/appsettings.json` | Dev/prod publish | Defaults del plugin, KV refs, aliases globales | Activo; falta revisar divergencia de aliases documentados |
| `src/plugins/DocumentIA.AssetResolver/appsettings.Development.json` | Dev local | SQL local ODS + ApiKey dev | Activo local, ignorado por Git, contiene valores sensibles dev |
| `scripts/set-app-settings.ps1` | Operacion Azure | Aplica App Settings no secretos de Function App | Activo, pero no configura AssetResolver en Functions |
| `scripts/set-functionapp-keyvault-references.ps1` | Operacion Azure | Aplica modo Key Vault basico y reinicia Functions | Activo, pero parcial; no cubre todos los settings sensibles |
| `scripts/set-keyvault-secrets.ps1` | Operacion Azure | Carga secretos en Key Vault | Activo o reutilizable; revisar que cubre nombres actuales |
| `scripts/verify-prod-prereqs.ps1` / `scripts/verify-program-config.ps1` | Operacion / validacion | Validaciones auxiliares | Activos o utiles para preflight |
| `scripts/smoke-test-functions.ps1`, `smoke_e2e*.ps1`, `tests/api-tests/*.ps1` | Pruebas | Smoke/e2e/manual tests contra Functions/GDC/AssetResolver | Activos, pero algunos dependen de `local.settings.json` |

### Ficheros Obsoletos, Historicos O Candidatos A Limpieza

| Fichero / familia | Motivo | Recomendacion |
|---|---|---|
| `.env.example` | Plantilla historica; usa nombres `AZURE_*` que no son la fuente real de runtime .NET actual | Reemplazar por plantilla local actualizada o marcar como legacy |
| `scripts/2 setup-config-files.ps1` | Genera `.env.example` y `.gitignore` de una version inicial del proyecto; no refleja la configuracion actual | Archivar o actualizar; no usar como guia actual |
| `scripts/1 setup-folders.ps1` a `scripts/5 setup-ci-cd.ps1` | Scaffolding inicial del MVP, no operacion diaria | Marcar como historicos si ya no se usan |
| `.azure/plan.md` | Plan antiguo de Azure Content Understanding, no deployment plan vigente | Archivar como historico o mover a docs/superpowers si aplica |
| `.github/workflows/infrastructure.yml` | Apunta a `infrastructure/**` inexistente y Bicep inexistente; sintaxis `creds` sospechosa | Eliminar, deshabilitar o reescribir si GitHub Actions vuelve a ser necesario |
| `README.md` | Contiene referencias antiguas a `.env`, Bicep, .NET 8 y rutas/manuales movidos | Actualizar para evitar que se use como guia de configuracion |
| `docs/not in use/**` | Documentacion explicitamente no canonica | Mantener fuera del flujo operativo o archivar |
| `docs/08_CHECKLISTS_DESPLIEGUE.md` | Parte del checklist dice SQL/Admin pendientes, pero ya existen | Actualizar; no usar literalmente sin revision |
| `docs/01_ARQUITECTURA_SISTEMA.md` | Dice que MI/RBAC Cognitive Services esta pendiente, pero ya hay roles asignados | Actualizar estado real |
| `docs/04_MANUAL_EXPLOTACION.md` | Mezcla estado actual con procedimientos/manuales previos | Actualizar secretos, migraciones y Azure SQL real |
| `.github/agents/Azure_function_codegen_and_deployment.chatmode.md` | Agente generico con acciones de deploy/delete no especificas del estado actual | No usar para operacion del MVP sin revision |
| `.superpowers.old/**` | Copia/paquete historico ajeno al runtime de DocumentIA | Excluir del inventario operativo |

### Artefactos Generados O Duplicados Que No Deben Ser Fuente De Verdad

| Fichero / familia | Observacion | Riesgo |
|---|---|---|
| `publish/functions/**` | Copia publicada de `host.json` y `config/**` | Contiene secretos duplicados; no debe editarse como fuente |
| `src/**/bin/**` | Salidas de compilacion con copias de `appsettings`, `host.json`, `local.settings` y `config/**` | Multiplica secretos y confunde auditorias |
| `src/**/obj/**` | Intermedios de build | No debe auditarse como fuente funcional |
| `scripts/seeds/20260410-080308/**` | Snapshot antiguo de seeds | Duplicado con posibles secretos; no se sabe si corresponde a BD real |
| `src/backend/.vs/**/applicationhost.config` | Config de IIS Express/Visual Studio | Local, no fuente de runtime Azure |
| `.venv/**`, `src/ai-models/venv/**` | Entornos Python | No son configuracion del producto; excluir de auditoria operativa |
| `artifacts/**` | Evidencias/reportes generados | No son fuente de configuracion activa salvo que se declare explicitamente |

### Configuracion Local Dev Frente A Azure

| Area | Dev local observado | Azure observado | Diagnostico |
|---|---|---|---|
| Functions config | `local.settings.json` existe localmente e incluye `SecretsSource`, `KeyVaultName`, SQL, Storage, GDC y AssetResolver | App Settings de Azure contienen la mayoria de settings, pero no `AssetResolver__*` | Desalineado: dev tiene AssetResolver, prod no |
| AssetResolver API key | En local hay `AssetResolver:ApiKey` como KV reference y `AssetResolver:ApiKey_dev` literal | En Functions prod no existe `AssetResolver__ApiKey`; en Web App plugin si existe `ApiKey` KV ref | Desalineado y con clave `_dev` candidata a limpiar/documentar |
| AssetResolver URL | En local hay `AssetResolver:BaseUrl` y `AssetResolver:BaseUrl_dev` | En Functions prod no existe `AssetResolver__BaseUrl` | Desalineado |
| Admin FunctionKey | Dev `appsettings.Development.json` la deja vacia; `appsettings.json` la trae literal | Azure tiene `FunctionsAdminApi__FunctionKey` literal | Activo pero no alineado con Key Vault |
| Modelos IA | Local seeds contienen endpoints/API keys literales | Azure App Settings y Key Vault tambien tienen claves por proveedor | Duplicidad; falta decidir fuente unica: BD/Key Vault/App Settings |
| Migraciones | Local puede usar `RunDatabaseMigrationsOnStartup`; scripts/docs lo tratan como control clave | Prod observado `false`, pipeline intenta `true` | Desalineado |
| `.env` | No se observo `.env` como fuente real de la app .NET | No aplica | `.env.example` es legacy para este runtime |

### Configuraciones Existentes Sin Uso Claro O Potencialmente Legacy

| Configuracion | Donde aparece | Motivo de duda |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT` | App Settings prod y `.env.example` | Existe junto a `Classification__GptFallback__Endpoint` / `Extraction__GptFallback__Endpoint`; no se confirmo consumo directo actual |
| `DOCUMENT_INTELLIGENCE_ENDPOINT` | App Settings prod | Existe junto a `Classification__AzureDocumentIntelligence__Endpoint`; candidato legacy |
| `AzureStorageConnectionString` | Codigo `BlobStorageService`, Key Vault y App Settings | Si se usa para documentos, es activo; si no hay flujo blob activo, queda pendiente validar por uso funcional |
| `SQL_CONNECTION` | AssetResolver Web App | Existe ademas de `ConnectionStrings__AssetResolverDb`; no se confirmo consumo por codigo del plugin |
| `AssetResolver:ApiKey_dev` / `AssetResolver:BaseUrl_dev` | `local.settings.json` local | No se observo consumo directo en codigo; parecen auxiliares manuales |
| `GDC__RepositoryId`, `GDC__RepositoryName`, `GDC__NominalUser` vacios | App Settings prod/local | Pueden ser opcionales, pero deben documentarse como vacios esperados o eliminarse si no aplican |
| `src/backend/DocumentIA.Functions/bin/Release/net8.0/tipologias/*.json` | Artefacto generado | Contiene nombres antiguos (`cedula`, `nota.simple.validation`) no presentes en config canonica actual |

## Hallazgos Principales

### Criticos

1. La Function App no tiene `AssetResolver__BaseUrl` ni `AssetResolver__ApiKey`, aunque el codigo los consume y el pipeline intenta configurarlos.
   - Riesgo: la orquestacion puede no invocar AssetResolver correctamente desde produccion.

2. Hay API keys reales versionadas en ficheros de configuracion y artefactos.
   - Ubicaciones observadas: `src/backend/DocumentIA.Functions/config/**`, `scripts/seeds/**`, `publish/**`, `bin/**`.
   - Riesgo: exposicion, duplicidad y rotacion dificil.

3. Admin usa `FunctionsAdminApi__FunctionKey` como valor literal sensible, tanto localmente como en Azure App Settings.
   - Riesgo: secreto fuera de Key Vault y rotacion manual.

4. `RunDatabaseMigrationsOnStartup` esta desalineado.
   - Pipeline aplica `true`.
   - Produccion observada tiene `false`.
   - Stage dedicado de migraciones existe pero esta deshabilitado.

5. `httpsOnly=false` y `publicNetworkAccess=Enabled` en Function App, Admin Web App y AssetResolver Web App.
   - Riesgo: superficie publica mayor que la esperada para un entorno con private endpoints.

### Altos

6. La documentacion indica que Azure SQL esta pendiente, pero el recurso existe y la DB `DocumentIA` esta online.

7. La documentacion indica que RBAC para Managed Identity en Cognitive Services esta pendiente, pero la MI de la Function App ya tiene roles sobre DI/OpenAI/AI Services.

8. El endpoint esperado de Content Understanding en checklist no coincide con el endpoint real observado en App Settings.

9. `.github/workflows/infrastructure.yml` apunta a `infrastructure/**` y Bicep inexistente, con sintaxis sospechosa en `creds`.

10. `README.md` y varias guias conservan referencias historicas a Bicep, .NET 8 y recursos `rg-documentia-mvp` que no coinciden con produccion.

### Medios

11. Key Vault no muestra expiracion configurada para secretos.

12. La MI de la Function App tiene roles `User Access Administrator` sobre Key Vault y Storage, aparentemente excesivos para runtime.

13. Existen settings legacy o duplicados (`AZURE_OPENAI_ENDPOINT`, `DOCUMENT_INTELLIGENCE_ENDPOINT`, `AzureStorageConnectionString`) junto a la estructura actual `Classification__*` / `Extraction__*`.

14. `GDC__BypassSslValidation=true` esta activo en produccion.

15. `local.settings.json` no existe versionado, lo cual es correcto para secretos, pero no hay plantilla actualizada equivalente con las claves reales.

## Configuracion Activa Confirmada

### Function App `srbappprodocai`

- Runtime: `dotnet-isolated`, Functions `~4`.
- `Extraction__DefaultProvider=azure-content-understanding`.
- `Classification__DefaultProvider=azure-document-intelligence`.
- Fallback GPT habilitado en clasificacion y extraccion.
- `Extraction__AzureContentUnderstanding__DefaultProcessingLocation=geography`.
- `RunDatabaseMigrationsOnStartup=false`.
- `WEBSITE_VNET_ROUTE_ALL=1`.
- Identidad: SystemAssigned `e700ab11-6478-4aa3-ad3c-b6b7a92279ab`.

### Admin Web App `srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai`

- `FunctionsAdminApi__BaseUrl=https://srbappprodocai.azurewebsites.net/api/`.
- `FunctionsAdminApi__FunctionKey` configurada como literal sensible.
- Identidad SystemAssigned sin roles observados.

### AssetResolver Web App `srbwebpluginassetresolver`

- `ApiKey` como Key Vault reference.
- `ConnectionStrings__AssetResolverDb` como Key Vault reference.
- `SQL_CONNECTION` tambien como Key Vault reference.
- `WEBSITE_VNET_ROUTE_ALL=1`.
- Identidad con `Key Vault Secrets User` sobre `srbkvprodocai`.

### Key Vault `srbkvprodocai`

Secretos observados habilitados:

- `AssetResolverApiKey`
- `AzureStorageConnectionString`
- `AzureWebJobsStorage`
- `Classification--AzureDocumentIntelligence--ApiKey`
- `Classification--GptFallback--ApiKey`
- `docsai-sql-connectionstring-to-ods`
- `Extraction--AzureContentUnderstanding--ApiKey`
- `Extraction--GptFallback--ApiKey`
- `GDC--HttpBasicPassword`
- `GDC--HttpBasicUsername`
- `GDC--Password`
- `GDC--Username`
- `SqlConnectionString`
- `user-ods-dwh`

## Limitaciones Pendientes

No se pudo auditar el contenido real de las tablas dinamicas `ModeloConfigs`, `TipologiaConfigs` y `PluginTipologiaConfigs` porque la autenticacion SQL por Entra requirio MFA en `sqlcmd`.

Queda pendiente una consulta de solo lectura aprobada sobre SQL para validar:

- Modelos activos por tipo, proveedor y clave.
- Tipologias publicadas/archivadas por version.
- Plugins activos por tipologia.
- Presencia de API keys literales dentro de `ConfiguracionJson`.
- Diferencias entre seeds versionados y estado real en BD.

## Plan De Correccion Propuesto

### Fase 0 - Preparacion Y Control

1. Congelar el alcance: no mezclar correcciones de configuracion con cambios funcionales.
2. Crear una matriz de ownership por fuente: repo, App Settings, Key Vault, SQL, pipeline, docs.
3. Ejecutar auditoria SQL de solo lectura con MFA resuelto o con cuenta/flujo aprobado.
4. Clasificar cada clave como activa, legacy, duplicada, obsoleta o pendiente de decision.

### Fase 1 - Secretos Y Referencias

1. Retirar API keys literales de seeds versionados y artefactos revisables.
2. Sustituir valores sensibles por referencias Key Vault o placeholders seguros.
3. Mover `FunctionsAdminApi__FunctionKey` a Key Vault reference o definir alternativa sin Function Key literal.
4. Definir politica de expiracion/rotacion para secretos Key Vault.
5. Eliminar o aislar artefactos generados con secretos (`publish/`, `bin/`, snapshots de seeds).

### Fase 2 - Alineacion Runtime Azure

1. Restaurar o confirmar `AssetResolver__BaseUrl` en `srbappprodocai`.
2. Restaurar o confirmar `AssetResolver__ApiKey` como referencia a `AssetResolverApiKey`.
3. Decidir valor objetivo de `RunDatabaseMigrationsOnStartup` para produccion.
4. Alinear pipeline y App Settings reales con esa decision.
5. Revisar `GDC__BypassSslValidation=true` y documentar decision temporal o remediacion.

### Fase 3 - Seguridad De Plataforma

1. Habilitar `httpsOnly=true` en apps si no hay dependencia que lo impida.
2. Revisar `publicNetworkAccess` frente a private endpoints y necesidades reales de acceso.
3. Reducir RBAC de la MI de Function App si `User Access Administrator` no es necesario en runtime.
4. Revisar Storage `srbstgproapppdocai`: public access, network default action y blob public access.
5. Revisar public access de Cognitive Services frente a private endpoint/DNS.

### Fase 4 - CI/CD Y Automatizacion

1. Decidir si `RunMigrations` sera stage real, tarea manual o responsabilidad del arranque.
2. Eliminar o reparar `.github/workflows/infrastructure.yml`.
3. Documentar pipeline activo unico y su service connection.
4. Validar que `azure-pipelines.yml` aplica todos los settings requeridos por codigo.
5. Añadir comprobacion post-deploy de settings criticos sin imprimir valores.

### Fase 5 - Documentacion

1. Actualizar `README.md` con stack real y despliegue actual.
2. Actualizar `docs/08_CHECKLISTS_DESPLIEGUE.md` para reflejar Azure SQL existente, Admin Web App existente y endpoint CU real.
3. Actualizar `docs/01_ARQUITECTURA_SISTEMA.md` con estado real de MI/RBAC.
4. Actualizar `docs/04_MANUAL_EXPLOTACION.md` con estrategia actual de migraciones y secretos.
5. Crear una plantilla segura de configuracion local sin secretos reales.

### Fase 6 - Validacion Final

1. Ejecutar build y tests relevantes.
2. Ejecutar smoke test Functions.
3. Ejecutar test AssetResolver con Key Vault reference resuelta de forma segura.
4. Validar healthcheck Admin/Desktop cuando corresponda.
5. Confirmar que no quedan secretos literales versionados ni artefactos generados sensibles.

## Evidencias De Solo Lectura Usadas

- Revision de archivos del repo mediante busqueda y lectura.
- Azure Resource Graph / listados de recursos.
- Azure CLI con consultas de metadatos y app settings sanitizados.
- Azure DevOps pipeline/runs en modo lectura.
- Listado de secretos por nombre y metadatos, sin imprimir valores.
- Listado de RBAC por identidad.

## Decision Pendiente Antes De Ejecutar Cambios

Antes de corregir, conviene confirmar:

1. Si el objetivo de produccion es seguir usando API keys o migrar ya a Managed Identity para servicios AI.
2. Si Admin debe seguir autenticando con Function Key o adoptar otro mecanismo.
3. Si las migraciones deben ejecutarse en arranque, pipeline o manualmente.
4. Si los artefactos `publish/` y `scripts/seeds/` deben conservarse historicamente o regenerarse limpios.
5. Si las apps deben cerrar acceso publico ahora o tras una prueba de conectividad privada.