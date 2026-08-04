# Monitor del Admin: KPIs unificados, consulta en servidor, serie temporal y detalle enlazable

**Fecha:** 2026-08-04
**Work items:** AB#99965, AB#99966, AB#99967, AB#99968 (Feature AB#99958)
**Fuera de alcance:** AB#99969 y AB#99970 (Configuración), que van en un ciclo posterior. AB#99971 y AB#99972 ya están completados.

---

## 1. Problema

La página `Monitor.razor` (1.129 líneas) descarga hasta 200 ejecuciones y hace todo el trabajo en el navegador. De ahí salen cuatro defectos:

1. **Dos cabeceras de KPI que miden cosas distintas.** Las tarjetas superiores se calculan en cliente sobre las ≤200 filas descargadas (`_errores`, `_mediaDuracion`, `_pctFallback`, derivados de `_filteredEjecuciones`), mientras el Cuadro de Mando inferior viene de `GetAgregadosAsync(dias)` y describe los últimos 30 días completos en base de datos. **Pueden contradecirse**, y no hay nada en la interfaz que lo advierta.
2. **Filtrado y búsqueda solo en cliente.** El endpoint `Admin_GetUltimasEjecuciones` únicamente acepta `top` (acotado a 1-200); los filtros de flujo, actividad y estado se aplican con LINQ sobre la lista ya descargada. No existe búsqueda por GUID ni por nombre de documento, ni paginación: lo que no entra en esas 200 filas es inaccesible desde la interfaz.
3. **Sin serie temporal.** `GetAgregadosAsync` agrupa por tipología y por modelo, nunca por fecha, y el proyecto no tiene ninguna librería de gráficos.
4. **Detalle no enlazable.** La única ruta es `@page "/monitor"`. El detalle se abre desplegando la fila y se pide por identificador interno de base de datos, así que no se puede compartir por correo ni referenciar desde un ticket. `GetByGuidAsync` existe en el repositorio pero no lo llama nadie.

## 2. Decisiones tomadas

