# Breakpoint de caché tras el tramo estable del mensaje de usuario (AB#101428)

Diseño corto. Padre AB#99934. Origen: sección 10 de `2026-11-18-prerrequisitos-cutover-tdn1-virtual-cache-luna-design.md` (medición del 2026-11-18). Rama `feature/101411-prerrequisitos-cutover`, worktree `documento-ia-luna`.

## Intención

Con gpt-5.6-luna la caché de prompt solo lee en breakpoints. Hoy el único breakpoint está al final del system prompt (1.127 tokens), y el tramo estable grande (instrucciones y catálogo, unos 12.400 tokens) viaja en el mensaje de usuario sin breakpoint. Resultado medido: 6,1% de la fase 1 en caché y ratio de coste 0,85 frente al control. Este cambio pone un segundo breakpoint al final del tramo estable del mensaje de usuario para que la caché lea lo mismo que lee gpt-4.1-mini (unos 13.500 tokens) y el ratio baje hacia 0,5. Es la unidad 1 del orden acordado: breakpoint, golden completo, umbral de confianza, cutover con ADR.

## Objetivos, no objetivos y anti-objetivos

- Objetivo: fase 1, fase 2 y restringida envían el mensaje de usuario en dos partes de texto, tramo estable y resto, con `prompt_cache_breakpoint` en la primera cuando el modelo tiene caché encendida.
- Objetivo: medir con la muestra de 50 y cumplir los criterios de AB#101428.
- No objetivo: tocar plantillas de BD (`PromptTemplates`), filas por defecto de `ModeloConfigs` o `prompt.gpt56-luna`, ni dependencias NuGet.
- No objetivo: proteger el prefijo frente a un `Instrucciones.Prompt` variable. Decisión del usuario (2026-11-18): si un cliente envía prompt, se asume que el catálogo no casa y se paga la escritura.
- Anti-objetivo: que el modelo reciba un texto distinto al actual, o que cambie el cuerpo de cualquier modelo sin `promptCache` encendido (gpt-4.1-mini incluido).

## Restricciones

- Solo DEV. Despliegue de la rama con el 802 y vuelta a `develop` al terminar (cada run con sí).
- `promptCache.mode` sigue en `explicit` en la fila luna. Dos breakpoints por petición, dentro del límite de cuatro escrituras en explicit.
- El prefijo hasta el segundo breakpoint incluye el system, así que siempre supera los 1.024 tokens mínimos.
- SDK `Azure.AI.OpenAI` 2.9.0-beta.1 sin cambiar.

## Enfoques considerados

- **A. Dos partes de texto en el mensaje de usuario, breakpoint en la primera (elegido).** No cambia el texto que ve el modelo ni las plantillas, y deja intactos los modelos sin caché. Coste: cambia la firma interna de `CompleteChatAsync` y sus tres llamadas.
- **B. Mover instrucciones y catálogo al system prompt.** Reutiliza el breakpoint actual, pero cambia el prompt de todos los modelos e invalida la comparación con las pasadas 1 a 3. Descartado.
- **Marcador en el texto que la política parte.** Descartado: sin política activa el marcador llegaría al modelo.

El usuario eligió A el 2026-11-18.

## Lo decidido

1. **Corte.** Cada montaje parte la plantilla de usuario en el primer `{DOCUMENT_TEXT}` antes de sustituir. Tramo estable: la mitad anterior con sus sustituciones (`{CONTEXT_PROMPT}`, `{TDN1_CATALOG}`, `{TDN1_CODE}`, `{TDN2_CATALOG}`, `{CATALOGO}`). Resto: el documento y todo lo que va detrás, incluida la instrucción adicional de resumen que hoy se añade al final.
2. **Envío.** `CompleteChatAsync` recibe tramo estable y resto por separado. Con caché encendida, el mensaje de usuario lleva dos `ChatMessageContentPart` de texto. Con caché apagada o ausente, una sola parte con la concatenación, de modo que el cuerpo es byte a byte el de hoy.
3. **Política.** `PromptCacheRequestPolicy`, con `breakpoint` activo, marca además la primera parte del primer mensaje `user` cuando su `content` es un array de más de una parte. Con una sola parte no marca nada en el usuario.
4. **Telemetría.** `TrackPrompt` y el log de prompt completo siguen recibiendo el texto de usuario concatenado.
5. **Medición.** Muestra de 50 del Batch con `-OnlyFiles`, misma configuración que las pasadas 1 a 3 (`ParallelQueries` 2, `MaxPages` 10, `ClassificationOnly`, `ForceReprocess`), consultas `medicion/01-03`. Se compara contra el control de la sección 10 (0,006391 €/doc). Si el control se repite en la misma sesión, mejor; si no, se usa el del 2026-11-18 y se dice.

## Criterios de aceptación (de AB#101428)

- Al menos el 50% de la entrada de la fase 1 en caché en la pasada de luna.
- Ratio de coste medio por documento frente al control ≤ 0,7.
- Cero HTTP 400.
- Cuerpo idéntico al actual con `promptCache` apagado o ausente (test de regresión).
- Suite unitaria verde, build sin warnings nuevos y `dotnet format --verify-no-changes` limpio.

## Para el constructor

- Firma exacta de `CompleteChatAsync` y si el corte vive en un helper común. Recomendación: un helper estático que devuelva `(estable, resto)` desde plantilla y sustituciones, con tests propios.
- Plantilla sin `{DOCUMENT_TEXT}`: todo va como una sola parte y no hay breakpoint en el usuario.
- Diferencia conocida y aceptada: hoy, si un valor sustituido contiene literalmente un marcador como `{DOCUMENT_TEXT}`, la cadena de `Replace` lo vuelve a sustituir. Con el corte previo, no. Es un caso patológico; basta un comentario.
- Test de hilo con transporte falso, como `OpenAiReasoningWireTests`, que compruebe que el SDK serializa dos partes como array `content` sin fusionarlas y que la política deja el breakpoint en la primera.
