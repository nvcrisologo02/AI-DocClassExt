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
    2. Lee Excel con ID y gptdescripciones
    3. Obtiene Nombre de la tabla Tipologias
    4. Actualiza ConfiguracionJson con TipologiaNombre y gptdescripcion
    5. Genera reporte de cambios

.PARAMETER ExcelPath
    Ruta al archivo Excel con ID y gptdescripcion
    Default: ".\tiposgpt.xlsx"

.PARAMETER DryRun
    Si es $true, solo simula cambios sin ejecutar UPDATE

.EXAMPLE
    # ⚠️ DEPRECATED - Ver DESCRIPTION para reactivar
    .\Actualizar-TipologiaNombre-EnTipologias.ps1 -ExcelPath ".\tiposgpt.xlsx" -DryRun $true
#>

param(
    [string]$ExcelPath = ".\tiposgpt.xlsx",
    [bool]$DryRun = $true
)

$ErrorActionPreference = "Stop"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "BACKUP Y ACTUALIZACIÓN DE TIPOLOGÍAS" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""

# Verificar Excel
if (-not (Test-Path $ExcelPath)) {
    Write-Host "❌ ERROR: Excel no encontrado en: $ExcelPath" -ForegroundColor Red
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

# Verificar módulo ImportExcel
if (-not (Get-Module -Name ImportExcel -ListAvailable -ErrorAction SilentlyContinue)) {
    Write-Host "⚠️  Instalando módulo: ImportExcel" -ForegroundColor Yellow
    Install-Module -Name ImportExcel -Force -Scope CurrentUser -SkipPublisherCheck
}

Write-Host ""
Write-Host "DRY RUN: $DryRun" -ForegroundColor $(if ($DryRun) { "Yellow" } else { "Red" })
Write-Host ""

# ============================================================================
# 1. CREAR BACKUP BCP
# ============================================================================

Write-Host "💾 CREANDO BACKUP BCP..." -ForegroundColor Cyan
Write-Host ""

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = ".\scripts\backups"
$backupFile = "$backupDir\Tipologias_Backup_$timestamp.bcp"

if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection
    $conn.ConnectionString = $connectionString
    $conn.Open()
    
    # Query para exportar
    $query = @"
SELECT 
    Id, Codigo, Nombre, Descripcion,
    TipologiaFamilia, ResolvedTdn1, ResolvedTdn2, ConfiguracionJson,
    IsPublished, CreatedAt, UpdatedAt
FROM [dbo].[Tipologias]
ORDER BY Codigo
"@

    # Ejecutar consulta y guardar en archivo SQL
    $sqlBackupFile = "$backupDir\Tipologias_Backup_$timestamp.sql"
    $query | Out-File -FilePath $sqlBackupFile -Encoding UTF8
    
    Write-Host "✅ Backup SQL guardado: $sqlBackupFile" -ForegroundColor Green
    
} catch {
    Write-Host "❌ ERROR al crear backup: $_" -ForegroundColor Red
    $conn.Close()
    exit 1
}

# ============================================================================
# 2. LEER EXCEL
# ============================================================================

Write-Host ""
Write-Host "📄 LEYENDO EXCEL..." -ForegroundColor Cyan
Write-Host ""

try {
    $excelData = Import-Excel -Path $ExcelPath -WorksheetName "Sheet1" -ErrorAction Stop
    Write-Host "✅ Excel cargado: $($excelData.Count) filas" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR al leer Excel: $_" -ForegroundColor Red
    $conn.Close()
    exit 1
}

# Validar que tiene las columnas esperadas
$firstRow = $excelData[0]
$hasId = $firstRow.PSObject.Properties.Name -contains "Id"
$hasGptDesc = $firstRow.PSObject.Properties.Name -contains "gptDescripcion"

if (-not $hasId -or -not $hasGptDesc) {
    Write-Host "❌ ERROR: Excel debe tener columnas 'Id' y 'gptDescripcion'" -ForegroundColor Red
    Write-Host "   Columnas encontradas: $($firstRow.PSObject.Properties.Name -join ', ')" -ForegroundColor Red
    $conn.Close()
    exit 1
}

Write-Host "✅ Estructura Excel válida (Id, gptDescripcion)" -ForegroundColor Green

# Crear mapa de gptDescripciones por ID
$updateMap = @{}
foreach ($row in $excelData) {
    if ($row.Id) {
        $updateMap[$row.Id] = $row.gptDescripcion
    }
}

Write-Host "   Registros en Excel: $($updateMap.Count)" -ForegroundColor Green
Write-Host ""

# ============================================================================
# 3. LEER TIPOLOGÍAS DE BD Y PROCESAR ACTUALIZACIONES
# ============================================================================

Write-Host "🔍 PROCESANDO ACTUALIZACIONES..." -ForegroundColor Cyan
Write-Host ""

$cambios = @()
$actualizadas = 0
$omitidas = 0

