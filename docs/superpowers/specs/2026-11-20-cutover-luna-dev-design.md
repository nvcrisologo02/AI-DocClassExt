# Cutover a gpt-5.6-luna en DEV: clasificación, prompt, resumen y extracción

Fecha: 2026-11-20 · Línea: core · Padre: AB#99934 (migración de la clasificación a gpt-5.6-luna) · Task: AB#99939 (descripción desfasada: habla de gpt-5-mini; se reescribe con sí) · Tamaño: proyecto (diseño completo) · Estado: aprobado por el usuario el 2026-11-20

## Intención

Microsoft retira gpt-4.1-mini, que es lo que sirve el deployment `gpt-4o-mini`, el **2027-04-14**. La prueba de luna en DEV (specs `2026-11-17-migracion-clasificacion-gpt56-luna-design.md`, `2026-11-18-prerrequisitos-cutover-tdn1-virtual-cache-luna-design.md` y `2026-11-19-umbral-confianza-tipologia-virtual-design.md`) dejó la evidencia de clasificación, prompt libre y resumen. Esta unidad hace que **en DEV** todo lo que hoy usa `gpt-4o-mini` pase a usar `gpt-5.6-luna` sin tocar las filas de gpt-4. Deja ensayado lo que la próxima release llevará a PRE y PRO (activación, rollback y comprobación) y quita de las tipologías la referencia al modelo concreto, que con el cambio dejaría de ser cierta.

Decisiones del usuario (2026-11-20):

- Solo DEV. PRE y PRO irán con una release nueva que se prepara aparte.
- Alcance completo: clasificación, prompt libre, resumen y extracción, las cinco filas que apuntan a `gpt-4o-mini`.
- La extracción va **sin prueba previa de calidad**: el fallback de extracción GPT y la tipología con modelo GPT son residuales. Se deja escrito aquí y en el ADR.
- Las filas de gpt-4 **no se sustituyen**: se añaden las de luna que falten y se cambia la configuración (indicadores de fallback, claves) para que entre luna. El rollback es devolver la configuración.
- Prompt y resumen pasan por **herencia del modelo por defecto**: las tipologías dejan de fijar la clave y la toman de `PromptDefaults:ModelKey`, que cambia por entorno. Se limpian las tipologías en esta unidad.
- Si la clave de prompt por defecto no resuelve, **falla siempre** (sin volver en silencio a gpt-4).

## Situación actual (leída en DEV el 2026-11-20)

Filas de `ModeloConfigs` con `DeploymentName = gpt-4o-mini`:

| Id | Key | Tipo | Cómo se elige hoy |
|---|---|---|---|
| 1003 | `classification.gpt4o-mini-fallback` | 0 | Única fila con `UseAsFallback` (umbral 0,6, `MaxTokens` 150). El modelo por defecto es `default.azure-di` |
| 1014 | `gpt-4o-mini` | 0 | Solo si se pide por clave |
| 4 | `default.gpt4o-mini` | 2 | Por clave: petición → `PromptConfig.ModelKey` de la tipología → `PromptDefaults:ModelKey` |
| 1002 | `default.gpt4o-mini_ex` | 1 | Por clave: `Extraction.ModelKey` de la tipología 2001 (IBI), la única |
| 1004 | `extraction.gpt4o-mini-fallback` | 1 | Única fila con `UseAsFallback` (`MaxTokens` 16000) |

Filas de luna ya existentes, sin indicadores: 1027 `classification.gpt56-luna` (Tipo 0, `ReasoningEffort none`, `MaxTokens` 2000, `promptCache` explicit con breakpoint, umbral 0,6) y 1028 `prompt.gpt56-luna` (Tipo 2, `ReasoningEffort none`).

Código y datos:

