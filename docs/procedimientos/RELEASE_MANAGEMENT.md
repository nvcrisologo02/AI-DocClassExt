# Runbook de release — DEV → PRE → PRO

Procedimiento permanente para llevar una versión de DocumentIA desde `develop` hasta PRO con
PRE como puerta técnica obligatoria. Sustituye a la versión anterior de este documento (agosto
de 2026) y absorbe `CI_CD_DEPLOYMENT_DETAILS.md`. Cada release crea su instancia en
`docs/releases/vX.Y.Z/` (ver [docs/releases/README.md](../releases/README.md)).

Lectores: quien ejecuta la release (proyecto) y quien la apoya en cuota, roles, red y service
connections (plataforma). El ejecutor se indica por fase; los pasos que pueden necesitar a
plataforma (alta del SPN en la BD antes de 2.1, cuota o roles en 2.4) lo dicen en el propio paso.

## Reglas fijas

1. **El esquema de BD va siempre antes que el código.** El código nuevo asume las columnas
   nuevas; el código anterior ignora las columnas que no conoce.
2. **PRE es puerta obligatoria.** Nada se despliega en PRO sin haber pasado por PRE con la
   misma versión de código, esquema, configuración y artefactos de IA.
3. **Nada llega a PRO sin las cuatro puertas en PASS y el Smoke pre-release (Test Plan 100069).**
   Azure DevOps no tiene hoy ningún check de aprobación en los environments `pre` y `prod`
   (verificado el 2026-09-24): el pipeline puede desplegar a PRO sin aprobación, así que la
   puerta humana es el "go" escrito en `release.md` (Fase 3).
4. **La configuración nunca se promociona por `Id`.** El export de DEV (`config-vX.Y.Z.sql`) es
   un MERGE por clave primaria con `IDENTITY_INSERT` y los `Id` no coinciden entre entornos: se
   usa como referencia y evidencia, y en PRE y PRO solo se aplican los seeds propios de la
   release (2.3 y 4.3).

Acceso a SQL: `sqlcmd -G` falla por MFA en este entorno. Las consultas de los pasos (0.5, 4.1 y
las comprobaciones de 2.4.5) se lanzan con `pwsh ./scripts/database/query-sql.ps1 -Server <servidor> -Query "<consulta>"`,
que pide a az CLI un token para `https://database.windows.net/` y conecta con `AccessToken`
(mismo mecanismo que `replicate-config-data.ps1 -EntraAuth` y `export-config-release.ps1`).
Los `.sql` con lotes `GO` (`scripts/ai/*.sql`, índices de `scripts/database/`) no caben en ese
script: se ejecutan con el mismo token partiendo por `GO`, como hizo
`docs/auxiliares/temps/2026-09-20/run-sql-token.ps1` (no versionado a fecha 2026-09-24).

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
| Suscripción (`subscriptionId` de `infra/ai/resources.<env>.json`) | 8764f9ff-fe37-4c03-bde9-6294622bef6d | a4f6b357-8f13-4488-9ee8-b9f635426f91 | 647c7246-54bc-4d31-b909-431cacf03272 |
| Environment de ADO (ID) | dev (38) | pre (39) | prod (34) |

## Cómo leer las fases

Cada fase tiene objetivo, prerrequisitos, pasos con casilla, verificación, vuelta atrás y
ejecutor. Los pasos numerados `N.M` se copian a `docs/releases/vX.Y.Z/runbook.md` y se marcan
con fecha y resultado. Comandos en Git Bash salvo los `.ps1`, que van con `pwsh`. Toda sesión
manual usa `az login` (sin credenciales en claro).

"Desde el commit candidato" (el de 0.1): los pasos locales se ejecutan tras
`git checkout <commit>`; los pipelines se lanzan sobre `develop` y en el resumen del run se
comprueba que el commit es el candidato. Si `develop` ha avanzado, no se sigue hasta aclararlo.

## Fase 0 — Preparación y versión

Objetivo: fijar qué se despliega y abrir el registro de la release. Ejecutor: proyecto.

Prerrequisitos: `develop` contiene todo lo que va en la release y `origin/develop` está al día.

- [ ] **0.1** Fijar el commit candidato: `git fetch origin && git log --oneline -1 origin/develop`. Anotar el hash.
- [ ] **0.2** Decidir la versión SemVer (regla en `docs/releases/README.md`) y el tag anterior (`git describe --tags --abbrev=0 origin/master` o el último `vX.Y.Z`; para la primera release, `deploy-pro-2026-09-20`).
- [ ] **0.3** Crear `docs/releases/vX.Y.Z/` copiando `docs/releases/_plantilla/` y rellenar la cabecera de `release.md`.
- [ ] **0.4** Listar los cambios: `git log <tag-anterior>..<commit> --oneline` y clasificar por PBIs, features, fixes, infraestructura y configuración en `release.md` (los `AB#` salen de los mensajes).
- [ ] **0.5** Identificar las migraciones nuevas frente a la última aplicada en PRO (consulta abajo) y los scripts de la release fuera de EF (índices `ONLINE`, procedimientos, seeds de configuración en `scripts/`). Anotar en `release.md`.

      ls src/backend/DocumentIA.Data/Migrations | tail
      pwsh ./scripts/database/query-sql.ps1 -Server srbsqlprodocai.database.windows.net -Query "SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC"

- [ ] **0.6** Identificar cambios de configuración (ModeloConfigs, PromptTemplates, Tipologias, CatalogoTdn1, CatalogoTdn2, PluginTipologiaConfigs) y de artefactos de IA (`infra/ai/deployments.<env>.json`, `infra/ai/cu-analyzers.json`, `infra/ai/di-artifacts.json`). Anotar en `release.md`.

Verificación: `release.md` tiene commit, versión, tag anterior y las tres listas de la sección
"Contenido de la release" (migraciones y scripts fuera de EF, configuración, IA), aunque estén
vacías.
Vuelta atrás: no aplica; la fase no toca ningún entorno. Si cambia el commit candidato, se
repite desde 0.1 y se reescribe la cabecera de `release.md`.

