# Runbook de release — DEV → PRE → PRO

Procedimiento permanente para llevar una versión de DocumentIA desde `develop` hasta PRO con
PRE como puerta técnica obligatoria. Sustituye a la versión anterior de este documento (agosto
de 2026) y absorbe `CI_CD_DEPLOYMENT_DETAILS.md`. Cada release crea su instancia en
`docs/releases/vX.Y.Z/` (ver [docs/releases/README.md](../releases/README.md)).

Lectores: quien ejecuta la release (proyecto) y quien la apoya en cuota, roles, red y service
connections (plataforma). Cada paso indica su ejecutor.

## Reglas fijas

1. **El esquema de BD va siempre antes que el código.** El código nuevo asume las columnas
   nuevas; el código anterior ignora las columnas que no conoce.
2. **PRE es puerta obligatoria.** Nada se despliega en PRO sin haber pasado por PRE con la
   misma versión de código, esquema, configuración y artefactos de IA.
3. **Nada llega a PRO sin las cuatro puertas en PASS y el Smoke pre-release (Test Plan 100069).**
   Azure DevOps no tiene hoy ningún check de aprobación en los environments `pre` y `prod`
   (verificado el 2026-09-24): el pipeline puede desplegar a PRO sin aprobación, así que la
   puerta humana es el "go" escrito en `release.md` (Fase 3).

## Entornos

| Dato | DEV | PRE | PRO |
|---|---|---|---|
| Parámetro `targetEnvironment` | `dev` | `pre` | `prod` |
| Service connection | AI DocClassExt DEV | AI DocClassExt PRE | AI DocClassExt PRO |
| Grupo de recursos | SRBRGDEVDOCSAI | SRBRGPREDOCSAI | SRBRGDOCSAIPROD |
| Function App | srbappdevdocai | srbapppredocai | srbappprodocai |
| Admin (App Service) | srbwebadmindevdocai | srbwebadminpredocai | srbwebadminprodocai |
| AssetResolver | srbwebpluginassetresolverdev | srbwebpluginassetresolverpre | srbwebpluginassetresolver |
| Key Vault | srbkvdevdocai | srbkvpredocai | srbkvprodocai |
| SQL Server (BD `DocumentIA`) | srbsqldevdocai | srbsqlpredocai | srbsqlprodocai |
| `ENVIRONMENT_NAME` | Development | Preproduction | Production |
| Azure OpenAI | srbaisrv01devdocai | srbaisrv01predocai | upe48-mm2avmdm-swedencentral |
| Content Understanding primario / secundario | srbaisrv01devdocai / srbaisrv02devdocai | srbaisrv01predocai / srbaisrv02predocai | upe48-mm2avmdm-swedencentral / srbaisrv-westeurope |
| Document Intelligence | srbdidevdocai | srbdipredocai | srbdiprodocai |
| Environment de ADO (ID) | dev (38) | pre (39) | prod (34) |

## Cómo leer las fases

Cada fase tiene objetivo, prerrequisitos, pasos con casilla, verificación, vuelta atrás y
ejecutor. Los pasos numerados `N.M` se copian a `docs/releases/vX.Y.Z/runbook.md` y se marcan
con fecha y resultado. Comandos en Git Bash salvo los `.ps1`, que van con `pwsh`. Toda sesión
manual usa `az login` (sin credenciales en claro).

## Fase 0 — Preparación y versión

Objetivo: fijar qué se despliega y abrir el registro de la release. Ejecutor: proyecto.

Prerrequisitos: `develop` contiene todo lo que va en la release y `origin/develop` está al día.

- [ ] **0.1** Fijar el commit candidato: `git fetch origin && git log --oneline -1 origin/develop`. Anotar el hash.
- [ ] **0.2** Decidir la versión SemVer (regla en `docs/releases/README.md`) y el tag anterior (`git describe --tags --abbrev=0 origin/master` o el último `vX.Y.Z`; para la primera release, `deploy-pro-2026-09-20`).
- [ ] **0.3** Crear `docs/releases/vX.Y.Z/` copiando `docs/releases/_plantilla/` y rellenar la cabecera de `release.md`.
- [ ] **0.4** Listar los cambios: `git log <tag-anterior>..<commit> --oneline` y clasificar por PBIs, features, fixes, infraestructura y configuración en `release.md` (los `AB#` salen de los mensajes).
- [ ] **0.5** Identificar las migraciones nuevas: `ls src/backend/DocumentIA.Data/Migrations | tail` frente a la última aplicada en PRO (`SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC`). Anotar en `release.md`.
- [ ] **0.6** Identificar cambios de configuración (ModeloConfigs, PromptTemplates, Tipologias, CatalogoTdn1, CatalogoTdn2, PluginTipologiaConfigs) y de artefactos de IA (`infra/ai/deployments.<env>.json`, `infra/ai/cu-analyzers.json`, `infra/ai/di-artifacts.json`). Anotar en `release.md`.

