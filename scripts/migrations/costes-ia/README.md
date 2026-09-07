# Catálogo de tarifas de servicios de IA (AB#100234)

Alta del catálogo que traduce el consumo de IA de cada ejecución a euros.

## Qué hace

`01-seed-tarifas-ia.sql` inserta una fila en `ModeloConfigs` con `Tipo=4` (Tarifas) y `Key='tarifas.ia'`, cuyo JSON lleva una línea por modelo físico y fecha de vigencia. No hay cambio de esquema: reutiliza la columna de configuración que ya existe.

El script hace copia de seguridad de la tabla antes de tocar nada, es idempotente y no pisa un catálogo ya existente.

## Antes de ejecutarlo

El catálogo trae **precios de lista reales**, consultados el 7 de septiembre de 2026 en la API pública de precios de Azure para West Europe, que es donde están los recursos. Quedan dos cosas por hacer.

**Añadir las dos líneas que dependen de identificadores del entorno.** El clasificador de Document Intelligence y el analizador de Content Understanding se tarifan por su identificador propio, que el paso 0 del script revela. Los precios ya están anotados en el propio script, solo falta pegar la clave. Sin ellas el sistema funciona, pero esos consumos quedan sin coste y el agregado sale marcado como incompleto.

**Contrastar contra la facturación real.** Son precios de lista. Si la suscripción tiene descuento, el coste calculado saldrá por encima del real. Compara un lote pequeño contra Cost Management antes de dar los importes por definitivos.

### De dónde sale cada precio

El tipo de despliegue determina qué medidor factura, y eso cambia el precio hasta un 70 por ciento entre variantes del mismo modelo. Los despliegues reales de producción, leídos de Azure:

| Despliegue | Modelo real | Tipo | Medidor |
| --- | --- | --- | --- |
| `gpt-4.1-892749` | gpt-4.1 | GlobalStandard | global |
| `gpt-4.1-mini-590191` | gpt-4.1-mini | GlobalStandard | global |
| `text-embedding-3-large-010650` | text-embedding-3-large | GlobalStandard | global |
| `gpt-5-mini` | gpt-5-mini | DataZoneStandard | zona de datos |

Hay un detalle que conviene no perder: en desarrollo existe un despliegue llamado `gpt-4o-mini` que en realidad sirve el modelo gpt-4.1-mini con tipo Standard, es decir **precio regional**, más caro que el global. Por eso el catálogo lleva dos líneas distintas. La clave es el nombre del despliegue, no el del modelo.

### Reproducir la consulta

El proxy corporativo rompe la verificación de revocación del certificado, así que hace falta `--ssl-no-revoke`:

```bash
curl -sS --ssl-no-revoke --get https://prices.azure.com/api/retail/prices \
  --data-urlencode "currencyCode=EUR" \
  --data-urlencode "$filter=productName eq 'Azure OpenAI' and armRegionName eq 'westeurope'"
```

Para ver los tipos de despliegue:

```bash
az cognitiveservices account deployment list \
  --name srbaisrv-westeurope --resource-group SRBRGDOCSAIPROD \
  --subscription "Producción Central" -o table
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
