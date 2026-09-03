# Integración de Extracción con Azure AI Content Understanding

## Índice

1. [Descripción general](#1-descripción-general)
2. [Arquitectura del flujo](#2-arquitectura-del-flujo)
3. [Componentes principales](#3-componentes-principales)
4. [Configuración y seed](#4-configuración-y-seed)
5. [Añadir soporte para una nueva tipología](#5-añadir-soporte-para-una-nueva-tipología)
6. [Configuración en local (desarrollo)](#6-configuración-en-local-desarrollo)
7. [Configuración en Azure (producción)](#7-configuración-en-azure-producción)
8. [Mapeo de campos](#8-mapeo-de-campos)
9. [Proveedor mock (sin Azure)](#9-proveedor-mock-sin-azure)
10. [Preguntas frecuentes](#10-preguntas-frecuentes)
11. [Clasificación por defecto con Azure Document Intelligence](#11-clasificación-por-defecto-con-azure-document-intelligence)
12. [Copiar un analizador con la Copy API (sin reentrenar)](#12-copiar-un-analizador-con-la-copy-api-sin-reentrenar)

---

## 1. Descripción general

El pipeline de procesamiento de documentos incluye un paso de **extracción de datos estructurados** a partir del contenido del documento. Este paso está soportado por **Azure AI Content Understanding**, un servicio de Azure AI Foundry que analiza documentos en formato PDF u otros formatos y devuelve los campos definidos en un analizador previamente entrenado.

La integración es **totalmente guiada por configuración**: no hay código específico por tipología. Añadir una nueva tipología con extracción Azure solo requiere:

- Definir un analizador en Azure AI Foundry.
- Registrar ese analizador en la tabla `ModeloConfigs` mediante Admin API/DocumentIA.Admin.
- Habilitar la extracción en `Tipologias.ConfiguracionJson` para la tipología publicada.

Los ficheros `models.json` y `*.validation.json` del repositorio son seed o referencia historica; pueden estar desactualizados y no son la fuente de verdad operativa.

---

## 2. Arquitectura del flujo

```
DocumentProcessOrchestrator
        │
        ▼
  ExtraerActivity
        │
        ▼
ConfigurableExtraerDataProvider   ◄── Lee la tipología de TipologiaValidationConfig
        │                               campo: Extraction.Provider
        ├── "mock"                  ──► MockExtraerDataProvider
        │
        └── "azure-content-understanding" ──► AzureContentUnderstandingProvider
                                                    │
                                                    ├─ ExtractionModelRegistryLoader  ◄── ModeloConfigs (BD)
                                                    │     (resuelve AnalyzerId, ContentType, etc.)
                                                    │
                                                    ├─ ContentUnderstandingClient     ◄── Azure AI Foundry
                                                    │     (AnalyzeBinaryAsync)
                                                    │
                                                    └─ ContentUnderstandingResultMapper
                                                          (transforma el JSON de respuesta
                                                           a DatosExtraidos según la config
                                                           de campos de la tipología)
```

### Datos que fluyen

| Paso | Entrada | Salida |
|------|---------|--------|
| `ExtraerActivity` | `ExtraccionInput` (tipología + documento en base64 + datos normalizados previos) | `ExtraccionResultado` |
| `AzureContentUnderstandingProvider` | `ExtraccionInput` | `ExtraccionResultado` con `DatosExtraidos` |
| `ContentUnderstandingResultMapper` | JSON de respuesta de Azure + `TipologiaValidationConfig` | `Dictionary<string, object>` |

---

## 3. Componentes principales

### `ConfigurableExtraerDataProvider`
**Fichero:** `DocumentIA.Functions/Services/ConfigurableExtraerDataProvider.cs`

Actúa como router. Lee el campo `Extraction.Provider` de la configuración publicada de la tipología en BD y delega en el proveedor correspondiente. Si el campo está vacío, usa el valor de `Extraction:DefaultProvider` de la configuración de la Function App.

Proveedores soportados:

| Valor en config | Proveedor | Descripción |
|-----------------|-----------|-------------|
| `azure-content-understanding` | `AzureContentUnderstandingProvider` | Llamada a Azure AI Content Understanding. Si la completitud es baja puede activar fallback GPT. |
| `azure-document-intelligence` / `azure-di` | `AzureDocumentIntelligenceExtraerDataProvider` | Extracción con Azure Document Intelligence (layout/modelo). |
| `azure-openai` / `openai` / `gpt` | `GptDirectExtraerDataProvider` | Extracción directa con GPT (sin CU previo). Requiere `Extraction:GptFallback:Endpoint/DeploymentName/ApiKey`. |
| `mock` | `MockExtraerDataProvider` | Datos fijos para testing. |

### `AzureContentUnderstandingProvider`
**Fichero:** `DocumentIA.Functions/Services/AzureContentUnderstandingProvider.cs`

Implementa la llamada real al servicio Azure AI Content Understanding:

1. Carga la configuración de la tipología para obtener `ModelKey`.
2. Resuelve el modelo en `ExtractionModelRegistryLoader` para obtener `AnalyzerId`, `ContentType`, etc.
3. Decodifica el campo `Documento.Content.Base64` del contrato de entrada.

> Nota: cuando la petición llega por `documento.objectIdGDC`, el orquestador recupera previamente el binario desde GDC y lo hidrata en `Documento.Content.Base64` antes de entrar en las actividades de normalización/extracción.
4. Llama a `ContentUnderstandingClient.AnalyzeBinaryAsync` con `WaitUntil.Completed` (operación de larga duración, espera hasta que Azure finaliza el análisis).
5. Parsea el JSON de respuesta y lo pasa al mapper.

### `ContentUnderstandingResultMapper`
**Fichero:** `DocumentIA.Functions/Services/ContentUnderstandingResultMapper.cs`

Transforma la respuesta de Azure en el diccionario `DatosExtraidos`. Para cada campo definido en la tipología:

- Busca el campo en `result.contents[0].fields` del JSON de respuesta.
- Si existe una `fieldMapping` explícita, sigue el `SourcePath` indicado (notación de punto, p. ej. `Owner.Name`).
- Si `AutoMapUnmappedFields` es `true`, intenta localizar el campo por el mismo nombre que tiene en la configuración.
- Convierte el valor al tipo CLR apropiado según el tipo de campo de Azure CU (`valueString`, `valueDate`, `valueNumber`, `valueBoolean`, `valueArray`, `valueObject`, `content`, etc.).

### `ExtractionModelRegistryLoader`
**Fichero:** `DocumentIA.Core/Configuration/ExtractionModelRegistryLoader.cs`

Carga y cachea en memoria los modelos publicados en la tabla `ModeloConfigs` (tipo `Extraccion`). Proporciona el método `GetModel(string key)` que lanza `KeyNotFoundException` si la clave no existe. El fichero `config/extraction/models.json` queda como seed/bootstrap o referencia historica.

---

## 4. Configuración y seed

### 4.1 Registro de modelos de extraccion

Fuente de verdad runtime: tabla `ModeloConfigs` (`TipoModelo.Extraccion`), gestionada por DocumentIA.Admin o Admin API.

Seed/referencia historica: `DocumentIA.Functions/config/extraction/models.json`.

Cada entrada describe un analizador de Azure AI Content Understanding.

```json
{
  "models": [
    {
      "key": "nota.simple.1_4.azure-cu",
      "provider": "azure-content-understanding",
      "analyzerId": "nota-simple-1-4",
      "contentType": "application/pdf",
      "processingLocation": "global",
      "inputRange": ""
    }
  ]
}
```

| Campo | Obligatorio | Descripción |
|-------|-------------|-------------|
| `key` | Sí | Identificador único. Referenciado desde los ficheros de validación de tipología en `extraction.modelKey`. |
| `provider` | Sí | Debe ser `azure-content-understanding`. |
| `analyzerId` | Sí | ID del analizador tal como está creado en Azure AI Foundry. |
| `contentType` | Sí | MIME type del documento (`application/pdf`, `image/jpeg`, etc.). Si está vacío se intenta detectar automáticamente. |
| `processingLocation` | No | Región de procesamiento. Si está vacío, usa `DefaultProcessingLocation` de la configuración de la Function App (por defecto `global`). |
| `inputRange` | No | Rango de páginas a analizar (p. ej. `"pages": "1-3"`). Vacío = documento completo. |

### 4.2 Fichero de validación de tipología
**Ruta:** `DocumentIA.Functions/config/tipologias/<tipologia>.validation.json`

Ejemplo del bloque `extraction` en `nota.simple.1_4.validation.json`:

```json
{
  "tipologiaId": "notasimple",
  "version": "1.4",
  "extraction": {
    "enabled": true,
    "provider": "azure-content-understanding",
    "modelKey": "nota.simple.1_4.azure-cu",
    "autoMapUnmappedFields": true,
    "fieldMappings": []
  },
  "fields": [ ... ]
}
```

| Campo | Obligatorio | Descripción |
|-------|-------------|-------------|
| `enabled` | Sí | `true` para activar la llamada a Azure. `false` deja el paso sin extracción. |
| `provider` | Sí | `azure-content-understanding` o `mock`. |
| `modelKey` | Sí | Clave del modelo en `models.json`. Si la tipología **no** define ningún `modelKey` (ni `secondaryModelKey`, ni llega forzado por request), desde AB#100192 la extracción se trata como **no configurada**: resultado vacío controlado con modelo `sin-configurar`, sin llamar a CU ni al fallback GPT (antes reventaba con `KeyNotFoundException` y la ejecución cerraba `EXTRACCION_INCOMPLETA`). |
| `autoMapUnmappedFields` | No | Si `true` (por defecto), intenta casar automáticamente campos de Azure con los campos declarados en `fields[]` por nombre coincidente. |
| `fieldMappings` | No | Mapeos explícitos cuando el nombre del campo en Azure difiere del nombre en la tipología (ver [sección 8](#8-mapeo-de-campos)). |

### 4.3 `appsettings.json`
**Ruta:** `DocumentIA.Functions/appsettings.json`

Valores por defecto (sin datos sensibles). Se usan fuera del runtime de Azure Functions (p. ej., pruebas de integración).

```json
{
  "Extraction": {
    "DefaultProvider": "mock",
    "AzureContentUnderstanding": {
      "Endpoint": "",
      "ApiKey": "",
      "AuthMode": "ApiKey",
      "DefaultProcessingLocation": "global"
    }
  }
}
```

### 4.4 `local.settings.json`
**Ruta:** `DocumentIA.Functions/local.settings.json`

Configuración local para desarrollo. Este fichero **no se sube al repositorio** (está en `.gitignore`).

Las claves relevantes dentro de `Values` son:

```json
{
  "Values": {
    "Extraction:DefaultProvider": "mock",
    "Extraction:AzureContentUnderstanding:Endpoint": "https://<tu-recurso>.cognitiveservices.azure.com/",
    "Extraction:AzureContentUnderstanding:ApiKey": "<tu-api-key>",
    "Extraction:AzureContentUnderstanding:AuthMode": "ApiKey",
    "Extraction:AzureContentUnderstanding:DefaultProcessingLocation": "global"
  }
}
```

---

## 5. Añadir soporte para una nueva tipología

Supongamos que queremos añadir extracción Azure para una tipología `tasacion.1_0`.

### Paso 1: Crear el analizador en Azure AI Foundry

En el portal de Azure AI Foundry, crear un analizador con el ID que quieras usar (p. ej. `tasacion-1-0`) y entrenarlo con los documentos representativos. Anotar el `analyzerId` resultante.

### Paso 2: Registrar el modelo en BD

Crear o actualizar una entrada en `ModeloConfigs` mediante DocumentIA.Admin o Admin API. Puede usarse este JSON como plantilla o seed de un entorno nuevo:

```json
{
  "key": "tasacion.1_0.azure-cu",
  "provider": "azure-content-understanding",
  "analyzerId": "tasacion-1-0",
  "contentType": "application/pdf",
  "processingLocation": "global",
  "inputRange": ""
}
```

### Paso 3: Habilitar la extracción en la configuración de la tipología

En `Tipologias.ConfiguracionJson`, añadir el bloque `extraction`. El fichero `config/tipologias/tasacion.1_0.validation.json` solo sirve como plantilla/seed:

```json
{
  "tipologiaId": "tasacion",
  "version": "1.0",
  "extraction": {
    "enabled": true,
    "provider": "azure-content-understanding",
    "modelKey": "tasacion.1_0.azure-cu",
    "autoMapUnmappedFields": true,
    "fieldMappings": []
  },
  "fields": [ ... ]
}
```

### Paso 4: Ajustar `fieldMappings` si es necesario

Si algún campo de la tipología tiene un nombre diferente al que devuelve Azure CU, añadirlo en `fieldMappings` (ver [sección 8](#8-mapeo-de-campos)).

### Paso 5: Verificar

No hay cambios de código. Ejecutar los tests existentes y arrancar la Function App en local apuntando al nuevo analizador.

---

## 6. Configuración en local (desarrollo)

### Opción A: Usar el mock (sin llamada a Azure)

En `local.settings.json` asegurarse de que:

```json
"Extraction:DefaultProvider": "mock"
```

Y en el fichero de validación de la tipología, poner `"provider": "mock"` (o dejarlo vacío para que use el default).

### Opción B: Llamada real a Azure con API Key

1. Crear un recurso **Azure AI Services** en Azure Portal o Azure AI Foundry.
2. Obtener el **endpoint** (p. ej. `https://mi-recurso.cognitiveservices.azure.com/`) y una **API Key**.
3. Rellenar `local.settings.json`:

```json
"Extraction:AzureContentUnderstanding:Endpoint": "https://mi-recurso.cognitiveservices.azure.com/",
"Extraction:AzureContentUnderstanding:ApiKey": "<api-key>",
"Extraction:AzureContentUnderstanding:AuthMode": "ApiKey"
```

4. Asegurarse de que el analizador referenciado en `models.json` (`analyzerId`) existe en ese recurso.

### Opción C: Llamada real a Azure con identidad (DefaultAzureCredential)

1. Iniciar sesión con `az login` o Visual Studio.
2. Configurar:

```json
"Extraction:AzureContentUnderstanding:Endpoint": "https://mi-recurso.cognitiveservices.azure.com/",
"Extraction:AzureContentUnderstanding:AuthMode": "DefaultAzureCredential"
```

3. La identidad local debe tener el rol **Cognitive Services User** en el recurso de Azure AI Services.

---

## 7. Configuración en Azure (producción)

### Variables de entorno obligatorias en la Function App

Añadir en **Configuración > Variables de entorno** (o mediante Bicep/Terraform):

| Variable | Valor |
|----------|-------|
| `Extraction__DefaultProvider` | `azure-content-understanding` (o `mock` para deshabilitar) |
| `Extraction__AzureContentUnderstanding__Endpoint` | Endpoint del recurso Azure AI Services |
| `Extraction__AzureContentUnderstanding__AuthMode` | `DefaultAzureCredential` (recomendado) o `ApiKey` |
| `Extraction__AzureContentUnderstanding__ApiKey` | Solo si `AuthMode=ApiKey`. Mejor usar un secreto de Key Vault. |
| `Extraction__AzureContentUnderstanding__DefaultProcessingLocation` | `global` (por defecto) |

> **Nota:** Azure Functions usa `__` como separador de jerarquía en variables de entorno (equivalente a `:` en JSON).

### Autenticación recomendada en producción

Usar **Managed Identity** para evitar gestionar API keys:

1. Activar la **identidad administrada asignada por el sistema** en la Function App.
2. Asignar el rol **Cognitive Services User** a esa identidad en el recurso de Azure AI Services.
3. Configurar `AuthMode=DefaultAzureCredential`.

---

## 8. Mapeo de campos

Por defecto (`autoMapUnmappedFields: true`), el mapper intenta localizar cada campo declarado en `fields[]` de la tipología buscando en la respuesta de Azure un campo con el mismo nombre (sin distinción de mayúsculas).

### Mapeo automático (por nombre)

Si el analizador devuelve un campo llamado `FincaRegistral` y la tipología tiene un campo llamado `FincaRegistral`, la asignación ocurre automáticamente sin configuración adicional.

### Mapeo explícito con `fieldMappings`

Si el nombre del campo en el analizador de Azure difiere del nombre en la tipología, o si el valor está anidado dentro de un objeto o array, se usa `fieldMappings`:

```json
"fieldMappings": [
  {
    "targetField": "Titular",
    "sourcePath": "Owner.Name"
  },
  {
    "targetField": "FechaInscripcion",
    "sourcePath": "InscriptionData.Date"
  }
]
```

- `targetField`: nombre del campo en la tipología (en `fields[]`).
- `sourcePath`: ruta en la respuesta de Azure usando notación de punto. Soporta:
  - Acceso directo: `CampoSimple`
  - Objetos anidados: `Objeto.SubCampo` (navega por `valueObject`)
  - Arrays indexados: `Lista.0.Nombre` (accede al primer elemento del `valueArray`)

### Tipos de valor soportados

El mapper convierte automáticamente los tipos de Azure Content Understanding al tipo CLR correspondiente:

| Tipo Azure CU | Tipo CLR | Ejemplo |
|---------------|----------|---------|
| `valueString` | `string` | `"Madrid"` |
| `valueDate` | `DateOnly` | `2024-03-15` |
| `valueDateTime` | `DateTimeOffset` | `2024-03-15T10:00:00Z` |
| `valueTime` | `TimeOnly` | `10:00:00` |
| `valuePhoneNumber` | `string` | `"+34 912 345 678"` |
| `valueNumber` / `valueInteger` | `double` / `long` | `123.45` |
| `valueBoolean` | `bool` | `true` |
| `valueArray` | `List<object>` | `[...]` |
| `valueObject` | `Dictionary<string, object>` | `{...}` |
| `content` | `string` | texto bruto del campo |

---

## 9. Proveedor mock (sin Azure)

Para desarrollo local o entornos sin conexión a Azure, cualquier tipología puede usar el proveedor `mock`.

El `MockExtraerDataProvider` devuelve datos precargados en código (por tipología) sin hacer ninguna llamada externa. Es el comportamiento por defecto cuando `Extraction:DefaultProvider = mock` y la tipología no tiene `provider` explícito.

Para forzar el mock en una tipología específica aunque el default sea Azure:

```json
"extraction": {
  "enabled": true,
  "provider": "mock",
  ...
}
```

---

## 10. Preguntas frecuentes

**¿Qué pasa si el analizador de Azure no devuelve un campo que está declarado en la tipología?**
El campo simplemente no aparece en `DatosExtraidos`. No se produce ningún error. La validación posterior puede marcar ese campo como faltante si tiene `required: true`.

**¿Qué pasa si `ModeloConfigs` no contiene la clave referenciada en la tipología?**
`ExtractionModelRegistryLoader.GetModel()` lanza `KeyNotFoundException` y la activity falla con un error descriptivo. El `models.json` físico solo serviría como seed de un entorno nuevo.

---

## Cambios recientes (marzo 2026)

- Se ha actualizado el modelo registrado para la tipología `nota.simple.1_4` a un analizador real exportado: el `analyzerId` ahora es `CU_NS_1.4_2` y su `processingLocation` es `geography` (antes `nota-simple-1-4` / `global`).

- Se confirmó mediante validación del schema del analizador (`CU_NS_1.4_2`) que los 28 campos principales coinciden con los nombres presentes en `nota.simple.1_4.validation.json`, por lo que el mapeo automático funciona sin `fieldMappings` adicionales.

- Ajustes de validación realizados en la tipología:
  - `CalificacionUrbanistica`: se añadió el valor `Urbana` al enum para cubrir un posible valor devuelto por el analizador.
  - `CuotaParticipacion`: se cambió la regex para aceptar formatos porcentuales producidos por el analizador (`100%`, `33.33%`, etc.). Regex actual: `^\\d+(\\.\\d+)?%$`.

Implicaciones:

- Actualiza `ModeloConfigs` con el `analyzerId` y `processingLocation` correctos antes de ejecutar en entorno apuntando al recurso Azure.
- Si se despliega en un entorno distinto (staging/prod), valida que el registro de BD de ese entorno contiene el modelo apropiado. Mantén `models.json` solo como seed o referencia.


**¿Qué pasa si `Endpoint` no está configurado y se usa `azure-content-understanding`?**
`AzureContentUnderstandingProvider` lanza `InvalidOperationException` con el mensaje `"Extraction:AzureContentUnderstanding:Endpoint es obligatorio"` en el arranque de la Function App.

**¿Se puede usar un analizador diferente para distintos entornos (dev/staging/prod)?**
Sí. El `analyzerId` está en `ModeloConfigs` para cada entorno. Se puede mantener un `models.json` diferente como seed inicial, pero la decisión operativa debe quedar publicada en BD.

**¿La llamada a Azure es síncrona o asíncrona?**
La llamada usa `WaitUntil.Completed`, que hace que el SDK espere a que Azure finalice el análisis antes de devolver. Es una operación de larga duración (puede tardar segundos o minutos según el documento). Al estar dentro de una Durable Activity, el orquestador no se bloquea: la Activity se ejecuta en su propio worker y el orquestador continúa cuando recibe el resultado.

**¿Cómo puedo ver el JSON raw que devuelve Azure?**
El log de la Function App con nivel `Debug` o `Trace` mostrará el JSON completo. También se puede instrumentar `AzureContentUnderstandingProvider` temporalmente para volcar `operation.Value.ToString()`.

---

## 11. Clasificación por defecto con Azure Document Intelligence

Desde marzo 2026, la clasificación del orquestador usa por defecto **Azure Document Intelligence** cuando no se indique lo contrario en el contrato de entrada.

Precedencia para clasificación:

1. `Instrucciones.Classification.Provider` y `Instrucciones.Classification.Model` (si no son `auto`).
2. `Classification:DefaultProvider` y `Classification:DefaultModelKey`.

Regla importante de negocio:

- Si `Instrucciones.ExpectedType` viene informado, se **omite** la activity de clasificación y se fuerza:
  - `Modelo = expectedtype-input`
  - `Confianza = 1.0`
  - `TipologiaDetectada = ExpectedType`

Configuración mínima en `appsettings.json` / variables de entorno:

```json
{
  "Classification": {
    "DefaultProvider": "azure-document-intelligence",
    "DefaultModelKey": "default.azure-di",
    "AzureDocumentIntelligence": {
      "Endpoint": "https://<tu-recurso>.cognitiveservices.azure.com/",
      "ApiKey": "<api-key>",
      "ApiVersion": "2024-11-30"
    }
  }
}
```

Registro de modelos de clasificación:

- Archivo: `DocumentIA.Functions/config/classification/models.json`
- Ejemplo:

```json
{
  "models": [
    {
      "key": "default.azure-di",
      "provider": "azure-document-intelligence",
      "classifierId": "CHANGEME_CLASSIFIER_ID",
      "apiVersion": "2024-11-30"
    }
  ]
}
```

Nota de diseño:

- La tipología **no** selecciona el modelo de clasificación. La clasificación ocurre antes y es precisamente la que determina la tipología del documento.

---

## 12. Copiar un analizador con la Copy API (sin reentrenar)

La **Copy API de Content Understanding** copia el analizador **completo** —schema, configuración (extractiva/generativa, prompts, clasificación/segmentación) **y el estado entrenado ("knowledge")**— **sin reentrenar ni reconstruir nada**. El analizador destino queda **funcionalmente idéntico** al origen: mismos inputs → mismos resultados.

> [!WARNING]
> **Estado real (verificado 2026-07-17): la Copy API CROSS-RESOURCE NO funciona en este entorno.**
> `grantCopyAuthorization` responde `200` pero con un cuerpo **sin el campo `source`** (stub), y el `:copy`
> devuelve siempre `ModelNotFound / "has not granted the necessary permissions"`, incluso tras: (a) corregir
> el casing del RG, (b) reintentos esperando propagación, y (c) **asignar `Cognitive Services User` a la
> identidad administrada del recurso destino sobre el origen** — que es lo que exige el *pull* cross-resource
> (lo hace la MI del destino, no tu usuario). El flujo con token (`:getCopyAuthorization`) da `404` en esta
> api-version. Todo apunta a una limitación de servicio con analizadores **project-scoped de Foundry**;
> pendiente de **caso de soporte Azure**.
>
> **Método de réplica cross-resource que SÍ funciona** (y el que se usó de verdad el 1-jun-2026):
> **reconstruir/reentrenar** el analizador en el destino desde su definición (`PUT create-or-replace`),
> reentrenando desde el blob de datos etiquetados. Usa **`scripts/deployment/recreate-cu-analyzer.ps1`**
> ([12.2b](#122b-réplica-cross-resource-real-recreatereentrenar)). No es un snapshot bit a bit, pero es
> funcionalmente equivalente. **La copia intra-recurso** (same-resource, snapshot/rollback, [12.6]) **sí
> funciona** porque no cruza recursos.

Cubre dos escenarios de este proyecto:

| Escenario | Modo | Pasos | Para qué |
|-----------|------|-------|----------|
| **Replicar entre regiones** | cross-resource | 2 (autorizar + copiar) | Sweden Central → West Europe, para failover / reparto de carga |
| **Snapshot / rollback** ([12.6](#126-copia-intra-recurso-snapshot-de-rollback)) | same-resource | 1 (copiar) | Clonar a un ID nuevo para congelar un estado. **No permite editar campos** |
| **Añadir / quitar campos** ([12.7](#127-añadir-o-quitar-campos-de-un-analizador-existente)) | — | — | Editar el schema en el **proyecto** de Studio y reconstruir. La copia **no** sirve para esto |

> **¿Queda igual de entrenado?** Sí. Es una copia del snapshot entrenado, no un nuevo entrenamiento. Ver los matices en [12.4](#124-garantías-y-matices).

### 12.1 Recursos reales

El CU **primario** está en **Sweden Central** y el **secundario** en **West Europe**. Cuando se crea o reentrena un analizador en Sweden hay que **replicarlo** al de West Europe.

| Rol | Recurso | Región | Endpoint |
|-----|---------|--------|----------|
| **Origen** (primario) | `upe48-mm2avmdm-swedencentral` | `swedencentral` | `https://upe48-mm2avmdm-swedencentral.services.ai.azure.com/` |
| **Destino** (secundario) | `srbaisrv-westeurope` | `westeurope` | `https://srbaisrv-westeurope.services.ai.azure.com/` |

- Subscription: `647c7246-54bc-4d31-b909-431cacf03272` (*Producción Central*).
- Resource group: `SRBRGDOCSAIPROD` (ambos recursos). El casing que devuelve `az` varía (`srbrgdocsaiprod` en el origen), pero los nombres de RG son *case-insensitive*: es el mismo grupo.
- Ambos recursos son **kind `AIServices`** (Foundry), SKU `S0`. Es un requisito: un recurso `FormRecognizer` (como `srbdiprodocai`) **no puede alojar analizadores de CU**.
- API version: `2025-11-01`.
- Analizador actual de nota simple: `CU_NS_1.5_0` (export en `scripts/arm/analyzer-CU_NS_1.5_0-export.json`).
- El endpoint de CU es el de **AI Foundry** (`*.services.ai.azure.com`), **no** el que devuelve `az cognitiveservices account show --query properties.endpoint` (que es `*.cognitiveservices.azure.com`). Confírmalo con:
  ```bash
  az cognitiveservices account show -n <recurso> -g SRBRGDOCSAIPROD \
      --query 'properties.endpoints."Content Understanding"' -o tsv
  ```

#### Permisos: hace falta **data plane**, no control plane

> [!IMPORTANT]
> La credencial necesita el rol **`Cognitive Services User`** sobre **ambos** recursos (origen y destino).
> **`Contributor` y `Cognitive Services Contributor` NO sirven**: su lista de `dataActions` está **vacía** — solo cubren control plane. Con ellos, cualquier llamada a `/contentunderstanding/*` devuelve **`401 PermissionDenied`** (p. ej. falta `Microsoft.CognitiveServices/accounts/MultiModalIntelligence/defaults/read`), aunque puedas crear y borrar el recurso entero desde el portal.

Comprobar qué roles tienes realmente sobre cada recurso:

```bash
az role assignment list --all --assignee $(az ad signed-in-user show --query id -o tsv) \
    --include-inherited --include-groups \
    --query "[].{role:roleDefinitionName, scope:scope}" -o table
```

Conceder el rol que falta (requiere `User Access Administrator` u `Owner` en el scope):

```bash
az role assignment create \
    --assignee $(az ad signed-in-user show --query id -o tsv) \
    --role "Cognitive Services User" \
    --scope "/subscriptions/647c7246-54bc-4d31-b909-431cacf03272/resourceGroups/SRBRGDOCSAIPROD/providers/Microsoft.CognitiveServices/accounts/srbaisrv-westeurope"
```

> Los roles granulares `Cognitive Service Content Understanding Owner/Contributor/Reader` (anunciados en mayo de 2026) **aún no están disponibles** en este tenant: `az role definition list --name "..."` devuelve vacío. Hasta entonces, `Cognitive Services User` es la vía.

La copia soporta cruzar subscriptions e incluso tenants, siempre que la credencial tenga el rol en ambos extremos.

#### Model deployments: los alias los resuelve `/contentunderstanding/defaults`

Los analizadores **no** referencian nombres de deployment, sino **alias** de modelo:

```jsonc
{ "analyzerId": "CU_NS_1.5_0", "models": { "completion": "gpt-4.1", "embedding": "text-embedding-3-large" } }
```

El alias → deployment lo resuelve el recurso vía `GET/PATCH /contentunderstanding/defaults`. Por eso **los nombres de deployment pueden diferir entre origen y destino** sin romper nada, siempre que los *defaults* del destino mapeen los mismos alias. Si no los mapean, **la copia se crea pero falla al analizar**.

Estado actual (los sufijos numéricos son aleatorios y **no coinciden** entre recursos):

| Alias | Origen (`swedencentral`) | Destino (`westeurope`) |
|-------|--------------------------|------------------------|
| `gpt-4.1` | `gpt-4.1-715420` · GlobalStandard · 150 | `gpt-4.1-892749` · GlobalStandard · 250 |
| `gpt-4.1-mini` | `gpt-4.1-mini-622960` · GlobalStandard · 250 | `gpt-4.1-mini-590191` · GlobalStandard · 250 |
| `text-embedding-3-large` | `text-embedding-3-large-030358` · GlobalStandard · 150 | `text-embedding-3-large-010650` · GlobalStandard · 250 |

CU exige los tres modelos (`gpt-4.1`, `gpt-4.1-mini`, `text-embedding-3-large`). El origen despliega además `gpt-4o` y uno llamado `gpt-4o-mini` que **en realidad sirve `gpt-4.1-mini`** (el nombre engaña); ninguno de los dos está en los *defaults*, así que no afectan a CU.

Consultar los defaults de un recurso:

```bash
TOKEN=$(az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv)
curl -s -H "Authorization: Bearer $TOKEN" \
    "https://srbaisrv-westeurope.services.ai.azure.com/contentunderstanding/defaults?api-version=2025-11-01"
```

Para alinearlos, usa `-SyncDefaults` ([12.2](#122-script-recomendado)) en lugar de hacerlo a mano.

### 12.2 Script recomendado

`scripts/deployment/copy-cu-analyzer.ps1` cubre **los dos modos**, con poll de la operación de larga duración y verificación final. Soporta API key o Entra ID (`az login`).

```powershell
# --- Replicar Sweden -> West Europe (cross-resource), con Entra ID ---
./scripts/deployment/copy-cu-analyzer.ps1 `
    -SourceAnalyzerId CU_NS_1.5_0 `
    -ResourceGroup SRBRGDOCSAIPROD

# --- Replicar alineando primero los defaults de modelo del destino ---
./scripts/deployment/copy-cu-analyzer.ps1 `
    -SourceAnalyzerId CU_NS_1.5_0 `
    -ResourceGroup SRBRGDOCSAIPROD `
    -SyncDefaults

# --- Replicar con API keys explícitas ---
./scripts/deployment/copy-cu-analyzer.ps1 `
    -SourceAnalyzerId CU_NS_1.5_0 `
    -ResourceGroup SRBRGDOCSAIPROD `
    -SourceKey <key-sweden> -TargetKey <key-westeurope>

# --- Versionar en el mismo recurso (same-resource) ---
./scripts/deployment/copy-cu-analyzer.ps1 -SameResource `
    -SourceAnalyzerId CU_NS_1.5_0 `
    -TargetAnalyzerId CU_NS_1.6_0
```

**Preflight (automático).** Antes de tocar nada, el script valida y aborta con un mensaje accionable si algo falta:

1. Data plane accesible en el **origen** (si no: imprime el `az role assignment create` exacto — ver [12.1](#121-recursos-reales)).
2. El analizador origen **existe**, y extrae los alias de modelo que usa.
3. Data plane accesible en el **destino**.
4. Los *defaults* del destino **resuelven esos alias**. Si no, aborta sugiriendo `-SyncDefaults` — evita el fallo silencioso de "el analizador se copia pero no analiza".

Notas de uso:

- **Cross-resource:** `-ResourceGroup` es obligatorio. Por defecto `TargetAnalyzerId = SourceAnalyzerId` (mismo ID en ambas regiones), lo que simplifica el registro en `ModeloConfigs`.
- **`-SameResource`:** no usa `-ResourceGroup` ni endpoint destino (el destino es el propio recurso origen). `-TargetAnalyzerId` es **obligatorio** y debe ser **distinto** del origen.
- **`-SyncDefaults`** (solo cross-resource): replica los *defaults* de modelo del origen en el destino **mapeando por modelo subyacente**, no por nombre de deployment (los sufijos difieren entre recursos). Si un modelo del origen no está desplegado en el destino, lo avisa en vez de dejar un mapeo roto. Es idempotente: relanzarlo no rompe nada.
- **`-SkipPreflight`**: omite las validaciones. Solo para depurar.
- Los defaults del script ya apuntan a los recursos reales de [12.1](#121-recursos-reales); solo hay que sobreescribirlos para otro entorno.

> [!IMPORTANT]
> Para **replicar entre regiones (cross-resource)**, `copy-cu-analyzer.ps1` **no funciona hoy** (ver aviso al inicio de §12). Usa `recreate-cu-analyzer.ps1` ([12.2b](#122b-réplica-cross-resource-real-recreatereentrenar)). El modo **same-resource** (snapshot/rollback) de `copy-cu-analyzer.ps1` sí funciona.

### 12.2b Réplica cross-resource real: recreate/reentrenar (recomendado)

Mientras la Copy API cross-resource siga bloqueada, la réplica Sweden → West Europe se hace **reconstruyendo** el analizador en el destino desde su definición (`PUT create-or-replace`, api-version `2025-11-01`). El servicio **reentrena** desde `knowledgeSources` (los datos etiquetados en blob). El resultado es funcionalmente equivalente al origen, aunque **no es un snapshot bit a bit**: valida con documentos de prueba antes de repuntar el registro.

Script: **`scripts/deployment/recreate-cu-analyzer.ps1`** (GET definición del origen → quita campos read-only → `PUT` en destino → poll del build hasta `ready`).

```powershell
# --- Replicar Sweden -> West Europe reconstruyendo/reentrenando en el destino ---
./scripts/deployment/recreate-cu-analyzer.ps1 `
    -SourceAnalyzerId CU_NS_1.6_0_GGAA `
    -ResourceGroup SRBRGDOCSAIPROD `
    -SyncDefaults

# --- Desde un export local (scripts/arm/…), sobrescribiendo si ya existe ---
./scripts/deployment/recreate-cu-analyzer.ps1 `
    -FromExport scripts/arm/analyzer-CU_NS_1.5_0-export.json `
    -SourceAnalyzerId CU_NS_1.5_0 -ResourceGroup SRBRGDOCSAIPROD -SyncDefaults -Force
```

> [!IMPORTANT]
> **Prerrequisito de este método:** el recurso **destino** debe poder **leer el blob de datos etiquetados**
> con **su identidad administrada**. Es decir, la MI de `srbaisrv-westeurope` necesita el rol
> **`Storage Blob Data Reader`** sobre la cuenta de storage del etiquetado (`srbstgproapppdocai`). Ya está
> concedido (fue lo que habilitó la réplica del 1-jun-2026). Si faltara, el build termina en `failed` por no
> poder leer los documentos de `knowledgeSources`.

- **`-SyncDefaults`**: igual que en `copy-cu-analyzer.ps1`, alinea los alias de modelo del destino antes de reconstruir. Necesario si el destino no mapea `gpt-4.1` / `text-embedding-3-large` a un deployment.
- **`-Force`**: sobrescribe el analizador destino si ya existe (`allowReplace=true`).
- **`-FromExport <ruta>`**: usa una definición local (p. ej. un export de `scripts/arm/`) en vez de hacer GET al origen.
- Al versionar, este método encaja con la política de "**construir desde el proyecto**" ([12.7](#127-añadir-o-quitar-campos-de-un-analizador-existente)): la versión nueva ya se construye reentrenando, así que replicarla al secundario reentrenando es coherente.

### 12.3 Pasos REST manuales (equivalentes al script)

**Paso 1 — Grant Copy Authorization (sobre el ORIGEN, Sweden):**
```http
POST https://upe48-mm2avmdm-swedencentral.services.ai.azure.com/contentunderstanding/analyzers/CU_NS_1.5_0:grantCopyAuthorization?api-version=2025-11-01
Content-Type: application/json
Ocp-Apim-Subscription-Key: {key-sweden}

{
  "targetAzureResourceId": "/subscriptions/647c7246-54bc-4d31-b909-431cacf03272/resourceGroups/{RG}/providers/Microsoft.CognitiveServices/accounts/srbaisrv-westeurope",
  "targetRegion": "westeurope"
}
```
Devuelve un token de autorización con `expiresAt` (caduca en 24 h).

**Paso 2 — Copy (sobre el DESTINO, West Europe):** operación de larga duración, se poll-ea el `Operation-Location`.
```http
POST https://srbaisrv-westeurope.services.ai.azure.com/contentunderstanding/analyzers/CU_NS_1.5_0:copy?api-version=2025-11-01
Content-Type: application/json
Ocp-Apim-Subscription-Key: {key-westeurope}

{
  "sourceAzureResourceId": "/subscriptions/647c7246-54bc-4d31-b909-431cacf03272/resourceGroups/{RG}/providers/Microsoft.CognitiveServices/accounts/upe48-mm2avmdm-swedencentral",
  "sourceAnalyzerId": "CU_NS_1.5_0",
  "sourceRegion": "swedencentral"
}
```

**Paso 3 — Verificar (sobre el DESTINO):**
```http
GET https://srbaisrv-westeurope.services.ai.azure.com/contentunderstanding/analyzers/CU_NS_1.5_0?api-version=2025-11-01
Ocp-Apim-Subscription-Key: {key-westeurope}
```

> Alternativa SDK: `scripts/azure_contentunderstanding_sample.py` usa el SDK de Python; los métodos equivalentes son `grant_copy_authorization` (origen) y `begin_copy_analyzer` (destino). Firma .NET: `GrantCopyAuthorizationAsync` + `CopyAnalyzerAsync(WaitUntil.Completed, ...)`.

### 12.4 Garantías y matices

- **Igual de entrenado:** la copia replica el estado entrenado; el destino produce los mismos resultados que el origen para los mismos documentos. No hay reentrenamiento.
- **Es un snapshot puntual:** si más adelante **reentrenas o modificas** el analizador en Sweden, el de West Europe **no** se actualiza solo → hay que **volver a ejecutar la copia**.
- **Los `defaults` de modelo NO se copian:** la Copy API replica el analizador, pero el mapeo alias → deployment es **configuración del recurso destino** ([12.1](#121-recursos-reales)). Si el destino no mapea los alias que el analizador usa, la copia se crea con `status` correcto y **falla en tiempo de análisis**. Por eso el preflight lo comprueba y `-SyncDefaults` lo corrige.
- **Analizadores referenciados:** si el analizador usa clasificación/segmentación que referencia a otros analizadores, hay que **copiar también esos referenciados**.
- **Naming de versiones:** al versionar (`CU_NS_1.5_0` → `CU_NS_1.6_0`), la versión nueva se **construye desde el proyecto** ([12.7](#127-añadir-o-quitar-campos-de-un-analizador-existente)), no se obtiene copiando la anterior. Una vez construida y probada, se replica a West Europe y se actualiza el registro correspondiente.
- **La copia no cambia el schema:** el analizador destino tiene exactamente los mismos campos que el origen. Si necesitas campos distintos, la copia no es la herramienta ([12.6](#126-copia-intra-recurso-snapshot-de-rollback)).

### 12.5 Registro en BD tras la copia

Copiar el analizador **no** cambia la configuración de la aplicación. Para que el CU secundario se use realmente, el registro debe estar publicado en `ModeloConfigs` (ver [sección 4](#4-configuración-y-seed)). El proyecto usa `modelKey` (primario, Sweden) y `secondaryModelKey` (secundario, West Europe); p. ej. `nota.simple.1_4.azure-cu.sweden` / `nota.simple.1_4.azure-cu.westeurope`, cada uno con su `processingLocation` (`swedencentral` / `westeurope`) apuntando al mismo `analyzerId` copiado.

### 12.6 Copia intra-recurso: snapshot de rollback

> [!WARNING]
> **La copia intra-recurso NO sirve para añadir ni quitar campos.** Replica el analizador con su `fieldSchema` **congelado**, y un analizador construido es **inmutable**: la API GA no expone ningún `PATCH`/update de `fieldSchema`. Para cambiar campos, ve a [12.7](#127-añadir-o-quitar-campos-de-un-analizador-existente).

Para lo que **sí** sirve: crear un **clon con un ID nuevo** que congela el estado actual, como *backup* o punto de rollback antes de publicar una versión nueva. Es la variante **de un solo paso** de la Copy API (no requiere `grantCopyAuthorization`, que es solo para cruzar recursos). El analizador original queda intacto.

Con el script:

```powershell
./scripts/deployment/copy-cu-analyzer.ps1 -SameResource `
    -SourceAnalyzerId CU_NS_1.5_0 `
    -TargetAnalyzerId CU_NS_1.5_0_backup
```

REST equivalente (un solo POST, sin `grantCopyAuthorization`):

```http
POST https://upe48-mm2avmdm-swedencentral.services.ai.azure.com/contentunderstanding/analyzers/CU_NS_1.5_0_backup:copy?api-version=2025-11-01
Content-Type: application/json
Ocp-Apim-Subscription-Key: {key-sweden}

{ "sourceAnalyzerId": "CU_NS_1.5_0" }
```

SDK equivalentes: Python `begin_copy_analyzer(analyzer_id="CU_NS_1.5_0_backup", source_analyzer_id="CU_NS_1.5_0")` · .NET `CopyAnalyzerAsync(WaitUntil.Completed, "CU_NS_1.5_0_backup", "CU_NS_1.5_0")`.

### 12.7 Añadir o quitar campos de un analizador existente

Los campos **no** se editan sobre el analizador, sino sobre el **proyecto** de Content Understanding Studio del que se construyó. Modelo mental:

```
Proyecto (Studio)  ──build──▶  Analyzer v1   ← artefacto inmutable
   │  schema                   Analyzer v2   ← otro build del mismo proyecto
   │  datos etiquetados        Analyzer v3
   └── vive en Blob Storage
```

Un **proyecto** produce **N analizadores**. Es el patrón que ya sigue este repositorio:

| Analizadores | `tags.projectId` compartido |
|---|---|
| `CU_NS_1.4_3`, `CU_NS_1.5_0` | `01ecc742-3215-4bf8-bdc2-ea7a7ef00fd1` (Nota Simple) |
| `CERA16`, `CERA16_v1` | `30189708-d5c2-48ab-8ecf-beb626f8fabb` |

> **El proyecto no es un objeto de la API.** La API GA `2025-11-01` expone **un único grupo de operaciones: `Content Analyzers`** — no hay grupo `Projects`. El proyecto son blobs, y el analizador los referencia:
> ```jsonc
> "knowledgeSources": [{ "kind": "labeledData",
>   "containerUrl": "https://srbstgproapppdocai.blob.core.windows.net/documentai",
>   "prefix": "labelingProjects/{projectId}/train" }],
> "tags": { "projectId": "{projectId}" }
> ```
> Duplicar un proyecto implicaría copiar ese prefijo a un GUID nuevo con `azcopy` (requiere **Storage Blob Data Contributor** sobre `srbstgproapppdocai`). Es **no soportado y frágil**: Studio mantiene su propio registro. **Normalmente no hace falta** — ver la nota de seguridad abajo.

**Flujo recomendado (Studio):**

1. Abre [CU Studio](https://aka.ms/cu-studio) y entra en el proyecto correspondiente (para Nota Simple, `projectId` `01ecc742-…`).
2. **Edita el schema**: añade o elimina campos.
3. **Etiqueta los campos nuevos** en la pestaña *Knowledge* / *Label data*. *Auto label* prerellena con el analizador actual y tú corriges. Sin este paso los campos nuevos van *zero-shot*.
4. **Build analyzer** con un ID de versión nuevo (p. ej. `CU_NS_1.6_0`).
5. **Prueba** contra documentos representativos.
6. **Replica** a West Europe con la copia cruzada ([12.2](#122-script-recomendado)) y **repunta** `modelKey` en `ModeloConfigs`. La versión anterior queda como rollback.

> [!NOTE]
> **Editar el proyecto no afecta a los analizadores ya construidos.** La documentación es explícita: *"After you add samples, rebuild the analyzer so the analyzer can use the samples"*. `CU_NS_1.5_0` sigue sirviendo en producción con su snapshot mientras iteras el schema para la 1.6.

**Atajo por API (solo si no necesitas etiquetar).** La [guía de migración](https://learn.microsoft.com/azure/ai-services/content-understanding/how-to/migration-preview-to-ga#update-analyzers) documenta este patrón, y es la única vía sin Studio:

```http
GET  /contentunderstanding/analyzers/CU_NS_1.5_0?api-version=2025-11-01
     -> editas fieldSchema.fields (añades/quitas), conservas knowledgeSources y models
PUT  /contentunderstanding/analyzers/CU_NS_1.6_0?api-version=2025-11-01
```

Ojo con dos cosas: para **reutilizar un ID** hay que **borrar** antes el analizador existente; y los campos **nuevos** creados así van **zero-shot** (ningún ejemplo etiquetado los cubre), así que la calidad será peor que por Studio. Es una vía razonable para **eliminar** campos, y floja para añadirlos.