Verificación: `release.md` tiene commit, versión, tag anterior y las tres listas (migraciones, configuración, IA), aunque estén vacías.

## Fase 1 — DEV

Objetivo: demostrar que el commit candidato funciona en DEV con su IA propia. Ejecutor: proyecto.

Prerrequisitos: DEV desplegado con el commit candidato (pipeline 799 `targetEnvironment=dev` o los pipelines por componente).

- [ ] **1.1** Build y tests en local sobre el commit candidato:
      `dotnet build src/backend/DocumentIA.sln` (sin warnings nuevos),
      `dotnet test src/backend/DocumentIA.Tests.Unit`, `dotnet test src/backend/DocumentIA.Tests.Admin`,
      `dotnet test src/plugins/DocumentIA.AssetResolver.Tests`, `dotnet format src/backend/DocumentIA.sln --verify-no-changes`.
      Copiar los n/n a `release.md`.
- [ ] **1.2** E2E en DEV: `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment dev -Profile smoke` y después `-Profile full -Parallel 2`. Requiere `tests/e2e-postdeploy/config/environments.json` (gitignored). FAIL esperados y vigentes están en `tests/e2e-postdeploy/README.md`.
- [ ] **1.3** Export de configuración de referencia desde DEV:
      `pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqldevdocai.database.windows.net -ReleaseTag vX.Y.Z`
      Genera `artifacts/db-config/config-vX.Y.Z.sql` y `config-vX.Y.Z.hashes.json` (gitignored). Copiar el `.hashes.json` a `docs/releases/vX.Y.Z/evidencias/`.

Verificación: smoke 6/6, full sin FAIL no esperados, `.hashes.json` guardado.
Vuelta atrás: no aplica (DEV no es destino de la release).

## Fase 2 — PRE, puerta técnica

Objetivo: reproducir en PRE exactamente lo que irá a PRO y pasar las cuatro puertas. Ejecutor:
proyecto; plataforma solo si falta cuota, rol o red.

Prerrequisitos: Fase 1 completa. El SPN de la service connection de PRE está dado de alta en la
BD (`scripts/database/grant-pipeline-sql-user.sql`, one-time por entorno).

### 2.1 Esquema

- [ ] **2.1.1** Pipeline 807 Migrations-BD con `targetEnvironment=pre` desde el commit candidato. El stage Generate publica `migrations.sql`; el stage Apply corre en el pool privado `docia-mdp-private` y su pre-check compara el conjunto de migraciones aplicadas con el del repo (no solo la última).
- [ ] **2.1.2** Comprobar en el run que el artefacto `MigrationsScript` contiene exactamente las migraciones de 0.5 y que `__EFMigrationsHistory` de PRE las registra.

Vuelta atrás: ver Anexo B, capa 4. El código anterior tolera columnas nuevas, así que un esquema adelantado no obliga a retroceder.

### 2.2 Código

- [ ] **2.2.1** Pipeline 799 con `targetEnvironment=pre` desde el commit candidato. Stages esperados: Build → DeployFunctions (jobs Functions y Admin) → DeployAssetResolver → ValidateConfiguration. `RunMigrations` aparece como omitido: está deshabilitado a propósito.
- [ ] **2.2.2** `ValidateConfiguration` en verde (contrato de app settings de `scripts/config/azure-appsettings-contract.json` verificado por `scripts/testing/validate-azure-appsettings-contract.ps1`).
- [ ] **2.2.3** Anotar el ID del run y el hash desplegado en `runbook.md`.

Vuelta atrás: Anexo B, capa 1.

### 2.3 Configuración del release

