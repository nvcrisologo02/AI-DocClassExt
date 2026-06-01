# Contrato de la API HTTP — DocumentIA

## 1. Descripción

El sistema expone una única Function HTTP de entrada: `IngestDocument`. El procesamiento es **asíncrono** (Durable Functions): la petición `POST` devuelve inmediatamente un `202 Accepted` con un `instanceId`, y el cliente hace polling sobre la URL de estado para conocer el resultado final.

Este documento define el contrato formal de la API (campos, validaciones y semántica). Para guía de uso paso a paso y ejemplos operativos, ver `docs/05_MANUAL_USO_CONFIGURACION.md`.

---

## 2. Autenticación

La función usa `AuthorizationLevel.Function`. Se requiere incluir la **Function Key** en cada petición.

**Cabecera recomendada:**
```
x-functions-key: <function-key>
```

También se acepta como query parameter: `?code=<function-key>`

En local (con `func host start`), el nivel es `Anonymous` efectivamente — no se requiere clave.

---

## 3. Endpoint de ingesta

### `POST /api/IngestDocument`

#### Headers

| Header | Requerido | Valor |
|---|---|---|
| `Content-Type` | Sí | `application/json` o `multipart/form-data` |
| `x-functions-key` | Sí (producción) | Function Key |

#### Body — `ContratoEntrada`

```json
{
  "instrucciones": {
    "expectedType": "",
    "skipDuplicateCheck": false,
    "forceReprocess": false,
    "classificationOnly": false,
    "executeIntegrarWhenClassificationOnly": null,
    "maxPagesForClassificationOnly": 0,
    "skipGDCUpload": null,
    "classification": {
      "provider": "auto",
      "model": "auto",
      "nivelClasificacion": null,
      "markdown": null
    },
    "extraction": {
      "provider": "auto",
      "model": "auto"
    },
    "prompt": {
      "systemPrompt": "Eres un asistente experto en documentos inmobiliarios.",
      "userPromptTemplate": "Resume el siguiente documento en 3 puntos clave:\n\n{{CONTENT}}",
      "modelKey": "gpt-4o-mini",
      "temperature": 0.2,
      "maxTokens": 800,
      "contentMode": "markdown"
    }
  },
  "documento": {
    "name": "nota_simple_finca_123.pdf",
    "objectIdGDC": null,
    "blobPath": null,
    "content": {
      "base64": "<contenido-en-base64>"
    }
  },
  "trazabilidad": {
    "correlationId": "a1b2c3d4-0000-0000-0000-000000000000",
    "submittedBy": "sistema-origen",
    "idActivo": null
  }
}
```

#### Descripción de campos

**`instrucciones`**

