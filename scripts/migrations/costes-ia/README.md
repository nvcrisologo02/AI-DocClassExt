# Catálogo de tarifas de servicios de IA (AB#100234)

Alta del catálogo que traduce el consumo de IA de cada ejecución a euros.

## Qué hace

`01-seed-tarifas-ia.sql` inserta una fila en `ModeloConfigs` con `Tipo=4` (Tarifas) y `Key='tarifas.ia'`, cuyo JSON lleva una línea por modelo físico y fecha de vigencia. No hay cambio de esquema: reutiliza la columna de configuración que ya existe.

El script hace copia de seguridad de la tabla antes de tocar nada, es idempotente y no pisa un catálogo ya existente.

## Antes de ejecutarlo

El catálogo está **completo y validado contra la factura**. Los precios no son de lista: son los efectivos, derivados de dividir el coste facturado entre la cantidad consumida, medidor a medidor, sobre el grupo de recursos de producción.

Aplicando el catálogo a las cantidades reales de un periodo se reconstruye la factura con una desviación del 0,01 por ciento. Ese contraste confirma también que la suscripción no tiene descuento sobre tarifa pública.

No queda nada pendiente. El paso 0 del script sigue estando para verificar que los identificadores que usa la configuración coinciden con los que aquí se tarifan.

### Dos trampas que costaron caro averiguar

**Hay dos cuentas de IA, en regiones distintas.** El grupo de recursos tiene `srbaisrv-westeurope` en West Europe y `upe48-mm2avmdm-swedencentral` en Sweden Central. El despliegue que usa el pipeline, llamado `gpt-4o-mini`, vive en la segunda y sirve en realidad el modelo gpt-4.1-mini con tipo Standard, es decir precio regional de Sweden Central. Tomarlo de West Europe daba un veinte por ciento de más.

**El nombre del medidor no es el que parece.** Para gpt-5-mini existe un medidor `5 mini pp ... Dz` que corresponde a otro modo y cuesta casi el doble. El que factura de verdad es `GPT 5 Mini Inpt/outpt/cchd Inpt DZone`.

La lección general: no basta con buscar el modelo en la lista de precios. Hay que mirar qué medidor aparece en la factura y derivar de ahí el precio unitario.

### Cómo reproducir el cálculo

Precio efectivo por medidor, que es la fuente que usa este catálogo:

```bash
az rest --method post   --url "https://management.azure.com/subscriptions/{sub}/resourceGroups/SRBRGDOCSAIPROD/providers/Microsoft.CostManagement/query?api-version=2023-03-01"   --body @query.json
```

Con un cuerpo que agrupe por la dimensión `Meter` y agregue `Cost` y `UsageQuantity`. El precio unitario es el cociente de ambos. Esa API tiene un límite de peticiones agresivo, así que conviene espaciar los intentos.

Los precios de lista, si hacen falta para contrastar, salen de la API pública. El proxy corporativo rompe la verificación de revocación del certificado, así que hace falta `--ssl-no-revoke`:

```bash
curl -sS --ssl-no-revoke --get https://prices.azure.com/api/retail/prices   --data-urlencode "currencyCode=EUR"   --data-urlencode "$filter=productName eq 'Azure OpenAI' and armRegionName eq 'swedencentral'"
```

Y los tipos de despliegue:

```bash
az cognitiveservices account deployment list   --name upe48-mm2avmdm-swedencentral --resource-group SRBRGDOCSAIPROD   --subscription "Producción Central" -o table
```

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
| Clasificador de Document Intelligence | su identificador, hoy `DocumentAICC_v0` y `DocumentAICC_v1` |
| Páginas de Content Understanding | el medidor, no el analizador: `cu.documentPagesMinimal`, `cu.documentPagesBasic` o `cu.documentPagesStandard` |
| Contextualización de Content Understanding | el identificador del analizador, hoy `CU_NS_1.5_0` y `CU_NS_1.6_0_GGAA` |
| Modelo generativo interno de Content Understanding | el que declara el propio servicio, hoy `gpt-4.1` y `text-embedding-3-large` |

Content Understanding factura por su cuenta las páginas y la contextualización, pero los tokens del modelo generativo se cargan al despliegue de Foundry conectado y el servicio los declara aparte. Por eso llevan línea propia.

**Las páginas se tarifan por medidor, no por analizador.** El precio depende del procesamiento que el servicio haya aplicado, y la diferencia es enorme: un documento digital sale a 0,0086 euros por mil páginas y una imagen con análisis de layout a 4,2933, casi quinientas veces más. Como el sistema procesa también ficheros de Office, que van siempre por el medidor barato, meterlos todos en el mismo saco inflaría su coste en ese factor.

**Sobre la detección de fórmulas:** los analizadores la llevan activada, heredada del analizador base, y factura un añadido por página. Se comprobó en la facturación real y **ese medidor no aparece**, así que no se está pagando y no hay nada que desactivar. Se deja anotado para que nadie vuelva a plantearlo.

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

## Ver los costes en Admin

La sección está en `/costes`, sin entrada en el menú de momento. Usa los mismos filtros que el Monitor y muestra el total del periodo, el coste por ejecución, el desglose por actividad, tipología y modelo, la evolución diaria y el listado con una columna de coste. El detalle desplegable de cada ejecución enseña el desglose por llamada.

Lo estimado se presenta siempre separado de lo medido: la cabecera dice si el importe lo incluye, cada fila estimada lleva una marca, y un interruptor decide si suma. Por defecto no suma.

## Coste de las ejecuciones anteriores

Las ejecuciones anteriores a la funcionalidad no guardaron tokens ni páginas de extracción. Lo que se puede hacer es **estimar**, y para eso está `scripts/database/backfill-costes-estimados.ps1`.

Estima el layout a partir de las páginas del contrato, que es el 94 por ciento del gasto real, y la clasificación generativa con un coste medio por documento tomado de un lote controlado. La extracción no se puede estimar. Cada fila que rellena queda marcada como estimada y el script nunca pisa un coste medido. Se lanza a mano, por lotes, y admite `-WhatIf` para ver cuántas filas tocaría.

No compares la suma de estas estimaciones con la factura: la cuenta de IA de producción la consumen también desarrollo y preproducción, cuyas ejecuciones viven en otras bases de datos.
