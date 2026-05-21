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
| `FallbackThreshold` | double | **Nivel 3 de la jerarquía de umbrales** (ver `../referencias/CONFIANZA_AGREGADA.md §5.1`). Umbral de confianza DI de último recurso para activar fallback GPT cuando la petición y la tipología no especifican umbral. Rango `[0.0–1.0]`. `0.0` = solo en excepción. `1.0` = siempre. | `0.6` |
| `Temperature` | double | Temperatura del modelo. `0.0` = máximo determinismo. | `0.0` |
| `MaxTokens` | int | Máximo tokens en respuesta GPT. | `150` |
| `TimeoutSeconds` | int | Timeout de la llamada GPT. | `30` |

> **Comportamiento especial:** Si DI clasifica como `RESTO` (tipología genérica), el fallback GPT se activa **de forma obligatoria**, independientemente del umbral y de si `Enabled = false`.  
> Si GPT devuelve `Desconocido` o confianza < 0.3, el orquestador termina de forma controlada con `Estado = NO_CLASIFICADO` (sin error técnico de ejecución).

---

## 5. Extracción

Sección raíz: `Extraction`

### 5.1 Enrutamiento

| Clave | Clase | Valores | Descripción |
|---|---|---|---|
| `Extraction:DefaultProvider` | `ExtractionRoutingSettings` | `azure-content-understanding` \| `azure-openai` \| `azure-document-intelligence` \| `mock` | Proveedor activo de extracción global. Puede ser sobreescrito por la configuración de cada tipología (`extraction.provider` en el JSON de validación / BD). |

### 5.2 Azure Content Understanding

Sección: `Extraction:AzureContentUnderstanding`

| Clave | Tipo | Descripción | Default |
|---|---|---|---|
| `Endpoint` | string | URL del recurso Azure AI. Ej: `https://xxx.services.ai.azure.com/` | — |
| `ApiKey` | string | API Key del recurso. | — |
| `AuthMode` | string | `ApiKey` \| `DefaultAzureCredential` | `ApiKey` |
| `DefaultProcessingLocation` | string | `global` \| `geography` | `global` |

### 5.3 GPT Fallback de Extracción / Extracción GPT directa

Sección: `Extraction:GptFallback`

> Esta sección configura **tanto el fallback GPT** (CU→GPT cuando la completitud/confianza no supera el umbral) **como el proveedor GPT directo** (`azure-openai`). Ambos modos comparten el registro de modelos (`ModelosConfig` en BD / `extraction-models.json`) y los mismos parámetros de conexión.

| Clave | Tipo | Descripción | Default |
|---|---|---|---|
| `Enabled` | bool | Activa el fallback GPT cuando CU no supera el umbral. No afecta al proveedor directo `azure-openai` (que siempre se activa si así está configurado en la tipología). | `false` |
| `Endpoint` | string | URL Azure OpenAI. | — |
| `ApiKey` | string | API Key Azure OpenAI. | — |
| `AuthMode` | string | `ApiKey` \| `DefaultAzureCredential` | `ApiKey` |
| `DeploymentName` | string | Nombre del deployment GPT. | — |
| `MinFieldsRatio` | double | **Nivel 3 de la jerarquía de umbrales** (ver `../referencias/CONFIANZA_AGREGADA.md §5.1`). Ratio mínimo de campos rellenos de último recurso cuando la petición y la tipología no especifican umbral. Si CU no llega, se activa GPT. Rango `[0.0–1.0]`. | `0.5` |
| `Temperature` | double | Temperatura del modelo. | `0.0` |
| `MaxTokens` | int | Máximo tokens en respuesta GPT. | `2000` |
| `TimeoutSeconds` | int | Timeout de la llamada GPT. | `60` |

### 5.4 Modos de extracción GPT

El sistema dispone de **dos modos distintos** de extracción basada en GPT:

| Modo | Activación | `FallbackUsado` en salida | Descripción |
|---|---|---|---|
| **Directo** (`azure-openai`) | Tipología tiene `extraction.provider = azure-openai` | `false` | GPT extrae directamente sin pasar por CU. Útil para tipologías donde CU no tiene analizador entrenado. |
| **Fallback** (CU→GPT) | Tipología usa CU pero completitud/confianza no supera umbral | `true` | GPT complementa la extracción cuando CU es insuficiente. |

**Validación temprana (modo directo):** Al iniciarse, se valida que el modelo esté completamente configurado (endpoint, deployment, API key si no usa Managed Identity). Un error en esta validación aparece como `InvalidOperationException` con un mensaje que indica exactamente qué clave de configuración falta, p. ej.:
```
Extracción GPT: modelo 'default.gpt4o-mini_ex' requiere ApiKey configurada cuando AuthMode=ApiKey.
Verifica la configuración en appsettings/KeyVault (ej: Extraction:GptFallback:ApiKey).
```