| Campo | Tipo | Descripción |
|---|---|---|
| `expectedType` | string | Si se especifica, omite la clasificación IA y usa este valor como tipología. Ej: `"nota-simple"`. Vacío = clasificación automática. |
| `skipDuplicateCheck` | bool | `true` = omitir la verificación de duplicados por SHA256. Default: `false`. |
| `forceReprocess` | bool | `true` = aunque el documento sea duplicado, procesar igualmente (no reutilizar ejecución anterior). Default: `false`. |
| `classificationOnly` | bool | `true` = ejecutar solo clasificación/resolución de tipología. Omite `Extraer`, `Prompt`, `Validar` y `ObtenerActivo` (marcadas como `Skipped` en seguimiento). Default: `false`. |
| `executeIntegrarWhenClassificationOnly` | bool? | Solo aplica cuando `classificationOnly=true`. `null/false` = no ejecutar `Integrar`. `true` = ejecutar `Integrar` si hay `trazabilidad.idActivo`. |
| `maxPagesForClassificationOnly` | int | Solo aplica cuando `classificationOnly=true`. `0` = sin límite. `N > 0` = usa solo las primeras N páginas para clasificación. |
| `skipGDCUpload` | bool? | `null` = respetar la configuración de la tipología. `true` = no subir al GDC. `false` = forzar subida. |
| `classification.provider` | string | `auto` \| `azure-document-intelligence` \| `gpt` \| `mock`. Si se informa `nivelClasificacion`, el sistema **fuerza automáticamente** `provider="gpt"` (D2). |
| `classification.model` | string | Reservado. Usar `"auto"`. El model key se resuelve desde la configuración de la tipología. |
| `classification.umbral` | double? | _(Opcional)_ Umbral de confianza para activar fallback GPT y para el check `BAJA_CONFIANZA_CLASIFICACION`. `[0.0–1.0]`. Si se omite (`null`), se aplica la jerarquía: tipología → configuración servidor. |
| `classification.nivelClasificacion` | string? | _(Opcional)_ Nivel jerárquico de clasificación deseado. Valores: `"TDN1"` (solo nivel 1) \| `"TDN1/TDN2"` (dos fases, nivel completo). `null` / vacío = clasificación completa por defecto. Si se informa, el proveedor se fuerza a `"gpt"` automáticamente y el campo forma parte de la clave de deduplicación (`SHA256 + classificationOnly + nivelClasificacion`). |
| `classification.markdown` | string? | _(Opcional)_ Markdown pre-procesado del documento aportado por el caller. Si se informa, el paso `ExtraerMarkdownLayoutActivity` (paso 2.8) se omite y se usa este texto directamente como contexto para la clasificación. Útil para reutilizar texto ya extraído en integraciones batch. |
| `extraction.provider` | string | `auto` \| `azure-content-understanding` \| `azure-cu` \| `azure-document-intelligence` \| `azure-di` \| `azure-openai` \| `gpt` \| `mock`. Si se especifica un valor distinto de `"auto"`, sobreescribe el proveedor configurado en la tipología para esta petición. Con `azure-openai` se activa extracción GPT directa (sin CU). |
| `extraction.model` | string | Model key del registro de modelos de extracción. Si se especifica un valor distinto de `"auto"`, sobreescribe el `modelKey` configurado en la tipología para esta petición. Debe coincidir con una clave publicada en `ModeloConfigs` (los JSON de modelos son solo seed/referencia). |
| `extraction.umbral` | double? | _(Opcional)_ Ratio mínimo de campos para considerar la extracción CU suficiente. `[0.0–1.0]`. Si se omite (`null`), se aplica la jerarquía: tipología → configuración servidor (`MinFieldsRatio`). |

**`instrucciones.prompt`** _(opcional)_

Permite ejecutar un prompt ad-hoc sobre el documento sin necesidad de configurar la tipología en base de datos. Si la tipología ya tiene `PromptConfig` definida, los campos informados en este objeto tienen **precedencia** sobre la configuración de tipología (merge campo a campo).

| Campo | Tipo | Validación | Descripción |
|---|---|---|---|
| `prompt.systemPrompt` | string? | ≤ 5000 chars | System message para el modelo. Si se omite, se usa el de la tipología o ninguno. |
| `prompt.userPromptTemplate` | string? | ≤ 5000 chars | Plantilla de usuario. Puede incluir `{{CONTENT}}` como marcador del contenido del documento. Si se omite, se usa el de la tipología. |
| `prompt.modelKey` | string? | Clave existente en registro de modelos | Modelo a usar. Si se omite, se usa el de la tipología o el default del sistema. |
| `prompt.temperature` | double? | `[0.0 – 2.0]` | Temperatura de generación. Si se omite, se usa la de la tipología o `0.0`. |
| `prompt.maxTokens` | int? | `[100 – 4000]` | Máximo de tokens en la respuesta. Si se omite, se usa el de la tipología o `2000`. |
| `prompt.contentMode` | string? | `"markdown"` \| `"vision"` | Cómo se envía el contenido al modelo. `markdown` = texto extraído; `vision` = imagen del documento. Si se omite, se usa el de la tipología o `"markdown"`. |

> **Nota:** Si la tipología tiene `PromptEnabled = false` y **no** existe `instrucciones.prompt` en la petición, el paso de prompt se omite. Si se envía `instrucciones.prompt`, el paso se ejecuta aunque la tipología no tenga `PromptConfig`.

