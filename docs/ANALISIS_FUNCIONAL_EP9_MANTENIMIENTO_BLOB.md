# Análisis Funcional – EP 9: Mantenimiento y Limpieza de Blob Storage

> **BORRADOR NO PRODUCTIVO**
> Documento en elaboración para roadmap. No representa funcionalidad completada ni comportamiento vigente en producción.

**Versión:** 1.1
**Fecha:** 2026-02-26
**Épica:** EP 9 – Mantenimiento y limpieza de Blob Storage
**Estado:** Borrador

---

## Índice

1. [Contexto y motivación](#1-contexto-y-motivaci%C3%B3n)
2. [Alcance funcional](#2-alcance-funcional)
3. [Actores y roles](#3-actores-y-roles)
4. [Modelo de datos](#4-modelo-de-datos)
5. [Flujos funcionales](#5-flujos-funcionales)
6. [Configuración de políticas de retención](#6-configuraci%C3%B3n-de-pol%C3%ADticas-de-retenci%C3%B3n)
7. [Motor de limpieza automática](#7-motor-de-limpieza-autom%C3%A1tica)
8. [Inventario y reporting de ocupación](#8-inventario-y-reporting-de-ocupaci%C3%B3n)
9. [Hold: protección frente a limpieza](#9-hold-protecci%C3%B3n-frente-a-limpieza)
10. [Auditoría y observabilidad](#10-auditor%C3%ADa-y-observabilidad)
11. [Reglas de negocio](#11-reglas-de-negocio)
12. [Criterios de aceptación globales](#12-criterios-de-aceptaci%C3%B3n-globales)
13. [Dependencias y riesgos](#13-dependencias-y-riesgos)
14. [Glosario](#14-glosario)

---

## 1. Contexto y motivación

### 1.1 Situación actual

DocumentIA ingesta y procesa documentos en un pipeline donde `NormalizarActivity` calcula integridad (`SHA256/CRC32`) y la subida a Blob Storage (Azure Blob / Azurite en local) se realiza en `SubirBlobActivity`, tras la verificación de duplicado. La ruta de cada blob se persiste en el campo `RutaBlobStorage` de la entidad `DocumentoEntity`. A fecha de este análisis:

- `IBlobStorageService.DeleteDocumentAsync` está implementado pero **nunca se invoca** desde el pipeline.
- No existe ninguna política de retención, TTL ni proceso de limpieza programada.
- El volumen de blobs crece indefinidamente conforme se procesan documentos.

### 1.2 Problema

La ocupación del Blob Storage crece de forma ilimitada, lo que supone:

| Impacto | Descripción |
|---------|-------------|
| **Coste** | Facturación creciente por GB almacenado en Azure Blob Storage |
| **Compliance** | Documentos con datos personales retenidos más tiempo del legalmente necesario (GDPR/LOPD) |
| **Operativo** | Sin visibilidad sobre qué está almacenado, cuánto ocupa ni cuándo expira |
| **Técnico** | Sin mecanismo para limpiar datos de prueba ni entornos non-prod |

### 1.3 Objetivo

Implementar un sistema de mantenimiento que:

1. Permita definir una **política de retención por tipología** (número de días, acción al expirar).
2. Ejecute de forma **automática y programada** la limpieza de blobs expirados.
3. Proporcione **visibilidad** sobre la ocupación actual y las próximas expiraciones.
4. Garantice la **trazabilidad completa** de cada operación de borrado mediante auditoría.
5. Soporte un mecanismo de **hold** para proteger documentos que no deben borrarse.

---

## 2. Alcance funcional

### Incluido en EP 9

| Feature | Descripción |
|---------|-------------|
| F 9.1 | Política de retención configurable por tipología |
| F 9.2 | Motor de limpieza automática (timer + Durable Functions) |
| F 9.3 | Inventario y reporting de ocupación |
| F 9.4 | Observabilidad y auditoría de operaciones de limpieza |

### Fuera de alcance

- Migración retroactiva de blobs ya existentes (se contempla como tarea de backfill opcional).
- Cifrado adicional de blobs antes del borrado (cubierto por EP 7).
- Archivado a Azure Blob Storage tier *Cool/Archive* (puede ser una evolución posterior).
- Interfaz de usuario (sólo API y métricas).

---

## 3. Actores y roles

| Actor | Rol | Interacción con el sistema |
|-------|-----|---------------------------|
| **Responsable de operaciones** | Configura la política de retención en JSON | Define `maxRetentionDays` y `actionOnExpiry` por tipología |
| **Sistema (scheduler)** | Ejecuta el ciclo de limpieza | Invoca `BlobCleanupTimerTrigger` periódicamente |
| **Responsable legal / Compliance** | Gestiona holds activos | Activa/desactiva `HoldActivo` en documentos bajo litigo o auditoría |
| **Arquitecto / Monitorización** | Consulta ocupación | Llama a `GET /api/BlobInventory` o revisa dashboards |
| **Auditor** | Revisa operaciones de borrado | Consulta tabla `Auditoria` con acciones `BlobEliminado` |

---

## 4. Modelo de datos

### 4.1 Nuevos campos en DocumentoEntity

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `HoldActivo` | `bool` (default `false`) | Protege el blob frente al motor de limpieza |

### 4.2 Nueva tabla: BlobRetentionTrigger

Registra el trigger de expiración de cada blob en el momento de la ingesta.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `Id` | `int` PK | Auto-increment |
| `DocumentoId` | `int` FK → `Documentos.Id` | Documento asociado |
| `FechaExpiracionBlob` | `datetime` (índice) | Fecha a partir de la cual el blob puede borrarse |
| `Accion` | `varchar(50)` | `delete` o `anonymize` |
| `Estado` | `varchar(50)` | `Pendiente`, `Ejecutado`, `OmitidoHold`, `Error` |
| `ExecutedAt` | `datetime?` | Fecha en que se ejecutó la acción |
| `ErrorMessage` | `varchar(500)?` | Detalle del error si Estado = Error |

**Índice:** `IX_BlobRetentionTrigger_FechaExpiracion` sobre `(FechaExpiracionBlob, Estado)` para consultas eficientes del motor de limpieza.

### 4.3 Cambios en AuditoriaEntity

Se añade soporte para nuevas acciones de auditoría relacionadas con blobs:

| Acción | Descripción |
|--------|-------------|
| `BlobEliminado` | Borrado exitoso de un blob |
| `BlobEliminadoError` | Intento de borrado fallido |
| `BlobOmitidoPorHold` | Borrado omitido porque `HoldActivo = true` |
| `BlobNoEncontrado` | El blob no existía en storage al intentar borrarlo |

### 4.4 Diagrama entidad-relación (simplificado)

```
Documentos (existente)
  ├── Id PK
  ├── RutaBlobStorage  ──── (se pone a NULL tras borrado)
  ├── HoldActivo  ◄──── NUEVO
  └── ...
       │
       └── BlobRetentionTrigger  ◄──── NUEVA TABLA
             ├── Id PK
             ├── DocumentoId FK
             ├── FechaExpiracionBlob
             ├── Accion
             ├── Estado
             ├── ExecutedAt
             └── ErrorMessage

Auditoria (existente, ampliada)
  ├── DocumentoId FK
  ├── Accion  (BlobEliminado, BlobOmitidoPorHold, ...)
  ├── Resultado
  └── ...
```

---

## 5. Flujos funcionales

### 5.1 Flujo de ingesta con registro de retención

```
IngestAPITrigger
     │
     ▼
DocumentProcessOrchestrator
     │
  ├─► NormalizarActivity
  │
  ├─► VerificarDuplicadoActivity (si aplica)
  │
  ├─► SubirBlobActivity (si no se corta por duplicado)
  │       └── Sube blob → devuelve RutaBlobStorage
     │
     ├─► ... (clasificar, extraer, validar, integrar)
     │
     └─► PersistirActivity  ◄── MODIFICADO
              ├── Guarda DocumentoEntity (RutaBlobStorage)
              └── IBlobRetentionPolicyService.GetPolicy(tipologia)
                       ├── maxRetentionDays != -1 ?
                       │       YES → crea BlobRetentionTrigger
                       │              FechaExpiracionBlob = FechaProceso + maxRetentionDays
                       │              Estado = Pendiente
                       └── NO → no se crea trigger (retención indefinida)
```

### 5.2 Flujo del motor de limpieza automática

```
BlobCleanupTimerTrigger (cron)
     │
     ▼
BlobMaintenanceOrchestrator (Durable)
     │
     ├── Consulta BlobRetentionTrigger WHERE FechaExpiracionBlob <= NOW
     │   AND Estado = 'Pendiente'
     │   ORDER BY FechaExpiracionBlob ASC
     │   (en lotes de batchSize, configurable por tipología)
     │
     └── Por cada lote → DeleteExpiredBlobsActivity
              │
              ├── Para cada trigger del lote:
              │       ├── Carga DocumentoEntity → HoldActivo?
              │       │       YES → marca trigger OmitidoHold
              │       │              registra auditoría BlobOmitidoPorHold
              │       │              CONTINÚA con siguiente
              │       │
              │       ├── IBlobStorageService.ExistsAsync(blobPath)
              │       │       NO → marca trigger Ejecutado (blob ya no existe)
              │       │            registra auditoría BlobNoEncontrado
              │       │
              │       ├── IBlobStorageService.DeleteDocumentAsync(blobPath)
              │       │       OK → RutaBlobStorage = NULL en DocumentoEntity
              │       │            trigger → Estado Ejecutado, ExecutedAt = NOW
              │       │            registra auditoría BlobEliminado
              │       │
              │       └── ERROR → trigger → Estado Error, ErrorMessage = ex.Message
              │                   registra auditoría BlobEliminadoError
              │                   CONTINÚA con siguiente (no interrumpe el lote)
              │
              └── Emite métricas Application Insights (blobs_deleted, errors, freed_bytes)
```

### 5.3 Flujo de consulta de inventario

```
Cliente HTTP
     │
     ▼ GET /api/BlobInventory?tipologia=Tasacion
     │
BlobInventoryTrigger (HTTP Function)
     │
     ▼
IBlobInventoryService.GetInventoryAsync(filtros)
     │
     ├── Agrega DocumentoEntity WHERE RutaBlobStorage IS NOT NULL
     │   [AND Tipologia = filtro si se proporciona]
     │
     ├── Cruza con BlobRetentionTrigger para próximas expiraciones
     │
     └── Devuelve BlobInventoryResponse:
              ├── TotalBlobsActivos
              ├── TamanoTotalBytesEstimado
              ├── ProximasExpiraciones7Dias
              ├── ProximasExpiraciones30Dias
              └── DesglosePorTipologia[]
                      ├── Tipologia
                      ├── BlobsActivos
                      └── TamanoEstimadoBytes
```

---

## 6. Configuración de políticas de retención

La política de retención se define dentro del fichero JSON de configuración de cada tipología, bajo el bloque `retentionPolicy`.

### 6.1 Esquema JSON

```json
{
  "codigo": "Tasacion",
  "nombre": "Tasación Inmobiliaria",
  "version": "1.0",
  "umbrales": { ... },
  "retentionPolicy": {
    "maxRetentionDays": 365,
    "actionOnExpiry": "delete",
    "batchSize": 100
  }
}
```

### 6.2 Descripción de campos

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| `maxRetentionDays` | `int` | Sí | Días que se retiene el blob desde `FechaProceso`. `-1` = indefinido (no crea trigger). `0` = expira inmediatamente. |
| `actionOnExpiry` | `string` | Sí | Acción al expirar: `delete` (borrado físico) o `anonymize` (en combinación con EP 7). En EP 9 sólo se implementa `delete`. |
| `batchSize` | `int` | No (default: 50) | Máximo de blobs procesados por lote en cada ciclo de limpieza. Evita timeouts en Durable Functions. |

### 6.3 Comportamiento por valores de maxRetentionDays

| Valor | Comportamiento |
|-------|---------------|
| `-1` | No se crea `BlobRetentionTrigger`. El blob se conserva hasta intervención manual. |
| `0` | El trigger se crea con `FechaExpiracionBlob = FechaProceso`. El blob expira en el primer ciclo de limpieza posterior a la ingesta. Uso típico: entornos de test. |
| `> 0` | El trigger se crea con `FechaExpiracionBlob = FechaProceso + N días`. |

### 6.4 Tipología sin bloque retentionPolicy

Si la configuración de una tipología no incluye `retentionPolicy`, se aplica la política global de la aplicación definida en `appsettings.json`. Si tampoco existe política global, el blob se trata como retención indefinida (equivalente a `maxRetentionDays = -1`).

```json
// appsettings.json – política global de fallback
{
  "BlobRetention": {
    "DefaultMaxRetentionDays": -1,
    "DefaultActionOnExpiry": "delete"
  }
}
```

---

## 7. Motor de limpieza automática

### 7.1 Componentes

| Componente | Tipo | Responsabilidad |
|-----------|------|-----------------|
| `BlobCleanupTimerTrigger` | Azure Function (TimerTrigger) | Desencadena el ciclo de limpieza según cron |
| `BlobMaintenanceOrchestrator` | Durable Functions Orchestrator | Coordina la consulta y el procesamiento por lotes |
| `DeleteExpiredBlobsActivity` | Durable Functions Activity | Ejecuta el borrado físico y actualiza la BD |
| `IBlobRetentionRepository` | Interfaz de datos | Consulta y actualiza `BlobRetentionTrigger` |

### 7.2 Configuración del scheduler

```json
// local.settings.json
{
  "Values": {
    "BlobCleanupCron": "0 0 2 * * *"
  }
}
```

La expresión cron por defecto ejecuta el ciclo cada día a las 02:00 UTC. Es configurable por entorno sin necesidad de redeploy.

### 7.3 Procesamiento por lotes y control de idempotencia

- Los triggers se consultan **ordenados por `FechaExpiracionBlob` ASC**, priorizando los más antiguos.
- Cada lote incluye como máximo `batchSize` triggers en estado `Pendiente`.
- Si una ejecución falla a mitad, los triggers ya procesados tienen estado `Ejecutado` y no se reprocesarán.
- Los triggers en estado `Error` no se reintentarán automáticamente (requieren intervención o una tarea de reconciliación futura).

### 7.4 Tratamiento de errores

| Situación | Comportamiento |
|-----------|---------------|
| Blob no existe en storage | Trigger → `Ejecutado`, auditoría `BlobNoEncontrado`. No se considera error. |
| Error de red / timeout al borrar | Trigger → `Error`, auditoría `BlobEliminadoError`. Ciclo continúa. |
| `HoldActivo = true` en documento | Trigger → `OmitidoHold`, auditoría `BlobOmitidoPorHold`. Ciclo continúa. |
| Error al actualizar BD | Logging de error crítico, trigger permanece `Pendiente` para reintento. |

---

## 8. Inventario y reporting de ocupación

### 8.1 Endpoint

```
GET /api/BlobInventory
GET /api/BlobInventory?tipologia={codigo}
```

**Autenticación:** igual al resto de endpoints del sistema (Function Key o AAD según entorno).

### 8.2 Respuesta

```json
{
  "timestamp": "2026-02-25T10:30:00Z",
  "totalBlobsActivos": 12450,
  "tamanoTotalBytesEstimado": 9876543210,
  "proximasExpiraciones7Dias": 340,
  "proximasExpiraciones30Dias": 1200,
  "desglosePorTipologia": [
    {
      "tipologia": "Tasacion",
      "blobsActivos": 10200,
      "tamanoEstimadoBytes": 8123456789,
      "proximasExpiraciones7Dias": 300
    },
    {
      "tipologia": "NotaSimple",
      "blobsActivos": 2250,
      "tamanoEstimadoBytes": 1753086421,
      "proximasExpiraciones7Dias": 40
    }
  ]
}
```

### 8.3 Estimación del tamaño

El tamaño se estima a partir del campo `TamanoBytes` de `DocumentoEntity` (si está disponible). Si el campo está a cero o nulo, se excluye de la suma. No se realizan llamadas al SDK de Azure Storage para obtener el tamaño real (coste y latencia elevados para colecciones grandes).

---

## 9. Hold: protección frente a limpieza

### 9.1 Concepto

Un documento puede quedar protegido frente al motor de limpieza mediante el flag `HoldActivo = true`. Casos de uso típicos:

- Documentos bajo proceso judicial o arbitral.
- Documentos sometidos a auditoría externa.
- Documentos trazados como prueba en un expediente regulatorio.

### 9.2 Activación y desactivación

En el alcance de EP 9, el hold se gestiona directamente mediante el repositorio. En una evolución futura se puede exponer un endpoint protegido (`PUT /api/Documents/{id}/hold`).

```csharp
// IDocumentoRepository
Task SetHoldAsync(int documentoId, bool holdActivo);
```

### 9.3 Impacto en el motor de limpieza

Cuando `DeleteExpiredBlobsActivity` encuentra un documento con `HoldActivo = true`:
1. **No borra el blob** ni modifica `RutaBlobStorage`.
2. Marca el trigger como `OmitidoHold` (no como `Ejecutado`), de modo que volverá a evaluarse en ciclos futuros cuando se desactive el hold.
3. Registra una entrada en `Auditoria` con acción `BlobOmitidoPorHold`.

---

## 10. Auditoría y observabilidad

### 10.1 Entradas de auditoría por operación

Cada operación del motor de limpieza genera una entrada en la tabla `Auditoria`:

| Acción | Severidad | Mensaje ejemplo |
|--------|-----------|-----------------|
| `BlobEliminado` | Info | "Blob eliminado por retención: documents/2024/01/abc123.pdf" |
| `BlobNoEncontrado` | Warning | "Blob no encontrado en storage, trigger marcado como ejecutado" |
| `BlobEliminadoError` | Error | "Error al eliminar blob: {ex.Message}" |
| `BlobOmitidoPorHold` | Info | "Borrado omitido: documento con HoldActivo" |

### 10.2 Logging estructurado

Cada ciclo de limpieza emite un log estructurado con los siguientes campos:

```json
{
  "ciclo_id": "cleanup-20260225-02:00",
  "blobs_procesados": 150,
  "blobs_eliminados": 143,
  "blobs_omitidos_hold": 3,
  "blobs_no_encontrados": 2,
  "blobs_error": 2,
  "bytes_liberados_estimados": 1234567890,
  "duracion_ms": 4200
}
```

### 10.3 Métricas en Application Insights

| Métrica | Tipo | Descripción |
|---------|------|-------------|
| `blob_maintenance_deleted` | Counter | Blobs borrados por ciclo |
| `blob_maintenance_errors` | Counter | Errores de borrado por ciclo |
| `blob_maintenance_hold_skipped` | Counter | Blobs omitidos por hold por ciclo |
| `blob_maintenance_freed_bytes` | Sum | Bytes estimados liberados por ciclo |
| `blob_maintenance_duration_ms` | Histogram | Duración del ciclo completo |

---

## 11. Reglas de negocio

| Referencia | Regla |
|------------|-------|
| RN-01 | Un blob sólo se borra si `FechaExpiracionBlob <= FechaActual` Y `Estado = Pendiente` Y `HoldActivo = false`. |
| RN-02 | Un blob con `maxRetentionDays = -1` nunca genera trigger y nunca es candidato a limpieza automática. |
| RN-03 | El borrado de un blob no elimina los metadatos ni los resultados del procesamiento en SQL Server; sólo pone `RutaBlobStorage` a `null`. |
| RN-04 | Si el blob ya no existe en storage, el trigger se marca como `Ejecutado` (operación idempotente). |
| RN-05 | Un error en el borrado de un blob individual no interrumpe el proceso del resto del lote. |
| RN-06 | Los triggers en estado `OmitidoHold` se reevalúan en cada ciclo; cuando `HoldActivo` vuelve a `false`, el trigger pasa a `Pendiente` en el siguiente ciclo. |
| RN-07 | El tamaño del lote (`batchSize`) está limitado para evitar timeouts en Durable Functions; el valor por defecto es 50 y el máximo recomendado es 500. |
| RN-08 | El endpoint `/api/BlobInventory` es de sólo lectura; no realiza ninguna modificación en el estado del sistema. |
| RN-09 | Si cambia la política de retención de una tipología, no se recalculan retroactivamente los triggers ya creados (sólo aplica a ingestas futuras). |

---

## 12. Criterios de aceptación globales

- [ ] Los documentos ingestados con una tipología que tiene `retentionPolicy.maxRetentionDays > 0` generan un registro en `BlobRetentionTrigger` con la fecha de expiración correcta.
- [ ] Los documentos ingestados con `maxRetentionDays = -1` (o sin política definida) no generan registro en `BlobRetentionTrigger`.
- [ ] El ciclo de limpieza se ejecuta automáticamente según el cron configurado.
- [ ] Tras el ciclo, los blobs expirados sin hold están eliminados del Blob Storage y `RutaBlobStorage = null` en la BD.
- [ ] Los documentos con `HoldActivo = true` no son borrados, independientemente de su fecha de expiración.
- [ ] Cada operación de borrado (exitosa o fallida) tiene su correspondiente entrada en la tabla `Auditoria`.
- [ ] El endpoint `GET /api/BlobInventory` devuelve datos coherentes con el estado de la BD en menos de 2 segundos.
- [ ] Los tests unitarios cubren: cálculo de `FechaExpiracionBlob`, lógica de hold, borrado exitoso y escenario blob-no-encontrado.
- [ ] El test de integración valida el ciclo completo contra Azurite y SQL Server local.

---

## 13. Dependencias y riesgos

### 13.1 Dependencias técnicas

| Dependencia | Naturaleza | Notas |
|-------------|------------|-------|
| `IBlobStorageService.DeleteDocumentAsync` | Interna | Ya implementado, sin uso actual |
| `TipologiaConfigLoader` | Interna | Debe extenderse para leer `retentionPolicy` |
| `DocumentIADbContext` / EF Core | Interna | Requiere nueva migración para `BlobRetentionTrigger` y `HoldActivo` |
| Azure Durable Functions (Timer) | Azure Platform | Disponible en el runtime actual |
| EP 7 – Protección de datos y GDPR | Interna | La acción `anonymize` de `actionOnExpiry` queda pendiente hasta completarse EP 7 |

### 13.2 Riesgos

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|-------------|---------|------------|
| Borrado masivo accidental por mala configuración de `maxRetentionDays` | Media | Alto | Validación en `RetentionPolicyService` (mínimo configurable en settings); dry-run mode opcional |
| Blobs huérfanos (ruta en BD pero blob borrado externamente) | Baja | Bajo | Regla RN-04: se trata como borrado exitoso |
| Crecimiento del volumen de triggers pendientes supera `batchSize` por ciclo | Media | Medio | Monitorizar métrica `blob_maintenance_deleted`; ajustar `batchSize` o frecuencia del cron |
| Cambio de estructura de `RutaBlobStorage` (formato ruta) | Baja | Medio | Usar siempre `IBlobStorageService` para las operaciones; no parsear la ruta directamente |

---

## 14. Glosario

| Término | Definición |
|---------|------------|
| **Blob** | Fichero binario almacenado en Azure Blob Storage / Azurite |
| **BlobRetentionTrigger** | Registro en BD que indica cuándo expira un blob y qué acción aplicar |
| **Hold** | Protección activa sobre un documento que impide su borrado aunque haya expirado |
| **Ciclo de limpieza** | Ejecución completa del motor desde el timer hasta el procesamiento del último lote |
| **Expiración** | Momento a partir del cual un blob es candidato a ser eliminado (`FechaExpiracionBlob <= NOW`) |
| **Retención indefinida** | Política con `maxRetentionDays = -1`; el blob no tiene fecha de expiración |
| **Lote (batch)** | Subconjunto de triggers expirados procesados en una invocación de `DeleteExpiredBlobsActivity` |
| **actionOnExpiry** | Acción a ejecutar cuando el blob expira: `delete` (borrado físico) o `anonymize` (EP 7) |
