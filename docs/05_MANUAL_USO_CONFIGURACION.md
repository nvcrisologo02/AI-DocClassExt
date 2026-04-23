# 5. Manual de Uso y Configuracion — DocumentIA MVP

> Ultima actualizacion: 2026-03-31  
> Proyecto: AI DocClassExt — SAREB

---

## 5.1 Guia de la API REST

### 5.1.1 Informacion General

| Atributo | Valor |
|----------|-------|
| **Base URL (local)** | `http://localhost:7071` |
| **Base URL (Azure)** | `https://srbappprodocai.azurewebsites.net` |
| **Autenticacion** | Function Key via header `x-functions-key` o query `?code=<key>` |
| **Content-Type** | `application/json` |
| **Flujo** | Asincrono: POST devuelve 202 + statusQueryUri. Polling hasta Completed. |

### 5.1.2 Procesamiento de Documento

#### Request

```powershell
# PowerShell — envio basico
$pdfBytes = [IO.File]::ReadAllBytes("C:\docs\nota_simple.pdf")
$base64 = [Convert]::ToBase64String($pdfBytes)

$body = @{
    documento = @{
        name = "nota_simple.pdf"
        content = @{ base64 = $base64 }
    }
    trazabilidad = @{
        correlationId = [guid]::NewGuid().ToString()
        submittedBy = "mi-sistema"
    }
} | ConvertTo-Json -Depth 5

$response = Invoke-RestMethod -Method POST `
    -Uri "http://localhost:7071/api/IngestDocument" `
    -ContentType "application/json" `
    -Body $body
```

```bash
# curl — envio basico
curl -X POST http://localhost:7071/api/IngestDocument \
  -H "Content-Type: application/json" \
  -d '{
    "documento": {
      "name": "nota_simple.pdf",
      "content": { "base64": "<BASE64_PDF>" }
    },
    "trazabilidad": {
      "correlationId": "a1b2c3d4-...",
      "submittedBy": "mi-sistema"
    }
  }'
```

#### Response 202

```json
{
  "instanceId": "abc123def456ghi789",
  "statusQueryUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/abc123def456ghi789",
  "correlationId": "a1b2c3d4-..."
}
```

#### Polling

```powershell
# Polling hasta completado
do {
    Start-Sleep -Seconds 2
    $status = Invoke-RestMethod $response.statusQueryUri
    $actividad = $status.customStatus.actividadActual
    $completadas = ($status.customStatus.actividadesCompletadas -join ", ")
    Write-Host "[$($status.runtimeStatus)] Actividad: $actividad | Completadas: $completadas"
} while ($status.runtimeStatus -in @("Running", "Pending"))

$status.output | ConvertTo-Json -Depth 10
```

### 5.1.3 Envio con Opciones Avanzadas

```powershell
# Saltar clasificacion (tipologia conocida)
$body = @{
    instrucciones = @{
        expectedType = "nota-simple"
        skipDuplicateCheck = $false
        forceReprocess = $false
    }
    documento = @{ name = "nota.pdf"; content = @{ base64 = $base64 } }
    trazabilidad = @{ submittedBy = "batch" }
} | ConvertTo-Json -Depth 5

# Forzar reproceso de documento duplicado
$body = @{
    instrucciones = @{ forceReprocess = $true }
    documento = @{ name = "nota.pdf"; content = @{ base64 = $base64 } }
} | ConvertTo-Json -Depth 5

# Omitir subida a GDC
$body = @{
    instrucciones = @{ skipGDCUpload = $true }
    documento = @{ name = "nota.pdf"; content = @{ base64 = $base64 } }
} | ConvertTo-Json -Depth 5

# Proveedor y umbral custom
$body = @{
    instrucciones = @{
        extraction = @{
            provider = "azure-content-understanding"
            umbralCompletitud = 0.8
        }
        classification = @{
            umbral = 0.7
        }
    }
    documento = @{ name = "nota.pdf"; content = @{ base64 = $base64 } }
} | ConvertTo-Json -Depth 5

