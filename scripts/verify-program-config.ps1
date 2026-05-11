# Verifica que Program.cs esta configurado correctamente para plugins

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  VERIFICACION DE PROGRAM.CS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
$programFile = Join-Path $projectRoot "src\backend\DocumentIA.Functions\Program.cs"

if (-not (Test-Path $programFile)) {
    Write-Host "ERROR: No se encuentra Program.cs en la ruta esperada" -ForegroundColor Red
    exit 1
}

Write-Host "Analizando: $programFile" -ForegroundColor Gray
Write-Host ""

$content = Get-Content $programFile -Raw

# Verificaciones
$checks = @{
    "using DocumentIA.Plugins.Integration" = "Import de namespace de plugins"
    "services.AddHttpClient()" = "HttpClientFactory registrado"
    "services.AddSingleton<PluginManager>" = "PluginManager registrado"
    "services.AddSingleton<PluginFactory>" = "PluginFactory registrado"
    "services.AddSingleton<PluginConfigLoader>" = "PluginConfigLoader registrado"
}

$allPassed = $true

foreach ($check in $checks.GetEnumerator()) {
    $pattern = [regex]::Escape($check.Key)
    if ($content -match $pattern) {
        Write-Host "[OK] $($check.Value)" -ForegroundColor Green
    } else {
        Write-Host "[FALTA] $($check.Value)" -ForegroundColor Red
        Write-Host "  Buscar: $($check.Key)" -ForegroundColor Gray
        $allPassed = $false
    }
}

Write-Host ""

if ($allPassed) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  CONFIGURACION CORRECTA" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Green
    
    Write-Host "Program.cs esta configurado correctamente." -ForegroundColor White
    Write-Host "`nProximos pasos:" -ForegroundColor Yellow
    Write-Host "1. Compilar nuevamente: .\scripts\compile-plugins.ps1" -ForegroundColor White
    Write-Host "2. Iniciar Docker: docker-compose up -d" -ForegroundColor White
    Write-Host "3. Iniciar Functions: cd src\backend\DocumentIA.Functions && func start`n" -ForegroundColor White
} else {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  CONFIGURACION INCOMPLETA" -ForegroundColor Red
    Write-Host "========================================`n" -ForegroundColor Red
    
    Write-Host "Actualiza Program.cs con el codigo proporcionado." -ForegroundColor Yellow
    Write-Host "Archivo: src\backend\DocumentIA.Functions\Program.cs`n" -ForegroundColor Gray
    exit 1
}
