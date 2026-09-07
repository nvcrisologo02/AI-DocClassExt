# Catálogo de tarifas de servicios de IA (AB#100234)

Alta del catálogo que traduce el consumo de IA de cada ejecución a euros.

## Qué hace

`01-seed-tarifas-ia.sql` inserta una fila en `ModeloConfigs` con `Tipo=4` (Tarifas) y `Key='tarifas.ia'`, cuyo JSON lleva una línea por modelo físico y fecha de vigencia. No hay cambio de esquema: reutiliza la columna de configuración que ya existe.

El script hace copia de seguridad de la tabla antes de tocar nada, es idempotente y no pisa un catálogo ya existente.

## Antes de ejecutarlo

**Los importes vienen a cero y hay que sustituirlos por los precios reales.** No es un descuido: un precio inventado da una cifra que parece buena y no lo es. Con el catálogo vacío o incompleto el sistema funciona igual, registra tokens y páginas, deja el coste a nulo y marca el agregado como incompleto.

Los precios dependen de la región, del tipo de despliegue y de los acuerdos de la suscripción. Fuentes para obtenerlos:

- Portal de Azure, Cost Management, análisis de costes agrupando por medidor sobre el grupo de recursos de producción.
- Páginas de precios de Azure OpenAI, Content Understanding y Document Intelligence.
- El informe de costes en `docs/auxiliares/temps/2026-07-21/`, que ya tiene el mapeo de medidor a proceso y sirve de contraste.

El paso 0 del script lista los modelos que el sistema tiene configurados. El catálogo debe cubrirlos todos para que el coste salga completo.

## Orden respecto al despliegue

Da igual. El código tolera la ausencia del catálogo, y el catálogo sin código no molesta a nadie. Lo natural es desplegar primero y cargar el catálogo después, comprobando con una ejecución de prueba.

Tras insertar o modificar el catálogo hay que esperar hasta cinco minutos: el registro se cachea en memoria.

## Cómo se mantiene

**Cambiar un precio es añadir una línea nueva con su fecha de vigencia, nunca editar la anterior.** El sistema aplica a cada ejecución la línea de mayor vigencia que no supere su fecha, así que las ejecuciones antiguas siguen cuadrando con lo que se facturó entonces. Editar una línea reescribiría el pasado.

La fila se puede editar desde la pantalla de modelos de Admin, en la sección Tarifas, como cualquier otra fila de configuración.

## Claves de modelo

La tarifa se resuelve por el nombre del modelo físico que consta en cada consumo, no por la fila de configuración que lo invocó. Varias filas de `ModeloConfigs` comparten el mismo despliegue, así que ligar el precio a la fila lo duplicaría y lo desalinearía.

| Origen | Clave que hay que usar |
| --- | --- |
| Azure OpenAI | el nombre del despliegue, por ejemplo `gpt-4o-mini` o `gpt-5-mini` |
| Layout a Markdown | `prebuilt-layout` |
| Clasificador de Document Intelligence | su identificador de clasificador |
| Content Understanding | su identificador de analizador |
| Modelo generativo interno de Content Understanding | el que declara el propio servicio, por ejemplo `gpt-4.1` y `text-embedding-3-large` |

Content Understanding factura por su cuenta las páginas y la contextualización, pero los tokens del modelo generativo se cargan al despliegue de Foundry conectado y el servicio los declara aparte. Por eso llevan línea propia.

## Campos de precio

Todos opcionales; se aplica solo lo informado.

| Campo | Se aplica a |
| --- | --- |
| `EurEntradaPor1M` | tokens de entrada a precio pleno |
| `EurEntradaCachePor1M` | tokens de entrada servidos desde caché |
| `EurSalidaPor1M` | tokens de salida, razonamiento incluido |
| `EurContextualizacionPor1M` | tokens de contextualización de Content Understanding |
| `EurPorPagina` | servicios que facturan por página |

Los tokens cacheados son un subconjunto de los de entrada, no un sumando. El sistema resta la parte cacheada y le aplica su precio reducido. Informar solo el precio pleno sobrevalora el coste en clasificación, donde la tasa de acierto de caché es alta por el tamaño del prompt fijo.

## Comprobación

Tras cargar el catálogo, lanzar una petición con `IncluirCostes` en verdadero y revisar que el bloque de costes llega con importes distintos de cero y con las tarifas marcadas como completas. Si alguna sale incompleta, el propio bloque dice qué modelos faltan por tarifar.