## Fase 1 — DEV

Objetivo: demostrar que el commit candidato funciona en DEV con su IA propia. Ejecutor: proyecto.

Prerrequisitos: esquema de DEV al día (pipeline 807 Migrations-BD con `targetEnvironment=dev`) y
DEV desplegado con el commit candidato (pipeline 799 `targetEnvironment=dev` o los pipelines por
componente).

- [ ] **1.1** Build, tests y formato en local sobre el commit candidato con los comandos del bloque siguiente; copiar los n/n a `release.md`.

      dotnet build src/backend/DocumentIA.sln
      dotnet test src/backend/DocumentIA.Tests.Unit
      dotnet test src/backend/DocumentIA.Tests.Admin
      dotnet test src/plugins/DocumentIA.AssetResolver.Tests
      dotnet format src/backend/DocumentIA.sln --verify-no-changes

  El build no debe añadir warnings.

- [ ] **1.2** E2E en DEV: `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment dev -Profile smoke` y después `-Profile full -Parallel 2`. Requiere `tests/e2e-postdeploy/config/environments.json` (gitignored). FAIL esperados y vigentes están en `tests/e2e-postdeploy/README.md`.
- [ ] **1.3** Export de configuración de referencia desde DEV con `export-config-release.ps1` (comando abajo); copiar el `.hashes.json` a `docs/releases/vX.Y.Z/evidencias/`.

      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqldevdocai.database.windows.net -ReleaseTag vX.Y.Z

  Genera `artifacts/db-config/config-vX.Y.Z.sql` y `config-vX.Y.Z.hashes.json` (gitignored).
  Es la referencia de DEV para los diffs por clave natural de 2.3 y 4.3; no se aplica en
  ningún entorno.

Verificación: smoke 6/6, full sin FAIL no esperados, `.hashes.json` guardado.
Vuelta atrás: no aplica (DEV no es destino de la release).

## Fase 2 — PRE, puerta técnica

Objetivo: reproducir en PRE exactamente lo que irá a PRO y pasar las cuatro puertas. Ejecutor:
proyecto; plataforma solo si falta cuota, rol o red.

Prerrequisitos: Fase 1 completa. El SPN de la service connection de PRE está dado de alta en la
BD (`scripts/database/grant-pipeline-sql-user.sql`, one-time por entorno; lo ejecuta un
administrador de Entra del servidor SQL, plataforma si el proyecto no lo es).

### 2.1 Esquema

- [ ] **2.1.1** Pipeline 807 Migrations-BD con `targetEnvironment=pre` desde el commit candidato. El stage Generate publica `migrations.sql`; el stage Apply corre en el pool privado `docia-mdp-private` y su pre-check compara el conjunto de migraciones aplicadas con el del repo (no solo la última).
- [ ] **2.1.2** En el log del stage Apply, `Pendientes: N` lista exactamente las migraciones de 0.5 y el cierre es `Migrations aplicadas y verificadas correctamente` (`MigrationsScript` es idempotente y contiene todas las del repo, no solo las nuevas).
- [ ] **2.1.3** Scripts SQL de la release fuera de EF listados en 0.5, aplicados en PRE con token de Entra y medidos (duración por lote); antes de 2.1.1 si sustituyen a una migración EF (la registran en `__EFMigrationsHistory`).

Ejemplos de 2.1.3: `scripts/database/indice-monitor-costes-pro.sql` (AB#100662, aplicado en PRE
y PRO) e `indice-monitor-reutilizacion-pro.sql` (release del 20/09). Patrón recomendado en
`docs/procedimientos/DATABASE_MIGRATION_STRATEGY.md`.

Vuelta atrás: ver Anexo B, capa 4. El código anterior tolera columnas nuevas, así que un esquema adelantado no obliga a retroceder.

### 2.2 Código

- [ ] **2.2.1** Pipeline 799 con `targetEnvironment=pre` desde el commit candidato. Stages esperados: Build → DeployFunctions (jobs Functions y Admin) → DeployAssetResolver → ValidateConfiguration. `RunMigrations` aparece como omitido: está deshabilitado a propósito.
- [ ] **2.2.2** `ValidateConfiguration` en verde (contrato de app settings de `scripts/config/azure-appsettings-contract.json` verificado por `scripts/testing/validate-azure-appsettings-contract.ps1`).
- [ ] **2.2.3** Anotar el ID del run y el hash desplegado en la tabla "Runs de pipeline" de `release.md`.
- [ ] **2.2.4** Antes de 2.2.1: guardar en `evidencias/` los endpoints de IA y los nombres de los app settings de `srbapppredocai` (comandos abajo); es de donde restaura el Anexo B, capas 2 y 3.

      az functionapp config appsettings list --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91 --resource-group SRBRGPREDOCSAI --name srbapppredocai --query "[?ends_with(name, '__Endpoint') && name != 'GDC__Endpoint'].[name, value]" -o table > docs/releases/vX.Y.Z/evidencias/appsettings-ia-pre-antes.txt
      az functionapp config appsettings list --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91 --resource-group SRBRGPREDOCSAI --name srbapppredocai --query "[].name" -o tsv > docs/releases/vX.Y.Z/evidencias/appsettings-nombres-pre-antes.txt

  Solo endpoints y nombres: el listado completo lleva cadenas de conexión y no va a
  `evidencias/`. La consulta de endpoints es la del paso 2b de `infra/ai/README.md`.

Vuelta atrás: Anexo B, capa 1.

### 2.3 Configuración del release

