# Prerrequisitos del cutover de clasificación: Tdn1 por familia en la tipología virtual y caché de prompt de gpt-5.6-luna

Fecha: 2026-11-18 · PBI: AB#99934 · Rama de partida: `feature/99934-gpt56-luna-dev` (a763074) · Estado: borrador pendiente de aprobación.

## 1. Intención

La prueba en DEV de gpt-5.6-luna (spec `2026-11-17-migracion-clasificacion-gpt56-luna-design.md`, sección 10) dejó dos incógnitas que impiden decidir el cutover con datos limpios:

- **TDN1 "empeora" por un artefacto del backend, no del modelo.** En el camino de la tipología virtual el código que se persiste como `Tdn1` es lo que el modelo escribe antes de los dos puntos en `propuesta`. gpt-4.1-mini escribe la familia (`ESIN: ...`); luna escribe muchas veces el TDN2, con lo que el contrato persiste `Tdn1 = Tdn2 = FICH-19` y el harness lo cuenta como fallo de TDN1 (136 de 467 predicciones). Normalizado por familia, luna mejora TDN1 (65,95% frente a 56,32%).
- **El coste no baja porque la caché de prompt no entra.** Luna cachea el 7% de la entrada de la fase 1 donde gpt-4.1-mini cachea el 76% con el mismo prefijo. La documentación oficial explica el cambio de reglas en la familia GPT-5.6: existe `prompt_cache_key`, hay breakpoints explícitos, el breakpoint implícito por defecto cae en el **último** mensaje (el texto del documento) y las escrituras en caché se cobran. El backend no envía clave ni breakpoint.

Esta unidad corrige lo primero en el backend para cualquier modelo y comprueba en DEV, con una muestra de la golden, si la caché de luna se recupera dando forma a la petición. Con esos dos datos el usuario decide cutover, segunda candidata o no migrar. Decisión del usuario (2026-11-18): prerrequisitos primero, sin comprometer el cutover.

## 2. Alcance

Incluye:
- `Identificacion.Tdn1` = familia TDN1 resuelta en la fase 1 también cuando el resultado es una tipología virtual con TDN2 conocido. Independiente del modelo.
- Caché de prompt configurable por modelo en `ModeloConfigs` (clave de caché y breakpoint tras el system prompt), enviada solo a los modelos que la declaran, con lectura de `cache_write_tokens`.
- Humo directo contra el deployment para confirmar que el servicio acepta los campos con la api-version del SDK antes de tocar el backend.
- Medición en DEV sobre una muestra fija de ~50 documentos de la golden: control (gpt-4.1-mini), luna sin caché y luna con caché, con las consultas de la sección 10 versionadas en el repo.
- Cierre de los pendientes documentales M5 (consultas de medición versionadas) y M6 (manual § reasoningEffort).

No incluye (van al cutover, si se decide): umbral de confianza con luna (44 bajas frente a 10), cuota Data Zone en PRE/PRO, ADR, subida del SDK OpenAI, cambios en `prompt.gpt56-luna`, ninguna acción en PRE ni PRO, segunda candidata gpt-5.4-mini.

## 3. Estado actual verificado (2026-11-18)