> **Resultado:** El resultado del prompt se almacena en `datosExtraidos["PromptResult"]` dentro del output final.

**`instrucciones.assetResolver`** _(opcional)_

| Campo | Tipo | Descripción |
|---|---|---|
| `assetResolver.enabled` | bool? | `true` = forzar resolucion de activo via AssetResolver. `false` = desactivar. `null` = respetar config tipologia (default: deshabilitado). |
| `assetResolver.camposBusqueda` | object? | Override de valores de busqueda. Si se informa, sobreescribe los datos extraidos del documento. |
| `assetResolver.camposBusqueda.idufir` | string? | IDUFIR a buscar directamente (sin extraer del documento). |
| `assetResolver.camposBusqueda.referenciaCatastral` | string? | Referencia Catastral a buscar directamente. |
| `assetResolver.camposSolicitados` | string[]? | Columnas de `DM_POSICION_AAII_TB` a retornar. Si `null`, usa config tipologia o default (`ID_ACTIVO_SAREB`). |

**`documento`**

| Campo | Tipo | Descripción |
|---|---|---|
| `name` | string | Nombre del fichero (con extensión). Recomendado; si llega vacío con `objectIdGDC`, se intenta completar desde metadatos de GDC. |
| `objectIdGDC` | string? | ObjectId del documento ya archivado en GDC. Si se informa, no debe enviarse `content.base64` ni `blobPath`. |
| `blobPath` | string? | Ruta `container/path` del documento ya subido a Blob Storage (modo blob-first). Si se informa, no debe enviarse `objectIdGDC`. |
| `content.base64` | string? | Contenido del documento codificado en Base64. Opcional cuando se informa `blobPath` u `objectIdGDC`. |

**`trazabilidad`**

| Campo | Tipo | Descripción |
|---|---|---|
| `correlationId` | string | ID de correlación para trazabilidad. Si no se envía, el sistema genera un UUID. |
| `submittedBy` | string | Identificador del sistema o usuario que envía el documento. |
| `idActivo` | string? | ID del activo al que pertenece el documento. Opcional; puede ser resuelto por plugins. |

Reglas de validación de entrada:

- `documento.objectIdGDC` es excluyente con `documento.content.base64` y `documento.blobPath`.
- Debe enviarse al menos una fuente de documento: `objectIdGDC`, `blobPath` o `content.base64`.
- Si se usa `documento.objectIdGDC`, el backend fuerza `instrucciones.skipGDCUpload = true`.
- `classificationOnly=true` es incompatible con `expectedType` informado. El trigger HTTP devuelve `400`.

Modo multipart/blob-first:

- En `multipart/form-data` se esperan dos partes: `file` (binario) y `metadata` (JSON `ContratoEntrada`).
- El trigger sube el binario a Blob Storage antes de iniciar la orquestación y rellena hashes/tamaño precomputados.

---

#### Respuesta — `202 Accepted`

```json
{
  "instanceId": "abc123def456...",
  "statusQueryUri": "https://<host>/runtime/webhooks/durabletask/instances/abc123def456...",
  "correlationId": "a1b2c3d4-0000-0000-0000-000000000000"
}
```

| Campo | Descripción |
|---|---|
| `instanceId` | ID de la instancia de orquestación Durable. Usar para polling. |
| `statusQueryUri` | URL completa para consultar el estado de la instancia. |
| `correlationId` | Copia del `correlationId` enviado en la petición. |

#### Respuestas de error

| Código | Causa |
|---|---|
| `400 Bad Request` | Body inválido, `ContratoEntrada` no deserializable, `instrucciones.prompt` fuera de rango, o violación de reglas de entrada (`objectIdGDC` + `base64` simultáneos / ninguno informado, `classificationOnly` + `expectedType`). |
| `401 Unauthorized` | Function Key ausente o inválida. |
| `500 Internal Server Error` | Error inesperado en el trigger. |

---

## 4. Polling de estado

### `GET {statusQueryUri}`

La URL se obtiene del campo `statusQueryUri` del `202 Accepted`. Requiere la Function Key del Durable Task extension.

#### Estados intermedios (Running)