# Activar resolucion de activo con IDUFIR override
$body = @{
    instrucciones = @{
        assetResolver = @{
            enabled = $true
            camposBusqueda = @{ idufir = "28077000012345" }
            camposSolicitados = @("ID_ACTIVO_SAREB", "ID_IDUFIR", "ID_REF_CATAST", "NOM_ACTIVO_GL")
        }
    }
    documento = @{ name = "nota.pdf"; content = @{ base64 = $base64 } }
} | ConvertTo-Json -Depth 5
```

### 5.1.4 Consultar Tipologias Publicadas

```powershell
# Sin autenticacion (Anonymous)
Invoke-RestMethod http://localhost:7071/api/tipologias | ConvertTo-Json -Depth 5
```

---

## 5.2 Contrato de Entrada Detallado

### ContratoEntrada

| Campo | Tipo | Obligatorio | Descripcion |
|-------|------|-------------|------------|
| `instrucciones` | object | No | Opciones de procesamiento. Si no se envia, se usan defaults. |
| `instrucciones.expectedType` | string | No | Tipologia conocida (omite clasificacion). Ej: `"nota-simple"`, `"nota-simple@1.4"`. Vacio = clasificacion automatica. |
| `instrucciones.skipDuplicateCheck` | bool | No | `true` = no verificar duplicados. Default: `false`. |
| `instrucciones.forceReprocess` | bool | No | `true` = reprocesar aunque sea duplicado. Default: `false`. |
| `instrucciones.skipGDCUpload` | bool? | No | `null` = respetar config tipologia. `true` = no subir GDC. `false` = forzar subida. |
| `instrucciones.classification` | object | No | Config clasificacion para esta peticion. |
| `instrucciones.classification.provider` | string | No | `"auto"` / `"azure-document-intelligence"` / `"mock"`. Default: `"auto"`. |
| `instrucciones.classification.model` | string | No | Reservado. Usar `"auto"`. |
| `instrucciones.classification.umbral` | double? | No | Umbral confianza clasificacion (0.0-1.0). `null` = usar config tipologia/servidor. |
| `instrucciones.extraction` | object | No | Config extraccion para esta peticion. |
| `instrucciones.extraction.provider` | string | No | `"auto"` / `"azure-content-understanding"` / `"azure-cu"` / `"azure-document-intelligence"` / `"azure-di"` / `"azure-openai"` / `"gpt"` / `"mock"`. Con `"azure-openai"` se activa el modo GPT directo (sin CU). |
| `instrucciones.extraction.model` | string | No | Model key del registro. `"auto"` = usar config tipologia. |
| `instrucciones.extraction.umbral` | double? | No | Umbral legacy (aplica si no se informan los especificos). |
| `instrucciones.extraction.umbralCompletitud` | double? | No | Ratio minimo campos para NO activar fallback GPT (0.0-1.0). |
| `instrucciones.extraction.umbralConfianza` | double? | No | Confianza global minima de extraccion CU (0.0-1.0). |
| `instrucciones.assetResolver` | object | No | Configuracion de resolucion de activo para esta peticion. |
| `instrucciones.assetResolver.enabled` | bool? | No | `true` = forzar resolucion de activo. `false` = desactivar. `null` = respetar config tipologia. |
| `instrucciones.assetResolver.camposBusqueda` | object | No | Valores override de busqueda. |
| `instrucciones.assetResolver.camposBusqueda.idufir` | string? | No | IDUFIR a buscar (sobreescribe el extraido del documento). |
| `instrucciones.assetResolver.camposBusqueda.referenciaCatastral` | string? | No | Referencia Catastral a buscar (sobreescribe la extraida). |
| `instrucciones.assetResolver.camposSolicitados` | string[]? | No | Columnas de `DM_POSICION_AAII_TB` a retornar. Si `null`, usa config tipologia o default (`ID_ACTIVO_SAREB`). |
| `documento` | object | **Si** | Documento a procesar. |
| `documento.name` | string | **Si** | Nombre del archivo con extension. Ej: `"nota_simple.pdf"`. |
| `documento.content.base64` | string | **Si** | Contenido PDF codificado en Base64 (RFC 4648, sin saltos de linea). |
| `trazabilidad` | object | No | Informacion de trazabilidad. |
| `trazabilidad.correlationId` | string | No | UUID de correlacion. Auto-generado si no se informa. |
| `trazabilidad.submittedBy` | string | No | Identificador del sistema/usuario que envia. |
| `trazabilidad.idGDC` | string? | No | ID existente en GDC (si el documento ya esta archivado). |
| `trazabilidad.idActivo` | string? | No | ID del activo inmobiliario. Puede ser resuelto por plugins. |

### Jerarquia de Umbrales

```
Extracción (completitud):
  instrucciones.extraction.umbralCompletitud
    ?? instrucciones.extraction.umbral
    ?? tipologia.confidenceConfig.extracUmbralFallbackCompletitud
    ?? tipologia.confidenceConfig.extracUmbralFallback
    ?? modelo.minFieldsRatio

Extracción (confianza):
  instrucciones.extraction.umbralConfianza
    ?? instrucciones.extraction.umbral
    ?? tipologia.confidenceConfig.extracUmbralFallbackConfianza
    ?? tipologia.confidenceConfig.extracUmbralFallback
    ?? modelo.minFieldsRatio

Clasificación:
  instrucciones.classification.umbral
    ?? tipologia.confidenceConfig.clasifUmbralFallback
    ?? modelo.fallbackThreshold
