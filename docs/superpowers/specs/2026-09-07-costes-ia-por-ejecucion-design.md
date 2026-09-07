# Control de costes de IA por ejecución

**Fecha:** 2026-09-07
**Estado:** Diseño aprobado, pendiente de plan de implementación
**Work items:** pendientes de crear (el MCP de Azure DevOps no conectó en la sesión de diseño). Ver "Trabajo previo".

## Problema

Hoy no existe forma de saber lo que ha costado procesar un documento concreto. El coste de IA solo se conoce a posteriori y de forma agregada, cruzando Cost Management con la volumetría de la base de datos, un ejercicio manual que además ha dejado de devolver histórico desde agosto de 2026. No se puede responder a preguntas operativas básicas: cuánto cuesta una tipología, qué solicitante consume más, si un cambio de modelo ha salido rentable, o cuánto se ha gastado en una ejecución que acabó en error.

Los datos para responder ya llegan al sistema y se tiran. El SDK de OpenAI devuelve el consumo de tokens en cada respuesta y ningún proveedor lo lee. Content Understanding devuelve un bloque `usage` con páginas, tokens de contextualización y los tokens del modelo generativo que factura por dentro, y solo se le extrae el número de páginas.

## Objetivo

Que cada ejecución registre lo que ha consumido en servicios de IA, desglosado por llamada y agregado en un total, expresado en euros y en tokens. Persistido en base de datos con el menor impacto posible en el esquema. Opcional en el contrato de salida: oculto por defecto, visible solo si el llamador lo pide.

Alcance restringido a **servicios de IA**. No se contabiliza Blob Storage, SQL, Functions ni ninguna otra infraestructura.

## Alcance

### Dentro

- Captura del consumo real en los siete puntos donde el sistema llama a un servicio de IA.
- Catálogo de tarifas por modelo con vigencia temporal.
- Cálculo del coste en euros por llamada y agregado por ejecución.
- Persistencia del total en dos columnas nuevas y del desglose en el contrato ya persistido.
- Parámetro de entrada que activa la devolución del bloque en la salida.

### Fuera

- Pantalla de costes en Admin e informes agregados. El dato queda disponible en base de datos para explotarlo después.
- Carga automática de tarifas desde la Retail Prices API. Las tarifas se mantienen a mano.
- Coste de almacenamiento, cómputo o red.
- Presupuestos, alertas o cortes por umbral de gasto.

## Puntos de consumo de IA

El pipeline llama a un servicio de IA en siete sitios. Todos quedan instrumentados.

| Punto | Servicio | Unidad que se factura |
| --- | --- | --- |
| Clasificación con Document Intelligence | Custom classifier | Páginas |
| Clasificación con modelo generativo, fases 1 y 2 | Azure OpenAI | Tokens de entrada, cacheados y salida |
| Clasificación restringida y rescate | Azure OpenAI | Tokens |
| Layout a Markdown, en sus cuatro puntos de llamada | Document Intelligence prebuilt-layout | Páginas |
| Extracción con Content Understanding | Content Understanding | Páginas, tokens de contextualización y tokens del modelo generativo interno |
| Extracción con Document Intelligence a medida | Custom analyzer | Páginas |
| Extracción directa o de refuerzo, y prompt libre o resumen | Azure OpenAI | Tokens |

La clasificación forzada por `ExpectedType` no llama a ningún servicio y no genera consumo.

Content Understanding merece una nota. El servicio factura por su cuenta las páginas y la contextualización, pero los tokens del modelo generativo que usa por dentro se cargan al deployment de Foundry conectado, y los declara en su respuesta bajo `usage.tokens` con claves del tipo `gpt-4.1-input`, `gpt-4.1-output` y `text-embedding-3-large`. Ese bloque se traduce a consumos independientes, uno por modelo, para que el coste siga siendo correcto cuando el servicio cambie de modelo generativo sin que nosotros toquemos nada.

## Modelo de datos en el contrato

Dos clases nuevas en `DocumentIA.Core.Models`.

`ConsumoIA` representa **una llamada a un servicio de IA**:

