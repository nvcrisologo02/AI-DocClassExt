# ADR-004: Cambio del modelo de IA generativa de gpt-4.1-mini a gpt-5.6-luna
Fecha: 2026-11-20 · Estado: aceptada · Work Item: AB#99939 · Session ID: retroactivo

## Contexto y problema

El deployment `gpt-4o-mini` (modelo gpt-4.1-mini) atiende hoy la clasificación GPT de fallback, el prompt libre, el resumen y la extracción GPT de fallback. Azure OpenAI lo retira el 2027-04-14, así que hay que elegir sustituto y la forma de cambiarlo con vuelta atrás. La configuración de modelos vive en `dbo.ModeloConfigs` (filas por clave, indicadores `IsDefault` y `UseAsFallback`) y 219 tipologías de DEV fijaban `PromptConfig.ModelKey = default.gpt4o-mini`, de modo que un cambio de modelo de prompt exigía reescribir todas las tipologías.

## Opciones consideradas

- Candidatos de modelo: gpt-5.6-terra (×4-7 de coste sin ganancia medida), gpt-5.4-nano y gpt-5.6-luna, los dos últimos con `reasoning_effort` none (análisis del 2026-11-17, spec `2026-11-17-migracion-clasificacion-gpt56-luna-design.md`).
- Para prompt y resumen: (1) herencia de `PromptDefaults:ModelKey` con tipologías sin clave; (2) redirección de clave por app setting; (3) reescribir las 219 tipologías a `prompt.gpt56-luna`; (4) sobrescribir el `DeploymentName` de la fila `default.gpt4o-mini`.
- Para clasificación y extracción por fallback: mover `UseAsFallback` a filas nuevas de luna, conservando las de gpt-4.

## Decisión

gpt-5.6-luna (Data Zone Standard, `reasoning_effort` none, caché de prompt explícita en clasificación) sustituye a gpt-4.1-mini en clasificación GPT, prompt libre, resumen y extracción GPT. El cambio se hace por configuración: indicadores `UseAsFallback` en filas nuevas (`classification.gpt56-luna`, `extraction.gpt56-luna`, `default.gpt56-luna_ex`), tipologías sin clave de prompt que heredan `PromptDefaults:ModelKey`, y ese setting fijado por entorno en el pipeline.

## Motivos

- Golden de 467 documentos en DEV (2026-11-19): TDN1 66,0% y TDN2 41,8% frente a 56,8% y 34,3% del control; coste por documento 0,45 del control con el 76% de la fase 1 en caché; 0 HTTP 400 tras el reintento por mitades.
- Con el umbral de baja confianza en la tipología virtual (AB#101435): 79 contratos en baja confianza (29 virtuales persistidos) y 113 errores de TDN1 entre 388 OK; ningún OK por debajo de 0,6.
- Prompt libre y resumen comprobados con luna en DEV (AB#101374): respuestas equivalentes y sin tokens de razonamiento.
- La herencia deja la configuración diciendo la verdad: las tipologías no repiten el modelo y el siguiente cambio es un app setting. La redirección añadía un mecanismo nuevo y dejaba 219 tipologías mintiendo; reescribirlas obligaba a repetirlo en cada cambio; sobrescribir la fila de gpt-4 rompía la convivencia.
- Las filas de gpt-4 se conservan: el rollback es mover los indicadores (script 07) y el setting, sin borrar nada.

## Consecuencias

- Si `PromptDefaults:ModelKey` no resuelve, prompt y resumen fallan siempre: no hay vuelta silenciosa a gpt-4. Antes de fijar el setting en un entorno, la fila `prompt.gpt56-luna` debe existir y estar activa.
- La extracción GPT de fallback pasa a luna sin medición propia, por volumen residual.
- `ensure-app-settings.ps1` no pisa claves existentes: cambiar `PromptDefaults__ModelKey` tras el primer despliegue exige `az functionapp config appsettings set` además de la variable del pipeline.
- Tras cada 799 o seed de tipologías hay que revisar indicadores, tipología 2001 y claves de prompt (README de `scripts/migrations/gpt56-luna/`).
- PRE y PRO llegan con la release: deployment Data Zone y cuota, filas y tarifa, scripts 06/07/08 portados, setting por entorno y vigilancia de 48 h en PRO. El deployment `gpt-4o-mini` se mantiene durante la convivencia; su borrado es una decisión posterior.
- Resultado del cutover en DEV (2026-11-20): run 80195 del 802 sobre 20fec98 en verde con `PromptDefaults__ModelKey = prompt.gpt56-luna`; el 08 cambió 219 tipologías y el 06 dio 2 altas, 4 indicadores y la 2001 (segunda ejecución de ambos sin cambios); humo de 50/50 OK con clasificación, prompt, resumen y extracción en luna y 0 errores de resolución; rollback ensayado en ambos sentidos (07 más setting a `default.gpt4o-mini`, y 06 más setting de vuelta) con las pasadas cortas en el modelo esperado (la de rollback con 1 ERROR, ver límites), y DEV queda en luna. Límites: la pasada de rollback fue 10 OK y 1 ERROR en `PersistirActivity` (0 filas afectadas) por procesar en paralelo el mismo fichero dos veces, defecto preexistente (Bug AB#101437); el recuento «Ok» del script de humo se basa en `runtimeStatus = Completed`. La edición del Admin no se comprobó. El modo combinado de extracción tampoco se comprobó en DEV: solo lo cubre el test unitario `ModoCombinado_ClaveHeredada_SeActiva`, y se verifica en PRE. Detalle en la sección «Resultado en DEV (2026-11-20)» de la spec `2026-11-20-cutover-luna-dev-design.md`.

## Alternativas descartadas y por qué

- gpt-5.6-terra: coste ×4-7 sin ganancia medida de calidad.
- Redirección de clave por app setting: mecanismo nuevo y configuración que no dice la verdad.
- Reescribir las 219 tipologías a `prompt.gpt56-luna`: se repite en cada cambio de modelo y el rollback es masivo.
- Sobrescribir el `DeploymentName` de `default.gpt4o-mini`: las filas de gpt-4 no se sustituyen, por decisión del usuario, para conservar la convivencia y el rollback.

## Notas posteriores

- 2026-11-20 (AB#101438): el «falla siempre» de Consecuencias aplica al prompt libre y al resumen de `OpenAIPromptDataProvider`. En la clasificación GPT la clave del resumen es solo una etiqueta y no selecciona modelo: sin `PromptDefaults:ModelKey` el resumen sale sin etiqueta y el error queda en el log (commit 66babf8). La decisión no cambia.
- 2026-11-22 (AB#101447, revisión documental): la documentación permanente redacta el modelo generativo por entorno (DEV `gpt-5.6-luna`; PRE y PRO `gpt-4o-mini` = gpt-4.1-mini hasta la siguiente release). El deployment de `gpt-5.6-luna` sigue sin declararse en `infra/ai/deployments.dev.json`; se incorpora con la release a PRE y PRO.
