# Spec — Test E2E post-despliegue de clasificación y extracción

Fecha: 2026-08-13
Estado: aprobado (pendiente de plan de implementación)

## 1. Objetivo

Disponer de un test E2E ejecutable tras cada despliegue que valide la funcionalidad de
DocumentIA en clasificación y extracción contra el entorno real (DEV y PRO), y que mida
de forma explícita el **grado de cobertura funcional** alcanzado. La integración con GDC
queda configurable y desactivada por defecto.

## 2. Decisiones de alcance (validadas con el usuario)

| Decisión | Valor |
|---|---|
| Entornos y ejecución | DEV y PRO, ejecución manual con un solo runner parametrizado por entorno |
| Perímetro | Pipeline de ingest completo + variantes de configuración + extracción + prueba de distintos providers/modelos en clasificación y extracción |
| Corpus | Sintético, pequeño y versionado en el repo, con resultados esperados conocidos; ingestas marcadas con `submittedBy = "e2e-postdeploy"` |
| Cobertura funcional | Matriz funcional versionada en el repo (fuente de verdad) + publicación opcional a Azure DevOps Test Plans |
| Base técnica | Evolucionar el runner PowerShell existente de `tests/api-tests/` (no crear consola nueva) |
| Mutación de entorno | Prohibida: solo variación por request (payload de `IngestDocument`); no se crean ni modifican tipologías/prompts |
| Perfiles | `smoke` (~10-15 min, cada despliegue) y `full` (45-90 min, releases importantes o bajo demanda) |
| GDC | Configurable con flag explícito; off por defecto; sin el flag, sus ítems cuentan como "no aplicable" |

## 3. Assets existentes que se reutilizan

- `tests/api-tests/documentia-e2e-common.ps1`: construcción del payload de `IngestDocument`
  (provider/model/umbral/nivelClasificacion/markdown/classificationOnly/expectedType/payloadMode),
  polling de orquestación y aserciones declarativas sobre el output.
- `tests/api-tests/documentia-e2e-runner.ps1`: motor de baterías desde casos JSON con
  agregación PASS/FAIL/SKIP y artefactos por caso.
- `tests/api-tests/ado-testplans-common.ps1`: publicación de resultados a Azure DevOps
  Test Plans (org sareb, proyecto AI DocClassExt) vía REST con PAT.
- `tests/api-tests/classification-process-cases.json`: 23 casos (grupos A-H) como semilla
  del perfil `full`.
- `scripts/testing/smoke-test-functions.ps1`: health check post-deploy oficial
  (GET `/api/tipologias`, POST `/api/healthcheck`); su lógica se invoca como paso 0.
- Patrón de configuración del harness Evaluation (repo DocumentIA.Batch):
  `config.sample.json` copiado a config local gitignored con las function keys.

## 4. Estructura

```
tests/e2e-postdeploy/
  run-e2e-postdeploy.ps1          # entrypoint único (pwsh 7)
  config/
    environments.sample.json      # plantilla: baseUrl + functionKey por entorno
    # environments.json           # copia local real, gitignored
  cases/
    smoke-cases.json              # casos del perfil smoke
    full-*.json                   # casos del perfil full, por dominio
  corpus/
    <tipologia>/<escenario>.pdf   # documentos sintéticos versionados (+ docx/xlsx/corrupto)
  coverage/
    functional-matrix.json        # catálogo funcional con ids estables
  README.md
```

Invocación:

```powershell
pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile smoke [-IncludeGdc] [-PublishToAdo]
```

Artefactos de cada ejecución (JSON por caso, `report.md`, `coverage.json`) van a una
carpeta de resultados gitignored bajo `tests/e2e-postdeploy/artifacts/<timestamp>/`.

### Formato de caso (extensión del actual)

Cada caso JSON conserva el contrato actual (`id`, `group`, `name`, `documentPath`,
parámetros de clasificación, `assertions`) y añade:

- `profiles`: `["smoke"]`, `["full"]` o ambos.
- `covers`: lista de ids de la matriz funcional (p. ej. `["CLS-02", "PIP-01"]`).
- `requires`: dependencias opcionales; hoy solo `["gdc"]`.
- `extraction`: `{ "model": "...", "umbral": 0.80 }` opcional por caso.
- `documentPath` pasa a ser **relativo al repo** (hoy hay rutas locales absolutas).