Código (rama `feature/99934-gpt56-luna-dev`, a763074):
- `GptClasificarDataProvider.BuildVirtualTipologiaDetectada` (líneas 1064-1086) devuelve como código virtual lo anterior a `:` en `propuesta`; si no hay, el TDN2; si no, el TDN1. `BuildVirtualResult` (1046) lo pone en `TipologiaDetectada` y el TDN2 en `Tdn2Detectado`; en la llamada de la fase 2 (476-481) el proveedor ya conoce `tdn1Code`, la familia resuelta en la fase 1, pero no la propaga.
- `DocumentProcessOrchestrator` (1823-1870): si `ClasificacionParcial` y el resultado es virtual, `Identificacion.Tipologia = TipologiaFamilia = RunTipologia = tipologiaParcial` y `Identificacion.Tdn1 = tipologiaParcial` cuando no es `Desconocido`; `Identificacion.Tdn2 = Tdn2Detectado` se asigna antes (1818-1821).
- `CompleteChatAsync` (834-887): `SystemChatMessage(systemText)` (contenido como string), `UserChatMessage` con una parte de texto, `ChatCompletionOptions` con `ResponseFormat` JSON y `OpenAiModelCapabilities.ConfigureChatOptions` (temperatura, `max_completion_tokens` y `reasoning_effort` solo con esfuerzo válido). El cliente se crea **por llamada** en `CreateChatClient(model)` con `AzureOpenAIClientOptions { RetryPolicy = maxRetries 0 }`. El consumo sale de `UsoOpenAiMapper.Mapear(ChatTokenUsage)`: `TokensEntrada`, `TokensEntradaCache` (= `InputTokenDetails.CachedTokenCount`), `TokensSalida`, `TokensRazonamiento`. No hay campo de escrituras en caché.
- `ClassificationModelConfig` (Core, `ClassificationModelRegistryLoader.cs` 149-169): `Key`, `DeploymentName`, `ApiVersion` (solo la usan los proveedores de Document Intelligence), `Temperature`, `MaxTokens`, `ReasoningEffort` (nullable). Se carga del JSON de `ModeloConfigs.ConfiguracionJson`.
- SDK: `Azure.AI.OpenAI 2.9.0-beta.1` (última publicada en nuget.org) arrastra `OpenAI 2.9.1`, que no expone `PromptCacheKey` ni `cache_write_tokens`; sí expone un `Patch` (JSON) en al menos un tipo. `OpenAI 2.13` expone `PromptCacheKey`; `2.14` añade `cache_write_tokens`. Ninguna expone `prompt_cache_breakpoint`. El humo `scripts/migrations/gpt56-luna/smoke-chat.sh` usa la api-version `2025-04-01-preview`, la que el README atribuye al SDK.

Documentación oficial (Microsoft Learn, "Prompt caching", leída el 2026-11-18):
- Requisito: 1.024 tokens idénticos al inicio. Nuestra fase 1 envía ~18K de entrada por llamada.
- GPT-5.6 y posteriores: `prompt_cache_key` mejora los aciertos (misma clave para el mismo prefijo; por encima de ~15 peticiones/min por clave puede fallar), `prompt_cache_options.mode` `implicit` (por defecto, breakpoint en el último mensaje) o `explicit`, `prompt_cache_breakpoint: {"mode":"explicit"}` en un bloque de contenido (`text` en Chat Completions) marca el fin del prefijo reutilizable; `ttl` `30m` por defecto. La respuesta informa `cached_tokens` y `cache_write_tokens`. Las escrituras se cobran. Modelos anteriores devuelven 400 con esos parámetros. La caché funciona en Data Zone Standard; la hipótesis "es el SKU" pierde fuerza frente a la forma de la petición.

DEV: deployment `gpt-5.6-luna` 150K TPM DataZoneStandard, modelo 2026-07-09; filas `classification.gpt56-luna` y `prompt.gpt56-luna` activas sin `IsDefault`; DEV ejecuta `develop` (run 80153). Harness del Batch: `eval/run_golden_luna_dev.ps1` (sin commit, línea batch), `eval/golden.csv` 467 filas con `expected_tdn1`/`expected_tdn2`.

## 4. Restricciones

- Nada en PRE ni PRO. No se tocan las filas por defecto de DEV. No se leen `eval/runs/config-dev.json` ni `environments.json`.
- El trabajo sale de `feature/99934-gpt56-luna-dev` (develop no lleva el SDK beta ni `OpenAiModelCapabilities`) y no se integra en develop hasta la decisión de cutover. Rama nueva `feature/<task>-prerrequisitos-cutover` desde la rama luna cuando existan las tasks (propuesta, ver 6.7).
- gpt-4.1-mini no puede recibir los parámetros nuevos: todo lo de caché va detrás de la configuración por modelo y por defecto está apagado.
- Español; sin coautores ni modelos; `AB#` en cada commit; escrituras en ADO, pipeline y push con sí explícito. Sesiones de 20-40 minutos: la medición se hace en una sesión propia.