```

En cada capa (instrucciones/tipología), el umbral legado se usa solo cuando el específico de ese criterio es `null`.

---

## 5.3 Contrato de Salida Detallado

### ContratoSalida (output en statusQueryUri cuando runtimeStatus = Completed)

| Campo | Tipo | Descripcion |
|-------|------|------------|
| **identificacion** | | |
| `.documento` | string | Nombre del archivo procesado |
| `.guid` | string | UUID unico de esta ejecucion |
| `.tipologia` | string | Tipologia completa detectada (ej: `nota.simple.1_4`) |
| `.tipologiaFamilia` | string | Familia (ej: `nota-simple`) |
| `.tipologiaVersion` | string | Version (ej: `1.4`) |
| `.fechaProceso` | datetime | Timestamp UTC del procesamiento |
| `.paginas` | int | Numero de paginas del PDF |
| **integridad** | | |
| `.crc32` | string | Hash CRC32 del documento |
| `.sha256` | string | Hash SHA256 (usado para deduplicacion) |
| `.md5` | string | Hash MD5 (usado para GDC) |
| `.rutaBlobStorage` | string | Ruta del blob en Azure Storage |
| `.gestorDocumental` | string? | ObjectId en GDC (si se subio) |
| `.idActivo` | string? | IdActivo final (puede ser resuelto por plugin) |
| `.idActivoEntrada` | string? | IdActivo original de la peticion |
| `.idActivoCambiado` | bool | `true` si plugin cambio el IdActivo |
| **datosExtraidos** | Dictionary&lt;string, object&gt; | Campos extraidos del documento |
| **resultado** | | |
| `.estado` | string | Estado final: `OK`, `VALIDACION_CON_ERRORES`, `BAJA_CONFIANZA_CLASIFICACION`, `DUPLICADO`, `ERROR` |
| `.mensajeError` | string? | Detalle cuando estado = ERROR |
| `.confianzaGlobal` | double | MIN(clasif, extrac, validacion). Rango [0.0-1.0] |
| `.estadoCalidad` | string | `OK` (>=0.85), `REVISION` (>=0.70), `ERROR` (<0.70) |
| `.confianzaClasificacion` | double | Confianza de clasificacion |
| `.confianzaExtraccion` | double | Confianza de extraccion |
| `.confianzaValidacion` | double | Confianza de validacion |
| `.reutilizadaPorDuplicado` | bool | `true` si se reutilizo resultado anterior |
| `.mensajeReutilizacion` | string? | Mensaje si fue reutilizado |
| **detalleEjecucion** | | |
| `.clasificacion` | object | Detalles de clasificacion (modelo, confianzas, fallback) |
| `.extraccion` | object | Detalles de extraccion (modelo, confianza por campo, fallback) |
| `.postproceso` | object | Normalizaciones, validaciones, inconsistencias, confianza validacion |
| `.integracion` | object | Plugins ejecutados, datos originales vs finales, IdActivo |
| `.assetResolver` | object? | Resultado de resolucion de activo: ejecutado, exitoso, activos encontrados, criterios, duracion. `null` si AssetResolver deshabilitado. |
| `.gdc` | object | Resultado subida GDC (exitoso, objectId, intentos, duracion) |
| `.seguimiento` | object | Timeline de actividades con estado y duracion por actividad |
| `.prompt` | object? | Resultado del prompt libre (si habilitado en tipologia) |

---

## 5.4 Interpretacion del Resultado

### Estados Finales

| Estado | Significado | Accion recomendada |
|--------|------------|-------------------|
| `OK` | Procesamiento completo, datos fiables | Consumir datos normalmente |
| `VALIDACION_CON_ERRORES` | Extraccion OK pero validacion detecto errores | Revisar `postproceso.inconsistencias`. Datos pueden requerir correccion manual. |
| `BAJA_CONFIANZA_CLASIFICACION` | IA no pudo clasificar con confianza suficiente | Verificar documento manualmente. Posible documento no soportado. |
| `DUPLICADO` | Documento ya procesado (SHA256 identico) | Consultar resultado anterior. Usar `forceReprocess=true` si se desea reprocesar. |
| `ERROR` | Error en el procesamiento | Consultar `resultado.mensajeError` y `detalleEjecucion.seguimiento`. |

### EstadoCalidad

| EstadoCalidad | Rango ConfianzaGlobal | Significado |
|--------------|----------------------|------------|
| `OK` | >= 0.85 | Datos de alta confianza |
| `REVISION` | >= 0.70 y < 0.85 | Datos requieren revision humana |
| `ERROR` | < 0.70 | Datos no fiables |

### Indicadores de Fallback

| Campo | Valor | Significado |
|-------|-------|------------|
| `clasificacion.fallbackLLM` | `true` | Clasificacion fue hecha por GPT (DI tuvo baja confianza) |
| `extraccion.fallbackUsado` | `true` | Extracción complementada por GPT (CU tuvo baja completitud/confianza) |
| `extraccion.fallbackUsado` | `false` | Extracción directa GPT (`azure-openai`) o CU suficiente sin activar fallback |
| `clasificacion.fallbackRazon` | string | Motivo del fallback |
| `seguimiento.actividades[].fallbackActivado` | `true` | Actividad especifica uso fallback |

---

## 5.5 Configuracion de Validacion

> Los archivos `.validation.json` descritos aquí son la **estructura de referencia y de seed**. En runtime, la configuración se lee desde la columna `ConfiguracionJson` de la tabla `TipologiasConfig` en BD.

### 5.5.1 Estructura del Archivo de Validacion (referencia / seed)

Archivo: `config/tipologias/{tipologia-codigo}.validation.json`

```json
{
  "tipologiaId": "nota.simple.1_4",
  "version": "1.4",
  "extractionConfig": {
    "expectedFields": [
      "FincaRegistral",
      "IDUFIR_CRU",
      "Direccion",
      "ReferenciaCatastral",
      "FechaDocumento",
      "Titulares",
      "Cargas"
    ]
  },
  "promptConfig": {
    "enabled": false,
    "systemPrompt": "",
    "userPromptTemplate": ""
  },
  "confidenceConfig": {
    "umbralOK": 0.85,
    "umbralRevision": 0.70
  },
  "fields": {
    "FincaRegistral": {
      "rules": [
        {
          "type": "required",
          "severity": "Error",
          "message": "Finca registral es obligatoria"
        },
        {
          "type": "regex",
          "severity": "Warning",
          "params": { "pattern": "^\\d+$" },
          "message": "Se espera formato numerico"
        }
      ]
    },
    "IDUFIR_CRU": {
      "rules": [
        { "type": "required", "severity": "Warning" },
        { "type": "regex", "severity": "Error", "params": { "pattern": "^\\d{14}$" }, "message": "IDUFIR debe tener 14 digitos" }
      ]
    },
    "Titulares": {
      "rules": [
        { "type": "required", "severity": "Error" },
        { "type": "array", "severity": "Error", "params": { "minItems": 1 }, "message": "Debe haber al menos un titular" }
      ]
    },
    "NIF_Titular": {
      "rules": [
        { "type": "nif", "severity": "Error", "message": "NIF/NIE/CIF invalido" }
      ]
    },
    "ReferenciaCatastral": {
      "rules": [
        { "type": "catastralReference", "severity": "Warning", "message": "Formato de referencia catastral invalido" }
      ]
    },
    "FechaDocumento": {
      "rules": [
        { "type": "required", "severity": "Warning" },
        { "type": "dateFormat", "severity": "Error", "params": { "format": "dd/MM/yyyy" } }
      ]
    }
  }
}
```

### 5.5.2 Tipos de Regla Disponibles

| Tipo | Parametros | Uso |
|------|-----------|-----|
| `required` | — | Campo obligatorio (no null, no vacio) |
| `regex` | `pattern` | Valor debe coincidir con expresion regular |
| `range` | `min`, `max` | Valor numerico en rango |
| `dateFormat` | `format` | Fecha en formato especificado (`dd/MM/yyyy`, `yyyy-MM-dd`) |
| `length` | `minLength`, `maxLength` | Longitud de cadena en rango |
| `nif` | — | Validacion algoritmica NIF/NIE/CIF espanol |
| `enum` | `values[]` | Valor debe estar en lista permitida |
| `catastralReference` | — | Formato referencia catastral espanola (20 chars) |
| `address` | — | Estructura basica de direccion |
| `boolean` | — | Valor booleano (true/false/si/no) |
| `array` | `minItems`, `maxItems` | Validacion de arrays (titulares, cargas) |

### 5.5.3 Severidades

| Severidad | Impacto en ConfianzaValidacion | Aparece en |
|-----------|-------------------------------|-----------|
| `Error` | Reduce confianza. Detiene validacion del campo. | `postproceso.inconsistencias` |
| `Warning` | Sin impacto en confianza. | `postproceso.validaciones` |
| `Info` | Sin impacto. Informativo. | `postproceso.validaciones` |

---

## 5.6 Checklist: Anadir Nueva Tipologia

> Los archivos JSON de `config/tipologias/` son **solo seed**. En runtime la Function App usa BD. El proceso recomendado crea la tipología directamente en BD via Admin API.

| # | Paso | Detalle | Verificacion |
|---|------|---------|-------------|
| 1 | Preparar JSON de validacion | Crear `config/tipologias/{codigo}.validation.json` con campos y reglas (usado como seed o referencia) | JSON valido, campos coinciden con modelo CU |
| 2 | Preparar JSON de plugins (si aplica) | Crear `config/tipologias/{codigo}.plugins.json` (seed) | JSON valido, pluginKeys correctos |
| 3 | Entrenar modelo CU (si extraccion custom) | Azure Content Understanding → crear analyzer con campos custom | Modelo publicado y accesible |
| 4 | Registrar modelo en BD | `POST /management/modelos` con `Key`, `Tipo` y `ConfiguracionJson` (ver §5.6.1 para el schema por tipo de proveedor) | 200 OK |
| 5 | Entrenar clasificador DI (si clasificacion custom) | Azure Document Intelligence → entrenar modelo custom | Modelo publicado |
| 6 | Crear tipologia via API (fuente de verdad) | `POST /management/tipologias` con codigo, nombre, version, umbrales y ConfiguracionJson | Estado = Draft |
| 7 | Publicar tipologia | `POST /management/tipologias/{id}/publicar` | Estado = Published |
| 8 | Verificar en catalogo | `GET /api/tipologias` debe incluir la nueva tipologia | Aparece en lista |
| 9 | Test de ingesta | Enviar documento de prueba con la nueva tipologia | Estado final = OK o REVISION |

---

### 5.6.1 Schema de ConfiguracionJson por Tipo de Proveedor

> Todos los parametros de conexion AI se almacenan **exclusivamente en BD** (tabla `ModeloConfigs`). No hay claves en `appsettings` para estos valores.
> La columna `ConfiguracionJson` del modelo contiene un objeto JSON cuya estructura depende del `provider`.

#### Proveedor `azure-document-intelligence` (Clasificacion)

```json
{
  "endpoint": "https://<nombre>.cognitiveservices.azure.com/",
  "apiKey": "<clave>",
  "authMode": "ApiKey",
  "apiVersion": "2024-11-30",
  "pollIntervalMs": 1000,
  "timeoutSeconds": 120,
  "isDefault": true,
  "fallbackThreshold": 0.6
}
```

| Campo | Tipo | Obligatorio | Notas |
|-------|------|-------------|-------|
| `endpoint` | string | Sí | URL base del servicio Azure DI |
| `apiKey` | string | Si `authMode=ApiKey` | API Key del servicio |
| `authMode` | string | Sí | `"ApiKey"` o `"ManagedIdentity"` |
| `apiVersion` | string | No | Default: `"2024-11-30"` |
| `pollIntervalMs` | int | No | ms entre sondeos de estado. Default: `1000` |
| `timeoutSeconds` | int | No | Timeout total. Default: `120` |
| `isDefault` | bool | No | Marca como modelo de clasificacion por defecto |
| `fallbackThreshold` | double | No | Confianza mínima DI para no activar fallback GPT |

#### Proveedor `azure-document-intelligence` (Extraccion)

```json
{
  "endpoint": "https://<nombre>.cognitiveservices.azure.com/",
  "apiKey": "<clave>",
  "authMode": "ApiKey",
  "apiVersion": "2024-11-30",
  "analyzerId": "<id-del-modelo-custom>",
  "pollIntervalMs": 1000,
  "timeoutSeconds": 120,
  "isDefault": false,
  "useAsFallback": false
}
```

#### Proveedor `azure-content-understanding` (Extraccion)

```json
{
  "endpoint": "https://<nombre>.cognitiveservices.azure.com/",
  "apiKey": "<clave>",
  "authMode": "ApiKey",
  "analyzerId": "<id-del-analyzer-CU>",
  "processingLocation": "westeurope",
  "contentType": "application/pdf",
  "timeoutSeconds": 120,
  "isDefault": true,
  "useAsFallback": false
}
```

| Campo | Tipo | Obligatorio | Notas |
|-------|------|-------------|-------|
| `analyzerId` | string | Sí | ID del analyzer publicado en Azure Content Understanding |
| `processingLocation` | string | No | Region de procesamiento. Ej: `"westeurope"` |
| `contentType` | string | No | Default: `"application/pdf"` |

#### Proveedor `azure-openai` (Extraccion / Prompt / Clasificacion)

```json
{
  "endpoint": "https://<nombre>.openai.azure.com/",
  "apiKey": "<clave>",
  "authMode": "ApiKey",
  "deploymentName": "gpt-4o-mini",
  "temperature": 0.0,
  "maxTokens": 4096,
  "timeoutSeconds": 120,
  "minFieldsRatio": 0.5,
  "isDefault": false,
  "useAsFallback": true
}
```

| Campo | Tipo | Obligatorio | Notas |
|-------|------|-------------|-------|
| `deploymentName` | string | Sí | Nombre del deployment en Azure OpenAI |
| `temperature` | double | No | Temperatura de generacion. Default: `0.0` |
| `maxTokens` | int | No | Tokens maximos de respuesta. Default: `4096` |
| `minFieldsRatio` | double | No | Completitud minima para activar este modelo como fallback |
| `useAsFallback` | bool | No | `true` = actuar como fallback de extraccion GPT |

#### Proveedor `azure-document-intelligence-layout` (Layout)

```json
{
  "endpoint": "https://<nombre>.cognitiveservices.azure.com/",
  "apiKey": "<clave>",
  "authMode": "ApiKey",
  "apiVersion": "2024-11-30",
  "pollIntervalMs": 1000,
  "timeoutSeconds": 120,
  "isDefault": true
}
```

> **Nota sobre `authMode`:** Con `"ManagedIdentity"` el campo `apiKey` se ignora y la autenticacion se realiza via Managed Identity de la Function App (sin credenciales en BD).

---

## 5.7 Configuracion de Plugins

> Los archivos `.plugins.json` son **solo seed**. En runtime, los plugins se resuelven desde la tabla `PluginTipologiaConfigs` en BD. Para modificar plugins en produccion, usar `PUT /management/tipologias/{id}/plugins`.

### 5.7.1 Estructura del Archivo de Plugins (referencia / seed)

Archivo: `config/tipologias/{tipologia-codigo}.plugins.json`

```json
{
  "tipologiaCodigo": "nota.simple.1_4",
  "plugins": [
    {
      "pluginKey": "refCatExcel",
      "pluginType": "REST",
      "enabled": true,
      "priority": 1,
      "configuration": {
        "baseUrl": "https://api.ejemplo.com/catastro",
        "method": "POST",
        "headers": {
          "Authorization": "Bearer <TOKEN>"
        },
        "bodyTemplate": "{ \"referencia\": \"{ReferenciaCatastral}\" }",
        "responseMapping": {
          "idActivo": "$.result.idActivo",
          "direccionEnriquecida": "$.result.direccion"
        },
        "returnsIdActivo": true
      },
      "retryPolicy": {
        "maxRetries": 3,
        "initialDelayMs": 500
      }
    }
  ]
}
```

### 5.7.2 Tipos de Plugin

| Tipo | `pluginType` | Configuracion especifica |
|------|-------------|------------------------|
| **REST** | `"REST"` | `baseUrl`, `method`, `headers`, `bodyTemplate`, `responseMapping` |
| **SOAP** | `"SOAP"` | `endpoint`, `soapAction`, `requestTemplate`, `responseXPath` |
| **Custom** | `"Custom"` | `assemblyPath` (ruta DLL .NET), `className`, `methodName` |

### 5.7.3 Prioridades

- **Priority 1** (critico): Si falla, detiene toda la cadena de plugins.
- **Priority 2+** (no critico): Si falla, se registra warning y continua con el siguiente.
- Ejecucion en orden ascendente de prioridad.

### 5.7.4 Retry Policy

```json
"retryPolicy": {
  "maxRetries": 3,
  "initialDelayMs": 500
}
```

Backoff exponencial: `delay = initialDelayMs * 2^(attempt - 1)`
| Intento | Delay |
|---------|-------|
| 1 | 500ms |
| 2 | 1000ms |
| 3 | 2000ms |

---

## 5.7b Configuracion de AssetResolver (Resolucion de Activo)

### 5.7b.1 Descripcion

La actividad `ObtenerActivo` permite resolver automaticamente el `IdActivo` de un documento consultando la tabla `DM_POSICION_AAII_TB` del plugin AssetResolver. Se ejecuta **entre Validar e Integrar** y es **opcional** (deshabilitada por defecto).

El AssetResolver soporta **cuatro criterios de busqueda** configurables:
- **IDUFIR**: busqueda exacta por codigo IDUFIR (columna `ID_IDUFIR`).
- **Referencia Catastral**: busqueda exacta por referencia (columna `ID_REF_CATAST`).
- **Direccion fuzzy**: busqueda fuzzy por direccion con scoring (columnas `DES_NOMBRE_VIA`, `NUM_VIA`, `DES_MUNICP`, `NUM_COD_POSTAL`).
- **Direccion tipificada**: busqueda exacta/parcial por campos de direccion estructurados (filtros AND independientes en BD).

Ver contrato funcional de precedencia en [ESPECIFICACION_PLUGIN_ASSETRESOLVER.md](ESPECIFICACION_PLUGIN_ASSETRESOLVER.md#13-contrato-funcional-de-precedencia-de-criterios-y-propagacion-de-idactivo).

Cada criterio se puede habilitar/deshabilitar independientemente, y los resultados se combinan segun un **modo de combinacion configurable** (AND u OR).

### 5.7b.2 Habilitacion por Precedencia

```
instrucciones.assetResolver.enabled        (peticion HTTP)
  ?? tipologia.assetResolver.enabled       (config tipologia en BD)
    ?? false                                (default: deshabilitado)