- `Actividad`: la actividad del pipeline, por ejemplo `Clasificar`, `Extraer`, `Prompt` o `Layout`.
- `Operacion`: el punto concreto, por ejemplo `clasificacion.fase1`, `layout.preclasificacion` o `extraccion.cu.contextualizacion`.
- `Proveedor`: `AzureOpenAI`, `DocumentIntelligence` o `ContentUnderstanding`.
- `Modelo`: el modelo físico que se ha usado. El nombre del deployment para OpenAI, el identificador del clasificador o `prebuilt-layout` para Document Intelligence, el del analyzer para Content Understanding.
- Tokens, todos opcionales: entrada, entrada cacheada, salida, razonamiento y contextualización.
- `Paginas`, opcional.
- `CosteEur`, decimal opcional.
- `TarifaAplicada`: la línea de tarifa usada, con su fecha de vigencia, por ejemplo `gpt-5-mini@2026-07-21`.
- `Descartado`: cierto cuando el resultado de esa llamada se descartó. La clasificación evalúa varios proveedores y se queda con uno, pero **todos se han pagado**. Sin esta marca el coste de una ejecución con fallback aparecería subestimado.

`CostesIA` es el agregado de la ejecución:

- `Version`, para poder evolucionar el bloque sin romper a los consumidores.
- `Consumos`: la lista de `ConsumoIA`.
- `CosteTotalEur`, `TokensTotales` y `PaginasTotales`. Los tokens totales suman entrada, salida y contextualización de todos los consumos. No suman aparte los cacheados ni los de razonamiento, porque ya están contenidos en la entrada y en la salida respectivamente.
- `TarifasCompletas` y `ModelosSinTarifa`: si algún modelo no tiene precio en el catálogo, el consumo se registra igual con sus tokens y páginas, el coste queda a nulo y el total se marca como incompleto. Una tarifa que falta nunca hace fallar una ejecución.
- `ReutilizadaPorDuplicado` y `CosteEjecucionOriginalEur`, para el caso de deduplicación.

Cuelga del contrato como `DetalleEjecucion.Costes`, anulable y omitido del JSON cuando es nulo.

## Catálogo de tarifas

### Por qué por modelo físico y no por fila de configuración

La tabla `ModeloConfigs` tiene varias filas que apuntan al mismo deployment. El juego gpt-4o-mini, el juego gpt-5-mini en sus tres tipos, y los alias de clasificación que existen para que los casos de prueba puedan pedir un modelo por su nombre literal. Si la tarifa viviera en cada fila, el mismo precio estaría duplicado media docena de veces y se desalinearía en cuanto alguien editara uno solo.

La tarifa se resuelve por el **modelo físico que consta en el consumo**. Así gpt-4o-mini y gpt-5-mini cuestan distinto aunque una tipología cambie de juego, una petición que fuerce un modelo paga lo que cueste el deployment al que apunte ese alias, y los tokens internos de Content Understanding se tarifican con el precio del modelo que el propio servicio declara.

### Dónde vive

Una única fila en `ModeloConfigs` con un valor nuevo del enum `TipoModelo`, `Tarifas`, y clave `tarifas.ia`. Es una columna JSON que ya existe, así que no hay cambio de esquema, la fila se edita desde la pantalla de modelos de Admin como cualquier otra y el registro se refresca solo con la caché de cinco minutos que ya usan los demás cargadores.

### Formato

```json
{
  "Moneda": "EUR",
  "Tarifas": [
    { "Modelo": "gpt-4o-mini", "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.00, "EurEntradaCachePor1M": 0.00, "EurSalidaPor1M": 0.00 },
    { "Modelo": "gpt-5-mini", "VigenteDesde": "2026-07-21",
      "EurEntradaPor1M": 0.00, "EurEntradaCachePor1M": 0.00, "EurSalidaPor1M": 0.00 },
    { "Modelo": "gpt-4.1", "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.00, "EurEntradaCachePor1M": 0.00, "EurSalidaPor1M": 0.00 },
    { "Modelo": "text-embedding-3-large", "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.00 },
    { "Modelo": "prebuilt-layout", "VigenteDesde": "2026-04-01", "EurPorPagina": 0.00 },
    { "Modelo": "CU_NS_1.4_2", "VigenteDesde": "2026-04-01",
      "EurPorPagina": 0.00, "EurContextualizacionPor1M": 0.00 }
  ]
}
```

Los importes van a cero en este documento a propósito. Las cifras reales se cargan en el script de alta a partir de los precios vigentes de la suscripción, no se fijan aquí. Lo que este diseño fija es el formato.

Reglas del catálogo:

- **Una línea por modelo y fecha de vigencia.** Cambiar un precio es añadir una línea nueva, nunca editar la anterior. Las ejecuciones antiguas siguen cuadrando con lo que se facturó entonces.
- La línea aplicable es la de mayor `VigenteDesde` que no supere la fecha de la ejecución.
- Un modelo ausente del catálogo no rompe nada, deja el coste a nulo y marca el total como incompleto.
- El nombre del modelo se compara sin distinguir mayúsculas y sin espacios sobrantes.