El origen de la configuración es siempre DEV, pero el export de 1.3 no se aplica en PRE (regla
fija 4): `replicate-config-data.ps1 -Mode Apply` con `config-vX.Y.Z.sql` reescribiría por `Id`
filas que en PRE son otras (en `Tipologias` los `Id` 2224-2231 apuntan a códigos distintos en
DEV y PRE; decisión del cutover de PRE del 2026-09-23). En PRE se aplican solo los seeds propios
de la release listados en 0.5 (scripts SQL o PowerShell versionados, cada uno con su copia
`__bak`), después de comprobar por clave natural qué difiere de DEV.

- [ ] **2.3.1** Revisar `artifacts/db-config/config-vX.Y.Z.sql` (generado en 1.3): solo tablas de configuración, sin keys en claro (`grep -i 'apikey' config-vX.Y.Z.sql` debe devolver vacío). Es referencia; no se aplica.
- [ ] **2.3.2** Export de PRE antes de tocar nada y diff por clave natural frente al de DEV (comandos abajo); toda diferencia que no resuelvan los seeds de la release se explica en `runbook.md` antes de seguir.

      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqlpredocai.database.windows.net -ReleaseTag vX.Y.Z-pre-antes
      python scripts/database/diff-config-exports.py artifacts/db-config/config-vX.Y.Z-pre-antes.sql artifacts/db-config/config-vX.Y.Z.sql ModeloConfigs=Key PromptTemplates=PromptKey+Version Tipologias=Codigo CatalogoTdn1=Codigo CatalogoTdn2=Codigo PluginTipologiaConfigs=TipologiaCodigo

  El diff compara cada tabla por su índice único (no por `Id`; `PromptTemplates` por la clave
  compuesta `PromptKey+Version`) e ignora `Id` y auditoría. Termina con error si una columna
  de clave no existe o se repite en un fichero; saca
  por tabla las filas solo en PRE, solo en DEV y distintas, con las columnas que cambian.

- [ ] **2.3.3** Aplicar en PRE los seeds propios de la release listados en 0.5, con token de Entra (ver "Acceso a SQL") y su copia `__bak`; nunca el export completo. Sin seeds en la release, tachar con motivo.
- [ ] **2.3.4** Export de PRE tras 2.3.3 (`-ReleaseTag vX.Y.Z-pre`) y el mismo diff de 2.3.2: solo quedan diferencias explicadas. Su `.hashes.json` va a `evidencias/`: es la referencia de la puerta 4.

      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqlpredocai.database.windows.net -ReleaseTag vX.Y.Z-pre

  La idempotencia de los seeds se comprueba con este hash: relanzar un seed no lo cambia.

Fleco conocido: `CatalogoTdn1.Descripcion` difiere por CRLF entre DEV y PRE (CERA, COMU, CORR,
CUAD, NOTS). Aparece como "distinta" en el diff de 2.3.2 y 2.3.4; anotarlo mientras siga
abierto (si se normaliza, se alinea PRE a DEV). No afecta a la puerta 4, que compara PRE consigo
mismo.

Vuelta atrás: Anexo B, capa 2.

### 2.4 Artefactos de IA

Los artefactos de IA (datasets de etiquetado, analyzers de CU, clasificadores y modelos de DI)
se crean en DEV y se promocionan por saltos DEV → PRE → PRO, uno cada vez (ADR-001). En esta
fase el salto es DEV → PRE; el de PRE → PRO es el paso 4.12. La copia desde PRO fue la carga
inicial de DEV y PRE y solo se repite para recuperarla (`fromProd` en el pipeline,
`-SourceEnvironment prod -FromProd` en los scripts).

Solo cuando la release cambia deployments, analyzers, clasificadores o el modo de acceso a la IA.
Ejecutor: proyecto; plataforma si falta cuota para un deployment (2.4.1) o un rol sobre las
cuentas de IA.

#### Pipeline de promoción (AB#100675)

`azure-pipelines-ai-artifacts.yml` es el mecanismo de promoción, registrado en Azure DevOps
como el pipeline 832 `AI DocClassExt (AiArtifacts)` con el service connection WIF
`AI DocClassExt Promocion IA` (verificado el 2026-09-30: 15/15 roles y ensayos en seco
DEV → PRE en el run 79587 y PRE → PRO en el 79596, ambos sin 401 ni conflictos). El
procedimiento manual de más abajo queda como contingencia:
lanza los mismos scripts con la misma ruta.

Parámetros: `targetEnvironment` (destino; el origen es el salto anterior: `pre` ← `dev`,
`prod` ← `pre`), `fromProd` (solo con destino `dev` o `pre`), `preGatesPassed` (obligatorio
para `prod`), `releaseTag` (nombre de los ficheros de config; `auto`, el valor por defecto,
= `build-<id>`), `runValidation` (por defecto sí; cuesta dinero) y `dryRun` (por defecto no;
con él todos los scripts de `scripts/ai/` reciben `-DryRun` y el run solo lee: lanzar así el
primer run de cada salto). Corre entero en el pool privado `docia-mdp-private`.

Aprobaciones y Permits: los environments `pre` y `prod` de ADO tienen un check de aprobación
(checks 64 y 65, único aprobador, timeout 30 días) que afecta a **todos** los pipelines que
despliegan a esos environments, no solo a este. La primera vez que el pipeline usa un recurso
protegido (service connection, environment, pool) ADO pide un Permit que se concede en la UI;
los del salto DEV → PRE se concedieron en el run 79587 y el primer run contra `prod` pedirá
además el del SC de PRO y el del environment `prod`.

| Etapa | Qué hace | Identidad |
|---|---|---|
| Guard | Rechaza `dev` sin `fromProd`, `fromProd` con `prod` y `prod` sin `preGatesPassed` | — |
| Export | `export-config-release.ps1` contra la BD del origen (solo lectura) → artefacto `db-config`. No corre con `fromProd` | SC del origen |
| AiArtifacts | Job de despliegue sobre el environment de ADO del destino: `copy-labeling-dataset.ps1` por analyzer versionado, `apply-deployments.ps1` (fuera de `prod`), `copy-cu-analyzers.ps1` en las cuentas primaria y secundaria, `copy-di-artifacts.ps1` y `validate-analyzer.ps1` (destino frente al origen). Publica los manifiestos (`ai-artifacts-<env>-<intento>`) para versionarlos a mano en `infra/ai/` | SC `AI DocClassExt Promocion IA`; `apply-deployments.ps1` con el SC del destino |
| ConfigSeed | Export de la BD del destino y diff por clave natural frente al origen (`diff-config-exports.py`, mismas claves que 2.3.2; en `prod` sin `ModeloConfigs`) → artefacto `config-diff`. **No aplica nada** (regla fija 4). No corre con `fromProd` | SC del destino |