El origen de la configuración es siempre DEV. Nunca se aplica un export por `Id` sobre un
entorno con datos propios sin revisarlo: el `.sql` usa MERGE por clave primaria con
`IDENTITY_INSERT` y conserva los `Id` del origen.

- [ ] **2.3.1** Revisar `artifacts/db-config/config-vX.Y.Z.sql` (generado en 1.3): solo tablas de configuración, sin keys en claro (`grep -i 'apikey' config-vX.Y.Z.sql` debe devolver vacío).
- [ ] **2.3.2** Aplicar en PRE con token de Entra:
      `pwsh ./scripts/database/replicate-config-data.ps1 -Mode Apply -EntraAuth -TargetConnectionString "Server=tcp:srbsqlpredocai.database.windows.net,1433;Database=DocumentIA;Encrypt=True;" -InputFile artifacts/db-config/config-vX.Y.Z.sql`
      (parámetros verificados en la cabecera del script el 2026-09-24: `-Mode`, `-EntraAuth`, `-TargetConnectionString`, `-InputFile`).
- [ ] **2.3.3** Segunda pasada del mismo comando: debe informar 0 filas cambiadas (idempotencia).

Fleco conocido: `CatalogoTdn1.Descripcion` difiere por CRLF entre DEV y PRE (CERA, COMU, CORR,
CUAD, NOTS). El hash de esa tabla no coincidirá hasta que se alinee PRE a DEV; documentar la
diferencia en la puerta 4 mientras siga abierta.

Vuelta atrás: Anexo B, capa 2.

### 2.4 Artefactos de IA

#### Flujo objetivo (pendiente de AB#100675)

Pipeline `azure-pipelines-ai-artifacts.yml` con `targetEnvironment=pre`: Export (1.3 hecho por
el pipeline, artefacto `db-config`) → AiArtifacts (`scripts/ai/apply-deployments.ps1` desde
`infra/ai/deployments.pre.json`, `scripts/ai/copy-cu-analyzers.ps1`, `scripts/ai/copy-di-artifacts.ps1`,
`scripts/ai/copy-labeling-dataset.ps1`, `scripts/ai/validate-analyzer.ps1`) → ConfigSeed
(`replicate-config-data.ps1 -Mode Apply` desde el pool privado). `prod` queda excluido de
AiArtifacts por condición. Criterio de aceptación del pipeline: su primera ejecución sobre un
PRE ya promocionado a mano debe ser idempotente (todo "ok"). Hasta que AB#100675 esté Done se
usa el procedimiento manual siguiente.

#### Procedimiento manual vigente (secuencia validada en PRE el 2026-09-23)

Solo cuando la release cambia deployments, analyzers, clasificadores o el modo de acceso a la IA.
Cada script SQL crea una copia `ModeloConfigs__bak_<yyyyMMdd_HHmmss>` antes de tocar nada.

- [ ] **2.4.1** Deployments: `pwsh ./scripts/ai/apply-deployments.ps1 -Environment pre` (crea los que faltan; los existentes que difieren se informan como `differs` y no se tocan).
- [ ] **2.4.2** Analyzers CU: `pwsh ./scripts/ai/copy-cu-analyzers.ps1 -Environment pre` (Copy API desde PRO; salta los idénticos).
- [ ] **2.4.3** Clasificadores DI: `pwsh ./scripts/ai/copy-di-artifacts.ps1 -Environment pre`.
- [ ] **2.4.4** Validación: `pwsh ./scripts/ai/validate-analyzer.ps1 -Environment pre` (markdown idéntico y acuerdo de campos ≥ 0,85 frente a PRO).
- [ ] **2.4.5** Alias y modo de acceso, solo si cambian: `scripts/ai/set-resource-aliases.sql` y `scripts/ai/set-auth-mode-identity.sql` contra `srbsqlpredocai` con token de Entra; segunda pasada 0 filas. Después `scripts/ai/clear-model-api-keys.sql` (0 filas con key al terminar).
- [ ] **2.4.6** Reiniciar `srbapppredocai` si 2.4.5 cambió filas: `az functionapp restart -n srbapppredocai -g SRBRGPREDOCSAI`.
- [ ] **2.4.7** Purgar keys de las copias `ModeloConfigs__bak_*` creadas en 2.4.5 (mismo patrón que en DEV el 2026-09-22).