```

### 5.7b.3 Configuracion en Tipologia

En la tabla `Tipologias`, el campo `ConfiguracionJson` puede incluir una seccion `assetResolver`:

```json
{
  "assetResolver": {
    "enabled": true,
    "modoCombinacionCriterios": "OR",
    "busquedaIdufirHabilitada": true,
    "busquedaReferenciaCatastralHabilitada": true,
    "busquedaDireccionHabilitada": false,
    "camposSolicitados": ["#ALL#"],
    "mapeoIdufir": ["IDUFIR", "IDUFIR_CRU", "CRU"],
    "mapeoReferenciaCatastral": ["ReferenciaCatastral", "RefCatastral"],
    "mapeoDireccionCompleta": ["Localizacion", "Direccion"],
    "mapeoDireccionNombreVia": ["NombreVia", "Calle"],
    "mapeoDireccionNumero": ["NumeroVia", "Numero"],
    "mapeoDireccionMunicipio": ["Municipio", "Localidad"],
    "mapeoDireccionCodigoPostal": ["CodigoPostal", "CP"],
    "umbralScoreDireccion": 0.75
  }
}
```

| Campo | Tipo | Default | Descripcion |
|-------|------|---------|------------|
| `enabled` | bool | `false` | Activa/desactiva la resolucion de activo para esta tipologia. |
| `modoCombinacionCriterios` | string | `"OR"` | Como combinar resultados de multiples criterios: `AND` (interseccion) u `OR` (union). |
| `busquedaIdufirHabilitada` | bool | `true` | Si `true`, incluye IDUFIR como criterio de busqueda. |
| `busquedaReferenciaCatastralHabilitada` | bool | `true` | Si `true`, incluye Referencia Catastral como criterio. |
| `busquedaDireccionHabilitada` | bool | `false` | Si `true`, habilita busqueda fuzzy por direccion. |
| `busquedaDireccionTipificadaHabilitada` | bool | `false` | Si `true`, habilita busqueda por campos tipificados (AND en BD). Requiere que el request incluya el objeto `direccionTipificada`. |
| `camposSolicitados` | string[] | `null` | Columnas de `DM_POSICION_AAII_TB` a retornar. `#ALL#` expande a todas. |
| `mapeoIdufir` | string[] | `[]` | Claves de `DatosExtraidos` donde buscar el IDUFIR. |
| `mapeoReferenciaCatastral` | string[] | `[]` | Claves de `DatosExtraidos` donde buscar la Referencia Catastral. |
| `mapeoDireccionCompleta` | string[] | `[]` | Claves para direccion completa (ej. "CALLE MAYOR 1, MADRID"). Se parsea automaticamente. |
| `mapeoDireccionNombreVia` | string[] | `[]` | Claves para nombre de via. |
| `mapeoDireccionNumero` | string[] | `[]` | Claves para numero de via. |
| `mapeoDireccionMunicipio` | string[] | `[]` | Claves para municipio. |
| `mapeoDireccionCodigoPostal` | string[] | `[]` | Claves para codigo postal. |
| `umbralScoreDireccion` | double | `0.75` | Score minimo [0.0-1.0] para aceptar un match por direccion. |