Los scripts son idempotentes y el pipeline no pasa `-Force`: sobre un entorno ya promocionado
todo sale `present` u `ok`, y un analyzer o clasificador que existe en destino con otra
definición para la ejecución con `conflict`. Criterio de aceptación de su primera ejecución:
sobre PRE, ya promocionado a mano, todo idempotente.

Con el pipeline, 2.4.1 a 2.4.4 son un run con `targetEnvironment=pre` desde el commit
candidato: anotar el ID en la tabla "Runs de pipeline" de `release.md`, llevar
`validacion-analyzers-pre.txt` (artefacto `ai-artifacts-pre-*`) y el diff de ConfigSeed a
`evidencias/` y seguir en 2.4.5. El diff de ConfigSeed no sustituye a 2.3.2: es una
comprobación más.

#### Procedimiento manual de contingencia (secuencia validada en PRE el 2026-09-23)

Solo si el pipeline 832 no está disponible. Cada script SQL crea una copia
`ModeloConfigs__bak_<yyyyMMdd_HHmmss>` antes de tocar nada. Los scripts de copia y validación
resuelven solos el origen (`-Environment pre` implica origen DEV) y aceptan `-DryRun`, que solo
lee: lanzarlo antes de cada paso.

- [ ] **2.4.1** Deployments: `pwsh ./scripts/ai/apply-deployments.ps1 -Environment pre` (crea los que faltan; los existentes que difieren se informan como `differs` y no se tocan).
- [ ] **2.4.2** Analyzers CU desde DEV (Copy API; salta los idénticos y para con `conflict` si en PRE hay otra definición). Si la release trae un analyzer o una versión de dataset nuevos, antes `pwsh ./scripts/ai/copy-labeling-dataset.ps1 -AnalyzerId <id> -Environment pre`, que la validación de 2.4.4 necesita.

      pwsh ./scripts/ai/copy-cu-analyzers.ps1 -Environment pre
      pwsh ./scripts/ai/copy-cu-analyzers.ps1 -Environment pre -Only CU_NS_1.5_0,CU_NS_1.6_0_GGAA -Target cu_secondary -SourceTarget cu_secondary

- [ ] **2.4.3** Clasificadores DI desde DEV: `pwsh ./scripts/ai/copy-di-artifacts.ps1 -Environment pre`.
- [ ] **2.4.4** Validación: `pwsh ./scripts/ai/validate-analyzer.ps1 -Environment pre` (markdown idéntico y acuerdo de campos ≥ 0,85 frente a DEV, el origen del salto). Informe en `evidencias/`.
- [ ] **2.4.5** Alias y modo de acceso, solo si cambian: `scripts/ai/set-resource-aliases.sql` y `scripts/ai/set-auth-mode-identity.sql` contra `srbsqlpredocai` con token de Entra; segunda pasada 0 filas. Después `scripts/ai/clear-model-api-keys.sql` (0 filas con key al terminar).
- [ ] **2.4.6** Reiniciar `srbapppredocai` si 2.4.5 cambió filas: `az functionapp restart -n srbapppredocai -g SRBRGPREDOCSAI`.
- [ ] **2.4.7** Purgar keys de las copias `ModeloConfigs__bak_*` creadas en 2.4.5 (mismo patrón que en DEV el 2026-09-22).
- [ ] **2.4.8** Si 2.4.5 cambió filas, repetir el export de 2.3.4 (`-ReleaseTag vX.Y.Z-pre`, sobrescribe): la referencia de la puerta 4 debe incluir el `ModeloConfigs` final.

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
| 2 Golden | Repo DocumentIA.Batch: `dotnet run --project src/DocumentIA.Batch.Evaluation -c Debug -- run --set golden --env PRE --parallel 2 --label vX.Y.Z-pre --corpus-root <corpus> --config <eval-config-pre.json>` y `-- compare --a eval/runs/<linea-base-DEV> --b eval/runs/<run-PRE>` | p ≥ 0,05 en TDN1 y TDN2 frente a la línea base de DEV con el mismo modelo. Si la release cambia el modelo, regenerar la línea base en DEV antes |
| 3 Coste | Cost Management por grupo de recursos (SRBRGPREDOCSAI y SRBRGDOCSAIPROD) con agrupación ResourceId + Meter para el día del smoke y la golden; para PRO usar `az rest` (con `curl` y token de az CLI devuelve 401). Métricas de Azure Monitor de los recursos de IA como confirmación el mismo día | Meters de IA (`gpt … Tokens`, `S0 Pre-built Pages`) facturados en SRBRGPREDOCSAI; PRO sin rampa de tokens ni páginas atribuible a la release |
| 4 Deriva | Export de PRE al terminar las puertas 1 y 2 (`pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqlpredocai.database.windows.net -ReleaseTag vX.Y.Z-pre-puerta4`, solo lectura) frente al `config-vX.Y.Z-pre.hashes.json` de 2.3.4 o 2.4.8. Compara PRE consigo mismo, no con DEV | Hash idéntico en ModeloConfigs, PromptTemplates, Tipologias, CatalogoTdn1 y CatalogoTdn2 (sin deriva desde la referencia). Toda diferencia se explica por escrito en `runbook.md` o se corrige antes de la Fase 3 |

