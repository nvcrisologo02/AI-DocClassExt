<#
.SYNOPSIS
    ⚠️ DEPRECATED - Hace backup de tabla Tipologias y actualiza TipologiaNombre + gptdescripcion desde Excel

.DESCRIPTION
    ⚠️ ESTADO DEPRECATED (2026-06-10): Este script depende de scripts\config\db-connection.json que no existe en el repositorio.
    Si necesitas usar este script:
    1. Crea scripts\config\db-connection.json con estructura: { "ConnectionString": "<value>", ... }
    2. O modifica el script para usar variables de entorno en lugar de JSON
    3. O contacta al equipo de plataforma para aclarar si esta herramienta sigue en uso

    1. Crea backup BCP de tabla Tipologias
    2. Lee Excel con tipologías y gptdescripciones
    3. Actualiza ConfiguracionJson con TipologiaNombre y gptdescripcion
    4. Genera reporte de cambios

.PARAMETER ExcelPath
    Ruta al archivo Excel con datos de tipologías
    Default: ".\tiposgpt.xlsx"

.PARAMETER DryRun
    Si es $true, solo simula cambios sin ejecutar UPDATE

.PARAMETER KeepBackupAfterSuccess
    Si es $true, mantiene el archivo de backup. Default: $true

.EXAMPLE
    # ⚠️ DEPRECATED - Ver DESCRIPTION para reactivar
    .\Backup-Y-Actualizar-Tipologias.ps1 -ExcelPath ".\tiposgpt.xlsx" -DryRun $true
#>

param(
    [string]$ExcelPath = ".\tiposgpt.xlsx",
    [bool]$DryRun = $true,
    [bool]$KeepBackupAfterSuccess = $true
)

$ErrorActionPreference = "Stop"

# ============================================================================
# 1. INICIALIZACIÓN Y VALIDACIONES
# ============================================================================

Write-Host "═" * 80 -ForegroundColor Cyan
Write-Host "BACKUP Y ACTUALIZACIÓN DE TIPOLOGÍAS" -ForegroundColor Cyan
Write-Host "═" * 80 -ForegroundColor Cyan
Write-Host ""

# Verificar Excel
if (-not (Test-Path $ExcelPath)) {
    Write-Host "❌ ERROR: Excel no encontrado en: $ExcelPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Por favor, coloca el archivo tiposgpt.xlsx en la carpeta raíz del workspace"
    exit 1
}

Write-Host "✅ Excel encontrado: $ExcelPath" -ForegroundColor Green

# Cargar configuración de DB
$configPath = ".\scripts\config\db-connection.json"
if (-not (Test-Path $configPath)) {
    Write-Host "❌ ERROR: Configuración de BD no encontrada en: $configPath" -ForegroundColor Red
    exit 1
}

$dbConfig = Get-Content $configPath | ConvertFrom-Json
$connectionString = $dbConfig.connectionString
Write-Host "✅ Configuración de BD cargada" -ForegroundColor Green

# Verificar módulos PowerShell
$modulesNeeded = @("ImportExcel")
foreach ($module in $modulesNeeded) {
    if (-not (Get-Module -Name $module -ListAvailable -ErrorAction SilentlyContinue)) {
        Write-Host "⚠️  Instalando módulo: $module" -ForegroundColor Yellow
        Install-Module -Name $module -Force -Scope CurrentUser -SkipPublisherCheck
    }
}

Write-Host ""
Write-Host "DRY RUN: $DryRun" -ForegroundColor $(if ($DryRun) { "Yellow" } else { "Red" })
Write-Host ""

# ============================================================================
# 2. LEER EXCEL
# ============================================================================

Write-Host "📄 LEYENDO EXCEL..." -ForegroundColor Cyan
Write-Host ""

try {
    $excelData = Import-Excel -Path $ExcelPath -WorksheetName "Sheet1" -ErrorAction Stop
    Write-Host "✅ Excel cargado exitosamente" -ForegroundColor Green
    Write-Host "   Filas: $($excelData.Count)" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR al leer Excel: $_" -ForegroundColor Red
    exit 1
}

# Validar estructura de datos
$requiredColumns = @("Tipologia", "Nombre", "gptdescripcion")
$missingColumns = @()

foreach ($col in $requiredColumns) {
    if ($excelData[0].PSObject.Properties.Name -notcontains $col) {
        $missingColumns += $col
    }
}

