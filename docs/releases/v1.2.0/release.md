# Release v1.2.0

Fecha: pendiente (ventana de PRE; en preparación, Fase 0-1 hechas) · Tag: v1.2.0 (sin crear) · Commit: 85626406 · Tag anterior: v1.1.1 (ba0a5b3) · Entorno: PRE y PRO (espejo de DEV)

Commit candidato: `85626406`, HEAD de `develop` tras integrar el hito 2 de la release (`feature/101570-release-v1.2.0-pre`: luna, embeddings y scripts de smoke) y la rama de ConfigSeed (`feature/101075-configseed-alta-clave-natural`). Sustituye al candidato previo `bc41f289`. Guía del operador: [GUIA_OPERADOR_PRE.md](GUIA_OPERADOR_PRE.md).

## Validación

- Build/tests sobre 85626406: <PENDIENTE>
- Build `dotnet build src/backend/DocumentIA.sln` → 0 warnings / 0 errores · Tests unitarios: 1678/1678 · Formato `dotnet format --verify-no-changes`: limpio · Tests Python del diff (`python -m unittest scripts.database.tests.test_diff_config_exports`): OK. Valores pendientes de confirmar sobre 85626406 en la línea anterior.
- E2E DEV: smoke sobre DEV con el candidato previo `bc41f289`: run 1076980 del Test Plan 100069 (5 Passed, 3 N/A) y runner `20261126-100941/101546` (9/9). Sin cambios en `src/` entre `bc41f289` y `85626406` (`git diff --stat bc41f289..85626406 -- src/` vacío), por lo que el resultado vale para el candidato. Evidencia: `evidencias/testplan-100069-smoke-dev-2026-11-26.txt`.
- E2E PRE: smoke pendiente (Fase 2) · E2E PRO: smoke pendiente.
- Ensayo en seco del pipeline 832 `pre` con `applyMissingConfig=true`: run 80266 (2026-11-26), 30 altas previstas. Evidencia: `evidencias/832-run-80266-dryrun-pre.txt`.
- DEV en modo híbrido desde 2026-11-26 12:57:42Z (script 07, copia `ModeloConfigs__bak_20261126_125742`); el script 08 de luna se repitió en DEV el mismo día para las 4 tipologías de AB#101564. Evidencia: `evidencias/dev-hibrido-2026-11-26.txt`.

## Contenido de la release

Las tres listas de la Fase 0 del runbook (0.5 y 0.6).

Orden de la ventana de PRE (decisión del usuario, 2026-11-26): 832 `pre` con `applyMissingConfig=false` → pausar ingesta → 799 → `aplicar-luna.ps1` → reinicio → copia de artefactos v5 → 07 → 832 con `applyMissingConfig`. Detalle en `GUIA_OPERADOR_PRE.md`.

