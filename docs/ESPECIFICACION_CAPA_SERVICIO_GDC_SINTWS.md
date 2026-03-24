# Especificación de la Capa de Servicio GDC (SINTWS — IDocService)

> **Versión**: 1.0 | **Fecha**: 24/03/2026  
> **Base**: WSDL `gdc.wsdl`, proyecto SoapUI `DESARROLLO (16.2)`, Manual SINTWS v7.0  
> **Objetivo**: Referencia técnica completa para invocar el WebService SOAP de GDC, con énfasis en `create` (subida de documento vinculada a un IdActivo/IdExpediente) y `searchEntities` (consulta previa de duplicados). Sirve de guía para validar nuestro desarrollo en `GdcService.cs`.

---

## Índice

1. [Información general del servicio](#1-información-general-del-servicio)
2. [Autenticación: bloque Identity](#2-autenticación-bloque-identity)
3. [Modelo de datos común](#3-modelo-de-datos-común)
4. [**`create` — Crear documento en GDC**](#4-create--crear-documento-en-gdc)
5. [**`searchEntities` — Buscar documentos**](#5-searchentities--buscar-documentos)
6. [`get` — Recuperar documento por ObjectId](#6-get--recuperar-documento-por-objectid)
7. [`update` — Actualizar metadatos](#7-update--actualizar-metadatos)
8. [`delete` — Eliminar documento](#8-delete--eliminar-documento)
9. [`bajaLogica` — Baja lógica](#9-bajalogica--baja-lógica)
10. [`moveDocument` — Mover documento entre expedientes](#10-movedocument--mover-documento-entre-expedientes)
11. [`addDocumentVinculacion` — Añadir vinculación](#11-adddocumentvinculacion--añadir-vinculación)
12. [`addEntityField` — Añadir campo a entidad](#12-addentityfield--añadir-campo-a-entidad)
13. [`getRepositories` — Listar repositorios](#13-getrepositories--listar-repositorios)
14. [`resolveIDSarebValues` — Resolver IDSareb](#14-resolveidsarebvalues--resolver-idsareb)
15. [`validateActivo` — Validar activo](#15-validateactivo--validar-activo)
16. [`getZipArchive` — Descargar ZIP de documentos](#16-getziparchive--descargar-zip-de-documentos)
17. [Catálogo de errores (ErrorCode)](#17-catálogo-de-errores-errorcode)
18. [Endpoints y entornos](#18-endpoints-y-entornos)
19. [Guía de validación contra nuestro desarrollo](#19-guía-de-validación-contra-nuestro-desarrollo)

---

## 1. Información general del servicio

| Propiedad | Valor |
|---|---|
| **Nombre del servicio** | `IDocService` |
| **Protocolo** | SOAP 1.2 |
| **Binding** | `IDocServiceSoapBinding` |
| **targetNamespace** | `http://services.api.sint.sareb.es/` |
| **WSDL (DEV)** | `http://srbwidd01.sareb.srb:8080/sintws/IDocService?wsdl` |
| **WSDL (PRE)** | `https://srbwidd03.sareb.srb:8090/sintws/IDocService?wsdl` |
| **Content-Type** | `application/soap+xml; charset=UTF-8` |
| **SOAPAction** | Específica por operación (ver cada sección) |

### Namespaces XML necesarios

Todos los mensajes deben declarar estos namespaces. Se usan los prefijos siguientes a lo largo del documento:

| Prefijo | URI |
|---|---|
| `soap` | `http://www.w3.org/2003/05/soap-envelope` (SOAP 1.2) |
| `ser` | `http://services.api.sint.sareb.es/` |
| `auth` | `http://auth.model.api.sint.sareb.es` |
| `data` | `http://data.model.api.sint.sareb.es` |
| `fld` | `http://field.data.model.api.sint.sareb.es` |
| `fval` | `http://fieldvalue.data.model.api.sint.sareb.es` |
| `srch` | `http://search.model.api.sint.sareb.es` |
| `doc` | `http://doc.model.api.sint.sareb.es` |
| `exc` | `http://exceptions.model.api.sint.sareb.es` |
| `xsi` | `http://www.w3.org/2001/XMLSchema-instance` |

> **Nota sobre SOAP 1.1 vs 1.2**: Algunos ejemplos en los proyectos SoapUI usan el namespace de SOAP 1.1 (`http://schemas.xmlsoap.org/soap/envelope/`). Ambas versiones son aceptadas por el servicio. Nuestro `GdcService.cs` usa **SOAP 1.2**.

---

## 2. Autenticación: bloque Identity

Todas las operaciones reciben como primer parámetro (`arg0`) un objeto `Identity`:

```xml
<ser:arg0 xsi:type="auth:Identity">
  <auth:applicationId>DOC_IA_MVP</auth:applicationId>
  <auth:nominalUser>UPE12345</auth:nominalUser>    <!-- usuario nominal (opcional) -->
  <auth:username>srv_dociaapp_des</auth:username>   <!-- cuenta de servicio técnica -->
</ser:arg0>
```

| Campo | Requerido | Descripción |
|---|---|---|
| `applicationId` | **Sí** | Código de la aplicación integradora asignado por SAREB Sistemas (ej. `DOC_IA_MVP`) |
| `username` | **Sí** | Cuenta de servicio técnica (ej. `srv_dociaapp_des`) |
| `nominalUser` | No | Usuario nominal que origina la acción (para auditoría). Puede estar vacío. |

**Configuración en nuestro proyecto** (`appsettings.json` / Key Vault):

```json
"GdcSettings": {
  "ApplicationId": "DOC_IA_MVP",
  "Username":      "srv_dociaapp_des",
  "NominalUser":   ""
}
```

---

## 3. Modelo de datos común

### 3.1 Entity (documento)

```
Entity
├── ETag         (string, opcional — para concurrencia optimista, en create se pone "etag")
├── id           (string, nillable — vacío en create, relleno en update/get)
├── typeId       (string — siempre "document" para documentos)
└── fields       (ArrayOfField)
    ├── SingleField  { name, fieldValue: FieldValue }
    └── ArrayField   { name, fieldValues: ArrayOfFieldValue }
```

### 3.2 Tipos de FieldValue

| Tipo (`xsi:type`) | Uso | Elemento contenido |
|---|---|---|
| `fval:StringFieldValue` | Texto genérico | `<fval:value>texto</fval:value>` |
| `fval:LabeledFieldValue` | Texto + etiqueta visible | `<fval:value>cod</fval:value><fval:label>desc</fval:label>` |
| `fval:IntegerFieldValue` | Número entero | `<fval:value>42</fval:value>` |
| `fval:DoubleFieldValue` | Decimal | `<fval:value>3.14</fval:value>` |
| `fval:BooleanFieldValue` | Booleano | `<fval:value>true</fval:value>` |
| `fval:DateFieldValue` | Fecha/hora ISO‑8601 | `<fval:value>2024-03-01T00:00:00+01:00</fval:value>` |
| `fval:EntityFieldValue` | Referencia a otra entidad | `<fval:targetId>...</fval:targetId>` + `<fval:extraFields>` |
| `fval:FileContentFieldValue` | Contenido binario (Base64) | `<fval:dataSource>{base64}</fval:dataSource>` |
| `fval:URLContentFieldValue` | Contenido por URL | `<fval:url>...</fval:url>` + `contentType` + `name` |

### 3.3 Campos del documento (`typeId=document`)

Tabla completa de metadatos extraída del WSDL y ejemplos SoapUI:

| Campo GDC | Tipo | Req. | Descripción |
|---|---|---|---|
| `nombre_documento` | String | **Sí** | Nombre lógico del documento (puede incluir timestamp) |
| `nombre_fichero` | String | **Sí** | Nombre del fichero físico en OTCS |
| `description` | String | No | Descripción libre |
| `id_servicer` | String | **Sí** | ID del documento en el sistema origen (UUID o referencia) |
| `entidad_origen` | String | **Sí** | Código de la entidad/servicer origen |
| `proceso_carga` | String | **Sí** | Código del proceso de carga (ej. `PC03`, `CKP1`, `DOC_IA_MVP`) |
| `origen_documento` | String | **Sí**¹ | Código origen del documento (ej. `CK01`, `9019`) |
| `publico` | String | **Sí** | `"verdadero"` / `"falso"` (no booleano nativo) |
| `servicer` | String | **Sí** | Código del servicer |
| `tipo_expediente` | String | **Sí** | `"AI"` (Activo Inmobiliario), `"AF"` (Activo Financiero), `"OP"`, etc. |
| `serie` | String | **Sí** | Serie documental (ej. `"AI05"`, `"AI09"`, `"AI11"`) |
| `tipo_documento` | String | **Sí** | Código del tipo de documento (ej. `"CERT"`, `"ESIN"`, `"NOTS"`) |
| `subtipo_documento` | String | No | Código del subtipo de documento (ej. `"CERT17"`, `"ESIN87"`) |
| `expediente` | EntityFieldValue | **Sí** | Vincula el doc al expediente del activo (ver § 4.3) |
| `vinculacion` | ArrayField de EntityFieldValue | No | Vinculaciones adicionales a otros expedientes |
| `Content` | FileContentFieldValue | **Sí** | Binario del documento en Base64 |
| `checksum` | String | No | Checksum / MD5 |
| `contrato` | String | No | Número de contrato asociado |
| `estado` | String | No | Estado del documento (ej. `"DP"`) |
| `num_registro` | String | No | Número de registro externo |
| `fh_documento` | DateFieldValue | No | Fecha del documento |
| `fh_alta_ext` | DateFieldValue | No | Fecha de alta en sistema externo |
| `fh_expurgo` | DateFieldValue | No | Fecha de expurgo |
| `fh_caducidad` | DateFieldValue | No | Fecha de caducidad |
| `fh_baja_logica` | DateFieldValue | No | Fecha de baja lógica |
| `nivel_confidencialidad` | String | No | Nivel confidencialidad (ej. `"02"`) |
| `LOPD` | String | No | Clasificación LOPD/RGPD (ej. `"BAJA"`) |
| `categoria` | LabeledFieldValue | No | Categoría documental (ej. `"AI0702"`) |
| `prov_custodia` | String | No | Proveedor de custodia (ej. `"PCUS03"`) |
| `ref_custodia` | String | No | Referencia custodia externa |
| `tipo_identificacion` | String | No | Tipo de documento identificativo (ej. `"05"`) |
| `num_identificacion` | String | No | Número del documento identificativo |
| `doc_original` | String | No | Indicador de documento original (ej. `"DO01"`) |
| `pais_expedicion` | String | No | País de expedición (código ISO) |
| `id_importacion` | String | No | ID de importación batch |

> ¹ Obligatorio desde SINTWS v4.0 (2020).

---

## 4. `create` — Crear documento en GDC

### 4.1 Descripción

Crea un nuevo documento en GDC (OTCS) y lo ubica en la carpeta del expediente/activo indicado.

- **Operación WSDL**: `create(Identity arg0, Entity arg1) → string`
- **Retorno**: `objectId` (ID de nodo en OTCS, ej. `"290338"`)
- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/create`

### 4.2 Petición mínima funcional (DocumentIA MVP)

Este es el XML con los campos usados por nuestro `GdcService.cs`. Las llaves `{...}` son variables:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <ns1:create xmlns:ns1="http://services.api.sint.sareb.es/">

      <!-- IDENTIDAD -->
      <ns1:arg0
          xmlns:ns2="http://auth.model.api.sint.sareb.es"
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xsi:type="ns2:Identity">
        <ns2:applicationId>{GdcSettings.ApplicationId}</ns2:applicationId>
        <ns2:username>{GdcSettings.Username}</ns2:username>
        <ns2:nominalUser>{GdcSettings.NominalUser}</ns2:nominalUser>
      </ns1:arg0>

      <!-- ENTIDAD DOCUMENTO -->
      <ns1:arg1
          xmlns:ns2="http://data.model.api.sint.sareb.es"
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xsi:type="ns2:Entity">
        <ns2:ETag>etag</ns2:ETag>
        <ns2:fields>

          <!-- nombre_documento: nombre lógico del fichero -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>nombre_documento</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{NombreDocumento}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- nombre_fichero: nombre del fichero físico -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>nombre_fichero</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{NombreFichero}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- id_servicer: identificador único en el sistema origen -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>id_servicer</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{IdServiser}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- origen_documento: código del sistema que origina el doc -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>origen_documento</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{OrigenDocumento}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- entidad_origen: código de la entidad/servicer -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>entidad_origen</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{EntidadOrigen}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- proceso_carga: código del proceso de carga -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>proceso_carga</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{ProcesoCarga}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- publico: "verdadero" o "falso" (string, no boolean) -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>publico</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>verdadero</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- servicer: código del servicer -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>servicer</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{Servicer}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- tipo_expediente: "AI" = Activo Inmobiliario, "AF" = Activo Financiero -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>tipo_expediente</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>AI</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- serie: serie documental (ej. AI05, AI09, AI11) -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>serie</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{Serie}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- tipo_documento: código del tipo de documento clasificado -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>tipo_documento</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{TipoDocumento}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!-- subtipo_documento (opcional) -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>subtipo_documento</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>{SubtipoDocumento}</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>

          <!--
            ===========================
            VINCULACIÓN AL ACTIVO/EXPEDIENTE
            ===========================
            El campo "expediente" ubica el documento dentro de la
            carpeta OTCS del activo en GDC.
            
            - id_expediente = IdActivo de SAREB (el mismo valor que 
              usamos como idExpediente en todos nuestros flujos)
            - clase_expediente = tipo de expediente configurado
              (ej. "AI04", "AI03", "AF01", "GEN")
            - targetId = ID interno OTCS del expediente (si se conoce);
              si está vacío, SINTWS lo resuelve por id_expediente + clase_expediente
          -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>expediente</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:EntityFieldValue">
              <ns4:extraFields>
                <ns3:Field xsi:type="ns3:SingleField">
                  <ns3:name>id_expediente</ns3:name>
                  <ns3:fieldValue xsi:type="ns4:StringFieldValue">
                    <ns4:value>{IdActivo}</ns4:value>    <!-- ← ID del activo SAREB -->
                  </ns3:fieldValue>
                </ns3:Field>
                <ns3:Field xsi:type="ns3:SingleField">
                  <ns3:name>clase_expediente</ns3:name>
                  <ns3:fieldValue xsi:type="ns4:StringFieldValue">
                    <ns4:value>{ClaseExpediente}</ns4:value>  <!-- ej: "AI04" -->
                  </ns3:fieldValue>
                </ns3:Field>
              </ns4:extraFields>
              <ns4:targetId></ns4:targetId>  <!-- vacío: SINTWS resuelve automáticamente -->
            </ns3:fieldValue>
          </ns3:Field>

          <!-- Content: binario del documento en Base64 -->
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>Content</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:FileContentFieldValue">
              <ns4:dataSource>{Base64Content}</ns4:dataSource>
            </ns3:fieldValue>
          </ns3:Field>

        </ns2:fields>
        <ns2:id xsi:nil="true"/>
        <ns2:typeId>document</ns2:typeId>
      </ns1:arg1>

    </ns1:create>
  </soap:Body>
</soap:Envelope>
```

### 4.3 La vinculación IdActivo ↔ IdExpediente

> **Concepto clave**: En SAREB, el término `IdActivo` (que nosotros manejamos) es el mismo identificador que GDC llama `id_expediente`. Se trata del número de expediente del activo en el sistema GDC/OTCS.

```
DocumentIA                GDC (SINTWS)
-----------                ------------
entrada.IdActivo    →     expediente.extraFields.id_expediente
(ej: "1058669")           (ej: <ns4:value>1058669</ns4:value>)
                    
clase_expediente    →     expediente.extraFields.clase_expediente
(config tipología)        (ej: <ns4:value>AI04</ns4:value>)

targetId            →     vacío (SINTWS resuelve internamente)
```

**Valores habituales de `clase_expediente`** observados en los ejemplos SoapUI:

| Valor | Descripción presumiblemente |
|---|---|
| `AI03` | Activo Inmobiliario tipo 03 |
| `AI04` | Activo Inmobiliario tipo 04 (más frecuente en los ejemplos) |
| `AI05` | Activo Inmobiliario tipo 05 |
| `AF01` | Activo Financiero tipo 01 |
| `GEN`  | General |

### 4.4 Respuestas posibles

#### Éxito — Documento creado

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <ns1:createResponse xmlns:ns1="http://services.api.sint.sareb.es/">
      <ns1:return>290338</ns1:return>   <!-- objectId del nodo OTCS -->
    </ns1:createResponse>
  </soap:Body>
</soap:Envelope>
```

#### Error — Documento ya existe (duplicado)

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <soap:Fault>
      <faultcode>soap:Server</faultcode>
      <faultstring>...</faultstring>
      <detail>
        <ServiceException xmlns="http://exceptions.model.api.sint.sareb.es">
          <errorCode>DOC_OBJECT_EXISTS</errorCode>
          <errorMessage>Document already exists</errorMessage>
        </ServiceException>
      </detail>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>
```

> En nuestro `GdcService.cs`, cuando se recibe `DOC_OBJECT_EXISTS` en el Fault, se devuelve `YaExistia=true, Exitoso=true` (no se considera un error).

#### Error — No autorizado

```xml
<ServiceException>
  <errorCode>NOT_AUTHORIZED</errorCode>
  <errorMessage>...</errorMessage>
</ServiceException>
```

---

## 5. `searchEntities` — Buscar documentos

### 5.1 Descripción

Busca documentos en GDC aplicando filtros estructurados. Se usa principalmente para:
1. **Detección de duplicados** antes del `create` (mismos `id_expediente` + `checksum`)
2. **Consulta de documentos** de un activo
3. **Listas de documentos** con paginación

- **Operación WSDL**: `searchEntities(Identity arg0, Query arg1, List<DocRepository> arg2) → SearchResult`
- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/searchEntities`

### 5.2 Estructura del objeto Query (arg1)

```
Query
├── entityTypeId    (string) — siempre "document" para documentos
├── filter          (Expression) — condición de búsqueda (ver § 5.4)
├── firstResultIndex (int) — paginación, empieza en 1
├── maxResults      (int) — máximo de resultados por página
├── orderingField   (OrderingField, opcional)
│   ├── ascending   (boolean)
│   └── fieldName   (string)
└── resultsProfile  (EntityProfile)
    ├── applicationId (string, puede ser nil)
    ├── fieldNames   (lista de strings — campos a devolver)
    ├── ignoreContent (boolean — true para no devolver el binario)
    └── ignoreMetadata (boolean)
```

### 5.3 Tipos de Expression (filtros)

| Tipo (`xsi:type`) | Uso | Operadores disponibles |
|---|---|---|
| `srch:FieldExpression` | Filtra por un campo del documento | `EQUALS`, `IN`, `RANGE` |
| `srch:EntityExpression` | Filtra por campo de una entidad relacionada (ej. `expediente`) | `EQUALS`, `IN`, `RANGE` |
| `srch:SetExpression` | Combina múltiples expresiones | `AND`, `OR` |

**Tipos de valor para los filtros:**

| Tipo (`xsi:type`) | Uso |
|---|---|
| `srch:StringValue` | Un único string para EQUALS |
| `srch:StringValueList` | Lista de strings para IN |
| `srch:DateRangeValue` | Rango de fechas para RANGE |

### 5.4 Caso de uso principal: consulta por IdActivo (idExpediente)

Esta es la petición que nuestro sistema ejecuta para detectar si un documento ya existe:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <ns1:searchEntities xmlns:ns1="http://services.api.sint.sareb.es/">

      <!-- IDENTIDAD -->
      <ns1:arg0
          xmlns:ns2="http://auth.model.api.sint.sareb.es"
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xsi:type="ns2:Identity">
        <ns2:applicationId>{GdcSettings.ApplicationId}</ns2:applicationId>
        <ns2:username>{GdcSettings.Username}</ns2:username>
        <ns2:nominalUser>{GdcSettings.NominalUser}</ns2:nominalUser>
      </ns1:arg0>

      <!-- QUERY -->
      <ns1:arg1
          xmlns:ns2="http://search.model.api.sint.sareb.es"
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xsi:type="ns2:Query">

        <ns2:entityTypeId>document</ns2:entityTypeId>

        <!--
          FILTRO: busca documentos del expediente {IdActivo} con checksum {MD5}
          Usa EntityExpression para filtrar por campo de la entidad relacionada "expediente"
        -->
        <ns2:filter xsi:type="ns2:SetExpression">
          <ns2:expressions>

            <!-- Filtro por id_expediente (= IdActivo de SAREB) -->
            <ns2:Expression xsi:type="ns2:EntityExpression">
              <ns2:condition>IN</ns2:condition>
              <ns2:entityName>expediente</ns2:entityName>
              <ns2:fieldName>id_expediente</ns2:fieldName>
              <ns2:value xsi:type="ns2:StringValueList">
                <ns2:values>
                  <ns1:string>{IdActivo}</ns1:string>
                </ns2:values>
              </ns2:value>
            </ns2:Expression>

            <!-- Filtro por checksum (MD5) — evita duplicados exactos -->
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
        <ns2:maxResults>1</ns2:maxResults>   <!-- solo necesitamos saber si existe -->
        <ns2:orderingField xsi:nil="true"/>

        <!-- Perfil de resultados: solo metadatos mínimos, sin contenido binario -->
        <ns2:resultsProfile
            xmlns:ns3="http://data.model.api.sint.sareb.es"
            xsi:type="ns3:EntityProfile">
          <ns3:applicationId xsi:nil="true"/>
          <ns3:fieldNames>
            <ns1:string>nombre_documento</ns1:string>
            <ns1:string>checksum</ns1:string>
            <ns1:string>expediente</ns1:string>
          </ns3:fieldNames>
          <ns3:ignoreContent>true</ns3:ignoreContent>   <!-- no descargar el binario -->
          <ns3:ignoreMetadata>false</ns3:ignoreMetadata>
        </ns2:resultsProfile>

      </ns1:arg1>

      <!-- arg2: repositorios (vacío = todos) -->

    </ns1:searchEntities>
  </soap:Body>
</soap:Envelope>
```

### 5.5 Ejemplo alternativo: búsqueda solo por IdActivo (sin checksum)

Útil para listar todos los documentos de un activo:

```xml
<ns2:filter xsi:type="ns2:EntityExpression">
  <ns2:condition>IN</ns2:condition>
  <ns2:entityName>expediente</ns2:entityName>
  <ns2:fieldName>id_expediente</ns2:fieldName>
  <ns2:value xsi:type="ns2:StringValueList">
    <ns2:values>
      <ns1:string>{IdActivo}</ns1:string>
    </ns2:values>
  </ns2:value>
</ns2:filter>
```

### 5.6 Ejemplo: búsqueda por tipo_documento (FieldExpression simple)

```xml
<ns2:filter xsi:type="ns2:FieldExpression">
  <ns2:condition>EQUALS</ns2:condition>
  <ns2:fieldName>tipo_documento</ns2:fieldName>
  <ns2:value xsi:type="ns2:StringValue">
    <ns2:value>CERT</ns2:value>
  </ns2:value>
</ns2:filter>
```

### 5.7 Ejemplo: búsqueda multi-campo con AND

Ejemplo real del proyecto SoapUI — búsqueda por `tipo_documento IN [NOTS]` AND `entidad_origen IN [9999]` AND `proceso_carga IN [PC01]`:

```xml
<ns2:filter xsi:type="ns2:SetExpression">
  <ns2:expressions>
    <ns2:Expression xsi:type="ns2:FieldExpression">
      <ns2:condition>IN</ns2:condition>
      <ns2:fieldName>tipo_documento</ns2:fieldName>
      <ns2:value xsi:type="ns2:StringValueList">
        <ns2:values>
          <ns1:string>NOTS</ns1:string>
        </ns2:values>
      </ns2:value>
    </ns2:Expression>
    <ns2:Expression xsi:type="ns2:FieldExpression">
      <ns2:condition>IN</ns2:condition>
      <ns2:fieldName>entidad_origen</ns2:fieldName>
      <ns2:value xsi:type="ns2:StringValueList">
        <ns2:values>
          <ns1:string>9999</ns1:string>
        </ns2:values>
      </ns2:value>
    </ns2:Expression>
    <ns2:Expression xsi:type="ns2:FieldExpression">
      <ns2:condition>IN</ns2:condition>
      <ns2:fieldName>proceso_carga</ns2:fieldName>
      <ns2:value xsi:type="ns2:StringValueList">
        <ns2:values>
          <ns1:string>PC01</ns1:string>
        </ns2:values>
      </ns2:value>
    </ns2:Expression>
  </ns2:expressions>
  <ns2:operator>AND</ns2:operator>
</ns2:filter>
```

### 5.8 Respuesta de searchEntities

```xml
<ns1:searchEntitiesResponse xmlns:ns1="http://services.api.sint.sareb.es/">
  <ns1:return xsi:type="ns2:SearchResult" xmlns:ns2="http://search.model.api.sint.sareb.es">
    <ns2:data>             <!-- ArrayOfEntity -->
      <ns3:Entity xmlns:ns3="http://data.model.api.sint.sareb.es">
        <ns3:id>290338</ns3:id>
        <ns3:typeId>document</ns3:typeId>
        <ns3:fields>
          <ns4:Field xsi:type="ns4:SingleField" xmlns:ns4="http://field.data.model.api.sint.sareb.es">
            <ns4:name>nombre_documento</ns4:name>
            <ns4:fieldValue xsi:type="ns5:StringFieldValue" xmlns:ns5="http://fieldvalue.data.model.api.sint.sareb.es">
              <ns5:value>documento.pdf</ns5:value>
            </ns4:fieldValue>
          </ns4:Field>
          <!-- ... resto de campos solicitados en resultsProfile ... -->
        </ns3:fields>
      </ns3:Entity>
    </ns2:data>
    <ns2:firstResultIndex>1</ns2:firstResultIndex>
    <ns2:hasMoreResults>false</ns2:hasMoreResults>
    <ns2:totalItemsResult>1</ns2:totalItemsResult>
  </ns1:return>
</ns1:searchEntitiesResponse>
```

**Lógica en nuestro código**: si `totalItemsResult > 0` → el documento ya existe → devolver `YaExistia=true`.

### 5.9 Filtrado con repositorio específico (arg2)

El tercer parámetro `arg2` permite limitar la búsqueda a un repositorio concreto:

```xml
<ns1:arg2>
  <ns2:DocRepository xmlns:ns2="http://doc.model.api.sint.sareb.es"
                     xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                     xsi:type="ns2:DocRepository">
    <ns2:id>175056</ns2:id>
    <ns2:name>05 AAFF-Activos Financieros</ns2:name>
  </ns2:DocRepository>
</ns1:arg2>
```

Si se omite `arg2`, la búsqueda se realiza en todos los repositorios accesibles.

---

## 6. `get` — Recuperar documento por ObjectId

Obtiene todos los metadatos (y opcionalmente el contenido binario) de un documento conociendo su `objectId` en OTCS.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/get`

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <ns1:get xmlns:ns1="http://services.api.sint.sareb.es/">
      <ns1:arg0 xmlns:ns2="http://auth.model.api.sint.sareb.es"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:type="ns2:Identity">
        <ns2:applicationId>CKP1</ns2:applicationId>
        <ns2:username>COMPLETAR_GDC_USERNAME</ns2:username>
        <ns2:nominalUser></ns2:nominalUser>
      </ns1:arg0>
      <ns1:arg1>4526609</ns1:arg1>   <!-- objectId del documento en OTCS -->
      <ns1:arg2 xmlns:ns2="http://data.model.api.sint.sareb.es"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:type="ns2:EntityProfile">
        <ns2:applicationId xsi:nil="true"/>
        <ns2:fieldNames>
          <!-- dejar vacío para obtener todos los campos -->
        </ns2:fieldNames>
        <ns2:ignoreContent>false</ns2:ignoreContent>   <!-- true = solo metadatos -->
        <ns2:ignoreMetadata>false</ns2:ignoreMetadata>
      </ns1:arg2>
    </ns1:get>
  </soap:Body>
</soap:Envelope>
```

**Retorno**: objeto `Entity` completo con todos sus campos y opcionalmente el contenido binario.

---

## 7. `update` — Actualizar metadatos

Actualiza los metadatos de un documento existente.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/update`
- **Retorno**: `ArrayOfString` (lista de campos actualizados o mensajes)

```xml
<soap:Envelope ...>
  <soap:Body>
    <ns1:update xmlns:ns1="http://services.api.sint.sareb.es/">
      <ns1:arg0 ...> <!-- Identity --> </ns1:arg0>
      <ns1:arg1 xmlns:ns2="http://data.model.api.sint.sareb.es"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:type="ns2:Entity">
        <ns2:id>290338</ns2:id>           <!-- objectId existente -->
        <ns2:typeId>document</ns2:typeId>
        <ns2:fields>
          <!-- solo los campos a modificar -->
        </ns2:fields>
      </ns1:arg1>
    </ns1:update>
  </soap:Body>
</soap:Envelope>
```

---

## 8. `delete` — Eliminar documento

Elimina permanentemente un documento del GDC.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/delete`
- **Retorno**: void (sin cuerpo en la respuesta)

```xml
<soap:Envelope ...>
  <soap:Body>
    <ns1:delete xmlns:ns1="http://services.api.sint.sareb.es/">
      <ns1:arg0 ...> <!-- Identity --> </ns1:arg0>
      <ns1:arg1>290338</ns1:arg1>   <!-- objectId a eliminar -->
    </ns1:delete>
  </soap:Body>
</soap:Envelope>
```

---

## 9. `bajaLogica` — Baja lógica

Marca un documento como dado de baja lógicamente (sin eliminarlo físicamente), registrando la fecha de baja.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/bajaLogica`
- **Retorno**: `dateTime` (fecha en que se realizó la baja)

```xml
<soap:Envelope ...>
  <soap:Body>
    <ns1:bajaLogica xmlns:ns1="http://services.api.sint.sareb.es/">
      <ns1:arg0 ...> <!-- Identity --> </ns1:arg0>
      <ns1:arg1>{objectId}</ns1:arg1>   <!-- ID del documento -->
      <ns1:arg2>{motivoBaja}</ns1:arg2>  <!-- texto libre con el motivo -->
    </ns1:bajaLogica>
  </soap:Body>
</soap:Envelope>
```

---

## 10. `moveDocument` — Mover documento entre expedientes

Mueve uno o varios documentos de su expediente actual a otro expediente.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/moveDocument`
- **Retorno**: `ArrayOfMoveResult` (estado de cada movimiento)

```xml
<soap:Envelope ...>
  <soap:Body>
    <ns1:moveDocument xmlns:ns1="http://services.api.sint.sareb.es/">
      <ns1:arg0 ...> <!-- Identity --> </ns1:arg0>
      <ns1:arg1>
        <ns2:MoveData xmlns:ns2="http://doc.model.api.sint.sareb.es">
          <ns2:objectId>{objectIdDocumento}</ns2:objectId>
          <ns2:idExpediente>{nuevoIdExpediente}</ns2:idExpediente>
          <ns2:claseExpediente>{nuevaClaseExpediente}</ns2:claseExpediente>
          <ns2:newName>{nuevoNombre}</ns2:newName>     <!-- opcional -->
          <ns2:entityId>{entityId}</ns2:entityId>       <!-- opcional -->
        </ns2:MoveData>
      </ns1:arg1>
    </ns1:moveDocument>
  </soap:Body>
</soap:Envelope>
```

**Respuesta por cada `MoveData`:**

```xml
<ns2:MoveResult xmlns:ns2="http://doc.model.api.sint.sareb.es">
  <ns2:objectId>290338</ns2:objectId>
  <ns2:state>true</ns2:state>          <!-- true=éxito, false=error -->
  <ns2:description>OK</ns2:description>
</ns2:MoveResult>
```

---

## 11. `addDocumentVinculacion` — Añadir vinculación

Añade una vinculación de un documento ya existente a un expediente adicional (sin moverlo).

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/addDocumentVinculacion`
- **Retorno**: `string`

```xml
<ser:addDocumentVinculacion>
  <ser:arg0> <!-- Identity --> </ser:arg0>
  <ser:arg1>{objectId}</ser:arg1>    <!-- objectId del documento -->
  <ser:arg2>                          <!-- EntityFieldValue con el expediente destino -->
    <fval:extraFields>
      <fld:Field>
        <fld:name>id_expediente</fld:name>
        <fld:fieldValue xsi:type="fval:StringFieldValue">
          <fval:value>{idExpedienteDestino}</fval:value>
        </fld:fieldValue>
      </fld:Field>
    </fval:extraFields>
    <fval:targetId>{targetId}</fval:targetId>
  </ser:arg2>
</ser:addDocumentVinculacion>
```

---

## 12. `addEntityField` — Añadir campo a entidad

Añade un campo de tipo entidad a un documento existente.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/addEntityField`
- **Retorno**: `boolean`

```xml
<ser:addEntityField>
  <ser:arg0> <!-- Identity --> </ser:arg0>
  <ser:arg1>{objectId}</ser:arg1>       <!-- objectId del documento -->
  <ser:arg2>{nombreCampo}</ser:arg2>   <!-- nombre del campo de entidad -->
  <ser:arg3>                             <!-- EntityFieldValue -->
    <fval:extraFields> ... </fval:extraFields>
    <fval:targetId>{targetId}</fval:targetId>
  </ser:arg3>
</ser:addEntityField>
```

---

## 13. `getRepositories` — Listar repositorios

Devuelve la lista de repositorios GDC accesibles para la identidad indicada.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/getRepositories`
- **Retorno**: `ArrayOfDocRepository`

```xml
<ser:getRepositories>
  <ser:arg0>
    <auth:applicationId>CKP1</auth:applicationId>
    <auth:username>COMPLETAR_GDC_USERNAME</auth:username>
  </ser:arg0>
</ser:getRepositories>
```

**Respuesta**:

```xml
<ns1:getRepositoriesResponse>
  <ns1:return>
    <ns2:DocRepository xmlns:ns2="http://doc.model.api.sint.sareb.es">
      <ns2:id>175056</ns2:id>
      <ns2:name>05 AAFF-Activos Financieros</ns2:name>
    </ns2:DocRepository>
    <ns2:DocRepository>
      <ns2:id>175057</ns2:id>
      <ns2:name>01 AAII-Activos Inmobiliarios</ns2:name>
    </ns2:DocRepository>
    <!-- ... -->
  </ns1:return>
</ns1:getRepositoriesResponse>
```

---

## 14. `resolveIDSarebValues` — Resolver IDSareb

Resuelve IDs de SAREB a sus valores de entidad en GDC.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/resolveIDSarebValues`
- **Retorno**: `ArrayOfResolvedIDSareb`

```xml
<ser:resolveIDSarebValues>
  <ser:arg0> <!-- Identity --> </ser:arg0>
  <ser:arg1>
    <ser:string>{idSareb1}</ser:string>
    <ser:string>{idSareb2}</ser:string>
  </ser:arg1>
</ser:resolveIDSarebValues>
```

**Respuesta**:

```xml
<ns2:ResolvedIDSareb xmlns:ns2="http://doc.model.api.sint.sareb.es">
  <ns2:idSareb>VAI-41730</ns2:idSareb>
  <ns2:entityId>99149</ns2:entityId>    <!-- targetId de OTCS -->
  <ns2:cuenta>...</ns2:cuenta>
</ns2:ResolvedIDSareb>
```

---

## 15. `validateActivo` — Validar activo

Valida que un activo tiene configurados todos los tipos de documento requeridos en GDC.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/validateActivo`
- **Retorno**: `ActivoValidationReport`

```xml
<ser:validateActivo>
  <ser:arg0> <!-- Identity --> </ser:arg0>
  <ser:arg1>{idExpediente}</ser:arg1>
  <ser:arg2>
    <ser:string>CERT</ser:string>
    <ser:string>NOTS</ser:string>
    <!-- tipos de documento a validar -->
  </ser:arg2>
</ser:validateActivo>
```

**Respuesta**:

```xml
<ns2:ActivoValidationReport xmlns:ns2="http://doc.model.api.sint.sareb.es">
  <ns2:valid>true</ns2:valid>
  <ns2:missingTipoDocumento/>   <!-- vacío si valid=true -->
</ns2:ActivoValidationReport>
```

---

## 16. `getZipArchive` — Descargar ZIP de documentos

Descarga varios documentos (por objectId) empaquetados en un ZIP.

- **SOAPAction**: `http://services.api.sint.sareb.es/IDocServicePortType/getZipArchive`
- **Retorno**: `DocumentsPackageResult` con el contenido ZIP en Base64

```xml
<ser:getZipArchive>
  <ser:arg0> <!-- Identity --> </ser:arg0>
  <ser:arg1>
    <ser:string>{objectId1}</ser:string>
    <ser:string>{objectId2}</ser:string>
  </ser:arg1>
</ser:getZipArchive>
```

---

## 17. Catálogo de errores (ErrorCode)

Todos los errores se devuelven como SOAP Fault con `ServiceException` en el bloque `<detail>`:

```xml
<soap:Fault>
  <faultcode>soap:Server</faultcode>
  <faultstring>...</faultstring>
  <detail>
    <ServiceException xmlns="http://exceptions.model.api.sint.sareb.es">
      <errorCode>ERROR_GDC_001</errorCode>
      <errorMessage>Descripción del error</errorMessage>
    </ServiceException>
  </detail>
</soap:Fault>
```

| ErrorCode | Descripción | Tratamiento recomendado |
|---|---|---|
| `NOT_AUTHORIZED` | Credenciales incorrectas o sin permisos | Error fatal — revisar `applicationId`/`username` |
| `DOC_OBJECT_EXISTS` | El documento ya existe en GDC | `YaExistia=true, Exitoso=true` — NO es error |
| `DOC_NOT_FOUND` | ObjectId no encontrado | Error recuperable |
| `FOLDER_NOT_FOUND` | Expediente/carpeta no existe en OTCS | Verificar `id_expediente` + `clase_expediente` |
| `FOLDER_NOT_EMPTY` | Carpeta no vacía (en operaciones de borrado) | — |
| `VALIDATION_ERROR_GDC` | Metadatos inválidos (campo requerido vacío, valor no permitido) | Revisar campos obligatorios |
| `SEARCH_ERROR` | Error en la consulta | Verificar sintaxis del filtro |
| `OPERATION_NOT_FOUND` | Operación no encontrada | Verificar SOAPAction |
| `UNEXPECTED_ERROR` | Error interno del servidor | Reintento con backoff |
| `WS_CONNECTION_ERROR` | Error de conexión con OTCS | Reintento con backoff |
| `ERROR_GDC_001`..`011` | Errores específicos GDC | Consultar manual SINTWS §7 |
| `ER_CC_GDC_001`..`009` | Errores de capa de comunicación | Consultar manual SINTWS §7 |
| `ERROR_SINCRONIZACION_DWH_001/002` | Errores de sincronización DWH | No crítico para la subida |
| `SINCRONIZACION_DWH_OK` | Sincronización DWH exitosa | Informativo |
| `MIGRACIONSHP_SERV_EXCEPTION` | Error en migración SharePoint | — |

---

## 18. Endpoints y entornos

| Entorno | Endpoint | Puerto | Protocolo |
|---|---|---|---|
| **Desarrollo (DEV)** | `srbwidd01.sareb.srb` | `8443` | HTTPS |
| **Preproducción (PRE)** | `srbwidd03.sareb.srb` | `8090` | HTTP/HTTPS |
| **Producción (PRO)** | `srbwidd03.sareb.srb` | `8090` | HTTPS |

URLs completas:

```
DEV:  https://srbwidd01.sareb.srb:8443/sintws/IDocService
PRE:  https://srbwidd03.sareb.srb:8090/sintws/IDocService
```

> **Nota**: Los endpoints usan hostnames de red interna SAREB (`*.sareb.srb`). En entornos fuera de la red SAREB se necesita VPN o el mock server local.

---

## 19. Guía de validación contra nuestro desarrollo

Esta sección describe cómo usar SoapUI para validar que nuestro `GdcService.cs` genera los mensajes SOAP correctos.

### 19.1 Herramientas necesarias

- **SoapUI 5.7+** (archivo de proyecto: `DESARROLLO (16.2)-soapui-project.xml`)
- Acceso VPN a red SAREB (o mock servidor local en `localhost:7071`)
- OpenSSL para calcular MD5/SHA256 de ficheros de prueba

### 19.2 Casos de prueba: `create`

#### CP-01: Crear documento simulando DocumentIA MVP

**Objetivo**: Verificar que el XML generado por `GdcService.BuildCreateSoapBody()` es estructuralmente correcto.

**Petición SoapUI** (copiar en operation `create → Request 1`):

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <ns1:create xmlns:ns1="http://services.api.sint.sareb.es/">
      <ns1:arg0 xmlns:ns2="http://auth.model.api.sint.sareb.es"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:type="ns2:Identity">
        <ns2:applicationId>DOC_IA_MVP</ns2:applicationId>
        <ns2:username>srv_dociaapp_des</ns2:username>
        <ns2:nominalUser></ns2:nominalUser>
      </ns1:arg0>
      <ns1:arg1 xmlns:ns2="http://data.model.api.sint.sareb.es"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:type="ns2:Entity">
        <ns2:ETag>etag</ns2:ETag>
        <ns2:fields>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>nombre_documento</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>nota_simple_VAI-12345_20260324.pdf</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>nombre_fichero</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>nota_simple_VAI-12345_20260324.pdf</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>id_servicer</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>f47ac10b-58cc-4372-a567-0e02b2c3d479</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>origen_documento</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>DOC_IA_MVP</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>entidad_origen</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>9999</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>proceso_carga</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>DOC_IA_MVP</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>publico</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>verdadero</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>servicer</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>9999</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>tipo_expediente</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>AI</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>serie</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>AI09</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>tipo_documento</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:StringFieldValue">
              <ns4:value>NOTS</ns4:value>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>expediente</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:EntityFieldValue">
              <ns4:extraFields>
                <ns3:Field xsi:type="ns3:SingleField">
                  <ns3:name>id_expediente</ns3:name>
                  <ns3:fieldValue xsi:type="ns4:StringFieldValue">
                    <ns4:value>VAI-12345</ns4:value>   <!-- IdActivo de prueba -->
                  </ns3:fieldValue>
                </ns3:Field>
                <ns3:Field xsi:type="ns3:SingleField">
                  <ns3:name>clase_expediente</ns3:name>
                  <ns3:fieldValue xsi:type="ns4:StringFieldValue">
                    <ns4:value>AI04</ns4:value>
                  </ns3:fieldValue>
                </ns3:Field>
              </ns4:extraFields>
              <ns4:targetId></ns4:targetId>
            </ns3:fieldValue>
          </ns3:Field>
          <ns3:Field xmlns:ns3="http://field.data.model.api.sint.sareb.es" xsi:type="ns3:SingleField">
            <ns3:name>Content</ns3:name>
            <ns3:fieldValue xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es" xsi:type="ns4:FileContentFieldValue">
              <ns4:dataSource>JVBERi0xLjQ...</ns4:dataSource>  <!-- Base64 del PDF -->
            </ns3:fieldValue>
          </ns3:Field>
        </ns2:fields>
        <ns2:id xsi:nil="true"/>
        <ns2:typeId>document</ns2:typeId>
      </ns1:arg1>
    </ns1:create>
  </soap:Body>
</soap:Envelope>
```

**Resultado esperado**: La respuesta contiene `<ns1:return>` con un número entero positivo (objectId).

#### CP-02: Crear documento duplicado

**Objetivo**: Verificar que el servicio devuelve `DOC_OBJECT_EXISTS` cuando el mismo documento ya existe.

**Acción**: Ejecutar la misma petición CP-01 dos veces seguidas.

**Resultado esperado en la segunda llamada**: SOAP Fault con `<errorCode>DOC_OBJECT_EXISTS</errorCode>`.

---

### 19.3 Casos de prueba: `searchEntities`

#### CS-01: Buscar documentos de un activo por IdActivo

**Objetivo**: Verificar la consulta previa de duplicados.

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <ns1:searchEntities xmlns:ns1="http://services.api.sint.sareb.es/">
      <ns1:arg0 xmlns:ns2="http://auth.model.api.sint.sareb.es"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:type="ns2:Identity">
        <ns2:applicationId>DOC_IA_MVP</ns2:applicationId>
        <ns2:username>srv_dociaapp_des</ns2:username>
      </ns1:arg0>
      <ns1:arg1 xmlns:ns2="http://search.model.api.sint.sareb.es"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                xsi:type="ns2:Query">
        <ns2:entityTypeId>document</ns2:entityTypeId>
        <ns2:filter xsi:type="ns2:EntityExpression">
          <ns2:condition>IN</ns2:condition>
          <ns2:entityName>expediente</ns2:entityName>
          <ns2:fieldName>id_expediente</ns2:fieldName>
          <ns2:value xsi:type="ns2:StringValueList">
            <ns2:values>
              <ns1:string>VAI-12345</ns1:string>
            </ns2:values>
          </ns2:value>
        </ns2:filter>
        <ns2:firstResultIndex>1</ns2:firstResultIndex>
        <ns2:maxResults>10</ns2:maxResults>
        <ns2:orderingField xsi:nil="true"/>
        <ns2:resultsProfile xmlns:ns3="http://data.model.api.sint.sareb.es" xsi:type="ns3:EntityProfile">
          <ns3:applicationId xsi:nil="true"/>
          <ns3:fieldNames>
            <ns1:string>nombre_documento</ns1:string>
            <ns1:string>checksum</ns1:string>
            <ns1:string>tipo_documento</ns1:string>
            <ns1:string>expediente</ns1:string>
          </ns3:fieldNames>
          <ns3:ignoreContent>true</ns3:ignoreContent>
          <ns3:ignoreMetadata>false</ns3:ignoreMetadata>
        </ns2:resultsProfile>
      </ns1:arg1>
    </ns1:searchEntities>
  </soap:Body>
</soap:Envelope>
```

**Resultado esperado**: `SearchResult` con `totalItemsResult > 0` si el CP-01 se ejecutó correctamente.

#### CS-02: Buscar por IdActivo + checksum (detección de duplicados exacta)

**Objetivo**: Confluir exactamente la lógica de `ConsultarDocumentoAsync()`.

Reemplazar el filtro por:

```xml
<ns2:filter xsi:type="ns2:SetExpression">
  <ns2:expressions>
    <ns2:Expression xsi:type="ns2:EntityExpression">
      <ns2:condition>IN</ns2:condition>
      <ns2:entityName>expediente</ns2:entityName>
      <ns2:fieldName>id_expediente</ns2:fieldName>
      <ns2:value xsi:type="ns2:StringValueList">
        <ns2:values>
          <ns1:string>VAI-12345</ns1:string>
        </ns2:values>
      </ns2:value>
    </ns2:Expression>
    <ns2:Expression xsi:type="ns2:FieldExpression">
      <ns2:condition>EQUALS</ns2:condition>
      <ns2:fieldName>checksum</ns2:fieldName>
      <ns2:value xsi:type="ns2:StringValue">
        <ns2:value>{MD5-del-fichero}</ns2:value>
      </ns2:value>
    </ns2:Expression>
  </ns2:expressions>
  <ns2:operator>AND</ns2:operator>
</ns2:filter>
```

**Resultado esperado**: `totalItemsResult = 1` si el documento del CP-01 tiene ese checksum. `totalItemsResult = 0` si no existe.

---

### 19.4 Checklist de validación

| # | Verificación | Método SoapUI | Resultado esperado |
|---|---|---|---|
| V-01 | Credenciales correctas aceptadas | `create` con user/pass de DEV | HTTP 200, `<return>` con objectId |
| V-02 | Credenciales incorrectas rechazadas | `create` con user incorrecto | SOAP Fault `NOT_AUTHORIZED` |
| V-03 | Documento se crea correctamente | CP-01 | objectId no nulo |
| V-04 | Segundo create = duplicado | CP-02 | Fault `DOC_OBJECT_EXISTS` |
| V-05 | searchEntities encuentra el doc creado | CS-01 tras CP-01 | `totalItemsResult >= 1` |
| V-06 | searchEntities por checksum exacto | CS-02 | `totalItemsResult = 1` |
| V-07 | IdActivo inexistente | `create` con `id_expediente` inventado | Fault `FOLDER_NOT_FOUND` o `VALIDATION_ERROR_GDC` |
| V-08 | Documento recuperado por objectId | `get` con objectId de V-03 | Entity con todos los campos |
| V-09 | Timeout > 120s rethrow | Proxy lento o URL incorrecta | Timeout en cliente |

---

*Fin del documento. Para dudas sobre el mock server local, ver [GUIA_INTEGRACION_GDC.md](./manuales/GUIA_INTEGRACION_GDC.md) §9.*
