# Runbook de release DEV → PRE → PRO con PRE como puerta técnica

- **Work item**: PBI AB#100676 (Feature AB#100301, Epic AB#100298)
- **Fecha**: 2026-09-24
- **Rama**: `feature/100298-ia-propia-por-entorno`
- **Estado**: diseño aprobado por secciones (S16 y S17), pendiente de plan de implementación
- **Origen**: Step 5 de la Task 18 del plan `docs/superpowers/plans/2026-09-15-ia-propia-por-entorno.md`

## Problema

La práctica real de release está repartida en documentos que se contradicen entre sí y con
los pipelines:

- `docs/procedimientos/RELEASE_MANAGEMENT.md` (691 líneas, inglés, 2026-08-07) describe un
  ciclo genérico: siete etapas de pipeline que no existen, migraciones EF Core aplicadas en el
  despliegue (el stage `RunMigrations` del pipeline principal está deshabilitado con
  `condition: false`), sin PRE, sin las puertas de calidad, sin `tests/e2e-postdeploy`, sin
  golden, sin Cost Management ni hash de configuración.
- `docs/procedimientos/CI_CD_DEPLOYMENT_DETAILS.md` (1163 líneas, inglés) afirma que todos los
  despliegues van a un único grupo de recursos y una única service connection, lista cinco
  pipelines omitiendo `azure-pipelines-migrations.yml`, presenta Functions y Admin como stages
  separados (son dos jobs del mismo stage), propone un rollback por slot swap sin que exista
  ningún deployment slot, y sus consultas KQL leen `customMetrics` cuando el código emite
  `customEvents`.
- `docs/procedimientos/DATABASE_MIGRATION_STRATEGY.md` (333 líneas, inglés) es correcto en las
  secciones de desarrollo (crear y probar migraciones EF Core en local) y desfasado en las de
  despliegue (staging, blue-green, rollback automático por `Down()`, aprobación del environment
  `prod` que no está configurada).
- La práctica real vive en `docs/procedimientos/RUNBOOK_RELEASE_PRO_2026-09.md`, que es la
  instancia de las releases del 03/09 y del 20/09, no un procedimiento reutilizable.

Desde la fase 2a de AB#100298, PRE tiene IA propia y se han definido cuatro puertas de
calidad (smoke, golden, coste, deriva) que hoy solo están documentadas como evidencias en
`docs/auxiliares/temps/`.

## Objetivo

Un único runbook permanente en español, `docs/procedimientos/RELEASE_MANAGEMENT.md`, que
describa el flujo completo DEV → PRE → PRO con PRE como puerta técnica obligatoria, con
comandos y pipelines reales, ejecutor por paso y vuelta atrás por fase; una carpeta
`docs/releases/` con un registro por versión SemVer; y la retirada o recorte de los documentos
que hoy contradicen la práctica.

## Fuera de alcance

