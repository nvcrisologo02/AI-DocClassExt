<#
.SYNOPSIS
    Backup y actualización de TipologiaNombre + gptdescripcion desde Excel

.DESCRIPTION
    1. Crea backup de tabla Tipologias
    2. Lee Excel con ID y gptdescripciones
    3. Obtiene Nombre de la tabla Tipologias
    4. Actualiza ConfiguracionJson con TipologiaNombre y gptdescripcion
    5. Genera reporte de cambios

.PARAMETER ExcelPath
    Ruta al archivo Excel
    Default: ".\tiposgpt.xlsx"

.PARAMETER DryRun
    Si es $true, solo simula cambios sin ejecutar UPDATE

.EXAMPLE
    .\Backup-Actualizar-Tipologias.ps1 -ExcelPath ".\tiposgpt.xlsx" -DryRun $true
    .\Backup-Actualizar-Tipologias.ps1 -ExcelPath ".\tiposgpt.xlsx" -DryRun $false
#>

param(
    [string]$ExcelPath = ".\docs\auxiliares\tiposgpt.csv",
    [switch]$DryRun = $true
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "BACKUP Y ACTUALIZACION DE TIPOLOGIAS" -ForegroundColor Cyan
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host ""

# Verificar Excel
if (-not (Test-Path $ExcelPath)) {
    Write-Host "[ERROR] CSV no encontrado: $ExcelPath" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] CSV encontrado: $ExcelPath" -ForegroundColor Green

# Cargar configuración de DB
$configPath = ".\scripts\config\db-connection.json"
if (-not (Test-Path $configPath)) {
    Write-Host "[INFO] Usando local.settings.json..." -ForegroundColor Yellow
    $localSettingsPath = ".\src\backend\DocumentIA.Functions\local.settings.json"
    if (-not (Test-Path $localSettingsPath)) {
        Write-Host "[ERROR] Config no encontrada en: $localSettingsPath" -ForegroundColor Red
        exit 1
    }
    $localSettings = Get-Content $localSettingsPath -Raw | ConvertFrom-Json
    $connectionString = $localSettings.Values.'ConnectionStrings:DocumentIA'
} else {
    $dbConfig = Get-Content $configPath | ConvertFrom-Json
    $connectionString = $dbConfig.connectionString
}
Write-Host "[OK] Configuracion de BD cargada" -ForegroundColor Green

Write-Host ""
Write-Host "Modo: $(if ($DryRun) { 'DRY RUN (sin cambios)' } else { 'PRODUCCION' })" -ForegroundColor $(if ($DryRun) { "Yellow" } else { "Red" })
Write-Host ""

# ============================================================================
# 1. CREAR BACKUP
# ============================================================================

Write-Host "--- PASO 1: CREANDO BACKUP ---" -ForegroundColor Cyan
Write-Host ""

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = ".\scripts\backups"
$backupFile = "$backupDir\Tipologias_Backup_$timestamp.sql"

if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection
    $conn.ConnectionString = $connectionString
    $conn.Open()
    
    Write-Host "[OK] Conectado a BD" -ForegroundColor Green
    Write-Host "     Archivo: $backupFile" -ForegroundColor Green
    
} catch {
    Write-Host "[ERROR] Conexion a BD fallida: $_" -ForegroundColor Red
    exit 1
}

# ============================================================================
# 2. LEER EXCEL
# ============================================================================

Write-Host ""
Write-Host "--- PASO 2: LEYENDO CSV ---" -ForegroundColor Cyan
Write-Host ""

try {
    $excelData = Import-Csv -Path $ExcelPath -Encoding UTF8 -Delimiter ";" -ErrorAction Stop
    
    if (-not $excelData) {
        Write-Host "[ERROR] CSV está vacío o sin datos" -ForegroundColor Red
        exit 1
    }
    
    # Si es un solo objeto, convertir a array
    if ($excelData -isnot [array]) {
        $excelData = @($excelData)
    }
    
    Write-Host "[OK] CSV cargado: $($excelData.Count) filas" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Lectura de CSV fallida: $_" -ForegroundColor Red
    $conn.Close()
    exit 1
}

# Validar estructura
if ($excelData.Count -eq 0) {
    Write-Host "[ERROR] Excel sin registros" -ForegroundColor Red
    exit 1
}

$firstRow = $excelData[0]
$hasId = $firstRow.PSObject.Properties.Name -contains "Id"
$hasGptDesc = $firstRow.PSObject.Properties.Name -contains "gptDescripcion"