### 5.7b.4 Logica de Deteccion de Criterios

La resolucion de valores para cada criterio sigue esta logica:

1. **Si el criterio esta deshabilitado** (`busquedaXxxHabilitada = false`): no se intenta detectar ni usar.
2. **Si hay mapeo explicito en tipologia** (ej. `mapeoIdufir: ["IDUFIR_CRU"]`): solo se busca en esas claves de `DatosExtraidos`.
3. **Si no hay mapeo pero el criterio esta habilitado**: se usan los aliases globales del plugin (`FieldAliases`).
4. **Override por instrucciones**: `instrucciones.assetResolver.camposBusqueda.idufir` tiene precedencia absoluta.

### 5.7b.5 Modo de Combinacion de Criterios

| Modo | Comportamiento |
|------|---------------|
| `OR` (default) | Union de resultados. Si IDUFIR encuentra activo A y Direccion encuentra activo B, ambos se incluyen. |
| `AND` | Interseccion de resultados. Solo devuelve activos que coincidan en **todos** los criterios resueltos. Si algun criterio devuelve 0 resultados, el resultado final es vacio. |

**Ejemplo AND**: Si `busquedaIdufirHabilitada = true` y `busquedaDireccionHabilitada = true`, y el IDUFIR resuelve activo 5 pero la direccion no coincide con activo 5, el resultado sera `Found = false`.

