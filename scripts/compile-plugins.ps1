# Script de compilacion y verificacion del sistema de plugins
# DocumentIA MVP - Sistema de Plugins de Integracion
# Ruta base: C:\temp\MVP\documento-ia-clasificacion-mvp

param(
    [switch]$SkipTests,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  COMPILACION SISTEMA DE PLUGINS" -ForegroundColor Cyan
Write-Host "  DocumentIA MVP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Obtener directorio raiz del proyecto
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
$backendPath = Join-Path $projectRoot "src\backend"

Write-Host "Directorio del proyecto: $projectRoot" -ForegroundColor Gray
Write-Host "Directorio backend: $backendPath" -ForegroundColor Gray
Write-Host ""

# Verificar que estamos en el directorio correcto
if (-not (Test-Path $backendPath)) {
    Write-Host "ERROR: No se encuentra la carpeta src\backend" -ForegroundColor Red
    Write-Host "Asegurate de ejecutar el script desde la raiz del proyecto" -ForegroundColor Yellow
    exit 1
}

# Cambiar al directorio backend
Push-Location $backendPath

try {
    # Paso 1: Limpiar solucion
    Write-Host "[1/8] Limpiando solucion..." -ForegroundColor Yellow
    dotnet clean --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo la limpieza de la solucion"
    }
    Write-Host "  OK - Limpieza completada" -ForegroundColor Green

    # Paso 2: Restaurar paquetes
    Write-Host "`n[2/8] Restaurando paquetes NuGet..." -ForegroundColor Yellow
    dotnet restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo la restauracion de paquetes"
    }
    Write-Host "  OK - Paquetes restaurados" -ForegroundColor Green

    # Paso 3: Compilar DocumentIA.Core
    Write-Host "`n[3/8] Compilando DocumentIA.Core..." -ForegroundColor Yellow
    dotnet build "DocumentIA.Core\DocumentIA.Core.csproj" --configuration Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo compilacion de DocumentIA.Core"
    }
    Write-Host "  OK - DocumentIA.Core compilado" -ForegroundColor Green

    # Paso 4: Compilar DocumentIA.Data
    Write-Host "`n[4/8] Compilando DocumentIA.Data..." -ForegroundColor Yellow
    dotnet build "DocumentIA.Data\DocumentIA.Data.csproj" --configuration Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo compilacion de DocumentIA.Data"
    }
    Write-Host "  OK - DocumentIA.Data compilado" -ForegroundColor Green

    # Paso 5: Compilar DocumentIA.Plugins
    Write-Host "`n[5/8] Compilando DocumentIA.Plugins..." -ForegroundColor Yellow
    dotnet build "DocumentIA.Plugins\DocumentIA.Plugins.csproj" --configuration Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR compilando DocumentIA.Plugins" -ForegroundColor Red
        Write-Host "`n  Reintentando con output detallado..." -ForegroundColor Yellow
        dotnet build "DocumentIA.Plugins\DocumentIA.Plugins.csproj" --configuration Release --no-restore
        throw "Fallo compilacion de DocumentIA.Plugins"
    }
    Write-Host "  OK - DocumentIA.Plugins compilado" -ForegroundColor Green

    # Paso 6: Compilar DocumentIA.Functions
    Write-Host "`n[6/8] Compilando DocumentIA.Functions..." -ForegroundColor Yellow
    dotnet build "DocumentIA.Functions\DocumentIA.Functions.csproj" --configuration Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR compilando DocumentIA.Functions" -ForegroundColor Red
        Write-Host "`n  Reintentando con output detallado..." -ForegroundColor Yellow
        dotnet build "DocumentIA.Functions\DocumentIA.Functions.csproj" --configuration Release --no-restore
        throw "Fallo compilacion de DocumentIA.Functions"
    }
    Write-Host "  OK - DocumentIA.Functions compilado" -ForegroundColor Green

    # Paso 7: Compilar DocumentIA.Tests.Unit
    Write-Host "`n[7/8] Compilando DocumentIA.Tests.Unit..." -ForegroundColor Yellow
    if (Test-Path "DocumentIA.Tests.Unit\DocumentIA.Tests.Unit.csproj") {
        dotnet build "DocumentIA.Tests.Unit\DocumentIA.Tests.Unit.csproj" --configuration Release --no-restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  ADVERTENCIA: Fallo compilacion de tests" -ForegroundColor Yellow
        } else {
            Write-Host "  OK - DocumentIA.Tests.Unit compilado" -ForegroundColor Green
        }
    } else {
        Write-Host "  ADVERTENCIA: No se encontro DocumentIA.Tests.Unit.csproj" -ForegroundColor Yellow
    }

    # Paso 8: Ejecutar tests unitarios
    if (-not $SkipTests) {
        Write-Host "`n[8/8] Ejecutando tests unitarios..." -ForegroundColor Yellow
        if (Test-Path "DocumentIA.Tests.Unit\DocumentIA.Tests.Unit.csproj") {
            $testVerbosity = if ($Verbose) { "normal" } else { "minimal" }
            dotnet test "DocumentIA.Tests.Unit\DocumentIA.Tests.Unit.csproj" --configuration Release --no-build --verbosity $testVerbosity
            if ($LASTEXITCODE -ne 0) {
                Write-Host "  ADVERTENCIA: Algunos tests fallaron" -ForegroundColor Yellow
            } else {
                Write-Host "  OK - Todos los tests pasaron" -ForegroundColor Green
            }
        } else {
            Write-Host "  OMITIDO - No se encontro proyecto de tests" -ForegroundColor Gray
        }
    } else {
        Write-Host "`n[8/8] Tests omitidos (parametro -SkipTests)" -ForegroundColor Gray
    }

    # Verificacion de archivos de configuracion
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  VERIFICACION DE CONFIGURACION" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    # Verificar carpeta de configuraciones en Functions
    $configPathFunctions = "DocumentIA.Functions\config\tipologias"
    Write-Host "`nVerificando: $configPathFunctions" -ForegroundColor Yellow

    if (Test-Path $configPathFunctions) {
        $jsonFiles = Get-ChildItem -Path $configPathFunctions -Filter "*.plugins.json" -ErrorAction SilentlyContinue
        if ($jsonFiles) {
            Write-Host "  OK - Encontrados $($jsonFiles.Count) archivos de configuracion:" -ForegroundColor Green
            foreach ($file in $jsonFiles) {
                Write-Host "    - $($file.Name)" -ForegroundColor White
                
                # Validar JSON
                try {
                    $content = Get-Content $file.FullName -Raw | ConvertFrom-Json
                    if ($content.tipologiaId) {
                        Write-Host "      [VALIDO] Tipologia: $($content.tipologiaId)" -ForegroundColor Gray
                    }
                } catch {
                    Write-Host "      [ERROR] JSON invalido" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "  ADVERTENCIA: No hay archivos *.plugins.json" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ADVERTENCIA: No existe la carpeta" -ForegroundColor Yellow
        Write-Host "  Creando carpeta..." -ForegroundColor Gray
        New-Item -ItemType Directory -Force -Path $configPathFunctions | Out-Null
        Write-Host "  OK - Carpeta creada" -ForegroundColor Green
    }

    # Verificar carpeta shared/config/tipologias
    $configPathShared = Join-Path $projectRoot "src\shared\config\tipologias"
    Write-Host "`nVerificando: src\shared\config\tipologias" -ForegroundColor Yellow

    if (Test-Path $configPathShared) {
        $validationFiles = Get-ChildItem -Path $configPathShared -Filter "*.validation.json" -ErrorAction SilentlyContinue
        $pluginFiles = Get-ChildItem -Path $configPathShared -Filter "*.plugins.json" -ErrorAction SilentlyContinue
        
        Write-Host "  OK - Archivos de validacion: $($validationFiles.Count)" -ForegroundColor Green
        Write-Host "  OK - Archivos de plugins: $($pluginFiles.Count)" -ForegroundColor Green
    } else {
        Write-Host "  INFO - Carpeta shared no creada aun" -ForegroundColor Gray
    }

    # Verificar estructura de carpetas del proyecto Plugins
    Write-Host "`nVerificando estructura de DocumentIA.Plugins..." -ForegroundColor Yellow
    $pluginFiles = @(
        "DocumentIA.Plugins\Integration\IIntegrationPlugin.cs",
        "DocumentIA.Plugins\Integration\RestPlugin.cs",
        "DocumentIA.Plugins\Integration\PluginManager.cs",
        "DocumentIA.Plugins\Integration\ResilientPlugin.cs",
        "DocumentIA.Plugins\Integration\PluginFactory.cs",
        "DocumentIA.Plugins\Integration\PluginConfigLoader.cs",
        "DocumentIA.Plugins\Integration\PluginConfiguration.cs"
    )

    $missingFiles = @()
    foreach ($file in $pluginFiles) {
        if (Test-Path $file) {
            $fileName = Split-Path $file -Leaf
            Write-Host "  [OK] $fileName" -ForegroundColor Green
        } else {
            $fileName = Split-Path $file -Leaf
            Write-Host "  [FALTA] $fileName" -ForegroundColor Red
            $missingFiles += $file
        }
    }

    # Verificar IntegrarActivity actualizada
    Write-Host "`nVerificando Activities..." -ForegroundColor Yellow
    if (Test-Path "DocumentIA.Functions\Activities\IntegrarActivity.cs") {
        $content = Get-Content "DocumentIA.Functions\Activities\IntegrarActivity.cs" -Raw
        if ($content -match "PluginManager") {
            Write-Host "  [OK] IntegrarActivity.cs actualizada con PluginManager" -ForegroundColor Green
        } else {
            Write-Host "  [PENDIENTE] IntegrarActivity.cs requiere actualizacion" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  [FALTA] IntegrarActivity.cs" -ForegroundColor Red
    }

    # Verificar tests de plugins
    Write-Host "`nVerificando tests de plugins..." -ForegroundColor Yellow
    if (Test-Path "DocumentIA.Tests.Unit\Plugins") {
        $testFiles = Get-ChildItem -Path "DocumentIA.Tests.Unit\Plugins" -Filter "*.cs"
        Write-Host "  OK - $($testFiles.Count) archivos de test encontrados" -ForegroundColor Green
    } else {
        Write-Host "  INFO - Carpeta de tests de plugins no creada" -ForegroundColor Gray
    }

    # Resumen final
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  RESUMEN DE COMPILACION" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    Write-Host "`nEstado de proyectos:" -ForegroundColor White
    Write-Host "  [OK] DocumentIA.Core" -ForegroundColor Green
    Write-Host "  [OK] DocumentIA.Data" -ForegroundColor Green
    Write-Host "  [OK] DocumentIA.Plugins" -ForegroundColor Green
    Write-Host "  [OK] DocumentIA.Functions" -ForegroundColor Green

    if ($missingFiles.Count -gt 0) {
        Write-Host "`nArchivos faltantes ($($missingFiles.Count)):" -ForegroundColor Yellow
        foreach ($file in $missingFiles) {
            Write-Host "  - $file" -ForegroundColor Red
        }
    }

    # Proximos pasos
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  PROXIMOS PASOS" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Cyan

    $needsConfig = -not (Test-Path "$configPathFunctions\tasacion.plugins.json")
    $needsProgramUpdate = $true # Siempre mostrar este paso

    if ($needsConfig) {
        Write-Host "`n1. Crear archivos de configuracion JSON:" -ForegroundColor White
        Write-Host "   Copia tasacion.plugins.json y cedula.plugins.json" -ForegroundColor Gray
        Write-Host "   a: DocumentIA.Functions\config\tipologias\" -ForegroundColor Gray
    }

    if ($needsProgramUpdate) {
        Write-Host "`n2. Actualizar Program.cs:" -ForegroundColor White
        Write-Host "   Agrega la configuracion de DI para PluginManager y PluginFactory" -ForegroundColor Gray
        Write-Host "   Ver archivo proporcionado: Program.cs (Fase 6)" -ForegroundColor Gray
    }

    Write-Host "`n3. Iniciar servicios Docker:" -ForegroundColor White
    Write-Host "   cd $projectRoot" -ForegroundColor Gray
    Write-Host "   docker-compose up -d" -ForegroundColor Gray

    Write-Host "`n4. Iniciar Azure Functions:" -ForegroundColor White
    Write-Host "   cd DocumentIA.Functions" -ForegroundColor Gray
    Write-Host "   func start" -ForegroundColor Gray

    Write-Host "`n5. Probar integracion:" -ForegroundColor White
    Write-Host "   cd $projectRoot\scripts" -ForegroundColor Gray
    Write-Host "   .\test-ingest.ps1" -ForegroundColor Gray

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  COMPILACION COMPLETADA EXITOSAMENTE" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan

} catch {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "  ERROR EN COMPILACION" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "`n$($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nRevisa los errores anteriores para mas detalles.`n" -ForegroundColor Yellow
    exit 1
} finally {
    # Volver al directorio original
    Pop-Location
}