| Decisión | Elección | Motivo |
|---|---|---|
| Ventana temporal | Un rango elegible que gobierna KPIs, gráficos y tabla a la vez | Elimina de raíz la contradicción entre bloques: todo lo que se ve en pantalla describe el mismo recorte |
| Rango por defecto al abrir | Últimos 7 días | Vista operativa del día a día, carga más rápida |
| Reparto backend/frontend | Dos endpoints (listado y agregados) que aceptan el mismo filtro | Al paginar solo se pide la página; los agregados se recalculan solo si cambia el filtro |
| Gráficos | ApexCharts auto-hospedado | Coherente con el self-host de jsoneditor (AB#99972); sin dependencias de red externa |
| Detalle | Fila desplegable **y** página `/monitor/{guid}`, sobre un componente compartido | Se conserva la ojeada rápida y se gana el enlace compartible, sin duplicar la presentación |
| Búsqueda | GUID por igualdad, nombre de documento por fragmento | El GUID va a índice; el nombre necesita búsqueda parcial para ser útil |

## 3. Contrato de API

Tres endpoints de solo lectura bajo `management/ejecuciones`. El modo solo lectura del Admin no los afecta: todos son `GET`.

### 3.1 Filtro compartido

Un único tipo `EjecucionFiltro` en `DocumentIA.Core`, usado por los dos primeros endpoints y por el repositorio:

| Campo | Tipo | Notas |
|---|---|---|
| `Desde` | `DateTime` (UTC) | Obligatorio |
| `Hasta` | `DateTime` (UTC) | Obligatorio |
| `Tipologia` | `string?` | Igualdad exacta |
| `Estado` | `string?` | Valor normalizado: `OK`, `REVISION`, `ERROR` |
| `Flujo` | `string?` | `Clasificacion` o `Completo` |
| `Busqueda` | `string?` | GUID exacto o fragmento de nombre de documento |

Que ambos endpoints compartan tipo es lo que hace **imposible** que la cabecera y la tabla describan recortes distintos.

### 3.2 `GET management/ejecuciones`

Parámetros: los del filtro más `page` (base 1, por defecto 1) y `pageSize` (por defecto 25, acotado a 1-200).

```json
{ "items": [ /* EjecucionResumen */ ], "total": 1234, "page": 1, "pageSize": 25 }
```

`total` es el número de filas que cumplen el filtro, no las devueltas, para poder pintar el paginador.

### 3.3 `GET management/ejecuciones/agregados`

Parámetros: los del filtro, sin paginación.

```json
{
  "total": 1234, "ok": 900, "revision": 200, "error": 134, "fallbacks": 87,
  "confianzaMedia": 0.82, "duracionMediaMs": 29600,
  "serie": [ { "fecha": "2026-08-01", "total": 120, "ok": 90, "revision": 20, "error": 10, "fallbacks": 8 } ],
  "porTipologia": [ /* AgregadoGrupo */ ],
  "porModelo": [ /* AgregadoGrupo */ ]
}
```

`serie` es la novedad: un punto por día natural dentro del rango, con los días sin ejecuciones **presentes y a cero**, para que el gráfico no dibuje una línea engañosa saltándose huecos. El relleno de huecos se hace en el servidor tras la consulta, no en SQL.

### 3.4 `GET management/ejecuciones/{guid}`

Devuelve el mismo `EjecucionDetalle` que hoy sirve el endpoint por identificador interno. Se apoya en `GetByGuidAsync`, hoy sin uso. Si no existe, `404`.

**El endpoint por `{id:int}` se retira.** Las filas del listado ya traen `EjecucionGuid`, así que tanto la fila desplegable como la página nueva pueden pedir el detalle por GUID. El Admin es el único consumidor de estos endpoints, de modo que no hay razón para arrastrar dos formas de pedir lo mismo ni para exponer el identificador interno de base de datos en una URL.

## 4. Cambios en el repositorio

`DocumentoEjecucionRepository`:

- **`GetPagedAsync(EjecucionFiltro filtro, int page, int pageSize)`** → `(IReadOnlyList<DocumentoEjecucionEntity> items, int total)`. Ordena por `FechaEjecucion` descendente. Una única consulta de conteo más una de página.
- **`GetAgregadosAsync(EjecucionFiltro filtro)`** sustituye a `GetAgregadosAsync(int dias)`, conserva los desgloses por tipología y modelo, y añade la agrupación por fecha.
- **`GetByGuidAsync`** — sin cambios; pasa a tener llamante.

**Mejora puntual necesaria:** la clasificación de estados (`EstadoFinal == "OK" || == "Completado" || == "Completed"`, y las equivalentes de revisión y error) está hoy repetida **seis veces** en el fichero. Este trabajo añadiría al menos dos repeticiones más. Se extrae a un único punto —expresiones reutilizables sobre `DocumentoEjecucionEntity` traducibles por EF— y se usa en todos los sitios. Es código que hay que tocar de todas formas; dejarlo duplicado sería empeorarlo.

Nota de rendimiento: conviene comprobar que existe índice sobre `FechaEjecucion` y sobre `EjecucionGuid`. Si falta, se añade por migración: sin ellos, la paginación y la búsqueda por GUID degradan a recorrido de tabla según crezca el histórico.

## 5. Estructura del frontend

`Monitor.razor` se reparte en componentes con una responsabilidad cada uno:

| Componente | Responsabilidad |
|---|---|
| `Pages/Monitor.razor` | Página. Posee el estado del filtro, orquesta las llamadas y reparte datos a los hijos |
| `Components/Monitor/MonitorFiltros.razor` | Selector de rango, filtros y campo de búsqueda. Emite el filtro hacia arriba |
| `Components/Monitor/MonitorKpis.razor` | **La única** cabecera de KPIs, alimentada solo por agregados |
| `Components/Monitor/MonitorGraficos.razor` | Serie temporal vía interop con ApexCharts |
| `Components/Monitor/MonitorTabla.razor` | Tabla y paginador |
| `Components/Monitor/EjecucionDetalle.razor` | **Presentación del detalle, compartida** |
| `Pages/MonitorDetalle.razor` | `@page "/monitor/{guid}"`. Envoltura fina sobre `EjecucionDetalle` |

`EjecucionDetalle.razor` es la pieza que sostiene la decisión de mantener las dos vistas: la fila desplegable y la página nueva lo reutilizan, así que la presentación del detalle existe una sola vez.

**Comportamiento de carga:**

- Al cambiar el **filtro** (rango, tipología, estado, flujo o búsqueda): se piden agregados y primera página, y el paginador vuelve a 1.
- Al cambiar de **página**: solo se pide la página. Los agregados no se tocan.
- La búsqueda aplica un retardo de ~300 ms para no lanzar una consulta por pulsación.
- Mientras se recarga se conservan los datos anteriores atenuados, en lugar de vaciar la pantalla.

## 6. Gráficos

ApexCharts se incorpora a `wwwroot/lib/apexcharts` con el mismo procedimiento aplicado a jsoneditor: descarga fijando versión, verificación del SHA512 contra el hash SRI publicado por el origen, y entrada en `.gitattributes` para que Git no normalice saltos de línea y el contenido siga coincidiendo byte a byte.

Un módulo de interop (`wwwroot/js/apexcharts-interop.js`) expone crear, actualizar y destruir. El gráfico es una serie diaria de volumen con los errores superpuestos. El servidor manda las etiquetas de día ya formateadas, evitando adaptadores de fecha.

`MonitorGraficos.razor` implementa `IDisposable` y destruye la instancia al desmontarse: en Blazor Server el componente puede desaparecer sin recargar la página, y no hacerlo dejaría instancias colgando en el navegador.

## 7. Pruebas

Sobre el stack existente (xunit, FluentAssertions, EF InMemory, siguiendo el patrón de `CatalogoTdnRepositoryTests`):

**Repositorio**
- Cada filtro por separado y en combinación acota el resultado esperado.
- La paginación devuelve las filas correctas y un `total` que corresponde al filtro, no a la página.
- La búsqueda distingue GUID exacto de fragmento de nombre.
- La serie incluye los días sin ejecuciones a cero y cubre el rango completo.
- La clasificación de estados extraída trata igual las variantes (`OK`/`Completado`/`Completed`, etc.).

**Endpoints**
- Parseo y acotado de parámetros: `pageSize` fuera de rango se ajusta, fechas ausentes o inválidas se rechazan con `400`.
- `GET /{guid}` inexistente devuelve `404`.

**Coherencia (el defecto que motiva el trabajo)**
- Un test comprueba que, para un mismo filtro, el `total` del listado coincide con el `total` de los agregados y con la suma de la serie. Es la garantía automatizada de que la cabecera y la tabla no pueden volver a contradecirse.

**Limitación conocida:** EF InMemory no traduce igual que SQL Server, en particular en agrupaciones por fecha. Si algún test de la serie no es representativo con InMemory, se cubre esa parte con SQLite en memoria, que sí traduce.

Verificación manual antes de dar por terminado: arrancar el Admin, comprobar que cabecera, gráfico y tabla responden al mismo rango, que la paginación no recalcula agregados, y que `/monitor/{guid}` abre y es compartible.

## 8. Riesgos

| Riesgo | Mitigación |
|---|---|
| Sin índices sobre `FechaEjecucion` / `EjecucionGuid` la consulta degrada al crecer el histórico | Verificar y añadir por migración si faltan |
| Partir un fichero de 1.129 líneas puede arrastrar regresiones no evidentes | Se parte por responsabilidades ya separables; verificación manual de la página completa al terminar |
| ApexCharts añade ~500 KB al recurso servido | Asumido en la decisión; se carga desde local, no penaliza en cada visita |
| Retirar el endpoint por `{id:int}` rompería a cualquier consumidor no previsto | Verificado (2026-08-04): su único consumidor es `Monitor.razor` vía `MonitorService.GetEjecucionDetalleAsync`. No hay otros |