```json
{
  "name": "DocumentProcessOrchestrator",
  "instanceId": "abc123...",
  "runtimeStatus": "Running",
  "customStatus": {
    "actividad": "Extraer",
    "completadas": ["Normalizar", "VerificarDuplicado", "SubirBlob", "Clasificar", "ResolverTipologia"],
    "totalActividades": 9,
    "duracionMs": 4200
  },
  "input": { ... },
  "output": null,
  "createdTime": "2026-03-27T10:00:00Z",
  "lastUpdatedTime": "2026-03-27T10:00:04Z"
}
```

#### Estado final — Éxito

```json
{
  "runtimeStatus": "Completed",
  "output": {
    "identificacion": {
      "documento": "nota_simple_finca_123.pdf",
      "guid": "xxxxxxxx-...",
      "tipologia": "nota.simple.1_4",
      "tipologiaFamilia": "nota-simple",
      "tipologiaVersion": "1.4",
      "fechaProceso": "2026-03-27T10:00:10Z",
      "paginas": 5,
      "propuestaTipologia": null
    },
    "integridad": {
      "crc32": "a1b2c3d4",
      "sha256": "abcdef...",
      "md5": "fedcba...",
      "tamanoBytes": 933347,
      "rutaBlobStorage": "documents/nota_simple_finca_123.pdf",
      "gestorDocumental": "GDC",
      "idActivo": "ACTIVO-001",
      "idActivoEntrada": null,
      "idActivoCambiado": false
    },
    "datosExtraidos": {
      "FincaRegistral": "12345",
      "IDUFIR_CRU": "...",
      "Direccion": "Calle Mayor 1",
      "ReferenciaCatastral": "...",
      "FechaDocumento": "2026-01-15"
    },
    "resultado": {
      "estado": "OK",
      "mensajeError": null,
      "confianzaGlobal": 0.92,
      "estadoCalidad": "OK",
      "confianzaClasificacion": 0.95,
      "confianzaExtraccion": 0.91,
      "confianzaValidacion": 0.9,
      "reutilizadaPorDuplicado": false,
      "mensajeReutilizacion": null
    },
    "detalleEjecucion": { ... }
      "detalleEjecucion": {
        "instanceId": "abc123def456...",
        "operationId": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4",
        "..."
      }
  }
}
```

---

## 4.bis. Healthcheck

### `POST /api/healthcheck`

Endpoint de salud para probes/monitoring (Application Insights, balanceadores, Admin UI, Desktop). No requiere Function Key (`AuthorizationLevel.Anonymous`). Solo método `POST`.

#### Request

Sin body (o body vacío). Sin headers obligatorios.

#### Respuesta — `200 OK`

```json
{
  "ok": true,
  "status": "healthy",
  "timestamp": "2026-05-01T08:30:00Z",
  "components": {
    "functions":     { "status": "healthy", "message": "Running" },
    "assetResolver": { "status": "healthy", "message": "HTTP 200" },
    "gdc":           { "status": "healthy", "message": "Reachable (document not found)" },
    "modelProviders": {
      "status": "healthy",
      "classification": { "status": "healthy", "message": "Loader registered" },
      "extraction":     { "status": "healthy", "message": "Loader registered" },
      "prompt":         { "status": "healthy", "message": "Loader registered" }
    }
  }
}
```

| Campo | Descripción |
|---|---|
| `ok` | Booleano agregado. `false` solo si `status == "unhealthy"`. |
| `status` | Estado agregado: `"healthy"` \| `"degraded"` \| `"unhealthy"` \| `"unconfigured"`. Calculado por precedencia `unhealthy > degraded > unconfigured > healthy`. |
| `timestamp` | Timestamp UTC del muestreo. |
| `components.functions` | Salud del runtime Functions (siempre `healthy` si la respuesta sale). |
| `components.assetResolver` | Probe HTTP a `GET {AssetResolver:BaseUrl}/api/assets/ping` (timeout 8 s). |
| `components.gdc` | Probe SOAP a `consultarObjetoExiste` con `idObjeto=HEALTHCHECK_PROBE` y MD5 sintético (timeout 5 s). |
| `components.modelProviders.classification\|extraction\|prompt` | Verifica que el `*ModelRegistryLoader` esté registrado en DI. **Es un objeto único, no un array.** |

