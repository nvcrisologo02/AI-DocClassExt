# setup-docs.ps1
# Script para crear documentacion del proyecto DocumentIA MVP

param()

$rootFolder = "documento-ia-clasificacion-mvp"

# Verificar si la carpeta raiz existe
if (-not (Test-Path $rootFolder)) {
    Write-Host "Error: La carpeta raiz '$rootFolder' no existe. Ejecute primero setup-folders.ps1" -ForegroundColor Red
    exit 1
}

Write-Host "Creando documentacion en $rootFolder..." -ForegroundColor Green
Set-Location $rootFolder

# README.md
$readmeContent = @"
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
```bash
cd src/backend
dotnet build
func start --csharp
``````

### AI Models Training
``````bash
cd src/ai-models
python training/train_classifier.py
``````

## Deploy

``````bash
# Infraestructura
az deployment group create --resource-group rg-documentia-mvp \
  --template-file infrastructure/bicep/main.bicep

# Functions
func azure functionapp publish <function-app-name>
``````

## Documentacion

Ver carpeta docs/ para:
- Arquitectura detallada
- Contratos de entrada/salida
- Manuales de operacion

## Licencia

Uso interno - Proyecto MVP
"@

$readmeContent | Out-File -FilePath "README.md" -Encoding UTF8

Write-Host "`n✓ Documentacion creada exitosamente!" -ForegroundColor Green
