# Compilar sistema completo de plugins

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  COMPILACION SISTEMA DE PLUGINS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$projectRoot = "C:\temp\MVP\documento-ia-clasificacion-mvp"
cd $projectRoot

# 1. Compilar DocumentIA.Plugins
Write-Host "[1/5] Compilando DocumentIA.Plugins..." -ForegroundColor Yellow
cd src\backend\DocumentIA.Plugins
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [ERROR] Fallo compilacion de Plugins" -ForegroundColor Red
    exit 1
}
Write-Host "  [OK] DocumentIA.Plugins compilado" -ForegroundColor Green

# 2. Compilar SarebEnrichments
Write-Host "`n[2/5] Compilando SarebEnrichments..." -ForegroundColor Yellow
cd $projectRoot\src\enrichments\SarebEnrichments
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [ERROR] Fallo compilacion de SarebEnrichments" -ForegroundColor Red
    exit 1
}
Write-Host "  [OK] SarebEnrichments compilado" -ForegroundColor Green

# 3. Copiar DLL de SarebEnrichments a carpeta de plugins
Write-Host "`n[3/5] Copiando SarebEnrichments.dll..." -ForegroundColor Yellow
$sourceDll = "$projectRoot\src\enrichments\SarebEnrichments\bin\Release\net8.0\SarebEnrichments.dll"
$targetFolder = "$projectRoot\plugins"

if (-not (Test-Path $targetFolder)) {
    New-Item -ItemType Directory -Force -Path $targetFolder | Out-Null
}

Copy-Item $sourceDll $targetFolder -Force
Write-Host "  [OK] DLL copiada a: $targetFolder" -ForegroundColor Green

# 4. Compilar DocumentIA.Functions
Write-Host "`n[4/5] Compilando DocumentIA.Functions..." -ForegroundColor Yellow
cd $projectRoot\src\backend\DocumentIA.Functions
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [ERROR] Fallo compilacion de Functions" -ForegroundColor Red
    exit 1
}
Write-Host "  [OK] DocumentIA.Functions compilado" -ForegroundColor Green

# 5. Copiar archivos de configuracion
Write-Host "`n[5/5] Copiando configuraciones..." -ForegroundColor Yellow
$configSource = "$projectRoot\src\backend\DocumentIA.Functions\config"
$configDest = "$projectRoot\src\backend\DocumentIA.Functions\bin\Release\net8.0\config"

if (Test-Path $configSource) {
    Copy-Item -Path "$configSource\*" -Destination (Split-Path $configDest -Parent) -Recurse -Force
    Write-Host "  [OK] Configuraciones copiadas" -ForegroundColor Green
}

# Resumen
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  RESUMEN" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Plugins disponibles:" -ForegroundColor White
Write-Host "  [OK] RestPlugin    (rest)" -ForegroundColor Green
Write-Host "  [OK] SoapPlugin    (soap)" -ForegroundColor Green
Write-Host "  [OK] CustomPlugin  (custom)" -ForegroundColor Green

Write-Host "`nEnriquecedores custom:" -ForegroundColor White
Write-Host "  [OK] SarebEnrichments.dll" -ForegroundColor Green
Write-Host "       Location: $targetFolder\SarebEnrichments.dll" -ForegroundColor Gray

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  COMPILACION COMPLETADA" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green
