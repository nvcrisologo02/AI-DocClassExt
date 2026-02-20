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

```bash
# Infraestructura
az deployment group create --resource-group rg-documentia-mvp \
  --template-file infrastructure/bicep/main.bicep

# Functions
func azure functionapp publish <function-app-name>
```

## Documentacion

Ver carpeta docs/ para:
- Arquitectura detallada
- Contratos de entrada/salida
- Manuales de operacion
- Manual de plugins: `docs/manuales/MANUAL_PLUGINS.md`
- Manual de activities (Azure Functions): `docs/manuales/MANUAL_ACTIVITIES_AZURE_FUNCTIONS.md`
- Plantillas de plugins: `docs/contratos/PLANTILLA_PLUGINS_JSON.md`

## Licencia

Uso interno - Proyecto MVP