Detalle de la puerta 2: `<eval-config-pre.json>` es una copia local, fuera de git, de
`DocumentIA.Batch:src/DocumentIA.Batch.Evaluation/config.sample.json` (otro repositorio) con un entorno
`PRE` (`BackendUrl` `https://srbapppredocai.azurewebsites.net`, `FunctionKey` de
`tests/e2e-postdeploy/config/environments.json`) y `SelectedEnvironment=PRE`; lleva la function
key, así que no se versiona ni va a `evidencias/`. `dotnet run` compila antes de ejecutar.
Anotar en `release.md` el commit del repo DocumentIA.Batch usado.

- [ ] **2.5.1** Puerta 1 (smoke) pasada; resumen del run y salida de la KQL en `evidencias/`.
- [ ] **2.5.2** Puerta 2 (golden) pasada; `compare.md` en `evidencias/`.
- [ ] **2.5.3** Puerta 4 (deriva) pasada; los dos `.hashes.json` (referencia de 2.3.4 o 2.4.8 y el de la puerta) en `evidencias/`.
- [ ] **2.5.4** Puerta 3 (coste) pasada al día siguiente; cuadre en `evidencias/`.

Verificación: 2.1 a 2.4 marcados o tachados con motivo, y las cuatro puertas en la tabla de
aprobación de `release.md` (la 3 puede quedar pendiente hasta el día siguiente).
Vuelta atrás: si una puerta falla, se corrige en `develop`, se vuelve a 0.1 con un commit nuevo
y se repite la Fase 2 entera. No se parchea PRE a mano.

## Fase 3 — Aprobación

Objetivo: dejar constancia escrita de que la release puede ir a PRO. Ejecutor: proyecto.

Prerrequisitos: puertas 1, 2 y 4 en PASS (la 3 puede estar pendiente al empezar, no al firmar).

- [ ] **3.1** Smoke pre-release PRO: ejecutar el Test Plan 100069 (casos SMK-1 a SMK-8) contra PRE con el commit candidato y registrar el ID del run de Test Plans y el resultado en `release.md`. El MCP de Azure DevOps no expone Test Plans; usar la REST API (skill `azure-devops-testplans`).
- [ ] **3.2** Revisión del diff de 0.4 por quien aprueba: cada `AB#` listado está en Azure DevOps en To Validate o superior si es PBI o Bug, y en Done si es Task.
- [ ] **3.3** Puerta 3 (coste) cerrada (2.5.4).
- [ ] **3.4** "Go" escrito en el bloque de aprobación de `release.md`: fecha, nombre y las cuatro puertas con fecha de PASS.

Nota: los environments `pre` y `prod` de Azure DevOps no tienen approval check (verificado el
2026-09-24 con `GET _apis/pipelines/checks/configurations`). Mejora propuesta, fuera de este
runbook: un check de aprobación en el environment `prod` con su propio work item.

Verificación: el bloque de aprobación de `release.md` tiene el run del Test Plan 100069, las
cuatro puertas con fecha de PASS y el "go" con fecha y nombre.
Vuelta atrás: sin "go" no hay Fase 4. La release vuelve a 0.1.

## Fase 4 — PRO

Objetivo: desplegar en PRO lo mismo que se validó en PRE, en el mismo orden. Ejecutor: proyecto;
plataforma solo si hay cuota, roles o red.

Prerrequisitos: "go" de 3.4. Ventana acordada con negocio si la release lleva migraciones que
bloquean tablas grandes.

