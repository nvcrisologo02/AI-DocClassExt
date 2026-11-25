# Especificacion Unificada GDC (SINTWS) — DocumentIA

> Version: 2.0 consolidada
> Fecha: 24/04/2026 · Última revisión: 2026-11-25 (AB#101553, validación TLS del GDC contra la CA de SAREB; pendiente de despliegue, DEV primero). Revisiones previas: 2026-11-22 (AB#101447, contraste con `GdcService`, `ResilientGdcService`, `GdcSettings`, `SubirGDCActivity`, `ObtenerDocumentoGDCActivity` y `azure-pipelines.yml`)
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
4. Descarga el contenido (`get` con contenido), lo sube al blob de documentos y deja `BlobPath` y los hashes precalculados (`PreComputedSHA256`, `MD5`, tamaño) en el documento (blob-first, AB#100293); `Base64` solo queda si falla la subida. Si el checksum ya existe y no hay `forceReprocess`, intenta reutilizar la ejecución con la misma clave de intención (AB#100930, ADR-003).

En el paso de subida GDC:
- `skipGDC = instrucciones.skipGDCUpload ?? tipologia.skipGDCUpload`.
- Si `IdActivo` está vacío la actividad se marca omitida (`IdActivo no disponible`) y no sube.
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

Claves relevantes (defecto de `GdcSettings` · valor desplegado por el pipeline):
- `GDC:TimeoutSeconds` (30 · 60)
- `GDC:BypassSslValidation` (`false`; el pipeline lo fija a `false`. `true` es solo un interruptor de emergencia que acepta cualquier certificado; hasta el despliegue de AB#101553 los tres entornos siguen con `true`)
- `GDC:TrustedRootCertificate` (PEM opcional con la raíz y la emisora de confianza; sustituye al fichero sin desplegar)
- `GDC:TrustedRootCertificatePath` (`config/certs/sareb-ca-chain.pem`: raíz y emisora de SAREB, raíz válida hasta 2040-12-16; validación con `CustomRootTrust` y sin comprobación de revocación; si falta o está corrupto el host no arranca)
- `GDC:HttpBasicUsername` / `GDC:HttpBasicPassword` (Key Vault `GDC--HttpBasicUsername` / `GDC--HttpBasicPassword`; autenticación HTTP Basic del gateway, distinta del `Identity` SOAP)
- `GDC:ApplicationId` (`CKP1`), `GDC:DocumentTypeId` (`document`), `GDC:ContentFieldName` (`content` · `Content`), `GDC:OrigenDocumento` (`8878`), `GDC:ClaseExpediente` (`AI04`), `GDC:DefaultMatricula` (`OTROS_DOCUMENTOS` · `AI-99-SCXX-00`), `GDC:Servicer` / `GDC:EntidadOrigen` (`9999`), `GDC:ProcesoCarga` (`PC01`), `GDC:TipoExpediente` (`AI`), `GDC:Publico` (`verdadero`)
- Resiliencia (`ResilientGdcService`, envuelve `Consultar`, `Subir`, `ObtenerMetadatos` y `Obtener`): `GDC:MaxRetries` (3), `GDC:InitialDelayMs` (200), `GDC:ExponentialBackoff` (`true`), `GDC:CircuitBreakerFailures` (5), `GDC:CircuitBreakerDurationMs` (30000). Un reintento tras timeout en `create` puede devolver `AlreadyExists` en vez de duplicar.
- El timeout de 120 s de `SubirGDC` es el timer del orquestador, no una configuración.
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

Valores que despliega `azure-pipelines.yml` (`GDC_ENDPOINT`, revisado 2026-11-22):

- DEV: `https://srbwidd03.sareb.srb:8090/sintws/IDocService`
- PRE: `https://srbwidi03.sareb.srb:8090/sintws-162/IDocService`
- PRO: `https://srbwidp05.sareb.srb:8090/sintws/IDocService` (réplica; el primario `srbwidp04` no se usa desde la Function App)

No verificados en la revisión y conservados como referencia histórica: `https://srbwidd03.sareb.srb:8443/sintws/IDocService` (DEV alternativo), `https://srbwidi03.sareb.srb:8443/sintws-162/IDocService` (antiguo «QA») y el mock local `http://localhost:8888/sintws/IDocService`.

> Nota: en PROD el Function App tiene configurada la réplica `srbwidp05` (el primario es `srbwidp04`); en DEV el host es `srbwidd03`. El valor efectivo se resuelve por `GDC:Endpoint` (app setting `GDC__Endpoint`). Aviso operativo: en PROD existe un secreto de Key Vault `GDC--Endpoint` que, por el volcado masivo de secretos, tiene **precedencia** sobre el app setting.

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

Estados habituales en `detalleEjecucion.gdc.mensaje` (revisado 2026-11-22 contra `GdcService` y `SubirGDCActivity`):
- `OK`
- `AlreadyExists` (SOAP Fault `DOC_OBJECT_EXISTS` en `create`)
- `Documento ya existente` (deduplicación previa de `SubirGDCActivity`, `YaExistia = true`)
- `Skipped` (skip explícito)
- `IdActivo ausente, no se puede subir a GDC` y `Documento sin contenido...` (validaciones previas de la actividad)
- `Timeout` (timer de 120 s del orquestador)
- `Exception` / `UnknownResponse` / `HTTP <status>` / `ObjectId vacío`

Regla importante:
- `Skipped` no implica error. Es omision intencional y se reporta como exitosa.

---

## 11. Relacion con otros documentos

- Contrato HTTP de entrada/salida:
  - `docs/contratos/CONTRATO_API_HTTP.md`
- Diseno tecnico general del pipeline:
  - `docs/03_DISENO_TECNICO_DETALLADO.md`

Este documento es el canon para GDC en DocumentIA.
