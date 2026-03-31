# 4. Manual de Explotacion — DocumentIA MVP

> Ultima actualizacion: 2026-03-31  
> Proyecto: AI DocClassExt — SAREB

---

## 4.1 Requisitos de Infraestructura

### 4.1.1 Entorno Local (Desarrollo)

| Componente | Version minima | Instalacion |
|-----------|---------------|-------------|
| .NET SDK | 8.0 | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Azure Functions Core Tools | v4.x | `npm install -g azure-functions-core-tools@4 --unsafe-perm true` |
| Docker Desktop | 4.x+ | [docker.com](https://www.docker.com/products/docker-desktop/) |
| Azure CLI | 2.50+ | `winget install Microsoft.AzureCLI` (solo para deploy) |
| dotnet-ef (EF Tools) | 8.0+ | `dotnet tool install --global dotnet-ef` |
| PowerShell | 5.1+ / 7+ | Preinstalado en Windows |

### 4.1.2 Recursos Azure (Produccion)

| Recurso | Nombre | Region | Proposito |
|---------|--------|--------|----------|
| Resource Group | `SRBRGDOCSAIPROD` | West Europe | Contenedor de recursos |
| Function App | `srbappprodocai` | West Europe | Backend (Consumption Plan, Linux) |
| Storage Account | `srbstgproapppdocai` | West Europe | Durable Functions hub (AzureWebJobsStorage) |
| Storage Account | `srbstgprodocai` | West Europe | Almacenamiento de documentos (blobs) |
| Document Intelligence | `srbdiprodocai` | West Europe | Clasificacion de documentos |
| Azure OpenAI | `upe48-mm2avmdm` | Sweden Central | GPT-4o-mini (fallback clasif/extrac + prompt) |
| Content Understanding | `upe48-mm2avmdm` | Sweden Central | Extraccion de campos |
| Application Insights | `srbappiprodocai` | West Europe | Telemetria y monitorizacion |
| Key Vault | (pendiente) | West Europe | Secretos (EP7 roadmap) |
| Azure SQL | (pendiente) | — | BD productiva (actualmente Docker SQL local) |

---

## 4.2 Instalacion Local Paso a Paso

### Paso 1: Clonar repositorio

```powershell
git clone <URL_REPOSITORIO> documento-ia-clasificacion-mvp
cd documento-ia-clasificacion-mvp
```

### Paso 2: Iniciar servicios Docker

```powershell
docker-compose up -d
```

Esto arranca:
- **Azurite** (emulador Azure Storage): puertos 10000 (Blob), 10001 (Queue), 10002 (Table)
- **SQL Server 2022** (Developer): puerto 1433, usuario `sa`, password en docker-compose

Verificar que ambos contenedores estan corriendo:

```powershell
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

### Paso 3: Aplicar migraciones de base de datos

Las migraciones se aplican automaticamente al iniciar la Function App si `RunDatabaseMigrationsOnStartup=true` en `local.settings.json`. Alternativamente, aplicar manualmente:

```powershell
cd src\backend\DocumentIA.Functions
dotnet ef database update --project ..\DocumentIA.Data\DocumentIA.Data.csproj --startup-project .
```

Verificar conectividad:

```powershell
.\scripts\check-database.ps1
```

### Paso 4: Configurar local.settings.json

El archivo `local.settings.json` ya viene configurado para desarrollo local con Azurite y Docker SQL. Verificar/ajustar:

| Setting | Valor local por defecto |
|---------|----------------------|
| `AzureWebJobsStorage` | `UseDevelopmentStorage=true` |
| `SqlConnectionString` | `Server=localhost,1433;Database=DocumentIA;User Id=sa;Password=<TU_PASSWORD>;TrustServerCertificate=True;` |
| `AzureStorageConnectionString` | `UseDevelopmentStorage=true` |
| `Classification:DefaultProvider` | `azure-document-intelligence` |
| `Extraction:DefaultProvider` | `azure-content-understanding` |

**API Keys de AI**: Completar con las claves de los servicios Azure AI correspondientes. Consultar con el equipo.

### Paso 5: Compilar plugins de enriquecimiento

```powershell
.\scripts\compile-all-plugins.ps1
```

Esto compila `SarebEnrichments.dll` y la copia a `plugins/`.

### Paso 6: Iniciar la Function App

```powershell
cd src\backend\DocumentIA.Functions
func start
```

O usar la tarea de VS Code: `Ctrl+Shift+B` (ejecuta `build (functions)` + `func: host start`).

La primera vez, la app:
1. Ejecuta migraciones EF Core (auto-create de la BD `DocumentIA`).
2. Carga seed data de tipologias desde `config/`.
3. Inicia el host de Functions con los endpoints disponibles.

### Paso 7: Verificar endpoints

```powershell
# Verificar que la Function App esta corriendo
Invoke-RestMethod http://localhost:7071/api/tipologias | ConvertTo-Json -Depth 5
```

### Paso 8 (Opcional): Iniciar frontend Desktop

```powershell
cd src\frontend\DocumentIA.Desktop
dotnet run
```

### Paso 9 (Opcional): Iniciar frontend Admin

```powershell
cd src\frontend\DocumentIA.Admin
dotnet run --launch-profile http
```

---

## 4.3 Scripts Disponibles

| Script | Proposito | Uso |
|--------|----------|-----|
| `1 setup-folders.ps1` | Crear estructura de carpetas inicial del proyecto | `.\scripts\1 setup-folders.ps1` |
| `2 setup-config-files.ps1` | Inicializar archivos de configuracion | `.\scripts\2 setup-config-files.ps1` |
| `3 setup-docs.ps1` | Crear estructura de documentacion | `.\scripts\3 setup-docs.ps1` |
| `4 setup-dev-tools.ps1` | Instalar/configurar herramientas de desarrollo | `.\scripts\4 setup-dev-tools.ps1` |
| `5 setup-ci-cd.ps1` | Configurar pipeline CI/CD | `.\scripts\5 setup-ci-cd.ps1` |
| `activate-pim.ps1` | Activar rol PIM (Privileged Identity Management) via device code | `.\scripts\activate-pim.ps1 -UseUser` |
| `list-pim-eligible.ps1` | Listar asignaciones PIM elegibles | `.\scripts\list-pim-eligible.ps1 -UseUser` |
| `check-database.ps1` | Verificar conectividad y estado de la BD SQL | `.\scripts\check-database.ps1` |
| `compile-all-plugins.ps1` | Compilar todas las DLLs de enrichments y copiar a `plugins/` | `.\scripts\compile-all-plugins.ps1` |
| `deploy-manual.ps1` | Deploy manual a Azure (build, zip, opcional Kudu upload) | `.\scripts\deploy-manual.ps1 [-KuduUser '$x' -KuduPassword 'y']` |
| `set-app-settings.ps1` | Configurar Application Settings en Azure via CLI | `.\scripts\set-app-settings.ps1` |
| `list-analyzers.ps1` | Listar analizadores disponibles en Document Intelligence | `.\scripts\list-analyzers.ps1` |
| `run-analyze.ps1` | Ejecutar analisis DI sobre un documento | `.\scripts\run-analyze.ps1` |
| `test-plugin-integration.ps1` | Probar integracion de plugins custom | `.\scripts\test-plugin-integration.ps1` |
| `verify-program-config.ps1` | Verificar configuracion de la aplicacion | `.\scripts\verify-program-config.ps1` |
| `azure_contentunderstanding_sample.py` | Ejemplo Python de Azure Content Understanding | `python .\scripts\azure_contentunderstanding_sample.py` |

### Scripts de Mock Servers

| Script | Proposito |
|--------|----------|
| `scripts\Mock Servers\start-mock-servers.ps1` | Iniciar servidores mock para desarrollo sin servicios reales |
| `scripts\Mock Servers\stop-mock-servers.ps1` | Detener servidores mock |

---

## 4.4 Variables de Entorno y Configuracion

### 4.4.1 local.settings.json (Desarrollo Local)

| Variable | Tipo | Descripcion | Requerida |
|----------|------|------------|-----------|
| **Infraestructura** | | | |
| `AzureWebJobsStorage` | string | Connection string Storage para Durable Functions hub | Si |
| `FUNCTIONS_WORKER_RUNTIME` | string | Runtime: `dotnet-isolated` | Si |
| `SqlConnectionString` | string | Connection string SQL Server | Si |
| `AzureStorageConnectionString` | string | Connection string Storage para documentos (blobs) | Si |
| `RunDatabaseMigrationsOnStartup` | bool | Aplicar migraciones EF Core al iniciar | Si |
| **Clasificacion** | | | |
| `Classification:DefaultProvider` | string | Proveedor: `azure-document-intelligence` / `mock` | Si |
| `Classification:DefaultModelKey` | string | Clave del modelo DI en el registro (`default.azure-di`) | Si |
| `Classification:AzureDocumentIntelligence:Endpoint` | string | URL del recurso Document Intelligence | Si* |
| `Classification:AzureDocumentIntelligence:ApiKey` | string | API Key de Document Intelligence | Si* |
| `Classification:AzureDocumentIntelligence:AuthMode` | string | `ApiKey` / `DefaultAzureCredential` | Si |
| `Classification:AzureDocumentIntelligence:ApiVersion` | string | Version API DI (`2024-11-30`) | Si |
| `Classification:GptFallback:Enabled` | bool | Habilitar fallback GPT para clasificacion | No |
| `Classification:GptFallback:Endpoint` | string | URL Azure OpenAI | Cond. |
| `Classification:GptFallback:ApiKey` | string | API Key Azure OpenAI | Cond. |
| `Classification:GptFallback:DeploymentName` | string | Deployment GPT (`gpt-4o-mini`) | Cond. |
| `Classification:GptFallback:FallbackThreshold` | double | Umbral confianza DI para activar fallback (0.0-1.0) | No |
| `Classification:GptFallback:Temperature` | double | Temperatura GPT (0.0 recomendado) | No |
| `Classification:GptFallback:MaxTokens` | int | Max tokens respuesta GPT | No |
| `Classification:GptFallback:TimeoutSeconds` | int | Timeout GPT en segundos | No |
| **Extraccion** | | | |
| `Extraction:DefaultProvider` | string | Proveedor: `azure-content-understanding` / `mock` | Si |
| `Extraction:AzureContentUnderstanding:Endpoint` | string | URL Content Understanding | Si* |
| `Extraction:AzureContentUnderstanding:ApiKey` | string | API Key CU | Si* |
| `Extraction:AzureContentUnderstanding:AuthMode` | string | `ApiKey` / `DefaultAzureCredential` | Si |
| `Extraction:AzureContentUnderstanding:DefaultProcessingLocation` | string | `geography` / `global` | No |
| `Extraction:GptFallback:Enabled` | bool | Habilitar fallback GPT para extraccion | No |
| `Extraction:GptFallback:Endpoint` | string | URL Azure OpenAI | Cond. |
| `Extraction:GptFallback:ApiKey` | string | API Key Azure OpenAI | Cond. |
| `Extraction:GptFallback:DeploymentName` | string | Deployment GPT | Cond. |
| `Extraction:GptFallback:MinFieldsRatio` | double | Ratio minimo de campos para NO activar fallback (0.0-1.0) | No |
| `Extraction:GptFallback:Temperature` | double | Temperatura GPT | No |
| `Extraction:GptFallback:MaxTokens` | int | Max tokens respuesta GPT | No |
| `Extraction:GptFallback:TimeoutSeconds` | int | Timeout GPT | No |
| **GDC** | | | |
| `GDC:Endpoint` | string | URL del servicio SOAP GDC SINTWS | Si |
| `GDC:TimeoutSeconds` | int | Timeout GDC en segundos | Si |
| `GDC:ApplicationId` | string | ID aplicacion GDC | Si |
| `GDC:Username` | string | Usuario servicio GDC | Si |
| `GDC:Password` | string | Password servicio GDC | Si |
| `GDC:HttpBasicUsername` | string | Usuario HTTP Basic Auth para GDC | Si |
| `GDC:HttpBasicPassword` | string | Password HTTP Basic Auth | Si |
| `GDC:BypassSslValidation` | bool | Omitir validacion SSL (solo desarrollo/red interna) | No |
| `GDC:DefaultMatricula` | string | Matricula por defecto para archivado GDC | Si |
| `GDC:ClaseExpediente` | string | Clase de expediente GDC | Si |
| `GDC:TipoExpediente` | string | Tipo expediente GDC | Si |
| `GDC:OrigenDocumento` | string | Codigo de origen documento | Si |
| `GDC:Servicer` | string | Codigo servicer | Si |
| `GDC:EntidadOrigen` | string | Codigo entidad origen | Si |
| `GDC:ProcesoCarga` | string | Codigo proceso de carga | Si |
| `GDC:Publico` | string | Visibilidad documento (`verdadero`/`falso`) | Si |

> **Si***: Requerido cuando el provider no es `mock`.  
> **Cond.**: Requerido cuando el fallback correspondiente esta habilitado (`Enabled=true`).

### 4.4.2 host.json (Configuracion del Runtime)

| Seccion | Parametro | Valor | Descripcion |
|---------|-----------|-------|------------|
| `extensions.durableTask` | `hubName` | `DocumentIAHub` | Nombre del hub de Durable Functions |
| | `maxConcurrentActivityFunctions` | `10` | Actividades simultaneas maximas |
| | `maxConcurrentOrchestratorFunctions` | `10` | Orquestadores simultaneos maximos |
| | `extendedSessionsEnabled` | `false` | Sesiones extendidas |
| | `tracing.traceInputsAndOutputs` | `false` | No trazar inputs/outputs (contienen PDF base64) |
| `logging.applicationInsights` | `samplingSettings.isEnabled` | `true` | Muestreo de telemetria |
| | `maxTelemetryItemsPerSecond` | `20` | Rate limit telemetria |
| `logging.logLevel` | `default` | `Warning` | Nivel log general |
| | `Function` | `Information` | Nivel log para funciones |

---

## 4.5 Despliegue en Azure

### 4.5.1 Orden de Aprovisionamiento

```mermaid
flowchart TD
    RG["1. Resource Group<br/>SRBRGDOCSAIPROD"] --> KV["2. Key Vault<br/>(pendiente)"]
    RG --> STG1["3. Storage Account<br/>srbstgproapppdocai<br/>(Durable hub)"]
    RG --> STG2["4. Storage Account<br/>srbstgprodocai<br/>(documentos)"]
    RG --> SQL["5. Azure SQL<br/>(pendiente — Docker temp)"]
    RG --> DI["6. Document Intelligence<br/>srbdiprodocai"]
    RG --> OAI["7. Azure OpenAI<br/>+ Content Understanding"]
    RG --> AI["8. Application Insights<br/>srbappiprodocai"]

    STG1 --> FA["9. Function App<br/>srbappprodocai"]
    STG2 --> FA
    SQL --> FA
    DI --> FA
    OAI --> FA
    AI --> FA
    KV --> FA
```

### 4.5.2 Procedimiento de Deploy

#### Opcion A: Deploy via script (con credenciales Kudu)

```powershell
# 1. Obtener credenciales Kudu desde Cloud Shell
az functionapp deployment list-publishing-credentials `
    --resource-group SRBRGDOCSAIPROD --name srbappprodocai `
    --query "{user:publishingUserName,pass:publishingPassword}" -o tsv

# 2. Ejecutar deploy con esas credenciales
.\scripts\deploy-manual.ps1 -KuduUser '$srbappprodocai' -KuduPassword '<PASSWORD>'
```

El script:
1. Compila `SarebEnrichments.dll` (plugin custom)
2. Copia DLL a `plugins/`
3. Ejecuta `dotnet publish --configuration Release`
4. Ajusta rutas de plugins a relativas (para Linux/Azure)
5. Crea ZIP con rutas Unix (requerido por Kudu)
6. Sube via Kudu zipdeploy

#### Opcion B: Deploy via Cloud Shell

```powershell
# 1. Ejecutar script sin parametros para generar zip
.\scripts\deploy-manual.ps1

# 2. Subir publish/functions.zip al Cloud Shell (boton Upload)

# 3. En Cloud Shell:
az functionapp deploy --resource-group SRBRGDOCSAIPROD --name srbappprodocai `
    --src-path ~/functions.zip --type zip
```

### 4.5.3 Configurar App Settings en Azure

```powershell
# Editar set-app-settings.ps1 con los valores de produccion (connection strings, API keys)
.\scripts\set-app-settings.ps1
```

El script aplica en 3 bloques:
1. **Infraestructura**: Storage, SQL, App Insights, runtime
2. **AI**: Classification (DI + GPT fallback) + Extraction (CU + GPT fallback)
3. **GDC**: Endpoint SOAP, credenciales, campos taxonomia

> **IMPORTANTE**: Cuando Key Vault este disponible, migrar los siguientes secretos:
> - `SqlConnectionString`
> - `Extraction__AzureContentUnderstanding__ApiKey`
> - `Extraction__GptFallback__ApiKey`
> - `Classification__AzureDocumentIntelligence__ApiKey`
> - `Classification__GptFallback__ApiKey`
> - `GDC__Password`, `GDC__HttpBasicPassword`

---

## 4.6 Operaciones Frecuentes

### 4.6.1 Verificar Estado de la Function App

```powershell
# Local
Invoke-RestMethod http://localhost:7071/api/tipologias | ConvertTo-Json

# Azure
$key = "<FUNCTION_KEY>"
Invoke-RestMethod "https://srbappprodocai.azurewebsites.net/api/tipologias?code=$key" | ConvertTo-Json
```

### 4.6.2 Enviar Documento para Procesamiento

```powershell
$body = @{
    documento = @{
        name = "nota_simple_test.pdf"
        content = @{
            base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("ruta\al\documento.pdf"))
        }
    }
    trazabilidad = @{
        correlationId = [guid]::NewGuid().ToString()
        submittedBy = "manual-test"
    }
} | ConvertTo-Json -Depth 5

$resp = Invoke-RestMethod -Method POST -Uri "http://localhost:7071/api/IngestDocument" `
    -ContentType "application/json" -Body $body

# Guardar instanceId para polling
$resp.instanceId
$resp.statusQueryUri
```

### 4.6.3 Consultar Estado de Procesamiento

```powershell
# Polling hasta completado
$statusUri = $resp.statusQueryUri
do {
    Start-Sleep -Seconds 2
    $status = Invoke-RestMethod $statusUri
    Write-Host "Estado: $($status.runtimeStatus) - $($status.customStatus.actividadActual)"
} while ($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending")

# Resultado final
$status.output | ConvertTo-Json -Depth 10
```

### 4.6.4 Forzar Reproceso de Documento Duplicado

```powershell
$body = @{
    instrucciones = @{ forceReprocess = $true }
    documento = @{ name = "doc.pdf"; content = @{ base64 = "..." } }
} | ConvertTo-Json -Depth 5
```

### 4.6.5 Verificar Base de Datos

```powershell
.\scripts\check-database.ps1
```

---

## 4.7 Backup y Restauracion

### 4.7.1 SQL Server Docker (Local)

```powershell
# Backup
docker exec documentia-sql /opt/mssql-tools18/bin/sqlcmd `
    -S localhost -U sa -P "COMPLETAR_SQL_PASSWORD" -C `
    -Q "BACKUP DATABASE [DocumentIA] TO DISK = '/var/opt/mssql/backup/DocumentIA.bak'"

# Restaurar
docker exec documentia-sql /opt/mssql-tools18/bin/sqlcmd `
    -S localhost -U sa -P "COMPLETAR_SQL_PASSWORD" -C `
    -Q "RESTORE DATABASE [DocumentIA] FROM DISK = '/var/opt/mssql/backup/DocumentIA.bak' WITH REPLACE"
```

### 4.7.2 Docker Volumes

```powershell
# Exportar volume de SQL
docker run --rm -v sql_data:/data -v ${PWD}:/backup busybox tar czf /backup/sql_data_backup.tar.gz -C /data .

# Importar
docker run --rm -v sql_data:/data -v ${PWD}:/backup busybox tar xzf /backup/sql_data_backup.tar.gz -C /data
```

### 4.7.3 Azure SQL (Produccion — cuando este disponible)

Azure SQL ofrece backup automatico (PITR — Point-In-Time Restore) con retencion de 7-35 dias. No requiere configuracion adicional.

### 4.7.4 Blob Storage

- **Produccion**: SSE habilitado, soft delete configurable, lifecycle management para archivado.
- **Local (Azurite)**: Volume Docker `azurite_data`. Backup igual que SQL volume.

---

## 4.8 Monitorizacion

### 4.8.1 Application Insights — Consultas KQL Utiles

**Documentos procesados en las ultimas 24h:**

```kusto
customEvents
| where timestamp > ago(24h)
| where name == "DocumentProcessed"
| summarize count() by bin(timestamp, 1h)
| render timechart
```

**Errores por actividad:**

```kusto
traces
| where timestamp > ago(24h)
| where severityLevel >= 3
| where message contains "Activity"
| summarize count() by message
| order by count_ desc
```

**Duracion promedio del pipeline:**

```kusto
customMetrics
| where timestamp > ago(7d)
| where name == "DuracionTotalMs"
| summarize avg(value), percentile(value, 95), max(value) by bin(timestamp, 1d)
```

**Ejecuciones con fallback AI activado:**

```kusto
traces
| where timestamp > ago(24h)
| where message contains "Fallback"
| project timestamp, message, severityLevel
| order by timestamp desc
```

### 4.8.2 Live Metrics

Acceder desde Azure Portal → Application Insights → Live Metrics para monitorear en tiempo real:
- Requests/second
- Failures
- Server response time
- Exceptions

### 4.8.3 Alertas Recomendadas

| Alerta | Condicion | Severidad |
|--------|-----------|-----------|
| Pipeline lento | DuracionTotalMs > 60000 (60s) | Warning |
| Tasa de errores alta | Failures > 10% en 5 min | Critical |
| GDC no disponible | GDC timeout consecutivos > 3 | Critical |
| Fallback frecuente | Fallback activaciones > 50% en 1h | Warning |
| BD no accesible | SQL connection failures > 0 en 5 min | Critical |

---

## 4.9 Rotacion de Claves

### 4.9.1 Estado Actual

Las claves estan en Application Settings de la Function App. No hay Key Vault configurado aun (roadmap EP7).

### 4.9.2 Claves a Rotar Periodicamente

| Clave | Servicio | Ubicacion actual | Frecuencia recomendada |
|-------|----------|-----------------|----------------------|
| API Key Document Intelligence | `srbdiprodocai` | App Settings | 90 dias |
| API Key Azure OpenAI | `upe48-mm2avmdm` | App Settings | 90 dias |
| API Key Content Understanding | `upe48-mm2avmdm` | App Settings | 90 dias |
| Password SQL Server | Docker / Azure SQL | App Settings | 90 dias |
| Credenciales GDC | GDC SINTWS | App Settings | Segun politica SAREB |
| Function Key | `srbappprodocai` | Azure Portal | 180 dias |
| Storage Account Keys | `srbstgprodocai`, `srbstgproapppdocai` | App Settings | 90 dias |

### 4.9.3 Procedimiento de Rotacion

1. Generar nueva clave en el servicio correspondiente (Azure Portal).
2. Actualizar `set-app-settings.ps1` con la nueva clave.
3. Ejecutar `.\scripts\set-app-settings.ps1`.
4. Verificar que la Function App funciona correctamente.
5. Revocar la clave antigua.

> **Futuro (con Key Vault)**: Las claves se almacenaran en Key Vault con rotacion automatica. Los App Settings referenciaran `@Microsoft.KeyVault(SecretUri=...)`.

---

## 4.10 Gestion de Tipologias sin Cambiar Codigo

### Paso 1: Crear archivos de configuracion

```
config/tipologias/
  mi-nueva-tipologia.validation.json    # Reglas de validacion
  mi-nueva-tipologia.plugins.json       # Plugins de integracion (opcional)
```

### Paso 2: Registrar tipologia via API Admin

```powershell
$body = @{
    codigo = "mi-nueva-tipologia"
    nombre = "Mi Nueva Tipologia"
    version = "1.0"
    umbralClasificacion = 0.85
    umbralExtraccion = 0.80
} | ConvertTo-Json

Invoke-RestMethod -Method POST -Uri "http://localhost:7071/management/tipologias" `
    -ContentType "application/json" -Body $body `
    -Headers @{ "x-functions-key" = "<FUNCTION_KEY>" }
```

### Paso 3: Registrar modelo AI (si es nuevo)

```powershell
$body = @{
    key = "mi-tipologia-cu-v1"
    tipo = "Extraccion"
    provider = "azure-content-understanding"
    modelo = "analyzer-mi-tipologia-v1"
    activo = $true
} | ConvertTo-Json

Invoke-RestMethod -Method POST -Uri "http://localhost:7071/management/modelos" `
    -ContentType "application/json" -Body $body `
    -Headers @{ "x-functions-key" = "<FUNCTION_KEY>" }
```

### Paso 4: Publicar tipologia

```powershell
Invoke-RestMethod -Method POST `
    -Uri "http://localhost:7071/management/tipologias/{id}/publicar" `
    -Headers @{ "x-functions-key" = "<FUNCTION_KEY>" }
```

### Paso 5: Verificar

```powershell
# Debe aparecer en la lista de tipologias publicadas
Invoke-RestMethod http://localhost:7071/api/tipologias | ConvertTo-Json
```

---

## 4.11 Referencias

| Documento | Contenido |
|-----------|-----------|
| [01_ARQUITECTURA_SISTEMA.md](01_ARQUITECTURA_SISTEMA.md) | Arquitectura y despliegue |
| [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md) | Configuracion tecnica detallada |
| [05_MANUAL_USO_CONFIGURACION.md](05_MANUAL_USO_CONFIGURACION.md) | Uso de API y configuraciones |
| [README-activate-pim.md](../scripts/README-activate-pim.md) | Guia PIM |
