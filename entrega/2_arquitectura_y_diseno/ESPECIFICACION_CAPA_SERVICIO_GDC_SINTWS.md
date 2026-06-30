# Especificacion Unificada GDC (SINTWS) — DocumentIA

> Version: 2.0 consolidada
> Fecha: 24/04/2026
> Alcance: Documento canonico de GDC para DocumentIA (sustituye y unifica la antigua especificacion tecnica y la guia operativa de integracion).

---

## 1. Objetivo y alcance

Este documento es la fuente de verdad para la integracion GDC en DocumentIA.

Incluye:
- Flujo funcional real en el orquestador.
- Especificacion tecnica de operaciones SOAP implementadas.
- Configuracion operativa y endpoints consumidos.
- Errores habituales y criterios de interpretacion del resultado GDC.

No incluye operaciones WSDL no implementadas en el codigo actual.

---

## 2. Flujo real implementado

La integracion soporta dos modos de entrada:

1. Entrada por `documento.content.base64`.
2. Entrada por `documento.objectIdGDC`.

Cuando se usa `objectIdGDC`, el sistema ejecuta un preflujo:

1. Fuerza `instrucciones.skipGDCUpload = true`.
2. Obtiene metadatos en GDC (`get` sin contenido).
3. Si existe checksum MD5 y no hay `skipDuplicateCheck`, verifica duplicado temprano en BD.
4. Descarga contenido (`get` con contenido) para hidratar el documento y continuar pipeline.

En el paso de subida GDC:
- `skipGDC = instrucciones.skipGDCUpload ?? tipologia.skipGDCUpload`.
- Si `skipGDC = true`, se omite subida intencionalmente y se devuelve:
  - `detalleEjecucion.gdc.exitoso = true`
  - `detalleEjecucion.gdc.mensaje = "Skipped"`

---

## 3. Arquitectura de componentes

Componentes principales:
- `src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs`
- `src/backend/DocumentIA.Functions/Activities/SubirGDCActivity.cs`
- `src/backend/DocumentIA.Functions/Activities/ObtenerMetadatosDocumentoGDCActivity.cs`
- `src/backend/DocumentIA.Functions/Activities/ObtenerDocumentoGDCActivity.cs`
- `src/backend/DocumentIA.Functions/Activities/VerificarDuplicadoPorMD5Activity.cs`
- `src/backend/DocumentIA.Functions/Services/GdcService.cs`
- `src/backend/DocumentIA.Functions/Services/ResilientGdcService.cs`
- `src/backend/DocumentIA.Core/Services/IGdcService.cs`

---

## 4. Operaciones SOAP implementadas (y solo estas)

### 4.1 `create`

Uso:
- Alta documental en GDC desde `SubirDocumentoAsync`.

Resultado esperado:
- `return` con `objectId` => `Exitoso=true`, `Mensaje="OK"`.
- SOAP Fault `DOC_OBJECT_EXISTS` => `Exitoso=true`, `YaExistia=true`, `Mensaje="AlreadyExists"`.

### 4.2 `searchEntities`

Uso:
- Deduplicacion previa a subida (`ConsultarDocumentoAsync`).

Estrategia:
- Si `GDC:ClaseExpediente` esta configurado: `expediente.id_expediente + checksum`.
- Si no lo esta: fallback por `checksum`.

### 4.3 `get`

Uso:
- Metadatos por objectId (`ObtenerMetadatosDocumentoAsync`, sin contenido).
- Descarga por objectId (`ObtenerDocumentoAsync`, con contenido Base64).

Campos leidos:
- `checksum`
- `nombre_fichero` / `nombre_documento`
- `content` (o fallback `Content`) para `dataSource` Base64.

---

## 5. Namespaces y contrato base SOAP

Namespaces usados:
- `http://services.api.sint.sareb.es/`
- `http://auth.model.api.sint.sareb.es`
- `http://data.model.api.sint.sareb.es`
- `http://field.data.model.api.sint.sareb.es`
- `http://fieldvalue.data.model.api.sint.sareb.es`

Formato de transporte:
- `Content-Type: application/soap+xml`
- Envolvente SOAP 1.2

Identity requerida:
- `applicationId`
- `username`
- `password`
- `nominalUser` (opcional)

---

## 6. Modelo de datos principal (`document`)

Campos usados por DocumentIA en `create`:
- `origen_documento`
- `entidad_origen`
- `proceso_carga`
- `publico`
- `servicer`
- `tipo_expediente`
- `serie`
- `tipo_documento`
- `subtipo_documento` (opcional)
- `nombre_documento`
- `nombre_fichero`
- `matricula_doc`
- `checksum`
- `expediente` (si `ClaseExpediente` configurado)
- `content` (campo configurable por `GDC:ContentFieldName`)

---

## 7. Configuracion

Clave principal:
- `GDC:Endpoint`

Claves relevantes:
- `GDC:TimeoutSeconds`
- `GDC:ApplicationId`
- `GDC:Username`
- `GDC:Password`
- `GDC:NominalUser`
- `GDC:OrigenDocumento`
- `GDC:EntidadOrigen`
- `GDC:ProcesoCarga`
- `GDC:Servicer`
- `GDC:TipoExpediente`
- `GDC:Publico`
- `GDC:DocumentTypeId`
- `GDC:ContentFieldName`
- `GDC:ClaseExpediente`
- `GDC:RepositoryId`
- `GDC:RepositoryName`
- `GDC:DefaultMatricula`
- `GDC:MaxRetries`
- `GDC:InitialDelayMs`
- `GDC:ExponentialBackoff`
- `GDC:CircuitBreakerFailures`
- `GDC:CircuitBreakerDurationMs`

---

## 8. Endpoints consumidos actualmente

El backend consume un endpoint configurable de `IDocService`:

- DEV: `https://srbwidd03.sareb.srb:8090/sintws/IDocService`
- DEV alternativo: `https://srbwidd03.sareb.srb:8443/sintws/IDocService`
- QA: `https://srbwidi03.sareb.srb:8443/sintws-162/IDocService`
- PRD: `https://srbwidp04.sareb.srb:8090/sintws/IDocService`
- PRD replica: `https://srbwidp05.sareb.srb:8090/sintws/IDocService`
- Mock local: `http://localhost:8888/sintws/IDocService`

Operaciones sobre ese endpoint:
- `searchEntities`
- `create`
- `get`

---

## 9. Resiliencia y timeout

Resiliencia por `ResilientGdcService`:
- Reintentos con backoff configurable.
- Circuit breaker configurable.

Timeout de orquestador:
- `SubirGDCActivity` con timeout de 120 segundos.
- Si vence timeout: `Mensaje="Timeout"`.

---

## 10. Interpretacion de estados GDC en salida

Estados habituales en `detalleEjecucion.gdc.mensaje`:
- `OK`
- `AlreadyExists`
- `Skipped`
- `Timeout`
- `SoapFault` / `Exception` / `UnknownResponse`

Regla importante:
- `Skipped` no implica error. Es omision intencional y se reporta como exitosa.

---

## 11. Relacion con otros documentos

- Contrato HTTP de entrada/salida:
  - `docs/contratos/CONTRATO_API_HTTP.md`
- Diseno tecnico general del pipeline:
  - `docs/03_DISENO_TECNICO_DETALLADO.md`
- Manual de activities:
  - `docs/not in use/MANUAL_ACTIVITIES_AZURE_FUNCTIONS.md`

Este documento es el canon para GDC en DocumentIA.
