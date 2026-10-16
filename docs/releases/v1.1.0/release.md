# Release v1.1.0

Fecha: pendiente (ventana por fijar) · Tag: v1.1.0 · Commit candidato: `81ef03c` (`develop`, 2026-10-16, subido a origin con `92b7f9f` el 2026-10-16 07:46Z; antes `636c680`, `8ad5b1e` y `290ad6b`) · Tag anterior: `v1.0.0` (`333a9cf`, código de `17889bb`) · Entorno: PRO

**Candidato reabierto de nuevo el 2026-10-16 en `81ef03c`** (decisión del usuario): el Go de `636c680` queda sustituido para incluir el Bug AB#100983 (tasks AB#100984 y AB#100985). Los pipelines 802 y 799 aseguran `BlobRetentionCleanupCron`, `BlobRetention__DefaultDays` y `BlobRetention__BatchSize`, y el 799 también `DOTNET_GCHeapHardLimitPercent`. El contrato de settings los exige y un test unitario lo vigila. Sin cambios de código de aplicación, migraciones ni Admin. En PRO no cambia ningún valor: los settings ya existen y `ensure-app-settings.ps1` no los pisa; el 799 de la Fase 4 los validará. Validado en DEV desde la rama (802 run 79800, `f6568c7`, mismo árbol que `81ef03c`), con el backfill de DEV hecho (7.272 filas). Se repiten las Fases 1 a 3 sobre `81ef03c` cuando se suba `develop`; golden conservada y coste N/A con el mismo motivo. Hecho el 2026-10-16 desde `develop` `92b7f9f` (solo docs encima de `81ef03c`): DEV con el 802 run 79801 (smoke 6/6, GET de markdown 22/22), PRE con el 802 run 79802 (settings de retención creados, trigger indexado, backfill de PRE de 504 filas), puertas 1 y 4 PASS y Test Plan 100069 run 1076844.

**Candidato reabierto el 2026-10-16 en `636c680`** (decisión del usuario): el candidato `8ad5b1e`
aprobado el 2026-10-15 no llegó a PRO (sin tag, PRO sigue en v1.0.0) y la release se rehace
desde `develop` con todo lo que lleva, es decir, añade el PBI AB#100968 (GET de markdown
persistido: `8932041`, `3611fc4`, `dc7c994`, `499b0b4`, `636c680`; solo `DocumentIA.Functions`,
tests y docs; sin migraciones, sin Admin, sin YAML, sin configuración). Se repiten la Fase 1 (DEV),
la Fase 2 (PRE) y la Fase 3 sobre `636c680` con estas salvedades decididas por el usuario: la
puerta 2 (golden) se conserva del 2026-10-15 porque el GET no toca el proceso de clasificación
(mismo código de orquestador, modelos y prompts que `8ad5b1e`), y la puerta 3 (coste) sigue N/A
con el mismo motivo. Lo registrado abajo para `290ad6b` y `8ad5b1e` se conserva como histórico.

Paso 0.1 del runbook: candidato fijado el 2026-10-14 en `290ad6b`, el merge `--no-ff` de
`feature/100930-dedup-clave-intencion` (punta `c4fab0f`) en `develop`. Versión MINOR porque la
release lleva el PBI AB#100930. La rama se validó en DEV sobre `7640186` (pipeline 802, run
79770, DUP-01..11 11/11 PASS); `c4fab0f` añade solo el criterio de resumen por defecto en la
reutilización con su test unitario y no está desplegado en ningún entorno.

Candidato actualizado el 2026-10-15 a `8ad5b1e`: sobre `290ad6b` solo añade `39155ec` (docs de esta
release), `3449b52` (los pipelines 802 y 799 fijan el app setting `Dedup__ModoEstricto=true`;
decisión del usuario en la Fase 2: el interruptor lo gestiona el pipeline como el resto de settings
de funcionalidad, no un `az` manual) y `8ad5b1e` (el filtro de validación de esa tarea incluye el
prefijo `Dedup__`; sin él, el run 79780 del 802 en DEV creó el setting y falló al no verlo). El
código de aplicación es idéntico al de `290ad6b`, con el que se hizo la Fase 1.

## Validación