## 5. Enfoques considerados

Normalización de Tdn1: una sola forma razonable. El proveedor ya tiene la familia de la fase 1; se propaga al contrato. No se consulta el catálogo ni se pide nada al modelo.

Prueba de caché (decisión del usuario 2026-11-18: la 1):
1. **Inyección en el JSON sobre el cliente actual, sin cambiar dependencias.** Solo para modelos marcados, añadir `prompt_cache_key`, `prompt_cache_options` y el `prompt_cache_breakpoint` al final del system prompt, y leer `cache_write_tokens` del cuerpo crudo. Aislado en una clase, testeable sobre el cuerpo serializado. Rápido y reversible. Elegido.
2. Subir `OpenAI` a 2.14 por referencia directa por encima de `Azure.AI.OpenAI 2.9.0-beta.1`: `PromptCacheKey` y `cache_write_tokens` tipados; el breakpoint seguiría por `Patch`; el cambio arrastra embeddings, prompt y fallback y expone a incompatibilidades con el paquete beta. Mejor si luna se adopta; peor para una prueba.
3. Solo clave de caché, sin breakpoint: menos código; si la causa es el breakpoint implícito en el último mensaje, no recupera la caché y hay que repetir.

## 6. Diseño

### 6.1 Tdn1 por familia en la tipología virtual

- `ResultadoClasificacion` gana `Tdn1Detectado` (string?, familia TDN1 resuelta en la fase 1). `BuildVirtualResult` la recibe y la rellena en **todos** los caminos del proveedor GPT donde ya existe `tdn1Code`: fase 2 sin TDN2 parseable, fase 2 con TDN2 sin tipología publicada y el error de parseo de la fase 2. En el camino `tdn1_virtual_propuesta` (la fase 1 no resolvió familia) no hay familia que poner y queda nulo; tampoco hay `Tdn2Detectado`, así que ahí no puede darse `Tdn1 = Tdn2`. Los caminos `tdn1_extraido_de_propuesta`, `global_fallback_final` y `global_fallback_baja_confianza` nacen en `ConfigurableClasificarDataProvider` con un código TDN1 ya extraído en `TipologiaDetectada`; no cambian. Decisión mía: no se deriva la familia del prefijo del TDN2 (`FICH-19` → `FICH`) cuando falta `tdn1Code`, porque ese prefijo es una convención del catálogo, no una garantía.
- Orquestador, rama virtual (1823-1975, que termina con `return salida`): `Identificacion.Tdn1 = Tdn1Detectado` si viene; si no, el comportamiento actual (`tipologiaParcial` salvo `Desconocido`). `Tipologia`, `TipologiaFamilia`, `RunTipologia`, `TipologiaNombre` y `PropuestaTipologia` no cambian: son lo que ven hoy los consumidores. Decisión mía: corregir solo `Tdn1`; cambiar `TipologiaFamilia` sería un cambio de contrato que no hace falta para la medición. El bloque de nivel `TDN1` (2034-2070), que usa `Identificacion.Tdn1` como `TipologiaId`, queda después del `return` de la rama virtual y solo lo alcanza la clasificación parcial no virtual, que esta unidad no toca: verificado el 2026-11-18 en el código.
- Efecto por modelo: con gpt-4.1-mini (`ESIN: ...`) el valor es el mismo que hoy; con luna (`FICH-19: ...`) pasa de `FICH-19` a `FICH`. `Tdn2` ya era `FICH-19`.
- Tests unitarios: proveedor (propuesta `FICH-19: ...` con `tdn1Code` `FICH` → `Tdn1Detectado` `FICH`, `Tdn2Detectado` `FICH-19`, `TipologiaDetectada` `FICH-19`) y orquestador (virtual con `Tdn1Detectado` → `Tdn1` familia, resto igual; sin `Tdn1Detectado` → comportamiento actual, los tests existentes 1524-1650 siguen en verde).

### 6.2 Caché de prompt configurable por modelo

