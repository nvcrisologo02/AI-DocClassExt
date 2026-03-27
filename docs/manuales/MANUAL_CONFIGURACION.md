# Manual de Configuración — DocumentIA Functions

## 1. Visión general

El sistema se configura a través de dos ficheros:

| Fichero | Ámbito |
|---|---|
| `appsettings.json` | Valores por defecto (template, sin secretos). |
| `local.settings.json` | Entorno local. **No se sube al repositorio.** |
| Variables de entorno de Azure | Producción/staging en Azure Functions App Settings. |

Las secciones de configuración se enlazan a clases tipadas en `Program.cs` mediante `services.Configure<T>()`.

---

## 2. Base de datos

| Clave | Descripción | Ejemplo |
|---|---|---|
| `SqlConnectionString` | Connection string SQL Server. También se acepta `ConnectionStrings:DocumentIA`. | `Server=localhost,1433;Database=DocumentIA;User Id=sa;Password=...;TrustServerCertificate=True;` |
| `RunDatabaseMigrationsOnStartup` | Si `"true"`, aplica migraciones EF al arrancar. Útil en local. | `"true"` |

> **Producción:** desactivar `RunDatabaseMigrationsOnStartup` y aplicar migraciones con pipeline CI/CD.

---

## 3. Azure Storage / Blob

| Clave | Descripción |
|---|---|
| `AzureWebJobsStorage` | Requerida por Durable Functions. En local: `"UseDevelopmentStorage=true"` (Azurite). |
| `AzureStorageConnectionString` | Connection string del Storage Account para subida de blobs. En local igual que `AzureWebJobsStorage`. |

---

## 4. Clasificación

Sección raíz: `Classification`

### 4.1 Enrutamiento

| Clave | Clase | Valores | Descripción |
|---|---|---|---|
| `Classification:DefaultProvider` | `ClassificationRoutingSettings` | `azure-document-intelligence` \| `mock` | Proveedor activo de clasificación. |
| `Classification:DefaultModelKey` | `ClassificationRoutingSettings` | `default.azure-di` | Clave de modelo DI. Reservado para multi-modelo. |

### 4.2 Azure Document Intelligence

Sección: `Classification:AzureDocumentIntelligence`

| Clave | Tipo | Descripción | Default |
|---|---|---|---|
| `Endpoint` | string | URL del recurso DI. Ej: `https://xxx.cognitiveservices.azure.com/` | — |
| `ApiKey` | string | API Key del recurso. Vacío si se usa Managed Identity. | — |
| `ApiVersion` | string | Versión de la API DI. | `2024-11-30` |
| `PollIntervalMs` | int | Intervalo de polling para operaciones asíncronas (ms). | `1000` |
| `TimeoutSeconds` | int | Timeout total de la operación DI. | `120` |

### 4.3 GPT Fallback de Clasificación

Sección: `Classification:GptFallback`

| Clave | Tipo | Descripción | Default |
|---|---|---|---|
| `Enabled` | bool | Activa/desactiva el fallback GPT. **Rollback instantáneo**: poner a `false`. | `false` |
| `Endpoint` | string | URL Azure OpenAI. Ej: `https://xxx.openai.azure.com/` | — |
| `ApiKey` | string | API Key Azure OpenAI. | — |
| `AuthMode` | string | `ApiKey` \| `DefaultAzureCredential` | `ApiKey` |
| `DeploymentName` | string | Nombre del deployment GPT. Ej: `gpt-4o-mini` | — |
| `FallbackThreshold` | double | **Nivel 3 de la jerarquía de umbrales** (ver `CONFIANZA_AGREGADA.md §5.1`). Umbral de confianza DI de último recurso para activar fallback GPT cuando la petición y la tipología no especifican umbral. Rango `[0.0–1.0]`. `0.0` = solo en excepción. `1.0` = siempre. | `0.6` |
| `Temperature` | double | Temperatura del modelo. `0.0` = máximo determinismo. | `0.0` |
| `MaxTokens` | int | Máximo tokens en respuesta GPT. | `150` |
| `TimeoutSeconds` | int | Timeout de la llamada GPT. | `30` |

