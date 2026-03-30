# setup-dev-tools.ps1
# Script para crear archivos de desarrollo del proyecto DocumentIA MVP

param()

$rootFolder = "documento-ia-clasificacion-mvp"

# Verificar si la carpeta raiz existe
if (-not (Test-Path $rootFolder)) {
    Write-Host "Error: La carpeta raiz '$rootFolder' no existe. Ejecute primero setup-folders.ps1" -ForegroundColor Red
    exit 1
}

Write-Host "Creando archivos de desarrollo en $rootFolder..." -ForegroundColor Green
Set-Location $rootFolder

# Crear directorio si no existe
if (-not (Test-Path "src/ai-models")) {
    New-Item -ItemType Directory -Force -Path "src/ai-models" | Out-Null
}

# docker-compose.yml
$dockerComposeContent = @"
version: '3.8'

services:
  azurite:
    image: mcr.microsoft.com/azure-storage/azurite
    container_name: documentia-azurite
    ports:
      - "10000:10000"
      - "10001:10001"
      - "10002:10002"
    volumes:
      - azurite-data:/data

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: documentia-sql
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=COMPLETAR_SQL_PASSWORD
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sql-data:/var/opt/mssql

volumes:
  azurite-data:
  sql-data:
"@

$dockerComposeContent | Out-File -FilePath "docker-compose.yml" -Encoding UTF8
Write-Host "✓ docker-compose.yml creado" -ForegroundColor Gray

# requirements.txt
$requirementsContent = @"
# Azure SDKs
azure-ai-formrecognizer==3.3.0
azure-storage-blob==12.19.0
azure-identity==1.15.0
azure-keyvault-secrets==4.7.0

# Data Processing
pandas==2.1.4
numpy==1.26.2

# ML & Evaluation
scikit-learn==1.3.2

# Utilities
python-dotenv==1.0.0
pydantic==2.5.3

# Development
jupyter==1.0.0
jupyterlab==4.0.9
pytest==7.4.3
black==23.12.1
flake8==7.0.0
"@

$requirementsContent | Out-File -FilePath "src/ai-models/requirements.txt" -Encoding UTF8
Write-Host "✓ src/ai-models/requirements.txt creado" -ForegroundColor Gray

# pyproject.toml
$pyprojectContent = @"
[tool.poetry]
name = "documentia-ai-models"
version = "0.1.0"
description = "Scripts de entrenamiento de modelos IA para DocumentIA MVP"
authors = ["Your Team"]

[tool.poetry.dependencies]
python = "^3.10"
azure-ai-formrecognizer = "^3.3.0"
azure-storage-blob = "^12.19.0"
pandas = "^2.1.4"
scikit-learn = "^1.3.2"

[tool.poetry.dev-dependencies]
pytest = "^7.4.3"
black = "^23.12.1"
jupyter = "^1.0.0"

[build-system]
requires = ["poetry-core>=1.0.0"]
build-backend = "poetry.core.masonry.api"
"@

$pyprojectContent | Out-File -FilePath "src/ai-models/pyproject.toml" -Encoding UTF8
Write-Host "✓ src/ai-models/pyproject.toml creado" -ForegroundColor Gray

Write-Host "`n✓ Archivos de desarrollo creados exitosamente!" -ForegroundColor Green
Write-Host "`nSiguiente paso: Ejecutar setup-ci-cd.ps1" -ForegroundColor Yellow
