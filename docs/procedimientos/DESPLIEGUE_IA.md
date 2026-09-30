# Despliegue de IA — guía operativa de la promoción de artefactos por entornos

Guía de extremo a extremo para desplegar (promocionar) los artefactos de IA de
DocumentIA entre entornos: qué se promociona, en qué sentido, con qué mecanismo,
qué aprobaciones hacen falta, cómo se verifica y cómo se deshace. Ordena la
secuencia y remite a la fuente de cada comando; no los duplica (AB#100803).

| Fuente | Qué manda |
|---|---|
| [`infra/ai/README.md`](../../infra/ai/README.md) | Referencia técnica: ficheros y manifiestos, scripts paso a paso, pipeline en detalle, cutover, estados por entorno |
| [`RELEASE_MANAGEMENT.md`](./RELEASE_MANAGEMENT.md) §2.4, §2.5 y 4.12 | Cuándo se lanza dentro de una release, contingencia manual, puertas y evidencias |
| [ADR-001](../decisiones/ADR-001-promocion-artefactos-ia-dev-pre-pro.md) | La decisión del sentido DEV → PRE → PRO y sus consecuencias de permisos |
| [`RUNBOOK_RELEASE_PRO_2026-09.md`](./RUNBOOK_RELEASE_PRO_2026-09.md) | Runbook de una release concreta; sus reglas fijas (p. ej. la configuración no se promociona por `Id`) |

## 1. Qué se promociona

Los artefactos de IA que no se despliegan desde una definición versionada:

- **Datasets de etiquetado** de los analyzers/clasificadores (`copy-labeling-dataset.ps1`).
- **Deployments de modelo OpenAI** (`deployments.<env>.json`, `apply-deployments.ps1`; nunca contra PRO, cuyo fichero es `observed`).
- **Analyzers de Content Understanding**, por Copy API, en las cuentas primaria y secundaria (`cu-analyzers.json`, `copy-cu-analyzers.ps1`).
- **Clasificadores y modelos de Document Intelligence**, por su propia Copy API (`di-artifacts.json` con `promote: true`, `copy-di-artifacts.ps1`).
- **Validación** del destino frente al origen del salto (`validate-analyzer.ps1`, muestra fija de `infra/ai/validation/`).

Solo entra lo referenciado por filas activas de `ModeloConfigs` (regla de
alcance del README). El estado de cada pasada queda en los manifiestos
`infra/ai/*.manifest.json`, que escriben los scripts y **se versionan a mano**
tras cada run (no editar directamente). Detalle de cada fichero: README,
sección "Ficheros".

## 2. Ruta y reglas del salto

Los artefactos se etiquetan y entrenan en DEV y se promocionan por saltos
DEV → PRE → PRO, uno cada vez (ADR-001). La ruta la valida
`scripts/ai/lib/promotion-route.ps1`:

| Destino | Origen por defecto | Otros orígenes |
|---|---|---|
| `pre` | `dev` | `prod`, solo con `fromProd` (recuperación) |
| `prod` | `pre` | ninguno |
| `dev` | ninguno: exige `fromProd` | — |

Cualquier otro salto se rechaza (mismo entorno, `dev → prod`, `pre → dev`).
`fromProd` existe solo para recuperar la carga inicial que se hizo desde PRO en
septiembre de 2026; no es una vía de promoción.

Reglas que no se saltan:

- Una versión nueva de analyzer o clasificador lleva **identificador nuevo**; sustituir uno existente (`-Force`) es decisión manual fuera del pipeline.
- La configuración de BD **no se promociona por `Id`** (regla fija 4 del runbook): el pipeline solo exporta y compara, nunca aplica.
- PRO no cambia de recursos: `apply-deployments.ps1` no corre contra `prod`.

## 3. Mecanismo estándar: pipeline 832 "AI DocClassExt (AiArtifacts)"

`azure-pipelines-ai-artifacts.yml` (AB#100675) ejecuta los pasos 1-5 del flujo
para un salto, más el export y el diff de configuración. Corre entero en el
pool privado `docia-mdp-private`. Detalle completo: README, sección "Pipeline
de promoción (AB#100675)"; encaje en release: RELEASE_MANAGEMENT §2.4.

Parámetros:

| Parámetro | Por defecto | Efecto |
|---|---|---|
| `targetEnvironment` | `pre` | Destino; el origen es el salto anterior (`pre` ← `dev`, `prod` ← `pre`) |
| `fromProd` | `false` | Origen PRO para `dev`/`pre` (recuperación); quita Export y ConfigSeed |
| `preGatesPassed` | `false` | Obligatorio con `prod`: las cuatro puertas de PRE en PASS |
| `releaseTag` | `auto` (= `build-<id>`) | Nombre de los ficheros de config exportados |
| `runValidation` | `true` | Lanza la validación (cuesta dinero: `analyzeBinary` en los dos lados) |
| `dryRun` | `false` | Ensayo sin escrituras; **el primer run de cada salto se lanza así** |

Etapas: **Guard** (rechaza combinaciones inválidas de parámetros) → **Export**
(config de la BD del origen, artefacto `db-config`) → **AiArtifacts** (job de
despliegue sobre el environment del destino: datasets, deployments fuera de
`prod`, analyzers CU en primaria y secundaria, artefactos DI y validación;
publica manifiestos e informe en `ai-artifacts-<env>-<intento>`) →
**ConfigSeed** (export del destino y diff por clave natural, artefacto
`config-diff`; no aplica nada).

El pipeline es idempotente: sobre un entorno ya promocionado todo sale
`present`/`ok`, y un artefacto que existe en destino con otra definición para
el run con `conflict`.

## 4. Aprobaciones y Permits

- **Service connection WIF `AI DocClassExt Promocion IA`**: federado, sin secretos, 15 asignaciones de rol, autorizado solo para este pipeline. La misma identidad opera origen y destino (exigencia de la Copy API de CU). `apply-deployments.ps1` y ConfigSeed usan el SC del entorno correspondiente.
- **Approval checks**: los environments `pre` (check 64) y `prod` (check 65) piden aprobación de un único aprobador, timeout 30 días. Son environments compartidos: la aprobación afecta a todos los pipelines que despliegan ahí.
- **Permits**: la primera vez que el pipeline usa un recurso protegido (SC, environment, pool) ADO detiene el run y pide un Permit que se concede en la UI. Los del salto DEV → PRE se concedieron en el run 79587; el primer run real contra `prod` pedirá además el Permit del SC de PRO y el del environment `prod`, más su aprobación.

## 5. Dentro de una release

1. **DEV → PRE** (Fase 2, RELEASE_MANAGEMENT §2.4): solo si la release cambia deployments, analyzers, clasificadores o el modo de acceso a la IA. Un run con `targetEnvironment=pre` desde el commit candidato cubre 2.4.1-2.4.4; anotar el ID del run en `release.md` y llevar la validación y el diff de ConfigSeed a `evidencias/`.
2. **Puertas de PRE** (§2.5): smoke E2E, golden, coste y deriva de configuración. Las cuatro en PASS son la condición de `preGatesPassed`.
3. **PRE → PRO** (Fase 4, paso 4.12): run con `targetEnvironment=prod` y `preGatesPassed=true` desde el commit candidato, después de 4.3 y antes de 4.4. Misma evidencia: ID del run, validación frente a PRE y diff de ConfigSeed.

Después de cada run real: descargar los manifiestos del artefacto
`ai-artifacts-<env>-<intento>` y **commitearlos en `infra/ai/`** (los
manifiestos `.prod.` no existen hasta la primera pasada real; el diff de
ConfigSeed es una comprobación más, no sustituye a la de configuración de
§2.3.2).

Pendiente conocido para el primer run real PRE → PRO (estado 2026-09-30, README
"Estado de verificación"): crear el contenedor `documentai` en
`srbstgprodocai`, versionar los manifiestos `.prod.` que genere la pasada, y
tener en cuenta que ningún script copia todavía el dataset del clasificador de
DI (`srbstgproapppdocai`).

## 6. Contingencia manual

Solo si el pipeline 832 no está disponible. La secuencia validada en PRE el
2026-09-23 está en RELEASE_MANAGEMENT §2.4 (pasos 2.4.1-2.4.8): deployments,
analyzers CU (primaria y secundaria), clasificadores DI, validación, alias y
modo de acceso, reinicio, purga de copias `__bak` y re-export de referencia.
Cada script acepta `-DryRun` (solo lectura): lanzarlo antes de cada paso. Los
parámetros exactos están en la cabecera de cada `.ps1`
(`Get-Help <script> -Full`) y los comandos comentados en el README (secciones
"Aplicar los deployments", "Copiar los analyzers…", "Copiar los
clasificadores…" y "Validar los analyzers").

## 7. Vuelta atrás y recuperación

- **Vuelta atrás de una release** (artefactos/configuración de IA): Anexo B de RELEASE_MANAGEMENT, capa 3 (alias de IA) y capa 2 (configuración desde las copias `ModeloConfigs__bak_*`). Nunca se empieza por la BD.
- **Si una puerta de PRE falla**: se corrige en `develop` y se repite la Fase 2 entera; no se parchea PRE a mano (§2.5).
- **Recuperar la carga inicial** de DEV o PRE: pipeline con `fromProd=true` (sin Export ni ConfigSeed) o los scripts con `-SourceEnvironment prod -FromProd`; comandos de referencia en el README, bloque "carga inicial y su recuperación".
- Un artefacto en `conflict` no se pisa: la salida es un identificador nuevo o una decisión manual documentada.

## 8. Verificación final

Un salto se da por bueno cuando: el run del pipeline termina `succeeded` sin
`conflict`; la validación da markdown idéntico y acuerdo de campos ≥ 0,85
frente al origen del salto; el diff de ConfigSeed solo contiene diferencias
explicadas; y los manifiestos del run están versionados en `infra/ai/`. La
evidencia va a `docs/releases/vX.Y.Z/evidencias/` y el ID del run a
`release.md`.