> **Comportamiento especial:** Si DI clasifica como `RESTO` (tipología genérica), el fallback GPT se activa **de forma obligatoria**, independientemente del umbral y de si `Enabled = false`.  
> Si GPT devuelve `Desconocido` o confianza < 0.3, el orquestador termina con `Estado = ERROR`.

---

## 5. Extracción

Sección raíz: `Extraction`

### 5.1 Enrutamiento

| Clave | Clase | Valores | Descripción |
|---|---|---|---|
| `Extraction:DefaultProvider` | `ExtractionRoutingSettings` | `azure-content-understanding` \| `mock` | Proveedor activo de extracción. |

### 5.2 Azure Content Understanding

Sección: `Extraction:AzureContentUnderstanding`

| Clave | Tipo | Descripción | Default |
|---|---|---|---|
| `Endpoint` | string | URL del recurso Azure AI. Ej: `https://xxx.services.ai.azure.com/` | — |
| `ApiKey` | string | API Key del recurso. | — |
| `AuthMode` | string | `ApiKey` \| `DefaultAzureCredential` | `ApiKey` |
| `DefaultProcessingLocation` | string | `global` \| `geography` | `global` |

### 5.3 GPT Fallback de Extracción

Sección: `Extraction:GptFallback`

| Clave | Tipo | Descripción | Default |
|---|---|---|---|
| `Enabled` | bool | Activa el fallback GPT cuando CU no supera el umbral. | `false` |
| `Endpoint` | string | URL Azure OpenAI. | — |
| `ApiKey` | string | API Key Azure OpenAI. | — |
| `AuthMode` | string | `ApiKey` \| `DefaultAzureCredential` | `ApiKey` |
| `DeploymentName` | string | Nombre del deployment GPT. | — |
| `MinFieldsRatio` | double | **Nivel 3 de la jerarquía de umbrales** (ver `CONFIANZA_AGREGADA.md §5.1`). Ratio mínimo de campos rellenos de último recurso cuando la petición y la tipología no especifican umbral. Si CU no llega, se activa GPT. Rango `[0.0–1.0]`. | `0.5` |
| `Temperature` | double | Temperatura del modelo. | `0.0` |
| `MaxTokens` | int | Máximo tokens en respuesta GPT. | `2000` |
| `TimeoutSeconds` | int | Timeout de la llamada GPT. | `60` |

---

## 6. GDC (Gestor Documental)

Sección raíz: `GDC`

| Clave | Tipo | Descripción |
|---|---|---|
| `Endpoint` | string | URL SOAP del servicio GDC. Ej: `https://host:8090/sintws/IDocService` |
| `TimeoutSeconds` | int/string | Timeout HTTP en segundos. |
| `ApplicationId` | string | ID de aplicación registrada en GDC. |
| `Username` | string | Usuario de servicio GDC. |
| `Password` | string | Contraseña del usuario GDC. |
| `NominalUser` | string | Usuario nominal (puede estar vacío). |
| `DocumentTypeId` | string | Tipo de objeto en GDC. Normalmente `"document"`. |
| `ContentFieldName` | string | Nombre del campo de contenido binario en GDC. Normalmente `"Content"`. |
| `OrigenDocumento` | string | Código de origen del documento. |
| `RepositoryId` | string | ID del repositorio GDC destino. |
| `RepositoryName` | string | Nombre del repositorio (usado si `RepositoryId` está vacío). |
| `ClaseExpediente` | string | Clase de expediente para la búsqueda adaptativa de duplicados. Ej: `"AI04"`. Dejar vacío para búsqueda sin filtro de clase. |
| `DefaultMatricula` | string | Matrícula por defecto cuando no se dispone de una. |
| `Servicer` | string | Código servicer. |
| `EntidadOrigen` | string | Código de entidad origen. |
| `ProcesoCarga` | string | Código proceso de carga. |
| `TipoExpediente` | string | Tipo de expediente GDC. |
| `Publico` | string | `"verdadero"` / `"falso"`. Visibilidad del documento en GDC. |
| `HttpBasicUsername` | string | Usuario HTTP Basic para autenticación en el proxy/gateway GDC. |
| `HttpBasicPassword` | string | Contraseña HTTP Basic. |