### 5.7b.6 Busqueda por Direccion

Cuando `busquedaDireccionHabilitada = true`, el servicio:

1. **Resuelve la direccion** desde `DatosExtraidos` usando los mapeos configurados.
2. Si se usa `mapeoDireccionCompleta`, **parsea automaticamente** la cadena:
   - Extrae codigo postal con regex `\b\d{5}\b`.
   - Extrae numero de via con regex.
   - Separa municipio y nombre de via por coma.
3. **Calcula score fuzzy** contra cada fila de `DM_POSICION_AAII_TB` usando pesos:
   - CodigoPostal: 30%
   - Municipio: 30%  
   - NombreVia: 30%
   - Numero: 10%
4. Solo incluye activos cuyo score >= `umbralScoreDireccion`.

**Respuesta enriquecida** en `criteriosUsados.direccion`:
```json
{
  "direccionCompleta": "CALLE MAYOR 1, 28013 MADRID",
  "nombreVia": "CALLE MAYOR",
  "numero": "1",
  "municipio": "MADRID",
  "codigoPostal": "28013",
  "direccionNormalizada": "calle mayor 1 madrid 28013",
  "score": 0.92,
  "candidatosEvaluados": 1523,
  "razon": "Match encontrado con score 0.92"
}
```

### 5.7b.7 Ejemplos de Configuracion por Caso de Uso

#### Solo IDUFIR (comportamiento clasico)
```json
{
  "assetResolver": {
    "enabled": true,
    "busquedaIdufirHabilitada": true,
    "busquedaReferenciaCatastralHabilitada": false,
    "busquedaDireccionHabilitada": false,
    "mapeoIdufir": ["IDUFIR_CRU", "IDUFIR"]
  }
}
```

#### Solo Direccion (sin IDUFIR ni RefCatastral)
```json
{
  "assetResolver": {
    "enabled": true,
    "busquedaIdufirHabilitada": false,
    "busquedaReferenciaCatastralHabilitada": false,
    "busquedaDireccionHabilitada": true,
    "mapeoDireccionCompleta": ["Localizacion"],
    "umbralScoreDireccion": 0.75
  }
}
```

#### Todos los criterios con AND (alta precision)
```json
{
  "assetResolver": {
    "enabled": true,
    "modoCombinacionCriterios": "AND",
    "busquedaIdufirHabilitada": true,
    "busquedaReferenciaCatastralHabilitada": true,
    "busquedaDireccionHabilitada": true,
    "mapeoIdufir": ["IDUFIR_CRU"],
    "mapeoReferenciaCatastral": ["ReferenciaCatastral"],
    "mapeoDireccionCompleta": ["Localizacion"],
    "umbralScoreDireccion": 0.8
  }
}
```

#### IDUFIR + Direccion como fallback (OR)
```json
{
  "assetResolver": {
    "enabled": true,
    "modoCombinacionCriterios": "OR",
    "busquedaIdufirHabilitada": true,
    "busquedaReferenciaCatastralHabilitada": false,
    "busquedaDireccionHabilitada": true,
    "mapeoIdufir": ["IDUFIR_CRU"],
    "mapeoDireccionCompleta": ["Localizacion"],
    "umbralScoreDireccion": 0.7
  }
}
```

### 5.7b.8 Configuracion del Plugin AssetResolver

El plugin corre como proceso independiente (puerto 5006 en local). Su conexion y autenticacion se configuran en `local.settings.json` (local) o Key Vault (produccion):

```json
{
  "AssetResolver:BaseUrl": "http://localhost:5006/",
  "AssetResolver:ApiKey": "dev-local-api-key-replace-in-prod"
}
```

