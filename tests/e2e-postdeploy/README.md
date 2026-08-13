# E2E post-despliegue — clasificación y extracción

Batería E2E ejecutable tras cada despliegue de DocumentIA. Valida el pipeline
de ingest (clasificación TDN1/TDN2 con distintos providers y modelos,
extracción, resumen, contrato HTTP) contra DEV o PRO y mide la cobertura
funcional contra `coverage/functional-matrix.json`.

## Prerrequisitos

- PowerShell 7 (`pwsh`).
- `config/environments.json`: copiar de `environments.sample.json` y rellenar
  las function keys (el fichero está en `.gitignore`; nunca commitearlo).
- Opcional (publicación a ADO): credencial con permiso Test Read/Write,
  resuelta con precedencia `-Pat` (parámetro) > `$env:ADO_PAT` > `adoPat` en
  `environments.json` > token AAD de la sesión `az` activa (ver sección
  siguiente). `adoPat` es un campo top-level opcional de
  `environments.json` (no por entorno; mismo fichero gitignored que las
  function keys — nunca commitearlo).

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

El polling del estado de la orquestación (`/runtime/webhooks/durabletask/...`)
requiere la misma function key que el POST inicial; se envía automáticamente
si `environments.json` la tiene informada.

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

Nota sobre el % de cobertura: se calcula sobre los casos ejecutados en el
perfil de la ejecución actual (`-Profile smoke` o `-Profile full`). Un ítem
cubierto solo por casos de otro perfil (p. ej. un caso `full` que cubre un
ítem cuando se ejecuta `smoke`) aparece como `SIN CASO` en ese reporte, no
como cubierto: el runner no mira casos fuera del perfil activo.

## Ampliar el corpus

`tools/generate_corpus.py` regenera el corpus sintético (reportlab,
python-docx, openpyxl). Por defecto ningún documento puede contener datos
reales. La incorporación de documentos reales al corpus (asset versionado en
el repositorio) es una excepción que requiere aprobación explícita y directa
del responsable del proyecto para cada fichero, con nombre neutro (sin
identificadores reales del documento ni del sujeto, para evitar además sesgo
de clasificación por nombre de fichero) y trazabilidad de la decisión.

Set aprobado el 2026-08-13 (calidad de extracción, AB#100088): dos recibos
de IBI reales (`corpus/cera/recibo-ibi-real-1.pdf`, `recibo-ibi-real-2.pdf`,
casos FE-FE3/FE-FE4 con `cera.16`) y dos notas simples reales
(`corpus/nota-simple/nota-simple-real-1.pdf`, `nota-simple-real-2.pdf`,
casos FE-FE5/FE-FE6 con `nota.simple_bal`). Nota: FE-FE5 falla de forma
reproducible en DEV (`ExtraerActivity` cancelada con ese documento) — es un
defecto de producto en investigación, no del harness.

## Test Plan espejo en ADO

- `ado/bootstrap-testplan.ps1` crea (idempotente) el Test Plan "E2E
  Post-despliegue DocumentIA" en `sareb.visualstudio.com/AI DocClassExt`, con
  suites `Smoke` y `Full`, un Test Case WI por caso de `cases/*-cases.json` y
  escribe `cases/ado-mapping.json` (se commitea; mapea `caseKey` a
  `testCaseId`/`suiteId`). Reejecutarlo no crea duplicados: busca por nombre
  de plan/suite y por título de Test Case antes de crear.
- Autenticación: precedencia `-Pat` > `$env:ADO_PAT` > `adoPat` en
  `environments.json` > token AAD de la sesión `az` activa (`az account
  get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798`), usado
  como `Bearer`. Requiere `az login` previo con acceso al proyecto si no hay
  ninguna de las otras credenciales.
- `ado/publish-results.ps1` es el puente que invoca `run-e2e-postdeploy.ps1
  -PublishToAdo`: lee `ado-mapping.json`, enriquece los resultados con
  `TestCaseId`/`SuiteId` y llama a `Publish-AdoTestPlanResults` (definida en
  `tests/api-tests/ado-testplans-common.ps1`). Esa función admite PAT
  (Basic) o token AAD (Bearer) — PAT manda si ambos están informados — con la
  misma precedencia de credenciales que `bootstrap-testplan.ps1`; si no hay
  ninguna credencial disponible, la publicación se omite con un aviso (no
  falla la batería). Los casos con estado `NA` (p. ej. GDC no activo) se
  normalizan a `SKIP` antes de publicar para que ADO los registre como
  `NotApplicable`.
- Estado actual: bootstrap ejecutado el 2026-08-13 (plan `100091`, suites
  `100093`/`100094`) vía fallback AAD, con Test Cases para todos los casos de
  `cases/*-cases.json` (incluido `FE-FE2`, incorporado tras la validación
  contra DEV de AB#100088).

## Limitaciones conocidas

- `PIP-06` (PAGINAS_EXCEDIDAS) no se cubre: exigiría una tipología con
  `maxPaginasDocumento` bajo, y este runner no muta configuración de entorno.
- La selección de modelo/provider de extracción por request se descartó por
  decisión de producto (la extracción es CU con fallback GPT);
  `extraction.model` viaja en el payload pero solo es efectivo para el
  provider CU de la tipología.
- GDC es opt-in (`-IncludeGdc`); en DEV el servidor GDC ha estado
  históricamente no disponible.
- `expectedType` usa los identificadores reales publicados en DEV:
  `nota.simple_bal`, `cera.16` y `resumen.documental` (confirmados vía
  `GET /api/tipologias`, AB#100088).
- El documento corrupto (`FH-FH5`) no se rechaza en el HTTP: el endpoint
  admite el payload (202) y la orquestación completa con
  `Resultado.Estado = NO_CLASIFICADO`; el caso valida ese comportamiento real
  en lugar de un `400` síncrono.
- `S-S5` (base64 inválido) responde `500` en vez de `400`: `Convert.FromBase64String`
  en `IngestAPITrigger.cs` no está envuelto en try/catch y la excepción cae al
  handler genérico. Es un defecto del backend (falta validación explícita de
  formato antes de decodificar), no del arnés de pruebas; el caso documenta el
  comportamiento real observado mientras no se corrija.
- `FC-FC4`/`FC-FC5` (modelo de clasificación explícito `gpt-4o-mini` /
  `gpt-5-mini` por request) fallan en DEV: el registro de modelos de
  clasificación (tabla de configuración en BD, `ClassificationModelRegistryLoader`)
  no tiene esas claves activas para el tipo Clasificación; solo resuelve el
  modelo por defecto. No se puede corregir sin mutar configuración de entorno
  (fuera de alcance de este runner); los casos quedan documentados como FAIL
  esperado hasta que se dé de alta esa configuración en DEV.
