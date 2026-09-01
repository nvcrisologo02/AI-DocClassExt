# Optimización de rendimiento del Monitor Admin (BD PRO > 60k ejecuciones) — Diseño

**Work items:** PBI AB#100182 (padre: Epic 100005), tasks AB#100183–AB#100186.
**Fecha:** 2026-09-01

## Problema

La monitorización del Admin (MonitorV2) es lenta y pesada contra la BD de PRO
(> 60.000 filas en `DocumentoEjecuciones`, tabla de 2,88 GB). Causas verificadas en código:

1. **Listado paginado con LOBs.** `DocumentoEjecucionRepository.GetPagedAsync` materializa la
   entidad completa: cinco columnas `nvarchar(max)` por fila (`ContratoSalidaCompletoJson`
   ~16 KB de media, `DatosOriginalesJson`, `DatosFinalesJson`, `ActivityTimelineJson`,
   `AssetResolverResultJson`) más `Include(Documento)` que arrastra
   `NormalizacionMarkdownCompressed` (~10 KB/fila). La Function
   (`Admin_GetUltimasEjecuciones`) descarta casi todo y proyecta ~20 escalares: cada página
   de 25 filas mueve del orden de 1 MB de SQL a la Function para pintar una tabla de texto.
2. **Agregados en ~23 consultas.** `GetAgregadosAsync` lanza por refresco: 1 count total,
   6 counts/averages globales, 5 GROUP BY (tipología, modelo, día, calidad, estado proceso,
   matriz) y 11 counts del histograma de confianza. Cada consulta re-escanea el rango filtrado.
3. **Índice no cubriente.** Solo existe índice sobre `FechaEjecucion`; rangos amplios fuerzan
   key lookups contra el índice clúster (engordado por los LOBs) o directamente un scan.
4. **Auto-refresco.** MonitorV2 repite el ciclo completo cada 30 s por defecto y se multiplica
   por pestaña abierta.

## Decisión (enfoques A + B aprobados)

1. **Proyección DTO ligera en el listado** (`EjecucionListadoItem`): `Select()` solo de los
   campos que consume la Function (escalares + `NombreArchivo`/`SubmittedBy` del documento +
   `ActivityTimelineJson`), `AsNoTracking`, sin `Include`.
2. **Consolidación de agregados**: quitar el `Include(Documento)` innecesario (EF resuelve el
   join desde el `Where` cuando el filtro usa la navegación), unificar los 6 counts/averages
   globales en un único `GroupBy(e => 1)` y el histograma en un solo GROUP BY con el índice
   de tramo calculado en SQL. Resultado: de ~23 a ~9 round-trips.
3. **Índice cubriente**: reemplazar el índice simple sobre `FechaEjecucion` por uno con
   `INCLUDE` de las columnas que usan filtros, agregados y proyección del listado (sin LOBs).
   Todos los agregados pasan a ser range-scans de índice; el listado solo hace key lookups
   para las 25 filas de la página (por `ActivityTimelineJson`).
4. **Medición antes/después** en DEV y PRO (App Insights, duración de
   `Admin_GetAgregados` y `Admin_GetUltimasEjecuciones`) documentada en el PBI.

Descartado por ahora (YAGNI): caché servidor y tablas de pre-agregación; se reevaluará con la
medición en la mano. Complementario e independiente del PBI AB#100165 (reducción de
almacenamiento: duplicidad JSON / markdown binario).

## Detalles de diseño

### DTO del listado

`EjecucionListadoItem` vive en `DocumentIA.Data/Repositories` junto a `EjecucionFiltro` y
`EjecucionAgregados`. `GetPagedAsync` cambia de firma a
`Task<(IReadOnlyList<EjecucionListadoItem> Items, int Total)>`. El coalesce
`SubmittedBy ?? Documento.SubmittedBy` baja del mapeo de la Function a la proyección SQL
(COALESCE), donde ya se hacía en el filtro. El detalle (`GetByGuidAsync`) no cambia: ahí sí
se necesitan los JSON completos.

### Histograma en una consulta

Los 11 counts por tramo se sustituyen por un GROUP BY sobre una cadena de condicionales con
los mismos literales (`< 0.40`, `< 0.50`, … `< 0.95`) que las comparaciones actuales, de modo
que el comportamiento en los bordes es idéntico. EF Core 8+ traduce condicionales anidados a
CASE WHEN (el mismo patrón que ya usa `porCalidad` con 3 ramas). Cambio de semántica asumido
y documentado: un valor negativo de confianza (no debería existir) antes no caía en ningún
tramo y ahora cae en el primero.

### Índice cubriente

```
IX_DocumentoEjecuciones_FechaEjecucion_Monitor
  ON DocumentoEjecuciones (FechaEjecucion)
  INCLUDE (EstadoFinal, ConfianzaGlobal, UseFallbackLLM, Tipologia, ModeloClasificacion,
           ClassificationOnly, DuracionTotalMs, DocumentoId, EjecucionGuid, SubmittedBy,
           ConfianzaClasificacion, DuracionClasificacionMs, DuracionExtraccionMs,
           DuracionGDCMs, DuracionValidacionMs, DuracionIntegracionMs, DuracionPersistenciaMs)
```

Sustituye al índice actual sobre `FechaEjecucion` (mismo prefijo de clave; mantener ambos
sería redundante). Migración EF + script idempotente. **PRO se aplica a mano** (el stage
Apply del pipeline Migrations-BD está bloqueado por Deny Public Network Access), con
`WITH (ONLINE = ON)` añadido al script manual.

### Lo que no cambia

- Contratos HTTP de `Admin_GetUltimasEjecuciones`, `Admin_GetAgregados` y el detalle:
  misma forma JSON, el frontend Blazor no se toca.
- Semántica de filtros y agregados (los tests existentes de
  `DocumentoEjecucionRepositoryCalidadTests` / `FiltroTests` deben seguir en verde).
- Auto-refresco del MonitorV2 (30 s): con las consultas optimizadas deja de ser un problema;
  si la medición dijera lo contrario, se abriría el enfoque C (caché) como trabajo aparte.

## Criterio de éxito

Con > 60k filas y rango de 30 días: `Admin_GetAgregados` y `Admin_GetUltimasEjecuciones` en
cientos de ms como máximo (hoy, segundos), y el payload del listado reducido en ~dos órdenes
de magnitud. Verificado con la medición antes/después de AB#100186.