## 5. Cambios en las librerías comunes (retrocompatibles)

1. **Autenticación**: soporte de function key (`x-functions-key`) tomada de
   `environments.json`; necesaria para DEV/PRO (hoy el runner solo funciona contra
   localhost sin auth). La key nunca se pasa por línea de comandos ni se loguea.
2. **Extracción parametrizable**: `extraction.model` y `extraction.umbral` por caso
   (hoy hardcodeados a `auto`/`0.80` en `New-DocumentIARequestBody`). Es el mecanismo
   para probar distintos modelos de extracción por request.
3. **Skip declarativo por dependencia**: un caso con `requires: ["gdc"]` sin
   `-IncludeGdc` termina en estado `N/A` (ni PASS ni FAIL) y así computa en cobertura.
4. **Paralelismo acotado**: `-Parallel <n>` (default 2, como el harness Evaluation)
   para el perfil `full`; `smoke` se ejecuta secuencial. Implementación con
   `ForEach-Object -Parallel` de pwsh 7.
5. **Trazabilidad**: `submittedBy` configurable; el runner post-deploy fija
   `e2e-postdeploy` para poder filtrar/limpiar estas ingestas en BD (mismo patrón que
   `migracion-dev`).

Las baterías existentes de `tests/api-tests/` deben seguir funcionando sin cambios en
sus casos (los campos nuevos son opcionales con defaults equivalentes al comportamiento
actual).

## 6. Perfiles y casos

### Perfil `smoke` (cada despliegue, ~10-15 min)

0. Health check: GET `/api/tipologias` con reintentos + POST `/api/healthcheck` con
   validación de componentes (reutiliza la lógica de `smoke-test-functions.ps1`).
1. Clasificación completa auto (TDN1_TDN2) de un documento de tipología conocida,
   pipeline entero hasta `Completed` con estado de negocio esperado.
2. `classificationOnly` con recorte de páginas.
3. Extracción de campos sobre tipología publicada con validación (aserción sobre
   `DetalleEjecucion.Extraccion` y campos no vacíos).
4. Resumen documental (`expectedType = resumen-documental`).
5. Contrato HTTP: un 400 esperado (base64 inválido).

### Perfil `full` (releases importantes / bajo demanda, 45-90 min)

Incluye el smoke y añade la matriz de variantes, todas por request:

