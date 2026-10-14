# Reutilización por duplicado con clave de intención

- **Work item**: AB#100930 (tasks AB#100931 a AB#100941)
- **Fecha**: 2026-10-13
- **Rama**: `feature/100930-dedup-clave-intencion`
- **Estado**: diseño aprobado el 2026-10-13, pendiente de plan de implementación

## Problema

La reutilización por duplicado existe para que un documento ya procesado con el mismo
objetivo devuelva el resultado guardado sin volver a pagar IA. Hoy la selección de la
ejecución histórica solo compara dos dimensiones de la petición, `ClassificationOnly` y
`NivelClasificacion`, y desde el 2026-09-02 (AB#100177) además recurre a la última ejecución
con contrato cuando ninguna coincide. Nunca ha comparado la restricción de tipologías, el
`ExpectedType`, los modelos ni las páginas procesadas.

Consecuencia: una petición puede recibir el resultado de otra con un objetivo distinto.
Medido en PRO desde el 2026-09-20 (desde que la fila reutilizada enlaza con su original):

| Reutilizaciones | Ejecuciones |
|---|---|
| Con el mismo modo y nivel que la original | 3.735 |
| Relajadas (modo o nivel distintos) | 145 |

De las 145 relajadas, 62 pidieron el pipeline completo con extracción y recibieron una
histórica de solo clasificación (44 solicitantes distintos); 78 pidieron solo clasificación
con nivel TDN1_TDN2 y recibieron una histórica sin nivel informado; 5 pidieron solo
clasificación y recibieron una completa. El cruce entre una restricción de tipologías y el
universo completo no se ha podido medir (el JSON del contrato no está indexado).

El fallback del 2026-09-02 se introdujo porque el camino "duplicado sin histórica
compatible" devolvía `DUPLICADO` con el contrato vacío (INC1338832: peticiones por GDC sin
resumen). Se relajó la coincidencia en lugar de corregir ese camino. Además, la fila de
reutilización se graba con el modo y nivel de la petición nueva, así que el desajuste no se
ve en el Monitor.

## Objetivo

Una ejecución histórica se reutiliza solo si persigue exactamente el mismo objetivo que la
petición nueva y cubre al menos las páginas pedidas. Cuando no existe, el documento se
procesa y se persiste como una ejecución más. Si la histórica sirve pero no trae el resumen o
el prompt pedidos, se ejecuta solo ese paso sobre su markdown. La regla es auditable desde el
Monitor y barata con el volumen creciendo.

## Decisiones tomadas en sesión (2026-10-13)

- Dimensiones de la clave: modo, nivel, universo de tipologías (restricción o completo),
  `ExpectedType` y modelos efectivos. Fuera: prompt, resumen, AssetResolver, integración y GDC.
- Identidad exacta: una histórica "más completa" no sirve a una petición "menor" (salvo en
  páginas, donde cubrir más sí vale).
- Prompt y resumen se completan sobre la histórica cuando faltan.
- Enfoque A: clave persistida con índice y consulta de una fila, derivación al vuelo para
  filas antiguas, backfill del histórico e interruptor de vuelta atrás.

## Fuera de alcance

- Cambiar cómo se detecta el duplicado (SHA256 del contenido y MD5 de GDC siguen igual).
- Reutilizar entre documentos distintos o por similitud.
- Caducidad temporal de las reutilizaciones.
- Reclasificar o corregir las 145 reutilizaciones relajadas ya servidas.

## Invariante transversal

Ninguna cifra existente del Monitor cambia de valor por este diseño. Las columnas nuevas son
aditivas; las reutilizaciones siguen marcadas con `ReutilizadaPorDuplicado` y enlazadas a su
original; el coste de una reutilización sigue siendo cero salvo el del prompt o resumen que
se ejecute para completarla.

## 1. Clave de intención

Se calcula en el orquestador antes de la verificación de duplicados, a partir de la petición
y de la configuración activa, y viaja en `DetalleEjecucion` para que `PersistirActivity` la
grabe. Componentes y normalización:

