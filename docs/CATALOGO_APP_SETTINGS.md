# Catalogo de App Settings (vivo)

> Generado automaticamente por `scripts/generate-config/generate-appsettings-catalog.ps1`  
> Fecha: 2026-05-01 01:52:08  
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
| Base de datos | 2 |
| Bootstrapping | 1 |
| Frontend Admin | 2 |
| GDC (SOAP) | 5 |
| Otros | 8 |
| Runtime ASP.NET | 1 |
| **TOTAL** | **23** |

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
| `ConnectionStrings:DocumentIA` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs)<br>[src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs](src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs) |
| `SqlConnectionString` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs)<br>[src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs](src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs) |

## Bootstrapping

| Clave | Usada en |
|---|---|
| `RunDatabaseMigrationsOnStartup` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |

## Frontend Admin

| Clave | Usada en |
|---|---|
| `FunctionsAdminApi:BaseUrl` | [src/frontend/DocumentIA.Admin/Program.cs](src/frontend/DocumentIA.Admin/Program.cs)<br>[src/frontend/DocumentIA.Admin/Services/SystemConfigService.cs](src/frontend/DocumentIA.Admin/Services/SystemConfigService.cs) |
| `FunctionsAdminApi:FunctionKey` | [src/frontend/DocumentIA.Admin/Program.cs](src/frontend/DocumentIA.Admin/Program.cs) |

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
| `Classification` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `DOCUMENTIA_ADMIN_URL` | [src/backend/DocumentIA.Tests.E2E/WizardE2ETestBase.cs](src/backend/DocumentIA.Tests.E2E/WizardE2ETestBase.cs) |
| `DOTNET_ENVIRONMENT` | [src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs](src/backend/DocumentIA.Functions/Triggers/Admin/ConfigurationAdminFunction.cs) |
| `Extraction` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `FieldAliases` | [src/plugins/DocumentIA.AssetResolver/Program.cs](src/plugins/DocumentIA.AssetResolver/Program.cs) |
| `GDC` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `PromptDefaults` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `Pipeline` | [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs) |
| `Pipeline:MaxPaginasDocumento` | Límite global de páginas por documento. 0 = sin límite. Puede sobreescribirse por tipología con `maxPaginasDocumento` en `ConfiguracionJson`. |

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