- Providers de clasificación: `auto`, `di`, `gpt`, `hybrid-*`.
- Modelos de clasificación alternativos (`classification.model`).
- Modelos de extracción alternativos (`extraction.model`).
- Niveles: `TDN1` vs `TDN1_TDN2`.
- Markdown pre-procesado en el payload.
- Umbral de confianza (por debajo/por encima).
- Deduplicación (`skipDuplicateCheck` / `forceReprocess`).
- Límite de páginas (`PAGINAS_EXCEDIDAS` y bypass).
- Formatos: PDF, DOCX, XLSX.
- Documentos inválidos: corrupto (400/`InvalidContent`), payloads malformados (grupo 4xx).
- Prompt ad-hoc por request (Epic AB#99122).
- GDC (solo con `-IncludeGdc`): ingest con `skipGDCUpload = false` y aserción sobre
  `idGDC` en el output.

Semilla: adaptación de los 23 casos de `classification-process-cases.json` al corpus
versionado y al formato extendido.

## 7. Matriz de cobertura funcional

`coverage/functional-matrix.json`: catálogo de funcionalidades observables desde fuera,
con id estable, área y descripción. Áreas: clasificación (`CLS-*`), extracción (`EXT-*`),
resumen (`SUM-*`), validación (`VAL-*`), pipeline/orquestación (`PIP-*`), contrato HTTP
(`HTTP-*`), GDC (`GDC-*`, marcadas `conditional: "gdc"`).

Reglas del runner:

- `covers` con id inexistente en la matriz → error de configuración (falla la ejecución).
- Ítem de matriz sin ningún caso que lo cubra → aviso en el reporte ("no cubierto").
- Ítem condicional cuya dependencia está desactivada → "no aplicable".

El reporte (`report.md` + `coverage.json`) incluye:

- Resultado por caso (PASS/FAIL/SKIP/N-A) con enlace al artefacto JSON.
- % de cobertura funcional global y por área, calculado sobre el perfil ejecutado,
  distinguiendo **no cubierto** (hueco real) de **no aplicable** (GDC off).
- Lista explícita de ítems no cubiertos, para que el hueco sea visible y accionable.

### Publicación opcional a ADO (`-PublishToAdo`)

Reutiliza `ado-testplans-common.ps1` contra un Test Plan dedicado "E2E Post-despliegue"
(a crear vía la skill `azure-devops-testplans`; el MCP no expone Test Plans). La matriz
del repo es la fuente de verdad; ADO es el espejo trazable de resultados. Requiere
`$env:ADO_PAT`.

## 8. Configuración y seguridad

- `environments.json` (gitignored): `{ "dev": { "baseUrl", "functionKey" }, "pro": {...} }`.
  Se añade la entrada al `.gitignore` y la plantilla `.sample` al repo.
- Sin secretos en consola, logs ni artefactos.
- PRO: las ingestas quedan en BD marcadas con `submittedBy = "e2e-postdeploy"`; los
  análisis de operación/coste deben filtrar ese marcador (igual que `migracion-dev`).
  No hay borrado automático en esta fase.

## 9. Documentación

- `tests/e2e-postdeploy/README.md`: uso, prerequisitos (pwsh 7, function keys, PAT
  opcional), cómo añadir un caso, un documento al corpus o un ítem a la matriz.
- Actualizar BLOQUE 8 del checklist de despliegue (`docs/08_CHECKLISTS_DESPLIEGUE.md` y
  copia de `entrega/`) para referenciar el runner como paso post-deploy junto al smoke
  actual.

## 10. Criterios de aceptación

1. `run-e2e-postdeploy.ps1 -Environment dev -Profile smoke` termina en verde contra DEV
   con reporte y cobertura generados.
2. El mismo comando con `-Environment pro` funciona con function key desde
   `environments.json` (sin tocar el script).
3. El perfil `full` ejecuta la matriz de variantes con paralelismo 2 y produce
   `report.md` + `coverage.json` con % por área.
4. Sin `-IncludeGdc`, los casos GDC aparecen como N/A y no penalizan cobertura; con el
   flag, se ejecutan de verdad.
5. Las baterías previas de `tests/api-tests/` siguen ejecutando sin modificar sus casos.
6. `-PublishToAdo` publica resultados en el Test Plan dedicado.
7. README y checklist actualizados.

## 11. Riesgos y limitaciones asumidas

- **Corpus sintético**: es la pieza más laboriosa y no existe. Se arranca con 6-8
  documentos para las tipologías principales y se crece incrementalmente; la matriz de
  cobertura hace visible lo que falta. Ningún documento del corpus puede contener datos
  reales de negocio.
- **Resultados dependientes de LLM**: las aserciones de clasificación sobre documentos
  sintéticos deben ser robustas (tipología esperada, no texto exacto); si un caso resulta
  inestable, se degrada su aserción a estado de negocio + contrato en lugar de resultado
  exacto, y se documenta.
- **Coste/tiempo en PRO**: el perfil `full` consume llamadas reales a DI/GPT; por eso es
  bajo demanda y no parte del post-deploy rutinario.
- **GDC en DEV**: históricamente inestable (NOT_AUTHORIZED del lado servidor); por eso
  GDC es opt-in incluso en DEV.

## 12. Fuera de alcance

- CRUD de administración de tipologías (ya cubierto por `smoke_crud_admin.ps1` /
  `smoke_e2e_v2.ps1`).
- Cliente Batch/Lite y su UI.
- Medición de *calidad* de clasificación (accuracy): eso es el harness Evaluation del
  repo DocumentIA.Batch; este E2E mide *funcionalidad* y cobertura funcional.
- Ejecución como stage de pipeline de Azure DevOps (diseño compatible, no incluido).
- Limpieza automática de las ingestas de test en BD.
