# Prompt y resumen independientes de la clasificación

**Fecha:** 2026-08-10
**Estado:** Pendiente de aprobación (spec escrito, work items creados, sin implementación)
**Work items:** AB#100027 (padre) — AB#100028 (Cambio A), AB#100029 (Cambio B), AB#100030 (Apunte C)

## Origen

La petición inicial fue desplegar un modelo GPT nuevo al que pasar **el documento directamente**
en lugar del markdown extraído, para poder resumir y ejecutar prompts sobre formatos que Document
Intelligence Layout no procesaba (XLSX, PPTX).

El análisis descartó esa vía por dos motivos, en este orden:

1. **Ningún modelo ingiere ficheros de forma nativa.** Las entradas de un modelo GPT son texto e
   imágenes. Cuando la Responses API de Azure OpenAI acepta un PDF, la plataforma extrae su texto
   y además **renderiza una imagen por página**, metiendo ambas cosas en el contexto
   ([File input](https://learn.microsoft.com/azure/foundry/openai/how-to/responses#file-input)).
   Para Office no existe siquiera esa vía: ni Azure OpenAI ni Microsoft 365 Copilot la tienen.
   Copilot parsea el fichero a texto, lo indexa y manda al modelo solo los fragmentos relevantes
   —*"Microsoft performs all searches, and only relevant context is shared with the reasoning
   model"*—, que es exactamente lo que ya hace este sistema con el markdown de DI Layout.
2. **La necesidad real era extraer texto de esos formatos**, no pasar el documento. Y esa
   capacidad ya existe: DI Layout `2024-11-30` soporta DOCX, XLSX, PPTX y HTML.

Verificado empíricamente en dev el 2026-08-10 (ver "Evidencia"): **XLSX y PPTX ya se procesan de
punta a punta hoy**, sin cambio alguno. Lo que queda no es habilitar formatos, sino corregir dos
condiciones del orquestador que impiden que el prompt y el resumen se ejecuten con contenido.

## Evidencia

Tres documentos enviados al ingest de `srbappdevdocai` con prompt ad-hoc
("extrae en 3 puntos clave... `{contenido}`"), sin pasar por el cliente batch.

**Sin `expectedType`** (flujo con clasificación, Paso 2.8 activo):

| Documento | Markdown DI Layout | Prompt | Resultado |
| --- | --- | --- | --- |
| `prueba.xlsx` (33 KB, catálogo de tipologías) | Generado | gpt-5-mini, 9,5 s | Resumen fiel al contenido real |
| `prueba.pptx` (3 MB, ~20 diapositivas) | Generado | gpt-5-mini, 4,3 s | Resumen fiel al contenido real |
| `control.pdf` (1 página, texto) | Generado | **No ejecutado** | `NO_CLASIFICADO` → el prompt se salta |

DI Layout autodetectó ambos formatos Office a partir de los bytes; no hay lista blanca de formatos
en el backend que lo impida. El flujo de prompts en dev corre hoy sobre **gpt-5-mini**.

**Con `expectedType = resumen.documental`**: los tres documentos —incluido el PDF— ejecutaron el
prompt **sin contenido**. El modelo respondió *"No has incluido el documento. Por favor pégalo
aquí o súbelo"* y la ejecución cerró en `OK`. Causa: esa tipología tiene la extracción
deshabilitada y el Paso 2.8 solo se ejecuta cuando **no** hay `ExpectedType`
([DocumentProcessOrchestrator.cs:719-721](../../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs#L719-L721)).

Hallazgos secundarios de la misma prueba, fuera del alcance de este spec:

- Ambos Office se clasificaron como `nota.simple.1_4` con confianza 0,995 y 0,951 porque su
  contenido menciona "nota simple"; la extracción de Content Understanding falló después sobre
  Office y la ejecución cerró en `VALIDACION_CON_ERRORES`. La limitación de extracción a PDF se
  acepta como correcta.
- El PPTX de ~20 diapositivas reporta `Paginas = 1` (ver "Apunte C").

## Alcance

**Dentro:**

1. Ejecutar prompt y resumen aunque la clasificación termine en tipología desconocida.
2. Obtener contenido bajo demanda cuando se pide prompt o resumen y no hay markdown disponible.

**Fuera:**

- **Cliente `DocumentIA.Batch`**: mantiene su filtro `.pdf` (`MainViewModel.cs:476` y `505`) y su
  Content-Type fijo (`DocumentIaBackendClient.cs:132`). Decisión explícita: el batch es para PDF.
- **Limitación de extracción por formato.** El `ContentType: "application/pdf"` del modelo
  `nota.simple.1_4.azure-cu` acota la extracción estructurada a PDF, y se considera correcto. Nota:
  el fichero `config/extraction/models.json` es un seed antiguo; la fuente de verdad es
  `ModeloConfigs` en base de datos, donde el valor es el mismo.
- Modelo GPT nuevo y vía de documento directo (descartados arriba).
- La guarda de "no llamar al LLM sin contenido", que pertenece a otro spec (ver "Relación con...").

## Relación con el spec de transporte inline de DI

[`2026-08-10-di-transporte-inline-guarda-sin-contenido-design.md`](2026-08-10-di-transporte-inline-guarda-sin-contenido-design.md)
(también pendiente de aprobación) cubre en su §4 la **guarda de contenido vacío**: si no hay
markdown ni base64 utilizable, no se invoca al LLM, la actividad queda en `Failed` y la ejecución
cierra en `SIN_CONTENIDO_DOCUMENTO`. Ese comportamiento **no se redefine aquí**.

Las dos specs son complementarias y su orden natural es este:

| Spec | Papel |
| --- | --- |
| Transporte inline | Hace que DI Layout **funcione** en dev/pre, y **falla en firme** si aun así no hay texto |
| Esta spec | Hace que el markdown **se pida** cuando el prompt lo necesita, y que el prompt **se ejecute** aunque no haya tipología |

El Cambio B de esta spec es un intento de recuperación **antes** de que la guarda dispare: reduce
la frecuencia con la que se llega a `SIN_CONTENIDO_DOCUMENTO`, no la sustituye. Si ambas se
implementan, el orden recomendado es primero el transporte (desbloquea DI en dev, sin el cual esta
spec no se puede verificar de extremo a extremo) y después esta.

## Diseño

### Cambio A — El prompt no depende de que haya tipología (AB#100028)

Hoy, cuando la clasificación no resuelve una tipología, el orquestador escribe el estado y sale por
`return` sin llegar al Paso 4.5, que es donde vive el prompt. Hay dos salidas de ese tipo:

| Salida | Línea | Caso |
| --- | --- | --- |
| Tipología no resoluble (`KeyNotFoundException`) | [1160-1181](../../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs#L1160-L1181) | La clasificación devolvió un código que no existe en catálogo |
| Tipología `Desconocido` | [1184-1215](../../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs#L1184-L1215) | La clasificación no resolvió nada |

En ambas, antes del `return`, se ejecuta el prompt si la petición lo pide —`Instrucciones.Prompt`
informado o `ForzarResumenPorDefecto` activo— reutilizando la función local
`EjecutarPromptLibreAsync` ([294-342](../../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs#L294-L342)),
que ya está en ese ámbito.

El markdown se toma de `datosNormalizados["Markdown"]`, que el Paso 2.8 deja informado en este
camino (la prueba con `control.pdf` confirmó `MarkdownGenerado = true` pese al `NO_CLASIFICADO`).
La variable `markdownNormalizacion` **no** sirve aquí: se declara en la línea 1757, después de
estas salidas.

No hay cambio en el proveedor: `OpenAIPromptDataProvider` ya tolera tipología ausente mediante
`BuildFallbackTipologiaConfig` ([239-249](../../src/backend/DocumentIA.Functions/Services/OpenAIPromptDataProvider.cs#L239-L249)).

**Lo que no cambia:** el estado sigue siendo `NO_CLASIFICADO` y las confianzas siguen a cero. No se
está clasificando el documento; se está respondiendo a lo que la petición pidió. `DatosExtraidos`
pasa a llevar `ResultadoPrompt` y/o `Resumen`.

### Cambio B — Contenido bajo demanda para el prompt (AB#100029)

La condición del Paso 2.8 se deja intacta. En su lugar, dentro de `EjecutarPromptLibreAsync`, si el
markdown recibido viene vacío se intenta obtenerlo llamando a `ExtraerMarkdownLayoutActivity` con
el documento en base64 —el mismo transporte que ya usa el Paso 2.8, sin SAS— y se registra en la
traza mediante `RegistrarMarkdown` (`MarkdownGenerado` y `OrigenMarkdown`).

**Restricción de ámbito, verificada en el código.** El markdown obtenido así **no** se propaga a
`datosNormalizados`: esa variable se declara en la línea 563, después de la función local
`EjecutarPromptLibreAsync` (línea 294), y C# no permite capturar una variable declarada más tarde.
Sí son accesibles `entrada` (línea 102), `salida` (línea 111), `context` y `RegistrarMarkdown`
(línea 242). Propagarla exigiría pasar `datosNormalizados` como parámetro a los tres puntos de
llamada, y no aporta: en las salidas tempranas del Cambio A no hay pasos posteriores que la
consuman, y `Postproceso.Markdown` se rellena desde `markdownNormalizacion` (línea 1935), no desde
`datosNormalizados`. Se asume la limitación de forma consciente.

Se usa `entrada.Documento.Content.Base64` (documento completo) y no el recorte de clasificación,
que tampoco está en ámbito. Es lo correcto para un resumen: el recorte a 3 páginas existe para
abaratar la clasificación, no para representar el documento.

Se elige este punto y no la condición del Paso 2.8 porque aquí la llamada se paga **solo cuando hay
un prompt o un resumen que la necesita**. Ampliar el Paso 2.8 para que corriera también con
`ExpectedType` extraería markdown en todas las ejecuciones con tipología esperada, la mayoría de
las cuales no piden prompt.

Si tras el intento sigue sin haber contenido, se cede a la guarda del spec de transporte inline: no
se llama al modelo. Mientras esa guarda no esté implementada, el comportamiento observado hoy
—llamada a ciegas y respuesta inútil persistida como resumen— se mantiene, y por eso el orden de
implementación importa.

### Apunte C — Conteo de páginas en Office (a valorar, no comprometido) (AB#100030)

`Identificacion.Paginas` vale 1 para un PPTX de ~20 diapositivas y 0 en las ejecuciones con
`expectedType`. Afecta a la telemetría y a cualquier estimación de coste por página. La vía barata
es tomar `Paginas` del resultado de layout cuando venga informado, que es lo que ya se hace en
otros puntos del orquestador (líneas 745-746, 1704-1705, 1819-1821). Si el arreglo exige lógica por
formato —una hoja de cálculo no tiene "páginas" en el mismo sentido—, se difiere. Se documenta como
tarea aparte, sin comprometer su ejecución.

## Pruebas

**Unitarias** (`DocumentProcessOrchestratorTests`, que ya tiene el patrón de actividades simuladas):

- Clasificación devuelve `Desconocido` y la petición trae prompt ad-hoc → se invoca `PromptActivity`
  y `DatosExtraidos["ResultadoPrompt"]` queda informado, con el estado en `NO_CLASIFICADO`.
- Clasificación devuelve `Desconocido` **sin** prompt ni resumen en la petición → **no** se invoca
  `PromptActivity` (evita la regresión de llamar al modelo cuando nadie lo pidió).
- Tipología no resoluble con prompt ad-hoc → mismo resultado que el primer caso.
- Prompt solicitado con markdown vacío → se invoca `ExtraerMarkdownLayoutActivity` antes de
  `PromptActivity`, y el markdown obtenido llega en `PromptActivityInput.MarkdownExtraido`, con
  `DetalleEjecucion.MarkdownGenerado = true`.
- Prompt solicitado con markdown ya disponible → **no** se invoca `ExtraerMarkdownLayoutActivity`
  (la extracción bajo demanda no debe pagarse cuando ya hay contenido).

**E2E en dev**, repitiendo los envíos de la evidencia:

1. `expectedType = resumen.documental` + XLSX → el resumen contiene datos reales del documento.
2. Documento no clasificable + prompt ad-hoc → `NO_CLASIFICADO` **con** `ResultadoPrompt` informado.
3. Sin `expectedType` + XLSX/PPTX → sin regresión respecto a lo medido hoy.

## Riesgos

**Coste por llamada adicional a DI Layout.** El Cambio B añade una extracción por ejecución que
pida prompt o resumen y no tenga markdown. Acotado por construcción: solo ocurre en ese caso, y
sustituye a una llamada al LLM que hoy se hace igualmente pero sin contenido —es decir, hoy ya se
está pagando el prompt para obtener una respuesta inservible.

**Ejecuciones `NO_CLASIFICADO` que antes no llamaban al modelo ahora sí.** Es el comportamiento
pedido, pero cambia el coste de ese conjunto de ejecuciones. Conviene medir cuántas ejecuciones
históricas terminan en `NO_CLASIFICADO` con prompt en la petición antes de desplegar.

**Documentos Office grandes sin recorte.** El recorte por páginas es exclusivo de PDF
(`PdfRecorteService`), así que un XLSX o PPTX voluminoso viaja entero al prompt. DI acota en 8
millones de caracteres por fichero Office, muy por encima de la ventana del modelo. No se aborda
aquí porque no hay caso medido; si aparece, el precedente es el truncado por caracteres del flujo
`hybrid-tdn` (`MaxCharactersPerWindow = 32000`).

**Interacción con la guarda del otro spec.** Si esta spec se implementa sola, el caso "sin
contenido tras el intento" sigue llamando al modelo a ciegas. Si se implementa la guarda sola, el
caso `expectedType` sin layout pasa de resumen falso a error, sin recuperación. El valor completo
está en las dos.

## Verificación

1. Reprocesar en dev `prueba.xlsx` con `expectedType = resumen.documental` y confirmar que el
   resumen persistido describe el contenido real del fichero.
2. Enviar un documento que la clasificación no resuelva, con prompt ad-hoc, y confirmar
   `NO_CLASIFICADO` con `ResultadoPrompt` informado.
3. Confirmar en logs que `ExtraerMarkdownLayoutActivity` se invoca una sola vez por ejecución.
4. Repetir el envío sin `expectedType` de XLSX y PPTX y comparar con los resúmenes obtenidos hoy.

## Referencias

- Ejecuciones de la evidencia (dev, 2026-08-10): `7e7b000cf5da48809d086089f5fe72a0` (xlsx con
  `expectedType`, prompt vacío), `af3fea65262b4417ba286e58b0d8be95` (xlsx sin `expectedType`,
  resumen correcto), `e4e9dda9007e43d8a7bf484c955236ac` (pptx), `95b261ac66124fa1a3e25e8d67a495be`
  (pdf, `NO_CLASIFICADO` sin prompt).
- [Supported file types — DI Layout](https://learn.microsoft.com/azure/ai-services/document-intelligence/prebuilt/layout?view=doc-intel-4.0.0#development-options):
  Office soportado en `2024-11-30`; en DOCX/XLSX/PPTX se extrae el texto embebido y **no** se hace
  OCR de imágenes incrustadas.
- [Responses API — File input](https://learn.microsoft.com/azure/foundry/openai/how-to/responses#file-input):
  texto + una imagen por página, solo PDF.
- [Semantic indexing for Microsoft 365 Copilot](https://learn.microsoft.com/microsoftsearch/semantic-index-for-copilot):
  cómo Copilot procesa adjuntos.
