# Clasificador híbrido por embeddings (A) + GPT en el orquestador, con modo sombra

- **Work item**: PBI AB#100779 (Feature AB#99948). Relacionados: AB#100719 (spike del clasificador A, cerrado), AB#100775 (GPT de producción frente a A sobre cal, cerrado), AB#100813 (integración de la rama del spike en DocumentIA.Batch), AB#100780 (crecimiento y reentreno versionado), AB#100781 (promoción del artefacto por ai-artifacts), AB#100782 (regla TASA→CERJ del GPT)
- **Fecha**: 2026-10-01
- **Rama**: `feature/100779-clasificador-embeddings-hibrido` desde `develop` (dff86b1). En DocumentIA.Batch, `feature/100779-exportar-modelo` desde `feature/100719-clasificador-embeddings` (6f84147)
- **Estado**: diseño aprobado por secciones, pendiente de plan de implementación
- **Decisión de arquitectura**: ADR-002 (`docs/decisiones/ADR-002-clasificador-embeddings-hibrido.md`)
- **Origen de los datos**: `eval/clasificador-embeddings/INFORME.md` §11-§12 de DocumentIA.Batch

## Problema

La clasificación TDN1/TDN2 la hace hoy el GPT en dos llamadas (`classification.phase1` y
`classification.phase2`), o una en peticiones restringidas (`classification.restricted`).
El spike AB#100719 entrenó un clasificador A (regresión logística sobre
`text-embedding-3-large`) y AB#100775 lo midió frente al GPT de producción sobre cal
(1.576 documentos): A acierta el 87,2 % en TDN1 frente al 83,9 % del GPT. El híbrido medido
fuera de línea (INFORME §12) con umbral de confianza 0,6 llega al 89,5 % en TDN1 y al
64,2 % en TDN2, mejor que cada componente por separado con significación, y ahorra el 86 %
de las llamadas GPT de clasificación.

Esas cifras son sobre cal, con etiquetas de la misma fuente que el entrenamiento y con la
confianza bruta de A sin calibrar. El umbral definitivo no se fija con ellas: se fija con
datos reales de una fase de sombra en DEV. Hoy no existe en el backend ningún cliente de
embeddings, ningún mecanismo para cargar un modelo entrenado por entorno, ni un patrón de
"modo off / sombra / activo" para una etapa de clasificación.

## Objetivo

Integrar A en el orquestador como primera etapa de clasificación con enrutado por confianza
y el GPT actual como respaldo, de forma que:

1. en modo **sombra** A se calcula y persiste en cada ejecución sin alterar el resultado;
2. en modo **híbrido** A contesta cuando su confianza alcanza el umbral y el GPT contesta en
   el resto, con el mismo contrato de salida;
3. el modo, los umbrales y la versión del modelo se cambian por configuración en BD, sin
   redesplegar;
4. el coste de la llamada de embeddings queda tarificado y sumado en la actividad
   Clasificar;
5. la clasificación restringida (`restriccionTipologias`) tiene su propio modo y sus
   puertas, apoyadas en un manifiesto del modelo.

Los ocho criterios de aceptación del PBI se cubren en dos fases (sección "Fases").

## Fuera de alcance

- Promoción del artefacto a PRE y PRO: AB#100781. Este PBI no toca PRO.
- Reentreno, crecimiento del corpus y versionado de modelos: AB#100780. Aquí se exporta un
  único modelo v1.
- Calibración de la confianza de A: v1 declara `calibrado = false`. La puerta de
  activación del híbrido restringido queda cerrada por construcción hasta que AB#100780
  entregue un modelo calibrado.
- Cambios de pantalla en el Monitor del Admin. Solo se añade el tipo de modelo nuevo al
  mantenimiento de `ModeloConfigs` para que la fila sea editable.