> **Cache**: el snapshot se cachea 45 s en `IMemoryCache` (clave `SystemHealthService:ComponentsHealthSnapshot`). Llamadas concurrentes en esa ventana devuelven el mismo resultado.

#### Respuesta — `503 Service Unavailable`

Mismo payload que `200 OK` pero con `status == "unhealthy"` y `ok: false`. Devuelto cuando alguno de los componentes críticos falla.

> **Compatibilidad legacy**: si la inyección de `ISystemHealthService` no está disponible (tests aislados), el endpoint responde con un payload mínimo `{ "ok": true, "timestamp": "..." }`. En producción siempre se devuelve el payload extendido.

> **Origen dual AAII / AACC**: el healthcheck no expone subcomponente específico para origen AACC. La consulta dual (`DM_POSICION_AAII_TB` + `DM_POSICION_AACC_TB`) se ejecuta dentro del componente `assetResolver`. Las precedencias y flags de habilitación se documentan en `docs/especificaciones/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md`.

---

## 5. Estados posibles en `resultado.estado`

| Estado | Descripción |
|---|---|
| `OK` | Procesamiento completado correctamente. |
| `OK` _(clasificación parcial — tipología virtual)_ | Solo cuando `nivelClasificacion` activa clasificación GPT y el modelo no puede mapear a ningún código de catálogo. `identificacion.tipologia = "Desconocido"`, `identificacion.propuestaTipologia` contiene la propuesta libre del modelo. El pipeline se detiene: extracción y validación se omiten. |
| `NO_CLASIFICADO` | Clasificación parcial (`clasificacionParcial = true`) con código TDN1 conocido, pero `ResolverTipologiaActivity` no encontró la tipología completa TDN1/TDN2. `identificacion.tdn1` refleja el código TDN1 detectado. El pipeline continúa (extracción, validación) con la tipología parcial. |
| `VALIDACION_CON_ERRORES` | Extracción completada pero alguna regla de validación no se cumplió. Los datos se devuelven. |
| `BAJA_CONFIANZA_CLASIFICACION` | La confianza de clasificación está por debajo del umbral. Se devuelven datos con advertencia. |
| `DUPLICADO` | El documento ya existe en la base de datos (mismo SHA256 + `classificationOnly` + `nivelClasificacion`). Se devuelve la ejecución anterior reutilizada. Ver `reutilizadaPorDuplicado = true`. |
| `ERROR` | Error irrecuperable durante el procesamiento (clasificación fallida, excepción no controlada). Consultar `mensajeError`. |

---

## 6. `detalleEjecucion` — estructura completa

