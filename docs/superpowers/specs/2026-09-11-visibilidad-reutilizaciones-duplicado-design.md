# Visibilidad de las ejecuciones reutilizadas por duplicado

- **Work item**: AB#100258
- **Fecha**: 2026-09-11
- **Rama**: `feature/100258-visibilidad-reutilizaciones`
- **Estado**: diseño aprobado, pendiente de plan de implementación

## Problema

Cuando una petición se resuelve por deduplicación y existe una ejecución histórica
reutilizable, el orquestador devuelve el contrato guardado y termina **sin escribir nada**:
ni fila en `DocumentoEjecuciones`, ni `Auditoria`. `PersistirActivity` es el único escritor
de esa tabla y el Monitor es el único lector, así que para Admin la petición no ha existido.

Caso confirmado en DEV el 2026-09-08: una petición del portal sobre
`DI13 00C0007699_PPTO (1).pdf` (documento 6395) devolvió 200 con
`DocumentIAInstanceId = 602d3becd8664f849b877b5caee23abe`, que es el `InstanceId` de una
orquestación del 14 de agosto. En BD no hay ninguna fila nueva: ni con ese `InstanceId`,
ni con el `CorrelationId` de la llamada.

Consecuencias operativas:

- No se puede saber cuántas peticiones se sirven por reutilización, sobre qué documentos,
  quién las lanza ni con qué latencia.
- No se puede cuantificar el coste de IA que la deduplicación está ahorrando.
- Si un cliente reporta un problema sobre una respuesta reutilizada (contenido obsoleto,
  resumen ausente, tipología que ya no aplica) no hay nada que mirar en el Monitor.
- La ejecución original queda como única huella, lo que induce a error al interpretar
  fechas y volúmenes.

Continúa AB#100178, que hizo persistir las salidas tempranas `NO_CLASIFICADO` y
`DUPLICADO`-sin-histórico, y dejó fuera justo la rama de reutilización correcta.

## Objetivo

Toda petición que llegue al backend deja rastro consultable desde Admin, incluidas las
servidas por reutilización; el rastro distingue una reutilización de una ejecución real; y
**ninguna cifra de calidad o coste existente cambia de valor**.

## Fuera de alcance

- **AB#100272** (el GUID que recibe el cliente no es buscable en el Monitor). Detectado al
  diseñar esto; se saca por afectar también a ejecuciones normales.
- Relleno retroactivo: no hay reutilizaciones históricas que reconstruir. El registro
  empieza en el despliegue.
- Contador materializado de "veces reutilizada" en la ejecución original: se resuelve como
  consulta de lectura sobre `EjecucionOriginalId`.

## Invariante transversal

> **"Última ejecución de un documento" significa "última ejecución con contrato"**, es
> decir, excluyendo reutilizaciones.

Tres sitios usan hoy esa definición (`TOP 1 ... ORDER BY FechaEjecucion DESC`):

| Consumidor | Situación |
|---|---|
| `ObtenerUltimaEjecucionDuplicadoActivity` | Ya cumple: filtra candidatas por `ContratoSalidaCompletoJson` no vacío. Efecto derivado: **una reutilización nunca se reutiliza**, no hay cadenas. |
| `tests/e2e-postdeploy/lib/postdeploy-db.ps1` | Hay que excluirlas: si no, una aserción puede caer sobre una fila sin contrato. |
| `scripts/database/backfill-markdown-cobertura.ps1` | Igual: leería un contrato NULL. |

## 1. Modelo de datos

Migración aditiva sobre `DocumentoEjecuciones`:

| Columna | Tipo | Significado |
|---|---|---|
| `ReutilizadaPorDuplicado` | `bit NOT NULL DEFAULT 0` | 1 = la fila registra una petición servida con el contrato de otra ejecución. |
| `EjecucionOriginalId` | `int NULL`, FK self-reference a `DocumentoEjecuciones.Id`, `ON DELETE NO ACTION` | Ejecución cuyo contrato se devolvió. Solo informada cuando el flag es 1. EF crea índice por convención sobre la FK. |