try {
    # Traer todas las tipologías
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Id, Codigo, Nombre, ConfiguracionJson FROM dbo.Tipologias ORDER BY Codigo"
    $reader = $cmd.ExecuteReader()
    $tipologias = @()
    
    while ($reader.Read()) {
        $tipologias += @{
            Id = $reader['Id']
            Codigo = $reader['Codigo']
            Nombre = $reader['Nombre']
            ConfiguracionJson = $reader['ConfiguracionJson'].ToString()
        }
    }
    
    $reader.Close()
    Write-Host "✅ Tipologías cargadas: $($tipologias.Count)" -ForegroundColor Green
    Write-Host ""
    
    # Procesar cada tipología que está en el Excel
    foreach ($tip in $tipologias) {
        if ($updateMap.ContainsKey($tip.Id)) {
            try {
                $json = $tip.ConfiguracionJson | ConvertFrom-Json
                
                # Guardar valores anteriores
                $nombreAnterior = $json.tipologiaNombre
                $gptDescAnterior = $json.gptdescripcion
                
                # Actualizar con nuevos valores
                $json.tipologiaNombre = $tip.Nombre
                $json.gptdescripcion = $updateMap[$tip.Id]
                
                $jsonActualizado = $json | ConvertTo-Json -Compress
                
                # Registrar cambio
                $cambios += @{
                    Id = $tip.Id
                    Codigo = $tip.Codigo
                    Nombre = $tip.Nombre
                    NombreAnterior = $nombreAnterior
                    GptDescAnterior = $gptDescAnterior
                    GptDescNuevo = $updateMap[$tip.Id]
                }
                
                if (-not $DryRun) {
                    $updateCmd = $conn.CreateCommand()
                    $updateCmd.CommandText = "UPDATE dbo.Tipologias SET ConfiguracionJson = @json, UpdatedAt = GETUTCNOW() WHERE Id = @id"
                    $updateCmd.Parameters.AddWithValue("@json", $jsonActualizado) | Out-Null
                    $updateCmd.Parameters.AddWithValue("@id", $tip.Id) | Out-Null
                    $updateCmd.ExecuteNonQuery() | Out-Null
                }
                
                Write-Host "  ✓ ID=$($tip.Id) [$($tip.Codigo)]" -ForegroundColor Green
                Write-Host "    - tipologiaNombre: '$nombreAnterior' → '$($tip.Nombre)'" -ForegroundColor Gray
                Write-Host "    - gptDescripcion: '$gptDescAnterior' → '$($updateMap[$tip.Id])'" -ForegroundColor Gray
                
                $actualizadas++
                
            } catch {
                Write-Host "  ❌ ID=$($tip.Id): $_" -ForegroundColor Red
            }
        }
    }
    
} finally {
    $conn.Close()
}

Write-Host ""
Write-Host "═" * 80 -ForegroundColor Cyan

# ============================================================================
# 4. GENERAR REPORTE
# ============================================================================

Write-Host ""
Write-Host "📋 REPORTE FINAL" -ForegroundColor Cyan
Write-Host ""
Write-Host "Actualizadas: $actualizadas" -ForegroundColor Green
Write-Host "Cambios registrados: $($cambios.Count)" -ForegroundColor Green

if ($DryRun) {
    Write-Host ""
    Write-Host "⚠️  MODO DRY RUN: Los cambios NO fueron guardados en la BD" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Para aplicar los cambios, ejecuta:" -ForegroundColor Yellow
    Write-Host "  .\scripts\Actualizar-TipologiaNombre-EnTipologias.ps1 -ExcelPath `"$ExcelPath`" -DryRun `$false" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "✅ Cambios aplicados exitosamente en la BD" -ForegroundColor Green
}

# Guardar reporte
$reportPath = "$backupDir\Reporte_Actualizacion_$timestamp.txt"
$reportLines = @()
$reportLines += "REPORTE DE ACTUALIZACIÓN DE TIPOLOGÍAS"
$reportLines += "====================================="
$reportLines += "Fecha: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$reportLines += "Modo: $(if ($DryRun) { 'DRY RUN (sin cambios)' } else { 'PRODUCCIÓN' })"
$reportLines += ""
$reportLines += "RESUMEN"
$reportLines += "======="
$reportLines += "Excel procesado: $ExcelPath"
$reportLines += "Registros Excel: $($updateMap.Count)"
$reportLines += "Tipologías actualizadas: $actualizadas"
$reportLines += ""
$reportLines += "CAMBIOS DETALLADOS"
$reportLines += "=================="

foreach ($cambio in $cambios) {
    $reportLines += "ID=$($cambio.Id) [$($cambio.Codigo)]"
    $reportLines += "  Nombre: $($cambio.Nombre)"
    $reportLines += "  tipologiaNombre anterior: $($cambio.NombreAnterior)"
    $reportLines += "  gptdescripcion anterior: $($cambio.GptDescAnterior)"
    $reportLines += "  gptdescripcion nuevo: $($cambio.GptDescNuevo)"
    $reportLines += ""
}

$reportLines += "BACKUP"
$reportLines += "======"
$reportLines += "Ubicación: $backupFile"
$reportLines += "SQL guardado: $backupDir\Tipologias_Backup_$timestamp.sql"

$reportLines -join "`r`n" | Out-File -FilePath $reportPath -Encoding UTF8
Write-Host ""
Write-Host "Reporte guardado: $reportPath" -ForegroundColor Green

Write-Host ""
Write-Host "=" * 80 -ForegroundColor Cyan
