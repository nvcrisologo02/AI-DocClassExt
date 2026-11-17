# Diseño: prueba en DEV de gpt-5.6-luna como sustituto de gpt-4.1-mini en clasificación, prompt libre y resumen

Fecha: 2026-11-17 · Línea: core · Work item: AB#99934 (PBI "Migración de los deployments gpt-4.1-mini/gpt-4o antes de su retirada: elegir modelo destino vigente", New, prioridad 3) · Estado: aprobada 2026-11-17 · Tasks: AB#101370 (deployment), AB#101371 (código), AB#101372 (BD y tarifa), AB#101373 (golden), AB#101374 (prompt y resumen)

## 1. Intención

El deployment `gpt-4o-mini` sirve hoy el modelo gpt-4.1-mini para la clasificación GPT (TDN1/TDN2 cuando Document Intelligence no decide), el prompt libre y el resumen. Microsoft retira gpt-4.1-mini el 2027-04-14. Hay que elegir el modelo destino y comprobar en DEV, con la golden de 467 documentos, que no pierde calidad y qué cuesta, antes de decidir el cutover.

Por qué gpt-5.6-luna: es el modelo pequeño vigente de la familia 5.6 (retirada 2028-01-11), está disponible en West Europe con SKU Data Zone Standard (residencia EU, como el `gpt-5-mini` actual) y a precio de lista Data Zone sale en torno a 0,24 c€ por documento frente a los 0,65 c€ medidos hoy. gpt-5.4-nano queda descartado porque no tiene Data Zone ni en West Europe ni en Sweden Central. gpt-5.4-mini (Data Zone, retirada 2027-09-21) queda como segunda opción si luna falla la puerta de calidad.

Por qué ahora y no con gpt-5-mini: el A/B de agosto (gpt-5-mini frente a gpt-4.1-mini) salió sin diferencia significativa de calidad y con el doble de coste, porque el código no puede enviar `reasoning_effort` ni `max_completion_tokens` y el modelo razona por defecto. Esa limitación es la que esta unidad resuelve.

## 2. Alcance

Objetivos:
- Que el backend pueda pedir a un deployment de razonamiento `reasoning_effort` (en concreto `none`) y un tope de salida con `max_completion_tokens`.
- Deployment `gpt-5.6-luna` en la cuenta de IA de DEV y filas en `ModeloConfigs` para clasificación y prompt, seleccionables solo por clave (no entran en la resolución por defecto).
- Medición en DEV con la golden (467) frente a una pasada de control del modelo actual el mismo día, con McNemar, coste por documento y latencia.
- Comprobación funcional del prompt libre y del resumen con el modelo nuevo.
- Tasks bajo AB#99934 que dejen el trabajo trazado.