### Migraciones y scripts fuera de EF
- Migraciones EF: ninguna.
- `scripts/migrations/gpt56-luna/aplicar-luna.ps1 -Environment pre` (scripts 01, 02, 05, 03, 09 y 08 de luna). Crea las 4 filas de luna de `ModeloConfigs`; no las crea el 832.
- `scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql` (modo `hibrido`, artefacto v5).
- `scripts/storage/copy-embeddings-artifacts.ps1 -Version v5 -TargetEnvironment pre`.
- Pipeline 832 con `applyMissingConfig` (altas por clave natural, ADR-005): 24 tipologías más CERJ-71 y LIPR-13 de `CatalogoTdn2`.
- `scripts/configuration/remove-legacy-keyvault-secrets.ps1 -TargetEnvironment pre` (borrado de secretos heredados).
- `scripts/storage/set-lifecycle-documents-clasif.ps1 -Environment pre` (lifecycle de blobs).
### Configuración
- App settings fijados por el 799 en cada despliegue: `PromptDefaults__ModelKey=prompt.gpt56-luna` y `GDC__BypassSslValidation=false`.
- `ModeloConfigs` · script 07 · fila `clasificador.embeddings` a modo `hibrido`, umbrales 0,6/0,5/0,8, artefacto v5 (ADR-006).
- `ModeloConfigs` · `aplicar-luna.ps1` · 4 filas de luna y PromptTemplates de luna (ADR-004).
- `Tipologias` y `CatalogoTdn2` · 832 con `applyMissingConfig` · 24 tipologías, CERJ-71 y LIPR-13 (ADR-005).
- Secretos heredados de Key Vault retirados (solo nombres en el script; AB#101450).
### Artefactos de IA
- Deployment `gpt-5.6-luna` (DataZoneStandard, 150K), lo crea el 832 en PRE desde `infra/ai/deployments.pre.json`.
- Artefactos `modelos/clasificador-embeddings/v5/` en el storage de PRE (copia desde DEV).

## Diff frente a v1.1.1

186 commits (`git log v1.1.1..85626406 --oneline`), agrupados por AB# (n = commits que lo citan).

### PBIs
- AB#99939 (18) cutover de gpt-5.6-luna en DEV, ADR-004; menores AB#101438 (5) y Bug AB#101437.
- AB#101570 (22) release v1.2.0: PRE espejo de DEV, ADR-006, scripts de luna, embeddings y smoke.
- AB#101124 (9) identidad SAREB en el Admin (subtareas AB#101125, AB#101126, AB#101127, AB#101128).
- AB#101075 (5) y AB#101164 (1) altas de configuración por clave natural en el 832 (ADR-005).
- AB#101564 (5) corte v5 de embeddings Lote 4.
### Features
- AB#101411 (2) Tdn1 por familia en la tipología virtual · AB#101412 (5) scripts de DEV y mediciones de la caché de prompt de luna · AB#101428 (6) breakpoint del tramo estable de la caché · AB#101435 (6) y AB#101433 (2) umbral de confianza en la tipología virtual y golden completo · AB#101414 (1) manual de luna.
- AB#101074 (2) alta de 19 tipologías solo clasificación.
- AB#100780 (8), AB#100779 (1), AB#101405 (4), AB#101421 (3), AB#101418 (3), AB#101313 (4) clasificador por embeddings: modelos v2-v5, sombra, runbook de cortes y especificaciones.
### Fixes
- AB#101245 (4) reintento por mitades ante HTTP 400 por longitud en embeddings.
- AB#101553 (12) validación TLS del GDC contra la CA de SAREB (`GDC__BypassSslValidation`).
- AB#101372 (2) `apply-sql-dev.ps1` rechaza servidores que no sean DEV · AB#101373 (2) y AB#101374 (1) comprobaciones y precisiones de la spec de luna.
- AB#100930 (2), AB#100988 (1), AB#100983 (1) observación posterior a v1.1.0 y v1.1.1 en PRO (registro de release).
### Infraestructura
- AB#101370 (4) deployment `gpt-5.6-luna` de DEV a 150K TPM · AB#101371 (10) deployment de luna de DEV en la infraestructura real.
- AB#99934 (5) prueba de gpt-5.6-luna en DEV · AB#99938 (1) `PromptDefaults__ModelKey` por entorno en el pipeline.
- AB#100252 (1) contratos: clave de sistema para el sondeo de estado.
### Configuración
- AB#101450 (14) retirada de app settings heredados (27 settings y 4 secretos) · AB#101447 (5) diseño de esa retirada · AB#101452 (5) retirada de `.env.example` y versionado de CLAUDE.md · AB#101451 (3) decisiones sobre MONITOREO, contactos y secreto `GDC--Endpoint`.
- Repaso documental AB#101445 y derivadas: AB#101446 (1), AB#101448 (2), AB#101449 (2), AB#101126 (2, ver identidad SAREB).
- AB#101172 (3) bloques de decisiones del documento de arquitectura intactos.

## Aprobación

| Puerta | Fecha | Resultado | Evidencia |
|---|---|---|---|
| 1 Smoke E2E PRE | | | |
| 2 Golden | | | |
| 3 Coste (condicional: PASS o N/A con motivo) | | | |
| 4 Deriva | | | |

Test Plan 100069 (SMK-1..8): run <id> el <fecha>, <resultado>. (Referencia en DEV: run 1076980, 5 Passed y 3 N/A.)
Go: <fecha> · <nombre>

## Runs de pipeline

| Pipeline | Entorno | Run | Resultado |
|---|---|---|---|
| 832 dryRun (`applyMissingConfig=true`) | pre | 80266 (2026-11-26) | 30 altas previstas |
| 807 Migrations-BD | pre | | |
| 799 completo | pre | | |
| 807 Migrations-BD | prod | | |
| 799 completo | prod | | |