Además, índice filtrado `IX_DocumentoEjecuciones_InstanceId_Reutilizadas` sobre `InstanceId`
con `WHERE ReutilizadaPorDuplicado = 1`: la guarda de idempotencia de §2.4 consulta por
`InstanceId` en cada petición servida por duplicado, y sin índice sería un scan de la tabla
entera en el único flujo cuya virtud es responder en milisegundos. Filtrado porque solo
consulta filas de reutilización.

Reparto de campos en una fila de reutilización:

- **De la llamada actual**: `FechaEjecucion`, `EjecucionGuid` (uno nuevo, generado por la
  actividad igual que en cualquier ejecución), `InstanceId`,
  `OperationId`, `SubmittedBy`, `ClassificationOnly`, `NivelClasificacion`,
  `DuracionTotalMs` (el real, decenas de ms), `ActivityTimelineJson` (el seguimiento real:
  normalizar → verificar duplicado → recuperar histórica).
- **Copiados de la original**, para que la fila sea legible en el listado sin join:
  `Tipologia`, `EstadoFinal`, `ConfianzaGlobal`, `ConfianzaClasificacion`,
  `ModeloClasificacion`, `UseFallbackLLM`, `IdActivo`.
- **Vacíos**: `ContratoSalidaCompletoJson`, `DatosOriginalesJson`, `DatosFinalesJson`,
  `AssetResolverResultJson`, las duraciones por actividad, y **todas las columnas de coste
  a NULL** con `CosteEstimado = 0`.

Mantener el `EstadoFinal` de la original es deliberado: la reutilización de un `ERROR` debe
seguir contando como error.

El coste evitado **no se guarda**: se calcula en lectura por join contra `CosteIAEur` de la
original, para no mantener un importe derivado.

Índice del Monitor `IX_DocumentoEjecuciones_FechaEjecucion_Monitor`: se recrea añadiendo
`ReutilizadaPorDuplicado` y `EjecucionOriginalId` al `INCLUDE`. El predicado por defecto
(`= 0`) se resuelve como residual sobre el índice, sin lookups; no hace falta subirlas a
columna clave. Sin esto se pierde lo ganado en AB#100182 / AB#100185.

## 2. Escritura desde el orquestador

### 2.1 Input

`PersistirInput` gana un bloque opcional. Si es `null`, la actividad se comporta
exactamente como hoy:

```csharp
public class PersistirInput
{
    public ContratoSalida Salida { get; set; } = new();
    public string? SubmittedBy { get; set; }
    public ReutilizacionInput? Reutilizacion { get; set; }   // nuevo
}

public class ReutilizacionInput
{
    public string EjecucionOriginalGuid { get; set; } = "";  // fila cuyo contrato se devolvio
    public string Sha256 { get; set; } = "";                 // para el lookup del documento
}
```

`Salida` es el contrato reutilizado —lo mismo que recibe el llamante— y el bloque aporta lo
que ese contrato no puede saber.

El bloque **no** transporta `InstanceId` ni `OperationId`: por la corrección de la sección
2.5, el orquestador ya los fija con los valores reales de esta llamada sobre el contrato
**antes** de persistir, y la actividad los sigue leyendo de `Salida.DetalleEjecucion` como
hace hoy. Esa precedencia es obligatoria y va probada.

`Sha256` explícito no es redundante: la rama de dedup por MD5 de GDC corre **antes** de
`NormalizarActivity`, así que `salida.Integridad` está vacío y un contrato antiguo puede
traer el SHA vacío. En esa rama se dispone de `duplicadoPorMd5.SHA256`.

### 2.2 Comportamiento de `PersistirActivity`

Con el bloque informado, cuatro desvíos por guarda explícita:

| Paso actual | Con reutilización |
|---|---|
| Alta o actualización de `Documentos` | **Se salta entero.** El documento existe por definición. Si el lookup devuelve `null`, no se inventa: log de error y no se persiste. Además evita que `ResolveFechaExpiracionBlobAsync` recalcule la caducidad del blob del original y que `FechaActualizacion` se mueva sin que nada haya cambiado. |
| Insert en `ResultadosProcesamiento` | **No se escribe.** Hoy no la lee nadie (solo la escribe esta actividad) y duplicaría los datos del original. |
| Insert en `DocumentoEjecuciones` | Sí, con el reparto de la sección 1. |
| Plugins y validaciones | **No se escriben.** No se ha ejecutado ninguno. |
| Insert en `Auditoria` | Sí, con acción `REUTILIZACION_DUPLICADO`. |