> **Nota:** En entorno de desarrollo con certificado autofirmado en el GDC, el handler HTTP omite la validación SSL (`DangerousAcceptAnyServerCertificateValidator`). En producción esto no aplica.

---

## 7. Configuración dinámica (BD + cache + seed)

La configuración operativa de tipologías, modelos y plugins se carga desde base de datos y se cachea en memoria para evitar reinicios y minimizar coste de infraestructura.

### 7.1 Fuente de verdad y estados

| Dominio | Tabla | Estados |
|---|---|---|
| Tipologías/versiones | `Tipologias` | `Draft` \| `Published` \| `Retired` |
| Modelos (clasificación/extracción/prompt) | `ModelosConfig` | `Draft` \| `Published` \| `Retired` |
| Plugins por tipología | `PluginTipologiaConfigs` | `Draft` \| `Published` \| `Retired` |

Solo la configuración en estado `Published` se utiliza en ejecución.

### 7.2 Cache en runtime

- Los loaders DB-backed usan `IMemoryCache` con TTL de 5 minutos.
- Se cachean por dominio (`modelos:*`, `tipologias:*`, `plugins:*`).
- Si se publica un cambio, puede tardar hasta 5 minutos en converger si no se fuerza recarga.

### 7.3 Seed inicial desde ficheros

En arranque, el servicio `ConfigurationSeedService` puede sembrar datos desde JSON para bootstrap inicial:

| Origen | Destino |
|---|---|
| `config/tipologias/*.validation.json` | `Tipologias` |
| `config/models/*.models.json` | `ModelosConfig` |
| `config/tipologias/*.plugins.json` | `PluginTipologiaConfigs` |

Esto permite mantener compatibilidad con artefactos históricos y acelerar provisión en nuevos entornos.

### 7.4 Modo fichero (compatibilidad)

Los loaders conservan constructor en modo fichero para pruebas unitarias y escenarios de compatibilidad local. En producción se recomienda modo BD.

### 7.5 Gestión COMPLETAR_GDC_HTTP_BASIC_USERNAMEistrativa

La gestión se realiza por API COMPLETAR_GDC_HTTP_BASIC_USERNAME (`/api/COMPLETAR_GDC_HTTP_BASIC_USERNAME/...`) y por la app `DocumentIA.Admin` para:

- guardar borradores,
- publicar,
- retirar configuración,
- listar estado actual.

---

## 8. Entorno local paso a paso

1. Instalar [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) para emular Azure Storage.
2. Levantar SQL Server local (Docker recomendado: `docker run -e ACCEPT_EULA=Y -e SA_PASSWORD=COMPLETAR_SQL_PASSWORD -p 1433:1433 mcr.microsoft.com/mssql/server`).
3. Copiar `local.settings.json` a partir de la plantilla del equipo y rellenar los valores de `Endpoint` y `ApiKey` de los servicios Azure.
4. Verificar que `RunDatabaseMigrationsOnStartup = "true"` para que EF aplique migraciones al arrancar.
5. Compilar y lanzar: tarea VS Code **"func: host start"** (usa `dotnet build` + `func host start`).

---

## 9. Variables de entorno en Azure (producción)

En Azure Functions App Settings, las secciones anidadas se escriben con `:` o `__`:

```
Classification:AzureDocumentIntelligence:Endpoint  = https://...
Classification:AzureDocumentIntelligence:ApiKey    = <key>
Classification:GptFallback:Enabled                 = true
Extraction:DefaultProvider                         = azure-content-understanding
GDC:Endpoint                                       = https://...
SqlConnectionString                                = Server=...
AzureWebJobsStorage                                = DefaultEndpointsProtocol=https;...
```

> **Recomendación:** usar Azure Key Vault references para secretos (`ApiKey`, `Password`, `SqlConnectionString`).

---

## 10. Autenticación con Managed Identity

Para evitar API Keys, establecer `AuthMode = "DefaultAzureCredential"` en las secciones de clasificación y extracción. Requiere rol `Cognitive Services User` sobre el recurso AI y `Cognitive Services OpenAI User` sobre Azure OpenAI asignados a la Managed Identity de la Function App.