| Componente | Valor normalizado |
|---|---|
| Modo | `solo-clasificacion` si `ClassificationOnly`, si no `completo` |
| Nivel | `Classification.NivelClasificacion` o el default global cuando viene vacío. Una fila antigua con nivel vacío equivale al default |
| Universo | `*` sin restricción; con restricción, los códigos efectivos tras `RestriccionTipologiasValidator` (publicados, en mayúsculas, sin duplicados, ordenados), unidos por `,` |
| ExpectedType | el valor normalizado, o vacío |
| Modelo de clasificación | el que el registro de modelos resolvería ahora para `Classification.Provider` y `Classification.Model`: nombre de deployment GPT, id de clasificador DI o marcador del híbrido. Con `ExpectedType` informado, `expectedtype-input`, igual que la fila actual |

El modelo de extracción no forma parte de la clave de igualdad porque depende de la
tipología resuelta, que no se conoce antes de clasificar. Se comprueba sobre la candidata (ver
sección 2). Las páginas tampoco (sección 10).

Representación: `ClaveIntencion`, hash SHA-256 de la concatenación canónica de los
componentes, truncado a 32 caracteres hexadecimales; y `ClaveIntencionJson`, los componentes
con sus nombres, para auditoría y Monitor. El cálculo vive en Core (`ClaveIntencionBuilder`),
sin dependencias de Functions, para poder usarlo también en el backfill.

## 2. Selección de la histórica

Nuevo método de repositorio `GetUltimaReutilizableAsync(documentoId, claveIntencion)` que
devuelve la última ejecución del documento con `ContratoSalidaCompletoJson` no vacío y la
misma clave. Lo sirve el índice `IX_DocumentoEjecuciones_Documento_Clave` sobre
`(DocumentoId, ClaveIntencion, FechaEjecucion DESC)` con `ContratoSalidaCompletoJson`,
`ActivityTimelineJson`, `EjecucionGuid`, `Tipologia`, `MarkdownPaginas` y `MarkdownCompleto`
incluidos. Una consulta, una fila; no se cargan plugins ni validaciones.

Segunda fase sobre la candidata, en memoria:

1. **Modelo de extracción** (solo modo completo): el `ModelKeyEfectivo` persistido en
   `DetalleEjecucion.Extraccion` debe coincidir con el modelo que la tipología de la candidata
   usaría hoy (configuración publicada de la tipología). Si la candidata no tiene tipología
   resuelta, la tipología ya no está publicada o el modelo difiere, no se reutiliza.
2. **Páginas** (sección 10).

Si la candidata más reciente no supera la segunda fase, se consulta la siguiente con la misma
clave (paginación de una en una, acotada a 5 intentos) antes de rendirse.

Filas anteriores al cambio: si el documento no tiene ninguna fila con clave, se cargan solo
sus ejecuciones con contrato y sin clave, se deriva la clave de cada una desde el contrato
(misma función que el backfill) y se graba en la fila para no repetirlo. A partir de ahí se
aplica la misma selección.

Desaparece el fallback a "la última con contrato". `ObtenerUltimaEjecucionDuplicadoActivity`
pasa a recibir la clave calculada y la necesidad de páginas, y devuelve, además del contrato,
el motivo cuando no reutiliza.

## 3. Camino sin histórica compatible