Los parámetros exactos de cada `.ps1` están en su cabecera (`Get-Help <script> -Full`); este
runbook no los duplica.

Vuelta atrás: Anexo B, capa 3.

### 2.5 Las cuatro puertas

Las puertas 1, 2 y 4 se pasan en la misma sesión. La 3 se cierra al día siguiente porque Cost
Management ingiere con un día de retraso. Las cuatro son condición para el "go" de la Fase 3;
la Fase 3 puede empezar con la 3 pendiente. Evidencias en `docs/releases/vX.Y.Z/evidencias/`
(texto corto, sin secretos).

| Puerta | Comando | PASS |
|---|---|---|
| 1 Smoke E2E | `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pre -Profile smoke` | 6/6 PASS y, en App Insights de PRE en la ventana del smoke, cero dependencias a hosts de IA de PRO y cero 401/403 (KQL "hosts de IA" del Anexo D) |
| 2 Golden | Repo DocumentIA.Batch: `dotnet run --project src/DocumentIA.Batch.Evaluation -c Debug --no-build -- run --set golden --env PRE --parallel 2 --label vX.Y.Z-pre --corpus-root <corpus> --config <eval-config-pre.json>` y `-- compare --a eval/runs/<linea-base-DEV> --b eval/runs/<run-PRE>` | p ≥ 0,05 en TDN1 y TDN2 frente a la línea base de DEV con el mismo modelo. Si la release cambia el modelo, regenerar la línea base en DEV antes |
| 3 Coste | Cost Management por grupo de recursos (SRBRGPREDOCSAI y SRBRGDOCSAIPROD) con agrupación ResourceId + Meter para el día del smoke y la golden; para PRO usar `az rest` (con `curl` y token de az CLI devuelve 401). Métricas de Azure Monitor de los recursos de IA como confirmación el mismo día | Meters de IA (`gpt … Tokens`, `S0 Pre-built Pages`) facturados en SRBRGPREDOCSAI; PRO sin rampa de tokens ni páginas atribuible a la release |
| 4 Deriva | `scripts/ai/config-hash.sql` contra `srbsqlpredocai` (solo lectura) frente a `config-vX.Y.Z.hashes.json` de 1.3 | Hash idéntico en ModeloConfigs, PromptTemplates, Tipologias, CatalogoTdn1 y CatalogoTdn2. Toda diferencia se explica por escrito en `runbook.md` o se corrige antes de la Fase 3 |

- [ ] **2.5.1** Puerta 1 (smoke) pasada; resumen del run y salida de la KQL en `evidencias/`.
- [ ] **2.5.2** Puerta 2 (golden) pasada; `compare.md` en `evidencias/`.
- [ ] **2.5.3** Puerta 4 (deriva) pasada; salida de `config-hash.sql` en `evidencias/`.
- [ ] **2.5.4** Puerta 3 (coste) pasada al día siguiente; cuadre en `evidencias/`.

Vuelta atrás: si una puerta falla, se corrige en `develop`, se vuelve a 0.1 con un commit nuevo
y se repite la Fase 2 entera. No se parchea PRE a mano.

## Fase 3 — Aprobación

Objetivo: dejar constancia escrita de que la release puede ir a PRO. Ejecutor: proyecto.

Prerrequisitos: puertas 1, 2 y 4 en PASS (la 3 puede estar pendiente al empezar, no al firmar).

- [ ] **3.1** Smoke pre-release PRO: ejecutar el Test Plan 100069 (casos SMK-1 a SMK-8) contra PRE con el commit candidato y registrar el ID del run de Test Plans y el resultado en `release.md`. El MCP de Azure DevOps no expone Test Plans; usar la REST API (skill `azure-devops-testplans`).
- [ ] **3.2** Revisión del diff de 0.4 por quien aprueba: cada `AB#` listado está en estado To Validate o superior en Azure DevOps.
- [ ] **3.3** Puerta 3 (coste) cerrada (2.5.4).
- [ ] **3.4** "Go" escrito en el bloque de aprobación de `release.md`: fecha, nombre y las cuatro puertas con fecha de PASS.

Nota: los environments `pre` y `prod` de Azure DevOps no tienen approval check (verificado el
2026-09-24 con `GET _apis/pipelines/checks/configurations`). Mejora propuesta, fuera de este
runbook: un check de aprobación en el environment `prod` con su propio work item.