- `ClassificationModelConfig` gana `PromptCache` (objeto, nullable): `Enabled` (bool, por defecto false), `KeyPrefix` (string, por defecto `documentia-clasif`), `Breakpoint` (bool, por defecto true), `Ttl` (string, por defecto `30m`). Se lee del JSON de `ModeloConfigs` como el resto; sin la clave, todo apagado y el cuerpo de la petición es idéntico al actual (test de regresión sobre el cuerpo serializado).
- Clave por petición: `{KeyPrefix}:{operation}:{hash12(systemText)}`, donde `hash12` son los 12 primeros hex del SHA-256 del system prompt. Misma clave para el mismo prefijo (requisito de la documentación), rotación automática si cambia el prompt, y claves distintas para la fase 1 y para cada familia de la fase 2. A `ParallelQueries` 2 la fase 1 queda en ~5 peticiones/min por clave, bajo el límite orientativo de 15.
- Petición: `prompt_cache_key`, `prompt_cache_options: {"mode":"implicit","ttl":"30m"}` y el system prompt como **array de partes** con `prompt_cache_breakpoint: {"mode":"explicit"}` en la parte de texto. Decisión mía: mantener `implicit` (el breakpoint del último mensaje no estorba a la lectura del prefijo y no dependemos de un único breakpoint). El constructor puede pasar a `explicit` si el humo de 6.3 muestra que `implicit` escribe la caché del documento completo con coste apreciable, y lo deja escrito.
- Mecanismo de inyección (lo que queda al constructor, con orden de preferencia): (a) el `Patch` JSON del SDK `OpenAI 2.9.1` si `ChatCompletionOptions` y `ChatMessage` lo exponen; (b) si no, una `PipelinePolicy` por llamada registrada en `AzureOpenAIClientOptions` en `CreateChatClient` (el cliente ya se crea por llamada, así que la política puede llevar la clave y el flag de breakpoint de esa petición) que reescribe el cuerpo. En ambos casos la lógica vive en una clase propia (`PromptCacheRequestShaper` o similar) con tests sobre el JSON resultante: con `PromptCache` apagado el cuerpo no cambia; encendido, aparecen los tres campos y el system prompt pasa a array de partes.
- Respuesta: `ConsumoIA` gana `TokensEscrituraCache` (int?), leído de `usage.prompt_tokens_details.cache_write_tokens` del cuerpo crudo (`GetRawResponse()`), nulo si no viene. Se persiste con el resto de `CostesIA`; es un campo anulable nuevo y no exige subir `Version`. Monitor y Admin no lo muestran en esta unidad (decisión mía: la medición lee la BD; mostrarlo es cosa del cutover).
- Fórmula de coste: la tarifa por modelo gana un precio opcional de escritura en caché y la fórmula lo aplica a `TokensEscrituraCache` (tests de la fórmula con y sin precio). Si la tarifa no lo define, el coste de escritura es 0 y se avisa en el log una vez por modelo. Sin esto el ratio de la pasada 3 saldría subestimado.
- Precio de escritura: paso explícito del plan, antes de la pasada 3. Primero se busca en la página oficial de precios de Azure OpenAI para gpt-5.6-luna (Data Zone); el usuario lo confirma y entonces se aplica en `02-dev-tarifa-gpt56-luna.sql` (o un script 05). Si no está publicado, la pasada 3 se informa con dos cifras: coste sin escrituras y coste con una cota superior igual al precio de entrada normal por token escrito; el criterio de 6.5 se evalúa con la cota.
- Base de datos de DEV: `scripts/migrations/gpt56-luna/03-dev-promptcache-gpt56-luna.sql` añade `PromptCache` a `classification.gpt56-luna` (y solo a esa fila), idempotente, con backup previo como los scripts 01 y 02, y `04-dev-promptcache-off-gpt56-luna.sql` para apagarlo (necesario para la pasada "luna sin caché" de 6.4). `prompt.gpt56-luna` no se toca.

### 6.3 Humo directo contra el deployment (antes del backend)