| Campo | Descripción |
|---|---|
| `detalleEjecucion.instanceId` | ID de la instancia de orquestación Durable Functions. Permite hacer polling de estado y localizar la ejecución en Azure Portal. |
| `detalleEjecucion.operationId` | `operation_Id` de Application Insights (W3C TraceId). Usar en KQL: `union traces,requests,exceptions \| where operation_Id == "<operationId>"` para obtener la traza completa. |
| `detalleEjecucion.classificationOnly` | Refleja el valor de `instrucciones.classificationOnly` de la petición original. |
| `detalleEjecucion.nivelClasificacion` | Nivel de clasificación usado en la petición (`"TDN1"`, `"TDN1/TDN2"` o `null`). Forma parte de la clave de deduplicación cuando se informa. |
| `detalleEjecucion.runTipologia` | Clave de tipología usada en la ejecución. |
| `detalleEjecucion.clasificacion.modelo` | Modelo de clasificación usado. |
| `detalleEjecucion.clasificacion.confianza` | Confianza final (DI o GPT si hubo fallback). |
| `detalleEjecucion.clasificacion.confianzaDI` | Confianza bruta de DI. |
| `detalleEjecucion.clasificacion.confianzaGPT` | Confianza de GPT fallback (0 si no se activó). |
| `detalleEjecucion.clasificacion.proveedorClasif` | `"DocumentIntelligence"` \| `"GPT4oMini"` |
| `detalleEjecucion.clasificacion.fallbackLLM` | `true` si se usó GPT fallback. |
| `detalleEjecucion.clasificacion.fallbackRazon` | Motivo del fallback. |
| `detalleEjecucion.extraccion.proveedorExtrac` | `"AzureContentUnderstanding"` \| `"DICustom"` \| `"GPT4oMini"` |
| `detalleEjecucion.extraccion.confianzaExtraccion` | Confianza de extracción. |
| `detalleEjecucion.extraccion.fallbackUsado` | `true` si se usó GPT fallback en extracción. |
| `detalleEjecucion.extraccion.modelKeyEfectivo` | Model key realmente utilizado en ejecución (incluye selección round-robin/failover si aplica). |
| `detalleEjecucion.extraccion.endpointEfectivo` | Endpoint efectivo del modelo usado en extracción. |
| `detalleEjecucion.extraccion.processingLocationEfectiva` | Processing location efectiva del modelo de extracción. |
| `detalleEjecucion.postproceso.normalizaciones` | Lista de transformaciones aplicadas. |
| `detalleEjecucion.postproceso.validaciones` | Lista de reglas de validación ejecutadas. |
| `detalleEjecucion.postproceso.inconsistencias` | Lista de inconsistencias detectadas. |
| `detalleEjecucion.integracion.plugins` | Lista de plugins ejecutados con su resultado individual. |
| `detalleEjecucion.assetResolver` | Resultado de resolucion de activo. `null` si AssetResolver deshabilitado. |
| `detalleEjecucion.assetResolver.ejecutado` | `true` si el paso se ejecuto. |
| `detalleEjecucion.assetResolver.exitoso` | `true` si se encontro al menos un activo. |
| `detalleEjecucion.assetResolver.activosEncontrados` | Numero de activos encontrados. |
| `detalleEjecucion.assetResolver.criteriosUsados` | `{ idufir, referenciaCatastral }` — valores usados para la busqueda. |
| `detalleEjecucion.assetResolver.activos` | Array de `{ idActivo, fchCierre, camposSolicitados }`. |
| `detalleEjecucion.assetResolver.duracionMs` | Milisegundos de ejecucion. |
| `detalleEjecucion.assetResolver.error` | Detalle de error si fallo la llamada. |
| `detalleEjecucion.gdc.exitoso` | `true` si la operación GDC fue satisfactoria (subida OK/AlreadyExists o subida omitida intencionalmente con `Skipped`). |
| `detalleEjecucion.gdc.objectId` | ID del objeto creado en el GDC. |
| `detalleEjecucion.gdc.yaExistia` | `true` si el documento ya existía en GDC. |
| `detalleEjecucion.gdc.mensaje` | Estado textual del paso GDC: `OK`, `AlreadyExists`, `Skipped`, `Timeout`, etc. |
| `detalleEjecucion.seguimiento` | Timeline de actividades con tiempos individuales. |

---

## 7. Ejemplo completo de invocación (PowerShell)

```powershell
$body = @{
    instrucciones = @{
        expectedType      = ""
        skipDuplicateCheck = $false
        forceReprocess    = $false
      classificationOnly = $false
      executeIntegrarWhenClassificationOnly = $null
        skipGDCUpload     = $null
        classification    = @{ provider = "auto"; model = "auto" }
        extraction        = @{ provider = "auto"; model = "auto" }
    }
    documento = @{
        name    = "nota_simple.pdf"
        content = @{ base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\docs\nota_simple.pdf")) }
    }
    trazabilidad = @{
        correlationId = [guid]::NewGuid().ToString()
        submittedBy   = "test-script"
        idActivo      = "ACTIVO-001"
    }
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod `
    -Method POST `
    -Uri "http://localhost:7071/api/IngestDocument" `
    -Headers @{ "Content-Type" = "application/json" } `
    -Body $body

$instanceId = $response.instanceId
Write-Host "Instancia iniciada: $instanceId"

# Polling
do {
    Start-Sleep -Seconds 3
    $status = Invoke-RestMethod -Uri $response.statusQueryUri
    Write-Host "Estado: $($status.runtimeStatus)"
} while ($status.runtimeStatus -in @("Pending", "Running"))

$status.output.resultado
```