- El cutover de IA de PRO (Fase 3, AB#100302 y AB#100321). Es un evento único y va en la
  instancia `docs/releases/vX.Y.Z/runbook.md` de la release que lo ejecute. El runbook
  permanente solo incluye lo repetible: la vuelta atrás por alias mientras el acceso compartido
  siga abierto y el paso genérico "cambios de configuración de IA de la release".
- Configurar checks de aprobación en los environments de Azure DevOps. Se propone como mejora
  separada con su propio work item.
- `azure-pipelines-ai-artifacts.yml` (AB#100675). El runbook lo describe como flujo objetivo y
  mantiene el procedimiento manual vigente hasta que ese PBI esté Done.
- Renombrar `RELEASE_MANAGEMENT.md` o mover `RUNBOOK_RELEASE_PRO_2026-09.md`.
- Corregir `docs/08_CHECKLISTS_DESPLIEGUE.md` (su bloque 5 fija el endpoint de CU al de PRO,
  desfasado desde el cutover de DEV y PRE). Queda anotado como fleco.
- Etiquetar retroactivamente commits ya desplegados.

## Estado verificado el 2026-09-24

- Pipelines en la raíz, todos con `trigger: none` y `pr: none`, todos parametrizados con
  `targetEnvironment` (`dev`, `pre`, `prod`): `azure-pipelines.yml` (Build → RunMigrations
  deshabilitado → DeployFunctions con jobs Functions y Admin → DeployAssetResolver →
  ValidateConfiguration), `azure-pipelines-functions.yml`, `azure-pipelines-admin.yml`,
  `azure-pipelines-assetresolver.yml`, `azure-pipelines-bootstrap.yml` (sin `environment:`) y
  `azure-pipelines-migrations.yml` (Generate en agente hosted → Apply en el pool privado
  `docia-mdp-private` con `scripts/deployment/apply-migrations.ps1`, pre-check por conjunto).
- Environments de Azure DevOps: `dev` (38), `functions` (35), `pre` (39), `prod` (34). Ninguno
  tiene checks configurados; las tres service connections (`AI DocClassExt DEV`, `PRE`, `PRO`)
  tampoco. Leído por REST (`_apis/pipelines/checks/configurations`, count 0 en todos).
- EF Core es real: `src/backend/DocumentIA.Data/Migrations` y el pipeline Migrations-BD genera
  el script idempotente con `dotnet ef migrations script`.
- No existe ningún deployment slot en recursos ni scripts.
- `infra/ai/pipeline-variables.yml` es la única fuente de los endpoints `AI_*` por entorno; DEV
  apunta a su IA propia desde el 2026-09-22 y PRE desde el 2026-09-23; PRO sigue en sus
  recursos actuales.
- Scripts de promoción de IA y configuración, todos en `scripts/ai/`: `export-config-release.ps1`,
  `config-hash.sql`, `apply-deployments.ps1`, `copy-cu-analyzers.ps1`, `copy-di-artifacts.ps1`,
  `copy-labeling-dataset.ps1`, `validate-analyzer.ps1`, `set-resource-aliases.sql`,
  `set-auth-mode-identity.sql`, `clear-model-api-keys.sql`. Aplicación de configuración:
  `scripts/database/replicate-config-data.ps1`.
- Validación de app settings: `scripts/testing/validate-azure-appsettings-contract.ps1`;
  aplicación idempotente: `scripts/configuration/ensure-app-settings.ps1`; RBAC de Key Vault:
  `assign-keyvault-rbac.ps1` y `verify-keyvault-rbac.ps1` con `manageKeyVaultRbac=false`.
- Test Plan activo: 100069 "Smoke pre-release PRO" con casos SMK-1 a SMK-8. E2E post-despliegue:
  `tests/e2e-postdeploy/run-e2e-postdeploy.ps1` con perfiles `smoke` y `full`.
- Tags existentes: `deploy-pro-2026-09-20` (PRO en `fe1533f`), `release-2026-09-15`,
  `deploy-pro-2026-08-14` y anteriores. Ningún tag SemVer.
- `docs/releases/` no existe. `CLAUDE.md` no declara `Releases:`.
- Evidencias de las puertas: `docs/auxiliares/temps/2026-09-22/puertas-dev.md`,
  `docs/auxiliares/temps/2026-09-23/puertas-pre.md` y `cutover-pre-s12.md`.

## Decisiones

Tomadas en S16 (2026-09-24, primera parte del brainstorming), no se reabren:

1. Camino architectural: spec antes del runbook, después plan de implementación.
2. Alcance: flujo completo DEV → PRE → PRO. `CI_CD_DEPLOYMENT_DETAILS.md` se absorbe y se
   retira. `RUNBOOK_RELEASE_PRO_<fecha>.md` queda como instancia histórica.
3. Versionado SemVer con tag anotado `vX.Y.Z` sobre el commit desplegado, documento por release
   con la plantilla de gobernanza y `Releases: docs/releases/` declarado en `CLAUDE.md`. Los
   tags `deploy-pro-*` y `release-*` se conservan como histórico.
4. Promoción de IA a PRE: flujo objetivo por pipeline (Export, AiArtifacts, ConfigSeed) con
   subsección "Procedimiento manual" vigente hasta AB#100675 Done.

Tomadas en S17 (2026-09-24, segunda parte):

5. El cutover de IA de PRO no entra en el runbook permanente; va en la instancia de su release.
6. Puerta humana antes de PRO: Test Plan 100069 ejecutado contra PRE y "go" del responsable del
   proyecto registrado en `release.md`. El runbook documenta que Azure DevOps no tiene approval
   check y lo propone como mejora separada. Ejecutor por paso: proyecto para código, pipelines,
   BD, IA y validación; plataforma para cuota, roles, red y service connections. Sin nombres de
   personas en la plantilla.
7. `DATABASE_MIGRATION_STRATEGY.md` se recorta a desarrollo (secciones 1 a 3, traducidas) y
   delega el despliegue de esquema en el runbook.
8. `docs/releases/` con una carpeta por release: `release.md`, `runbook.md` y `evidencias/`.
9. Estructura del runbook: un solo documento por fases con checklists copiables y anexos
   (enfoque A frente a núcleo más anexos separados o plantilla de instancia separada).
10. Primera versión SemVer: `v1.0.0` en la siguiente release a PRO, con tag anterior
    `deploy-pro-2026-09-20`.

## Diseño

### 1. Ficheros afectados

| Fichero | Tratamiento |
|---|---|
| `docs/procedimientos/RELEASE_MANAGEMENT.md` | Reescrito íntegro en español como runbook único por fases. No se conserva texto del actual; se conservan solo datos verificados. |
| `docs/procedimientos/CI_CD_DEPLOYMENT_DETAILS.md` | Eliminado. Lo vigente pasa a los anexos del runbook. Lo genérico o falso se descarta. |
| `docs/procedimientos/DATABASE_MIGRATION_STRATEGY.md` | Recortado a las secciones 1 a 3 (visión, crear una migración, probarla en local, buenas prácticas), traducido; las secciones 4 a 8 se sustituyen por un enlace a la fase de esquema del runbook. |
| `docs/releases/README.md` | Nuevo: regla SemVer, índice de releases (tabla versión, fecha, commit, work items) y cómo crear una carpeta nueva desde la plantilla. |
| `docs/releases/_plantilla/release.md` | Nuevo: plantilla de gobernanza adaptada (sin Session ID; el bloque "Pendiente de Fase 4" se sustituye por el bloque de aprobación). |
| `docs/releases/_plantilla/runbook.md` | Nuevo: checklists de las fases 0 a 5 con casilla, fecha y resultado por paso. |
| `CLAUDE.md` | Añade `Releases: docs/releases/` y un enlace al runbook en la sección de comandos. |
| `docs/INDEX.md` | Entradas de "Release & Versioning" actualizadas: retira `CI_CD_DEPLOYMENT_DETAILS`, añade `docs/releases/`, corrige la descripción de `DATABASE_MIGRATION_STRATEGY`. |
| `README.md` | Enlace al runbook desde la sección de despliegue. |

Se enlazan sin modificar: `docs/08_CHECKLISTS_DESPLIEGUE.md` (bootstrap de un entorno, RBAC de
Key Vault, `replicate-config-data.ps1`, referencia de scripts), `RUNBOOK_RELEASE_PRO_2026-09.md`,
`tests/e2e-postdeploy/README.md`, `infra/ai/README.md`, `docs/manuales/MANUAL_COSTES_IA.md`.

Contenido de `CI_CD_DEPLOYMENT_DETAILS.md` que se conserva (en anexos): catálogo de pipelines
con parámetros y stages reales; rollback por "Rerun from stage"; validación de contrato de app
settings; secretos vía `DOCIA_SECRET_*` y bootstrap; `EnvironmentName` fijado por los pipelines
y el paso "Ensure Functions environment name" del Admin; el secreto huérfano `GDC--Endpoint` en
el Key Vault de PRO; rama `develop` para lanzar pipelines; KQL corregidas a `customEvents`.
Contenido que se descarta: RG único, cinco pipelines, slot swap, backup y restore manual de
Azure SQL, timeline, escenarios de sprint, contactos con placeholders, "Next Steps".

### 2. Versionado y `docs/releases/`

Regla SemVer. MAJOR cuando cambia un contrato externo (API de ingest, esquema de
`ConfiguracionJson`, formato de los artefactos de IA); MINOR cuando la release lleva PBIs o
features; PATCH cuando solo lleva fixes o configuración. El tag anotado `vX.Y.Z` se crea en la
Fase 5 sobre el commit de `develop` desplegado y verificado en PRO, nunca antes.

```
docs/releases/
  README.md
  _plantilla/
    release.md
    runbook.md
  vX.Y.Z/
    release.md      obligatorio: cabecera (fecha, tag, commit, tag anterior), validación
                    (build, tests), diff por secciones, bloque de aprobación
    runbook.md      obligatorio: instancia ejecutada, casillas con fecha y resultado
    evidencias/     opcional: texto corto (resumen del smoke, comparación golden, .hashes.json,
                    factura filtrada). Sin binarios ni secretos. Lo voluminoso queda en
                    docs/auxiliares/temps y se referencia por ruta
```

Bloque de aprobación en `release.md`: resultado del Test Plan 100069 (SMK-1 a SMK-8, ID del run,
fecha); las cuatro puertas de PRE con PASS y fecha; "go" del responsable del proyecto como línea
con fecha y nombre, escrito a mano en cada instancia.

### 3. Estructura del runbook

Cabecera: propósito; lectores (ejecutor proyecto o plataforma por paso); tres reglas fijas
(esquema antes que código; PRE es puerta obligatoria; nada llega a PRO sin las cuatro puertas y
el smoke pre-release); tabla de entornos con grupo de recursos, Function App, Admin, Key Vault,
SQL, recursos de IA y service connection, tomada de `azure-pipelines.yml` e
`infra/ai/pipeline-variables.yml`.

Cada fase sigue el mismo esquema: objetivo, prerrequisitos, pasos numerados con comando o
pipeline y parámetros, verificación, vuelta atrás, ejecutor. Los pasos son casillas.

| Fase | Contenido | Pipelines y scripts |
|---|---|---|
| 0 Preparación | Commit candidato en `develop`; versión; carpeta `docs/releases/vX.Y.Z/` desde la plantilla; `git log <tag-anterior>..HEAD` clasificado; migraciones y configuración de la release identificadas | `git`, `scripts/ai/export-config-release.ps1` |
| 1 DEV | Build, unit y Admin en verde; E2E smoke y full en DEV; hash de configuración de DEV como referencia | `dotnet test`, `run-e2e-postdeploy.ps1 -Environment dev`, `scripts/ai/config-hash.sql` |
| 2 PRE | 2.1 Esquema. 2.2 Código. 2.3 Configuración del release. 2.4 Artefactos de IA. 2.5 Cuatro puertas | Ver sección 4 |
| 3 Aprobación | Test Plan 100069 contra PRE; revisión del diff y del bloque de aprobación; "go" registrado; nota sobre la ausencia de approval check en Azure DevOps | skill `azure-devops-testplans` |
| 4 PRO | Copia `prerel` verificada; Migrations-BD `prod`; seeds de configuración; `azure-pipelines.yml` `prod`; reinicio si hay cambios de alias; smoke en PRO; hash frente al `.hashes.json`; backfills | Ver sección 5 |
| 5 Cierre | Tag `vX.Y.Z`; `release.md` y `runbook.md` completos; `master` sincronizado; work items a Done; limpiezas diferidas con fecha (copias `prerel`, tablas `__bak`) | `git tag -a`, Azure DevOps |
| Anexo A | Catálogo de pipelines: fichero, parámetros, stages, environment, scripts que invoca | |
| Anexo B | Vuelta atrás por capas (sección 5) | |
| Anexo C | Secretos y app settings: bootstrap, `ensure-app-settings.ps1`, contrato de settings, secreto huérfano `GDC--Endpoint` | |
| Anexo D | Observabilidad durante el despliegue: KQL sobre `customEvents` (`CU.CircuitOpen`, `CU.CircuitClosed`, `CU.CircuitFailover`, `CU.RetryFailover`, `DocumentProcessed`), errores y latencia en App Insights | |

El runbook no repite: bootstrap de un entorno (enlace a `08_CHECKLISTS_DESPLIEGUE.md`),
creación de migraciones (enlace a `DATABASE_MIGRATION_STRATEGY.md`), operación de costes
(enlace a `MANUAL_COSTES_IA.md`).

### 4. Fase PRE en detalle

**2.1 Esquema.** Pipeline Migrations-BD con `targetEnvironment=pre`. El pre-check compara el
conjunto de migraciones aplicadas con el del repo (no solo la última). El artefacto
`MigrationsScript` es el SQL exacto aplicado.

**2.2 Código.** `azure-pipelines.yml` con `targetEnvironment=pre` desde el commit candidato. El
stage `ValidateConfiguration` debe terminar en verde. Los pipelines por componente solo para
redespliegues incrementales.

**2.3 Configuración del release.** Origen siempre DEV. `export-config-release.ps1` genera
`config-<RELEASE_TAG>.sql` y `config-<RELEASE_TAG>.hashes.json` (artefacto `db-config`).
Aplicación con `replicate-config-data.ps1 -Mode Apply` con token de Entra. Nunca se aplica un
export de DEV a PRE por Id; solo por clave natural. Fleco citado: `CatalogoTdn1.Descripcion`
difiere por CRLF entre DEV y PRE y bloquea la comparación por hash de esa tabla hasta que se
normalice (alinear PRE a DEV).

**2.4 Artefactos de IA.** Subsección "Flujo objetivo": pipeline `azure-pipelines-ai-artifacts.yml`
con etapas Export → AiArtifacts (`apply-deployments.ps1` desde `infra/ai/deployments.pre.json`,
`copy-cu-analyzers.ps1`, `copy-di-artifacts.ps1`, `copy-labeling-dataset.ps1`,
`validate-analyzer.ps1`) → ConfigSeed; `prod` excluido de AiArtifacts. Marcada "pendiente de
AB#100675" con su criterio de aceptación (primera ejecución idempotente sobre PRE). Subsección
"Procedimiento manual vigente": la secuencia validada en S12, en orden, con script, efecto
esperado y comprobación de idempotencia (segunda pasada, 0 filas):

1. Bloque del entorno en `infra/ai/pipeline-variables.yml` apuntando a sus recursos.
2. `azure-pipelines.yml` `pre` (crea los `AI__Resources__*__Endpoint`).
3. Keys de los recursos de PRE en `srbkvpredocai` (cuatro secretos).
4. Endpoints directos forzados en app settings; cero hosts de PRO.
5. `set-resource-aliases.sql` (copia `ModeloConfigs__bak_<fecha>`).
6. `set-auth-mode-identity.sql` (copia `ModeloConfigs__bak_<fecha>`).
7. Reinicio de la Function App.
8. Smoke E2E y comprobación en App Insights (cero hosts de PRO, cero 401/403).
9. `clear-model-api-keys.sql` (copia `ModeloConfigs__bak_<fecha>`).
10. Export post-cutover y purga de keys en las copias `__bak`.

**2.5 Cuatro puertas.** Condición para el "go" de la Fase 3: la Fase 3 puede empezar (Test Plan,
revisión del diff) con la puerta 3 pendiente, pero el "go" no se firma sin las cuatro en PASS.
Las puertas 1, 2 y 4 se pasan en la misma sesión; la 3 se cierra al día siguiente por la
latencia de ingesta de Cost Management.

| Puerta | Comando | Criterio de PASS | Evidencia |
|---|---|---|---|
| 1 Smoke E2E | `run-e2e-postdeploy.ps1 -Environment pre -Profile smoke` | 6/6 PASS; en App Insights de PRE cero llamadas a hosts de IA de PRO y cero 401/403 en la ventana | Resumen del run y consulta KQL |
| 2 Golden | Consola `DocumentIA.Batch.Evaluation run --set golden --env PRE --parallel 2` y comparación con la línea base de DEV del mismo modelo | p ≥ 0,05 en TDN1 y TDN2; si el modelo cambia, la línea base se regenera en DEV antes | Informe de comparación |
| 3 Coste | Cost Management por grupo de recursos con agrupación ResourceId + Meter (token de az CLI para DEV y PRE; `az rest` para PRO) y métricas de Azure Monitor de los recursos de IA | Meters de IA facturados en el grupo de recursos de PRE al día siguiente; PRO sin rampa de tokens ni páginas atribuible a la release | Salida filtrada del cuadre |
| 4 Deriva | `config-hash.sql` contra PRE frente al `.hashes.json` del export de la release | Hashes idénticos en todas las tablas de configuración; toda diferencia se explica por escrito o se corrige antes de PRO | `.hashes.json` y salida de la consulta |

### 5. Aprobación, PRO y vuelta atrás

**Fase 3 Aprobación.** Tres condiciones en `release.md`: las cuatro puertas en PASS con fecha;
Test Plan 100069 ejecutado contra PRE con SMK-1 a SMK-8 en Passed y el ID del run; diff
`git log <tag-anterior>..HEAD` clasificado por PBIs, features, fixes, infraestructura y
configuración. El "go" es una línea con fecha y nombre. El runbook deja constancia de que los
environments `prod` y `pre` de Azure DevOps no tienen approval check y de que el pipeline puede
desplegar a PRO sin aprobación; propone un check en el environment `prod` como mejora separada
con su propio work item. Ejecutor: proyecto.

**Fase 4 PRO.** Orden fijo:

1. Copia `DocumentIA-prerel-<fecha>` y verificación de conteos y última migración aplicada.
2. Migrations-BD `targetEnvironment=prod`; comprobar en `MigrationsScript` que el conjunto
   aplicado coincide con el del repo.
3. Seeds de configuración de la release con el mismo `config-<RELEASE_TAG>.sql` aplicado en PRE.
4. `azure-pipelines.yml` `prod` desde el commit candidato; `ValidateConfiguration` en verde.
5. Reinicio de `srbappprodocai` solo si la release toca alias o app settings de IA.
6. Smoke E2E en PRO (`-Environment pro -Profile smoke`).
7. `config-hash.sql` en PRO frente al `.hashes.json`; diferencias documentadas como cambio en
   caliente a retroportar.
8. Backfills reanudables si la release los trae, siempre después del smoke.

Ejecutor: proyecto; plataforma solo si hay cuota, roles o red.

**Anexo B, vuelta atrás por capas, de menos a más invasiva:**

1. Código: "Rerun from stage" del último run correcto del pipeline principal en `prod`. No
   existen slots; el runbook lo dice explícitamente.
2. Configuración: restaurar `ConfiguracionJson` desde la copia `ModeloConfigs__bak_<fecha>` que
   crea cada script SQL; relanzar `ensure-app-settings.ps1` con las variables del run anterior.
3. Alias de IA: mientras el acceso compartido a los recursos de PRO siga abierto, devolver el
   bloque del entorno en `pipeline-variables.yml` a los hosts de PRO, redesplegar, restaurar las
   versiones anteriores de los cuatro secretos en el Key Vault del entorno y reiniciar
   (secuencia inversa de S12).
4. Esquema: restaurar la copia `prerel` a una BD nueva y swap de nombres. Solo si una migración
   corrompió datos; el código anterior tolera columnas nuevas, así que nunca es el primer paso.

## Verificación de la unidad

Tests de código: N/A, la unidad no toca código. Comprobaciones que ejecutará el plan:

1. Cada ruta de script, pipeline o documento citada en el runbook existe en el repo (script de
   comprobación de rutas relativas y enlaces).
2. Parámetros y stages del anexo A coinciden con los YAML por lectura directa.
3. `docs/INDEX.md` y `README.md` sin enlaces a ficheros retirados.
4. Ninguna mención a modelos ni línea de atribución en los ficheros generados.
5. Prueba en seco: rellenar la plantilla `runbook.md` con lo hecho en la release del 20/09
   (`fe1533f`) y comprobar que las checklists cubren cada paso registrado en
   `RUNBOOK_RELEASE_PRO_2026-09.md`, sin crear `docs/releases/v…/`.
6. Revisión del usuario del runbook antes del commit.

Impacto documental: README, runbook, configuración (`CLAUDE.md`). ADR: no aplica (decisión de
proceso tomada en S16, no de arquitectura).

## Riesgos

| Riesgo | Mitigación |
|---|---|
| El runbook describe un pipeline (`ai-artifacts`) que aún no existe y se lee como si existiera | Subsección "Flujo objetivo" marcada "pendiente de AB#100675"; el procedimiento manual es el vigente hasta entonces |
| Duplicar contenido de `08_CHECKLISTS_DESPLIEGUE.md` y volver a tener dos verdades | El runbook enlaza y no copia; solo repite los datos de la tabla de entornos |
| Evidencias en `docs/releases/vX.Y.Z/evidencias/` con secretos o volumen | Regla escrita: solo texto corto, sin binarios ni secretos; lo voluminoso en `docs/auxiliares/temps` |
| La puerta 3 retrasa el cierre de PRE un día | El runbook lo declara como esperado y permite pasar a la Fase 3 con la puerta 3 "pendiente de factura" solo si las otras tres están en PASS, cerrándola antes del "go" |

## Referencias

- Plan de la Epic: `docs/superpowers/plans/2026-09-15-ia-propia-por-entorno.md` (Task 18, Step 5).
- Spec de migraciones: `docs/superpowers/specs/2026-08-07-pipeline-migrations-bd-design.md`.
- Evidencias: `docs/auxiliares/temps/2026-09-22/puertas-dev.md`,
  `docs/auxiliares/temps/2026-09-23/puertas-pre.md`, `docs/auxiliares/temps/2026-09-23/cutover-pre-s12.md`,
  `docs/auxiliares/temps/2026-09-24/factura-puerta3-pre-2026-09-23.md`.
- Traspasos: `docs/auxiliares/temps/2026-09-24/traspaso-ia-propia-por-entorno-s16.md`.
- Plantilla de release de gobernanza: `~/.claude/gobernanza/plantillas/release.md`.