`scripts/migrations/gpt56-luna/smoke-cache.sh`, hermano de `smoke-chat.sh`: dos peticiones idénticas de ~1.500 tokens al deployment `gpt-5.6-luna` de DEV con la api-version del SDK (`2025-04-01-preview`) y **el mismo cuerpo que generará el backend**: `response_format` `json_object`, `reasoning_effort` `none`, `max_completion_tokens`, system prompt como array de partes con el `prompt_cache_breakpoint`, mensaje de usuario como array de partes, `prompt_cache_key` y `prompt_cache_options`. Imprime `cached_tokens` y `cache_write_tokens` de cada respuesta. Resultado esperado: 200 en ambas y `cached_tokens` > 0 en la segunda. Si el servicio devuelve 400, se repite con `/openai/v1/chat/completions`; si solo funciona ahí, se para y se informa (cambiar la api-version del SDK es decisión aparte). Consume unos 3.000 tokens. Es una **puerta bloqueante**: T2 no empieza hasta que el humo pasa; T1 no depende de él.

### 6.4 Medición en DEV

- Muestra fija: `eval/golden-muestra-cache.csv` en el Batch (línea batch), generada por un script determinista `eval/build_muestra_cache.py` a partir de `golden.csv` (filas con `validated = True`; las 10 familias `expected_tdn1` con más filas, desempate alfabético; dentro de cada familia las 5 primeras por `file_name`; cabecera con el SHA-256 del `golden.csv` de origen). Se versiona en el Batch, en una rama propia, junto con `run_golden_luna_dev.ps1` (pendiente heredado sin commit) y un parámetro `-Golden` del script para apuntar a la muestra; el commit del Batch cita el AB# de T3 y se anota en el traspaso de core como toque a otra línea. Cómo el harness filtra por la muestra (manifiesto, subcorpus o parámetro) lo decide el constructor y lo deja en el README del harness.
- Despliegue: pipeline 802 sobre la rama de trabajo (sí explícito), misma mecánica que la prueba anterior; al terminar, 802 sobre `develop`.
- Tres pasadas secuenciales el mismo día, `ParallelQueries` 2, `MaxPages` 10, `ClassificationOnly`, `ForceReprocess`: (1) control `Model = auto`; (2) luna con `PromptCache` apagado (script 04); (3) luna con `PromptCache` encendido (script 03). La 2 va antes que la 3 para que la 3 no herede caché. Coste total estimado del orden de 1 €.
- Comprobación del estado antes de las pasadas 2 y 3: tras aplicar el script, una ejecución de sondeo de un documento (fuera de la muestra, `SubmittedBy` propio) y lectura en BD de `TokensEscrituraCache` de su fase 1: nulo o ausente en la pasada 2, con valor en la pasada 3. Hasta que el sondeo dé lo esperado (refresco del registro de modelos, hasta 5 min) no se lanza la pasada. El sondeo no entra en las métricas.
- Interferencias: antes de empezar se mira en Azure Monitor el consumo del deployment luna en la última hora y se avisa en el hilo; durante las pasadas, las consultas filtran por `SubmittedBy`, y cualquier 429 se cuenta por pasada. Si aparecen 429 persistentes por tráfico ajeno, la pasada se repite en otro momento en vez de darse por válida.
- Consultas versionadas en `scripts/migrations/gpt56-luna/medicion/*.sql` (coste y tokens por pasada y fase, cachés leída y escrita, tipologías virtuales y `Tdn1 = Tdn2`, latencia p50/p95), parametrizadas por `SubmittedBy` y ventana, como en la sección 10 de la spec anterior. Cierra el pendiente M5.
- Resultado en una sección 10 de esta spec con tabla por pasada: documentos, % entrada en caché fase 1 y fase 2, `cache_write_tokens` medios, coste medio por documento y ratio frente a control, HTTP 400/429, p50/p95, tipologías virtuales con `Tdn1 = Tdn2` (debe ser 0 en 2 y 3) y TDN1/TDN2 por harness (n = 50, se informa sin test de significación).

### 6.5 Criterios de éxito