---

## 8. Notas de integración

- El documento puede enviarse en Base64 (RFC 4648, sin saltos de línea) o por referencia `objectIdGDC`.
- El `correlationId` debe ser único por petición para facilitar trazabilidad en logs.
- Si `expectedType` está presente, el sistema omite la clasificación IA y usa el valor proporcionado con confianza 1.0.
- Las peticiones con `skipDuplicateCheck = false` (default) comparan el SHA256 del documento contra la base de datos interna. La reutilización exige compatibilidad por `hash + classificationOnly`.
- Si `skipGDCUpload=true` (o se fuerza automáticamente por entrada `objectIdGDC`), el paso GDC finaliza como `Skipped` y se reporta como exitoso (`detalleEjecucion.gdc.exitoso=true`).
- Si se envía `instrucciones.prompt`, la validación ocurre en el trigger HTTP (respuesta `400` inmediata si inválido). No es necesario esperar al resultado de la orquestación para detectar errores de prompt.
- En `classificationOnly=true`, `Integrar` solo se ejecuta con `executeIntegrarWhenClassificationOnly=true` y `trazabilidad.idActivo` informado.

### Endpoints externos GDC consumidos

El backend consume un único endpoint configurable `GDC:Endpoint` de SINTWS `IDocService` y ejecuta sobre él las operaciones SOAP:

- `searchEntities` (deduplicación previa a subida)
- `create` (alta documental)
- `get` (metadatos/descarga por `objectIdGDC`)

---

## 9. Endpoints COMPLETAR_GDC_HTTP_BASIC_USERNAME de configuración dinámica

Estos endpoints gestionan configuración de tipologías, modelos y plugins en base de datos. Todos son `AuthorizationLevel.Function`.

### 9.1 Tipologías

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/management/tipologias` | Lista tipologías/versiones con estado. |
| `GET` | `/api/management/tipologias/{codigo}` | Obtiene una tipología por código. |
| `PUT` | `/api/management/tipologias/{codigo}` | Crea/actualiza borrador (`Draft`). |
| `POST` | `/api/management/tipologias/{codigo}/publicar` | Publica (`Published`) la tipología. |
| `POST` | `/api/management/tipologias/{codigo}/retirar` | Retira (`Retired`) la tipología. |

### 9.2 Modelos

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/management/modelos` | Lista modelos por tipo/estado. |
| `GET` | `/api/management/modelos/{key}` | Obtiene un modelo por clave. |
| `PUT` | `/api/management/modelos/{key}` | Crea/actualiza borrador (`Draft`). |
| `POST` | `/api/management/modelos/{key}/publicar` | Publica (`Published`) el modelo. |
| `POST` | `/api/management/modelos/{key}/retirar` | Retira (`Retired`) el modelo. |

### 9.3 Plugins por tipología

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/management/plugins-tipologias` | Lista configuraciones de plugins por tipología. |
| `GET` | `/api/management/plugins-tipologias/{tipologiaCodigo}` | Obtiene configuración de una tipología. |
| `PUT` | `/api/management/plugins-tipologias/{tipologiaCodigo}` | Crea/actualiza borrador (`Draft`) de plugins. |
| `POST` | `/api/management/plugins-tipologias/{tipologiaCodigo}/publicar` | Publica (`Published`) la configuración de plugins. |
| `POST` | `/api/management/plugins-tipologias/{tipologiaCodigo}/retirar` | Retira (`Retired`) la configuración de plugins. |

### 9.4 Validaciones relevantes

- `PUT` de tipologías y plugins valida que el JSON sea deserializable.
- `POST publicar` vuelve a validar antes de promover a `Published`.
- Si un recurso no existe, la API devuelve `404 Not Found`.
- Si el JSON es inválido, la API devuelve `400 Bad Request` con detalle en `error`.