### Los tokens cacheados son un subconjunto, no un sumando

La API de OpenAI devuelve el total de tokens de entrada, y los cacheados **vienen incluidos dentro de ese total**, no aparte. Sumar ambos contaría dos veces la parte cacheada e inflaría el coste, justo en el tramo donde el proyecto tiene una tasa de acierto de caché alta por el tamaño del prompt fijo de clasificación.

La regla de cálculo es: los tokens de entrada a precio pleno son el total de entrada menos los cacheados, y los cacheados se tarifican con su propio precio reducido. El consumo guarda ambos valores tal como los devuelve la API, sin restar, y es la calculadora quien aplica la resta. Hay una prueba dedicada a esto.

### Presentación de la fila en Admin

El enum de tipos de modelo se usa en un desplegable de la pantalla de edición y en los listados por tipo, ambos con las opciones enumeradas una a una. Añadir el valor al enum sin tocar esas pantallas dejaría la fila de tarifas invisible en Admin. Entra en el alcance añadir la opción a la pantalla de edición y a los listados, para que la afirmación de que se edita como cualquier otra fila sea cierta.

### Cargador

`TarifaRegistryLoader` en `DocumentIA.Core.Configuration`, calcado del patrón de los otros cargadores de registro: caché en memoria de cinco minutos, lectura por `IServiceScopeFactory`, tolerante a JSON inválido.

## Cálculo y propagación

### El coste se calcula dentro de las actividades

Restricción de Durable Functions: el orquestador tiene que ser determinista y no puede leer la base de datos. El catálogo de tarifas vive en base de datos, así que **cada actividad calcula el coste de sus propias llamadas** y el orquestador se limita a acumular las listas y sumar. Sumar decimales sí es determinista.

`CalculadoraCosteIA` en `DocumentIA.Core.Services` recibe un consumo y la fecha de la ejecución, resuelve la línea de tarifa y devuelve el coste. Es una clase sin estado y sin dependencias de infraestructura, fácil de probar aislada.

### Los consumos viajan en los resultados que ya cruzan la frontera

Cuatro tipos de resultado ganan una lista de consumos: el de clasificación, el de extracción, el del prompt y el del layout a Markdown. Son exactamente los que ya viajan entre actividad y orquestador, así que no hace falta ningún canal nuevo.

El orquestador los va acumulando en `DetalleEjecucion.Costes.Consumos` según recibe cada resultado, y calcula los totales al cerrar. Los consumos de proveedores descartados entran igual, marcados.

### Captura por proveedor

- **Los proveedores de OpenAI** leen el bloque de consumo de la respuesta del SDK, que ya trae entrada, cacheados, salida y razonamiento. El SDK resuelto en la solución lo expone. Las llamadas combinadas, cuando una sola invocación resuelve extracción y resumen a la vez, generan **un solo consumo**, porque una sola llamada se ha pagado.
- **Los proveedores de Document Intelligence** registran las páginas que ya cuentan hoy.
- **Content Understanding** parsea el bloque `usage` completo, no solo el contador de páginas: páginas por tipo de meter, tokens de contextualización y el diccionario de tokens por modelo. De ahí salen varios consumos, uno por el servicio y uno por cada modelo generativo que declare.

### Fallo en la captura

Si un proveedor no puede leer su consumo, se registra el consumo con los campos a nulo y una operación marcada, y la ejecución sigue. **El control de costes nunca puede tumbar un procesamiento.**

## Persistencia

Dos columnas nullable en `DocumentoEjecuciones`, en una migración puramente aditiva:

- `CosteIAEur`, decimal de 18 con 6 decimales.
- `TokensIA`, entero, con el mismo criterio de suma que el total del contrato.

El desglose completo no necesita columna: viaja dentro del contrato que ya se serializa entero en `ContratoSalidaCompletoJson`.

El motivo de las dos columnas es práctico. Con ellas, agregar coste por fecha, tipología o solicitante es una consulta normal sobre columnas escalares. Sin ellas, cualquier informe sería un `OPENJSON` sobre la tabla más pesada de producción.

`PersistirActivity` las rellena desde el bloque agregado. Ambas admiten nulo, así que las ejecuciones anteriores al cambio no necesitan relleno retroactivo.

### Impacto en el tamaño de la base de datos

El bloque añade en torno a dos o tres kilobytes por ejecución al contrato persistido. Es un crecimiento real y conviene decirlo, más aún con la optimización de almacenamiento de producción en curso. Se mitiga así: en el contrato persistido los consumos van sin campos nulos, y el bloque no duplica ningún dato que ya esté en otra parte del contrato.

## Contrato de entrada y visibilidad en la salida