El plugin lee la tabla `DM_POSICION_AAII_TB` usando la connection string `AssetResolverDb` configurada en su `appsettings.json`.

### 5.7b.9 Alias de Campos Globales (FieldAliases)

El plugin define aliases globales en `appsettings.json` que se usan cuando no hay mapeo explicito en la tipologia:

```json
{
  "FieldAliases": {
    "Idufir": ["IDUFIR", "IDUFIR_CRU", "CRU", "CodigoRegistroUnico"],
    "ReferenciaCatastral": ["ReferenciaCatastral", "RefCatastral", "Catastral"],
    "DireccionCompleta": ["Localizacion", "Direccion", "DireccionCompleta", "Domicilio"]
  }
}
```

**Precedencia de aliases**: mapeos de tipologia > aliases globales del plugin.

### 5.7b.10 Resultado en ContratoSalida

Cuando AssetResolver se ejecuta, `detalleEjecucion.assetResolver` contiene:

| Campo | Tipo | Descripcion |
|-------|------|------------|
| `ejecutado` | bool | `true` si el paso se ejecuto (no fue skipped). |
| `exitoso` | bool | `true` si se encontro al menos un activo. |
| `activosEncontrados` | int | Numero de activos encontrados (tras deduplicacion). |
| `criteriosUsados` | object | Detalle de criterios: `idufir`, `referenciaCatastral`, `modoCombinacionCriterios`, `direccion`, `direccionTipificada`. |
| `activos` | array | Lista de `{ idActivo, fchCierre, camposSolicitados: {} }`. |
| `camposConError` | string[] | Columnas solicitadas que no existen en la tabla. |
| `mensaje` | string | Mensaje descriptivo del resultado. |
| `duracionMs` | long | Milisegundos de ejecucion. |
| `error` | string? | Detalle de error si fallo la llamada HTTP. |
| `criterioUtilizado` | string | Descripcion textual del criterio usado (ej. "Idufir OR Direccion"). |

### 5.7b.11 Propagacion del IdActivo

- Si se encuentra **exactamente 1 activo**, su `ID_ACTIVO_SAREB` se asigna como `IdActivo` y se propaga a Integracion y persistencia.
- Si hay **0 o multiples activos**, el IdActivo no se modifica en este paso.
- El IdActivo final (integridad) refleja: `AssetResolver (unico) ?? Plugin con returnsIdActivo ?? trazabilidad.idActivo de entrada`.

---

## 5.8 Codigos de Error y Troubleshooting

### 5.8.1 Codigos HTTP

| Codigo | Endpoint | Causa | Accion |
|--------|----------|-------|--------|
| 202 | POST /api/IngestDocument | Exito — procesamiento iniciado | Hacer polling en statusQueryUri |
| 400 | POST /api/IngestDocument | Body invalido o JSON malformado | Verificar estructura ContratoEntrada |
| 401 | Cualquiera | Function Key ausente o invalida | Incluir `x-functions-key` header |
| 404 | GET statusQueryUri | instanceId no encontrado | Verificar instanceId correcto |
| 500 | Cualquiera | Error interno no controlado | Consultar logs Function App |

### 5.8.2 Estados de Error en Resultado

| Estado resultado | Causa probable | Accion |
|-----------------|---------------|--------|
| `ERROR` con mensaje "No se ha podido identificar la tipologia" | Documento no coincide con ninguna tipologia publicada | Verificar tipologias publicadas. Verificar que el documento es un PDF valido. |
| `BAJA_CONFIANZA_CLASIFICACION` | IA no alcanzo el umbral de confianza | Revisar documento manualmente. Considerar ajustar umbrales o entrenar modelo. |
| `VALIDACION_CON_ERRORES` | Campos extraidos no pasan reglas de validacion | Revisar `postproceso.inconsistencias`. Datos pueden requerir correccion. |
| `ERROR` con timeout GDC | GDC no respondio en 120s | Verificar conectividad de red a GDC. Comprobar que `GDC:Endpoint` es accesible. |
| `runtimeStatus: Failed` | Excepcion no capturada en una actividad | Consultar logs App Insights. Error critico (BD no disponible, config incorrecta). |

### 5.8.3 FAQ y Troubleshooting

**P: El documento se clasifica como tipologia incorrecta.**
R: Verificar que el modelo DI esta entrenado con ejemplos suficientes del tipo documental. Revisar `clasificacion.confianzaDI` — si es baja, el fallback GPT puede suponer incorrectamente. Considerar usar `expectedType` para omitir clasificacion.

**P: La extraccion devuelve campos vacios.**
R: Verificar que el modelo CU tiene los campos correctos configurados. Revisar `extractionConfig.expectedFields` en el JSON de validacion. Comprobar que el analyzer de CU esta publicado. Si la completitud es baja, verificar que `GptFallback:Enabled=true`.

**P: Error SSL al conectar con GDC.**
R: El certificado del GDC SINTWS es emitido por una CA interna de SAREB no confiada fuera de la red corporativa. En desarrollo/Azure (Linux), configurar `GDC:BypassSslValidation=true`. En produccion, instalar el certificado CA corporativo.

**P: Plugin custom (.dll) no se encuentra.**
R: Verificar que `assemblyPath` en el plugins.json apunta a la ruta correcta. En local: ruta absoluta a `plugins/SarebEnrichments.dll`. En Azure (deploy): ruta relativa `plugins/SarebEnrichments.dll` (el script `deploy-manual.ps1` ajusta automaticamente).

**P: Timeout en GDC (120s).**
R: La actividad `SubirGDCActivity` tiene un timeout hardcoded de 120s en el orchestrator. Si GDC es lento, el resultado se marca con `gdc.exitoso=false` y `gdc.mensaje="Timeout"`, pero el documento se persiste igualmente. Verificar estado de red hacia `srbwidd03.sareb.srb:8090`.

