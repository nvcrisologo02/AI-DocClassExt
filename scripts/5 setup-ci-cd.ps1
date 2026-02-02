# setup-ci-cd.ps1
# Script para crear workflows de CI/CD del proyecto DocumentIA MVP

param()

$rootFolder = "documento-ia-clasificacion-mvp"

# Verificar si la carpeta raiz existe
if (-not (Test-Path $rootFolder)) {
    Write-Host "Error: La carpeta raiz '$rootFolder' no existe. Ejecute primero setup-folders.ps1" -ForegroundColor Red
    exit 1
}

Write-Host "Creando workflows de CI/CD en $rootFolder..." -ForegroundColor Green
Set-Location $rootFolder

# Crear directorio si no existe
if (-not (Test-Path ".github/workflows")) {
    New-Item -ItemType Directory -Force -Path ".github/workflows" | Out-Null
}

# GitHub Actions workflow
$workflowContent = @"
name: Deploy Infrastructure

on:
  push:
    branches: [ main ]
    paths:
      - 'infrastructure/**'
  workflow_dispatch:

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: `${{`{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy Bicep
        uses: azure/arm-deploy@v1
        with:
          resourceGroupName: rg-documentia-mvp
          template: ./infrastructure/bicep/main.bicep
          parameters: ./infrastructure/bicep/parameters/dev.parameters.json
"@

$workflowContent | Out-File -FilePath ".github/workflows/infrastructure.yml" -Encoding UTF8
Write-Host "✓ .github/workflows/infrastructure.yml creado" -ForegroundColor Gray

Write-Host "`n✓ Workflows de CI/CD creados exitosamente!" -ForegroundColor Green
Write-Host "`nTodos los scripts de setup han sido ejecutados correctamente!" -ForegroundColor Green
Write-Host "`nProyecto DocumentIA MVP listo para comenzar el desarrollo! 🚀" -ForegroundColor Yellow