Hoy: `Resultado.Estado = "DUPLICADO"`, contrato vacío, persistido (AB#100178). Pasa a ser: el
orquestador continúa por el flujo normal como con un documento nuevo (subida a blob,
normalización, clasificación, extracción, prompt, persistencia). La ejecución queda como una
más del mismo documento, con su clave. El estado `DUPLICADO` sin reutilización desaparece de
la salida. `DetalleEjecucion.MotivoNoReutilizacion` informa por qué no se reutilizó:
`sin-historica`, `clave-distinta`, `modelo-extraccion-distinto`, `paginas-insuficientes`,
`contrato-invalido`.

Aplica a las dos ramas de duplicado: por SHA256 (paso 2) y por MD5 de GDC (pre-GDC). En la
rama GDC el documento aún no se ha descargado; "continuar" significa descargarlo y seguir.

## 4. Reutilizar y completar

Con candidata válida se devuelve su clasificación y extracción como hoy. Después se compara lo
que la petición pide con lo que la histórica trae:

- Resumen: la petición lo pide (resumen por defecto forzado, o nivel y tipología que lo
  exigen) y el contrato histórico no tiene `DatosExtraidos.Resumen`.
- Prompt libre: la petición trae `Instrucciones.Prompt` y el histórico no tiene
  `DetalleEjecucion.Prompt` con la misma plantilla y modelo.

Si falta algo, se obtiene el markdown de la histórica por el resolutor de markdown (caché de
ejecución o BD), se comprueba que cubre las páginas pedidas (sección 10) y se ejecuta solo
`PromptActivity`. El resultado se añade al contrato devuelto; la fila de reutilización persiste
el prompt y su coste, que es el único coste de IA de esa ejecución
(`DetalleEjecucion.Costes.CostePromptEur`; `ReutilizadaPorDuplicado` sigue a `true`). Si la
histórica no tiene markdown guardado o no cubre las páginas, no se completa: se devuelve la
reutilización sin el prompt y `MotivoNoReutilizacion` queda vacío pero
`DetalleEjecucion.Prompt` informa `Omitido = "markdown-insuficiente"`. No se reprocesa el
documento por un prompt.

## 5. Persistencia, migración y backfill

Columnas nuevas en `DocumentoEjecuciones`: `ClaveIntencion nvarchar(32) NULL`,
`ClaveIntencionJson nvarchar(max) NULL`, `MotivoReutilizacion nvarchar(40) NULL` (en filas
reutilizadas: `clave-igual`; en filas no reutilizadas tras duplicado: el motivo de la sección
3; vacío en el resto). Índice `IX_DocumentoEjecuciones_Documento_Clave` descrito en la
sección 2.

Migración EF Core para columnas e índice. En PRO el índice se crea aparte con script `ONLINE`
(`scripts/database/indice-documento-clave-pro.sql`) y se registra en `__EFMigrationsHistory`,
como se hizo con `IX_Documentos_MD5`; el pipeline de migraciones debe quedar en `Pendientes: 0`.

Las filas de reutilización llevan la clave de la petición, que por definición es la de su
original; conservan el modo y nivel reales de la petición, como hoy.

Backfill `scripts/database/backfill-clave-intencion.ps1`, por lotes y reanudable por
`-DesdeId`, mismo patrón que `backfill-markdown-cobertura.ps1`: para cada fila con contrato y
sin clave, deriva los componentes del JSON (`DetalleEjecucion.ClassificationOnly`,
`NivelClasificacion`, `Clasificacion.RestriccionTipologias.Codigos`, `Clasificacion.Modelo`,
`Identificacion` para `ExpectedType`) y graba clave y JSON. Se lanza en el mismo run de
Migrations-BD que la migración (parámetro `backfillClaveIntencion`); la derivación al vuelo de
la sección 2 queda como red de seguridad.

> Nota 2026-10-14: cambio de decisión. El diseño del 2026-10-13 lanzaba el backfill un día después,
> tras validar el despliegue. Se lanza en el mismo run de Migrations-BD que la migración, activando el
> parámetro booleano `backfillClaveIntencion` (defecto `false`): tras aplicar las migraciones, y
> en el mismo pool privado, el pipeline ejecuta `-SelfTest`, `-WhatIf` y el backfill real por
> lotes, y aborta en el primer paso que falle. El script sigue siendo reanudable (`-DesdeId`).

## 6. Interruptor y vuelta atrás

App setting `Dedup__ModoEstricto` (`true` por defecto en el despliegue). Con `false`, la
selección vuelve a la regla actual (modo y nivel con fallback) y el camino sin histórica vuelve
a devolver `DUPLICADO` vacío; la clave se calcula y persiste en ambos modos, así que
desactivar no pierde datos. El interruptor se retira en la release siguiente a la validación
en PRO. Vuelta atrás de BD: las columnas son nullable y el índice es aditivo; no hace falta
deshacerlos para desplegar la versión anterior.

## 7. API y Monitor

Contrato de salida: `DetalleEjecucion` añade `ClaveIntencion`, `ClaveIntencionComponentes`
(objeto), `MotivoReutilizacion` y `MotivoNoReutilizacion`. Ningún campo existente cambia de
nombre, tipo ni semántica, salvo que `Resultado.Estado = "DUPLICADO"` deja de emitirse.
`CONTRATO_API_HTTP.md` y el troubleshooting se actualizan.

Monitor: el detalle de ejecución muestra los componentes de la clave y el motivo; el filtro de
reutilizaciones admite motivo. Los agregados no cambian.

## 8. Pruebas

Unitarias (Core y Functions):

- `ClaveIntencionBuilder`: orden y mayúsculas de códigos, nivel vacío igual a default,
  `ExpectedType` fija `expectedtype-input`, modo completo frente a solo clasificación,
  resolución del modelo de clasificación según registro.
- Derivación desde contratos antiguos: con y sin restricción, con nivel vacío, con
  `expectedtype-input`.
- `ObtenerUltimaEjecucionDuplicadoActivity`: misma clave reutiliza; clave distinta no; modelo
  de extracción distinto no; páginas insuficientes no; candidata inválida pasa a la siguiente;
  interruptor en `false` reproduce el comportamiento actual.
- Reutilizar y completar: resumen ausente se ejecuta sobre markdown de BD; markdown que no
  cubre no ejecuta y marca el motivo; prompt igual no se repite.
- Orquestador: sin histórica compatible continúa el flujo y persiste con motivo, en las dos
  ramas de duplicado.

E2E post-despliegue (perfil full, casos nuevos `DUP-06` a `DUP-10`): restricción y luego
universo completo no reutiliza; solo clasificación y luego completo no reutiliza; completo y
luego solo clasificación no reutiliza; misma clave reutiliza y completa el prompt ad hoc; misma
clave con más páginas pedidas reprocesa.

> Nota 2026-10-14: desvío de DUP-09. El diseño del 2026-10-13 decía «misma clave reutiliza y
> completa el resumen». DUP-09 demuestra la complección por prompt ad hoc: con
> `classificationProvider: gpt` la siembra siempre trae resumen combinado (el clasificador GPT
> devuelve `ResumenCombinado`), así que la rama «completar el resumen» no es alcanzable en E2E y
> queda cubierta solo por las pruebas unitarias. DUP-09 siembra solo clasificación con un prompt A
> y la pasada, con la misma clave y un prompt B (hash de plantilla distinto), ejecuta ese prompt
> sobre el markdown guardado; es la misma invariante de la sección 4 (reutilizar y completar) por
> la otra rama.

## 9. Despliegue, riesgos e impacto

- Coste: reprocesar los documentos con varias intenciones (10 en 90 días en PRO) y ejecutar
  prompts que hoy se omiten; ambos son el objetivo.
- Riesgo de integradores que dependan de `Estado = "DUPLICADO"` vacío: antes del despliegue se
  cuantifica en PRO (filas con ese estado por `SubmittedBy` en 90 días) y se avisa a los
  afectados en el contrato documentado.
- Riesgo de rendimiento en S0: la consulta por índice sustituye a una carga completa de
  ejecuciones por documento; la derivación al vuelo es acotada por documento y desaparece con
  el backfill.
- ADR: cambia la semántica de un contrato externo; se propondrá `ADR-003` al cerrar esta spec
  y antes de desplegar.
- Documentación: `CONTRATO_API_HTTP.md`, troubleshooting, `MODELO_DATOS` (columnas e índice),
  runbook de release (script de índice en PRO y backfill en el mismo run de Migrations-BD que la
  migración, con el parámetro `backfillClaveIntencion`; ver la nota de la sección 5, 2026-10-14).

## 10. Páginas

No forma parte de la clave de igualdad; es una comprobación de cobertura sobre la candidata.
La necesidad de páginas de la petición se calcula como ya lo hace el resolutor de markdown
(`NecesidadMarkdown`): en solo clasificación, `MaxPagesForClassificationOnly` o el default;
en modo completo o con `ForzarProcesadoSinLimitePaginas`, documento completo. La candidata
cubre si `MarkdownCompleto` es verdadero o `MarkdownPaginas` es mayor o igual que las páginas
mínimas pedidas; para la clasificación, además, `PaginasIncluidas` mayor o igual que las
pedidas. Se reutiliza la regla de cobertura existente del resolutor; no se escribe otra.

Si la petición pide más páginas de las que la candidata procesó, no se reutiliza
(`paginas-insuficientes`) y el documento se procesa entero con las páginas nuevas. Una
petición con menos páginas que la histórica recibe la histórica. Entre varias candidatas con
la misma clave se elige la más reciente que cubra.

## Criterio de éxito

En PRO, tras el despliegue y el backfill: cero reutilizaciones cuya clave difiera de la de su
original; las tres combinaciones relajadas medidas el 2026-10-13 dejan de aparecer; los
reprocesos nuevos por clave distinta quedan acotados a los documentos con varias intenciones;
la latencia de `ObtenerUltimaEjecucionDuplicadoActivity` no sube respecto a la actual
(p50 0,4 s en PRO).