**Markdown en modo directo:** El proveedor directo intenta obtener el markdown del documento desde `DatosNormalizados` (si viene de un paso previo). Si no está disponible, GPT extrae con el mínimo contexto disponible (nombre del archivo). Para obtener mejores resultados con tipologías de extracción directa, asegurarse de que el pipeline no omite la clasificación o que el markdown ya viene en los datos de entrada.

### 5.5 Jerarquía de umbrales de fallback CU→GPT

> Esta jerarquía solo aplica al modo **fallback** (CU→GPT). El modo directo (`azure-openai`) no participa en esta lógica.

El sistema evalúa dos criterios independientes para decidir si la extracción CU es suficiente:

- **Completitud**: ratio de campos esperados (según la tipología) que están presentes en `DatosExtraidos`.
- **Confianza**: valor de `ConfianzaExtraccion` devuelto por CU.

Si cualquiera de los dos criterios no supera su umbral, se activa el fallback GPT.

La jerarquía de resolución para **cada criterio** (de mayor a menor prioridad) es:

| Nivel | Origen | Campo (completitud) | Campo (confianza) |
|---|---|---|---|
| 1 · petición | `instrucciones.extraction` (API/HTTP) | `umbralCompletitud` | `umbralConfianza` |
| 2 · tipología | `confidenceConfig` (JSON validación / BD) | `extracUmbralFallbackCompletitud` | `extracUmbralFallbackConfianza` |
| 3 · legado | `instrucciones.extraction` o `confidenceConfig` | `umbral` / `extracUmbralFallback` | ídem |
| 4 · global | `Extraction:GptFallback` (`appsettings.json`) | `MinFieldsRatio` | `MinFieldsRatio` |

> El campo legado `umbral` / `extracUmbralFallback` actúa como valor único para ambos criterios cuando los específicos no están informados.

#### Campos de tipología (`confidenceConfig` en el JSON de validación / BD)

| Campo JSON | Tipo | Descripción | Default |
|---|---|---|---|
| `extracUmbralFallback` | double? | Umbral legado: aplica a completitud y confianza si los específicos no están informados. | `null` |
| `extracUmbralFallbackCompletitud` | double? | Ratio mín. de campos esperados presentes (nivel tipología). Rango `[0–1]`. | `null` |
| `extracUmbralFallbackConfianza` | double? | Confianza CU mínima para no activar fallback (nivel tipología). Rango `[0–1]`. | `null` |

#### Campos de la petición HTTP (`instrucciones.extraction`)

| Campo JSON | Tipo | Descripción |
|---|---|---|
| `umbral` | double? | Umbral legado (aplica a completitud y confianza si los específicos no están informados). |
| `umbralCompletitud` | double? | Override de completitud para esta petición. Tiene precedencia sobre tipología. Omitir = usar tipología o global. |
| `umbralConfianza` | double? | Override de confianza para esta petición. Tiene precedencia sobre tipología. Omitir = usar tipología o global. |

**Ejemplo de petición con umbrales independientes:**

```json
{
  "instrucciones": {
    "extraction": {
      "umbralCompletitud": 0.7,
      "umbralConfianza": 0.85
    }
  }
}
```

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

La gestión se realiza por API management (`/api/management/...`) y por la app `DocumentIA.Admin` para:

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

---

## 11. Operativa EP5 en Tipologías (versionado, diff y clonado)

### 11.1 Comparación de versiones con filtro rápido

En `DocumentIA.Admin`, el detalle de tipología incluye el bloque **Comparar versiones (A-2)**:

- Selección de versión izquierda/derecha dentro de la misma familia.
- Resumen agregado (`Total`, `Added`, `Removed`, `Modified`).
- Filtro rápido por `ChangeType`: `Todos`, `Añadidos`, `Eliminados`, `Modificados`.
- Expansión por fila para revisar `LeftValue` vs `RightValue` sin desbordamiento horizontal.

APIs asociadas:

- `GET /api/management/tipologias/{id}/versions`
- `GET /api/management/tipologias/{id}/diff/{otherId}`

### 11.2 Clonar una tipología existente para crear una nueva

Patrón recomendado: **exportar -> importar -> ajustar -> publicar**.

1. Exportar tipología base (`GET /api/management/tipologias/{id}/export`).
2. Importar ZIP (`POST /api/management/tipologias/import` con `zipBase64`).
3. Ajustar en `Draft`:
  - `codigo` (si cambia familia),
  - `version`,
  - `nombre`,
  - `ConfiguracionJson` y plugins.
4. Validar diff contra versión base y ejecutar prueba de ingesta.
5. Publicar (`POST /api/management/tipologias/{id}/publicar`).

### 11.3 Recomendaciones de control de cambios

- Mantener una única modificación funcional por versión cuando sea posible.
- Revisar `A-3` (auditoría) tras cada publicación para garantizar trazabilidad.
- Evitar edición manual de JSON seed para cambios productivos; la fuente de verdad es BD.