Nuevo campo booleano en `Instrucciones`, `IncluirCostes`, con valor falso por defecto. Un contrato que no lo mencione se comporta exactamente igual que hoy.

El bloque **se calcula y se persiste siempre**, con independencia del parámetro. Lo que el parámetro decide es solo si se devuelve. El orquestador anula el bloque después de persistir cuando no se ha pedido, y al ser anulable se omite del JSON de salida. Es el mismo patrón que ya se usa con el Markdown de postproceso y con el timeline de actividades.

Hay tres caminos de retorno y los tres tienen que aplicar la regla: el final normal, y los dos retornos tempranos por documento duplicado. Se resuelve con un único método auxiliar aplicado en los tres puntos, no con la regla copiada tres veces.

### Deduplicación

Cuando una petición se resuelve reutilizando una ejecución anterior no se gasta nada en IA, pero la salida histórica que se rehidrata trae el bloque de costes de aquella ejecución. Devolverlo tal cual haría contar dos veces el mismo gasto.

El bloque se sustituye por uno con la lista vacía y los totales a cero, marcado como reutilizado, y con el coste de la ejecución original en su propio campo a título informativo. Así la suma de costes sobre la tabla sigue cuadrando con la factura.

## Pruebas

Unitarias, en la solución de tests que ya existe:

- Calculadora: selección de línea por fecha de vigencia, precedencia de la línea más reciente, modelo ausente, coste por página y coste por contextualización.
- Calculadora, caso de los cacheados: una respuesta con entrada total y una parte cacheada tiene que tarificar la diferencia a precio pleno y la parte cacheada a precio reducido, nunca la suma de ambas cifras.
- Cargador de tarifas: JSON válido, JSON inválido que no lanza, fila ausente, caché.
- Cada proveedor: que el consumo capturado refleja la respuesta del servicio, incluido el desglose de Content Understanding y el caso de llamada combinada que genera un consumo único.
- Orquestador: acumulación de consumos, inclusión de los descartados, cálculo del total, ocultación del bloque en los tres caminos de retorno y sustitución en el caso duplicado.
- Persistencia: las dos columnas se rellenan desde el agregado, y quedan a nulo cuando no hay consumo.

## Documentación

- Actualizar la versión del modelo de datos en la especificación del diagrama de entidades, con las dos columnas nuevas.
- Documentar `IncluirCostes` y la forma del bloque de salida en la documentación de la API.
- Script de alta del catálogo de tarifas en `scripts/migrations/`, idempotente y con copia de seguridad previa de la tabla, siguiendo la convención de los scripts del juego gpt-5.

## Alternativas descartadas

- **Tarifa en cada fila de `ModeloConfigs`.** Duplica el precio en todas las filas que comparten deployment y se desalinea sola. Descartada al revisar los alias existentes.
- **Todo en el contrato JSON, sin columnas.** Cero migración, pero convierte cualquier informe de coste en un `OPENJSON` masivo sobre la tabla más grande de producción.
- **Tabla propia de consumos, una fila por llamada.** Lo más cómodo para analizar, y lo contrario del requisito de impacto mínimo en el esquema.
- **Tabla propia solo para las tarifas.** Más cómoda de consultar desde SQL, pero añade una tabla para lo que la configuración de modelos ya resuelve con el patrón establecido.
- **Tarifas en la configuración de la aplicación.** Cambiar un precio exigiría un despliegue.
- **Calcular el coste en el orquestador.** Imposible sin romper el determinismo de Durable Functions.

## Riesgos

- **Crecimiento de la base de datos**, cuantificado arriba, en tensión con la optimización de almacenamiento en curso.
- **Tarifas desactualizadas.** El coste es una estimación basada en un catálogo mantenido a mano. No sustituye a la facturación real y conviene contrastarlo periódicamente contra Cost Management.
- **Cobertura parcial del consumo interno de Content Understanding.** Depende de que el servicio siga declarando su bloque de uso con la forma documentada. Si cambia, el consumo se registra sin coste y el total se marca incompleto en lugar de dar una cifra falsa.
- **Una petición que falla a mitad** deja consumo registrado de los pasos ya ejecutados. Es lo correcto, se ha pagado, pero conviene tenerlo presente al comparar totales con volumetría de documentos completados.

## Trabajo previo

La norma del proyecto pide crear los work items en Azure DevOps antes de desarrollar. El servidor no conectó durante la sesión de diseño, así que hay que crearlos antes de empezar a implementar y referenciar sus identificadores reales en las ramas y los commits. La épica de control de costes y límites operativos del pipeline es el padre natural de este trabajo.