Build: `dotnet build src/backend/DocumentIA.sln` → correcto, 0 advertencias · Tests unitarios: 1530/1530 · Tests Admin: 154/154 · Tests AssetResolver: 14/14 · Formato: ok (sobre `636c680`, 2026-10-16 06:22Z–06:24Z; logs en `docs/auxiliares/temps/2026-10-16/1-1-*.log`; sobre `8ad5b1e` eran 1521 unitarios: los 9 nuevos son los del trigger de markdown)
E2E DEV: smoke 6/6 y full 31 PASS / 1 N/A sobre el código de `290ad6b` (802 run 79771 y 800 run 79772 desde `develop` `39155ec`; los 2 FAIL de FE-FE4/FE-FE5 fueron timeouts transitorios del fallback GPT de DEV, PASS al repetirlos el 2026-10-15; detalle en `runbook.md` 1.2) · E2E PRE: smoke 6/6 (`20261015-095036-pre-smoke`, 07:50Z–07:52Z, sobre `8ad5b1e`) · E2E PRO: smoke pendiente

Build, los tres juegos de tests y formato se pasaron el 2026-10-14 sobre `290ad6b` y se repitieron el 2026-10-15 sobre `3449b52` con el mismo resultado.
Los tests de AssetResolver fallaban en local con CS1705 (EF Core 8.0.31 frente a 8.0.32) por
las versiones flotantes `8.*` de `src/plugins/*.csproj` resueltas en restores distintos; se
corrige con `dotnet restore src/plugins/DocumentIA.AssetResolver.Tests --force` y no afecta
al pipeline (restaura ambos proyectos en el mismo run). Validación E2E en DEV de la rama:
`docs/auxiliares/temps/2026-10-14/dedup-clave-intencion-dev-7640186-report.md` (DUP-01..11).

## Contenido de la release

### Migraciones y scripts fuera de EF

