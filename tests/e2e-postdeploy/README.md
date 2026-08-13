# E2E post-despliegue — clasificación y extracción

Batería E2E ejecutable tras cada despliegue de DocumentIA. Valida el pipeline
de ingest (clasificación TDN1/TDN2 con distintos providers y modelos,
extracción, resumen, contrato HTTP) contra DEV o PRO y mide la cobertura
funcional contra `coverage/functional-matrix.json`.

## Prerrequisitos

- PowerShell 7 (`pwsh`).
- `config/environments.json`: copiar de `environments.sample.json` y rellenar
  las function keys (el fichero está en `.gitignore`; nunca commitearlo).
- Opcional (publicación a ADO): `$env:ADO_PAT` con permiso Test Read/Write. Si
  no está disponible, `ado/bootstrap-testplan.ps1` cae a un token AAD de la
  sesión `az` activa (ver sección siguiente); la publicación de resultados
  (`ado/publish-results.ps1`) requiere `ADO_PAT` sí o sí.

## Uso

    # tras cada despliegue (10-15 min)
    pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile smoke

    # bateria completa (45-90 min), bajo demanda
    pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile full -Parallel 2

    # con integracion GDC (off por defecto)
    pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile full -IncludeGdc

Salida: `artifacts/<timestamp>-<env>-<perfil>/` con `report.md` (resumen +
cobertura por área + huecos), `coverage.json` y un JSON por caso.
Exit codes: 0 verde, 1 con FAILs, 2 error de configuración.

Las ingestas quedan marcadas en BD con `submittedBy = "e2e-postdeploy"`:
filtrar ese marcador en análisis de operación y coste.

## Añadir un caso

1. Elegir el JSON de `cases/` (o crear uno nuevo `*-cases.json`).
2. Campos mínimos: `group`, `id`, `caseKey` (único), `name`, `documentPath`
   (relativo al repo), `profiles`, `covers` (ids de la matriz), y el contrato
   de `tests/api-tests/documentia-e2e-common.ps1` (provider, flags, assertions).
3. `serial: true` si el caso depende del orden (p. ej. deduplicación).
4. `requires: ["gdc"]` si necesita la condición GDC.
5. Ejecutar `Invoke-Pester -Path tests/e2e-postdeploy/tests` (valida esquema,
   unicidad y referencias a la matriz).

## Añadir un ítem a la matriz funcional

Editar `coverage/functional-matrix.json` (id estable `AREA-NN`). Si nadie lo
cubre, el reporte lo mostrará como `SIN CASO` — ese es el mecanismo para
hacer visibles los huecos. `conditional: "gdc"` marca ítems que cuentan como
N/A cuando la condición no está activa.

## Ampliar el corpus

`tools/generate_corpus.py` regenera el corpus sintético (reportlab,
python-docx, openpyxl). Ningún documento puede contener datos reales.

## Test Plan espejo en ADO

- `ado/bootstrap-testplan.ps1` crea (idempotente) el Test Plan "E2E
  Post-despliegue DocumentIA" en `sareb.visualstudio.com/AI DocClassExt`, con
  suites `Smoke` y `Full`, un Test Case WI por caso de `cases/*-cases.json` y
  escribe `cases/ado-mapping.json` (se commitea; mapea `caseKey` a
  `testCaseId`/`suiteId`). Reejecutarlo no crea duplicados: busca por nombre
  de plan/suite y por título de Test Case antes de crear.
- Autenticación: usa `$env:ADO_PAT` (Basic) si está definido; si no, obtiene
  un token AAD de la sesión `az` activa (`az account get-access-token
  --resource 499b84ac-1321-427f-aa17-267ca6975798`) y lo usa como `Bearer`.
  Requiere `az login` previo con acceso al proyecto.
- `ado/publish-results.ps1` es el puente que invoca `run-e2e-postdeploy.ps1
  -PublishToAdo`: lee `ado-mapping.json`, enriquece los resultados con
  `TestCaseId`/`SuiteId` y llama a `Publish-AdoTestPlanResults` (definida en
  `tests/api-tests/ado-testplans-common.ps1`). Esa función **solo admite
  `ADO_PAT` (Basic)**, no token AAD/Bearer; sin `ADO_PAT` la publicación se
  omite con un aviso (no falla la batería). Los casos con estado `NA` (p. ej.
  GDC no activo) se normalizan a `SKIP` antes de publicar para que ADO los
  registre como `NotApplicable`.
- Estado actual: bootstrap ejecutado el 2026-08-13 (plan `100091`, suites
  `100093`/`100094`, 26 Test Cases) vía fallback AAD; publicación con un run
  real contra DEV queda pendiente de la validación con backend real
  (AB#100088).

## Limitaciones conocidas

- `PIP-06` (PAGINAS_EXCEDIDAS) no se cubre: exigiría una tipología con
  `maxPaginasDocumento` bajo, y este runner no muta configuración de entorno.
- `EXT-04` (modelo de extracción alternativo) pendiente de identificar un
  segundo modelo válido por request.
- GDC es opt-in (`-IncludeGdc`); en DEV el servidor GDC ha estado
  históricamente no disponible.
- La validación con backend real (Tarea AB#100088) está pendiente; los
  valores de `expectedType` (`nota.simple`, `resumen.documental`) y el
  comportamiento del documento corrupto (FH-FH5) se confirmarán en esa pasada.
