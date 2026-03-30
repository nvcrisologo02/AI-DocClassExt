# 1. setup-folders.ps1
# Script para crear solo la estructura de carpetas del proyecto DocumentIA MVP

param()

$rootFolder = "documento-ia-clasificacion-mvp"

Write-Host "Creando estructura de carpetas para $rootFolder..." -ForegroundColor Green

# Crear carpeta raÃ­z si no existe
if (-not (Test-Path $rootFolder)) {
    New-Item -ItemType Directory -Force -Path $rootFolder | Out-Null
}
Set-Location $rootFolder

# Estructura de carpetas
$folders = @(
    ".github/workflows",
    "docs/arquitectura",
    "docs/contratos",
    "docs/manuales",
    "infrastructure/bicep/modules",
    "infrastructure/bicep/parameters",
    "infrastructure/terraform/modules",
    "src/backend/DocumentIA.Functions/Orchestrators",
    "src/backend/DocumentIA.Functions/Activities",
    "src/backend/DocumentIA.Functions/Triggers",
    "src/backend/DocumentIA.Core/Models",
    "src/backend/DocumentIA.Core/Services",
    "src/backend/DocumentIA.Core/Rules",
    "src/backend/DocumentIA.Core/Utilities",
    "src/backend/DocumentIA.Plugins/Abstractions",
    "src/backend/DocumentIA.Plugins/AI",
    "src/backend/DocumentIA.Plugins/Integration",
    "src/backend/DocumentIA.Data/Context",
    "src/backend/DocumentIA.Data/Repositories",
    "src/backend/DocumentIA.Data/Entities",
    "src/backend/DocumentIA.Tests/Unit",
    "src/backend/DocumentIA.Tests/Integration",
    "src/ai-models/training",
    "src/ai-models/scripts",
    "src/ai-models/notebooks",
    "src/shared/contracts",
    "src/shared/config/tipologias",
    "tests/load-tests",
    "tests/e2e-tests",
    "scripts"
)

foreach ($folder in $folders) {
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    Write-Host "✓ $folder" -ForegroundColor Gray
}

Write-Host "`✓ Estructura de carpetas creada exitosamente!" -ForegroundColor Green
Write-Host "`n Siguiente paso: Ejecutar setup-config-files.ps1" -ForegroundColor Yellow