if ($missingColumns.Count -gt 0) {
    Write-Host "❌ ERROR: Faltan columnas en Excel: $($missingColumns -join ', ')" -ForegroundColor Red
    Write-Host ""
    Write-Host "Columnas encontradas:" -ForegroundColor Yellow
    $excelData[0].PSObject.Properties.Name | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host "✅ Estructura Excel válida" -ForegroundColor Green
Write-Host "   Columnas: Tipologia, Nombre, gptdescripcion" -ForegroundColor Green

# Crear mapa de actualizaciones
$updateMap = @{}
foreach ($row in $excelData) {
    if ($row.Tipologia) {
        $updateMap[$row.Tipologia] = @{
            Nombre = $row.Nombre
            gptdescripcion = $row.gptdescripcion
        }
    }
}

Write-Host "   Tipologías a actualizar: $($updateMap.Count)" -ForegroundColor Green
Write-Host ""

# ============================================================================
# 3. CREAR BACKUP BCP
# ============================================================================

Write-Host "💾 CREANDO BACKUP BCP..." -ForegroundColor Cyan
Write-Host ""

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = ".\scripts\backups\Tipologias_Backup_$timestamp.bcp"
$backupDir = Split-Path $backupFile
$formatFile = ".\scripts\backups\Tipologias.fmt"

if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}

