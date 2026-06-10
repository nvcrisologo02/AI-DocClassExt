# Sistema de Clasificacion y Extraccion IA - MVP

Sistema modular de procesamiento de documentos con clasificacion y extraccion automatica basado en Azure AI Document Intelligence y Azure OpenAI.

## Arquitectura

- Backend: .NET 8.0 con Azure Durable Functions
- IA: Azure AI Document Intelligence + Azure OpenAI
- Entrenamiento: Python 3.10+
- Infraestructura: Azure (Bicep)

## Estructura del Proyecto

## Requisitos

### Backend (.NET)
- .NET 8.0 SDK
- Azure Functions Core Tools v4
- Visual Studio 2022 o VS Code con C# extension

### AI Models (Python)
- Python 3.10+
- pip o poetry
- Azure CLI

### Azure
- Suscripcion Azure activa
- Azure AI Document Intelligence
- Azure Functions
- Azure Storage Account
- Azure OpenAI (opcional)

## Setup Local

1. Clonar el repositorio
2. Configurar variables de entorno (copiar .env.example a .env)
3. Ejecutar scripts/setup-environment.sh o scripts/setup-environment.ps1
4. Restaurar dependencias .NET: dotnet restore src/backend/DocumentIA.sln
5. Instalar dependencias Python: pip install -r src/ai-models/requirements.txt

## Desarrollo

### Backend
`ash
cd src/backend
dotnet build
func start --csharp
```

### AI Models Training
```bash
cd src/ai-models
python training/train_classifier.py
```

## Deploy

Despliegue productivo gestionado por el pipeline `azure-pipelines.yml` (rama `main`) sobre el Resource Group **`SRBRGDOCSAIPROD`** (West Europe). Detalle completo en [docs/08_CHECKLISTS_DESPLIEGUE.md](docs/08_CHECKLISTS_DESPLIEGUE.md).

Despliegue manual (Functions) sobre la Function App existente:

```bash
func azure functionapp publish srbappprodocai
```

> Nota: actualmente el repositorio no incluye plantillas Bicep/Terraform productivas; los recursos se crearon previamente y el pipeline sólo despliega código.

## Documentacion

Ver carpeta docs/ para:
- **Cambios v1.4 (IMPORTANTE):** [docs/auxiliares/migracion-deployment/12_MIGRACION_PROMPTGPT_V1_4.md](docs/auxiliares/migracion-deployment/12_MIGRACION_PROMPTGPT_V1_4.md) — PromptGPT deprecation y ConfiguracionJson refactorizado
- Arquitectura detallada
- Contratos de entrada/salida
- Manuales de operacion
- Manual de plugins: [docs/manuales/MANUAL_PLUGINS.md](docs/manuales/MANUAL_PLUGINS.md)
- Manual del motor de validaciones: [docs/manuales/MANUAL_VALIDACIONES.md](docs/manuales/MANUAL_VALIDACIONES.md)
- Plantillas de plugins: [docs/contratos/PLANTILLA_PLUGINS_JSON.md](docs/contratos/PLANTILLA_PLUGINS_JSON.md)
- Fuente de verdad de configuracion: [docs/referencias/FUENTE_VERDAD_CONFIGURACION.md](docs/referencias/FUENTE_VERDAD_CONFIGURACION.md)

> Tipologias, modelos IA y plugins por tipologia se gestionan en BBDD mediante Admin API/DocumentIA.Admin. Los JSON fisicos del repositorio son seed inicial, plantillas o referencia historica; pueden estar desactualizados y no deben borrarse sin confirmacion explicita.

## Licencia

Uso interno - Proyecto MVP

## Notas de desarrollo (2026-04-15)

Últimos cambios relevantes para desarrollo local y pruebas:

- **Funciones (DocumentIA.Functions)**
  - Añadido fallback a `EntityFrameworkCore.InMemory` para entornos de desarrollo para evitar fallos en el arranque cuando la base de datos no está inaccesible. (Ver [src/backend/DocumentIA.Functions/Program.cs](src/backend/DocumentIA.Functions/Program.cs)).
  - Evitar la ejecución de migraciones automáticas cuando el proveedor no es relacional (`Database.IsRelational()` guard).
  - Añadida la dependencia `Microsoft.EntityFrameworkCore.InMemory` en `src/backend/DocumentIA.Functions/DocumentIA.Functions.csproj`.

- **Plugin AssetResolver (DocumentIA.AssetResolver)**
  - `appsettings.Development.json` actualizado para apuntar a un SQL local (ejemplo: `Server=127.0.0.1,1433;Database=DocumentIA;User Id=sa;Password=COMPLETAR_SQL_PASSWORD;TrustServerCertificate=True;`).
  - Búsqueda dual por origen: `DM_POSICION_AAII_TB` y `DM_POSICION_AACC_TB`, configurable con `AAII_Search` y `AACC_Search`.
  - Respuesta separada por origen (`ActivosAAII` y `ActivosAACC`) manteniendo `Activos` como agregado de compatibilidad.
  - Se añadieron campos obligatorios que siempre se devuelven: `FCH_ALTA`, `FCH_BAJA`, `DES_SERVICER`, `IND_STATUS`.
  - Cambio en la resolución por *aliases*: la resolución por aliases solo se ejecuta si ambos campos (IDUFIR y ReferenciaCatastral) vienen vacíos. Si alguno está **indicado** (por override o por mapeo en la tipología), la búsqueda se realiza únicamente por ese campo.
  - Proyecto de tests unitarios añadido: [src/plugins/DocumentIA.AssetResolver.Tests](src/plugins/DocumentIA.AssetResolver.Tests) con pruebas para `AssetResolverService`.

Instrucciones rápidas:

- Ejecutar tests:
  - `dotnet test src\plugins\DocumentIA.AssetResolver.Tests\DocumentIA.AssetResolver.Tests.csproj`
- Ejecutar AssetResolver en modo desarrollo:
  - Windows (PowerShell): `$Env:ASPNETCORE_ENVIRONMENT='Development'; cd src\plugins\DocumentIA.AssetResolver; dotnet run`
  - Linux/macOS: `export ASPNETCORE_ENVIRONMENT=Development; cd src/plugins/DocumentIA.AssetResolver; dotnet run`
- Endpoint de prueba:
  - `POST http://localhost:5006/api/assets/GetAAIIInfo` con header `X-Api-Key` (middleware exige API key).
  - Body ejemplo: `{ "CorrelationId": "c1", "ExtractedData": { "IDUFIR": "..." }, "RequestedFields": ["ID_ACTIVO_SAREB"] }`

Notas operativas:

- La tabla esperada en la base de datos es `DM_POSICION_AAII_TB`. Si aparece `Invalid object name 'DM_POSICION_AAII_TB'`, verifica que la cadena de conexión apunte a la base ODS correcta.
- Si el build/reporta errores de copia por archivo en uso, detén el proceso que está corriendo (`dotnet run`) antes de recompilar.

