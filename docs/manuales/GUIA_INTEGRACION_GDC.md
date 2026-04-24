# Guía de Integración con GDC (Gestor Documental Corporativo)

> **Versión**: 1.1 | **Fecha**: 18/03/2026  
> **Rama**: `feature/integracion-gdc-real` | **Último commit de referencia**: `756d3b3`  
> **SINTWS spec base**: "Integración con SINTWS v1.0, 07/01/2016 (VILT)" + "Capa de Servicios [SINTWS] Manual Int. y Admin. v7.0 (Nttdata, 29/04/2024)"

---

## Índice

1. [Visión funcional](#1-visión-funcional)
2. [Arquitectura de la integración](#2-arquitectura-de-la-integración)
3. [Flujo de proceso extremo a extremo](#3-flujo-de-proceso-extremo-a-extremo)
4. [Operaciones SOAP implementadas](#4-operaciones-soap-implementadas)
5. [Modelo de campos GDC (entidad document)](#5-modelo-de-campos-gdc-entidad-document)
6. [Configuración](#6-configuración)
7. [Resiliencia: reintentos y circuit breaker](#7-resiliencia-reintentos-y-circuit-breaker)
8. [Entornos y endpoints](#8-entornos-y-endpoints)
9. [Servidor mock para desarrollo](#9-servidor-mock-para-desarrollo)
10. [Preguntas pendientes con Sistemas SAREB](#10-preguntas-pendientes-con-sistemas-sareb)
11. [Errores habituales y soluciones](#11-errores-habituales-y-soluciones)

---

## 1. Visión funcional

El **GDC (Gestor Documental Corporativo)** de SAREB es un sistema Opentext Content Server (OTCS) expuesto a través del WebService **SINTWS** (`IDocService`). Permite almacenar, buscar y gestionar documentos asociados a activos inmobiliarios y financieros.

### ¿Qué hace esta integración?

Cuando el proceso de clasificación de DocumentIA finaliza, el documento procesado se **sube automáticamente al GDC** siempre que se cumplan dos condiciones:

1. Se dispone de un `IdActivo` válido (identificador del activo en SAREB).
2. El campo `SkipGDCUpload` efectivo **no** es `true`. Este campo puede venir de dos fuentes con la siguiente precedencia:
   - **`Instrucciones.SkipGDCUpload`** (en la petición): si viene informado (`true` o `false`) tiene prioridad absoluta.
   - **`skipGDCUpload` de la tipología** (en el JSON de configuración): si la petición no lo especifica (`null`/omitido), se usa el valor configurado para la tipología detectada.
   - Por defecto: `nota-simple` tiene `skipGDCUpload: false` (sí sube); `tasacion` y `resumen-documental` tienen `skipGDCUpload: true` (no suben).

Antes de subir, se realiza una **consulta previa** al GDC para evitar duplicados: si el documento ya existe (mismo `expediente.id_expediente` + mismo `checksum`), la operación devuelve éxito indicando `YaExistia = true`, sin volver a crear el documento.

### Resultado que devuelve la integración

El campo `salida.DetalleEjecucion.GDC` del contrato de salida contiene:

| Campo | Tipo | Descripción |
|---|---|---|
| `Exitoso` | bool | `true` si el documento quedó en GDC (nuevo o ya existía) |
| `ObjectId` | string | ID de nodo en OTCS del documento creado |
| `YaExistia` | bool | `true` si el documento ya estaba en GDC |
| `Mensaje` | string | `"OK"`, `"AlreadyExists"`, `"Timeout"`, etc. |
| `Intentos` | int | Número de reintentos realizados |
| `DuracionMs` | int | Milisegundos que tardó la llamada SOAP |
| `ErrorDetalle` | string | XML raw de respuesta o traza de excepción en caso de error |

---

## 2. Arquitectura de la integración

```
DocumentProcessOrchestrator (Durable Function)
    │
    ├─ [Paso 7] CallActivityAsync("SubirGDCActivity", input)
    │                │
    │                ▼
    │         SubirGDCActivity
    │           ├─ ConsultarDocumentoAsync()  ──► searchEntities (WSDL)
    │           └─ SubirDocumentoAsync()      ──► create (WSDL)
    │                │
    │                ▼
    │         IGdcService (inyectado como ResilientGdcService)
    │           └─ ResilientGdcService  ← decorador: reintentos + circuit breaker
    │               └─ GdcService      ← SOAP raw sobre HttpClient named "GDC"
    │                       │
    │                       ▼
    │               SINTWS WebService (IDocService)
    │               https://srbwidd03.sareb.srb:8090/sintws/IDocService?wsdl
    │
    └─ [Timeout 120s] → ResultadoGDC { Exitoso=false, Mensaje="Timeout" }
```

### Componentes clave

| Componente | Ruta |
|---|---|
| `GdcService` | `src/backend/DocumentIA.Functions/Services/GdcService.cs` |
| `ResilientGdcService` | `src/backend/DocumentIA.Functions/Services/ResilientGdcService.cs` |
| `SubirGDCActivity` | `src/backend/DocumentIA.Functions/Activities/SubirGDCActivity.cs` |
| `GdcSettings` | `src/backend/DocumentIA.Core/Configuration/GdcSettings.cs` |
| DI / HttpClient setup | `src/backend/DocumentIA.Functions/Program.cs` (sección GDC) |

---

## 3. Flujo de proceso extremo a extremo

```
[Entrada]
  ├─ entrada.Trazabilidad.IdActivo   (puede venir del ingest o resolverse via plugin)
  ├─ entrada.Documento.Content.Base64 (directo o hidratado desde `documento.objectIdGDC`)
  ├─ entrada.Documento.Name
  └─ entrada.Instrucciones.SkipGDCUpload (null/omitido = usar config de tipología)

[Orquestador — Paso 7]
  │  skipGDC = Instrucciones.SkipGDCUpload ?? tipologiaResuelta.SkipGDCUpload
  │  Si skipGDC == true → omitir subida (log: fuente = "instrucciones" o "config-tipologia")
  │
  └─ Si skipGDC == false: llamar SubirGDCActivity

[SubirGDCActivity.Run()]
  │
  ├─ 1. Resolver matrícula (matricula_doc):
  │      a) input.Matricula si viene informada
  │      b) TipologiaMGDCMatricula de la config de la tipología
  │      c) GdcSettings.DefaultMatricula ("OTROS_DOCUMENTOS" por defecto)
  │
  ├─ 2. Validar IdActivo → si vacío: devuelve error sin llamar al GDC
  │
  ├─ 3. ConsultarDocumentoAsync(idActivo, checksum)
  │      → searchEntities SOAP (EntityExpression IN expediente.id_expediente + checksum)
  │      → Si existe: devuelve YaExistia=true, Exitoso=true → FIN
  │
  └─ 4. SubirDocumentoAsync(input)
         → create SOAP (campos SINTWS v4.0+)
         ├─ createResponse.return = objectId → Exitoso=true, ObjectId=<id>
         ├─ SOAP Fault errorCode=DOC_OBJECT_EXISTS → YaExistia=true, Exitoso=true
         └─ Otro Fault / HTTP error → Exitoso=false, ErrorDetalle=<XML>
```

---

## 4. Operaciones SOAP implementadas

### 4.1 `create` — Subida de documento

Operación WSDL: `create(Identity arg0, Entity arg1) → string (objectId)`

**Namespaces**:

| Prefijo | URI |
|---|---|
| `ns1` | `http://services.api.sint.sareb.es/` |
| `ns0` | `http://auth.model.api.sint.sareb.es` |
| `ns2` | `http://data.model.api.sint.sareb.es` |
| `ns3` | `http://field.data.model.api.sint.sareb.es` |
| `ns4` | `http://fieldvalue.data.model.api.sint.sareb.es` |

**Estructura de petición** (campos SINTWS v4.0+, emitidos en orden):

```xml
<ns1:create xmlns:ns1="http://services.api.sint.sareb.es/"
            xmlns:ns0="http://auth.model.api.sint.sareb.es"
            xmlns:ns2="http://data.model.api.sint.sareb.es"
            xmlns:ns3="http://field.data.model.api.sint.sareb.es"
            xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

  <!-- arg0: Identidad de la aplicación integradora -->
  <ns1:arg0 xsi:type="ns0:Identity">
    <ns0:applicationId>{ApplicationId}</ns0:applicationId>
    <ns0:nominalUser>{NominalUser}</ns0:nominalUser>
    <ns0:username>{Username}</ns0:username>
  </ns1:arg0>

  <!-- arg1: Entidad documento con sus metadatos y contenido -->
  <ns1:arg1 xsi:type="ns2:Entity">
    <ns2:typeId>document</ns2:typeId>
    <ns2:fields>

      <!-- origen_documento — obligatorio desde SINTWS v4.0. Omitido si OrigenDocumento está vacío -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>origen_documento</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{GdcSettings.OrigenDocumento}</ns4:value>
        </ns3:fieldValue>
      </ns3:Field>

      <!-- entidad_origen — omitido si EntidadOrigen está vacío -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>entidad_origen</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{GdcSettings.EntidadOrigen}</ns4:value>   <!-- ej: "9999" -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- proceso_carga — omitido si ProcesoCarga está vacío -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>proceso_carga</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{GdcSettings.ProcesoCarga}</ns4:value>   <!-- ej: "PC01" -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- publico — siempre presente; default "verdadero" (string, no booleano SOAP) -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>publico</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{GdcSettings.Publico}</ns4:value>   <!-- "verdadero" | "falso" -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- servicer — omitido si Servicer está vacío -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>servicer</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{GdcSettings.Servicer}</ns4:value>   <!-- ej: "9999" -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- tipo_expediente — omitido si TipoExpediente está vacío; default "AI" -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>tipo_expediente</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{GdcSettings.TipoExpediente}</ns4:value>   <!-- "AI" = Activo Inmobiliario -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- serie — de tipología (gdcSerie). Omitido si vacío -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>serie</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{input.Serie}</ns4:value>   <!-- ej: "AI01" para nota simple -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- tipo_documento — de tipología (gdcTipoDocumento). Omitido si vacío -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>tipo_documento</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{input.TipoDocumento}</ns4:value>   <!-- ej: "NOTS" para nota simple -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- subtipo_documento — de tipología (gdcSubtipoDocumento). Omitido si vacío -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>subtipo_documento</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{input.SubtipoDocumento}</ns4:value>   <!-- ej: "NOTS01" para nota simple -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- nombre_documento — nombre lógico del doc; si no se especifica, usa nombre_fichero -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>nombre_documento</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>documento.pdf</ns4:value>
        </ns3:fieldValue>
      </ns3:Field>

      <!-- nombre_fichero — nombre real del fichero en OTCS (NO usar nombre_archivo) -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>nombre_fichero</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>documento.pdf</ns4:value>
        </ns3:fieldValue>
      </ns3:Field>

      <!-- matricula_doc — matrícula resuelta (tipología → DefaultMatricula) -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>matricula_doc</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{input.Matricula}</ns4:value>   <!-- ej: "AI-01-NOTS-01" -->
        </ns3:fieldValue>
      </ns3:Field>

      <!-- checksum — hash MD5 del contenido; usado también para deduplicación -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>checksum</ns3:name>
        <ns3:fieldValue xsi:type="ns4:StringFieldValue">
          <ns4:value>{input.MD5}</ns4:value>
        </ns3:fieldValue>
      </ns3:Field>

      <!-- expediente: ubica el doc en la carpeta OTCS del activo.
           Solo se incluye si ClaseExpediente está configurado. -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>expediente</ns3:name>
        <ns3:fieldValue xsi:type="ns4:EntityFieldValue">
          <ns4:extraFields>
            <ns3:Field xsi:type="ns3:SingleField">
              <ns3:name>id_expediente</ns3:name>
              <ns3:fieldValue xsi:type="ns4:StringFieldValue">
                <ns4:value>{IdActivo}</ns4:value>
              </ns3:fieldValue>
            </ns3:Field>
            <ns3:Field xsi:type="ns3:SingleField">
              <ns3:name>clase_expediente</ns3:name>
              <ns3:fieldValue xsi:type="ns4:StringFieldValue">
                <ns4:value>{ClaseExpediente}</ns4:value>  <!-- ej: "AI", "AAII" -->
              </ns3:fieldValue>
            </ns3:Field>
          </ns4:extraFields>
        </ns3:fieldValue>
      </ns3:Field>

      <!-- Contenido binario del fichero (Base64 en el campo configurable) -->
      <ns3:Field xsi:type="ns3:SingleField">
        <ns3:name>content</ns3:name>
        <ns3:fieldValue xsi:type="ns4:FileContentFieldValue">
          <ns4:dataSource>{Base64ContenidoFichero}</ns4:dataSource>
        </ns3:fieldValue>
      </ns3:Field>

    </ns2:fields>
  </ns1:arg1>
</ns1:create>
```

**Respuesta exitosa**:

```xml
<ns1:createResponse xmlns:ns1="http://services.api.sint.sareb.es/">
  <ns1:return>290338</ns1:return>   <!-- objectId del nodo creado en OTCS -->
</ns1:createResponse>
```

**Respuesta duplicado** (SOAP Fault):

```xml
<soap:Fault>
  <faultcode>soap:Server</faultcode>
  <faultstring>Document already exists</faultstring>
  <detail>
    <ServiceException xmlns="http://exceptions.model.api.sint.sareb.es">
      <errorCode>DOC_OBJECT_EXISTS</errorCode>
    </ServiceException>
  </detail>
</soap:Fault>
```

---

### 4.2 `searchEntities` — Consulta de existencia

Operación WSDL: `searchEntities(Identity arg0, Query arg1, List<DocRepository> arg2) → SearchResult`

Busca documentos para detectar duplicados antes de la subida. La estrategia de búsqueda es **adaptativa** según si `GDC:ClaseExpediente` está configurado:

| `ClaseExpediente` | Filtro usado | Cuándo se aplica |
|---|---|---|
| Configurado | `EntityExpression IN expediente.id_expediente AND EQUALS checksum` | Dedup exacto por activo + contenido |
| **Vacío** | `FieldExpression EQUALS checksum` | Fallback cuando el `expediente` no se almacena en create; dedup por contenido únicamente |

> **Importante**: si `ClaseExpediente` no está configurado, el campo `expediente` **no se incluye** en el `create`. En ese caso, buscar por `expediente.id_expediente` devolvería siempre 0 resultados (falso "no existe") y el create siguiente fallaría con `DOC_OBJECT_EXISTS`. El filtro por `checksum` evita ese comportamiento.

> **Nota**: el campo de hash en SINTWS v4.0 es `checksum` (no `md5`). El operador para buscar en entidades relacionadas es `EntityExpression IN` (no `FieldExpression EQUAL` sobre campo plano).

**Estructura de petición relevante**:

```xml
<ns1:searchEntities xmlns:ns1="http://services.api.sint.sareb.es/"
                    xmlns:ns0="http://auth.model.api.sint.sareb.es"
                    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

  <ns1:arg0 xsi:type="ns0:Identity">
    <!-- igual que en create -->
  </ns1:arg0>

  <ns1:arg1 xsi:type="ns2:Query" xmlns:ns2="http://search.model.api.sint.sareb.es"
                                  xmlns:ns3="http://data.model.api.sint.sareb.es">
    <ns2:entityTypeId>document</ns2:entityTypeId>
    <ns2:filter xsi:type="ns2:SetExpression">
      <ns2:expressions>
        <!-- Busca por id_expediente del activo (expediente = carpeta OTCS del activo) -->
        <ns2:Expression xsi:type="ns2:EntityExpression">
          <ns2:condition>IN</ns2:condition>
          <ns2:entityName>expediente</ns2:entityName>
          <ns2:fieldName>id_expediente</ns2:fieldName>
          <ns2:value xsi:type="ns2:StringValueList">
            <ns2:values><ns1:string>{IdActivo}</ns1:string></ns2:values>
          </ns2:value>
        </ns2:Expression>
        <!-- Busca por checksum (hash MD5 del fichero) -->
        <ns2:Expression xsi:type="ns2:FieldExpression">
          <ns2:condition>EQUALS</ns2:condition>
          <ns2:fieldName>checksum</ns2:fieldName>
          <ns2:value xsi:type="ns2:StringValue">
            <ns2:value>{MD5}</ns2:value>
          </ns2:value>
        </ns2:Expression>
      </ns2:expressions>
      <ns2:operator>AND</ns2:operator>
    </ns2:filter>
    <ns2:firstResultIndex>1</ns2:firstResultIndex>
    <ns2:maxResults>1</ns2:maxResults>
    <ns2:orderingField xsi:type="ns2:OrderingField">
      <ns2:ascending>false</ns2:ascending>
      <ns2:fieldName>create_date</ns2:fieldName>
    </ns2:orderingField>
    <ns2:resultsProfile xsi:type="ns3:EntityProfile">
      <ns3:fieldNames>
        <ns1:string>checksum</ns1:string>
        <ns1:string>nombre_fichero</ns1:string>
      </ns3:fieldNames>
      <ns3:ignoreContent>true</ns3:ignoreContent>
      <ns3:ignoreMetadata>false</ns3:ignoreMetadata>
    </ns2:resultsProfile>
  </ns1:arg1>

  <!-- arg2: repositorio donde buscar. Si RepositoryId está vacío → <ns1:arg2/> (todos) -->
  <ns1:arg2>
    <ns2:DocRepository xmlns:ns2="http://doc.model.api.sint.sareb.es"
                       xsi:type="ns2:DocRepository">
      <ns2:id>{GdcSettings.RepositoryId}</ns2:id>
      <ns2:name>{GdcSettings.RepositoryName}</ns2:name>
    </ns2:DocRepository>
  </ns1:arg2>
</ns1:searchEntities>
```

**Interpretación de respuesta** (en `ConsultarDocumentoAsync`): se busca cualquiera de los tags `ObjectId`, `objectId`, `object_id`, `entityId`, `id` con valor no vacío. Si se encuentra → documento existe.

---

## 5. Modelo de campos GDC (entidad document)

Campos que envía esta integración en la operación `create` (SINTWS v4.0+):

| Campo OTCS | Fuente en código | Obligatorio | Notas |
|---|---|---|---|
| `origen_documento` | `GdcSettings.OrigenDocumento` | ⚠ Recomendado | Identificador del sistema integrador. Se omite si el setting está vacío (⚠ error en producción). |
| `entidad_origen` | `GdcSettings.EntidadOrigen` | ⚠ Recomendado | Código de entidad/servicer origen. Normalmente igual a `Servicer`. Se omite si vacío. |
| `proceso_carga` | `GdcSettings.ProcesoCarga` | ⚠ Recomendado | Código del proceso de carga (ej: `"PC01"`, `"CKP1"`). Se omite si vacío. |
| `publico` | `GdcSettings.Publico` | ✅ Sí | Visibilidad del doc. String `"verdadero"` o `"falso"` (no booleano SOAP). Default: `"verdadero"`. |
| `servicer` | `GdcSettings.Servicer` | ⚠ Recomendado | Código del servicer (ej: `"9999"` en DEV). Se omite si vacío. |
| `tipo_expediente` | `GdcSettings.TipoExpediente` | ⚠ Recomendado | Tipo de expediente GDC. Default: `"AI"`. Se omite si vacío. |
| `serie` | `SubirGDCInput.Serie` ← `gdcSerie` de tipología | ⚠ Recomendado | Serie documental GDC (ej: `"AI01"` para nota simple). Se omite si vacío. |
| `tipo_documento` | `SubirGDCInput.TipoDocumento` ← `gdcTipoDocumento` de tipología | ⚠ Recomendado | Tipo de documento en catálogo GDC (ej: `"NOTS"`). Se omite si vacío. |
| `subtipo_documento` | `SubirGDCInput.SubtipoDocumento` ← `gdcSubtipoDocumento` de tipología | ⚪ Opcional | Subtipo GDC (ej: `"NOTS01"`). Se omite si vacío. |
| `nombre_documento` | `SubirGDCInput.NombreDocumento` ?? `NombreArchivo` | ✅ Sí | Nombre lógico del documento en OTCS. |
| `nombre_fichero` | `SubirGDCInput.NombreArchivo` | ✅ Sí | Nombre real del fichero en OTCS. **El campo es `nombre_fichero`, no `nombre_archivo`.** |
| `matricula_doc` | `SubirGDCInput.Matricula` → tipología (`tipologiaMGDCMatricula`) → `DefaultMatricula` | ✅ Sí | Matrícula del activo. Default: `"OTROS_DOCUMENTOS"`. |
| `checksum` | `SubirGDCInput.MD5` | ✅ Sí | Hash MD5 del contenido binario. Usado también para detección de duplicados en `searchEntities`. |
| `expediente` *(EntityFieldValue)* | `IdActivo` + `GdcSettings.ClaseExpediente` | ⚠ Recomendado | Ubica el documento en la carpeta OTCS del activo. Solo se incluye si `ClaseExpediente` está configurado. **Sistemas debe proporcionar el valor** (ej: `"AI"`, `"AAII"`). |
| `content` *(FileContentFieldValue)* | `SubirGDCInput.ContenidoBase64` | ✅ Sí | Contenido binario del fichero en Base64. Nombre del campo configurable vía `ContentFieldName`. |

---

## 6. Configuración

Todas las claves se leen de la sección `GDC` del fichero de configuración (en local: `local.settings.json`, clave plana `GDC:*`).

### Parámetros completos

| Clave | Tipo | Default | Descripción |
|---|---|---|---|
| `GDC:Endpoint` | string | — | URL del WSDL de SINTWS. **Obligatorio.** |
| `GDC:TimeoutSeconds` | int | `30` | Timeout HTTP total por llamada SOAP. |
| `GDC:ApplicationId` | string | — | Identificador de aplicación en GDC. **Requiere credencial de Sistemas.** |
| `GDC:Username` | string | — | Usuario técnico de la aplicación. **Requiere credencial de Sistemas.** |
| `GDC:Password` | string | — | Contraseña del usuario técnico. **Requiere credencial de Sistemas.** |
| `GDC:NominalUser` | string | — | Usuario nominal (opcional según spec). Puede dejarse vacío. |
| `GDC:HttpBasicUsername` | string | — | Usuario para HTTP Basic Auth en el transporte (si el gateway lo requiere). |
| `GDC:HttpBasicPassword` | string | — | Contraseña para HTTP Basic Auth. |
| `GDC:OrigenDocumento` | string | — | Valor del campo `origen_documento` (SINTWS v4.0). Normalmente el mismo que `ApplicationId`. |
| `GDC:EntidadOrigen` | string | — | Código de entidad/servicer origen (campo `entidad_origen`). **Confirmar con Sistemas.** |
| `GDC:ProcesoCarga` | string | — | Código del proceso de carga (campo `proceso_carga`, ej: `"PC01"`). **Confirmar con Sistemas.** |
| `GDC:Servicer` | string | — | Código del servicer (campo `servicer`, ej: `"9999"` en DEV). **Confirmar con Sistemas.** |
| `GDC:TipoExpediente` | string | `"AI"` | Tipo de expediente GDC (`"AI"` = Activo Inmobiliario, `"AF"` = Activo Financiero). |
| `GDC:Publico` | string | `"verdadero"` | Visibilidad del documento. `"verdadero"` o `"falso"` (string, no booleano SOAP). |
| `GDC:DocumentTypeId` | string | `"document"` | Tipo de entidad GDC a crear. No cambiar salvo indicación de Sistemas. |
| `GDC:ContentFieldName` | string | `"content"` | Nombre del campo de contenido binario en el schema de OTCS. Confirmar con Sistemas. |
| `GDC:ClaseExpediente` | string | — | Clase del expediente/carpeta en OTCS (ej: `"AI"`). Si vacío, no se añade campo `expediente`. **Confirmar con Sistemas.** |
| `GDC:RepositoryId` | string | — | ID numérico del repositorio GDC para `searchEntities`. Si vacío, busca en todos. **Confirmar con Sistemas.** |
| `GDC:RepositoryName` | string | — | Nombre del repositorio (ej: `"01 AAII-Activos Inmobiliarios"`). |
| `GDC:DefaultMatricula` | string | `"OTROS_DOCUMENTOS"` | Matrícula (`matricula_doc`) cuando ni el input ni la tipología la definen. |
| `GDC:MaxRetries` | int | `3` | Número máximo de reintentos ante fallos transitorios. |
| `GDC:InitialDelayMs` | int | `200` | Delay base para el primer reintento (ms). Con backoff exponencial: 200ms → 400ms → 800ms. |
| `GDC:ExponentialBackoff` | bool | `true` | Si `true`, el delay se dobla en cada reintento. |
| `GDC:CircuitBreakerFailures` | int | `5` | Número de fallos consecutivos para abrir el circuit breaker. |
| `GDC:CircuitBreakerDurationMs` | int | `30000` | Tiempo (ms) que el circuit breaker permanece abierto antes del intento de recuperación. |

### Ejemplo mínimo para DEV

```json
"GDC:Endpoint": "https://srbwidd03.sareb.srb:8090/sintws/IDocService",
"GDC:TimeoutSeconds": "60",
"GDC:ApplicationId": "DOC_IA_MVP",
"GDC:Username": "<usuario_técnico>",
"GDC:Password": "<contraseña>",
"GDC:NominalUser": "",
"GDC:OrigenDocumento": "DOC_IA_MVP",
"GDC:EntidadOrigen": "<confirmar con Sistemas>",
"GDC:ProcesoCarga": "<confirmar con Sistemas>",
"GDC:Servicer": "<confirmar con Sistemas>",
"GDC:TipoExpediente": "AI",
"GDC:Publico": "verdadero",
"GDC:ClaseExpediente": "<confirmar con Sistemas>",
"GDC:RepositoryId": "<confirmar con Sistemas>",
"GDC:RepositoryName": "<confirmar con Sistemas>"
```

> **Nota de seguridad**: `Username` y `NominalUser` son credenciales. En producción/QA deben inyectarse vía Azure Key Vault o variables de entorno secretas, **nunca** commiteadas en `local.settings.json`.

---

## 7. Resiliencia: reintentos y circuit breaker

La clase `ResilientGdcService` envuelve a `GdcService` añadiendo dos mecanismos:

### Reintentos con backoff exponencial

```
Intento 1: fallo → espera 200ms
Intento 2: fallo → espera 400ms
Intento 3: fallo → espera 800ms
Intento 4: lanza excepción
```

El número de reintentos y el delay base son configurables (`MaxRetries`, `InitialDelayMs`).

### Circuit breaker

Si se producen `CircuitBreakerFailures` (por defecto 5) fallos **consecutivos**:
- El circuit breaker se **abre**: las llamadas siguientes fallan inmediatamente con `InvalidOperationException("GDC circuit breaker abierto")`.
- Tras `CircuitBreakerDurationMs` (por defecto 30 segundos), pasa a estado **half-open**: deja pasar un intento de prueba.
- Si el intento de prueba tiene éxito, el circuit breaker se **cierra** y el contador se resetea.

### Timeout del orquestador

El `DocumentProcessOrchestrator` impone un timeout adicional de **120 segundos** sobre la actividad completa (Durable Functions timer). Si se supera, devuelve `ResultadoGDC { Exitoso=false, Mensaje="Timeout" }` sin afectar al resto del proceso.

---

## 8. Entornos y endpoints

| Entorno | URL IDocService |
|---|---|
| **Desarrollo (DEV)** | `https://srbwidd03.sareb.srb:8443/sintws/IDocService?wsdl` |
| **DEV (puerto alternativo)** | `https://srbwidd03.sareb.srb:8090/sintws/IDocService?wsdl` *(configurado actualmente)* |
| **Calidad (QA)** | `https://srbwidi03.sareb.srb:8443/sintws-162/IDocService?wsdl` |
| **Producción** | `https://srbwidp04.sareb.srb:8090/sintws/IDocService?wsdl` |
| **Producción (réplica)** | `https://srbwidp05.sareb.srb:8090/sintws/IDocService?wsdl` |

> El endpoint DEV usa un certificado SSL autofirmado. En modo `Development` el HttpClient está configurado para ignorar la validación SSL (`DangerousAcceptAnyServerCertificateValidator`). **Esto NO se aplica en QA ni Producción.**

### Repositorios conocidos

Los repositorios disponibles pueden consultarse con `getRepositories()` (`IDocProvider`). Los valores de los ejemplos de la spec son:

| ID | Nombre |
|---|---|
| `69466` | `01 AAII-Activos Inmobiliarios` |
| `70064` | `04 ENT-Entidad` |
| `70394` | `05 AAFF-Activos Financieros` |

> Estos IDs corresponden al entorno de la spec (2016). **Confirmar los IDs reales del entorno DEV/QA/PRD con Sistemas.**

---

## 9. Servidor mock para desarrollo

Para desarrollo y pruebas sin conectividad al GDC real existe un servidor mock Python:

**Ruta**: `scripts/Mock Servers/mock-gdc-server.py`

### Arranque

```powershell
# Arrancar todos los mocks (GDC + GDC SOAP + otros)
powershell -ExecutionPolicy Bypass -File "scripts\Mock Servers\start-mock-servers.ps1"

# O parar
powershell -ExecutionPolicy Bypass -File "scripts\Mock Servers\stop-mock-servers.ps1"
```

### Comportamiento del mock GDC

| Operación SOAP | Comportamiento |
|---|---|
| `searchEntities` | Si `id_activo` o `md5` contienen `"exists"` → devuelve `entityId=GDC-<hash>` |
| `create` | Si `nombre_fichero` o `id_activo` contienen `"already"` → SOAP Fault `DOC_OBJECT_EXISTS` |
| `create` | En cualquier otro caso → `<ns1:return>GDC-<hash></ns1:return>` |

### Cambiar la URL al mock

En `local.settings.json`, temporalmente:

```json
"GDC:Endpoint": "http://localhost:8888/sintws/IDocService"
```

---

## 10. Preguntas pendientes con Sistemas SAREB

Los siguientes parámetros **deben ser confirmados por el equipo de Sistemas** antes de la integración con DEV/QA real:

| # | Parámetro | Setting | Estado |
|---|---|---|---|
| 1 | **Credenciales**: `ApplicationId`, `Username`, `Password` | `GDC:ApplicationId`, `GDC:Username`, `GDC:Password` | ⏳ Pendiente |
| 2 | **`clase_expediente`** del tipo de activo inmobiliario (ej: `"AI"`, `"AAII"`, otro) | `GDC:ClaseExpediente` | ⏳ Pendiente |
| 3 | **Repositorio** donde subir documentos de activos: ID + nombre | `GDC:RepositoryId`, `GDC:RepositoryName` | ⏳ Pendiente |
| 4 | **`ContentFieldName`**: confirmar si el campo de contenido binario se llama `"content"` o diferente | `GDC:ContentFieldName` | ⏳ Pendiente |
| 5 | **`NominalUser`**: si aplica para esta integración | `GDC:NominalUser` | ⏳ Pendiente |
| 6 | **Endpoint correcto** para DEV y QA (puerto 8090 vs 8443) | `GDC:Endpoint` | ⏳ Confirmar |
| 7 | **`EntidadOrigen`**: código de entidad origen del documento | `GDC:EntidadOrigen` | ⏳ Pendiente |
| 8 | **`ProcesoCarga`**: código del proceso de carga (ej: `"PC01"`, `"CKP1"`) | `GDC:ProcesoCarga` | ⏳ Pendiente |
| 9 | **`Servicer`**: código del servicer (ej: `"9999"` en DEV) | `GDC:Servicer` | ⏳ Pendiente |
| 10 | **Series y tipos por tipología**: confirmar `gdcSerie`, `gdcTipoDocumento`, `gdcSubtipoDocumento` para cada tipología | JSON de tipología | ⏳ Pendiente — provisionales: `AI01/NOTS/NOTS01` para nota simple |

---

## 11. Errores habituales y soluciones

### `GDC circuit breaker abierto`

El circuit breaker se ha abierto debido a 5+ fallos consecutivos. Esperar 30 segundos o reiniciar la instancia de la Function App. Revisar logs para la causa raíz (credenciales, red, endpoint incorrecto).

### `Timeout` en DetalleEjecucion.GDC

La actividad `SubirGDCActivity` tardó más de 120 segundos. Posibles causas: red lenta, endpoint incorrecto, GDC saturado. Aumentar `GDC:TimeoutSeconds` si el GDC tarda habitualmente más de 60 segundos.

### SOAP Fault sin `DOC_OBJECT_EXISTS`

Revisar `ErrorDetalle` (contiene el XML raw del Fault) para ver el `errorCode` y `errorMessage` del servidor OTCS. Causas frecuentes:
- `INVALID_CREDENTIALS`: `ApplicationId` o `Username` incorrectos.
- `FIELD_REQUIRED`: falta un campo obligatorio (posiblemente `origen_documento` o `expediente`).
- `INVALID_ENTITY_TYPE`: `DocumentTypeId` incorrecto (debe ser `"document"` salvo indicación contraria).

### `Exitoso=false, Mensaje="UnknownResponse"`

El GDC devolvió HTTP 200 pero el XML no contiene ni `<Fault>` ni `<ns1:return>`. Puede indicar un cambio en la versión del WSDL o un respuesta inesperada. Revisar `ErrorDetalle`.

### SSL: `The SSL connection could not be established`

El endpoint DEV usa certificado autofirmado. Verificar que la Function App está corriendo en modo `Development` (variable `ASPNETCORE_ENVIRONMENT=Development` o equivalente). En Docker / contenedor, puede ser necesario añadir el certificado de la CA de SAREB.

### `IdActivo ausente, no se puede subir a GDC`

El documento llegó al paso 7 sin `IdActivo`. Revisar que el plugin de resolución de activo (ej: `ActivoEnrichment`) está activo y configurado para la tipología, o que el campo `Trazabilidad.IdActivo` se informa correctamente en la llamada de ingest.
