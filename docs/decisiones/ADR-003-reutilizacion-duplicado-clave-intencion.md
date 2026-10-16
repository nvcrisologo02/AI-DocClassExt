# ADR-003: Reutilización por duplicado con clave de intención
Fecha: 2026-10-14 · Estado: aceptada · Work Item: AB#100930 · Session ID: retroactivo

## Contexto y problema

La reutilización por duplicado existe para que un documento ya procesado con el mismo objetivo devuelva el resultado guardado sin volver a pagar IA. Hasta ahora la selección de la ejecución histórica comparaba solo dos dimensiones de la petición, `ClassificationOnly` y `NivelClasificacion`, y desde el 2026-09-02 (AB#100177) recurría además a la última ejecución con contrato cuando ninguna coincidía. Nunca comparó la restricción de tipologías, el `ExpectedType`, los modelos ni las páginas procesadas. Una petición podía recibir el resultado de otra con un objetivo distinto.

Medido en PRO desde el 2026-09-20 (desde que la fila reutilizada enlaza con su original): 3.735 reutilizaciones con el mismo modo y nivel que la original y 145 relajadas (modo o nivel distintos). De las 145, 62 pidieron el pipeline completo con extracción y recibieron una histórica de solo clasificación (44 solicitantes distintos); 78 pidieron solo clasificación con nivel TDN1_TDN2 y recibieron una histórica sin nivel informado; 5 pidieron solo clasificación y recibieron una completa. El cruce entre una restricción de tipologías y el universo completo no se pudo medir (el JSON del contrato no está indexado).

El fallback del 2026-09-02 se introdujo porque el camino «duplicado sin histórica compatible» devolvía `DUPLICADO` con el contrato vacío (INC1338832: peticiones por GDC sin resumen). Se relajó la coincidencia en lugar de corregir ese camino. Además, la fila de reutilización se grababa con el modo y nivel de la petición nueva, así que el desajuste no se veía en el Monitor.

Hay que decidir qué significa «misma petición» para reutilizar, qué se hace cuando no existe una histórica equivalente y cómo se hace barato y auditable con el volumen creciendo. La decisión cambia la semántica de un contrato externo: `Resultado.Estado = "DUPLICADO"` deja de emitirse.

## Opciones consideradas

- A. Clave de intención persistida: modo, nivel, universo de tipologías, `ExpectedType` y modelo de clasificación efectivo, con índice y consulta de una fila, derivación al vuelo para filas antiguas, backfill del histórico e interruptor de vuelta atrás.
- B. Mantener la regla anterior (modo y nivel, con fallback a la última ejecución con contrato) y corregir solo el camino `DUPLICADO` vacío.
- C. Ampliar la comparación a más dimensiones sobre la carga completa de ejecuciones del documento, sin clave persistida.

La spec solo describe el enfoque A y, de forma explícita, el problema de la regla anterior (B) y la carga completa de ejecuciones por documento (C, sección 9). Otras variantes que se evaluaran en la sesión de diseño del 2026-10-13 no constan en la spec: no verificado, completar si existen.

## Decisión

Se adopta A. Una ejecución histórica se reutiliza solo si persigue exactamente la misma intención que la petición nueva (identidad exacta: una histórica «más completa» no sirve a una petición «menor», salvo en páginas, donde cubrir más sí vale) y cubre al menos las páginas pedidas. Cuando no existe, el documento se procesa y se persiste como una ejecución más, con `MotivoNoReutilizacion` (`sin-historica`, `clave-distinta`, `modelo-extraccion-distinto`, `paginas-insuficientes`, `contrato-invalido`). Si la histórica sirve pero no trae el resumen o el prompt pedidos, se ejecuta solo ese paso sobre su markdown. Fuera de la clave quedan el prompt, el resumen, AssetResolver, la integración y GDC. El modelo de extracción y las páginas se comprueban sobre la candidata, no en la clave.

## Motivos

- La identidad exacta elimina de raíz el cruce de objetivos medido en PRO (145 reutilizaciones relajadas de 3.880) y deja el criterio de éxito comprobable: cero reutilizaciones cuya clave difiera de la de su original.
- La clave persistida, con el índice `IX_DocumentoEjecuciones_Documento_Clave`, sustituye una carga completa de ejecuciones por documento (con contrato `nvarchar(max)`) por una consulta de una fila; es relevante en el S0 de PRO. La derivación al vuelo de filas antiguas queda acotada por documento y desaparece con el backfill.
- Procesar el duplicado sin histórica compatible corrige el origen de INC1338832 (el camino `DUPLICADO` vacío) en lugar de enmascararlo con un fallback que servía resultados de otro objetivo.
- Dejar el prompt y el resumen fuera de la clave y completarlos sobre la histórica evita reprocesar un documento entero por un paso barato, y mantiene el coste de una reutilización en cero salvo el del prompt o resumen que se ejecute.
- El interruptor `Dedup__ModoEstricto` permite volver a la regla anterior sin deshacer datos: la clave se calcula y persiste en ambos modos, las columnas son nullable y el índice es aditivo.
- La auditoría queda en el Monitor: clave, componentes y motivo en el detalle y filtro por motivo, sin cambiar ningún agregado existente.

## Consecuencias

- Cambio de contrato externo: `DUPLICADO` deja de emitirse en modo estricto y un duplicado sin histórica compatible se procesa y devuelve el estado del procesamiento. Los campos nuevos de `detalleEjecucion` (`claveIntencion`, `claveIntencionComponentes`, `motivoReutilizacion`, `motivoNoReutilizacion`, `prompt.plantillaHash`, `prompt.omitido`) son aditivos. Antes del despliegue se cuantifica en PRO el uso de `DUPLICADO` vacío por `SubmittedBy` en 90 días y se avisa a los afectados.
- Coste: se reprocesan los documentos con varias intenciones (10 en 90 días en PRO) y se ejecutan prompts que antes se omitían; ambos son el objetivo. Una reutilización que completa un prompt o resumen lleva contrato y coste propios (`CostePromptEur`), con `ReutilizadaPorDuplicado` a `true`.
- BD: tres columnas nuevas en `DocumentoEjecuciones` (`ClaveIntencion`, `ClaveIntencionJson`, `MotivoReutilizacion`) e índice. En PRO el índice se crea con `scripts/database/indice-documento-clave-pro.sql` (ONLINE) y se registra en `__EFMigrationsHistory`, para dejar el pipeline de migraciones en `Pendientes: 0`.
- El backfill del histórico (`scripts/database/backfill-clave-intencion.ps1`) se lanza en el mismo run de Migrations-BD que la migración, con el parámetro `backfillClaveIntencion` (decisión del 2026-10-14; la spec original lo situaba un día después del despliegue). Las reutilizaciones anteriores al cambio y las filas `DUPLICADO` quedan sin clave.
- Riesgo de rendimiento en el S0: la consulta por índice reemplaza la carga completa; el criterio de éxito exige que la latencia de `ObtenerUltimaEjecucionDuplicadoActivity` no suba respecto a la actual (p50 0,4 s en PRO).
- Riesgo de integradores que dependan de `Estado = "DUPLICADO"` vacío: mitigado con el aviso previo y el interruptor.
- Aclaración (AB#100956, 2026-10-14, no reabre la decisión): una histórica en estado de fallo (`ERROR`, `PENDIENTE_REINTENTO`, `SIN_CONTENIDO_DOCUMENTO` y sus variantes históricas; lista `EstadoEjecucion.NoReutilizables`) nunca es candidata, con la misma clave o sin ella; el reenvío reprocesa y la fila nueva lleva `motivoNoReutilizacion = historica-fallida`. Aplica también con `Dedup__ModoEstricto=false`, donde es la única situación en la que el modo legado reprocesa en vez de devolver `DUPLICADO` vacío. Origen: Bug AB#100955 (en PRO, 129 de 173 `ERROR` en 7 días eran reutilizaciones de una original fallida).
- Modelo de extracción (revisión final de rama): hasta esta versión solo Content Understanding persistía `ModelKeyEfectivo`; las ejecuciones anteriores de tipologías con extracción GPT directo o Document Intelligence no lo llevan y se reprocesan una vez por documento e intención (modo completo, `modelo-extraccion-distinto`), tras lo cual la fila nueva ya lo lleva. Desde esta versión los tres proveedores lo persisten.
- Orquestaciones en vuelo: la release cambia la secuencia de actividades del orquestador (`CalcularClaveIntencionActivity` antes de la verificación de duplicados y nuevo tipo de salida de `ObtenerUltimaEjecucionDuplicadoActivity`), por lo que se drena y no se versiona (precedente AB#100176): paso obligatorio "Drenar orquestaciones en vuelo" del runbook de release (2.2.6 en PRE, 4.15 en PRO).
- El interruptor se retira en la release siguiente a la validación en PRO. Vuelta atrás de BD: no hace falta deshacer columnas ni índice para desplegar la versión anterior.
- Documentación afectada: `CONTRATO_API_HTTP.md`, `MANUAL_DEDUPLICACION.md`, troubleshooting, diseño técnico, manual de configuración y runbook de release.
- Si la validación del modo estricto en PRO desaconseja la regla, se registra en otro ADR que lo sustituya y este pasa a "sustituida por ADR-MMM".

## Alternativas descartadas y por qué

Las alternativas B y C son una reconstrucción a partir de la spec, no verificada (ver el aviso de «Opciones consideradas»).

- B, regla anterior con fallback: es la causa del problema medido (145 reutilizaciones con objetivo distinto) y oculta el desajuste en el Monitor.
- C, comparar más dimensiones sin clave persistida: obliga a cargar y deserializar los contratos de todas las ejecuciones del documento en la ruta de la petición, justo lo que el S0 de PRO no soporta con caché fría.
- Incluir el prompt y el resumen en la clave: obligaría a reprocesar el documento entero por un paso que se puede ejecutar sobre el markdown de la histórica.
- Incluir las páginas y el modelo de extracción en la clave: el modelo de extracción depende de la tipología resuelta, que no se conoce antes de clasificar; las páginas son una cobertura (más sirve a menos), no una igualdad. Se comprueban sobre la candidata.
- Reclasificar o corregir las 145 reutilizaciones ya servidas, reutilizar entre documentos distintos o por similitud, y añadir caducidad temporal: fuera de alcance de esta decisión.
