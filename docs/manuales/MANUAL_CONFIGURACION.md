# Manual de Configuración — DocumentIA Functions

> Última revisión: 2026-11-25 (AB#101450, retirada de los app settings heredados sin efecto; AB#101553, validación TLS del GDC contra la CA de SAREB; pendiente de despliegue, DEV primero). Revisiones previas: 2026-11-22 (AB#101447, contraste con `Program.cs`, las clases de opciones y `azure-pipelines.yml`). Este manual manda en **app settings por entorno**; la Parte B de [05_MANUAL_USO_CONFIGURACION.md](../05_MANUAL_USO_CONFIGURACION.md) manda en contrato, `ConfiguracionJson`, modelos y alias de IA.
>
> **Settings heredados retirados (AB#101450, 2026-11-25).** Los 27 app settings `Classification__GptFallback__*`, `Classification__AzureDocumentIntelligence__*`, `Classification__DefaultModelKey`, `Extraction__GptFallback__*` y los de conexión de `Extraction__AzureContentUnderstanding__*` (`Endpoint`, `ApiKey`, `AuthMode`, `DefaultProcessingLocation`) ya no existen en el pipeline ni en el contrato `scripts/config/azure-appsettings-contract.json` (que los prohíbe) ni en los entornos: DEV desde su despliegue (pendiente del despliegue a DEV, fecha al verificar); PRE y PRO con su siguiente despliegue. El pipeline los borra en cada despliegue si siguen presentes. No se enlazaban a ninguna clase (comprobado el 2026-11-22 en `Program.cs`): los parámetros de conexión, modelo y umbrales de IA salen de las filas de `ModeloConfigs` resueltas por alias e identidad administrada. De `Extraction__AzureContentUnderstanding__*` solo quedan los siete de resiliencia (`MaxConcurrentCalls`, `HardTimeoutSeconds`, `EnableCircuitBreaker`, `CircuitBreakerFailureThreshold`, `CircuitBreakerOpenSeconds`, `MaxRetries`, `InitialRetryDelayMs`). Las secciones 4.2, 4.3 y 5.3 y la fila de conexión de 5.2 se conservan solo como registro de lo retirado.
>
> **Modelo generativo por entorno:** lo deciden las filas `UseAsFallback` de `ModeloConfigs` y el app setting `PromptDefaults__ModelKey` (DEV `prompt.gpt56-luna` desde el 2026-11-20, ADR-004; PRE y PRO `default.gpt4o-mini`, deployment `gpt-4o-mini` = modelo gpt-4.1-mini, hasta la siguiente release).

## 1. Visión general

El sistema se configura por app settings (Azure) o `local.settings.json` (local):

| Fichero | Ámbito |
|---|---|
| `appsettings.json` | Solo lo lee `DocumentIADbContextFactory` en tiempo de diseño y las pruebas; **el host de Functions no lo carga** (`Program.cs` usa `HostBuilder` sin `appsettings`). Los valores por defecto efectivos son los de las clases de opciones. |
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
| `Classification:DefaultFlow` | `ClassificationRoutingSettings` | `hybrid-rules-gpt-di` (defecto de clase: `hybrid-tdn`) | Flujo que se ejecuta con `provider = auto`: secuencia de proveedores de `Classification:Flows`. En Azure el pipeline no fija `DefaultFlow` ni `Flows`, así que rige el defecto de la clase. |
| `Classification:DefaultProvider` | `ClassificationRoutingSettings` | `hybrid-tdn` \| `gpt` \| `azure-document-intelligence` \| `rules` \| `mock` | Proveedor usado solo si no hay `DefaultFlow`. El pipeline fija `azure-document-intelligence`, sin efecto práctico mientras exista `DefaultFlow`. |
| `Classification:UseGlobalFallback` / `GlobalFallbackProvider` | `ClassificationRoutingSettings` | `true` / `gpt` | Fallback final cuando el flujo no da resultado. |

### 4.2 Azure Document Intelligence (retirado en AB#101450)

Sección: `Classification:AzureDocumentIntelligence`. **Retirada en AB#101450**: no se enlazaba a ninguna clase; endpoint, clave, `AuthMode` y versión de API salen de la fila de `ModeloConfigs` (`ClassificationModelConfig`, alias `di`). Los app settings `Classification__AzureDocumentIntelligence__*` ya no existen en pipeline, contrato ni entornos.

### 4.3 GPT Fallback de Clasificación (retirado en AB#101450)

Sección: `Classification:GptFallback`. **Retirada en AB#101450**: no se enlazaba a ninguna clase. El modelo de respaldo es la fila con `UseAsFallback = true` de tipo Clasificación en `ModeloConfigs` (`classification.gpt56-luna` en DEV, `classification.gpt4o-mini-fallback` en PRE y PRO) y el umbral es `FallbackThreshold` de esa fila (0,6 por defecto); `Enabled` no apagaba nada. Los app settings `Classification__GptFallback__*` ya no existen en pipeline, contrato ni entornos.

> **Comportamiento especial:** Si DI clasifica como `RESTO` (tipología genérica), el fallback GPT se activa **de forma obligatoria**, independientemente del umbral y de si `Enabled = false`.  
> Si GPT devuelve `Desconocido` o confianza < 0.3, el orquestador termina de forma controlada con `Estado = NO_CLASIFICADO` (sin error técnico de ejecución). Si la petición incluye `instrucciones.prompt` o `forzarResumenPorDefecto`, el prompt y el resumen se ejecutan igualmente antes de cerrar: no dependen de que el documento sea clasificable.

---

## 5. Extracción

Sección raíz: `Extraction`

### 5.1 Enrutamiento

| Clave | Clase | Valores | Descripción |
|---|---|---|---|
| `Extraction:DefaultProvider` | `ExtractionRoutingSettings` | `azure-content-understanding` \| `azure-openai` \| `azure-document-intelligence` \| `mock` | Proveedor activo de extracción global. Puede ser sobreescrito por la configuración de cada tipología (`extraction.provider` en el JSON de validación / BD). |

### 5.2 Azure Content Understanding

Sección: `Extraction:AzureContentUnderstanding` (`AzureContentUnderstandingOptions`). Solo se enlazan los parámetros de resiliencia; endpoint, clave, `AuthMode` y `processingLocation` van por fila de `ModeloConfigs` (`ExtractionModelConfig`).

| Clave | Tipo | Descripción | Default de clase · valor del pipeline |
|---|---|---|---|
| `MaxConcurrentCalls` | int | Llamadas concurrentes a CU | 2 · 4 |
| `HardTimeoutSeconds` | int | Timeout duro por intento (`CU.HardTimeout`) | 300 · 90 |
| `EnableCircuitBreaker` | bool | Circuit breaker por `modelKey` con failover al secundario | `true` · `true` |
| `CircuitBreakerFailureThreshold` | int | Fallos para abrir | 5 · 5 |
| `CircuitBreakerOpenSeconds` | int | Tiempo abierto | 45 · 45 |
| `MaxRetries` | int | Reintentos en transitorios (429/500/502/503/504) | 2 · 3 |
| `InitialRetryDelayMs` | int | Retardo inicial del backoff | 500 · 500 |
| `Endpoint`, `ApiKey`, `AuthMode`, `DefaultProcessingLocation` | — | **Retirados en AB#101450**: el proveedor usa `model.Endpoint` / `model.ApiKey` / `model.AuthMode` de la fila de `ModeloConfigs` y `processingLocation` vacío cae a `global` | — |

### 5.3 GPT Fallback de Extracción / Extracción GPT directa (retirado en AB#101450)

Sección: `Extraction:GptFallback`. **Retirada en AB#101450**: no se enlazaba a ninguna clase. Endpoint, deployment, clave y `MinFieldsRatio` salen de la fila `UseAsFallback` de tipo Extracción en `ModeloConfigs` (`extraction.gpt56-luna` en DEV; en PRE y PRO la de gpt-4o-mini) y el proveedor directo resuelve `ModelKey` contra `ModeloConfigs`. Los app settings `Extraction__GptFallback__*` ya no existen en pipeline, contrato ni entornos.

> Esta sección describía **tanto el fallback GPT** (CU→GPT cuando la completitud/confianza no supera el umbral) **como el proveedor GPT directo** (`azure-openai`). Ambos modos comparten el registro de modelos en BD (`ModeloConfigs`) y los mismos parámetros de conexión. Los JSON de modelos son solo seed o referencia.

| Clave | Tipo | Descripción | Default |
|---|---|---|---|

### 5.4 Modos de extracción GPT

El sistema dispone de **dos modos distintos** de extracción basada en GPT:

| Modo | Activación | `FallbackUsado` en salida | Descripción |
|---|---|---|---|
| **Directo** (`azure-openai`) | Tipología tiene `extraction.provider = azure-openai` | `false` | GPT extrae directamente sin pasar por CU. Útil para tipologías donde CU no tiene analizador entrenado. |
| **Fallback** (CU→GPT) | Tipología usa CU pero completitud/confianza no supera umbral | `true` | GPT complementa la extracción cuando CU es insuficiente. |

**Validación temprana (modo directo):** Al iniciarse, se valida que el modelo esté completamente configurado (endpoint, deployment, API key si no usa Managed Identity). Un error en esta validación aparece como `InvalidOperationException` con un mensaje que indica exactamente qué clave de configuración falta, p. ej.:
```
Extracción GPT: modelo 'default.gpt4o-mini_ex' requiere ApiKey configurada cuando AuthMode=ApiKey.
Verifica la configuración en appsettings/KeyVault (campo ApiKey de la fila de ModeloConfigs).
```
(En DEV la fila equivalente es `default.gpt56-luna_ex`; la clave vive en la fila de `ModeloConfigs` o se sustituye por `AuthMode = DefaultAzureCredential`.)
```
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
| 4 · modelo | fila `UseAsFallback` de tipo Extracción en `ModeloConfigs` (`ExtractionModelConfig.MinFieldsRatio`) | `MinFieldsRatio` | `MinFieldsRatio` |

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
| `BypassSslValidation` | bool | `false` por defecto y en el pipeline. `true` es solo un interruptor de emergencia: el cliente GDC acepta cualquier certificado y el arranque escribe el aviso `[GDC] AVISO`. |
| `TrustedRootCertificate` | string | PEM opcional con la raíz y la emisora de confianza; si se informa, sustituye al fichero sin desplegar. |
| `TrustedRootCertificatePath` | string | Ruta del PEM de confianza. Por defecto `config/certs/sareb-ca-chain.pem`. |

> **Nota:** desde el despliegue de AB#101553 (pendiente: DEV primero; PRE y PRO con la siguiente release) el handler HTTP del GDC valida la cadena del certificado contra `config/certs/sareb-ca-chain.pem` (raíz y emisora de SAREB, raíz válida hasta 2040-12-16) con `CustomRootTrust` y sin comprobación de revocación. El pipeline fija `GDC__BypassSslValidation=false`. `true` queda solo como interruptor manual de emergencia: acepta cualquier certificado, deja el aviso `[GDC] AVISO` en el log de arranque y el siguiente despliegue lo devuelve a `false`. `GDC__TrustedRootCertificate` (PEM) sustituye al fichero sin desplegar. Un fichero ausente o corrupto impide arrancar el host. Hasta ese despliegue, los tres entornos siguen con `true`. `GDC__BypassSslValidation` admite solo `true` o `false` (otro valor impide arrancar el host). Un secreto de Key Vault `GDC--BypassSslValidation` o `GDC--TrustedRootCertificate` pisaría el app setting (el proveedor de Key Vault se carga el último) y el pipeline no lo restablecería; hoy no existe ninguno en los tres Key Vault (comprobado el 2026-11-25). Las credenciales HTTP Basic (`GDC:HttpBasicUsername` / `GDC:HttpBasicPassword`) vienen del Key Vault del entorno.

---

## 7. Configuración dinámica (BD + cache + seed)

La configuración operativa de tipologías, modelos y plugins se carga desde base de datos y se cachea en memoria para evitar reinicios y minimizar coste de infraestructura.

### 7.1 Fuente de verdad y estados

| Dominio | Tabla | Estados |
|---|---|---|
| Tipologías/versiones | `Tipologias` | `Draft` \| `Published` \| `Retired` |
| Modelos (clasificación/extracción/prompt/layout) | `ModeloConfigs` | Activo/Inactivo |
| Plugins por tipología | `PluginTipologiaConfigs` | `Draft` \| `Published` \| `Retired` |

Solo la configuración publicada/activa en BD se utiliza en ejecución. Los ficheros JSON del repositorio no son fuente de verdad operativa.

### 7.2 Cache en runtime

- Los loaders DB-backed usan `IMemoryCache` con TTL de 5 minutos.
- Se cachean por dominio (`modelos:*`, `tipologias:*`, `plugins:*`).
- Si se publica un cambio, puede tardar hasta 5 minutos en converger si no se fuerza recarga.

### 7.3 Seed inicial desde ficheros

En arranque, el servicio `ConfigurationSeedService` puede sembrar datos desde JSON para bootstrap inicial:

| Origen | Destino |
|---|---|
| `config/tipologias/*.validation.json` | `Tipologias` |
| `config/classification/models.json` | `ModeloConfigs` (`TipoModelo.Clasificacion`) |
| `config/extraction/models.json` | `ModeloConfigs` (`TipoModelo.Extraccion`) |
| `config/prompt/models.json` | `ModeloConfigs` (`TipoModelo.Prompt`) |
| `config/layout/models.json` | `ModeloConfigs` (`TipoModelo.Layout`) |
| `config/tipologias/*.plugins.json` | `PluginTipologiaConfigs` |

Esto permite mantener compatibilidad con artefactos históricos y acelerar provisión en nuevos entornos. En entornos con BBDD ya inicializada, estos ficheros pueden estar desactualizados respecto a la configuracion publicada; no deben usarse para validar el comportamiento real ni borrarse sin confirmacion explicita.

### 7.4 Modo fichero (compatibilidad)

Los loaders conservan constructor en modo fichero para pruebas unitarias y escenarios de compatibilidad local. En producción se recomienda modo BD.

### 7.5 Gestión Administrativa

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
4. `RunDatabaseMigrationsOnStartup` vacío, ausente o `true` aplica las migraciones al arrancar en local; en Azure el pipeline lo fija a `false` y las migraciones van por el pipeline Migrations-BD.
5. Compilar y lanzar: tarea VS Code **"func: host start"** (usa `dotnet build` + `func host start`).

---

## 9. Variables de entorno en Azure (producción)

En Azure Functions App Settings, las secciones anidadas se escriben con `:` o `__`:

```
FUNCTIONS_WORKER_RUNTIME                 = dotnet-isolated
SecretsSource / KeyVaultName             = AzureVault / srbkv<env>docai
EnvironmentName                          = Development | Preproduction | Production
AI__Resources__openai_primary__Endpoint  = https://... (y cu_primary, cu_secondary, di)
PromptDefaults__ModelKey                 = prompt.gpt56-luna (dev) | default.gpt4o-mini (pre, pro)
Extraction__DefaultProvider              = azure-content-understanding
Extraction__AzureContentUnderstanding__MaxConcurrentCalls = 4 (y el resto de resiliencia)
AzureOpenAIResilience__*                 = reintentos y circuit breaker de Azure OpenAI
Dedup__ModoEstricto                      = true
DocumentIntelligence__UseInlineContent   = true (dev, pre) | false (pro)
BlobRetention__BatchSize                 = 2000 (dev, pre) | 5000 (pro)
PromptTracing__Enabled / IncludePromptText / MaxPromptTextChars = true / true / 20000
DOTNET_GCHeapHardLimitPercent            = 28
GDC__Endpoint / GDC__BypassSslValidation = https://... / false
SqlConnectionString, AzureWebJobsStorage, AzureStorageConnectionString = @Microsoft.KeyVault(...)
```

Los 83 settings que fija `azure-pipelines.yml` (los de arriba; los heredados sin efecto se retiraron en AB#101450 y el número bajará cuando el pipeline se despliegue) están en INFRAESTRUCTURA_REAL_DESPLEGADA.md. No se fijan y rigen los defectos de clase: `CacheConfiguration:*` (caché de configuración), `Pipeline:MaxPaginasDocumento` (0 = sin límite), `HybridTdn:*`, `ClassificationPreparation:*`, `Extraction:InitialMarkdown:*`.

> **Recomendación:** usar Azure Key Vault references para secretos (`ApiKey`, `Password`, `SqlConnectionString`).

### 9.1 `EnvironmentName`

App setting no secreto que declara el entorno del Function App (`Development` / `Preproduction` / `Production`). Lo consume `GET management/configuration` (campo `environment`) con esta prioridad: `EnvironmentName` → `AZURE_FUNCTIONS_ENVIRONMENT` → `DOTNET_ENVIRONMENT` → `"Unknown"`.

No se usa `AZURE_FUNCTIONS_ENVIRONMENT` como fuente principal porque, en un Function App ya desplegado, esa variable también cambia el comportamiento del propio host (fallback a base de datos InMemory cuando vale `Development`); `EnvironmentName` permite declarar el entorno de cara al Admin sin ese efecto secundario.

Lo fija el pipeline (variable `ENVIRONMENT_NAME`: dev=`Development`, pre=`Preproduction`, prod=`Production`) en `azure-pipelines.yml` y también en `azure-pipelines-admin.yml` (paso "Ensure Functions environment name", idempotente), de forma que el aviso de entorno del Admin funciona aunque solo se despliegue el Admin.

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

