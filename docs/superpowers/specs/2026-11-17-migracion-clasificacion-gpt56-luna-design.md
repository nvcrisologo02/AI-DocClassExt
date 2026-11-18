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

Revisión del constructor hecha (2026-11-17): dos preguntas, una resuelta en 6.2 y 6.1 como decisión del diseño y otra resuelta por el usuario en 6.4. Aprobada por el usuario y planificada en tres fases (1: SDK y código con tests; 2: deployment, filas y tarifa en DEV, con los síes; 3: medición y resultado).

Fase 1 (código) cerrada el 2026-11-17: rama `feature/99934-gpt56-luna-dev`, 1579/1579 tests, revisión final de rama con los Important corregidos (6.2 y 8 anotados).

Fase 2 (DEV) cerrada el 2026-11-17:

- Deployment `gpt-5.6-luna` creado en `srbaisrv01devdocai` a las 13:39 UTC: modelo `gpt-5.6-luna` 2026-07-09, `DataZoneStandard`, 50K TPM, `provisioningState` Succeeded (cuota Data Zone suficiente). Smoke directo: HTTP 200, `finish_reason` stop, `reasoning_tokens` 0; el servicio acepta `reasoning_effort` y `max_completion_tokens` con api-version `2025-04-01-preview`.
- Filas `classification.gpt56-luna` (Id 1027) y `prompt.gpt56-luna` (Id 1028) y línea de tarifa `gpt-5.6-luna` aplicadas en DEV a las 13:41 UTC con backup; reejecución idempotente.
- Rama desplegada en DEV con el pipeline 802 (run 80138, f2ed454, succeeded 13:59 UTC). DEV ejecuta la rama hasta la Tarea 12.
- Humo de clasificación con `classification.gpt56-luna` (harness, 3 documentos ACTE-02, ACTR-01, ACTT-10): 3/3 OK; en `DocumentoEjecuciones` `ModeloClasificacion` gpt-5.6-luna, `CosteClasificacionEur` 0,0044-0,0049 (tarifa aplicada), `TokensIA` 21-26K, `TokensRazonamiento` 0 en las dos fases. Mismas predicciones TDN1 que el modelo por defecto en los tres documentos, con confianza 0,98-0,99 frente a 0,90-0,95.
- Regresión por api-version (Tarea 9, paso 4b): (2) embeddings en sombra: las seis ejecuciones nuevas traen el candidato `Proveedor=Embeddings` con `MotivoDescarte=sombra` y el consumo `classification.embeddings` registrado; (3) clasificación por defecto (`Model = auto`, 3 documentos): 3/3 OK con gpt-4o-mini, coste y tokens como antes. (1) extracción con fallback GPT y resumen: no se pueden forzar desde el harness; se ejercitaron con el runner E2E post-despliegue perfil smoke en DEV (artefacto `20261117-151609-dev-smoke`, 6/6 PASS): S-S3 (extracción dirigida `nota.simple_bal`) registró el consumo `extraction.gpt.fallback` con gpt-4o-mini (4946 tokens de entrada, 772 de salida) y `CosteExtraccionEur` 0,06; S-S4 (resumen documental) registró `prompt.resultado` con gpt-4o-mini (213 / 169) y estado OK; S-S1 (pipeline completo auto) y S-S2 (classificationOnly) OK. Ninguna regresión atribuible a la api-version `2025-04-01-preview` en los cuatro clientes.
- Observaciones para la Fase 3: la caché de prompt de luna fue de 1127 tokens frente a 13824 de gpt-4o-mini en el mismo documento (el coste de entrada real de luna puede ser mayor que el de lista por caché); `DuracionClasificacionMs` llega nulo en las seis ejecuciones, también en las del modelo por defecto, así que no es efecto del despliegue.

## 10. Resultado

