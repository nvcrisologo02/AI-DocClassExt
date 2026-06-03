# Catalogo de App Settings (vivo)

> Generado automaticamente por `scripts/generate-config/generate-appsettings-catalog.ps1`  
> Fecha: 2026-06-03 16:33:12  
> Fuente: escaneo regex sobre `src/**/*.cs` (patrones `IConfiguration["..."]`, `GetSection`, `GetValue<T>`, `Environment.GetEnvironmentVariable`)

## Como regenerar

```powershell
pwsh ./scripts/generate-config/generate-appsettings-catalog.ps1
```

## Resumen por categoria

| Categoria | Numero de claves |
|---|---:|
| AssetResolver Plugin | 3 |
| Azure Storage | 1 |
| Base de datos | 3 |
| Bootstrapping | 1 |
| Frontend Admin | 1 |
| GDC (SOAP) | 5 |
| Otros | 18 |
| Runtime ASP.NET | 1 |
| **TOTAL** | **33** |

## AssetResolver Plugin

| Clave | Usada en |
|---|---|
| `ApiKey` | [src/plugins/DocumentIA.AssetResolver/Middleware/ApiKeyMiddleware.cs](src/plugins/DocumentIA.AssetResolver/Middleware/ApiKeyMiddleware.cs) |
| `AssetResolver:ApiKey` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs)<br>[src/backend/DocumentIA.Functions/Services/SystemHealthService.cs](src/backend/DocumentIA.Functions/Services/SystemHealthService.cs) |
| `AssetResolver:BaseUrl` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs)<br>[src/backend/DocumentIA.Functions/Services/SystemHealthService.cs](src/backend/DocumentIA.Functions/Services/SystemHealthService.cs) |

## Azure Storage

| Clave | Usada en |
|---|---|
| `AzureStorageConnectionString` | [src/backend/DocumentIA.Core/Services/BlobStorageService.cs](src/backend/DocumentIA.Core/Services/BlobStorageService.cs) |

## Base de datos

| Clave | Usada en |
|---|---|
| `ConnectionStrings__DocumentIA` | [src/backend/DocumentIA.Data/Context/DocumentIADbContextFactory.cs](src/backend/DocumentIA.Data/Context/DocumentIADbContextFactory.cs) |
| `ConnectionStrings:DocumentIA` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs)<br>[src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs](src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs) |
| `SqlConnectionString` | [src/backend/DocumentIA.Data/Context/DocumentIADbContextFactory.cs](src/backend/DocumentIA.Data/Context/DocumentIADbContextFactory.cs)<br>[src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs)<br>[src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs](src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs) |

## Bootstrapping

| Clave | Usada en |
|---|---|
| `RunDatabaseMigrationsOnStartup` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |

## Frontend Admin

| Clave | Usada en |
|---|---|
| `FunctionsAdminApi:BaseUrl` | [src/frontend/DocumentIA.Admin/Services/SystemConfigService.cs](src/frontend/DocumentIA.Admin/Services/SystemConfigService.cs) |

## GDC (SOAP)

| Clave | Usada en |
|---|---|
| `GDC:BypassSslValidation` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `GDC:Endpoint` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs)<br>[src/backend/DocumentIA.Functions/Services/SystemHealthService.cs](src/backend/DocumentIA.Functions/Services/SystemHealthService.cs) |
| `GDC:HttpBasicPassword` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `GDC:HttpBasicUsername` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `GDC:TimeoutSeconds` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |

## Otros

| Clave | Usada en |
|---|---|
| `AZURE_FUNCTIONS_ENVIRONMENT` | [src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs](src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs) |
| `BlobRetention:BatchSize` | [src/backend/DocumentIA.Functions/Triggers/BlobCleanupTimerTrigger.cs](src/backend/DocumentIA.Functions/Triggers/BlobCleanupTimerTrigger.cs) |
| `BlobRetention:DefaultDays` | [src/backend/DocumentIA.Functions/Activities/PersistirActivity.cs](src/backend/DocumentIA.Functions/Activities/PersistirActivity.cs) |
| `Classification` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `Classification:Flows` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `ClassificationPreparation` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `DOCUMENTIA_ADMIN_URL` | [src/backend/DocumentIA.Tests.E2E/WizardE2ETestBase.cs](src/backend/DocumentIA.Tests.E2E/WizardE2ETestBase.cs) |
| `DOTNET_ENVIRONMENT` | [src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs](src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs) |
| `Extraction` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `Extraction:AzureContentUnderstanding` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `FieldAliases` | [src/plugins/DocumentIA.AssetResolver/Program.cs](src/plugins/DocumentIA.AssetResolver/Program.cs) |
| `GDC` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `HybridTdn` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `Performance` | [src/plugins/DocumentIA.AssetResolver/Program.cs](src/plugins/DocumentIA.AssetResolver/Program.cs) |
| `Performance:SqlCommandTimeoutSeconds` | [src/plugins/DocumentIA.AssetResolver/Program.cs](src/plugins/DocumentIA.AssetResolver/Program.cs) |
| `Pipeline` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `PromptDefaults` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `PromptTracing` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |

## Runtime ASP.NET

| Clave | Usada en |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | [src/frontend/DocumentIA.Admin/Services/SystemConfigService.cs](src/frontend/DocumentIA.Admin/Services/SystemConfigService.cs) |

---

## Notas

- Las claves con prefijo doble : indican secciones jerarquicas (ej. GDC:Endpoint).
- Para ver el valor concreto en cada entorno consultar Azure Portal -> Function App srbappprodocai -> Configuration, o el local.settings.json local.
- Los secretos referenciados via @Microsoft.KeyVault(...) se documentan en docs/INFRAESTRUCTURA_AZURE.md.
- Ver tambien .env.example (raiz del repo) y MANUAL_HEALTHCHECK.md para settings con probe especifico.

