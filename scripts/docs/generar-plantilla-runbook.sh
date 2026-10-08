#!/bin/sh
# Genera docs/releases/_plantilla/runbook.md a partir de las casillas de
# docs/procedimientos/RELEASE_MANAGEMENT.md, para que la plantilla no diverja del
# runbook real. La plantilla no se edita a mano: se cambia el runbook y se regenera.
# Solo copia las cabeceras de fase y las lineas "- [ ] **N.M**"; los bloques de
# codigo que van debajo de un paso no pasan a la plantilla.
# Uso: sh scripts/docs/generar-plantilla-runbook.sh (desde cualquier directorio)
cd "$(dirname "$0")/../.." || exit 1

{
  echo "# Runbook ejecutado — release vX.Y.Z"
  echo
  echo "Instancia de docs/procedimientos/RELEASE_MANAGEMENT.md. Marcar cada paso con fecha y resultado; tachar con motivo los que no aplican."
  echo
  grep -E '^## Fase |^- \[ \] \*\*[0-9]' docs/procedimientos/RELEASE_MANAGEMENT.md \
    | sed -E 's/^(- \[ \] \*\*[0-9.]+\*\*)(.*)$/\1\2 — <fecha> · <resultado>/'
  echo
  echo "## Limpiezas diferidas"
  echo "- [ ] Copia DocumentIA-prerel-<fecha> borrada el <fecha prevista>"
  echo "- [ ] Tablas ModeloConfigs__bak_* borradas el <fecha prevista>"
} > docs/releases/_plantilla/runbook.md