`ModeloConfigs` de PRO queda fuera de los diffs y de la comparación de deriva de esta fase hasta
el cutover de IA de PRO (Fase 3 de AB#100298): sus filas siguen con los recursos y el modo de
acceso propios de PRO y no se comparan con DEV. El `.sql` de un export de PRO puede llevar keys
en claro en `ModeloConfigs` (no verificado): no sale de `artifacts/db-config/` (gitignored) y a
`evidencias/` solo va el `.hashes.json`.

- [ ] **4.1** Copia de seguridad: `az sql db copy --name DocumentIA --dest-name DocumentIA-prerel-<yyyyMMdd> --server srbsqlprodocai --resource-group SRBRGDOCSAIPROD` y verificación contra el origen con `query-sql.ps1`: número de filas de `Documentos` y `DocumentoEjecuciones` y última fila de `__EFMigrationsHistory` iguales en las dos BD. Anotar en `runbook.md`.
- [ ] **4.2** Pipeline 807 Migrations-BD con `targetEnvironment=prod` desde el commit candidato. Comprobar en el log del stage Apply que `Pendientes: N` lista las migraciones de 0.5 y que cierra con `Migrations aplicadas y verificadas correctamente`.
- [ ] **4.3** Configuración en PRO, igual que 2.3.2 a 2.3.4: export previo y diff por clave natural sin `ModeloConfigs`, seeds propios de la release con su copia `__bak` (nunca `config-vX.Y.Z.sql`) y export de referencia `vX.Y.Z-pro` (comandos abajo).

      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqlprodocai.database.windows.net -ReleaseTag vX.Y.Z-pro-antes
      python scripts/database/diff-config-exports.py artifacts/db-config/config-vX.Y.Z-pro-antes.sql artifacts/db-config/config-vX.Y.Z.sql PromptTemplates=PromptKey+Version Tipologias=Codigo CatalogoTdn1=Codigo CatalogoTdn2=Codigo PluginTipologiaConfigs=TipologiaCodigo
      # seeds de la release listados en 0.5, con token de Entra
      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqlprodocai.database.windows.net -ReleaseTag vX.Y.Z-pro

- [ ] **4.4** Pipeline 799 con `targetEnvironment=prod` desde el commit candidato; `ValidateConfiguration` en verde. Anotar ID del run. Pipelines por componente solo en el caso que admite el Anexo A.
- [ ] **4.5** Reiniciar `srbappprodocai` solo si la release cambió alias o app settings de IA: `az functionapp restart -n srbappprodocai -g SRBRGDOCSAIPROD`.
- [ ] **4.6** Smoke en PRO: `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile smoke` → 6/6.
- [ ] **4.7** Deriva en PRO: export tras 4.6 (`-ReleaseTag vX.Y.Z-pro-despues`) frente al `config-vX.Y.Z-pro.hashes.json` de 4.3; hash idéntico salvo `ModeloConfigs`, que queda fuera. Toda diferencia se anota en `runbook.md` como cambio en caliente a retroportar a DEV.

      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqlprodocai.database.windows.net -ReleaseTag vX.Y.Z-pro-despues

- [ ] **4.8** Backfills reanudables, solo si la release los trae y solo después de 4.6 (ejemplos: `scripts/database/backfill-costes-estimados.ps1`, `scripts/database/backfill-markdown-cobertura.ps1`).
- [ ] **4.9** Observación de una hora con las KQL del Anexo D: sin subida de fallos ni de `CU.CircuitOpen`.
- [ ] **4.10** Scripts SQL de la release fuera de EF listados en 0.5 (los de 2.1.3), aplicados en PRO con token de Entra; siempre antes de 4.4 (esquema antes que código) y antes de 4.2 si sustituyen a una migración EF. Anotar la duración por lote.
- [ ] **4.11** Antes de 4.4: guardar en `evidencias/` los endpoints de IA y los nombres de los app settings de `srbappprodocai`, como en 2.2.4 (comandos abajo).

      az functionapp config appsettings list --subscription 647c7246-54bc-4d31-b909-431cacf03272 --resource-group SRBRGDOCSAIPROD --name srbappprodocai --query "[?ends_with(name, '__Endpoint') && name != 'GDC__Endpoint'].[name, value]" -o table > docs/releases/vX.Y.Z/evidencias/appsettings-ia-pro-antes.txt
      az functionapp config appsettings list --subscription 647c7246-54bc-4d31-b909-431cacf03272 --resource-group SRBRGDOCSAIPROD --name srbappprodocai --query "[].name" -o tsv > docs/releases/vX.Y.Z/evidencias/appsettings-nombres-pro-antes.txt

- [ ] **4.12** Artefactos de IA en PRO, solo si la release cambia analyzers, clasificadores o datasets (los de 2.4.2 y 2.4.3). Después de 4.3 y antes de 4.4, en el mismo orden que en PRE. Pipeline `azure-pipelines-ai-artifacts.yml` con `targetEnvironment=prod` y `preGatesPassed=true` desde el commit candidato (salto PRE → PRO). Anotar el ID del run y llevar a `evidencias/` la validación frente a PRE y el diff de ConfigSeed.

  Requisitos, cumplidos desde el 2026-09-30: el service connection `AI DocClassExt Promocion IA`
  con sus 15 roles y el approval check del environment `prod` (check 65, ADR-001). El primer
  run contra `prod` pedirá además en la UI los Permits del SC de PRO y del environment `prod`
  (ver 2.4). Sin pipeline no hay promoción a PRO: no se lanzan los scripts de copia contra PRO
  a mano; si el pipeline no está disponible, la release no lleva cambios de IA a PRO y se
  anota en `runbook.md`.

  En `prod` el pipeline no toca los deployments (`apply-deployments.ps1` queda fuera; PRO no
  cambia de recursos) ni `ModeloConfigs`, que sigue fuera del diff hasta el cutover de IA de PRO.
  No sobrescribe un artefacto existente con otra definición: una versión nueva lleva
  identificador nuevo, así que la vuelta atrás es por datos (la fila de `ModeloConfigs` o la
  tipología vuelven al identificador anterior con su copia `__bak`) y el artefacto nuevo se
  queda en PRO sin uso.

Verificación: 4.6 en 6/6, 4.7 sin diferencias no explicadas, 4.9 sin anomalías.
Vuelta atrás: Anexo B, en orden de capas.

## Fase 5 — Cierre y registro

Objetivo: dejar la release trazable. Ejecutor: proyecto.

Prerrequisitos: verificación de la Fase 4 cumplida (4.6 en 6/6, 4.7 sin diferencias no
explicadas, 4.9 sin anomalías) y sin vuelta atrás en curso.

- [ ] **5.1** Tag anotado sobre el commit desplegado: `git tag -a vX.Y.Z <commit> -m "Release vX.Y.Z a PRO el <fecha>"` y `git push origin vX.Y.Z`.
- [ ] **5.2** `release.md` completo: validación (build, tests), diff, aprobación, runs de pipeline. `runbook.md` con todas las casillas marcadas con fecha y resultado, y las no aplicables tachadas con motivo.
- [ ] **5.3** Sincronizar `master` con un merge `--no-ff` del commit desplegado (no hay fast-forward: `master` lleva sus propios merges), confirmado antes con el responsable del repo, y `git push origin master`.

      git checkout master && git pull --ff-only origin master
      git merge --no-ff <commit> -m "merge(release): sincronizacion con develop tras despliegue a PRO (<fecha>)"
      git push origin master

- [ ] **5.4** Work items de la release a Done en Azure DevOps con un comentario que enlaza `docs/releases/vX.Y.Z/release.md`.
- [ ] **5.5** Fila nueva en la tabla de `docs/releases/README.md`.
- [ ] **5.6** Limpiezas diferidas, con fecha prevista escrita en `runbook.md`: borrar `DocumentIA-prerel-<fecha>` tras el periodo de validación (7 días salvo indicación), borrar las tablas `ModeloConfigs__bak_*` de 2.4.5 y 4.3 una vez purgadas sus keys.
- [ ] **5.7** Commit de `docs/releases/vX.Y.Z/` en `develop`: `docs(release): registro de la release vX.Y.Z (AB#<PBI principal>)`.

Verificación: `git tag -l vX.Y.Z` devuelve el tag; `git merge-base --is-ancestor <commit> origin/master`
termina con código 0.
Vuelta atrás: un tag mal puesto que aún no se ha subido se borra con `git tag -d vX.Y.Z` y se
repite 5.1; si ya está en `origin`, no se mueve ni se borra sin confirmación explícita del
responsable del repo. Un merge erróneo en `master` no se deshace con reset ni push forzado sin
esa misma confirmación. Work items, `release.md` y la fila del README se corrigen editándolos.

## Anexo A — Catálogo de pipelines

Todos con `trigger: none` y `pr: none`: se lanzan a mano desde Azure DevOps ("Run pipeline")
eligiendo rama `develop` y el parámetro `targetEnvironment`. Los pipelines por componente (800,
801, 802) sirven para redespliegues incrementales y asumen un entorno ya inicializado con 803 o
con un run previo de 799. Fuente única de los endpoints de IA por entorno:
`infra/ai/pipeline-variables.yml`, consumido por 799 y 802.

En PRE y PRO el pipeline por defecto es 799. Los pipelines por componente se admiten solo cuando
la release no toca los demás componentes ni la configuración de bootstrap (803); se anota en
`runbook.md` cuáles se usaron. Precedente: el 20/09 se desplegó PRO con 802 y 800 en paralelo.

| ID ADO | Nombre en ADO | Fichero | Parámetros | Stages | Scripts que invoca |
|---|---|---|---|---|---|
| 799 | AI DocClassExt | `azure-pipelines.yml` | `targetEnvironment` (dev/pre/prod, default prod) | Build → RunMigrations (deshabilitado, `condition: false`) → DeployFunctions (jobs Functions y Admin) → DeployAssetResolver → ValidateConfiguration | `scripts/configuration/ensure-app-settings.ps1`, `scripts/configuration/assign-keyvault-rbac.ps1`, `scripts/configuration/verify-keyvault-rbac.ps1`, `scripts/testing/validate-azure-appsettings-contract.ps1` |
| 802 | AI DocClassExt (Functions) | `azure-pipelines-functions.yml` | `targetEnvironment` (default dev) | Build → Deploy | `ensure-app-settings.ps1` |
| 800 | AI DocClassExt (Admin) | `azure-pipelines-admin.yml` | `targetEnvironment` (default dev) | BuildAdmin → DeployAdmin (zipDeploy, RBAC KV, settings, "Ensure Functions environment name", contrato) | `ensure-app-settings.ps1`, `validate-azure-appsettings-contract.ps1` |
| 801 | AI DocClassExt (AssetResolver) | `azure-pipelines-assetresolver.yml` | `targetEnvironment` (default dev) | Build → Deploy | ninguno |
| 803 | AI DocClassExt azure-pipelines-bootstrap. | `azure-pipelines-bootstrap.yml` | `targetEnvironment` (default dev) | Bootstrap | `check-azure-permissions.ps1`, `set-keyvault-secrets.ps1`, `verify-prod-prereqs.ps1`, `set-functionapp-keyvault-references.ps1`, `ensure-app-settings.ps1`, `validate-azure-appsettings-contract.ps1` |
| 807 | Migrations-BD | `azure-pipelines-migrations.yml` | `targetEnvironment` (default dev), `addTransientFirewallRule` (bool, default false) | Generate (agente hosted, `dotnet ef migrations script --idempotent`) → Apply (pool `docia-mdp-private`) | `scripts/deployment/apply-migrations.ps1` |
| 828 | AI DocClassExt (828) | no identificado en el repo | — | — | definición sin fichero identificado; comprobar en ADO antes de usarla |
| 832 | AI DocClassExt (AiArtifacts) | `azure-pipelines-ai-artifacts.yml` | `targetEnvironment` (dev/pre/prod, default pre), `fromProd`, `preGatesPassed`, `releaseTag` (default `auto` = `build-<id>`), `runValidation`, `dryRun` | Guard → Export (origen del salto; no con `fromProd`) → AiArtifacts (pool `docia-mdp-private`, environment del destino) → ConfigSeed (solo diff; no con `fromProd`). Ver 2.4 | `scripts/ai/export-config-release.ps1`, `copy-labeling-dataset.ps1`, `apply-deployments.ps1` (fuera de `prod`), `copy-cu-analyzers.ps1`, `copy-di-artifacts.ps1`, `validate-analyzer.ps1`, `scripts/database/diff-config-exports.py` |

## Anexo B — Vuelta atrás por capas

De menos a más invasiva. Se retrocede solo la capa que falló; nunca se empieza por la BD.

1. **Código.** En Azure DevOps, abrir el último run correcto del pipeline 799 en `prod` y usar
   "Rerun" → "Rerun from stage" → `DeployFunctions`. Después `ValidateConfiguration` y smoke
   (4.6). No existen deployment slots en ningún recurso: no hay swap.
2. **Configuración.** BD: restaurar `ConfiguracionJson` desde la copia `ModeloConfigs__bak_<fecha>`
   que creó el script SQL (una `UPDATE ... FROM ModeloConfigs m JOIN ModeloConfigs__bak_<fecha> b ON b.Id = m.Id`
   sobre la columna cambiada, dentro del mismo entorno) y los seeds de otras tablas desde su
   propia copia `__bak`. App settings: relanzar 799 no restaura nada, porque
   `ensure-app-settings.ps1` no sobrescribe claves existentes. Las claves cuyo valor cambió se
   devuelven a mano con `az functionapp config appsettings set --subscription <id> --resource-group <rg> --name <app> --settings "CLAVE=VALOR"`
   con los valores guardados en 2.2.4 (PRE) o 4.11 (PRO), y se cotejan con la consulta de
   verificación del paso 2b de `infra/ai/README.md`. Reiniciar la Function App.
3. **Alias de IA.** Mientras el acceso compartido a los recursos de PRO siga abierto, secuencia
   inversa del cutover de PRE del 2026-09-23: devolver el bloque del entorno en
   `infra/ai/pipeline-variables.yml` a los hosts de PRO (un redespliegue con 799 no cambia las
   claves que ya existen); forzar con `az functionapp config appsettings set` las
   `AI__Resources__*__Endpoint` y los cuatro endpoints directos del paso 2b
   (`Extraction__AzureContentUnderstanding__Endpoint`, `Extraction__GptFallback__Endpoint`,
   `Classification__AzureDocumentIntelligence__Endpoint`, `Classification__GptFallback__Endpoint`)
   a los valores guardados en 2.2.4 o 4.11; restaurar `ConfiguracionJson` desde la copia `__bak`
   anterior a `set-resource-aliases.sql`; restaurar las versiones anteriores de los cuatro
   secretos de IA en el Key Vault del entorno (`az keyvault secret set-attributes` sobre la
   versión previa o `az keyvault secret set` con el valor anterior); reiniciar la Function App,
   comprobar con la consulta del paso 2b que las ocho claves llevan los hosts esperados y
   repetir el smoke.
4. **Esquema.** Solo si una migración corrompió datos. Restaurar la copia `DocumentIA-prerel-<fecha>`
   con un nombre nuevo (`az sql db copy`), parar la Function App, renombrar la BD actual a
   `DocumentIA-failed-<fecha>` y la copia a `DocumentIA`, desplegar el commit anterior (capa 1) y
   arrancar. Las ejecuciones entre la copia y el fallo se pierden: anotarlas en `runbook.md`.

## Anexo C — Secretos y app settings

- Los secretos viven en el Key Vault del entorno (nombres con `--` como separador). Los carga
  el pipeline 803 desde el variable group `docia-bootstrap-<env>-secrets` (variables
  `DOCIA_SECRET_*`) con `scripts/configuration/set-keyvault-secrets.ps1`; las referencias
  `@Microsoft.KeyVault(...)` las fija `scripts/configuration/set-functionapp-keyvault-references.ps1`.
- Los app settings los aplica `scripts/configuration/ensure-app-settings.ps1`: crea las claves
  que faltan y nunca sobrescribe las existentes (`[SKIP] <clave> ya existe`), sin opción para
  hacerlo. Los verifica `scripts/testing/validate-azure-appsettings-contract.ps1` contra
  `scripts/config/azure-appsettings-contract.json`. Cambiar el valor de una clave que ya existe
  (por ejemplo los endpoints directos de IA, paso 2b de `infra/ai/README.md`) exige
  `az functionapp config appsettings set`, con la captura previa de 2.2.4 o 4.11, la consulta de
  verificación de ese paso y nota en `runbook.md`.
- El RBAC de Key Vault no lo gestiona el pipeline (`manageKeyVaultRbac=false`): se asigna una
  vez con `scripts/configuration/assign-keyvault-rbac.ps1` y se comprueba con
  `verify-keyvault-rbac.ps1`. Detalle en `docs/08_CHECKLISTS_DESPLIEGUE.md`, bloque 3.
- `ENVIRONMENT_NAME` lo fijan 799 y 800; el Admin depende de que la Function App lo tenga (paso
  "Ensure Functions environment name" de 800).
- `GDC__Endpoint` es un app setting no secreto y distinto por entorno; 799 lo fija desde la
  variable de pipeline `GDC_ENDPOINT` (`azure-pipelines.yml`). En el Key Vault de PRO existe un
  secreto huérfano `GDC--Endpoint` que tiene precedencia sobre el app setting y debe eliminarse
  (aviso en `docs/especificaciones/ESPECIFICACION_CAPA_SERVICIO_GDC_SINTWS.md`, línea 176; su
  eliminación no consta como hecha, no verificado a fecha 2026-09-24).
- Rotación de un secreto: nuevo valor en el Key Vault y reinicio de la Function App. La Function
  App carga el Key Vault al arrancar (`AddAzureKeyVault` sin `ReloadInterval` en
  `src/backend/DocumentIA.Functions/Program.cs`), así que sin reinicio sigue con el valor
  anterior. La rotación de la contraseña del usuario SQL de aplicación (`docaisql`) está
  pendiente desde la release del 20/09 y no está versionada a fecha 2026-09-24.

## Anexo D — Observabilidad durante el despliegue

App Insights del entorno (PRE `srbappipredocai`; PRO `srbappiprodocai`, según `docs/infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md`).
Los eventos de dominio se emiten con `TrackEvent` y están en `customEvents`, no en `customMetrics`.

Hosts de IA usados en una ventana (puerta 1; en PRE no debe aparecer ningún host de PRO).
Sustituir `<inicio>` y `<fin>` por la ventana del smoke en UTC (formato `2026-09-23T07:56:30Z`):

```kusto
dependencies
| where timestamp between (datetime(<inicio>) .. datetime(<fin>))
| where type == "Http"
| extend host = tostring(parse_url(data).Host)
| where host has_any ("openai.azure.com", "services.ai.azure.com", "cognitiveservices.azure.com")
| summarize llamadas = count(), no_ok = countif(resultCode !in ("200", "201", "202")) by host, resultCode
| order by host asc
```

Estado del circuito de Content Understanding y failovers:

```kusto
customEvents
| where timestamp > ago(1h)
| where name in ("CU.CircuitOpen", "CU.CircuitClosed", "CU.CircuitFailover", "CU.CircuitRejected", "CU.RetryFailover", "CU.TransientError", "CU.HardTimeout")
| summarize count() by name, bin(timestamp, 5m)
| order by timestamp desc
```

Documentos procesados:

```kusto
customEvents
| where timestamp > ago(1h) and name == "DocumentProcessed"
| summarize documentos = count() by bin(timestamp, 15m)
```

Tasa de fallo de las funciones HTTP:

```kusto
requests
| where timestamp > ago(1h)
| summarize total = count(), fallos = countif(success == false) by name
| extend tasa_fallo = round(100.0 * fallos / total, 2)
| order by tasa_fallo desc
```

Cuándo mirar: durante 4.4 (errores de arranque), tras 4.6 (hosts y circuito) y en 4.9 (una hora).
