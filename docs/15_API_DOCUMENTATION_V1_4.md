# TipologiasAdminFunction - API Documentation v1.4

**Version:** 1.4 (PromptGPT Deprecation)  
**Last Updated:** 2026-06-05  
**Status:** Active  
**Base URL:** `https://{host}/api`

---

## Table of Contents

1. [Overview](#overview)
2. [Authentication](#authentication)
3. [Error Handling](#error-handling)
4. [Endpoints](#endpoints)
5. [Data Models](#data-models)
6. [Field Types](#field-types)
7. [v1.4 Migration Notes](#v14-migration-notes)
8. [Examples](#examples)
9. [FAQ](#faq)

---

## Overview

The **TipologiasAdminFunction** API provides CRUD operations for managing document classification Tipologías. Each Tipología defines a classification schema with configurable fields, validation rules, and prompt templates.

### Key Features

- ✅ Full lifecycle management (Draft → Published → Retired)
- ✅ JSON-based configuration as single source of truth
- ✅ Audit trail for all changes
- ✅ Cache invalidation on publish
- ✅ Bulk import/export via ZIP
- ✅ Version control and conflict detection

### v1.4 Changes

**⚠️ PromptGPT Field Deprecated**

In v1.4, the direct `.promptGPT` field is **deprecated but supported** for backward compatibility. All prompt templates are now managed via `configuracionJson.promptConfig`:

| Property | v1.3 | v1.4 | v1.5 | v2.0 |
|----------|------|------|------|------|
| `.promptGPT` | Primary | Supported (read deprecated) | Read-only | Removed |
| `configuracionJson.promptConfig.systemPrompt` | N/A | Primary | Primary | Primary |
| `configuracionJson.promptConfig.userPromptTemplate` | N/A | Primary | Primary | Primary |

**Migration Impact:** Existing Tipologías continue to work. Update `configuracionJson` to adopt v1.4 standard.

---

## Authentication

### Authorization Level

All endpoints use `AuthorizationLevel.Function` (Azure Functions key-based auth).

```powershell
# PowerShell Example
$headers = @{
    "x-functions-key" = "YOUR_FUNCTION_KEY"
    "Content-Type" = "application/json"
}
```

### Required Headers

- `Content-Type: application/json` (for POST/PUT)
- `x-functions-key` or `code` parameter (Azure Functions key)

---

## Error Handling

All endpoints return structured error responses:

```json
{
  "error": "Descriptive error message",
  "timestamp": "2026-06-05T10:30:45Z",
  "traceId": "0HMVD..."
}
```

### HTTP Status Codes

| Code | Meaning | Common Cause |
|------|---------|--------------|
| 200 | OK | Request succeeded |
| 201 | Created | Resource created |
| 400 | Bad Request | Invalid input, validation failure |
| 404 | Not Found | Tipología ID doesn't exist |
| 409 | Conflict | State transition not allowed, duplicate código |
| 500 | Internal Server Error | Server error |

---

## Endpoints

### 1. List All Tipologías

**GET** `/management/tipologias`

Retrieves all Tipologías sorted by name.

#### Response

```json
[
  {
    "id": 1,
    "codigo": "FACTURA",
    "nombre": "Factura Electrónica",
    "version": "1.4.0",
    "estado": "Published",
    "activa": true,
    "configuracionJson": { ... },
    "creadoPor": "user@example.com",
    "fechaCreacion": "2026-04-01T09:00:00Z",
    "fechaActualizacion": "2026-06-04T15:30:00Z"
  },
  { ... }
]
```

#### Status Codes
- `200 OK` — Success

---

### 2. Get Tipología by ID

**GET** `/management/tipologias/{id}`

Retrieves a single Tipología by ID.

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | int | Yes | Tipología ID |

#### Response

```json
{
  "id": 1,
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica",
  "version": "1.4.0",
  "estado": "Published",
  "activa": true,
  "configuracionJson": { ... },
  "creadoPor": "user@example.com",
  "fechaCreacion": "2026-04-01T09:00:00Z",
  "fechaActualizacion": "2026-06-04T15:30:00Z"
}
```

#### Status Codes
- `200 OK` — Success
- `404 Not Found` — Tipología does not exist

#### Example

```powershell
$response = Invoke-RestMethod `
  -Uri "https://myhost.azurewebsites.net/api/management/tipologias/1" `
  -Headers $headers `
  -Method Get

$response | ConvertTo-Json -Depth 10
```

---

### 3. Create Tipología

**POST** `/management/tipologias`

Creates a new Tipología. Initial state is **Draft**.

#### Request Body

```json
{
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica",
  "version": "1.4.0",
  "configuracionJson": { ... },
  "usuario": "user@example.com"
}
```

#### Request Fields

| Field | Type | Required | Validation |
|-------|------|----------|-----------|
| `codigo` | string | Yes | Alphanumeric + `_.-`, 3-100 chars, lowercase start |
| `nombre` | string | Yes | 1-255 chars, no special chars |
| `version` | string | Yes | Semantic versioning (e.g., `1.4.0`) |
| `configuracionJson` | object | Yes | Valid JSON schema (see below) |
| `usuario` | string | No | Audit trail. Defaults to `SYSTEM` |

#### Response

```json
{
  "id": 5,
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica",
  "version": "1.4.0",
  "estado": "Draft",
  "activa": true,
  "configuracionJson": { ... },
  "creadoPor": "user@example.com",
  "fechaCreacion": "2026-06-05T10:30:00Z",
  "fechaActualizacion": "2026-06-05T10:30:00Z"
}
```

#### Status Codes
- `201 Created` — Success
- `400 Bad Request` — Invalid input or schema
- `409 Conflict` — Código already exists

#### Example

```powershell
$body = @{
  codigo = "FACTURA"
  nombre = "Factura Electrónica"
  version = "1.4.0"
  configuracionJson = @{
    tipoDocumento = "Factura"
    campos = @(
      @{ nombre = "numero"; tipo = "string"; obligatorio = $true }
      @{ nombre = "fecha"; tipo = "date"; obligatorio = $true }
    )
    promptConfig = @{
      systemPrompt = "Extract invoice data..."
      userPromptTemplate = "Process this invoice..."
    }
  }
  usuario = "admin@example.com"
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod `
  -Uri "https://myhost.azurewebsites.net/api/management/tipologias" `
  -Headers $headers `
  -Method Post `
  -Body $body
```

---

### 4. Update Tipología

**PUT** `/management/tipologias/{id}`

Updates an existing Tipología. Only Draft and Retired states can be updated.

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | int | Yes | Tipología ID |

#### Request Body

```json
{
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica v2",
  "version": "1.4.1",
  "configuracionJson": { ... },
  "usuario": "user@example.com"
}
```

#### Constraints

- ⚠️ **Cannot update:** `id`, `estado`, `creadoPor`, `fechaCreacion`
- ✅ **Can update:** `nombre`, `version`, `configuracionJson`
- ⛔ **Cannot update Published:** If `estado == Published`, update fails with 409

#### Response

```json
{
  "id": 5,
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica v2",
  "version": "1.4.1",
  "estado": "Draft",
  "activa": true,
  "configuracionJson": { ... },
  "creadoPor": "user@example.com",
  "fechaCreacion": "2026-06-05T10:30:00Z",
  "fechaActualizacion": "2026-06-05T11:00:00Z"
}
```

#### Status Codes
- `200 OK` — Success
- `400 Bad Request` — Invalid input
- `404 Not Found` — Tipología does not exist
- `409 Conflict` — Cannot update Published Tipología

#### Example

```powershell
$body = @{
  codigo = "FACTURA"
  nombre = "Factura Electrónica v2"
  version = "1.4.1"
  configuracionJson = @{ ... }
  usuario = "admin@example.com"
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod `
  -Uri "https://myhost.azurewebsites.net/api/management/tipologias/5" `
  -Headers $headers `
  -Method Put `
  -Body $body
```

---

### 5. Publish Tipología

**POST** `/management/tipologias/{id}/publicar`

Transitions Tipología from **Draft** to **Published**. Validates all configuration and model references.

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | int | Yes | Tipología ID |

#### Request Body

```json
{
  "usuario": "user@example.com"
}
```

#### Pre-Conditions

- ✅ Tipología must be in **Draft** state
- ✅ `configuracionJson` must be valid
- ✅ All referenced models must exist

#### Response

```json
{
  "id": 5,
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica",
  "version": "1.4.0",
  "estado": "Published",
  "activa": true,
  "configuracionJson": { ... },
  "creadoPor": "user@example.com",
  "fechaCreacion": "2026-06-05T10:30:00Z",
  "fechaActualizacion": "2026-06-05T11:15:00Z"
}
```

#### Side Effects

- ✅ Cache invalidated (`tipologias:snapshot`)
- ✅ Audit record created
- ✅ State transitions to Published

#### Status Codes
- `200 OK` — Success
- `404 Not Found` — Tipología does not exist
- `409 Conflict` — Invalid state or configuration

#### Example

```powershell
$body = @{ usuario = "admin@example.com" } | ConvertTo-Json

$response = Invoke-RestMethod `
  -Uri "https://myhost.azurewebsites.net/api/management/tipologias/5/publicar" `
  -Headers $headers `
  -Method Post `
  -Body $body
```

---

### 6. Retire Tipología

**POST** `/management/tipologias/{id}/retirar`

Transitions Tipología from **Published** to **Retired**. Marks as inactive.

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | int | Yes | Tipología ID |

#### Request Body

```json
{
  "usuario": "user@example.com"
}
```

#### Response

```json
{
  "id": 5,
  "estado": "Retired",
  "activa": false,
  ...
}
```

#### Status Codes
- `200 OK` — Success
- `404 Not Found` — Tipología does not exist

---

### 7. Move to Draft

**POST** `/management/tipologias/{id}/draft`

Transitions Tipología from **Retired** back to **Draft** for re-editing.

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | int | Yes | Tipología ID |

#### Request Body

```json
{
  "usuario": "user@example.com"
}
```

#### Response

```json
{
  "id": 5,
  "estado": "Draft",
  "activa": true,
  ...
}
```

#### Status Codes
- `200 OK` — Success
- `404 Not Found` — Tipología does not exist

---

### 8. Get Audit Trail

**GET** `/management/tipologias/{id}/audit`

Retrieves complete change history for a Tipología.

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | int | Yes | Tipología ID |

#### Response

```json
[
  {
    "id": 101,
    "tipologiaId": 5,
    "usuario": "admin@example.com",
    "accion": "Created",
    "descripcion": "Initial creation",
    "fechaAuditoria": "2026-06-05T10:30:00Z",
    "valoresAnteriores": null,
    "valoresNuevos": { ... }
  },
  {
    "id": 102,
    "tipologiaId": 5,
    "usuario": "admin@example.com",
    "accion": "Updated",
    "descripcion": "Updated",
    "fechaAuditoria": "2026-06-05T11:00:00Z",
    "valoresAnteriores": { ... },
    "valoresNuevos": { ... }
  }
]
```

#### Status Codes
- `200 OK` — Success
- `404 Not Found` — Tipología does not exist

---

## Data Models

### TipologiaResponseDto (v1.4)

**⚠️ v1.4 Format:** Direct `.promptGPT` field is **omitted** from response (maintained internally via extension methods).

```json
{
  "id": 5,
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica",
  "version": "1.4.0",
  "estado": "Published",
  "activa": true,
  "configuracionJson": {
    "tipoDocumento": "Factura",
    "campos": [
      {
        "nombre": "numero",
        "tipo": "string",
        "obligatorio": true,
        "validacion": "^[0-9]{1,10}$"
      }
    ],
    "promptConfig": {
      "systemPrompt": "Extract invoice data following JSON schema...",
      "userPromptTemplate": "Process this invoice: {documento}"
    }
  },
  "creadoPor": "user@example.com",
  "fechaCreacion": "2026-06-05T10:30:00Z",
  "fechaActualizacion": "2026-06-05T11:00:00Z"
}
```

### TipologiaUpsertRequest

Used in POST (create) and PUT (update) operations.

```json
{
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica",
  "version": "1.4.0",
  "configuracionJson": { ... },
  "usuario": "user@example.com"
}
```

### ConfiguracionJson Schema

```json
{
  "tipoDocumento": "string (required)",
  "campos": [
    {
      "nombre": "string (required, alphanumeric + _ )",
      "tipo": "string|decimal|number|integer|int|date|datetime|boolean|bool|array|object (required)",
      "obligatorio": "boolean (default: false)",
      "validacion": "regex pattern (optional)",
      "descripcion": "string (optional)"
    }
  ],
  "promptConfig": {
    "systemPrompt": "string (required, AI system prompt)",
    "userPromptTemplate": "string (required, user prompt template with {campo} placeholders)"
  },
  "modelosReferenciados": ["string"] (optional, list of model IDs)
}
```

### Field Types Supported

| Type | Example | Validation |
|------|---------|-----------|
| `string` | `"ABC123"` | Text |
| `integer` / `int` | `42` | Whole number |
| `decimal` / `number` | `123.45` | Decimal number |
| `boolean` / `bool` | `true` / `false` | Boolean |
| `date` | `2026-06-05` | ISO date |
| `datetime` | `2026-06-05T10:30:00Z` | ISO datetime |
| `array` | `[...]` | JSON array |
| `object` | `{...}` | JSON object |

---

## v1.4 Migration Notes

### Breaking Changes: None ✅

v1.4 is **fully backward compatible** with v1.3. Existing clients continue to work.

### Deprecations ⚠️

| Feature | Status | Migration Path | Deadline |
|---------|--------|-----------------|----------|
| `.promptGPT` field (direct read) | Deprecated | Use `configuracionJson.promptConfig.systemPrompt` | v1.5 (2026-06-30) |
| Legacy DTO (with `.promptGPT`) | Supported | Not recommended for new code | v1.5 (2026-06-30) |
| Direct field access | Deprecated | Use extension methods: `tipologia.GetSystemPrompt()` | v2.0 (2026-07-31) |

### Recommended Updates

#### For v1.4 (New Code)

Use `ConfiguracionJson` for all prompt management:

```json
{
  "codigo": "FACTURA",
  "nombre": "Factura Electrónica",
  "version": "1.4.0",
  "configuracionJson": {
    "tipoDocumento": "Factura",
    "campos": [ ... ],
    "promptConfig": {
      "systemPrompt": "Extract invoice data...",
      "userPromptTemplate": "Process: {documento}"
    }
  }
}
```

#### For Developers (Backend Code)

**v1.3 (Old Way):**
```csharp
string systemPrompt = tipologia.PromptGPT;
```

**v1.4 (New Way):**
```csharp
string systemPrompt = tipologia.GetSystemPrompt();
string userTemplate = tipologia.GetUserPromptTemplate();
```

(See [12_MIGRACION_PROMPTGPT_V1_4.md](12_MIGRACION_PROMPTGPT_V1_4.md) for full guide)

### Data Migration for v1.5

In v1.5 (June 30, 2026), `.promptGPT` becomes **read-only**. If your data still uses direct prompts:

1. Extract `.promptGPT` value
2. Write to `configuracionJson.promptConfig.systemPrompt`
3. Re-publish Tipología

**Automatic migration script planned for v1.5 release.**

---

## Examples

### Complete Workflow: Create → Update → Publish

```powershell
# 1. Create a new Tipología (Draft state)
$create = @{
  codigo = "CONTRATO"
  nombre = "Contrato Comercial"
  version = "1.0.0"
  configuracionJson = @{
    tipoDocumento = "Contrato"
    campos = @(
      @{ nombre = "partes"; tipo = "array"; obligatorio = $true }
      @{ nombre = "fechaFirma"; tipo = "date"; obligatorio = $true }
    )
    promptConfig = @{
      systemPrompt = "Extract contract details..."
      userPromptTemplate = "Analyze contract: {documento}"
    }
  }
  usuario = "admin@example.com"
} | ConvertTo-Json -Depth 10

$tipologia = Invoke-RestMethod `
  -Uri "https://myhost/api/management/tipologias" `
  -Headers $headers `
  -Method Post `
  -Body $create

$id = $tipologia.id
Write-Host "Created Tipología ID: $id"

# 2. Update (still in Draft)
$update = @{
  codigo = "CONTRATO"
  nombre = "Contrato Comercial v1.1"
  version = "1.0.1"
  configuracionJson = @{
    tipoDocumento = "Contrato"
    campos = @(
      @{ nombre = "partes"; tipo = "array"; obligatorio = $true }
      @{ nombre = "fechaFirma"; tipo = "date"; obligatorio = $true }
      @{ nombre = "monto"; tipo = "decimal"; obligatorio = $false }
    )
    promptConfig = @{
      systemPrompt = "Extract contract details with amounts..."
      userPromptTemplate = "Analyze contract: {documento}"
    }
  }
  usuario = "admin@example.com"
} | ConvertTo-Json -Depth 10

$updated = Invoke-RestMethod `
  -Uri "https://myhost/api/management/tipologias/$id" `
  -Headers $headers `
  -Method Put `
  -Body $update

Write-Host "Updated: $($updated.nombre)"

# 3. Publish (Draft → Published)
$publish = @{ usuario = "admin@example.com" } | ConvertTo-Json

$published = Invoke-RestMethod `
  -Uri "https://myhost/api/management/tipologias/$id/publicar" `
  -Headers $headers `
  -Method Post `
  -Body $publish

Write-Host "Published: $($published.estado)"

# 4. Check audit trail
$audit = Invoke-RestMethod `
  -Uri "https://myhost/api/management/tipologias/$id/audit" `
  -Headers $headers `
  -Method Get

$audit | ForEach-Object {
  Write-Host "$($_.fechaAuditoria) - $($_.usuario): $($_.accion)"
}
```

### Batch Operations via cURL

```bash
# Create
curl -X POST "https://myhost/api/management/tipologias" \
  -H "x-functions-key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "codigo": "FACTURA",
    "nombre": "Factura",
    "version": "1.4.0",
    "configuracionJson": { ... },
    "usuario": "admin@example.com"
  }'

# Get all
curl -X GET "https://myhost/api/management/tipologias" \
  -H "x-functions-key: YOUR_KEY"

# Get by ID
curl -X GET "https://myhost/api/management/tipologias/5" \
  -H "x-functions-key: YOUR_KEY"

# Publish
curl -X POST "https://myhost/api/management/tipologias/5/publicar" \
  -H "x-functions-key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{ "usuario": "admin@example.com" }'
```

---

## FAQ

### Q: Can I update a Published Tipología?

**A:** No. You must first transition to **Retired** (via `/retirar`), then move back to **Draft** (via `/draft`), then update, and re-publish.

```
Published → Retirar → Retired → Draft → Update → Publicar → Published
```

### Q: What happens to the `.promptGPT` field in my existing data?

**A:** In v1.4, it's **maintained automatically** for backward compatibility:
- If you read `.promptGPT`, it returns the value from `configuracionJson.promptConfig.systemPrompt`
- If you write to `.promptGPT` (legacy code), it updates both locations internally
- **Recommended:** Migrate to `configuracionJson` for all new code

### Q: What if my `configuracionJson` is invalid?

**A:** You'll receive a `400 Bad Request` with a specific error message. Common issues:
- Missing `tipoDocumento`
- Field types not in allowed list
- Missing `promptConfig` object
- Invalid field validation regex

### Q: Can I bulk import Tipologías?

**A:** There's a separate `POST /management/tipologias/import` endpoint for ZIP imports. See [04_MANUAL_EXPLOTACION.md](04_MANUAL_EXPLOTACION.md) for details.

### Q: What's the timeout for publish validation?

**A:** 30 seconds. If validation of referenced models exceeds this, publish fails with `504 Gateway Timeout`. Reduce model complexity or check model availability.

### Q: How do I handle version conflicts?

**A:** The API accepts any valid semantic version string. Conflict detection is **application logic**:
- Audit trail tracks all changes
- For merge conflicts in v1.5, see [12_MIGRACION_PROMPTGPT_V1_4.md](12_MIGRACION_PROMPTGPT_V1_4.md)

### Q: What's the cache invalidation strategy?

**A:** Only `Publicar` (publish) invalidates the snapshot cache (`tipologias:snapshot`). This ensures:
- Published versions are immediately available
- Draft changes don't affect live classification
- Retirements take effect immediately

### Q: Can I get notified on changes?

**A:** The audit trail captures all changes. Recommended pattern:
- Poll `/management/tipologias/{id}/audit` periodically
- Or watch for `FechaActualizacion` changes
- For events: App Insights custom event logging planned for v2.0

---

## Support & Feedback

- **Issues:** File bug reports on Azure DevOps (AB#99732 epic)
- **Questions:** See [12_MIGRACION_PROMPTGPT_V1_4.md](12_MIGRACION_PROMPTGPT_V1_4.md) for detailed migration guide
- **Deprecation Timeline:** [12_MIGRACION_PROMPTGPT_V1_4.md](12_MIGRACION_PROMPTGPT_V1_4.md#deprecation-timeline)

**Document Version:** 1.4.0  
**Last Updated:** 2026-06-05  
**Next Review:** 2026-06-30 (v1.5 planning)