Vuelta atrás: sin "go" no hay Fase 4. La release vuelve a 0.1.

## Fase 4 — PRO

Objetivo: desplegar en PRO lo mismo que se validó en PRE, en el mismo orden. Ejecutor: proyecto;
plataforma solo si hay cuota, roles o red.

Prerrequisitos: "go" de 3.4. Ventana acordada con negocio si la release lleva migraciones que
bloquean tablas grandes.

- [ ] **4.1** Copia de seguridad: `az sql db copy --name DocumentIA --dest-name DocumentIA-prerel-<yyyyMMdd> --server srbsqlprodocai --resource-group SRBRGDOCSAIPROD` y verificación contra el origen: número de filas de `Documentos` y `DocumentoEjecuciones` y última fila de `__EFMigrationsHistory` iguales en las dos BD. Anotar en `runbook.md`.
- [ ] **4.2** Pipeline 807 Migrations-BD con `targetEnvironment=prod` desde el commit candidato. Comprobar en `MigrationsScript` que el conjunto aplicado es el de 0.5.
- [ ] **4.3** Configuración: aplicar en PRO el mismo `config-vX.Y.Z.sql` de 2.3 (`replicate-config-data.ps1 -Mode Apply` contra `srbsqlprodocai.database.windows.net`); segunda pasada 0 filas. Si la release trae seeds propios (catálogos, tarifas), van aquí, con su copia `__bak`.
- [ ] **4.4** Pipeline 799 con `targetEnvironment=prod` desde el commit candidato; `ValidateConfiguration` en verde. Anotar ID del run.
- [ ] **4.5** Reiniciar `srbappprodocai` solo si la release cambió alias o app settings de IA: `az functionapp restart -n srbappprodocai -g SRBRGDOCSAIPROD`.
- [ ] **4.6** Smoke en PRO: `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile smoke` → 6/6.
- [ ] **4.7** Deriva en PRO: `config-hash.sql` contra `srbsqlprodocai` frente a `config-vX.Y.Z.hashes.json`. Toda diferencia se anota en `runbook.md` como cambio en caliente a retroportar a DEV.
- [ ] **4.8** Backfills reanudables, solo si la release los trae y solo después de 4.6 (ejemplos: `scripts/database/backfill-costes-estimados.ps1`, `scripts/database/backfill-markdown-cobertura.ps1`).
- [ ] **4.9** Observación de una hora con las KQL del Anexo D: sin subida de fallos ni de `CU.CircuitOpen`.

Verificación: 4.6 en 6/6, 4.7 sin diferencias no explicadas, 4.9 sin anomalías.
Vuelta atrás: Anexo B, en orden de capas.

## Fase 5 — Cierre y registro

Objetivo: dejar la release trazable. Ejecutor: proyecto.

- [ ] **5.1** Tag anotado sobre el commit desplegado: `git tag -a vX.Y.Z <commit> -m "Release vX.Y.Z a PRO el <fecha>"` y `git push origin vX.Y.Z`.
- [ ] **5.2** `release.md` completo: validación (build, tests), diff, aprobación, runs de pipeline. `runbook.md` con todas las casillas marcadas con fecha y resultado, y las no aplicables tachadas con motivo.
- [ ] **5.3** Sincronizar `master` con el commit desplegado (merge fast-forward desde `develop`; confirmar antes con el usuario del repo) y `git push origin master`.
- [ ] **5.4** Work items de la release a Done en Azure DevOps con un comentario que enlaza `docs/releases/vX.Y.Z/release.md`.
- [ ] **5.5** Fila nueva en la tabla de `docs/releases/README.md`.
- [ ] **5.6** Limpiezas diferidas, con fecha prevista escrita en `runbook.md`: borrar `DocumentIA-prerel-<fecha>` tras el periodo de validación (7 días salvo indicación), borrar las tablas `ModeloConfigs__bak_*` de 2.4.5 y 4.3 una vez purgadas sus keys.
- [ ] **5.7** Commit de `docs/releases/vX.Y.Z/` en `develop`: `docs(release): registro de la release vX.Y.Z (AB#<PBI principal>)`.

Verificación: `git tag -l vX.Y.Z` devuelve el tag; `git log origin/master -1` es el commit desplegado.

> Fases 3 a 5 y anexos: en elaboración (AB#100676).