### 2.3 Enganche en el orquestador

Dos puntos: la rama de dedup por MD5 de metadatos GDC y la rama de dedup por SHA256 tras la
normalización.

No sirve `EjecutarPasoNegocioSinResultado`, que relanza la excepción: un fallo escribiendo
la traza convertiría en error una petición ya resuelta. **La visibilidad no puede costar
disponibilidad.** Va con try/catch propio, marcando `Failed` en el timeline y devolviendo
igualmente el contrato reutilizado.

### 2.4 Idempotencia

`CallActivityAsync` se registra en el historial de Durable, así que un replay no reejecuta
el insert. El hueco sería un reintento de la actividad tras un fallo posterior al commit;
hoy estas llamadas no llevan `RetryOptions`, pero la guarda va igualmente: si ya existe fila
con este `InstanceId` y `ReutilizadaPorDuplicado = 1`, no se inserta. Si `InstanceId` viniera
vacío no hay clave natural: se inserta sin guarda y se deja constancia en el log, porque
perder la traza es peor que arriesgar un duplicado que hoy no se produce.

### 2.5 Corrección del contrato de salida

Hoy el contrato devuelto lleva el `InstanceId` y el `OperationId` de la orquestación
**histórica**, mientras que el `Seguimiento` que lo acompaña sí es el de la llamada actual.
Es incoherente y fue lo que despistó al diagnosticar el caso de DEV.

Se corrige: el contrato devuelto lleva el `InstanceId` / `OperationId` reales de esta
llamada, y se añade `DetalleEjecucion.EjecucionOriginalGuid` (nullable, omitido cuando es
null) para no perder el puntero a la original. Es aditivo; los clientes ignoran los campos
que no conocen.

### 2.6 Lo que no se toca

La rama de `DUPLICADO` sin histórico reutilizable sigue igual: persiste desde AB#100178, su
`EstadoFinal` es `DUPLICADO` y su flag se queda a 0 — no reutilizó nada.

## 3. Monitor y agregados

### 3.1 Filtro central

`EjecucionFiltro` gana `FiltroReutilizadas Reutilizadas` (enum: `Excluir` = 0 por defecto,
`Incluir`, `Solo`) y `AplicarFiltro` lo aplica con una línea. Como es el embudo común del
listado, los agregados, el histograma, la matriz y los costes, **todas las consultas
actuales conservan sus números** sin tocarlas.

Enum y no `bool` como `IncluirEstimados`: son tres estados, y con booleanos habría una
combinación imposible.

### 3.2 Listado

`EjecucionListadoItem` gana `ReutilizadaPorDuplicado` y `EjecucionOriginalId`, proyectados
en `GetPagedAsync`. En `MonitorTabla`, badge "Reutilizada"; en `MonitorFiltros`, selector
tri-estado. `EjecucionJsonModal` se deshabilita en esas filas con el texto "el contrato es
el de la ejecución #X", enlazada.

### 3.3 Detalle, en los dos sentidos

- En la reutilización: banner "Servida reutilizando la ejecución #X del \<fecha\>", más lo
  que sí es suyo (fecha, solicitante, `InstanceId`, `OperationId`, duración real).
- En la original: bloque "Reutilizada N veces" con la lista (fecha, solicitante,
  `InstanceId`), vía `GetReutilizacionesAsync(int ejecucionOriginalId)`.

Sin el segundo sentido la trazabilidad sería de ida y no de vuelta.

### 3.4 KPIs

Dos números sobre la misma ventana temporal, calculados aparte con el filtro en `Solo`:
peticiones servidas por reutilización (recuento, en `MonitorKpis`) y coste evitado (en
`CostesKpis`, sumando `CosteIAEur` de las originales por join sobre `EjecucionOriginalId`).

## 4. Consumidor externo: el procedimiento por IdActivo

`sp_ObtenerDocumentoEjecucionesPorIdActivo` devuelve todas las ejecuciones de los documentos
de un activo, con su contrato. No hay ningún llamante en el repo: lo consume un sistema
externo. Si le llegan filas con `ContratoSalidaCompletoJson`, `DatosOriginalesJson` y
`DatosFinalesJson` a NULL, se le rompe el resultset sin aviso.

