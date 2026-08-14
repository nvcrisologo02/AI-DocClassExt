# Prompt de clasificación completo (tal como se envía al modelo)

Documento de referencia: reconstruye, literalmente y en orden, los mensajes que `GptClasificarDataProvider`
envía a Azure OpenAI para clasificar un documento, incluyendo qué placeholder se sustituye por qué, qué
bloques son condicionales y qué parámetros acompañan a la llamada.

Complementa a [ESPECIFICACION_PROMPTS_CONFIGURABLES.md](ESPECIFICACION_PROMPTS_CONFIGURABLES.md) (decisiones
de arquitectura, versionado y placeholders permitidos): aquí se ve el prompt **renderizado**, no la política.

> **Origen de los textos de este documento — LEER ANTES DE USARLO**
>
> Los literales reproducidos aquí proceden del **repositorio**: el seed inicial
> ([20260611180000_SeedInitialPrompts.cs](../../src/backend/DocumentIA.Data/Migrations/20260611180000_SeedInitialPrompts.cs))
> y el fallback de
> [appsettings.json](../../src/backend/DocumentIA.Functions/appsettings.json) (`ClassificationPrompts`),
> idénticos entre sí.
>
> **No son los prompts que corren en producción.** Comprobado el 2026-08-04 contra `srbsqlprodocai`:
> las versiones activas en `PromptTemplates` se editaron desde el Admin y son mucho más extensas
> (`classification.phase1.system` 4.342 chars vs ~600 del seed; `classification.phase1.user`
> 15.099 chars vs ~700), con puertas de desambiguación, ejemplos contrastivos, calibración explícita
> de confianza y orden de claves forzado (`confianza` primero).
>
> Este documento sirve como **mapa del mecanismo** (qué se sustituye, qué es condicional, qué
> parámetros se envían), que sigue siendo válido. Para el texto real ver el snapshot
> [PROMPT_PHASE1_RENDERIZADO_PRO.md](../auxiliares/temps/2026-08-04/PROMPT_PHASE1_RENDERIZADO_PRO.md)
> o extraerlo en vivo según §7.

---

## 1. Dónde encaja la clasificación GPT