No objetivos (quedan para otra unidad):
- El fallback GPT de extracción (`GptFallbackExtraerDataProvider`): se revisará junto con la extracción. Compila con el SDK nuevo pero no cambia de comportamiento.
- El cutover (cambiar las filas por defecto) en DEV, PRE o PRO, los app settings legacy del pipeline (Task AB#99938) y el borrado de `gpt-4o-mini`.
- Decidir el modelo definitivo: esta unidad produce la evidencia; la decisión y el ADR vienen después.
- El deployment `gpt-4o` que nombra el título del PBI.

Anti-objetivos (sería un fracaso aunque funcione):
- Medir con razonamiento por defecto: repetiría el A/B de agosto y no diría nada del coste real.
- Comparar contra una baseline antigua: la configuración de DEV (catálogo, prompts, tipologías) ha cambiado desde que se grabó BASELINE-GPT4OMINI-DEV.
- Sacar la inferencia de la zona de datos EU (SKU Global Standard).
- Cambiar el modelo que usa DEV por defecto durante la prueba.

## 3. Estado actual verificado (2026-11-17)

- Cuenta de IA de DEV: `srbaisrv01devdocai`, grupo `SRBRGDEVDOCSAI`, suscripción Core Desarrollo (`8764f9ff-fe37-4c03-bde9-6294622bef6d`), región West Europe. Deployments: `gpt-4o-mini` (gpt-4.1-mini, Standard, 150), `gpt-5-mini` (Data Zone Standard, 50), `gpt-4o`, `gpt-4.1-715420`, `gpt-4.1-mini-622960`, `text-embedding-3-large-030358`.
- `gpt-5.6-luna` versión `2026-07-09` está disponible en West Europe con SKU `DataZoneStandard` (también Global Standard y provisionados). La cuota Data Zone disponible no se ha podido leer (`az cognitiveservices usage list` devuelve vacío); se sabrá al crear el deployment.
- Selección de modelo: clasificación por `Instrucciones.Classification.Model` → fila de `ModeloConfigs` Tipo 0 por clave (`GptClasificarDataProvider`, "auto" = fila por defecto). Prompt libre y resumen por `PromptConfig.ModelKey` → fila Tipo prompt (`OpenAIPromptDataProvider`), con `PromptDefaults.ModelKey` como valor por defecto (`default.gpt4o-mini`). Las configuraciones se deserializan con System.Text.Json sin distinguir mayúsculas y las propiedades desconocidas se ignoran.
- Configuraciones: `ClassificationModelConfig` (Core, `ClassificationModelRegistryLoader.cs`) tiene Temperature y MaxTokens; `PromptModelConfig` (`PromptModelRegistryLoader.cs`) no, los recibe del `PromptConfig`. Ninguna tiene esfuerzo de razonamiento.
- `OpenAiModelCapabilities.ConfigureChatOptions` (Functions) detecta la familia de razonamiento por prefijo del deployment (`gpt-5*`, `o<n>*`) y para ella no envía ni `temperature` ni límite de salida. Tiene tests en `OpenAiModelCapabilitiesTests.cs`. Lo llaman `GptClasificarDataProvider` (clasificación), `OpenAIPromptDataProvider` (prompt libre y resumen) y `GptFallbackExtraerDataProvider` (extracción, fuera de alcance).
- SDK: `DocumentIA.Functions.csproj` referencia `Azure.AI.OpenAI` `2.*`, que resuelve 2.1.0 (última estable, con OpenAI 2.1.0). Ningún otro proyecto referencia el SDK. En NuGet la última versión es `2.9.0-beta.1` (depende de `OpenAI 2.9.1` y `Azure.Core 1.51.1`); la línea estable sigue en 2.1.0.
- Harness: `DocumentIA.Batch.Evaluation` (`run`, `compare`, `report`), config `eval/runs/config-dev.json` (contiene la function key de DEV, gitignored; `Provider`/`Model` "auto"), script `eval/run_cal_dev.ps1` como referencia de invocación desacoplada. Runs de referencia: `eval/runs/BASELINE-GPT4OMINI-DEV` (TDN1 55,89 %, TDN2 33,40 %) y la pasada más reciente del modelo actual, `20261015-080441-v1.1.0-pre`. `eval/runs` no está en git.
- Precios Data Zone de lista para gpt-5.6-luna leídos de la Retail Prices API el 2026-11-17: 0,20 € entrada, 0,02 € entrada cacheada y 1,19 € salida por millón de tokens. Tarifa efectiva de la app en `tarifas.ia` (fila Tipo 4, clave `tarifas.ia`): el catálogo se amplía con líneas nuevas y `VigenteDesde`, nunca editando las existentes, y la clave es el nombre del deployment.
- Memoria de la sesión con el análisis de coste y calidad: `migracion-modelo-clasificacion-candidatos.md` (memoria persistente del proyecto, fuera del repo).

## 4. Restricciones

- Escrituras en Azure DevOps y creación de recursos en Azure (el deployment) solo con un sí explícito en el hilo, mostrando antes el comando o payload. Nada se ejecuta contra PRE ni PRO.
- Residencia EU: SKU Data Zone Standard. Si no hay cuota Data Zone para la familia 5.6 en West Europe, se para y se pide; no se cae a Global Standard.
- No se renombran claves de `ModeloConfigs` ni se tocan las filas por defecto. Scripts numerados e idempotentes en `scripts/migrations/<tema>/`, con backup previo, siguiendo el patrón de `scripts/migrations/gpt5-mini/`. Las migraciones ya aplicadas no se editan.
- Build sin warnings nuevos (está a cero desde AB#100288), tests afectados en verde y `dotnet format --verify-no-changes` limpio antes de dar nada por terminado.
- Español en todo lo generado; sin coautores ni menciones de modelos de IA generativa en commits, código ni docs; `AB#99934` o la task hija en cada commit.
- Sesiones de 20-40 minutos: el plan se trocea en fases que quepan en una sesión cada una.

## 5. Enfoques considerados

El punto de decisión es cómo emitir `reasoning_effort` y `max_completion_tokens`, que el SDK estable 2.1.0 no sabe mandar.

1. **Subir `Azure.AI.OpenAI` a `2.9.0-beta.1`** (arrastra OpenAI 2.9.1). Propiedades tipadas (`ReasoningEffortLevel`, `MaxOutputTokenCount` serializado como `max_completion_tokens`), cambio de código pequeño en el helper y en los tres proveedores. Es lo que el README de la migración de gpt-5-mini dejó recomendado. Contras: dependencia en beta que acabará en PRO, posibles cambios de API en los otros usos del SDK y hay que confirmar que la serialización contra el api-version de Azure incluye ambos campos.
2. **Quedarse en 2.1.0 e inyectar los dos campos con una política de pipeline** sobre el cuerpo JSON, solo para los deployments de razonamiento. Sin cambio de dependencias, una clase y sus tests. Contras: manipula el cuerpo en el cable, duplica lo que el SDK hará y en el cutover se volvería a cambiar.
3. **Pasar al paquete `OpenAI` estable (2.15) contra el endpoint v1 de Azure** y retirar `Azure.AI.OpenAI`. Dirección oficial de Microsoft y único camino con paquete estable y API tipada. Contras: rehace la creación de clientes y la autenticación con Entra en los tres proveedores y en extracción, que queda fuera de alcance.

**Elegido: 1**, decisión del usuario el 2026-11-17. Motivo: es lo que se acabará desplegando, el cambio es pequeño y se verifica con un test que inspecciona el cuerpo serializado. Si esa verificación falla, se cae al enfoque 2 sin rehacer el resto (deployment, filas, tarifa y medición no dependen de él).

## 6. Diseño

### 6.1 Infraestructura en DEV

- Deployment `gpt-5.6-luna` en `srbaisrv01devdocai`: modelo `gpt-5.6-luna`, versión `2026-07-09`, SKU `DataZoneStandard`, capacidad 50 (misma que `gpt-5-mini`; ajustable con variable de entorno). Nombre del deployment igual al del modelo, como `gpt-5-mini`, porque la tarifa y el helper de capacidades se apoyan en ese nombre.
- Script `scripts/migrations/gpt56-luna/create-gpt56-luna-deployment.sh`, derivado de `gpt5-mini/create-gpt5-deployments.sh`, parametrizado por entorno (`ENV=dev|pre|pro` fija suscripción, grupo y cuentas) y, como aquel, idempotente y con comprobación previa de que el modelo y el SKU existen en la región. En esta unidad solo se ejecuta con `dev`.
- Ejecución: con sí explícito, tras mostrar el comando. Si la creación falla por cuota Data Zone, se registra la respuesta y se para (la petición de cuota es del usuario).
- Comprobación real tras crear el deployment: una llamada de chat directa (curl o script) a `gpt-5.6-luna` con `reasoning_effort: "none"` y `max_completion_tokens`, para confirmar que el servicio acepta ambos campos con ese modelo y api-version. El test de serialización de 6.2 solo prueba el cuerpo que genera el SDK, no la aceptación del servicio. (Decisión del diseño a partir de la revisión del constructor.)

### 6.2 Código (Functions y Core)

- `DocumentIA.Functions.csproj`: `Azure.AI.OpenAI` pasa de `2.*` a la versión fija `2.9.0-beta.1`. La subida del SDK (y las adaptaciones de compilación que exija) va en un commit propio, separado del soporte de `ReasoningEffort`, para poder revertir uno sin el otro. Si luna no pasa la puerta, el SDK se mantiene: hace falta igual para gpt-5.4-mini o cualquier otro modelo de razonamiento. (Decisión del diseño a partir de la revisión del constructor.) Se quita el comodín para que una restauración no salte de versión sin que nadie lo decida. Si el SDK 2.9 rompe compilación en otros usos (extracción, otros proveedores), se adaptan con el mínimo cambio y sin alterar comportamiento; cualquier cambio de comportamiento observado se anota en la spec como riesgo.
- `ClassificationModelConfig` y `PromptModelConfig`: nueva propiedad `ReasoningEffort` (`string?`, por defecto `null`). Valores admitidos: `none`, `minimal`, `low`, `medium`, `high`. `null` o vacío significa "no enviar" (comportamiento actual). Un valor fuera de la lista se registra como aviso y no se envía.
- `OpenAiModelCapabilities.ConfigureChatOptions`: nueva sobrecarga que recibe además `reasoningEffort`. Comportamiento:
  - Modelo clásico (gpt-4x, gpt-35): igual que hoy (`temperature` y `max_tokens`); `reasoningEffort` se ignora.
  - Modelo de razonamiento: no se envía `temperature`; el límite de salida (el SDK beta lo serializa como `max_completion_tokens` solo si el cliente de Azure activa `SetNewMaxCompletionTokensPropertyEnabled()`; si no, sale como `max_tokens`) y `reasoning_effort` se envían solo cuando `reasoningEffort` está informado y es válido (el límite, además, cuando `maxOutputTokens > 0`); sin esfuerzo, la sobrecarga se comporta como la antigua (ni temperature ni límite ni esfuerzo), de modo que las filas gpt-5-mini existentes no cambian (ajuste tras la revisión final de rama, 2026-11-17).
  - La sobrecarga actual se mantiene sin cambios (no delega en la nueva), para que extracción no cambie; delegar habría empezado a enviar `max_completion_tokens` a extracción.
- Puntos de llamada que cambian: `GptClasificarDataProvider` (pasa `modelConfig.ReasoningEffort`), `OpenAIPromptDataProvider` en prompt libre y en resumen (pasa el del `PromptModelConfig`). `GptFallbackExtraerDataProvider` no cambia.
- Tests (unitarios, `DocumentIA.Tests.Unit`):
  - Serialización: construir `ChatCompletionOptions` con la sobrecarga nueva para un deployment `gpt-5.6-luna`, serializar con el mecanismo del SDK (`ModelReaderWriter`) y comprobar que el JSON contiene `reasoning_effort: "none"` y `max_completion_tokens`, y no contiene `temperature` ni `max_tokens`. Para `gpt-4o-mini` comprobar lo contrario. **Este test es la puerta del enfoque 1**: si no se consigue que la serialización incluya ambos campos, se pasa al enfoque 2.
  - Validación de `ReasoningEffort` (valores válidos, nulo, inválido).
  - Deserialización de las dos configuraciones con y sin la propiedad nueva.
  - Los tests existentes de `OpenAiModelCapabilitiesTests` y `GptClasificarDataProviderTests` siguen en verde.
- Verificado en la Tarea 5 (2026-11-17): el cliente de Azure 2.9.0-beta.1 usa `api-version` `2025-04-01-preview`; la API de esfuerzo es `ChatCompletionOptions.ReasoningEffortLevel` (`ChatReasoningEffortLevel?`); para emitir `max_completion_tokens` en lugar de `max_tokens` hace falta llamar a `SetNewMaxCompletionTokensPropertyEnabled()` (experimental, `AOAI001`), que el helper activa en deployments de razonamiento.
- Api-version (revisión final de rama, 2026-11-17): el SDK 2.9.0-beta.1 cambia la api-version por defecto de todo el tráfico de Azure OpenAI del Function App, de `2024-10-21` (GA, SDK 2.1.0) a `2025-04-01-preview`, porque ningún cliente fija `ServiceVersion`. Afecta a cuatro clientes: `GptClasificarDataProvider` (clasificación), `OpenAIPromptDataProvider` (prompt libre y resumen), `OpenAiClientFactory` (extracción con fallback GPT) y `Classification/AzureOpenAIEmbeddingsCliente` (embeddings en sombra).
- Admin (`src/frontend/DocumentIA.Admin`, `ModeloEdit.razor`): el constructor comprueba si el editor de modelos edita el JSON libre o campos tipados. Si es JSON libre, nada que hacer; si es tipado, se añade el campo `ReasoningEffort` para que un guardado desde Admin no lo pierda.

### 6.3 Base de datos de DEV

- `scripts/migrations/gpt56-luna/01-dev-insert-gpt56-luna-rows.sql`: alta de `classification.gpt56-luna` (Tipo 0) y `prompt.gpt56-luna` (Tipo prompt) clonando las filas canónicas `classification.gpt5-mini` y `prompt.gpt5-mini` del propio entorno, cambiando `DeploymentName` a `gpt-5.6-luna` y añadiendo `"ReasoningEffort": "none"`. `Activo = 1`, sin `IsDefault` ni `UseAsFallback`: solo se resuelven pidiendo la clave. Idempotente (no inserta si la clave existe para su Tipo), backup `ModeloConfigs__bak_<ts>` previo, consulta de verificación al final. `MaxTokens` de clasificación: se mantiene el de la fila clonada (2000); con `reasoning_effort none` no hay tokens de razonamiento, así que sobra, pero bajarlo es una decisión del cutover, no de la prueba.
- `scripts/migrations/gpt56-luna/02-dev-tarifa-gpt56-luna.sql`: añade al catálogo `tarifas.ia` la línea `gpt-5.6-luna` con `VigenteDesde` 2026-11-17 y los precios Data Zone de lista (0,20 / 0,02 / 1,19 € por millón). Sin esto el coste por ejecución sale a nulo y la medición de coste no vale. El plan contrasta esos precios con el medidor Data Zone de West Europe en la Retail Prices API antes de ejecutar. Contrastado el 2026-11-17: los medidores `5.6 luna ShortCo {Inp|Cd Inp|Opt} Std DZ` de westeurope en EUR dan 0,1975 / 0,0197 / 1,185 (los redondeados eran de la lista pública); el script 02 guarda los del medidor. Los medidores `LongCo` (contexto largo) doblan el precio y no aplican a la clasificación (máximo 10 páginas). El JSON debe ser estricto: el cargador es tolerante a errores y un JSON inválido deja el catálogo vacío en silencio.
- Ejecución con el método documentado para DEV (`sqlcmd -G` o pyodbc con token), con sí explícito.

### 6.4 Medición

- Flujo de ramas (decisión del usuario, 2026-11-17): el trabajo va en una rama `feature/<task>-gpt56-luna-dev` desde `develop` y **no se integra en `develop` hasta tener el resultado de la prueba**, para que la release v1.2.0 en preparación no arrastre el SDK beta. El pipeline no tiene trigger automático, así que se lanza a mano sobre la rama de trabajo para desplegarla en DEV antes de medir; al terminar la medición se vuelve a desplegar `develop` en DEV. Si durante la medición hace falta DEV para otra cosa, se avisa y se decide en el hilo. El merge, cuando llegue, pide sí explícito.
- Pasada candidata: harness `run` con una copia de `config-dev.json` en la que `Model` = `classification.gpt56-luna` (el resto igual: `OnlyClassification`, `ForceReprocess`, `ParallelQueries` 2), sobre la golden completa (467). Directorio `eval/runs/<yyyyMMdd-HHmmss>-gpt56-luna-golden`.
- Pasada de control: misma golden, mismo día, `Model` = `auto` (gpt-4o-mini, es decir gpt-4.1-mini). Directorio `eval/runs/<yyyyMMdd-HHmmss>-gpt4omini-control`. Motivo: la configuración de DEV ha cambiado desde BASELINE-GPT4OMINI-DEV; la comparación válida es contra el modelo actual con la configuración actual. Coste estimado de ambas pasadas: del orden de 5 € en total.
- `compare` candidata frente a control (McNemar en TDN1 y TDN2) y, como referencia histórica, frente a `BASELINE-GPT4OMINI-DEV`.
- Coste y latencia: desde la BD de DEV con las consultas de costes por ejecución (AB#100224), filtrando las ejecuciones de cada pasada; se informa coste medio por documento, tokens de entrada, cacheados y salida, y p50/p95 de duración de la llamada GPT.
- Comprobación de que ninguna respuesta llegó truncada (`finish_reason` distinto de `stop`) ni con HTTP 400.

### 6.5 Prompt libre y resumen

No tienen golden. Comprobación funcional en DEV sobre 20 documentos de la golden repartidos entre tipologías con resumen y prompt definidos:
- Selección del modelo nuevo sin tocar el default de DEV: por `ModelKey` en la petición si el contrato lo admite (el código lo lee en `requestPrompt.ModelKey`), o en su defecto por el `PromptConfig.ModelKey` de dos o tres tipologías de prueba en DEV, restaurado al terminar. El constructor elige el primero que funcione y lo deja escrito.
- Criterios: respuesta no vacía y con el formato esperado (JSON válido donde el prompt lo pide), sin HTTP 400, latencia y coste comparables, y revisión manual por el usuario de 10 resúmenes lado a lado con los del modelo actual.

### 6.6 Criterios de éxito de la prueba

La prueba es satisfactoria (y habilita proponer el cutover como unidad nueva) si:
- TDN1 y TDN2 no son peores que la pasada de control: diferencia a favor del modelo actual no significativa (McNemar p > 0,05) o a favor de luna.
- Cero HTTP 400 y sin 429 persistentes con `ParallelQueries` 2.
- Coste medio por documento igual o inferior a la mitad del de la pasada de control (esperado ~0,24 frente a ~0,65 c€).
- p50 de la llamada GPT no superior a 1,5 veces el del control.
- Prompt libre y resumen cumplen 6.5.

Si falla la calidad, la siguiente candidata es gpt-5.4-mini (Data Zone), reutilizando todo salvo el deployment y las filas. Si falla por cuota o disponibilidad, se para y se informa.

### 6.7 Work items (propuesta, se crean con sí explícito)

Tasks hijas de AB#99934, creadas el 2026-11-17 con sí del usuario (AB#101370, AB#101371, AB#101372, AB#101373, AB#101374, en ese orden), con `AB#` propio en cada commit:
1. Deployment `gpt-5.6-luna` Data Zone en DEV y script parametrizado por entorno.
2. Soporte de `reasoning_effort` y `max_completion_tokens`: SDK `2.9.0-beta.1`, propiedad `ReasoningEffort`, helper y tests de serialización.
3. Filas `*.gpt56-luna` y tarifa `gpt-5.6-luna` en DEV.
4. Golden A/B gpt-5.6-luna frente a control gpt-4.1-mini en DEV: pasadas, compare, coste y latencia; resultado en la spec.
5. Comprobación funcional de prompt libre y resumen con gpt-5.6-luna en DEV.

La Task AB#99936 (A/B gpt-5-mini) se actualizó el 2026-11-17 con el resultado de agosto y su conclusión. No se tocan 99938 ni 99939 (cutover) en esta unidad.

### 6.8 Impacto documental previsto

- Técnica: `scripts/migrations/gpt56-luna/README.md` (nuevo, orden de ejecución y qué no cubre, como el de gpt5-mini); `docs/03_DISENO_TECNICO_DETALLADO.md` (propiedad `ReasoningEffort`, comportamiento del helper, versión del SDK); README del repo si lista la versión del SDK.
- Funcional: `docs/05_MANUAL_USO_CONFIGURACION.md` (campo `ReasoningEffort` en las configuraciones de modelo, valores y efecto).
- Arquitectura: sin ADR en esta unidad. El cambio de modelo de IA es disparador de ADR, pero la decisión se toma en el cutover con la evidencia de esta prueba; entonces se preguntará antes de crearlo.
- Explotación: `docs/08_CHECKLISTS_DESPLIEGUE.md` solo si el deployment nuevo entra en las comprobaciones; se decide en la revisión de cierre.

## 7. Lo que queda al constructor

- Forma exacta de la sobrecarga del helper y nombres de los tests, respetando el estilo de `OpenAiModelCapabilitiesTests`.
- Cómo serializar `ChatCompletionOptions` en el test (mecanismo del SDK 2.9) y la versión de API de Azure que usa el cliente por defecto en esa beta; si el api-version por defecto no acepta los campos, fijar la más reciente que los acepte y anotarlo.
- Adaptaciones de compilación que exija el SDK 2.9 en el resto de `DocumentIA.Functions`, sin cambiar comportamiento.
- Si el editor de modelos del Admin necesita el campo nuevo (6.2).
- Mecanismo de selección del modelo para prompt libre y resumen en la prueba (6.5).
- Consultas SQL concretas de coste y latencia por pasada (6.4), partiendo de las de AB#100224.
- Nombre y parámetros del script de deployment por entorno (6.1), manteniendo el comportamiento idempotente del de gpt5-mini.

## 8. Riesgos

- SDK en beta: cambios de API o de comportamiento en los proveedores no modificados (extracción). Mitigación: tests existentes en verde, versión fijada, anotar en la spec cualquier diferencia observada.
- La beta no serializa `reasoning_effort` contra el api-version de Azure: cubierto por el test puerta y el enfoque 2 de reserva.
- Cuota Data Zone de la familia 5.6 en West Europe desconocida: puede impedir el deployment o limitar la capacidad; con 50K TPM y `ParallelQueries` 2 la golden cabe.
- `ForceReprocess` y la reutilización por duplicado: la pasada de control usa la misma configuración que las pasadas anteriores del harness; si la dedup interfiere en la candidata, se verá en el número de ejecuciones reales frente a 467.
- Deriva entre pasadas: candidata y control se lanzan el mismo día, sin cambios de configuración en DEV entre ambas.
- Coste de la beta en PRO: no es decisión de esta unidad; se recoge en el cutover (AB#99939) junto con la alternativa del enfoque 3 si para entonces el paquete estable ya cubre los campos.
- Api-version preview en todo el tráfico: la pasada de control de la Fase 3 también irá con la api-version `2025-04-01-preview`, así que compara modelos con el mismo api-version pero no equivale a "develop tal cual" (que usa `2024-10-21`). Mitigación: las comprobaciones de regresión tras desplegar (Tarea 9, Paso 4b).

## 9. Estado

Revisión del constructor hecha (2026-11-17): dos preguntas, una resuelta en 6.2 y 6.1 como decisión del diseño y otra resuelta por el usuario en 6.4. Pendiente de aprobación del usuario. Tras la aprobación: `superpowers:writing-plans` con fases que quepan en sesiones de 20-40 minutos (1: SDK y código con tests; 2: deployment, filas y tarifa en DEV, con los síes; 3: medición y resultado).
