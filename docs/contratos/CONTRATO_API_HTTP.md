# Contrato de la API HTTP — DocumentIA

## 1. Descripción

El sistema expone una única Function HTTP de entrada: `IngestDocument`. El procesamiento es **asíncrono** (Durable Functions): la petición `POST` devuelve inmediatamente un `202 Accepted` con un `instanceId`, y el cliente hace polling sobre la URL de estado para conocer el resultado final.

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
| `Content-Type` | Sí | `application/json` |
| `x-functions-key` | Sí (producción) | Function Key |

#### Body — `ContratoEntrada`

```json
{
  "instrucciones": {
    "expectedType": "",
    "skipDuplicateCheck": false,
    "forceReprocess": false,
    "skipGDCUpload": null,
    "classification": {
      "provider": "auto",
      "model": "auto"
    },
    "extraction": {
      "provider": "auto",
      "model": "auto"
    }
  },
  "documento": {
    "name": "nota_simple_finca_123.pdf",
    "content": {
      "base64": "<contenido-en-base64>"
    }
  },
  "trazabilidad": {
    "correlationId": "a1b2c3d4-0000-0000-0000-000000000000",
    "submittedBy": "sistema-origen",
    "idGDC": null,
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
| `skipGDCUpload` | bool? | `null` = respetar la configuración de la tipología. `true` = no subir al GDC. `false` = forzar subida. |
| `classification.provider` | string | `auto` \| `azure-document-intelligence` \| `mock` |
| `classification.model` | string | Reservado. Usar `"auto"`. El model key se resuelve desde la configuración de la tipología. |
| `classification.umbral` | double? | _(Opcional)_ Umbral de confianza para activar fallback GPT y para el check `BAJA_CONFIANZA_CLASIFICACION`. `[0.0–1.0]`. Si se omite (`null`), se aplica la jerarquía: tipología → configuración servidor. |
| `extraction.provider` | string | `auto` \| `azure-content-understanding` \| `azure-cu` \| `azure-document-intelligence` \| `azure-di` \| `mock`. Si se especifica un valor distinto de `"auto"`, sobreescribe el proveedor configurado en la tipología para esta petición. |
| `extraction.model` | string | Model key del registro de modelos de extracción. Si se especifica un valor distinto de `"auto"`, sobreescribe el `modelKey` configurado en la tipología para esta petición. Debe coincidir con una clave del registro de modelos (`extraction-models.json`). |
| `extraction.umbral` | double? | _(Opcional)_ Ratio mínimo de campos para considerar la extracción CU suficiente. `[0.0–1.0]`. Si se omite (`null`), se aplica la jerarquía: tipología → configuración servidor (`MinFieldsRatio`). |

**`documento`**

| Campo | Tipo | Descripción |
|---|---|---|
| `name` | string | Nombre del fichero (con extensión). Ej: `"nota_simple.pdf"`. |
| `content.base64` | string | Contenido del documento codificado en Base64. |

**`trazabilidad`**

| Campo | Tipo | Descripción |
|---|---|---|
| `correlationId` | string | ID de correlación para trazabilidad. Si no se envía, el sistema genera un UUID. |
| `submittedBy` | string | Identificador del sistema o usuario que envía el documento. |
| `idGDC` | string? | ID del documento en el GDC (si ya existe previamente). Opcional. |
| `idActivo` | string? | ID del activo al que pertenece el documento. Opcional; puede ser resuelto por plugins. |

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
| `400 Bad Request` | Body inválido o `ContratoEntrada` no deserializable. |
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
      "paginas": 5
    },
    "integridad": {
      "crc32": "a1b2c3d4",
      "sha256": "abcdef...",
      "md5": "fedcba...",
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
  }
}
```

---

## 5. Estados posibles en `resultado.estado`

| Estado | Descripción |
|---|---|
| `OK` | Procesamiento completado correctamente. |
| `VALIDACION_CON_ERRORES` | Extracción completada pero alguna regla de validación no se cumplió. Los datos se devuelven. |
| `BAJA_CONFIANZA_CLASIFICACION` | La confianza de clasificación está por debajo del umbral. Se devuelven datos con advertencia. |
| `DUPLICADO` | El documento ya existe en la base de datos (mismo SHA256). Se devuelve la ejecución anterior reutilizada. Ver `reutilizadaPorDuplicado = true`. |
| `ERROR` | Error irrecuperable durante el procesamiento (clasificación fallida, excepción no controlada). Consultar `mensajeError`. |

---

## 6. `detalleEjecucion` — estructura completa

| Campo | Descripción |
|---|---|
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
| `detalleEjecucion.postproceso.normalizaciones` | Lista de transformaciones aplicadas. |
| `detalleEjecucion.postproceso.validaciones` | Lista de reglas de validación ejecutadas. |
| `detalleEjecucion.postproceso.inconsistencias` | Lista de inconsistencias detectadas. |
| `detalleEjecucion.integracion.plugins` | Lista de plugins ejecutados con su resultado individual. |
| `detalleEjecucion.gdc.exitoso` | `true` si la subida al GDC fue exitosa. |
| `detalleEjecucion.gdc.objectId` | ID del objeto creado en el GDC. |
| `detalleEjecucion.gdc.yaExistia` | `true` si el documento ya existía en GDC. |
| `detalleEjecucion.seguimiento` | Timeline de actividades con tiempos individuales. |

---

## 7. Ejemplo completo de invocación (PowerShell)

```powershell
$body = @{
    instrucciones = @{
        expectedType      = ""
        skipDuplicateCheck = $false
        forceReprocess    = $false
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
        idGDC         = $null
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

- El contenido del documento debe enviarse en Base64 **sin** saltos de línea (Base64 estándar RFC 4648).
- El `correlationId` debe ser único por petición para facilitar trazabilidad en logs.
- Si `expectedType` está presente, el sistema omite la clasificación IA y usa el valor proporcionado con confianza 1.0.
- Las peticiones con `skipDuplicateCheck = false` (default) comparan el SHA256 del documento contra la base de datos interna. Si coincide, se devuelve la ejecución previa sin reprocesar (ver [MANUAL_DEDUPLICACION.md](MANUAL_DEDUPLICACION.md)).

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