`IClasificarDataProvider` se resuelve a `ConfigurableClasificarDataProvider`
([Program.cs:206](../../src/backend/DocumentIA.Functions/Program.cs#L206)), que enruta a un flujo de providers
(`rules`, `di`, `hybrid-tdn`, `gpt`, `mock`). La clasificación por prompt aquí descrita es la del provider
`gpt` / `azure-openai` → [GptClasificarDataProvider.cs](../../src/backend/DocumentIA.Functions/Services/GptClasificarDataProvider.cs),
que además es el **fallback global por defecto** (`ClassificationRouting:GlobalFallbackProvider = "gpt"`).

El provider hace **dos llamadas encadenadas e independientes** al modelo (no hay historial: cada llamada es
un `system` + un `user` nuevos):

| Fase | Operación (telemetría) | Objetivo | Salida |
|------|------------------------|----------|--------|
| 1 | `classification.phase1` | Familia documental TDN1 | `{tdn1, propuesta, [resumen], confianza}` |
| 2 | `classification.phase2` | Tipología TDN2 dentro de la familia | `{tdn2, confianza}` |

La Fase 2 solo se ejecuta si la Fase 1 resolvió un TDN1, si `NivelClasificacion` es `TDN1_TDN2` (valor por
defecto) y si la familia tiene catálogo TDN2. El gate de confianza de Fase 1 (`<= 0.6` cortaba el flujo) está
**comentado** deliberadamente en el código: hoy siempre se intenta la Fase 2.

---

## 2. Anatomía de la llamada HTTP

Ambas fases usan el mismo armado en `CompleteChatAsync`
([GptClasificarDataProvider.cs:521-567](../../src/backend/DocumentIA.Functions/Services/GptClasificarDataProvider.cs#L521-L567)):

```csharp
var systemMessage = new SystemChatMessage(systemText);
var userMessage   = new UserChatMessage(ChatMessageContentPart.CreateTextPart(userText));

var options = new ChatCompletionOptions
{
    ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()   // response_format: {"type":"json_object"}
};
OpenAiModelCapabilities.ConfigureChatOptions(options, model.DeploymentName, model.Temperature,
    Math.Max(model.MaxTokens, maxOutputTokens ?? model.MaxTokens));

await chatClient.CompleteChatAsync(new List<ChatMessage> { systemMessage, userMessage }, options, ct);
```

Equivalente en JSON de la petición:

```json
{
  "messages": [
    { "role": "system", "content": "<PROMPT SYSTEM DE LA FASE>" },
    { "role": "user",   "content": "<PROMPT USER DE LA FASE, YA RENDERIZADO>" }
  ],
  "response_format": { "type": "json_object" },
  "temperature": <ClassificationModelConfig.Temperature>,
  "max_tokens": <max(model.MaxTokens, MaxTokens del prompt de resumen)>
}
```

Notas relevantes:

- **No se usa JSON Schema estructurado**, solo `json_object`. El formato exacto se impone por texto dentro del
  system prompt, y la respuesta se parsea con tolerancia en `GptHierarchicalClassificationParser`.
- **No se envían tools, few-shots ni mensajes de asistente.** Exactamente dos mensajes por llamada.
- **El documento nunca viaja como imagen/binario.** Va como texto plano dentro del prompt de usuario
  (markdown de DI Layout, ver §3.3).
- `temperature` y `max_tokens` **se omiten** para familias de razonamiento (`gpt-5*`, `o1`/`o3`/`o4`…), que las
  rechazan ([OpenAiModelCapabilities.cs](../../src/backend/DocumentIA.Functions/Services/OpenAiModelCapabilities.cs)).
- Sin reintentos del SDK (`maxRetries: 0`); la resiliencia (retry + circuit breaker + timeout por intento) la
  aporta `IAzureOpenAIResilienceExecutor`.

---

## 3. Fase 1 — Clasificación TDN1

### 3.1 System prompt (literal)

Clave BD: `classification.phase1.system`

```text
Eres un sistema experto en clasificación de documentos del sector inmobiliario español, especialmente documentos de SAREB (Sociedad de Gestión de Activos procedentes de la Reestructuración Bancaria). Analiza el documento adjunto y clasifícalo en una familia TDN1. Clasifica por el acto jurídico principal del documento, ignorando el medio de remisión (correo, notificación, traslado, etc.). Responde exclusivamente en JSON válido con esta estructura: {"tdn1": "CODIGO_TDN1" | null, "propuesta": "texto libre", "resumen": "resumen ejecutivo", "confianza": 0.0-1.0}. El campo 'confianza' debe ser un número entre 0.0 (ninguna certeza) y 1.0 (certeza absoluta) que refleje tu nivel de confianza en la clasificación. No incluyas texto fuera del JSON.
```

**Bloque condicional** — solo si la petición pide resumen (`GenerarResumenPorDefecto = true`) y el prompt de
resumen resuelto trae `SystemPrompt`, se **concatena al final**:

```text


INSTRUCCIÓN ADICIONAL PARA 'resumen':
{SystemPrompt del prompt de resumen resuelto: BD summary.system → tipología → PromptDefaults}
```

### 3.2 User prompt — plantilla (literal)

Clave BD: `classification.phase1.user`

```text
Prompt adicional de instrucciones (si aplica):
{CONTEXT_PROMPT}

Familias TDN1 disponibles:
{TDN1_CATALOG}

Si no puedes resolver una familia, devuelve tdn1=null y completa propuesta con una sugerencia no vinculante. Comenzando siempre por el codigo de la tipologia propuesta, seguido de la justificacion en no mas de 200 caracteres (ejemplo: 'ESCR-06: Se trata de una escritura de dación en pago en la que los deudores transmiten un bien al acreedor para cancelar la deuda hipotecaria existente y extinguir las obligaciones derivadas.'). - No inventes códigos ni nombres fuera del catálogo.

CONTENIDO DEL DOCUMENTO (texto/markdown):
{DOCUMENT_TEXT}
```

### 3.3 Sustitución de placeholders

| Placeholder | Se sustituye por | Origen |
|-------------|------------------|--------|
| `{CONTEXT_PROMPT}` | `SystemPrompt: <…>` y/o `UserPromptTemplate: <…>` de `Instrucciones.Prompt` de la petición, unidos por salto de línea. Si no hay ninguno → literal **`(sin prompt adicional)`** | `BuildInstructionPromptContext` |
| `{TDN1_CATALOG}` | Una línea por familia activa: `- {Codigo}: {Nombre}, {Descripcion}` | `ClassificationTipologiaPromptBuilder.BuildTdn1Catalog()` sobre `CatalogoTdn1` (activas), cacheado 5 min |
| `{DOCUMENT_TEXT}` | Texto del documento (markdown de DI Layout). Se toma la primera clave no vacía de `DatosNormalizados`: `Markdown`, `markdown`, `Texto`, `texto`, `ContentText`, `contentText`. Si no hay ninguna → cadena vacía | `ObtenerContextoTexto` |

**Bloques condicionales que se añaden al final del user prompt:**

1. Si hay prompt de resumen resuelto:

   ```text


   Instrucción adicional para devolver en resumen:
   {UserPromptTemplate del prompt de resumen, ya interpolado con {contenido}}
   ```

2. Si **no hay texto** de documento (`contextoTexto` vacío) — además se emite un `LogWarning`:

   ```text


   No hay contenido textual disponible para este fallback. Nombre de archivo: {nombre del fichero}.
   ```

> **Atención**: el segundo bloque implica que, sin texto extraído, el modelo clasifica **solo por el nombre del
> fichero**. Es la causa raíz de los "aciertos" engañosos de TDN1 cuando la extracción de texto falla.
>
> **Actualización 2026-08-10 (AB#100045).** Esta situación era sistemática con **todo documento
> no-PDF**: en modo blob-first el recorte de páginas fallaba con XLSX/PPTX/DOCX, el orquestador se
> quedaba sin base64 y el paso 2.8 llamaba a Document Intelligence sin contenido, recibiendo un
> `400 InvalidContent` que se capturaba en silencio. Toda la clasificación de Office se decidía por
> el nombre de fichero. Corregido: el paso 2.8 transporta el documento por `BlobPath` cuando no hay
> base64, y `PdfRecorteService` omite el recorte con formatos que no son PDF.
>
> Nótese que esta guarda de "sin contenido" es la de **clasificación**, distinta de la de
> **prompt/resumen** (`SIN_CONTENIDO_DOCUMENTO`): la clasificación sigue intentándose con el nombre
> del fichero, mientras que un prompt sin contenido aborta sin invocar al modelo.

### 3.4 Ejemplo renderizado (Fase 1)

Petición sin prompt adicional, sin resumen, con markdown disponible.

**system**

```text
Eres un sistema experto en clasificación de documentos del sector inmobiliario español, especialmente documentos de SAREB (Sociedad de Gestión de Activos procedentes de la Reestructuración Bancaria). Analiza el documento adjunto y clasifícalo en una familia TDN1. Clasifica por el acto jurídico principal del documento, ignorando el medio de remisión (correo, notificación, traslado, etc.). Responde exclusivamente en JSON válido con esta estructura: {"tdn1": "CODIGO_TDN1" | null, "propuesta": "texto libre", "resumen": "resumen ejecutivo", "confianza": 0.0-1.0}. El campo 'confianza' debe ser un número entre 0.0 (ninguna certeza) y 1.0 (certeza absoluta) que refleje tu nivel de confianza en la clasificación. No incluyas texto fuera del JSON.
```

**user**

```text
Prompt adicional de instrucciones (si aplica):
(sin prompt adicional)

Familias TDN1 disponibles:
- ACTE: Actas de expropiación, Actas formales de expropiación: mutuo acuerdo, ocupación, acta previa o manifestación posesoria. Excluye hojas de aprecio, pagos, alegaciones e informes.
- ACTR: Actas de reunión, Actas de reuniones, juntas, consejos, asambleas o comités. Excluye actas técnicas de obra, actas expropiatorias y notariales.
- ACTT: Actas técnicas, Actas técnicas de obra/control: inicio, replanteo, paralización, reinicio, seguimiento, recepción, rechazo, suspensión.
...
- ESCR: Escrituras, Escrituras públicas notariales de negocio jurídico: compraventa, préstamo, cancelación, dación, poder, herencia...
...
- SERE: Sentencias y resoluciones judiciales, Resoluciones/actos de juzgado, tribunal o LAJ: sentencia, auto, decreto, providencia, diligencia, mandamiento...
- TASA: Tasaciones y Valoraciones, Tasaciones, valoraciones o informes periciales cuyo objeto principal es fijar valor económico. Excluye certificados técnicos.
   [~60 familias activas en total]

Si no puedes resolver una familia, devuelve tdn1=null y completa propuesta con una sugerencia no vinculante. Comenzando siempre por el codigo de la tipologia propuesta, seguido de la justificacion en no mas de 200 caracteres (ejemplo: 'ESCR-06: Se trata de una escritura de dación en pago en la que los deudores transmiten un bien al acreedor para cancelar la deuda hipotecaria existente y extinguir las obligaciones derivadas.'). - No inventes códigos ni nombres fuera del catálogo.

CONTENIDO DEL DOCUMENTO (texto/markdown):
# ESCRITURA DE COMPRAVENTA

NUMERO MIL DOSCIENTOS TREINTA Y CUATRO

En Madrid, a quince de marzo de dos mil veintidós.

Ante mí, JOSÉ MARÍA ... [markdown completo del documento, tal cual lo devuelve DI Layout]
```

**Respuesta esperada del modelo**

```json
{"tdn1": "ESCR", "propuesta": "ESCR-01: Escritura pública de compraventa de vivienda con subrogación hipotecaria.", "confianza": 0.94}
```

---

## 4. Fase 2 — Refinado TDN2

Se ejecuta con el TDN1 ya resuelto. **No incluye instrucciones de resumen**: el resumen se genera una sola vez
en Fase 1 y se reutiliza.

### 4.1 System prompt (literal)

Clave BD: `classification.phase2.system`

```text
Eres un sistema experto en clasificación de documentos del sector inmobiliario español, especialmente documentos de SAREB (Sociedad de Gestión de Activos procedentes de la Reestructuración Bancaria). Debes seleccionar exclusivamente una tipología de la familia TDN1 ya resuelta basándote en el contenido del documento. Responde exclusivamente en JSON válido con esta estructura: {"tdn2": "CODIGO_TDN2", "resumen": "resumen ejecutivo", "confianza": 0.0-1.0}. El campo 'confianza' debe ser un número entre 0.0 (ninguna certeza) y 1.0 (certeza absoluta) que refleje tu nivel de confianza en la clasificación. No incluyas texto fuera del JSON.
```

Se envía **sin modificaciones**: la Fase 2 no concatena bloques condicionales al system prompt.

### 4.2 User prompt — plantilla (literal)

Clave BD: `classification.phase2.user`

```text
Familia TDN1 resuelta: {TDN1_CODE}

Tipologías disponibles en esta familia:
{TDN2_CATALOG}

Si no puedes resolver la tipología exacta, devuelve una propuesta no vinculante comenzando siempre por el código de la tipología propuesta, seguido de la justificación en no más de 200 caracteres (ejemplo: 'ESCR-06: Se trata de una escritura de dación en pago en la que los deudores transmiten un bien al acreedor para cancelar la deuda hipotecaria existente y extinguir las obligaciones derivadas.').

CONTENIDO DEL DOCUMENTO (texto/markdown):
{DOCUMENT_TEXT}
```

### 4.3 Sustitución de placeholders

| Placeholder | Se sustituye por |
|-------------|------------------|
| `{TDN1_CODE}` | Código TDN1 resuelto en Fase 1 (normalizado a mayúsculas) |
| `{TDN2_CATALOG}` | **Ver abajo: tiene dos modos** |
| `{DOCUMENT_TEXT}` | El mismo texto de documento que en Fase 1 (no se recorta ni se resume) |

`{TDN2_CATALOG}` se resuelve en `BuildTdn2CatalogByFamilia(tdn1)` con esta precedencia:

1. **Prompt personalizado por familia** (camino habitual en producción): si `CatalogoTdn1.TDN2_Prompt` de esa
   familia tiene contenido, **ese texto completo se inyecta tal cual** en el placeholder. No es un simple
   listado: es un mini-prompt con objetivo, ámbito, catálogo TDN2 con definiciones y reglas de desempate.
2. **Fallback generado dinámicamente**: si no hay prompt personalizado, se construye una línea por tipología
   publicada de la familia con formato `- {CodigoTipologia} [{TDN2}: {NombreTDN2}] {descripción GPT}`.

Ambos modos se cachean 5 minutos por familia.

### 4.4 Ejemplo renderizado (Fase 2, con prompt personalizado)

**user** (familia `ACTE`)

```text
Familia TDN1 resuelta: ACTE

Tipologías disponibles en esta familia:
OBJETIVO:
Clasifica solo dentro de ACTE (Actas de expropiación). No inventes códigos. Decide por el acto principal, no por menciones accesorias.

ÁMBITO:
Documentos administrativos oficiales específicos de un procedimiento expropiatorio en el que se deja asiento de la descripción de un bien, la titularidad y ocupación, el importe de valoración y su abono o consignación, entre otros. Dentro de cada TDN2 se encuentran embebidos: Actas complementarias y otros documentos relacionados

CATÁLOGO TDN2:

ACTE-01 | Expropiación: acta de mutuo acuerdo
Definición: Acuerdo alcanzado entre las partes sobre el valor indemnizatorio de los bienes y derechos expropiados

ACTE-02 | Expropiación: acta de ocupación
Definición: Título bastante para que en los diferentes Registros Públicos se inscriba o tome razón de la transmisión de dominio del bien...

[resto del prompt personalizado de la familia: TDN2 restantes, reglas de desempate y exclusiones]

Si no puedes resolver la tipología exacta, devuelve una propuesta no vinculante comenzando siempre por el código de la tipología propuesta, seguido de la justificación en no más de 200 caracteres (ejemplo: 'ESCR-06: Se trata de una escritura de dación en pago en la que los deudores transmiten un bien al acreedor para cancelar la deuda hipotecaria existente y extinguir las obligaciones derivadas.').

CONTENIDO DEL DOCUMENTO (texto/markdown):
[markdown completo del documento]
```

**Respuesta esperada del modelo**

```json
{"tdn2": "ACTE-02", "confianza": 0.88}
```

---

## 5. Incoherencias conocidas entre prompt y código

Merecen mención porque afectan a lo que el modelo devuelve frente a lo que el sistema consume:

- **`resumen` en el system prompt de Fase 2**: el system prompt de Fase 2 pide `"resumen": "resumen ejecutivo"`,
  pero el código **ignora ese campo** (usa siempre el resumen de Fase 1) y el user prompt no da instrucciones de
  resumen. Se están gastando tokens de salida en un campo descartado.
- **`resumen` en el system prompt de Fase 1 sin resumen solicitado**: el texto en BD/appsettings pide siempre
  `resumen`, aunque no se haya solicitado. La constante alternativa sin `resumen`
  (`ClassificationTipologiaPromptBuilder.Phase1ResponseFormatInstruction`) se calcula en el código pero **no se
  concatena** al prompt resuelto desde BD.
- **`propuesta` con código de tipología**: el prompt solo la exige "en texto libre" empezando por el código.
  Cuando el modelo responde en prosa sin código, el parser aplica un mapeo tolerante contra el catálogo TDN1
  (`ResolverTdn1PorCatalogoDesdePropuesta`, AB#99984) antes de declarar `Desconocido`.
- **`{DOCUMENT_TEXT}` sin truncado** en el provider GPT: el markdown entero entra en ambas fases. El recorte por
  ventana (`MaxCharactersPerWindow = 32000`, `PagesToInspect = 3`) solo aplica al flujo `hybrid-tdn`.

---

## 6. Resolución de los prompts en runtime

`ClassificationPromptProvider.GetPromptSetAsync()` resuelve los cuatro textos con esta cadena:

```
Caché en memoria (TTL 120 s)
  └─ miss → BD: PromptTemplates, versión activa de las 4 claves
              classification.phase1.system / .phase1.user
              classification.phase2.system / .phase2.user
        └─ si falta alguna de las 4 → appsettings.json → ClassificationPrompts:Phase1/Phase2
```

- Se exigen **las 4 claves activas**; si falta una, se descarta la BD entera y se cae al fallback (se registra
  `WARNING` indicando cuáles existían).
- Si las 4 existen pero con versiones distintas, se usa igualmente y se registra `VERSION MISMATCH`.
- El `Source` (`Database` / `Fallback`) y la `Version` se registran en cada resolución y en cada clasificación
  ("Prompts de clasificación resueltos desde {Source} (versión: {Version})").
- La versión activa es **inmutable**: editar desde el Admin crea una nueva versión draft que hay que activar.

Los catálogos (`{TDN1_CATALOG}`, `{TDN2_CATALOG}`) tienen su **propia caché de 5 minutos**, independiente de la
de prompts: un cambio en `CatalogoTdn1`/tipologías puede tardar hasta 5 min en verse reflejado.

---

## 7. Cómo obtener el prompt exacto que se envió

Este documento reproduce los literales del repositorio. Para el texto **realmente enviado** en un entorno:

**a) Log completo del prompt final.** Con `ClassificationPrompts:EnableFullPromptLogging = true`
(valor actual en `appsettings.json`), el provider vuelca los cuatro textos ya renderizados:

```kql
traces
| where message has "[Classification] FULL FINAL PROMPT"
| project timestamp, message
| order by timestamp desc
```

**b) Telemetría `Prompt.Trace`** (`PromptTraceTelemetryService`, si `PromptTracing:Enabled`): registra por cada
llamada `provider`, `operation` (`classification.phase1` / `classification.phase2`), `tipologia`, `modelKey`,
`deployment`, longitudes y **SHA-256** de system y user prompt. Con `IncludePromptText = true` añade además
`systemPromptSnippet` / `userPromptSnippet` truncados a `MaxPromptTextChars`.

```kql
customEvents
| where name == "Prompt.Trace" and tostring(customDimensions.provider) == "gpt-classification"
| project timestamp, operation = customDimensions.operation,
          systemSha = customDimensions.systemPromptSha256,
          userSha = customDimensions.userPromptSha256,
          systemLen = customMeasurements.systemPromptLength,
          userLen = customMeasurements.userPromptLength
```

El SHA-256 permite verificar si el prompt vigente coincide con el de este documento sin volcar el texto.

**c) Admin**: `/admin/prompts` muestra el contenido, versión y estado (Activo/Draft) de cada clave.

**d) Consulta directa a BD** (requiere autorización explícita para el entorno objetivo):

```sql
SELECT PromptKey, Version, IsActive, PublishedAtUtc, LEN(Content) AS Chars, Content
FROM PromptTemplates
WHERE PromptKey LIKE 'classification.phase%' AND IsActive = 1
ORDER BY PromptKey;
```

---

## 8. Referencias de código

| Pieza | Fichero |
|-------|---------|
| Armado y envío de las dos fases | [GptClasificarDataProvider.cs](../../src/backend/DocumentIA.Functions/Services/GptClasificarDataProvider.cs) |
| Resolución BD → caché → appsettings | [ClassificationPromptProvider.cs](../../src/backend/DocumentIA.Functions/Services/ClassificationPromptProvider.cs) |
| Construcción de catálogos TDN1/TDN2 | [ClassificationTipologiaPromptBuilder.cs](../../src/backend/DocumentIA.Core/Configuration/ClassificationTipologiaPromptBuilder.cs) |
| Parseo tolerante de las respuestas | [GptHierarchicalClassificationParser.cs](../../src/backend/DocumentIA.Functions/Services/GptHierarchicalClassificationParser.cs) |
| Enrutado entre providers | [ConfigurableClasificarDataProvider.cs](../../src/backend/DocumentIA.Functions/Services/ConfigurableClasificarDataProvider.cs) |
| Capacidades por familia de modelo | [OpenAiModelCapabilities.cs](../../src/backend/DocumentIA.Functions/Services/OpenAiModelCapabilities.cs) |
| Trazas de prompt | [PromptTraceTelemetryService.cs](../../src/backend/DocumentIA.Functions/Services/PromptTraceTelemetryService.cs) |
| Seed inicial de los 4 prompts | [20260611180000_SeedInitialPrompts.cs](../../src/backend/DocumentIA.Data/Migrations/20260611180000_SeedInitialPrompts.cs) |
| Fallback de prompts | [appsettings.json](../../src/backend/DocumentIA.Functions/appsettings.json) → `ClassificationPrompts` |

> Nota: el prompt de **extracción** (no clasificación) lo construye
> [GptPromptBuilder.cs](../../src/backend/DocumentIA.Functions/Services/GptPromptBuilder.cs) y sigue un camino
> distinto (catálogo de campos por tipología con tipos y reglas de validación). No se documenta aquí.
