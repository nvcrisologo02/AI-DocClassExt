# ADR-004: Altas de configuración por clave natural en ConfigSeed
Fecha: 2026-10-21 · Estado: aceptada (2026-10-22, tras el ensayo DEV→PRE del run 79876) · Work Item: AB#101075 · Session ID: sin Session ID (proyecto sin `Gobernanza: activa`; traspaso en `docs/auxiliares/temps/2026-10-21/`)

## Contexto y problema

La regla fija 4 de `RELEASE_MANAGEMENT.md` prohíbe promocionar la configuración por `Id`: el export de DEV es un `MERGE` por clave primaria con `IDENTITY_INSERT` y los `Id` no coinciden entre entornos. En PRE y PRO solo se aplican a mano los seeds propios de cada release (pasos 2.3.3 y 4.3). La etapa ConfigSeed del pipeline 832 (`azure-pipelines-ai-artifacts.yml`, ADR-001) ya calcula por clave natural qué falta en el destino, pero no aplica nada.

Dos hechos obligan a revisarlo. Primero, si en PRE y PRO se retira la escritura directa en BD, la carga manual de seeds deja de ser posible. Segundo, el caso real de AB#101074: 19 tipologías solo de clasificación y la fila `LIPR-13` de `CatalogoTdn2` existen en DEV y faltan en PRE y PRO, y la única vía hoy es repetir a mano el script en cada entorno.

Además, ConfigSeed excluía `ModeloConfigs` en `prod` porque sus filas llevaban recursos y keys propios de PRO. Desde el cutover de la v1.0.0 (2026-10-08, AB#100321) PRO resuelve sus recursos por `ResourceAlias` e identidad, y la exclusión ha quedado desfasada.

## Opciones consideradas

- A. Alta opcional en ConfigSeed: solo filas que existen únicamente en el origen, por clave natural, sin `Id`, con aprobación del environment del destino.
- B. Mantener la regla 4 tal cual: seeds manuales en cada release.
- C. Sincronización completa por clave natural: altas, actualizaciones y bajas.
- D. Alta completa desde el Admin (Blazor), entorno por entorno.

## Decisión

Se adopta A. Con el parámetro `applyMissingConfig` (desactivado por defecto), ConfigSeed genera con `diff-config-exports.py --emit-inserts` un `INSERT ... WHERE NOT EXISTS` por cada fila que solo existe en el origen y la etapa ConfigApply lo ejecuta tras la aprobación del environment, en una transacción y con copia `__bak` previa. Nunca actualiza, nunca borra y nunca copia `Id`. La regla fija 4 se mantiene para todo lo demás.

## Motivos

- Reutiliza el diff por clave natural que ya existe y ya se revisa en cada salto.
- Una alta de una fila que falta no pisa nada del destino: el riesgo queda acotado a filas nuevas, y reejecutar no cambia nada.
- Las claves ajenas por `Id` se resuelven en el destino por código (`CatalogoTdn2.Tdn1Id` por `CodigoTdn1`). Es la única de las 6 tablas: el resto se referencia por código o clave.
- El aprobador ve el diff y el SQL de altas (artefacto `config-diff`) antes de aprobar, porque la aplicación va en una etapa propia.

## Consecuencias

- La regla fija 4 admite una segunda vía en PRE y PRO, además de los seeds de la release: las altas por clave natural del pipeline 832.
- `ModeloConfigs` entra en el diff y en las altas de `prod`. Guardas: el generador rechaza una fila con `Endpoint` o `ApiKey` con valor en cualquier nivel del JSON (el resolver da prioridad al endpoint explícito sobre el alias), y ConfigSeed comprueba que el destino tiene el App Setting `AI__Resources__<alias>__Endpoint` de cada alias.
- Escribe con el SC del destino (`AI DocClassExt PRE`/`PRO`), que ya tiene escritura en la BD porque aplica las migraciones (pipeline 807). No se crea ninguna identidad nueva, así que el permiso no queda limitado a las 6 tablas. Las protecciones son el parámetro desactivado por defecto, `dryRun`, la aprobación, la lista blanca de tablas y sentencias de `apply-config-altas.ps1` y la copia `__bak`.
- Las filas distintas entre origen y destino se siguen resolviendo a mano o desde el Admin. El diff las sigue informando.
- Vuelta atrás: borrar las filas insertadas (listadas en `config-diff`) o restaurar desde `<Tabla>__bak_configseed_build<id>`. Las copias se borran en las limpiezas diferidas de la Fase 5.
- La comparación de deriva de 4.7 en PRO sigue sin `ModeloConfigs`. Este ADR no la cambia.

## Alternativas descartadas y por qué

- B: depende de la escritura manual en BD, que se quiere retirar, y repite a mano en cada entorno lo que el diff ya sabe calcular.
- C: actualizar o borrar por clave natural pisaría cambios hechos en caliente en PRE o PRO (`runbook.md` 4.7) y convertiría DEV en la fuente que manda sobre PRO. Es un cambio de mucho más riesgo que el problema a resolver.
- D: no se ha comprobado si el Admin permite el alta completa de tipologías y del catálogo TDN2 (pendiente de AB#101075), y sería otra carga manual por entorno sin diff ni evidencia.