- `20261013092225_ClaveIntencionDedup` (AB#100932) · única migración nueva frente a PRE y PRO
  (ambos en `20261005140618_IndiceDocumentosMD5` el 2026-10-14). Añade `ClaveIntencion`
  (64), `ClaveIntencionJson` (max) y `MotivoReutilizacion` (80) a `DocumentoEjecuciones` y el
  índice `IX_DocumentoEjecuciones_Documento_Clave`.
- `scripts/database/indice-documento-clave-pro.sql` (AB#100930) · **antes del 807** en PRE
  (2.1.3) y en PRO (4.10): mismas columnas e índice con `ONLINE = ON`, idempotente por bloque,
  y registra la migración en `__EFMigrationsHistory`, de modo que el 807 cierre con
  `Pendientes: 0`. Con token de Entra, partiendo por `GO`. Anotar la duración del build ONLINE.
- `scripts/database/backfill-clave-intencion.ps1` (AB#100938) · **en el mismo run del 807**
  con el parámetro `backfillClaveIntencion=true` (2.1.1 y 4.2): `-SelfTest`, `-WhatIf` y el
  backfill real por lotes; timeout 240 min; reanudable con `-DesdeId`. Idempotente (solo filas
  con `ClaveIntencion` NULL). Las filas `DUPLICADO` vacío y sin `DetalleEjecucion` quedan a
  NULL a propósito y el script las cuenta aparte.

### Configuración

- Tablas de configuración: ninguna (`ModeloConfigs`, `PromptTemplates`, `Tipologias`,
  `CatalogoTdn1`, `CatalogoTdn2`, `PluginTipologiaConfigs` sin cambios).
- App settings: `Dedup__ModoEstricto=true` en `srbapppredocai` y `srbappprodocai`, creado por los
  pipelines 802 y 799 en la tarea "Set CU Resiliencia App Settings" (variable `dedupModoEstricto`,
  `3449b52` y `8ad5b1e`; `ensure-app-settings.ps1` lo crea si falta y no pisa el existente). El código asume
  `true` sin el setting (`DedupOptions`); tenerlo explícito deja visible el interruptor de vuelta
  atrás (`false` = regla anterior por modo y nivel, sin deshacer datos). Opcionales con
  sus valores por defecto: `Dedup__MaxCandidatas` (5) y `Dedup__MaxDerivacionAlVuelo` (50). No
  están en el contrato de `ValidateConfiguration` y no hace falta añadirlos.

### Artefactos de IA

- Ninguno. `infra/ai/` solo cambia en documentación (README, manifiesto de DEV de
  `DocumentAICC_v1`, comentario de `pipeline-variables.yml`).

### Orden crítico de la ventana

1. Copia de seguridad (4.1) → índice ONLINE (4.10) → 807 con `backfillClaveIntencion=true`
   (4.2, `Pendientes: 0`) → app setting (4.3) → drenar orquestaciones en vuelo (4.15) → 799 (4.4)
   → smoke (4.6) → observación (4.9) y criterio de éxito.
2. El drenado (2.2.6 en PRE, 4.15 en PRO) es obligatorio: la release cambia la secuencia de
   actividades del orquestador y una orquestación iniciada con v1.0.0 y reanudada con v1.1.0
   rompe el replay o deserializa un resultado vacío.
3. Esquema antes que código: el código de v1.0.0 ignora las columnas nuevas, así que 4.10 y
   4.2 pueden adelantarse a la ventana.

### Criterio de éxito (cierre del PBI AB#100930)

Tras 4.9, con `query-sql.ps1` contra PRO, y repetido a D+1 y D+7:

    SELECT COUNT(*) FROM DocumentoEjecuciones r
    JOIN DocumentoEjecuciones o ON o.Id = r.EjecucionOriginalId
    WHERE r.ReutilizadaPorDuplicado = 1 AND r.FechaEjecucion >= '<fecha-hora de 4.4>'
      AND r.ClaveIntencion <> o.ClaveIntencion

Debe dar 0. Además: reparto de `MotivoReutilizacion` y `MotivoNoReutilizacion` desde 4.4 (un
pico de `modelo-extraccion-distinto` es el reproceso único de históricas GPT-directo y DI sin
`ModelKeyEfectivo`, esperado), ningún `EstadoFinal = 'DUPLICADO'` nuevo, y p50 de
`ObtenerUltimaEjecucionDuplicadoActivity` en App Insights ≤ 0,4 s. El PBI se cierra con este
resultado; en la release siguiente se retira el interruptor `Dedup__ModoEstricto`.

## Diff frente a v1.0.0

### PBIs
- AB#100930 [DEDUP] Reutilización por duplicado con clave de intención (tasks AB#100931 a
  AB#100941, Done; PBI en Committed hasta el cierre de la release).
- AB#100956 (task del Bug AB#100955): las históricas en estado de fallo no se reutilizan y el
  reenvío reprocesa (`7b6cb80`).
- AB#100983 [Bug] Retención de blobs fuera de los pipelines: DEV y PRE sin limpieza y el trigger no indexa (tasks AB#100984 y AB#100985; añadido el 2026-10-16).
- AB#100968 [API] GET de markdown persistido por sha256 o guid de ejecución (task AB#100971,
  validación en DEV; añadido al reabrir el candidato el 2026-10-16).

### Features
- `ce9605c`, `6cfbde2` clave de intención en Core, derivación desde contratos antiguos y
  cobertura de candidata (AB#100931).
- `d8d09de` columnas e índice por documento y clave (AB#100932).
- `5f4ffe7` consultas de candidatas por clave en el repositorio (AB#100933).
- `0e5ffd9`, `e40ff24`, `ca96e36` resolver de componentes, actividad de cálculo, selección de
  la histórica con segunda fase y modo legado, orquestador sin histórica compatible (AB#100934).
- `f21dabd` completar resumen o prompt sobre el markdown histórico (AB#100935).
- `beee6d9` clave y motivo en el contrato y en PersistirActivity (AB#100936).
- `3bf152f` Monitor: clave, componentes y motivo en el detalle y filtro por motivo (AB#100937).
- `fa7bb12`, `c034402` backfill por lotes y paso opcional en Migrations-BD (AB#100938).
- `5f08ba3`, `640ca80` E2E DUP-06 a DUP-10 (AB#100939).
- `b92bdba` contrato, manuales, troubleshooting, diseño técnico, runbook, README e2e y ADR-003
  (AB#100940).
- `8932041`, `dc7c994` GET `/api/documentos/{sha256}/markdown` y `/api/ejecuciones/{guid}/markdown`
  (`MarkdownFunction`, AuthorizationLevel.Function, gzip, ETag y 304, 404; solo lectura de BD) con
  sus tests; `3611fc4` y `636c680` contrato y guía (AB#100968).

### Fixes
- `7b6cb80` históricas en estado de fallo excluidas; DUP-11 (AB#100956).
- `7640186` revisión final de rama: `ModelKeyEfectivo` en GPT-directo y DI, drenado en el
  runbook, legado excluye reutilizaciones como originales (AB#100930).
- `c4fab0f` la reutilización pide el resumen por defecto con TDN1 sin exigir ClassificationOnly
  (AB#100930).
- `499b0b4` el GET de markdown escribe el cuerpo en asíncrono: con la integración ASP.NET Core
  del worker, `Body.Write` síncrono devolvía 500 con cabeceras correctas y cuerpo vacío (DEV,
  2026-10-15); el fake de tests rechaza desde entonces las escrituras síncronas (AB#100968).

### Infraestructura
- `azure-pipelines-migrations.yml`: parámetro `backfillClaveIntencion` y paso de backfill tras
  las migraciones (AB#100938).
- `docs/procedimientos/RELEASE_MANAGEMENT.md`: pasos 2.2.6 y 4.15 (drenado) y ejemplo de
  2.1.3; plantilla de runbook regenerada en esta release.
- Documentación de infraestructura ya en `develop` desde v1.0.0: alias e identidad en PRO
  (AB#100321, `e55f818`), proyecto de etiquetado de DI Studio replicado en DEV (AB#100925,
  `03be249`), D+1 de v1.0.0 (AB#100881, `e442b68`) y cierre de v1.0.0 (`aade2b7`, `712c522`).

### Configuración
- `f6568c7` y `81ef03c`: settings de retención de blobs y `DOTNET_GCHeapHardLimitPercent` desde los pipelines 802 y 799, contrato de settings y manual de configuración (AB#100983).
- Migración `ClaveIntencionDedup` y script ONLINE (arriba).
- App setting `Dedup__ModoEstricto=true` (arriba). Sin secretos nuevos.
- Contrato de salida: campos nuevos `detalleEjecucion.claveIntencion`,
  `claveIntencionComponentes`, `motivoReutilizacion`, `motivoNoReutilizacion`,
  `prompt.plantillaHash` y `prompt.omitido`; el estado `DUPLICADO` deja de emitirse en modo
  estricto (0 integradores afectados en 90 días, AB#100941). ADR-003.

## Aprobación

| Puerta | Fecha | Resultado | Evidencia |
|---|---|---|---|
| 1 Smoke E2E PRE | 2026-10-15 07:58Z | PASS (6/6; 0 hosts de PRO leídos por `target`; única excepción la conocida de AB#100906) | `evidencias/puerta1-kql-smoke-pre-2026-10-15.txt` |
| 2 Golden | 2026-10-15 09:37Z | PASS (TDN1 p=0,0654, TDN2 p=0,7539 frente a BASELINE-GPT4OMINI-DEV; 467/467 OK, 0 timeout; DocumentIA.Batch `73121ad`) | `evidencias/puerta2-compare-golden-pre-2026-10-15.md`, `puerta2-kql-golden-pre-2026-10-15.txt` |
| 3 Coste | 2026-10-15 10:05Z | N/A: la release no toca la IA ni usa el 799 (puerta condicional desde RELEASE_MANAGEMENT.md 2.5); métricas de Azure Monitor del día sin rampa en PRO atribuible a la release | `evidencias/puerta3-na-metricas-ia-2026-10-15.txt` |
| 4 Deriva | 2026-10-15 09:42Z | PASS: hashes idénticos a la referencia 2.3.4 (provisional a las 08:05Z, formal tras la golden) | `evidencias/config-v1.1.0-pre.hashes.json`, `config-v1.1.0-pre-puerta4.hashes.json` |

**Puertas sobre el candidato `81ef03c` (2026-10-16):** 1 PASS 08:32Z (smoke `20261016-102855-pre-smoke` 6/6, 08:28Z–08:30Z; KQL 08:28:30Z–08:33Z por `target`: `srbdipredocai` y `srbaisrv01predocai`, 0 hosts de PRO, 0 × 401/403, 0 eventos `CU.*`; los 400 de DI y las excepciones de PdfPig son de FH-FH5, PDF corrupto del juego ampliado lanzado en paralelo, y la única excepción ajena es la conocida de AB#100906; `evidencias/puerta1-kql-smoke-pre-2026-10-16-81ef03c.txt`) · 2 conservada del 2026-10-15 por decisión del usuario (AB#100983 solo toca pipelines y settings de limpieza) · 3 N/A con el mismo motivo · 4 PASS 08:33Z (export `v1.1.0-pre-puerta4-81ef03c`, cinco hashes idénticos a `config-v1.1.0-pre.hashes.json`; `evidencias/config-v1.1.0-pre-puerta4-81ef03c.hashes.json`). Fase 3 sobre `81ef03c`: Test Plan 100069 run **1076844** el 2026-10-16 08:40Z contra PRE, 5 Passed y 3 NotApplicable (mismo mapeo; `evidencias/testplan-100069-smoke-pre-2026-10-16-81ef03c.txt`); revisión 3.2 hecha: AB#100983 (Bug) pasado a To Validate y AB#100984 y AB#100985 (Tasks) a Done, con comentario y por decisión del usuario.
Go: **2026-10-16 · Ignacio Varas Crisologo** sobre el candidato `81ef03c` · puertas 1 (2026-10-16 08:32Z) y 4 (2026-10-16 08:33Z) en PASS, 2 conservada del 2026-10-15 09:37Z y 3 N/A con motivo; Test Plan 100069 run 1076844; revisión 3.2 hecha. Sustituye al Go del 2026-10-16 sobre `636c680`. Ventana de PRO y elevación PIM por fijar: no se ejecuta nada en PRO hasta entonces.

**Puertas sobre el candidato reabierto `636c680` (2026-10-16):** 1 PASS 06:58Z (smoke `20261016-085412-pre-smoke` 6/6, 0 hosts de PRO, GET de markdown en PRE OK; `evidencias/puerta1-kql-smoke-pre-2026-10-16.txt`, `get-markdown-pre-2026-10-16.txt`) · 2 conservada del 2026-10-15 por decisión del usuario (el GET no toca la clasificación) · 3 N/A con el mismo motivo · 4 PASS 07:00Z (`evidencias/config-v1.1.0-pre-puerta4-636c680.hashes.json`). Fase 3 sobre `636c680`: Test Plan 100069 run **1076842** el 2026-10-16 07:04Z contra PRE, 5 Passed y 3 NotApplicable (mismo mapeo que el 15/10; `evidencias/testplan-100069-smoke-pre-2026-10-16.txt`); revisión 3.2 hecha.
Go: **2026-10-16 · Ignacio Varas Crisologo** sobre el candidato `636c680` · puertas 1 (2026-10-16 06:58Z) y 4 (2026-10-16 07:00Z) en PASS, 2 conservada del 2026-10-15 09:37Z y 3 N/A con motivo; Test Plan 100069 run 1076842. Sustituye al Go del 2026-10-15 sobre `8ad5b1e`. Ventana de PRO y elevación PIM por fijar: no se ejecuta nada en PRO hasta entonces.

Test Plan 100069 (SMK-1..8): run 1076840 el 2026-10-15 11:33Z contra PRE, 5 Passed (SMK-1, 3, 4, 5, 6) y 3 NotApplicable (SMK-2 sin caso automatizado, SMK-7 no comprobado a mano, SMK-8 solo PRO); `evidencias/testplan-100069-smoke-pre-2026-10-15.txt`.
Go: **2026-10-15 · Ignacio Varas Crisologo** · puertas 1 (2026-10-15 07:58Z), 2 (2026-10-15 09:37Z) y 4 (2026-10-15 09:42Z) en PASS y 3 N/A con motivo (2026-10-15 10:05Z); Test Plan 100069 run 1076840; revisión de work items (runbook 3.2) hecha. Ventana de PRO por fijar: no se ejecuta nada en PRO hasta entonces.

## Runs de pipeline

| Pipeline | Entorno | Run | Resultado |
|---|---|---|---|
| 807 Migrations-BD (`backfillClaveIntencion=true`) | pre | 79778 (`39155ec`, 2026-10-15 07:07Z) | succeeded: Pendientes 0 (índice ONLINE aplicado antes en 2.1.3), backfill 1.013 filas, 60 reutilizaciones a NULL por diseño |
| 802 Functions + 800 Admin (en lugar del 799, por AB#100905; AssetResolver sin cambios) | pre | 79783 y 79784 (`8ad5b1e`, 2026-10-15 07:39Z) | ambos succeeded; `Dedup__ModoEstricto=true` creado por el 802; sin stage ValidateConfiguration (comprobación manual en runbook 2.2.2) |
| 802 Functions (candidato reabierto; sin 800 ni 807: Admin y migraciones sin cambios) | dev | 79798 (`636c680`, 2026-10-16 06:24Z) | succeeded; smoke DEV 6/6, GET de markdown 22/22 |
| 802 Functions (candidato reabierto) | pre | 79799 (`636c680`, 2026-10-16 06:39Z) | succeeded; puertas 1 y 4 PASS |
| 802 Functions (candidato `81ef03c`, desde `develop` `92b7f9f`) | dev | 79801 (2026-10-16 07:47Z) | succeeded; smoke DEV 6/6 (`20261016-095942-dev-smoke`), GET de markdown 22/22, settings `BlobRetention*` y GCHeap 28 presentes |
| 802 Functions (candidato `81ef03c`, desde `develop` `92b7f9f`) | pre | 79802 (2026-10-16 08:06Z) | succeeded; 3 settings `BlobRetention*` creados a las 08:17Z, trigger indexado en el segundo arranque (08:17:26Z); puertas 1 y 4 PASS |
| 807 Migrations-BD (`backfillClaveIntencion=true`) | prod | | |
| 799 completo | prod | | |
