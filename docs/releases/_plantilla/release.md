# Release vX.Y.Z

Fecha: YYYY-MM-DD · Tag: vX.Y.Z · Commit: <hash> · Tag anterior: vA.B.C · Entorno: PRO

## Validación

Build: `dotnet build src/backend/DocumentIA.sln` → <resultado> · Tests unitarios: <n/n> · Tests Admin: <n/n> · Tests AssetResolver: <n/n> · Formato: <ok/ko>
E2E DEV: smoke <n/n>, full <PASS/FAIL/N/A> · E2E PRE: smoke <n/n> · E2E PRO: smoke <n/n>

## Contenido de la release

Las tres listas de la Fase 0 del runbook (0.5 y 0.6); se dejan con "ninguno" si están vacías.

### Migraciones y scripts fuera de EF
- <migración EF> · <script SQL o PowerShell de `scripts/` (índice `ONLINE`, procedimiento, seed de configuración)>
### Configuración
- <tabla> · <seed que la cambia> · <qué cambia>
### Artefactos de IA
- <deployments, analyzers, clasificadores o modo de acceso> · <fichero de `infra/ai/`>

## Diff frente a vA.B.C

### PBIs
- AB#<n> <título>
### Features
- AB#<n> <título>
### Fixes
- <commit corto> <resumen>
### Infraestructura
- <cambios de recursos, pipelines, infra/ai>
### Configuración
- <tablas de configuración, app settings, secretos (solo nombres), migraciones>

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
| 807 Migrations-BD | pre | | |
| 799 completo | pre | | |
| 807 Migrations-BD | prod | | |
| 799 completo | prod | | |