Golden de 467 documentos en DEV el 2026-11-17, mismo código (rama `feature/99934-gpt56-luna-dev`, run 80138 del pipeline 802), mismo día, `ParallelQueries` 2, `MaxPages` 10, `ClassificationOnly`. Control `eval/runs/20261117-154955-gpt4omini-control` (`Model = auto`, es decir gpt-4.1-mini bajo el deployment `gpt-4o-mini`, 150K TPM Standard) de 14:50 a 16:10 UTC; candidata `eval/runs/20261117-154955-gpt56-luna-golden` (`classification.gpt56-luna`, `ReasoningEffort` none) de 16:10 a 17:41 UTC. Ambas 467/467 OK en el harness, cero HTTP 400, `TokensRazonamiento` 0 en todas las llamadas. Compares en `eval/runs/compare-20261117-154955-gpt4omini-control-vs-20261117-154955-gpt56-luna-golden/compare.md` y `compare-BASELINE-GPT4OMINI-DEV-vs-20261117-154955-gpt56-luna-golden/compare.md`; coste y latencia en `docs/auxiliares/temps/2026-11-17/golden-luna/coste-latencia-*.txt` (consultas incluidas).

### 10.1 Condiciones de la medición que hay que conocer

- **Cuota del deployment.** La candidata arrancó con 50K TPM (6.1) y, con dos peticiones en paralelo y unos 25K tokens por documento, el servicio devolvía 429 con `Retry-After` 60 s (13 de 48 llamadas en los primeros 30 minutos; `AzureOpenAIResilienceExecutor` honra la espera, así que el harness no lo ve como `rate_limit`). Con sí explícito, a las 16:22 UTC se subió el deployment a 150K TPM, igual que el `gpt-4o-mini` de DEV; el efecto tardó unos 11 minutos. Las ejecuciones hasta las 16:34 UTC (47 persistidas) se informan aparte como "cuota 50K". En el resto de la pasada hubo 22 respuestas 429 sobre 982 llamadas, reintentadas sin huella en la latencia. Para PRO, la cuota Data Zone de luna tendrá que dimensionarse como la actual del modelo por defecto; con 50K el criterio "sin 429 persistentes" no se cumple.
- **Red local.** Entre 16:35 y 16:41 UTC la máquina que ejecuta el harness perdió la resolución DNS del backend; la pasada 1 de luna dejó 241 filas en error y la pasada 2 (`--resume`) las rehizo. No afecta a DEV ni al modelo.
- **Qué hay en BD.** El orquestador no persiste las ejecuciones que terminan en `BAJA_CONFIANZA_CLASIFICACION` ni en error, así que coste, tokens y latencia de BD cubren 456 ejecuciones del control (10 de baja confianza y 1 error fuera) y 425 de luna (44 de baja confianza fuera). La exactitud sale del harness (467/467 en ambas). `DuracionClasificacionMs` llega nulo en DEV; la latencia es `DuracionTotalMs` (BD) y `duracion_ms` extremo a extremo del harness, con el mismo recorrido en los dos brazos.
- **Tipologías virtuales.** Cuando la fase 2 devuelve un TDN2 del catálogo sin tipología publicada en DEV, `GptClasificarDataProvider` devuelve una tipología virtual (`ClasificacionParcial`). Pasa por igual en los dos brazos (control 188 de 456, luna 159 de 425). La diferencia es de formato: la tipología virtual toma como código lo que el modelo escribe antes de los dos puntos en su campo `propuesta`; gpt-4.1-mini escribe la familia (`ESIN: ...`) y luna escribe muchas veces el código TDN2, o nada, con lo que el contrato persiste `Tdn1 = Tdn2 = FICH-19` y el harness lo cuenta como fallo de TDN1. Son 136 de las 467 predicciones de luna (0 en el control); 76 de ellas con la familia correcta y 22 con el TDN2 correcto. Por eso TDN1 se informa de dos maneras.

### 10.2 Tabla