- `ClassificationModelRegistryLoader.GetFallbackModel()` y `ExtractionModelRegistryLoader.GetFallbackModel()` exigen **exactamente una** fila con `UseAsFallback`; con cero o dos lanzan `KeyNotFoundException`. Caché de 5 minutos por registro.
- `PromptModelRegistryLoader.GetModel(modelKey)` busca por clave exacta (insensible a mayúsculas) y lanza si no existe. Caché de 5 minutos.
- Precedencia del modelo de prompt (`OpenAIPromptDataProvider`, ~L488-535): clave de la petición → clave de la tipología → `PromptDefaults:ModelKey` (`ApplyDefaults`, `FirstNonEmpty`). La petición sigue mandando y eso no cambia.
- **219 tipologías de DEV fijan `PromptConfig.ModelKey = "default.gpt4o-mini"`** y ninguna fija otra clave de prompt. Motivos:
  - `TipologiasAdminFunction` (~L670) **rechaza** prompt habilitado con `modelKey` vacío.
  - `PromptConfig.ModelKey` vale `"default.gpt4o-mini"` por defecto en el código, así que una tipología sin clave también la recibe al deserializar.
- Literales `"default.gpt4o-mini"` como último recurso en `OpenAIPromptDataProvider.ResolveDefaultPromptModelKey` (L555) y `GptClasificarDataProvider` (L803). `appsettings.json` trae `PromptDefaults:ModelKey = default.gpt4o-mini`.
- El contrato del prompt guarda `Modelo = DeploymentName` del modelo resuelto.
- `azure-pipelines.yml` (802) fija los app settings legacy `Classification__GptFallback__DeploymentName=gpt-4o-mini` y `Extraction__GptFallback__DeploymentName=gpt-4o-mini` (Task AB#99938). La revisión del constructor no encontró lecturas de ese `DeploymentName` legacy fuera de `GptFallbackExtraerSettings` y plantillas; queda por confirmar.

## Objetivos

1. En DEV, la clasificación GPT de fallback, el prompt libre, el resumen, la extracción GPT de fallback y la extracción de la tipología 2001 salen con `gpt-5.6-luna`, sin pedir ninguna clave.
2. Las filas de gpt-4 (1002, 1003, 1004, 4, 1014) quedan intactas en su contenido; solo cambia `UseAsFallback` en 1003 y 1004.
3. Las tipologías dejan de fijar el modelo de prompt cuando solo repiten el valor por defecto, y heredan `PromptDefaults:ModelKey`. El próximo cambio de modelo de prompt será un app setting.
4. Activación y rollback idempotentes, con backup y en una transacción, probados en DEV en el orden activar → rollback → activar.
5. Lista de lo que la release tendrá que hacer en PRE y PRO.
6. ADR del cambio de modelo (se pregunta antes de crearlo).

## No objetivos

- Nada en PRE ni PRO: ni deployment, ni cuota, ni filas, ni tipologías, ni app settings.
- Integrar la rama en `develop` (va con la release).
- Medir la calidad de extracción con luna.
- Borrar el deployment `gpt-4o-mini` o desactivar sus filas.
- Renombrar claves de `ModeloConfigs`.
- Aplicar la herencia a la extracción o a la clasificación (la extracción por clave afecta a una sola tipología; la clasificación va por indicadores).
- Cambiar umbrales de confianza.
- Cambios NuGet.

## Antiobjetivos

- Que en algún momento haya dos filas o ninguna con `UseAsFallback` en un mismo Tipo: el fallback falla con excepción.
- Que una tipología quede sin modelo de prompt resoluble.
- Que se pierda la precedencia de la petición sobre la tipología.
- Que se pierda el modo combinado de extracción y prompt por leer una clave vacía en lugar de la efectiva.
- Que el rollback dependa de rehacer a mano el JSON de una fila o de volver a tocar las 219 tipologías.
- Que se active luna en PRE o PRO por arrastre del pipeline.

## Enfoques considerados

Para clasificación y extracción por fallback solo hay un camino sensato: mover `UseAsFallback`. Para la extracción por clave de la tipología 2001, cambiar su `Extraction.ModelKey` a una fila nueva. La elección real era prompt y resumen:

1. **Herencia del modelo por defecto** (elegido por el usuario). Las tipologías dejan de fijar la clave y heredan `PromptDefaults:ModelKey`, que el pipeline fija por entorno. Requiere código pequeño (valor por defecto vacío, validación del Admin, consumidores que lean la clave efectiva) y un script que limpie las 219 tipologías. Deja la configuración diciendo la verdad, y el rollback y los cambios de modelo futuros son un setting.
2. Redirección de clave por app setting (`default.gpt4o-mini → prompt.gpt56-luna`). Sin tocar tipologías, pero dejaría las 219 diciendo `default.gpt4o-mini` cuando usan luna y añadiría un mecanismo nuevo. Elegido primero y sustituido por el 1 a propuesta del usuario.
3. Cambiar las 219 tipologías a `prompt.gpt56-luna`. Tendría que repetirse en cada cambio de modelo y el rollback sería masivo. Descartado.
4. Sobrescribir el `DeploymentName` de la fila 4. Descartado por el usuario: las filas de gpt-4 no se sustituyen.

## Diseño

### 1. Código: herencia del modelo de prompt

- `PromptConfig.ModelKey` pasa a valer `""` por defecto. Una clave vacía en la tipología significa «usar `PromptDefaults:ModelKey`».
- `OpenAIPromptDataProvider.ResolveDefaultPromptModelKey` y `GptClasificarDataProvider` (L803) dejan de usar el literal `"default.gpt4o-mini"` y toman `PromptDefaults:ModelKey`. Si está vacío, error claro (`PromptDefaults:ModelKey no configurado`). `appsettings.json` conserva `default.gpt4o-mini` como valor base.
- Si `PromptDefaults:ModelKey` apunta a una clave que no existe o está inactiva, `GetModel` lanza como hoy, con la clave en el mensaje. No hay vuelta silenciosa a gpt-4 ni comprobación al arrancar (decisión del usuario).
- **Consumidores de `PromptConfig.ModelKey`:** todos tienen que usar la clave efectiva (petición → tipología → defecto), no la cruda de la tipología. El plan los audita uno a uno. Mínimo:
  - `ConfigurableExtraerDataProvider.DebeUsarModoCombinado` (~L378-399) hoy devuelve `false` con clave vacía y perdería el modo combinado.
  - `PromptInstruccionesValidator`.
  - Los `ModelKey` que construye `GptClasificarDataProvider` (L734, L764, L803).
  - Cualquier sitio que persista o compare la clave.
  - Conviene un único método de resolución compartido.
- `TipologiasAdminFunction` (~L670): con prompt habilitado y `modelKey` vacío deja de ser error (significa heredar). Con `modelKey` informado sigue exigiendo que exista y esté activo. Se revisa la UI del Admin (Blazor) por si obliga a elegir modelo o rellena `default.gpt4o-mini` al crear o editar. Si lo hace, se añade la opción «por defecto», que equivale a vacío.
- Tests unitarios:
  - Tipología sin clave hereda el defecto.
  - La petición gana a la tipología y al defecto.
  - La tipología con clave explícita la conserva.
  - Defecto inexistente lanza.
  - Defecto vacío da error claro.
  - El modo combinado sigue activándose con clave heredada.
  - El Admin acepta clave vacía y rechaza una inexistente.
- Literal `Modelo = "gpt-4o-mini"` del camino «tipología no identificada» (`DocumentProcessOrchestrator` ~L1746): pasa a ser el `DeploymentName` del fallback de clasificación resuelto, con `gpt-4o-mini` como último recurso si no se resuelve. Lleva test. **Ajuste en la ejecución (2026-11-20, commit 6274755):** el orquestador Durable no puede leer el registro de modelos (sería E/S no determinista) y la excepción de `ClasificarActivity` solo trae el mensaje, así que `Modelo` queda vacío («no consta»). `ProveedorClasif` sigue en `GPT4oMini`. Admin oculta la línea «Modelo» si está vacía y la columna `ModeloClasificacion` es nullable.

### 2. Pipeline 802: `PromptDefaults__ModelKey` por entorno

- `azure-pipelines.yml`: nuevo app setting `PromptDefaults__ModelKey` con una variable por entorno: `prompt.gpt56-luna` en DEV y **`default.gpt4o-mini` en PRE y PRO** (no vacío: vacío sería error). La release cambiará PRE y PRO.
- AB#99938: confirmar si `Classification__GptFallback__DeploymentName` y `Extraction__GptFallback__DeploymentName` se leen en algún camino con registro en BD.
  - Si se leen, se cambian en DEV a `gpt-5.6-luna` en esta unidad y AB#99938 se cierra con ella.
  - Si no, se documenta que son legacy y la limpieza sigue en AB#99938.

### 3. Datos: scripts nuevos en `scripts/migrations/gpt56-luna/`

Patrón de los anteriores: idempotentes, backup (`ModeloConfigs__bak_<ts>`, `Tipologias__bak_<ts>`), transacción, consulta de verificación al final, solo DEV (aplicados con `apply-sql-dev.ps1`, que rechaza otros servidores). Al escribir JSON se respeta el casing de cada fila (`IsPascal`), como en los scripts de tipologías.

`06-dev-cutover-gpt56-luna.sql` (activación de modelos):

1. Alta, si no existen, de `extraction.gpt56-luna` y `default.gpt56-luna_ex` (Tipo 1):
   - Toman `Endpoint`, recurso y `AuthMode` de la 1027, que ya funciona con luna en DEV.
   - De la 1004 y la 1002 se toma el resto (`MaxTokens` 16000 y demás).
   - Llevan `DeploymentName = gpt-5.6-luna` y `ReasoningEffort: "none"`, sin `promptCache` (no medido en extracción).
   - Se crean con `UseAsFallback = false`. El script falla si no existe la 1027.
2. En la misma transacción, `UseAsFallback` a `true` en `classification.gpt56-luna` y en `extraction.gpt56-luna`, y a `false` en 1003 y 1004. Comprobación final dentro de la transacción: exactamente una fila con `UseAsFallback` por Tipo 0 y Tipo 1. Si no, `THROW` y rollback.
3. Tipología 2001: `Extraction.ModelKey` pasa de `default.gpt4o-mini_ex` a `default.gpt56-luna_ex`.

`07-dev-rollback-cutover-gpt56-luna.sql`:

- Hace lo inverso en una transacción: indicadores de vuelta a 1003 y 1004, y la tipología 2001 otra vez a `default.gpt4o-mini_ex`.
- Repite la comprobación de un fallback por Tipo.
- **No borra** las filas nuevas: se quedan sin indicador, como estaban 1027 y 1028 antes del cutover.
- Es idempotente.

`08-dev-tipologias-heredan-modelo-prompt.sql` (limpieza):

- Elimina la propiedad de clave de prompt (`PromptConfig.ModelKey` o su variante de casing) en las tipologías, de cualquier estado, cuyo valor sea `default.gpt4o-mini`.
- No toca las que fijen otra clave, y lo dice en la verificación (hoy son 0).
- Backup previo e idempotente.
- Recuento antes y después.
- Es seguro con el código actual: sin la propiedad, el código de hoy deserializa `default.gpt4o-mini` por defecto. Por eso puede aplicarse antes o después del despliegue.
- No forma parte del rollback: con la herencia, el rollback de prompt y resumen es el setting. Restaurar las tipologías desde el backup queda documentado como último recurso.

### 4. Rollback y activación de prompt y resumen

Activar es `PromptDefaults__ModelKey = prompt.gpt56-luna`. Deshacer es `PromptDefaults__ModelKey = default.gpt4o-mini`. El valor persistente va en el pipeline 802. En el ensayo se cambia en DEV con `az functionapp config appsettings` (escritura en DEV, con sí en el momento). Activación y rollback mueven a la vez el script 06/07 y el setting.

### 5. Orden en DEV

1. Rama con el código y el pipeline → 802 sobre DEV (con sí).
2. Script 08 (con sí) y comprobación de que las tipologías heredan (siguen en gpt-4 hasta el paso 3).
3. Script 06 (con sí). El setting ya está en `prompt.gpt56-luna` por el paso 1; si se prefiere separar, el paso 1 despliega con `default.gpt4o-mini` y el setting se cambia aquí.
4. Esperar el refresco de caché (5 minutos) o reiniciar.
5. Humo (Verificación 2).
6. Ensayo de rollback: script 07 y setting a `default.gpt4o-mini` → humo corto → script 06 y setting a `prompt.gpt56-luna` → humo corto.

### 6. ADR

`ADR-004-<slug>.md` en `docs/decisiones/`, con este contenido:

- gpt-5.6-luna sustituye a gpt-4.1-mini en clasificación GPT, prompt, resumen y extracción GPT.
- Evidencia: golden, caché y la revisión de prompt y resumen.
- La extracción no se ha medido, por residual.
- Las filas de gpt-4 se conservan y el rollback es por configuración.
- La herencia del modelo de prompt pasa a ser la forma de cambiar de modelo.
- PRE y PRO llegan con la release.

Se pregunta antes de crearlo.

## Decisiones tras la revisión del constructor (mías, salvo que se indique)

- **Modo combinado** (`ConfigurableExtraerDataProvider.DebeUsarModoCombinado`):
  - Compara el `DeploymentName` del modelo de prompt con el del fallback de extracción. Hoy coinciden en `gpt-4o-mini` y tras el cutover coincidirán en `gpt-5.6-luna`, así que se conserva, siempre que use la clave efectiva (ver Diseño 1).
  - El humo lo comprueba.
  - En la ventana intermedia (caché de 5 minutos, solo en DEV) puede apagarse y costar una llamada más por documento; se acepta.
- **Opciones y firmas:** `PromptModelRegistryLoader` no cambia de firma. La herencia se resuelve antes de llamar a `GetModel`.
- **`ProveedorClasif = "GPT4oMini"` y `ProveedorExtrac = "GPT4oMini"`** son valores fijos del contrato y no se cambian, porque los consumidores los conocen. El humo usa `Modelo` y `ModelKeyEfectivo`, no el proveedor. Por eso el `results.csv` del harness pone `GPT4oMini` en todo.
- **Pipeline 799 y semillas:**
  - El 799 reescribe filas de `ModeloConfigs` y en esta unidad no se lanza.
  - El README añade revisar los indicadores de fallback, la tipología 2001 y las claves de prompt de las tipologías después de cada 799 o seed de tipologías.
  - ConfigSeed solo da de alta por clave natural. Con la herencia, las tipologías que dé de alta deberían llegar sin clave de prompt: se revisa en el plan.
- **Alias 1014 (`gpt-4o-mini`, Tipo 0):** se queda en gpt-4 para quien lo pida por clave, por decisión del usuario de no sustituir filas. El criterio del humo es «sin clave pedida».
- **El humo se ejecuta dentro del plan,** con el harness del Batch (`c:/temp/MVP/DocumentIA.Batch`, `-OnlyFiles` sobre `eval/golden-muestra-cache.txt`, en procesamiento completo). Para IBI, un documento de esa tipología del corpus golden, o un caso sintético si no hay ninguno.

## Verificación

1. Build sin warnings nuevos, `dotnet test src/backend/DocumentIA.Tests.Unit`, `dotnet test src/backend/DocumentIA.Tests.Admin` (por la validación de tipologías) y `dotnet format --verify-no-changes`.
2. Humo en DEV tras activar, con la muestra fija de 50 en procesamiento completo, más un documento de IBI y, si hay forma de provocarlo, uno que fuerce el fallback de extracción (si no, se deja sin probar con motivo):
   - Clasificaciones GPT con `ModeloClasificacion` de luna; ninguna con `gpt-4o-mini`.
   - Prompt y resumen con `Modelo = gpt-5.6-luna`.
   - Extracción de la 2001 con `ModelKeyEfectivo = default.gpt56-luna_ex`.
   - Modo combinado activo donde lo estaba.
   - Costes con la tarifa `gpt-5.6-luna` y tokens de caché registrados.
   - Cero errores nuevos por resolución de modelo.
3. Una petición con `ModelKey` de prompt explícito sigue usando esa clave.
4. Tras el rollback, una pasada corta (5-10 documentos) vuelve a `gpt-4o-mini` en todo; tras reactivar, vuelve a luna.
5. Consultas de verificación de los scripts:
   - Un fallback por Tipo.
   - Filas de gpt-4 con el mismo JSON que en el backup, salvo `UseAsFallback`.
   - 0 tipologías con `default.gpt4o-mini` como clave de prompt.
   - El Admin lista y edita una tipología limpia sin error.

## Lista para la release (PRE y PRO)

Se escribe en el README de gpt56-luna, sin ejecutar nada:

- Integrar la rama en `develop`. Lleva el SDK `Azure.AI.OpenAI 2.9.0-beta.1`, `OpenAiModelCapabilities`, la caché, el umbral virtual y la herencia del modelo de prompt.
- Deployment `gpt-5.6-luna` Data Zone y su cuota en PRE y PRO. En PRO, ARM exige PIM. Hay que rellenar el bloque `pre` del script de deployment.
- Filas 1027/1028 equivalentes, tarifa y escritura de caché en PRE y PRO, con los scripts 01-05 por entorno o con ConfigSeed.
- Scripts 06/07/08 portados a PRE y PRO. La tipología IBI puede tener otro Id fuera de DEV: resolver por nombre o clave natural.
- Comprobar que la fila `prompt.gpt56-luna` existe y está activa **antes** de poner `PromptDefaults__ModelKey`, porque si no resuelve falla siempre.
- Vigilancia de 48 h en PRO: HTTP 400, latencia, coste y volumen de baja confianza.

## Queda al constructor

- Forma del método compartido de clave efectiva y dónde vive.
- Nombre de las variables del pipeline.
- Cómo provocar un fallback de extracción en el humo.
- Consultas de humo, a partir de `medicion/01-03`.
- Cambio de UI del Admin, si hace falta.

## Impacto documental previsto

- Técnica:
  - `docs/03_DISENO_TECNICO_DETALLADO.md`: resolución del modelo de prompt y herencia.
  - `docs/05_MANUAL_USO_CONFIGURACION.md`: `PromptConfig.ModelKey` vacío significa heredar, y `PromptDefaults:ModelKey` por entorno.
- Explotación:
  - `scripts/migrations/gpt56-luna/README.md`: scripts 06/07/08, orden, rollback y lista de la release.
  - Checklist de app settings (`PromptDefaults__ModelKey`).
- Arquitectura: ADR-004.
- Funcional: ninguno previsto. El comportamiento de negocio no cambia, cambia el modelo. Si el Admin cambia su formulario de tipologías, se revisa el manual de uso del Admin.

## Resultado en DEV (2026-11-20)

Cutover a gpt-5.6-luna ejecutado en DEV (AB#99939, pipeline AB#99938). Solo DEV: nada en PRE ni PRO.

- **Despliegue**: run 80195 del pipeline 802 sobre 20fec98 (`targetEnvironment=dev`), en verde. El log fija `PromptDefaults__ModelKey = prompt.gpt56-luna`.
- **Script 08**: 219 tipologías cambiadas (13 PascalCase y 206 camelCase) de 233, 0 con otra clave, backup `Tipologias__bak_20261120_092949`. Segunda ejecución: 0 cambios. Estado final: 233 heredan, 0 fijan `default.gpt4o-mini`.
- **Script 06** (09:30 UTC): 2 altas (1029 `extraction.gpt56-luna`, 1030 `default.gpt56-luna_ex`), 4 indicadores `UseAsFallback` movidos (1027 y 1029 a true; 1003 y 1004 a false), tipología 2001 a `default.gpt56-luna_ex`, un fallback por Tipo. Backups `ModeloConfigs__bak_20261120_093020` y `Tipologias__bak_20261120_093020`. Segunda ejecución: 0 cambios.
- **Humo** (tras 5 minutos de espera; la cuenta az de la sesión no tiene permiso sobre la configuración de la Function de DEV y no se reinició): 50/50 OK entre 09:39 y 09:50 UTC, más 6 peticiones extra, por HTTP directo a `/api/IngestDocument` con resumen forzado y extracción automática. Se usó HTTP porque el harness del Batch fija solo clasificación y sin resumen. Resultados:
  - 0 clasificaciones GPT con `gpt-4o-mini`: `classification.phase1` y `phase2` solo `gpt-5.6-luna` (59 cada una).
  - Prompt y resumen con `gpt-5.6-luna` (44 llamadas `prompt.json`); en modo combinado (9 ejecuciones) `Prompt.Modelo` trae la clave `prompt.gpt56-luna`.
  - Tipología 2001 (`IBI_1.1`, pedida por `expectedType`) con `ModelKeyEfectivo = default.gpt56-luna_ex` y extracción en luna.
  - Tarifa `gpt-5.6-luna@2026-11-17` en clasificación, extracción y prompt. 0 errores de resolución de modelo.
  - Admin de DEV: la lista de tipologías y el detalle de una tipología Published abren sin error.
- **Paso 6** (la petición manda): con prompt ad-hoc completo y `instrucciones.prompt.modelKey = "default.gpt4o-mini"` el prompt sale con `gpt-4o-mini`. Con solo `forzarResumenPorDefecto` la clave de la petición no aplica al resumen (sale luna). Es el comportamiento previo a esta unidad, no una regresión; queda como límite conocido.
- **Rollback ensayado**: 07 a las 09:56 UTC y `PromptDefaults__ModelKey=default.gpt4o-mini` puesto por el usuario (~10:00 UTC). Pasada de 8 ficheros más 3 extras entre 10:12 y 10:15 UTC, 11/11 OK, todo en `gpt-4o-mini` (clasificación, prompt, resumen y extracción; 2001 con `default.gpt4o-mini_ex`). El modo combinado no se pudo comprobar en la pasada corta. Después, 06 reaplicado a las 10:15 UTC (0 altas, 4 indicadores, 2001; backups `__bak_20261120_101545`) y setting vuelto a `prompt.gpt56-luna` por el usuario (~10:20 UTC).
- **Pasada de vuelta** (10:25 a 10:29 UTC): 11/11 OK; clasificación (`phase1` y `phase2`, 7 cada una), prompt (10) y extracción en `gpt-5.6-luna`, 2001 con `default.gpt56-luna_ex`, 0 errores de resolución; la extra con ad-hoc y `modelKey` explícito sale en `gpt-4o-mini`.
- **Estado final de DEV**: luna en los cuatro usos (clasificación, extracción, prompt y resumen). Las filas gpt-4 se conservan sin indicador `UseAsFallback`.
- **Límites**: la edición en el Admin no se comprobó porque una tipología Published no ofrece edición. 23 de 50 ejecuciones salen `Desconocido` en el flujo completo, sin línea base del mismo flujo con la que compararlas. La cuenta az de la sesión no tiene permiso sobre la configuración de la Function de DEV: el usuario cambió el setting en ambos sentidos.
- **Temporales** (no versionados), en `docs/auxiliares/temps/2026-11-20/cutover-luna/` del clon principal: `humo.sql`, `humo-ingest.ps1`, `pasada-corta.txt`, salidas `humo-01-*`, `rollback-01-*`, `vuelta-01-*` y respuestas guardadas sin la clave.
