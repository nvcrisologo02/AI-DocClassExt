# Sección de costes en Admin y relleno retroactivo

**Fecha:** 2026-09-07
**Estado:** Diseño aprobado, en implementación
**Depende de:** [Control de costes de IA por ejecución](2026-09-07-costes-ia-por-ejecucion-design.md), rama `feature/costes-ia-por-ejecucion`.

## Objetivo

Ver el coste de IA desde Admin: agregado por periodo, desglosado por actividad y por tipología, y por ejecución, con los mismos filtros y búsquedas del Monitor. La sección no aparece en el menú de momento. Además, estimar el coste de las ejecuciones anteriores a la funcionalidad, dejándolo marcado como estimación.

## Lo que se aprende del histórico

Producción tiene 72.676 ejecuciones desde abril. Se guardaron las páginas enviadas al layout en el 99 % de ellas y el modelo de clasificación en todas. **Nunca se guardaron tokens.** Tampoco las páginas de extracción de Content Understanding, que mayo y julio sí usaban.

Como el layout es el 94 % del gasto, lo derivable coincide con lo caro. Pero no se puede validar contra la factura: las páginas de septiembre en base de datos son 14.763 y la factura dice 28.361, porque **la cuenta de IA de producción la consumen también desarrollo y preproducción**, cuyas ejecuciones viven en otras bases de datos. Consecuencia para siempre: la factura del grupo de recursos no es comparable con la suma de costes de producción.

Por eso el relleno retroactivo es una **estimación**, se marca como tal y se separa en pantalla del coste real. Nunca se mezclan.

## Cambios en base de datos

Seis columnas nullable en `DocumentoEjecuciones`, migración aditiva, sin relleno en el despliegue:

| Columna | Tipo | Contenido |
| --- | --- | --- |
| `CosteLayoutEur` | decimal(18,6) | suma de consumos con actividad Layout |
| `CosteClasificacionEur` | decimal(18,6) | actividad Clasificar |
| `CosteExtraccionEur` | decimal(18,6) | actividad Extraer, incluido el modelo interno de Content Understanding |
| `CostePromptEur` | decimal(18,6) | actividad Prompt |
| `CosteEstimado` | bit | true cuando el importe procede del relleno retroactivo |

Motivo de las cuatro columnas de actividad: el desglose vive en el contrato JSON, y agregarlo por `OPENJSON` sobre una ventana de 90 días son decenas de miles de filas de 50 kilobytes. Es el problema de rendimiento que el Monitor ya arrastra. Las columnas escalares lo evitan sin tabla nueva.

`PersistirActivity` las rellena desde el bloque de costes agrupando los consumos por actividad. Los consumos descartados cuentan en su actividad, igual que en el total.

## API y repositorio

Nuevo método `GetCostesAsync(EjecucionFiltro)` en el repositorio de ejecuciones y endpoint `management/ejecuciones/costes` que devuelve:

- Totales del periodo: coste, coste medio por ejecución, tokens, páginas.
- Recuento de ejecuciones con coste real, con coste estimado y sin coste.
- Desglose por actividad: las cuatro columnas sumadas.
- Desglose por tipología y por modelo: total y coste, ordenado por coste.
- Serie diaria de coste.

Reutiliza `EjecucionFiltro` tal cual, más un booleano `IncluirEstimados`, por defecto falso: los agregados excluyen lo estimado salvo que se pida.

El listado paginado incorpora `CosteIAEur` y `CosteEstimado` en su proyección, para que la tabla pueda mostrarlos sin abrir el contrato.

## Admin

Página `/costes`, sin `NavLink` en el menú. Misma estructura y estilos que el Monitor v2, reutilizando `MonitorFiltros` sin cambios. Tres bloques:

- **Cabecera de importes**: total, coste medio por ejecución, tokens, páginas, y el reparto real, estimado y sin coste.
- **Desglose**: por actividad, por tipología y por modelo, ordenados por coste.
- **Listado**: la tabla del Monitor con una columna de coste, que marca los estimados. El detalle desplegable y el modal de JSON se reutilizan; el desglose por llamada ya viaja en el contrato.

`MonitorTabla` gana un parámetro opcional `MostrarCoste`, falso por defecto, de modo que las páginas del Monitor no cambian.

## Relleno retroactivo

Script PowerShell por marca de agua sobre `Id`, como los de relleno existentes, que se lanza a mano cuando se decida. Para cada ejecución sin coste:

- **Layout**: páginas incluidas del contrato por la tarifa de `prebuilt-layout`, si el contrato indica que se generó markdown.
- **Clasificación generativa**: no hay tokens, así que se aplica un coste medio por documento, parámetro del script, con valor por defecto tomado del lote controlado de julio: 0,0065 euros por documento tanto para gpt-4o-mini como para gpt-5-mini. Solo si el modelo de clasificación es generativo; las forzadas por tipo esperado no consumieron.
- **Extracción**: no se puede estimar, no hay páginas ni tokens. Queda a cero y la ejecución se marca estimada igualmente.

Escribe solo columnas escalares, nunca el contrato JSON: reescribir 72.000 LOB es caro y arriesgado, y el contrato debe seguir siendo lo que se devolvió al llamador. Marca `CosteEstimado = 1`. Es idempotente y no toca filas que ya tengan coste.

## Fuera de alcance

- Enlazar la página en el menú.
- Contrastar contra la factura del grupo de recursos: no es comparable, ver arriba.
- Estimar extracción histórica: no hay datos.

## Work items

Elemento padre 100235 bajo la épica 100005; tareas 100236 a 100240.