**P: "Cold start" lento en Azure.**
R: Normal en Consumption Plan (2-10s). Para reducirlo: considerar Premium Plan con warm instances, o mantener un health check periodico.
**P: AssetResolver devuelve `Found=false` con mensaje "No se encontraron criterios".**
R: Ningun criterio habilitado pudo resolver un valor desde `DatosExtraidos`. Verificar que:
- Los mapeos (`mapeoIdufir`, `mapeoDireccionCompleta`, etc.) incluyen las claves exactas que devuelve el extractor.
- O bien que `instrucciones.assetResolver.camposBusqueda.idufir` contiene el valor override.

**P: Score de direccion siempre <= 0.3.**
R: Probable discrepancia entre la cadena de entrada y los datos en `DM_POSICION_AAII_TB`. Verificar el campo `criteriosUsados.direccion.direccionNormalizada` en la respuesta. Considerar reducir `umbralScoreDireccion` a `0.5` para diagnostico o usar `mapeoDireccionNombreVia` + `mapeoDireccionMunicipio` por separado en lugar de `mapeoDireccionCompleta`.

**P: AssetResolver encuentra N>1 activos y el IdActivo no se propaga.**
R: Por diseno. La propagacion automatica solo se produce con match unico (`Count=1`). Con multiples activos, el sistema es conservador y no sobreescribe el IdActivo. Los activos encontrados estan en `detalleEjecucion.assetResolver.activos` para revision manual.

**P: La busqueda tipificada no filtra aunque se informen campos.**
R: Verificar que `busquedaDireccionTipificadaHabilitada: true` en la config de tipologia O en `instrucciones.assetResolver`. El objeto `direccionTipificada` debe llegar en el request al plugin (el orquestador lo propaga desde `instrucciones.assetResolver.camposBusqueda.direccionTipificada`).
**P: Migraciones EF no se aplican.**
R: Verificar que `RunDatabaseMigrationsOnStartup=true` en la configuracion. Si falla, aplicar manualmente:
```powershell
dotnet ef database update --project ..\DocumentIA.Data --startup-project .
```

**P: AssetResolver no encuentra el activo aunque existe en la tabla.**
R: Verificar los alias de campos. El IDUFIR se busca en `DatosExtraidos` usando las claves definidas en `mapeoIdufir` (tipologia) o `FieldAliases` (plugin). Si el campo extraido tiene un nombre diferente (ej: `CRU` en vez de `IDUFIR_CRU`), añadir ese alias en la configuracion de tipologia o en `appsettings.json` del plugin.

**P: AssetResolver devuelve multiples activos y no resuelve IdActivo.**
R: La resolucion automatica requiere match unico. Si la tabla tiene multiples registros con el mismo IDUFIR (distintas `FCH_CIERRE_DT`), se retorna el mas reciente por activo pero pueden existir multiples activos. Revisar `detalleEjecucion.assetResolver.activos` para identificar el correcto manualmente.

**P: AssetResolver devuelve `camposConError`.**
R: Los campos solicitados (`camposSolicitados`) no existen como columnas en `DM_POSICION_AAII_TB`. Verificar los nombres de columna exactos (son sensibles a mayusculas/minusculas en la configuracion). Consultar el schema de la tabla en `docs/auxiliares/DM_Posicion_AAII_TB.sql`.

---

## 5.9 Aplicacion de Administracion (Admin Blazor)

La aplicacion de COMPLETAR_GDC_HTTP_BASIC_USERNAMEistracion `DocumentIA.Admin` es una web Blazor Server accesible en produccion en la URL:

```
https://srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai.azurewebsites.net
```

En desarrollo local se levanta con:

```powershell
dotnet run --launch-profile http
# Disponible en http://localhost:5000
```

La configuracion de la URL base y la Function Key usadas por el Admin apuntan a la Function App de produccion (`appsettings.json`):

```json
"FunctionsAdminApi": {
  "BaseUrl": "https://srbappprodocai.azurewebsites.net/api/",
  "FunctionKey": "<clave de host de la Function App>"
}
```

### 5.9.1 Seccion Tipologias

Permite ver, crear, editar, publicar y archivar tipologias. Cada tipologia dispone de cuatro pestanas de edicion:

| Pestana | Contenido |
|---------|----------|
| **Info** | Nombre, familia, descripcion, version, estado (Borrador/Publicada/Archivada). |
| **Config JSON** | JSON de configuracion completo (proveedor extraccion, clasificacion, campos esperados, umbrales). |
| **Validacion JSON** | JSON de reglas de validacion (tipos de regla, severidades, campos requeridos). |
| **Prompt** | Prompt GPT usado en extraccion o clasificacion por fallback. |

### 5.9.2 Editor JSON — Modo Pantalla Completa

Todos los editores JSON del Admin (`Config JSON`, `Validacion JSON`, etc.) disponen de un boton **Maximizar / Minimizar** en la barra de herramientas del editor.

- **Maximizar**: expande el editor a pantalla completa (overlay fijo con `z-index: 2000`). El editor ocupa todo el viewport menos una franja para la barra de herramientas. Permite leer y editar JSONs extensos con comodidad.
- **Minimizar**: restaura el editor a su tamano original embebido en la pagina.

El boton se encuentra alineado a la derecha de la barra de modos (Arbol / Codigo / Vista / Texto). El editor refresca su layout al cambiar de modo para evitar artefactos visuales.

### 5.9.3 Seccion Modelos

Permite registrar y gestionar los modelos de IA disponibles (Azure Document Intelligence, Azure Content Understanding, Azure OpenAI). Cada modelo tiene un `key` unico que se referencia desde el JSON de configuracion de las tipologias.

### 5.9.4 Seccion Configuracion Consulta

Permite editar la configuracion global de consulta del sistema (umbrales, timeouts, parametros de fallback GPT, etc.) en formato JSON directamente desde el Admin.

---

## 5.10 Referencias

| Documento | Contenido |
|-----------|-----------|
| [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md) | Contratos API completos con JSON de ejemplo |
| [04_MANUAL_EXPLOTACION.md](04_MANUAL_EXPLOTACION.md) | Instalacion, despliegue, variables de entorno |
| [CONTRATO_API_HTTP.md](contratos/CONTRATO_API_HTTP.md) | Contrato API original detallado |
| [CONFIANZA_AGREGADA.md](CONFIANZA_AGREGADA.md) | Logica de calculo de confianza |