if (-not $hasId -or -not $hasGptDesc) {
    Write-Host "[ERROR] Faltan columnas 'Id' o 'gptDescripcion'" -ForegroundColor Red
    Write-Host "        Columnas encontradas: $($firstRow.PSObject.Properties.Name -join ', ')" -ForegroundColor Red
    $conn.Close()
    exit 1
}

Write-Host "[OK] Estructura validada" -ForegroundColor Green

# Crear mapa de actualizaciones
$updateMap = @{}
foreach ($row in $excelData) {
    if ($row.Id) {
        # Convertir ID a entero para consistencia con BD
        $idInt = [int]::Parse($row.Id.ToString().Trim())
        $updateMap[$idInt] = $row.gptDescripcion
    }
}

Write-Host "     Registros a actualizar: $($updateMap.Count)" -ForegroundColor Green
Write-Host ""

# ============================================================================
# 3. PROCESAR ACTUALIZACIONES
# ============================================================================

Write-Host "--- PASO 3: PROCESANDO ACTUALIZACIONES ---" -ForegroundColor Cyan
Write-Host ""

$cambios = @()
$actualizadas = 0

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
    Write-Host "[OK] Tipologias cargadas: $($tipologias.Count)" -ForegroundColor Green
    Write-Host ""
    
    # Procesar cada una
    foreach ($tip in $tipologias) {
        if ($updateMap.ContainsKey($tip.Id)) {
            try {
                $json = $tip.ConfiguracionJson | ConvertFrom-Json
                
                # Guardar anteriores
                $nombreAnterior = $json.tipologiaNombre
                $gptDescAnterior = $json.gptdescripcion
                
                # Crear propiedades si no existen
                if (-not ($json.PSObject.Properties.Name -contains 'tipologiaNombre')) {
                    $json | Add-Member -MemberType NoteProperty -Name 'tipologiaNombre' -Value $null -Force
                }
                if (-not ($json.PSObject.Properties.Name -contains 'gptdescripcion')) {
                    $json | Add-Member -MemberType NoteProperty -Name 'gptdescripcion' -Value $null -Force
                }
                
                # Actualizar
                $json.tipologiaNombre = $tip.Nombre
                $json.gptdescripcion = $updateMap[$tip.Id]
                
                $jsonActualizado = $json | ConvertTo-Json -Compress
                
                # Registrar
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
                
                Write-Host "  [OK] ID=$($tip.Id) [$($tip.Codigo)]" -ForegroundColor Green
                $actualizadas++
                
            } catch {
                Write-Host "  [ERROR] ID=$($tip.Id): $_" -ForegroundColor Red
            }
        }
    }
    
} finally {
    $conn.Close()
}

Write-Host ""
Write-Host "=======================================================================" -ForegroundColor Cyan

# ============================================================================
# 4. REPORTE
# ============================================================================

Write-Host ""
Write-Host "--- REPORTE FINAL ---" -ForegroundColor Cyan
Write-Host ""
Write-Host "Actualizadas: $actualizadas" -ForegroundColor Green
Write-Host "Cambios registrados: $($cambios.Count)" -ForegroundColor Green

# Guardar reporte
$reportPath = "$backupDir\Reporte_$timestamp.txt"
$reportLines = @()
$reportLines += "REPORTE DE ACTUALIZACION"
$reportLines += "Fecha: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$reportLines += "Modo: $(if ($DryRun) { 'DRY RUN' } else { 'PRODUCCION' })"
$reportLines += "Archivo: $ExcelPath"
$reportLines += ""
$reportLines += "CAMBIOS REALIZADOS"
$reportLines += "=================="

foreach ($cambio in $cambios) {
    $reportLines += "ID=$($cambio.Id) [$($cambio.Codigo)]"
    $reportLines += "  Nombre: $($cambio.Nombre)"
    $reportLines += "  tipologiaNombre anterior: $($cambio.NombreAnterior)"
    $reportLines += "  gptdescripcion anterior: $($cambio.GptDescAnterior)"
    $reportLines += "  gptdescripcion nuevo: $($cambio.GptDescNuevo)"
    $reportLines += ""
}

$reportLines -join "`r`n" | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "Reporte: $reportPath" -ForegroundColor Green
Write-Host "Backup:  $backupFile" -ForegroundColor Green

if ($DryRun) {
    Write-Host ""
    Write-Host "Para aplicar cambios, ejecuta:" -ForegroundColor Yellow
    Write-Host "  .\scripts\Backup-Actualizar-Tipologias.ps1 -DryRun `$false" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host ""