| Métrica | Control (gpt-4.1-mini) | Candidata (gpt-5.6-luna) | Lectura |
|---|---|---|---|
| TDN1 tal como lo informa el contrato | 56,32% (263/467) | 49,68% (232/467) | b01 77 / b10 46, McNemar p = 0,0066: **empeora** |
| TDN1 normalizado por familia del código | 56,32% (263/467) | 65,95% (308/467) | b01 14 / b10 59, p < 0,001: **mejora** |
| TDN2 | 34,05% (159/467) | 40,69% (190/467) | b01 17 / b10 48, p = 0,0002: **mejora** |
| TDN1 / TDN2 frente a BASELINE-GPT4OMINI-DEV (septiembre) | 55,89% / 33,40% | 49,68% / 40,69% | p 0,0141 (empeora) / p < 0,0001 (mejora) |
| BAJA_CONFIANZA_CLASIFICACION (harness) | 10 (más 1 error) | 44 | luna devuelve más confianzas bajo el umbral 0,6 |
| Coste de clasificación medio por documento (BD) | 0,006168 € (n 456) | 0,005251 € (n 425); 0,005222 € con cuota 150K (n 378) | ratio 0,85: **no llega a la mitad** |
| Tokens IA por documento (BD) | 27.841 | 27.956 | iguales |
| Entrada / en caché, fase 1 (por llamada) | 18.191 / 13.759 (76%; 453 de 456 llamadas con caché) | 18.223 / 1.229 (7%) | la caché explica el coste |
| Entrada / en caché, fase 2 | 6.333 / 1.653 | 6.301 / 46 | ídem |
| Coste por llamada fase 1 / fase 2 | 0,00357 € / 0,00228 € | 0,00358 € / 0,00133 € | fase 1 igual; fase 2 un 42% menor |
| p50 / p95 duración total (BD, cuota 150K) | 12.984 / 20.119 ms (n 456) | 10.872 / 18.737 ms (n 378) | ratio p50 0,84: **cumple** (≤ 1,5) |
| p50 / p95 extremo a extremo del harness | 13.091 / 20.740 ms (467) | 11.474 / 42.045 ms (467) | el p95 de luna arrastra el tramo con cuota 50K |
| p50 / p95 duración total con cuota 50K | — | 39.824 / 73.823 ms (n 47) | 429 más 60 s de espera por llamada |
| HTTP 400 / 429 | 0 / 0 | 0 / 54 de 982 (32 con 50K, 22 con 150K) | cero 400; 429 no persistentes a 150K |
| Tipologías virtuales | 188 de 456 | 159 de 425 | mismo fenómeno en ambos |

### 10.3 Veredicto frente a 6.6

| Criterio 6.6 | Resultado |
|---|---|
| TDN1 y TDN2 no peores que el control | **Mixto.** TDN2 mejora de forma significativa (6,6 puntos más). TDN1 empeora tal como lo informa el contrato (6,6 puntos menos) y mejora si se normaliza la tipología virtual por familia (9,6 puntos más); la diferencia es el formato del campo `propuesta` de luna en el camino virtual, que el backend puede normalizar sin tocar el modelo. |
| Cero HTTP 400 y sin 429 persistentes con `ParallelQueries` 2 | **Cumple a 150K TPM; no cumple a 50K.** |
| Coste medio igual o inferior a la mitad del control | **No cumple.** 0,85 del control. La tarifa de luna es menor, pero el `gpt-4o-mini` de DEV cobra el 76% de la entrada a precio de caché y luna solo el 7%. El ahorro esperado (unos 0,24 frente a unos 0,65 c€) se calculó con precios de lista sin caché. |
| p50 de la llamada GPT no superior a 1,5 veces el control | **Cumple.** 0,84 (sin el tramo de cuota 50K). |
| Prompt libre y resumen (6.5) | **Cumple** (10.4): 20/20 respuestas en cada brazo, modelo correcto, sin HTTP 400, textos no vacíos, coste 0,63 del control, latencia igual; veredicto del usuario sobre los 20 pares: comparable o mejor. |