try {
    # Usar BCP para exportar
    $sqlCmd = @"
SELECT 
    Id,
    Codigo,
    Descripcion,
    TipologiaFamilia,
    ResolvedTdn1,
    ResolvedTdn2,
    ConfiguracionJson,
    IsPublished,
    CreatedAt,
    UpdatedAt,
    VersionId
FROM [dbo].[Tipologias]
ORDER BY Id
"@

    # Exportar con BCP
    Write-Host "Ejecutando BCP export..." -ForegroundColor Gray
    
    $sqlCmd | sqlcmd `
        -S $dbConfig.server `
        -d $dbConfig.database `
        -U $dbConfig.username `
        -P $dbConfig.password `
        -o ".\scripts\backups\Tipologias_Backup_$timestamp.sql" `
        -h -1 `
        -s "|" | Out-Null

    Write-Host "✅ Backup exportado: $backupFile" -ForegroundColor Green
    Write-Host "   Archivo SQL guardado: .\scripts\backups\Tipologias_Backup_$timestamp.sql" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR al crear backup: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ============================================================================
# 4. LEER TIPOLOGÍAS ACTUALES DE BD
# ============================================================================

Write-Host "🔍 LEYENDO TIPOLOGÍAS ACTUALES DE BD..." -ForegroundColor Cyan
Write-Host ""

try {
    $tipologias = @()
    
    $query = @"
SELECT 
    Id,
    Codigo,
    ConfiguracionJson
FROM [dbo].[Tipologias]
WHERE IsPublished = 1
ORDER BY Codigo
"@

    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()

    $command = $connection.CreateCommand()
    $command.CommandText = $query
    $reader = $command.ExecuteReader()

    while ($reader.Read()) {
        $tipologias += @{
            Id = $reader["Id"]
            Codigo = $reader["Codigo"]
            ConfiguracionJson = $reader["ConfiguracionJson"]
        }
    }

    $reader.Close()
    $connection.Close()

    Write-Host "✅ Tipologías cargadas: $($tipologias.Count)" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR al leer BD: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ============================================================================
# 5. PREPARAR ACTUALIZACIONES
# ============================================================================

Write-Host "🔧 PREPARANDO ACTUALIZACIONES..." -ForegroundColor Cyan
Write-Host ""

$cambios = @()
$errores = @()

foreach ($tipologia in $tipologias) {
    $codigo = $tipologia.Codigo
    
    if ($updateMap.ContainsKey($codigo)) {
        $actualizacion = $updateMap[$codigo]
        
        try {
            $configJson = $tipologia.ConfiguracionJson | ConvertFrom-Json
            
            $cambioItem = @{
                Id = $tipologia.Id
                Codigo = $codigo
                TipologiaNombreAnterior = $configJson.tipologiaNombre
                TipologiaNombreNuevo = $actualizacion.Nombre
                gptdescripcionAnterior = $configJson.gptdescripcion
                gptdescripcionNuevo = $actualizacion.gptdescripcion
            }
            
            $cambios += $cambioItem
            
            Write-Host "  ✓ $codigo" -ForegroundColor Green
            Write-Host "    Nombre: '$($cambioItem.TipologiaNombreAnterior)' → '$($cambioItem.TipologiaNombreNuevo)'" -ForegroundColor Gray
            Write-Host "    Descripción: '$($cambioItem.gptdescripcionAnterior)' → '$($cambioItem.gptdescripcionNuevo)'" -ForegroundColor Gray
            
        } catch {
            $errores += "Error procesando $codigo : $_"
            Write-Host "  ❌ ERROR en $codigo : $_" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "Cambios a aplicar: $($cambios.Count)" -ForegroundColor Green
if ($errores.Count -gt 0) {
    Write-Host "Errores encontrados: $($errores.Count)" -ForegroundColor Red
}

Write-Host ""

# ============================================================================
# 6. APLICAR CAMBIOS (SI NO ES DRY RUN)
# ============================================================================

if ($DryRun) {
    Write-Host "⚠️  DRY RUN: Los cambios NO se ejecutarán en la BD" -ForegroundColor Yellow
} else {
    Write-Host "📝 APLICANDO CAMBIOS EN BD..." -ForegroundColor Cyan
    Write-Host ""
    
    $conexion = New-Object System.Data.SqlClient.SqlConnection
    $conexion.ConnectionString = $connectionString
    $conexion.Open()
    
    $actualizadas = 0
    $fallidas = 0
    
    foreach ($cambio in $cambios) {
        try {
            $comando = $conexion.CreateCommand()
            
            # Leer config actual
            $queryLectura = "SELECT ConfiguracionJson FROM [dbo].[Tipologias] WHERE Id = $($cambio.Id)"
            $cmdLectura = $conexion.CreateCommand()
            $cmdLectura.CommandText = $queryLectura
            $configJsonActual = $cmdLectura.ExecuteScalar()
            
            # Parsear y actualizar
            $config = $configJsonActual | ConvertFrom-Json
            $config.tipologiaNombre = $cambio.TipologiaNombreNuevo
            $config.gptdescripcion = $cambio.gptdescripcionNuevo
            
            $configJsonNuevo = $config | ConvertTo-Json -Compress
            
            # Hacer UPDATE
            $queryUpdate = @"
UPDATE [dbo].[Tipologias]
SET 
    ConfiguracionJson = @ConfigJson,
    UpdatedAt = GETUTCNOW()
WHERE Id = $($cambio.Id)
"@
            
            $comando.CommandText = $queryUpdate
            $comando.Parameters.AddWithValue("@ConfigJson", $configJsonNuevo) | Out-Null
            $comando.ExecuteNonQuery() | Out-Null
            
            Write-Host "  ✓ $($cambio.Codigo)" -ForegroundColor Green
            $actualizadas++
            
        } catch {
            Write-Host "  ❌ $($cambio.Codigo): $_" -ForegroundColor Red
            $fallidas++
        }
    }
    
    $conexion.Close()
    
    Write-Host ""
    Write-Host "Actualizadas: $actualizadas" -ForegroundColor Green
    if ($fallidas -gt 0) {
        Write-Host "Fallidas: $fallidas" -ForegroundColor Red
    }
}

Write-Host ""

# ============================================================================
# 7. GENERAR REPORTE
# ============================================================================

Write-Host "📋 GENERANDO REPORTE..." -ForegroundColor Cyan
Write-Host ""

$reportPath = ".\scripts\backups\Reporte_Actualizacion_$timestamp.txt"

$contenidoReporte = @"
REPORTE DE ACTUALIZACIÓN DE TIPOLOGÍAS
=====================================
Fecha: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Modo: $(if ($DryRun) { "DRY RUN (sin cambios)" } else { "PRODUCCIÓN" })

FUENTE DE DATOS
===============
Excel: $ExcelPath
Tipologías en Excel: $($updateMap.Count)

CAMBIOS PREPARADOS
==================
Total de cambios: $($cambios.Count)

$(if ($cambios.Count -gt 0) {
    "Detalle por tipología:`r`n" + (
        $cambios | ForEach-Object {
            "  - $($_.Codigo)`r`n" + 
            "    TipologiaNombre: '$($_.TipologiaNombreAnterior)' → '$($_.TipologiaNombreNuevo)'`r`n" +
            "    gptdescripcion: '$($_.gptdescripcionAnterior)' → '$($_.gptdescripcionNuevo)'"
        }
    ) -join "`r`n"
} else {
    "No hay cambios"
})

BACKUP
======
Ubicación BCP: $backupFile
SQL guardado: .\scripts\backups\Tipologias_Backup_$timestamp.sql

ESTADO FINAL
============
$(if ($DryRun) { "DRY RUN completado. Ejecutar sin -DryRun para aplicar cambios." } else { "Cambios aplicados exitosamente." })

"@

$contenidoReporte | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "✅ Reporte guardado: $reportPath" -ForegroundColor Green

Write-Host ""
Write-Host "═" * 80 -ForegroundColor Cyan
Write-Host "COMPLETADO" -ForegroundColor Cyan
Write-Host "═" * 80 -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "Para aplicar los cambios, ejecuta:" -ForegroundColor Yellow
    Write-Host "  .\scripts\Backup-Y-Actualizar-Tipologias.ps1 -ExcelPath `"$ExcelPath`" -DryRun `$false" -ForegroundColor Yellow
}