- Humo 6.3: 200 en las dos llamadas y `cached_tokens` > 0 en la segunda.
- Normalización: 0 resultados virtuales con `Tdn2` informado y `Tdn1 = Tdn2` en las pasadas 2 y 3; suite unitaria en verde; cuerpo de la petición sin cambios con `PromptCache` apagado.
- Caché: pasada 3 con al menos el 50% de la entrada de la fase 1 en caché (control 76%). Coste medio por documento de la pasada 3 frente a la 1: ≤ 0,5 cumple el 6.6 original; entre 0,5 y 0,7 es mejora parcial y la decisión es del usuario; > 0,7 con caché recuperada significa que manda la tarifa, no la caché, y se informa así.
- Cero HTTP 400 en las tres pasadas.

### 6.6 Impacto documental previsto

Técnica: `docs/03_DISENO_TECNICO_DETALLADO.md` (tipología virtual: `Tdn1` por familia; caché de prompt por modelo). Funcional: `docs/05_MANUAL_USO_CONFIGURACION.md` (clave `PromptCache` de `ModeloConfigs`; y cierre de M6 en § reasoningEffort: el aviso por valor inválido se registra también en modelos clásicos y en prompt/resumen el límite sale de `promptConfig.MaxTokens` / `max(resumenConfig, promptConfig)`). Explotación: `scripts/migrations/gpt56-luna/README.md` (scripts 03, 04, humo y consultas). Arquitectura: ninguno, el ADR se decide en el cutover.

### 6.7 Work items (propuesta; se crean con sí explícito, bajo AB#99934)

- T1 · Tdn1 por familia en la tipología virtual (Core, Functions, tests).
- T2 · Caché de prompt configurable por modelo: configuración, inyección, `TokensEscrituraCache`, tarifa, scripts 03 y 04, tests.
- T3 · Humo directo y medición en DEV sobre la muestra (muestra y harness en el Batch, precio de escritura, despliegue, sondeos, tres pasadas, consultas versionadas, sección 10).
- T4 · Documentación (6.6) y README.

AB#99938 y AB#99939 son tasks del plan gpt-5-mini de julio (app settings y cutover a gpt-5-mini); no encajan en esta unidad y se dejan como están.

## 7. Lo que queda al constructor

- `Patch` del SDK frente a `PipelinePolicy` (6.2), tras comprobar la API real de `OpenAI 2.9.1`.
- Cómo leer `cache_write_tokens` sin romper el mapeo tipado (cuerpo crudo frente a extensión del `Usage`).
- Nombres de clases y campos; dónde vive el hash de la clave.
- Cómo el harness consume la muestra; nombre del `SubmittedBy` de cada pasada.
- `implicit` o `explicit` en `prompt_cache_options.mode`, con el dato del humo.

## 8. Riesgos

- El servicio rechaza los campos con `2025-04-01-preview` (se detecta en 6.3 antes de escribir backend).
- El SDK serializa el system prompt como string; el breakpoint exige array de partes. Hay que construir el mensaje con `ChatMessageContentPart` o reescribirlo en la política.
- Con n = 50 la primera llamada por clave escribe caché y cobra; el ratio de coste de la pasada 3 sale algo pesimista. Se informa el coste de escritura aparte.
- Cambiar el system prompt entre pasadas rota la clave: no se toca ninguna plantilla durante la medición.
- DEV queda en la rama de trabajo durante la medición (unos 30 minutos); se avisa si alguien lo necesita.
- El `develop` local de core va 4 commits por delante de origin sin push (pendiente heredado); no afecta a esta rama, que sale de la rama luna.

## 9. Estado

Borrador (2026-11-18) revisado por un constructor (subagente): ocho preguntas, todas resueltas en el documento como decisiones del constructor principal (6.1 caminos virtuales y bloque de nivel TDN1; 6.2 fórmula de coste, precio de escritura y Monitor; 6.3 humo con el cuerpo del backend como puerta bloqueante; 6.4 muestra versionada en el Batch, sondeos antes de cada pasada e interferencias). Pendiente: aprobación del usuario y entonces `superpowers:writing-plans`.