- La regla TASA→CERJ del GPT (AB#100782).
- Mover el tramo de baja confianza del GPT a A (INFORME §12 lo mide pero no es de este PBI).

## Estado verificado el 2026-10-01

- `GptClasificarDataProvider` resuelve el deployment por `ModeloConfigs` (`Tipo =
  Clasificacion`) con `AiEndpointResolver`, llama por `AzureOpenAIClient` con ApiKey o
  `DefaultAzureCredential`, resiliencia por circuito `endpoint|deployment`, y registra el
  uso con `UsoOpenAiMapper` como `ConsumoIA` con `Actividad = Clasificar` y `Modelo =
  nombre del deployment`.
- La tarifa se resuelve por nombre de deployment contra la fila `tarifas.ia` de
  `ModeloConfigs` (`TarifaRegistryLoader`), se aplica en la activity
  (`TarificadorDeConsumos`) y se agrega en el orquestador con `CalculadoraCosteIA.Agregar`.
  Los consumos con `Descartado = true` cuentan igual en el coste total. El desglose por
  actividad suma en `ClasificacionEur` todo consumo con `Actividad = Clasificar`.
- `ResultadoClasificacion` ya tiene `DetalleProveedores` (`PropuestaProveedor`:
  proveedor, tipología, confianza, motivo de descarte) y `Consumos`. Todo el contrato se
  persiste en `DocumentoEjecucion.ContratoSalidaCompletoJson`.
- Las tipologías del catálogo llevan su par TDN1/TDN2 en `ConfiguracionJson`
  (`Classification.Tdn1`, `Classification.Tdn2`). La fase 2 del GPT convierte TDN2 en
  código de tipología con ese catálogo. Los códigos de `restriccionTipologias` son códigos
  de tipología.
- El orquestador (`DocumentProcessOrchestrator`, Paso 3) no llama a ninguna activity de
  clasificación cuando llega `ExpectedType` resoluble: construye el resultado
  `expectedtype-input` con confianza 1,0.
- No hay cliente de embeddings, ni ONNX, ni ML.NET en la solución. `Azure.AI.OpenAI 2.x`,
  ya referenciado, expone `GetEmbeddingClient`.
- `ModeloConfigs` es el único mecanismo de configuración sin redespliegue (caché de 5
  minutos en todos los loaders). `TipoModelo` llega hasta `Tarifas = 4`. El Admin enumera
  los tipos de forma explícita en `ModeloEdit.razor` y `Modelos.razor`.
- El modelo A del spike no está persistido: `evaluar.py` lo reentrena desde la caché npz
  (`ClasificadorLR`, C = 10, `min_ejemplos` = 5): un modelo multinomial TDN1 sobre 3072
  dimensiones y, por familia, un modelo TDN2 o una constante cuando la familia tiene un
  único subtipo cubierto. La confianza es la probabilidad máxima del modelo TDN1. El texto
  de entrada fue el markdown persistido del documento con espacios colapsados y recortado a
  24.000 caracteres (`embeddings.recortar`).
- DEV, PRE y PRO tienen `text-embedding-3-large` v1 con los mismos nombres de deployment
  (`text-embedding-3-large-030358` y `-010650`), verificado por ARM el 2026-09-30.
- Las orquestaciones Durable en vuelo no toleran que se inserte una activity nueva en su
  secuencia: el despliegue de AB#100176 ya exigió drenarlas.

## Diseño

### 1. Componentes

| Pieza | Repo y ubicación | Responsabilidad |
|---|---|---|
| `exportar_modelo.py` | DocumentIA.Batch, `eval/clasificador-embeddings/` | Entrena A con la partición `train` (mismo `ClasificadorLR`, C = 10, `min_ejemplos` = 5), lee del catálogo de tipologías de DEV el mapa tipología → par TDN1/TDN2 y escribe `clasificador-embeddings-v1.json` y `paridad-v1.json` (10 vectores de train con sus probabilidades TDN1 y TDN2). |
| Artefacto JSON | Blob, contenedor `documentai` del storage de aplicación del entorno, prefijo `modelos/clasificador-embeddings/v1/` | Manifiesto más pesos. En DEV se sube a mano en Fase A; la promoción DEV → PRE → PRO es AB#100781 por el pipeline ai-artifacts (ADR-001). |
| `ModeloEmbeddingsLoader` | `DocumentIA.Core/Configuration/` | Descarga y parsea el artefacto, lo cachea en memoria por ruta y ETag y lo revalida cada 5 minutos. Expone `ModeloEmbeddings` (manifiesto y matrices). Un artefacto inválido deja el modelo en nulo con error en log, sin excepción. |
| `ClasificadorEmbeddings` | `DocumentIA.Core/Services/Classification/` | Dado un vector de 3072 y el modelo: probabilidades TDN1 (softmax), familia ganadora, TDN2 (constante, sigmoide si binario, softmax si multiclase). Devuelve la distribución completa, necesaria para la masa `m` del modo restringido. Sin IO. |
| `DecisorHibrido` | mismo sitio | Aplica modo, umbrales, restricción, manifiesto y puertas sobre la distribución; devuelve `Decision` y motivo. Sin IO. Contiene toda la lógica del criterio 8. |
| `EmbeddingsClasificadorConfigLoader` | `DocumentIA.Core/Configuration/` | Lee la fila `ModeloConfigs` de `Tipo = Embeddings`, caché de 5 minutos. Sin fila, inactiva o JSON inválido → modo `off`. |
| `UsoEmbeddingsMapper` | `DocumentIA.Functions/Services/` | `EmbeddingTokenUsage` → `ConsumoIA`: solo tokens de entrada. No reutiliza `UsoOpenAiMapper`. |
| `EmbeddingsClasificarProvider` | `DocumentIA.Functions/Services/` | Preprocesa el texto (espacios colapsados, primeros 24.000 caracteres), llama a `EmbeddingClient` sobre el deployment configurado con el mismo esquema de autenticación y de circuito que el GPT, mapea el uso, encadena loader, clasificador y decisor, y emite la telemetría. |
| `ClasificarEmbeddingsActivity` | `DocumentIA.Functions/Activities/` | Activity determinista. Nunca propaga excepción: cualquier fallo vuelve como `Decision = DerivarGpt` con `Error`. Tarifica el consumo con `TarificadorDeConsumos`, igual que `ClasificarActivity`. |

El texto de entrada es el mismo markdown que recibe el GPT (`datosNormalizados["Markdown"]`,
ya recortado por páginas en el Paso 2.8), con el mismo preprocesado que el spike, para que
la distribución de confianza de la sombra sea comparable con la de cal.

### 2. Flujo en el orquestador

En `DocumentProcessOrchestrator`, Paso 3, antes del bloque actual de clasificación:

1. Si hay texto, el orquestador llama a `ClasificarEmbeddingsActivity` con el markdown, el
   `ExpectedType` ya resuelto por `ResolverTipologiaActivity`, los códigos de
   `restriccionTipologias` si los hay y el nivel de clasificación. Se llama también con
   `ExpectedType` informado y en peticiones restringidas: la activity decide internamente
   si el modo es `off` y entonces devuelve `Omitido` sin llamar a nada.
2. La activity devuelve `ResultadoEmbeddings` (sección 4).
3. Si `Decision == Contesta` (solo en modo `hibrido`): el orquestador construye el
   `ResultadoClasificacion` con `Modelo = "embeddings:<versión>"`, `Clasificador =
   "Embeddings"`, `ProveedorClasif = "Embeddings"`, la tipología de A y su confianza, y no
   llama a `ClasificarActivity`. En el caso `Desconocido` por masa insuficiente,
   `TipologiaDetectada = "Desconocido"` y `PropuestaTipologia` = predicción sin restringir
   de A.
4. En cualquier otro caso (`Omitido` o `DerivarGpt`) el flujo actual sigue intacto:
   `ExpectedType` → resultado `expectedtype-input`; si no, `ClasificarActivity` con GPT,
   restringido o no.
5. En todos los casos el `ResultadoEmbeddings` se adjunta al contrato y sus consumos se
   acumulan con `AcumularConsumos`. Nada de lo que devuelve A altera `Identificacion`,
   `ResultadosProcesamiento`, GDC ni la deduplicación salvo en la rama `Contesta`.

La activity es secuencial y previa al GPT. En sombra añade la latencia de una llamada de
embeddings (centenares de milisegundos frente a decenas de segundos del GPT). Se descarta
paralelizar sombra y GPT: obligaría a dos flujos distintos para sombra e híbrido.

### 3. Lógica de decisión (`DecisorHibrido`)

- Modo `off`: no se calcula embedding; `Omitido`.
- Modo `sombra`: se calcula y persiste todo; `DerivarGpt`, motivo `sombra`.
- Modo `hibrido`, petición sin restricción: `Contesta` si `confianza >= umbralConfianza`;
  si no, `DerivarGpt`, motivo `confianza_baja`. Con `ExpectedType` informado,
  `DerivarGpt`, motivo `expected_type`: el caller manda.
- Petición restringida: usa `restringido.modo` (`off`, `sombra` por defecto, `hibrido`) y
  sus umbrales (`umbralMasa`, `umbralConfianzaCondicionada`), separados de los del modo
  no restringido. Se calculan `m` = suma de las probabilidades de las tipologías
  permitidas y `confianzaCondicionada` = p(mejor permitida) / `m`. Ambos se persisten en
  cualquier modo distinto de `off`, junto con la predicción sin restringir.
- Puerta por petición (antes de decidir en `hibrido` restringido): algún código del
  conjunto no está en el manifiesto → `DerivarGpt`, motivo `cobertura`; dos códigos del
  conjunto comparten par TDN1/TDN2 → `DerivarGpt`, motivo `par_compartido`.
- Puerta de activación: `hibrido` restringido exige `manifiesto.calibrado == true`; si
  no, se comporta como `sombra` y registra motivo `sin_calibracion`.
- `hibrido` restringido con puertas abiertas: `m < umbralMasa` → `Contesta` con
  `Desconocido` y la predicción sin restringir como propuesta; `confianzaCondicionada >=
  umbralConfianzaCondicionada` → `Contesta` con la mejor permitida; si no →
  `DerivarGpt`, motivo `confianza_condicionada_baja`.
- Mapeo par → tipología con el catálogo de tipologías activas del entorno. Si el par de A
  no corresponde a ninguna tipología activa → `DerivarGpt`, motivo `sin_tipologia`, y el
  par se persiste igualmente.

### 4. Contrato y persistencia

- Nuevo bloque `ResultadoClasificacion.Embeddings` (`ResultadoEmbeddings`, nullable;
  ausente con modo `off` o sin texto): `VersionModelo`, `Modo`, `ModoRestringido`,
  `Tdn1`, `Tdn2`, `Tipologia`, `Confianza`, `Top3` (tres mejores TDN1 con probabilidad),
  `Decision`, `Motivo`, `Restringido { Masa, ConfianzaCondicionada, Puerta,
  PrediccionSinRestringir }`, `LatenciaMs`, `Error`, `Deployment`. La distribución
  completa no se persiste.
- Entrada adicional en `DetalleProveedores` con `Proveedor = "Embeddings"`, tipología,
  confianza y `MotivoDescarte` = motivo de la decisión.
- `DetalleEjecucion.Clasificacion.RamaClasificacion`: `"gpt"`, `"embeddings"` o
  `"expectedtype"` (criterio 4).
- Todo va en `ContratoSalidaCompletoJson`. No se crean tablas ni columnas. El análisis de
  sombra usa `JSON_VALUE` sobre DEV con la consulta versionada en
  `scripts/analisis/sombra-embeddings.sql`: acierto de A frente al GPT y frente al
  validado humano por tramo de confianza, cobertura por umbral, latencia y errores.
- `ResultadosProcesamiento.ModeloClasificacion` y `ConfianzaClasificacion` solo cambian
  en la rama `Contesta`.

### 5. Configuración sin redespliegue

Fila nueva en `ModeloConfigs`: `Tipo = Embeddings` (valor 5 del enum `TipoModelo`),
`Key = "clasificador.embeddings"`, `Provider = "AzureOpenAI"`, `ConfiguracionJson`:

```json
{
  "deploymentName": "text-embedding-3-large-030358",
  "resourceAlias": "<alias de la cuenta Azure OpenAI del entorno, a verificar en DEV>",
  "authMode": "ManagedIdentity",
  "artefacto": {
    "container": "documentai",
    "blobPath": "modelos/clasificador-embeddings/v1/clasificador-embeddings-v1.json"
  },
  "modo": "sombra",
  "umbralConfianza": 0.6,
  "restringido": { "modo": "sombra", "umbralMasa": 0.5, "umbralConfianzaCondicionada": 0.8 },
  "maxChars": 24000,
  "timeoutSeconds": 20
}
```

- Endpoint y credencial se resuelven con `AiEndpointResolver` y el mismo `authMode` que
  los modelos de clasificación: PRE y PRO apuntarán a su IA propia sin tocar código.
- Cambiar modo, umbral o versión del modelo es editar la fila; entra en 5 minutos. Sin
  fila, `Activo = 0` o JSON inválido → `off`, con error en log y telemetría, nunca
  excepción.
- Script de migración nuevo en `scripts/database/` que inserta la fila en `sombra` para
  DEV y en `off` para PRE y PRO (en esos entornos el artefacto no existe hasta AB#100781).
- Los umbrales de restringido son valores de arranque. El criterio 8 exige la simulación
  sobre cal con conjuntos restringidos antes de activar `hibrido` restringido en cualquier
  entorno; es tarea de la Fase B.
- Admin: una opción `Embeddings` en el selector de `ModeloEdit.razor` y una lista en
  `Modelos.razor`, para que la fila se edite desde el Admin y no solo por SQL.

### 6. Artefacto y manifiesto

```json
{
  "manifiesto": {
    "version": "v1",
    "entrenadoEn": "2026-10-..",
    "particion": "train",
    "nDocumentos": 0,
    "modeloEmbeddings": "text-embedding-3-large",
    "dimensiones": 3072,
    "maxChars": 24000,
    "calibrado": false,
    "umbralRecomendado": 0.6,
    "tipologias": [ { "codigo": "tasa.09", "tdn1": "TASA", "tdn2": "09" } ]
  },
  "tdn1": { "clases": ["CERA", "TASA"], "coef": [[0.0]], "intercept": [0.0] },
  "tdn2": {
    "TASA": { "clases": ["09", "12"], "coef": [[0.0]], "intercept": [0.0] },
    "CERA": { "constante": "16" }
  }
}
```

- `tipologias` lo construye el exportador cruzando las clases predecibles con el catálogo
  de tipologías activas de DEV: una tipología entra solo si su TDN1 está en
  `tdn1.clases` y su TDN2 en las clases o en la constante de su familia. Una familia TDN2
  con etiqueta vacía (`""`, subtipo no cubierto en el entrenamiento) se exporta como clase
  y el decisor la trata como "sin tipología".
- Tamaño estimado: 3072 × (K1 + ΣK2) coeficientes; con unas 30 familias y unos 150
  subtipos, unos 550 k valores, del orden de 8 MB en JSON. Se descarga una vez por
  instancia; no cabe en `ModeloConfigs`.
- `paridad-v1.json` acompaña al artefacto; una copia reducida embebida en el proyecto de
  tests fija la paridad entre sklearn y la inferencia en C#.

### 7. Costes

- `UsoEmbeddingsMapper`: `InputTokenCount` → `TokensEntrada`; `TokensSalida = 0`; sin
  cacheados ni razonamiento; `Actividad = Clasificar`, `Operacion = "embeddings"`,
  `Modelo = deploymentName`, `Descartado = false` (en sombra el coste es real).
- Línea nueva en el catálogo `tarifas.ia` por nombre de deployment
  (`text-embedding-3-large-030358` y `-010650`, 0,112 EUR por millón de tokens de
  entrada), por script de migración. Con ello `TarifasCompletas = true` y
  `ModelosSinTarifa` vacío (criterio 5) sin cambiar la resolución de tarifas. El
  desglose por actividad suma la línea en `ClasificacionEur` (criterio 6).

### 8. Telemetría

Evento `Classification.Embeddings` emitido desde el proveedor con `VersionModelo`,
`Modo`, `Decision`, `Motivo`, `Confianza`, `Tdn1`, `LatenciaMs`, `Error` y
`EjecucionGuid`; métrica `Classification.Embeddings.LatenciaMs`. El evento
`DocumentProcessed` añade `RamaClasificacion`. El orquestador no emite nada.

### 9. Fallos y degradación

- Cualquier fallo (artefacto no descargable o inválido, 429, timeout, respuesta sin
  vector, par sin tipología, dimensiones distintas de las del modelo) vuelve como
  `Decision = DerivarGpt` con `Error` y `Motivo`. En sombra no cambia nada; en híbrido el
  GPT contesta como hoy. La activity no propaga excepciones al orquestador.
- 429 de embeddings: sin reintento y sin `RateLimitExcedido`. El circuito por
  `endpoint|deployment` abre tras fallos consecutivos; mientras está abierto la activity
  devuelve `DerivarGpt`, motivo `circuito_abierto`, sin llamar. Una cuota de embeddings
  agotada no detiene la clasificación.
- El consumo solo se registra si la llamada devolvió `usage`.

### 10. Compatibilidad con lo operativo y con los clientes

- El contrato de salida solo crece: campos nuevos, todos opcionales, en
  `ResultadoClasificacion.Embeddings`, `DetalleProveedores` y
  `DetalleEjecucion.Clasificacion.RamaClasificacion`. Ningún campo existente cambia de
  nombre, tipo ni semántica. Un cliente que ignore propiedades desconocidas no nota nada.
- En modo `sombra` el resultado devuelto al cliente es idéntico al actual, campo a campo,
  salvo los añadidos. Los tests del orquestador lo fijan comparando el contrato con y sin
  sombra.
- En modo `hibrido` cambia la fuente del valor, no la forma: `Modelo` toma un valor nuevo
  (`embeddings:v1`) y `Clasificador` y `ProveedorClasif` toman `Embeddings`. Antes de la
  Fase B se revisan los consumidores de `ModeloClasificacion` (Monitor, consultas de
  operación, GDC) para que un valor nuevo no rompa filtros ni agrupaciones.
- El contrato de entrada no cambia: `ExpectedType`, `restriccionTipologias`,
  `classificationOnly` y `nivelClasificacion` conservan su comportamiento. `ExpectedType`
  sigue mandando sobre cualquier predicción de A.
- La deduplicación compara los mismos campos que hoy; una ejecución en sombra es
  reutilizable como lo era antes.
- `TipoModelo.Embeddings = 5` es un valor nuevo del enum: los loaders existentes filtran
  por su propio tipo y no ven la fila. El Admin recibe el valor nuevo en el formulario y
  en la lista para que no quede una fila invisible.
- Despliegue de Fase A: insertar una activity en el orquestador cambia la secuencia que
  las orquestaciones en vuelo repiten. Se despliega con las orquestaciones drenadas, como
  en AB#100176. Mientras `ModeloConfigs` no tenga la fila o esté en `off`, la activity
  devuelve `Omitido` y el comportamiento es el actual.
- PRE y PRO reciben el código con la fila en `off`: ni llamada de embeddings, ni descarga
  de artefacto, ni cambio de contrato más allá de los campos nulos.
- Sin cambios en los endpoints HTTP, en GDC, en los scripts de E2E ni en los prompts de
  BD.

### 11. Tests

- `ClasificadorEmbeddingsTests` (Core): paridad contra el fixture reducido con igualdad a
  1e-6 en TDN1 y TDN2; casos multiclase, binario y familia constante.
- `DecisorHibridoTests` (Core): tres modos no restringidos; `ExpectedType` deriva; tres
  modos restringidos; masa insuficiente → `Desconocido` con propuesta; puerta de
  cobertura; puerta de par compartido; puerta de calibración (`hibrido` con `calibrado =
  false` se comporta como sombra y lo registra); par sin tipología.
- `UsoEmbeddingsMapperTests`: solo tokens de entrada, salida 0, actividad y operación,
  deployment como modelo.
- `ModeloEmbeddingsLoaderTests`: parseo del artefacto, JSON inválido → sin modelo y sin
  excepción, caché por ETag, dimensiones incompatibles.
- `EmbeddingsClasificadorConfigLoaderTests`: fila ausente, inactiva o inválida → `off`.
- `DocumentProcessOrchestratorTests`: `Contesta` salta `ClasificarActivity`;
  `DerivarGpt` y `Omitido` dejan el flujo como hoy; consumos de A acumulados; contrato en
  sombra idéntico al actual salvo los campos añadidos.
- `CalculadoraCosteIA`: un caso con consumo de embeddings y de GPT en la misma ejecución
  sumando ambos en `ClasificacionEur`.
- DocumentIA.Batch: test de `exportar_modelo.py` sobre un juego sintético pequeño: el
  JSON reproduce `predict_proba` y `tipologias` solo incluye pares predecibles.
- Verificación en DEV antes de cerrar la Fase A: 20 ejecuciones reales con el bloque
  `Embeddings` persistido, `TarifasCompletas = true`, `ClasificacionEur` sumando
  embeddings y evento en App Insights.

### 12. Fases

- **Fase A, sombra en DEV** (criterios 1, 2, 4, 5, 6, 7): exportador y artefacto v1;
  loader, clasificador, decisor, proveedor, activity y mapper; contrato; fila
  `ModeloConfigs` y tarifa; Admin mínimo; telemetría; consulta de análisis. El decisor se
  implementa completo con sus tests; `hibrido` queda inactivo por configuración.
- **Corte entre fases**: con dos semanas de sombra o 500 ejecuciones en DEV, lo que antes
  llegue, se elige el umbral con `sombra-embeddings.sql` y se documenta en el Session Log.
  Se añade la simulación sobre cal con conjuntos restringidos para los umbrales de
  restringido.
- **Fase B, híbrido en DEV** (criterios 3 y 8): activar `hibrido` por configuración,
  verificar TDN1 del híbrido ≥ GPT sobre el mismo conjunto, revisar consumidores de
  `ModeloClasificacion`, documentar. La promoción del artefacto y la activación en PRE y
  PRO no son de este PBI.

## Riesgos

- Paridad sklearn / C#: un error de softmax o de orden de clases daría predicciones
  distintas de las medidas. Lo cubre el test de paridad con vectores exportados.
- Latencia añadida en sombra en el camino crítico: centenares de milisegundos por
  ejecución. Si en DEV supera un segundo de mediana se revisa antes de la Fase B.
- Texto de producción frente a texto del spike: el spike usó el markdown persistido
  completo; el orquestador clasifica sobre el recorte por páginas. Si la distribución de
  confianza en sombra difiere mucho de la de cal, se mide el efecto del recorte antes de
  fijar el umbral.
- Umbrales de restringido sin medir hasta la simulación sobre cal.
- La fila de `ModeloConfigs` en `off` en PRE y PRO es una convención: si alguien la
  activa sin artefacto en el storage, el loader falla, el modo efectivo es `DerivarGpt`
  con error, y la telemetría lo hace visible. No hay impacto funcional.

## Pendientes que resuelve el plan

- Nombre exacto del `resourceAlias` y del contenedor en DEV, verificados al crear la fila.
- Orden de las tasks en AB#100779 y relación con AB#100813 (integración de la rama del
  spike en DocumentIA.Batch, previa al exportador).
- Query exacta de `sombra-embeddings.sql` y columnas de `Top3`.

## Referencias

- `eval/clasificador-embeddings/INFORME.md` §11-§12 (DocumentIA.Batch,
  `feature/100719-clasificador-embeddings`, 6f84147).
- `docs/decisiones/ADR-001-promocion-artefactos-ia-dev-pre-pro.md`.
- `docs/decisiones/ADR-002-clasificador-embeddings-hibrido.md`.
- `docs/superpowers/specs/2026-09-07-costes-ia-por-ejecucion-design.md` (tarificación por
  consumo).
- `docs/superpowers/specs/2026-08-11-clasificacion-restringida-tipologias-design.md`.
