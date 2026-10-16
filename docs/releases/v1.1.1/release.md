# Release v1.1.1

Fecha: pendiente · Tag: v1.1.1 · Commit: `40b6c71` (código candidato; `develop` puede llevar encima solo commits de documentación y del juego E2E de esta release) · Tag anterior: v1.1.0 (`efa094d`) · Entorno: PRO

PATCH: un único fix de la reutilización por duplicado (AB#100988), encontrado en la validación en PRO de v1.1.0.

## Validación

Build: `dotnet build src/backend/DocumentIA.sln` → 0 advertencias, 0 errores · Tests unitarios: 1535/1535 · Tests Admin: 154/154 · Tests AssetResolver: 14/14 · Formato: ok (2026-10-16 ~22:55Z, sobre `45fab3a`, mismo árbol de código que `40b6c71`)
Pester del runner E2E (esquema del juego DUP incluido): 128/128
E2E DEV: smoke 6/6 (802 run 79818), juego DUP 12/12 (DUP-01..11 en el run completo; DUP-12 1/1 tras corregir su documento) · E2E PRE: smoke 6/6 (802 run 79819) · E2E PRO: smoke pendiente

## Contenido de la release

### Migraciones y scripts fuera de EF
- Ninguno. Última migración del repo `20261013092225_ClaveIntencionDedup`, la misma que tiene PRO (`__EFMigrationsHistory`, consultado el 2026-10-16). `git diff v1.1.0..40b6c71` no toca `src/backend/DocumentIA.Data`, `scripts/` ni `infra/`.
### Configuración
- Ninguna. Sin seeds; tablas de configuración y app settings sin cambios.
### Artefactos de IA
- Ninguno.

## Diff frente a v1.1.0

### PBIs
- Ninguno.
### Features
- Ninguna.
### Fixes
- `45fab3a` AB#100988 (Bug, Related a AB#100930) / AB#100989 (Task): en modo completo, `ObtenerUltimaEjecucionDuplicadoActivity.ModeloExtraccionCoincideAsync` toma como modelo esperado el vacío cuando la tipología tiene `extraction.enabled = false`, también con modelo pedido. Antes se comparaba el `ModelKeyEfectivo` vacío de la histórica con el `modelKey` que la tipología conserva y se reprocesaba con `modelo-extraccion-distinto` (181 de 208 tipologías publicadas en PRO). Evidencia en PRO: ejecuciones 137637 y 137641, tipología `nots.01`.
### Infraestructura
- Ninguna.
### Configuración
- Ninguna.

## Decisiones de la release

- Despliegue con el pipeline por componente **802** (Functions) en DEV, PRE y PRO (Anexo A: la release no toca Admin, AssetResolver, bootstrap ni configuración). Evita además el efecto AB#100905 del 799.
- Sin 807 ni backfill: no hay migraciones.
- **Puerta 2 (golden): N/A por decisión del usuario (2026-10-16).** El fix solo cambia la elegibilidad de una histórica para reutilizarse; no toca clasificación, modelos, prompts ni configuración. La golden va con `forceReprocess` y no pasa por la reutilización, así que no mediría el cambio. Sigue vigente la golden del 2026-10-15 sobre el código de clasificación de v1.1.0.
- **Puerta 3 (coste): N/A.** Sin cambios en recursos ni modelos de IA; el efecto esperado es menos coste (menos reprocesos).
- Drenado de orquestaciones (2.2.6/4.15): no obligatorio. La release no cambia la secuencia de actividades del orquestador; solo el resultado de una actividad.

## Aprobación

| Puerta | Fecha | Resultado | Evidencia |
|---|---|---|---|
| 1 Smoke E2E PRE | 2026-10-16 | PASS (6/6, 0 hosts de PRO, 0 × 401/403) | `evidencias/puerta1-kql-smoke-pre-2026-10-16.txt` |
| 2 Golden | 2026-10-16 | N/A (decisión del usuario, ver "Decisiones") | — |
| 3 Coste (condicional: PASS o N/A con motivo) | 2026-10-16 | N/A (sin cambios en IA) | — |
| 4 Deriva | 2026-10-16 | PASS (hash idéntico en las 5 tablas) | `evidencias/config-v1.1.1-pre.hashes.json`, `config-v1.1.1-pre-puerta4.hashes.json` |

Test Plan 100069 (SMK-1..8): run <id> el <fecha>, <resultado>.
Go: <fecha> · <nombre>

## Runs de pipeline

| Pipeline | Entorno | Run | Resultado |
|---|---|---|---|
| 802 Functions | dev | 79818 (`b7b62e7`, código `40b6c71`) | succeeded |
| 802 Functions | pre | 79819 (`3c0b79f`, código `40b6c71`) | succeeded |
| 802 Functions | prod | | |