Se le añade el parámetro opcional `@IncluirReutilizadas BIT = 0`, con filtro por defecto:
**el resultset de todos los llamantes actuales queda idéntico** y quien las quiera las pide.

## 5. Pruebas

### 5.1 Unitarias (`DocumentIA.Tests.Unit`)

- **Orquestador**: la rama de reutilización llama a `PersistirActivity` con el bloque
  informado; si esa llamada falla, la respuesta sigue siendo el contrato reutilizado y el
  timeline marca `Persistir` como `Failed`; la rama sin histórico conserva `DUPLICADO` y
  flag 0; el contrato devuelto lleva el `InstanceId` de la llamada y el
  `EjecucionOriginalGuid` de la original.
- **`PersistirActivity`**: con el bloque informado no toca `Documentos` ni
  `ResultadosProcesamiento`; inserta con el reparto de la sección 1; contrato y costes a
  NULL; la guarda por `InstanceId` no inserta dos veces; documento inexistente ⇒ no inserta
  y loguea error.
- **Repositorio**: los tres estados del filtro; con una reutilización en la ventana,
  Ok/Revisión/Error y los importes de coste no se mueven; los KPIs nuevos sí la cuentan;
  `GetPagedAsync` proyecta los campos nuevos.
- **Invariante**: una reutilización nunca es candidata a reutilizarse.

### 5.2 Admin (`DocumentIA.Tests.Admin`)

Siguiendo el patrón de los componentes del Monitor ya cubiertos: badge, filtro tri-estado,
banner del detalle, bloque "reutilizada N veces" y modal JSON deshabilitado.

### 5.3 Validación contra DEV real

Runner hermano `run-validacion-dedup.ps1` + `cases-validacion/dedup-cases.json`,
reutilizando `lib/postdeploy-db.ps1` y `lib/postdeploy-estado.ps1`. No se generaliza el
runner de markdown: es más cambio que beneficio.

| Caso | Comprueba |
|---|---|
| DUP-01 | Siembra: primera ingesta del documento de control. |
| DUP-02 | Reenvío ⇒ 200 reutilizado, fila marcada, FK correcta, contrato y costes NULL. |
| DUP-03 | Reenvío con `ForceReprocess` ⇒ ejecución real, flag 0. |
| DUP-04 | Los agregados del Monitor no se mueven entre DUP-01 y DUP-02. |
| DUP-05 | Idempotencia: un solo registro por `InstanceId`. |

Con limpieza al final y entrada en `coverage/functional-matrix.json`.

Nota operativa: antes de validar hay que borrar la fila 8061 de DEV, insertada a mano el
2026-09-09 para reproducir un duplicado sin resumen
(`DELETE FROM DocumentoEjecuciones WHERE Id = 8061;`).

## 6. Despliegue y riesgos

**Orden**: migración primero (aditiva; el código anterior la ignora), código después.
Rollback por `Down`.

En DEV y PRE basta el script idempotente de EF. En **PRO no**: la migración recrea el índice
cubriente sin `ONLINE = ON` y bloquearía la tabla durante el build. Para PRO se usa
`scripts/database/indice-monitor-reutilizacion-pro.sql`, que hace lo mismo con
`DROP_EXISTING = ON, ONLINE = ON`, sin transacción global, idempotente por bloque, y que
registra la migración en `__EFMigrationsHistory` para que EF no la repita. Mismo patrón que
el `indice-monitor-pro.sql` de AB#100185.

**Riesgos asumidos**: el número de filas crece con cada reenvío duplicado, aunque son filas
ligeras sin JSON y el crecimiento está acotado por el tráfico real; hoy no se puede
cuantificar porque precisamente no se registra, y la primera semana tras el despliegue dará
el número. El coste evitado es una estimación por join: si la original no tenía coste
medido, no suma.

## Criterio de éxito

Desde el Monitor se puede responder: cuántas peticiones se sirvieron por reutilización en un
período, sobre qué documentos, quién las lanzó y cuánto coste de IA se ahorró — sin que
ninguna cifra de calidad o coste existente cambie de valor.
