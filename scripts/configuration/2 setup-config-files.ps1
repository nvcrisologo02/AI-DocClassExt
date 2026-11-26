# setup-config-files.ps1
# Script para crear archivos de configuracion del proyecto DocumentIA MVP

param()

$rootFolder = "documento-ia-clasificacion-mvp"

# Verificar si la carpeta raiz existe
if (-not (Test-Path $rootFolder)) {
    Write-Host "Error: La carpeta raiz '$rootFolder' no existe. Ejecute primero setup-folders.ps1" -ForegroundColor Red
    exit 1
}

Write-Host "Creando archivos de configuracion en $rootFolder..." -ForegroundColor Green
Set-Location $rootFolder

# .gitignore
$gitignoreContent = @"
# .NET
bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user
launchSettings.json

# Azure Functions
local.settings.json
__blobstorage__/
__queuestorage__/
__azurite_db*__.json

# Python
__pycache__/
*.py[cod]
*$py.class
.Python
venv/
env/
.env
*.egg-info/
.pytest_cache/
.ipynb_checkpoints/

# IDE
.vscode/
.idea/
*.swp
*.swo

# OS
.DS_Store
Thumbs.db

# Secrets
*.pfx
*.key
appsettings.Development.json
"@

$gitignoreContent | Out-File -FilePath ".gitignore" -Encoding UTF8
Write-Host "✓ .gitignore creado" -ForegroundColor Gray

Write-Host "`n✓ Archivos de configuracion creados exitosamente!" -ForegroundColor Green
Write-Host "`nSiguiente paso: Ejecutar setup-docs.ps1" -ForegroundColor Yellow