**Conclusión.** La prueba no cumple 6.6 tal como está escrito: falla el criterio de coste y el de calidad de clasificación queda en mixto; prompt libre y resumen cumplen. En calidad, luna acierta más TDN2 y, descontado el artefacto de la tipología virtual, más TDN1; a cambio produce más resultados bajo el umbral de confianza. En coste, la ventaja de tarifa se la come la diferencia de caché de prompt. La decisión (cutover AB#99938/99939, segunda candidata gpt-5.4-mini como prevé 6.6, o no migrar todavía) es del usuario. Antes de cualquier cutover convendría:

1. Normalizar en el backend la tipología virtual (Tdn1 = familia del código devuelto, Tdn2 = código), independiente del modelo; corrige el informe de TDN1 para cualquier modelo que escriba el TDN2 en `propuesta`.
2. Entender la caché de luna (7% frente a 76% con el mismo prefijo de prompt): si es propio del SKU Data Zone o del modelo, el coste real no bajará; si es de la forma del prompt, se puede recuperar.
3. Revisar el umbral de confianza con luna (44 bajas frente a 10) antes de dar por buena la mejora de TDN2.

### 10.4 Prompt libre y resumen (Tarea 11, AB#101374)

Humo del 2026-11-18 en DEV sobre 20 documentos de la golden (2 por tipología: deac.07, actr.01, nots.01, nova.04, decl.09, nots.02, lipr.06, cedu.01, prpe.09, ofer.01), en modo solo clasificación con `ForzarResumenPorDefecto` y la plantilla de la tipología enviada como override en los dos brazos; la candidata añade `Instrucciones.Prompt.ModelKey = prompt.gpt56-luna`, el control usa el modelo de la tipología. Script `docs/auxiliares/temps/2026-11-17/golden-luna/prompt-smoke.ps1`; textos y métricas desde BD (`prompt-smoke-bd.json`, `resumenes-lado-a-lado.md`).

- Hallazgo previo: en DEV las diez tipologías tienen `PromptConfig.Enabled = false`, y con solo `ModelKey` en la petición el proveedor ejecuta únicamente el resumen forzado con el modelo por defecto (`promptActivo` falso; el resumen no obedece el `ModelKey` de la petición). Con `SystemPrompt` y `UserPromptTemplate` en la petición (`tienePromptOverride`), prompt y resumen usan el `ModelKey` efectivo. El camino medido es "prompt ad hoc", con la misma plantilla, el mismo modelo y el mismo resumen que tomaría la tipología si estuviera habilitada.

| Métrica | Control (gpt-4o-mini) | Candidata (gpt-5.6-luna) |
|---|---|---|
| Peticiones completadas / modelo de prompt informado | 20/20 / gpt-4o-mini en las 20 | 20/20 / gpt-5.6-luna en las 20 |
| Estado final | 19 OK, 1 NO_CLASIFICADO (cedu.01 → Desconocido) | 19 OK, 1 NO_CLASIFICADO (el mismo documento) |
| HTTP 400 / `Prompt.Error` | 0 / 0 | 0 / 0 |
| Resultado o resumen vacíos | 0 | 0 |
| Longitud media resultado / resumen (caracteres) | 1.298 / 963 | 2.692 / 786 |
| Tiempo de prompt p50 / máximo | 5.330 / 8.671 ms | 5.310 / 9.972 ms |
| `CostePromptEur` medio | 0,003890 € | 0,002447 € (0,63) |
| `TokensRazonamiento` | 0 | 0 |

Veredicto del usuario sobre los 20 pares (2026-11-18): **comparable o mejor**. Criterio 6.5 cumplido. Observación: la candidata escribe resultados más largos (hasta 1.535 tokens de salida con límite 1.600 en las actas largas); si el límite de salida del prompt importa en producción, habrá que revisarlo en el cutover.
