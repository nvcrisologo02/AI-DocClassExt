# Releases

Registro de cada versión desplegada en PRO. Procedimiento: [RELEASE_MANAGEMENT.md](../procedimientos/RELEASE_MANAGEMENT.md).

## Regla de versión

SemVer `vX.Y.Z`, tag anotado sobre el commit de `develop` desplegado y verificado en PRO
(Fase 5 del runbook). MAJOR: cambia un contrato externo (API de ingest, esquema de
`ConfiguracionJson`, formato de artefactos de IA). MINOR: la release lleva PBIs o features.
PATCH: solo fixes o configuración. La primera versión con este esquema es `v1.0.0`, con tag
anterior `deploy-pro-2026-09-20` (PRO en `fe1533f`). Los tags `deploy-pro-*` y `release-*`
anteriores se conservan como histórico y no se vuelven a crear.

## Cómo abrir una release

1. Copiar `_plantilla/` a `vX.Y.Z/`.
2. Rellenar `release.md` (cabecera y diff) en la Fase 0 y `runbook.md` a medida que se ejecuta.
3. `evidencias/`: solo texto corto (resumen del smoke, `compare.md`, `.hashes.json`, cuadre de
   coste). Sin binarios, sin secretos, sin function keys. Lo voluminoso queda en
   `docs/auxiliares/temps/<fecha>/` y se referencia por ruta.
4. Al cerrar, añadir la fila en la tabla de abajo.

Cómo regenerar la plantilla: `_plantilla/runbook.md` no se edita a mano; tras cambiar las casillas del runbook, `sh scripts/docs/generar-plantilla-runbook.sh`.

## Índice

| Versión | Fecha | Commit | Work items | Registro |
|---|---|---|---|---|
| v1.0.0 | 2026-10-08 | `333a9cf` (código de `17889bb`) | AB#100814, AB#100880, AB#100879, AB#100863, AB#100321 (Steps 1 y 2 ampliados a identidad), AB#100868, AB#100779 en `off`; backfill AB#100881 en D+1 | [v1.0.0/release.md](v1.0.0/release.md), [runbook.md](v1.0.0/runbook.md); tag `v1.0.0` |
| (histórico) | 2026-09-20 | `fe1533f` | ver `docs/procedimientos/RUNBOOK_RELEASE_PRO_2026-09.md` | tag `deploy-pro-2026-09-20` |
