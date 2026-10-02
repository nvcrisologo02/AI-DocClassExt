# ADR-002: Clasificador híbrido por embeddings con GPT de respaldo en el orquestador
Fecha: 2026-10-01 · Estado: propuesta · Work Item: AB#100779 · Session ID: sin Session ID (el proyecto no declara `Gobernanza: activa`)

## Contexto y problema

El spike AB#100719 entrenó un clasificador A (regresión logística sobre `text-embedding-3-large`) y AB#100775 lo midió frente al GPT de producción sobre cal (1.576 documentos): A acierta el 87,2 % en TDN1 y el GPT el 83,9 %. El híbrido medido fuera de línea (INFORME §12 de DocumentIA.Batch) con umbral 0,6 llega al 89,5 % en TDN1 y al 64,2 % en TDN2, mejor que cada componente con significación, y ahorra el 86 % de las llamadas GPT de clasificación.

Hay que decidir cómo integrar A en el orquestador: dónde corre la inferencia, en qué formato viaja el modelo entre entornos, cómo se enruta entre A y el GPT, cómo se activa sin redespliegue y qué pasa cuando los embeddings fallan. El umbral de operación no se decide aquí: se fija con datos de una fase de sombra en DEV.

## Opciones consideradas

- A. Pesos exportados a JSON (manifiesto más matrices) en el contenedor de artefactos de IA de cada entorno; inferencia en C# dentro de Functions con `EmbeddingClient` de `Azure.AI.OpenAI`, ya referenciado.
- B. Servicio Python aparte (Function Python o contenedor) que carga el modelo sklearn y expone la predicción.
- C. Exportar a ONNX y ejecutar con `Microsoft.ML.OnnxRuntime` en Functions.

## Decisión

Se adopta A. El modelo A se exporta desde DocumentIA.Batch a un artefacto JSON versionado con manifiesto, se publica en el contenedor `documentai` del storage de aplicación del entorno y se promociona DEV → PRE → PRO por el cauce de ADR-001. En Functions, una activity nueva previa al GPT calcula el embedding, infiere en C# y devuelve una decisión; el GPT actual responde en todo lo que A no contesta. El modo (`off`, `sombra`, `hibrido`), los umbrales y la versión del artefacto se leen de una fila de `ModeloConfigs` de tipo `Embeddings`, sin redespliegue. Se arranca en `sombra` en DEV.

## Motivos

- La inferencia de una regresión logística es un producto matriz-vector más softmax: no justifica una dependencia nativa ni un servicio aparte. La paridad con sklearn se fija con un test sobre vectores exportados.
- `ModeloConfigs` con caché de 5 minutos es el mecanismo del sistema para configurar modelos por entorno sin redesplegar; el clasificador entra por la misma puerta y se resuelve el endpoint con `AiEndpointResolver`, así que PRE y PRO usan su IA propia.
- El artefacto en blob sigue la convención de los demás artefactos de IA y encaja en el pipeline ai-artifacts (AB#100781) sin diseñar otro cauce.
- Una activity secuencial previa al GPT da un único flujo para sombra e híbrido: en sombra siempre deriva, en híbrido contesta por confianza. El coste es la latencia de una llamada de embeddings.
- La fase de sombra persiste la predicción y la confianza de A junto a las del GPT sin alterar el resultado, y es la única forma de fijar el umbral con datos reales del entorno en lugar de con cal.

## Consecuencias

- Código nuevo en Core (loader del artefacto, clasificador puro, decisor puro, loader de configuración) y en Functions (proveedor, activity, mapper de uso de embeddings). Enum `TipoModelo` gana `Embeddings = 5` y el Admin lo expone.
- El contrato de salida crece con campos opcionales (`ResultadoClasificacion.Embeddings`, una entrada en `DetalleProveedores`, `RamaClasificacion`). Ningún campo existente cambia. En `sombra` el resultado al cliente es idéntico al actual salvo los añadidos.
- El coste de embeddings se registra como `ConsumoIA` con `Actividad = Clasificar` y `Modelo` = nombre del deployment, con línea propia en `tarifas.ia`; suma en `ClasificacionEur`.
- Ante cualquier fallo de embeddings (artefacto, 429, timeout, respuesta inválida) la activity devuelve `DerivarGpt` sin excepción: el GPT clasifica como hoy. Un circuito por `endpoint|deployment` evita llamar con la cuota agotada.
- La clasificación restringida tiene modo y umbrales propios y depende de un manifiesto que declara las tipologías cubiertas y si el modelo está calibrado; v1 no lo está, así que el híbrido restringido queda cerrado por construcción hasta un modelo calibrado (AB#100780).
- Insertar una activity en el orquestador obliga a desplegar la Fase A con las orquestaciones drenadas (como en AB#100176). PRE y PRO reciben el código con la fila en `off`.
- Se asume el texto de entrada del spike (markdown con espacios colapsados, 24.000 caracteres) para que la confianza de la sombra sea comparable con cal.
- Este ADR pasa a `aceptada` al cerrar la Fase A (sombra operativa en DEV). Si la sombra desaconseja el híbrido, el ADR se sustituye por otro y el código queda en `off`.

## Alternativas descartadas y por qué

- B, servicio Python: paridad gratis con el spike, pero un componente nuevo que desplegar, autenticar y vigilar en tres entornos, un salto de red más en el camino crítico de clasificación y un pipeline de despliegue propio. Desproporcionado para la inferencia que hay que hacer.
- C, ONNX: paridad por herramienta y formato estándar, pero dependencia nativa pesada en el plan Linux de Functions y un modelo compuesto (TDN1 más un modelo TDN2 por familia) que habría que ensamblar en varios grafos. No aporta nada sobre A para una regresión logística.
- Guardar los pesos en `ModeloConfigs.ConfiguracionJson`: del orden de 8 MB por versión en una tabla de configuración leída por varios loaders. Se guarda solo la referencia al blob.
- Paralelizar sombra y GPT: ahorra la latencia de la llamada de embeddings a cambio de dos flujos distintos en el orquestador. Se prefiere un único flujo.
