# Release v1.1.0

Fecha: pendiente (ventana por fijar) · Tag: v1.1.0 · Commit candidato: `290ad6b` (`origin/develop`, 2026-10-14) · Tag anterior: `v1.0.0` (`333a9cf`, código de `17889bb`) · Entorno: PRO

Paso 0.1 del runbook: candidato fijado el 2026-10-14 en `290ad6b`, el merge `--no-ff` de
`feature/100930-dedup-clave-intencion` (punta `c4fab0f`) en `develop`. Versión MINOR porque la
release lleva el PBI AB#100930. La rama se validó en DEV sobre `7640186` (pipeline 802, run
79770, DUP-01..11 11/11 PASS); `c4fab0f` añade solo el criterio de resumen por defecto en la
reutilización con su test unitario y no está desplegado en ningún entorno.

## Validación

Build: `dotnet build src/backend/DocumentIA.sln` → correcto, 0 advertencias · Tests unitarios: 1521/1521 · Tests Admin: 154/154 · Tests AssetResolver: 14/14 · Formato: ok
E2E DEV: smoke pendiente sobre `290ad6b` (Fase 1.2), full N/A · E2E PRE: smoke pendiente · E2E PRO: smoke pendiente

Build, los tres juegos de tests y formato son del 2026-10-14 sobre el árbol de `290ad6b`.
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
- App settings: `Dedup__ModoEstricto=true` en `srbapppredocai` y `srbappprodocai`. El código
  asume `true` sin el setting (`DedupOptions`); ponerlo explícito deja visible el interruptor de
  vuelta atrás (`false` = regla anterior por modo y nivel, sin deshacer datos). Opcionales con
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

### Fixes
- `7b6cb80` históricas en estado de fallo excluidas; DUP-11 (AB#100956).
- `7640186` revisión final de rama: `ModelKeyEfectivo` en GPT-directo y DI, drenado en el
  runbook, legado excluye reutilizaciones como originales (AB#100930).
- `c4fab0f` la reutilización pide el resumen por defecto con TDN1 sin exigir ClassificationOnly
  (AB#100930).

### Infraestructura
- `azure-pipelines-migrations.yml`: parámetro `backfillClaveIntencion` y paso de backfill tras
  las migraciones (AB#100938).
- `docs/procedimientos/RELEASE_MANAGEMENT.md`: pasos 2.2.6 y 4.15 (drenado) y ejemplo de
  2.1.3; plantilla de runbook regenerada en esta release.
- Documentación de infraestructura ya en `develop` desde v1.0.0: alias e identidad en PRO
  (AB#100321, `e55f818`), proyecto de etiquetado de DI Studio replicado en DEV (AB#100925,
  `03be249`), D+1 de v1.0.0 (AB#100881, `e442b68`) y cierre de v1.0.0 (`aade2b7`, `712c522`).

### Configuración
- Migración `ClaveIntencionDedup` y script ONLINE (arriba).
- App setting `Dedup__ModoEstricto=true` (arriba). Sin secretos nuevos.
- Contrato de salida: campos nuevos `detalleEjecucion.claveIntencion`,
  `claveIntencionComponentes`, `motivoReutilizacion`, `motivoNoReutilizacion`,
  `prompt.plantillaHash` y `prompt.omitido`; el estado `DUPLICADO` deja de emitirse en modo
  estricto (0 integradores afectados en 90 días, AB#100941). ADR-003.

## Aprobación

| Puerta | Fecha | Resultado | Evidencia |
|---|---|---|---|
| 1 Smoke E2E PRE | | | |
| 2 Golden | | | |
| 3 Coste | | | |
| 4 Deriva | | | |

Test Plan 100069 (SMK-1..8): run <id> el <fecha>, <resultado>.
Go: <fecha> · <nombre>

## Runs de pipeline

| Pipeline | Entorno | Run | Resultado |
|---|---|---|---|
| 807 Migrations-BD (`backfillClaveIntencion=true`) | pre | | |
| 799 completo | pre | | |
| 807 Migrations-BD (`backfillClaveIntencion=true`) | prod | | |
| 799 completo | prod | | |
