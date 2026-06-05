# Backup Script para Tipologias - Fase 3 Preparacion
# Proposito: Hacer backup de las 204 tipologias antes de migracion v1.4 to v1.5
# Fecha: 2026-06-05

param(
    [string]$ConnectionString = $null,
    [string]$BackupPath = "$PSScriptRoot\..\artifacts\backups"
)

$ErrorActionPreference = "Stop"

# Funciones auxiliares
function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "========================================================================" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "========================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Yellow
}

function Get-ConnectionString {
    if ($ConnectionString) {
        Write-Info "Using ConnectionString from parameter"
        return $ConnectionString
    }
    
    # Try local.settings.json
    $localSettingsPath = "$PSScriptRoot\..\src\backend\DocumentIA.Functions\local.settings.json"
    if (Test-Path $localSettingsPath) {
        try {
            $content = Get-Content $localSettingsPath -Raw | ConvertFrom-Json
            $connStr = $content.Values.SqlConnectionString
            if ($connStr) {
                Write-Info "ConnectionString found in local.settings.json"
                return $connStr
            }
        }
        catch {
            Write-Info "Could not read local.settings.json"
        }
    }
    
    # Default
    $default = "Server=127.0.0.1,1433;Database=DocumentIA;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
    Write-Info "Using default connection string (local SQL Server)"
    return $default
}

function Test-DatabaseConnection {
    param([string]$ConnStr)
    
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection
        $conn.ConnectionString = $ConnStr
        $conn.Open()
        
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT @@VERSION"
        $result = $cmd.ExecuteScalar()
        
        $conn.Close()
        
        Write-Success "Database connection successful"
        return $true
    }
    catch {
        Write-ErrorMsg "Error connecting to database: $_"
        return $false
    }
}

function Export-TipologiasData {
    param(
        [string]$ConnStr,
        [string]$OutputPath
    )
    
    Write-Header "Exporting Tipologias"
    
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection
        $conn.ConnectionString = $ConnStr
        $conn.Open()
        
        $query = @"
SELECT 
    Id,
    Codigo,
    Nombre,
    Version,
    Activa,
    Estado,
    PromptGPT,
    ConfiguracionJson,
    ModeloClasificacionDI,
    UmbralClasificacion,
    ModeloExtraccionDI,
    UmbralExtraccion,
    FechaCreacion,
    FechaActualizacion,
    CreadoPor,
    PublicadaPor,
    PublicadaEn,
    VersionPublicada
FROM Tipologias
ORDER BY Id ASC
"@
        
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        $cmd.CommandTimeout = 300
        
        $reader = $cmd.ExecuteReader()
        $tipologias = @()
        
        while ($reader.Read()) {
            $obj = @{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $fieldName = $reader.GetName($i)
                $value = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
                $obj[$fieldName] = $value
            }
            $tipologias += $obj
        }
        
        $reader.Close()
        $conn.Close()
        
        Write-Success "Exported $($tipologias.Count) tipologias"
        
        # Save JSON
        $jsonPath = Join-Path $OutputPath "tipologias-backup.json"
        $tipologias | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonPath -Encoding UTF8
        Write-Success "JSON saved: $jsonPath"
        
        # Save CSV (metadata only)
        $csvPath = Join-Path $OutputPath "tipologias-backup.csv"
        $csvData = @()
        foreach ($t in $tipologias) {
            $csvData += @{
                Id = $t.Id
                Codigo = $t.Codigo
                Nombre = $t.Nombre
                Version = $t.Version
                Activa = $t.Activa
                Estado = $t.Estado
                PromptGPT_Length = if ($t.PromptGPT) { $t.PromptGPT.Length } else { 0 }
                ConfigJson_Length = if ($t.ConfiguracionJson) { $t.ConfiguracionJson.Length } else { 0 }
                FechaCreacion = $t.FechaCreacion
                FechaActualizacion = $t.FechaActualizacion
            }
        }
        $csvData | Export-Csv -Path $csvPath -Encoding UTF8 -NoTypeInformation
        Write-Success "CSV saved: $csvPath"
        
        # Statistics
        Write-Header "Tipologias Statistics"
        
        $conPromptGPT = ($tipologias | Where-Object { $_.PromptGPT -and $_.PromptGPT -ne "" }).Count
        $conConfigJson = ($tipologias | Where-Object { $_.ConfiguracionJson -and $_.ConfiguracionJson -ne "" }).Count
        $conAmbos = ($tipologias | Where-Object { 
            ($_.PromptGPT -and $_.PromptGPT -ne "") -and 
            ($_.ConfiguracionJson -and $_.ConfiguracionJson -ne "") 
        }).Count
        $soloPromptGPT = ($tipologias | Where-Object { 
            ($_.PromptGPT -and $_.PromptGPT -ne "") -and 
            (-not $_.ConfiguracionJson -or $_.ConfiguracionJson -eq "") 
        }).Count
        $soloConfigJson = ($tipologias | Where-Object { 
            (-not $_.PromptGPT -or $_.PromptGPT -eq "") -and 
            ($_.ConfiguracionJson -and $_.ConfiguracionJson -ne "") 
        }).Count
        $ninguno = ($tipologias | Where-Object { 
            (-not $_.PromptGPT -or $_.PromptGPT -eq "") -and 
            (-not $_.ConfiguracionJson -or $_.ConfiguracionJson -eq "") 
        }).Count
        
        Write-Info "Total:                 $($tipologias.Count)"
        Write-Info "Con PromptGPT:         $conPromptGPT"
        Write-Info "Con ConfigJson:        $conConfigJson"
        Write-Info "Con ambos (A1):        $conAmbos"
        Write-Info "Solo PromptGPT (A2):   $soloPromptGPT"
        Write-Info "Solo ConfigJson:       $soloConfigJson"
        Write-Info "Ninguno (A3):          $ninguno"
        
        $stats = @{
            Total = $tipologias.Count
            ConPromptGPT = $conPromptGPT
            ConConfigJson = $conConfigJson
            ConAmbos = $conAmbos
            SoloPromptGPT = $soloPromptGPT
            SoloConfigJson = $soloConfigJson
            Ninguno = $ninguno
        }
        
        # Save stats
        $statsPath = Join-Path $OutputPath "tipologias-stats.json"
        $stats | ConvertTo-Json | Out-File -FilePath $statsPath -Encoding UTF8
        Write-Success "Stats saved: $statsPath"
        
        return $stats
    }
    catch {
        Write-ErrorMsg "Error exporting tipologias: $_"
        throw
    }
}

function Export-TablesMetadata {
    param(
        [string]$ConnStr,
        [string]$OutputPath
    )
    
    Write-Header "Exporting Tables Metadata"
    
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection
        $conn.ConnectionString = $ConnStr
        $conn.Open()
        
        $query = @"
SELECT 'Tipologias' AS TableName, COUNT(*) AS RowCount FROM Tipologias
UNION ALL
SELECT 'Documentos', COUNT(*) FROM Documentos
UNION ALL
SELECT 'ResultadosProcesamiento', COUNT(*) FROM ResultadosProcesamiento
UNION ALL
SELECT 'Auditoria', COUNT(*) FROM Auditoria
ORDER BY TableName
"@
        
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        $cmd.CommandTimeout = 300
        
        $reader = $cmd.ExecuteReader()
        $metadata = @()
        
        while ($reader.Read()) {
            $metadata += @{
                TableName = $reader.GetString(0)
                RowCount = $reader.GetInt32(1)
            }
        }
        
        $reader.Close()
        $conn.Close()
        
        Write-Success "Metadata exported"
        
        foreach ($item in $metadata) {
            Write-Info "$($item.TableName): $($item.RowCount) rows"
        }
        
        $metaPath = Join-Path $OutputPath "tables-metadata.json"
        $metadata | ConvertTo-Json | Out-File -FilePath $metaPath -Encoding UTF8
        Write-Success "Metadata saved: $metaPath"
    }
    catch {
        Write-ErrorMsg "Error exporting metadata: $_"
    }
}

# Main flow
Write-Header "BACKUP TIPOLOGIAS - FASE 3"

try {
    # Create backup directory
    if (-not (Test-Path $BackupPath)) {
        New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupSessionDir = Join-Path $BackupPath $timestamp
    New-Item -ItemType Directory -Path $backupSessionDir -Force | Out-Null
    
    Write-Success "Backup directory: $backupSessionDir"
    
    # Get connection string
    $connStr = Get-ConnectionString
    
    # Validate connection
    if (-not (Test-DatabaseConnection $connStr)) {
        Write-ErrorMsg "Cannot connect to database"
        exit 1
    }
    
    # Export tipologias
    $stats = Export-TipologiasData -ConnStr $connStr -OutputPath $backupSessionDir
    
    # Export metadata
    Export-TablesMetadata -ConnStr $connStr -OutputPath $backupSessionDir
    
    # Create info file
    $infoPath = Join-Path $backupSessionDir "BACKUP_INFO.txt"
    $info = @"
=============================================================================
BACKUP DE TIPOLOGIAS - FASE 3
=============================================================================
Fecha: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Backup Path: $backupSessionDir

ESTADISTICAS:
- Total de Tipologias: $($stats.Total)
- Con PromptGPT: $($stats.ConPromptGPT)
- Con ConfigJson: $($stats.ConConfigJson)
- Con ambos (A1): $($stats.ConAmbos)
- Solo PromptGPT (A2): $($stats.SoloPromptGPT)
- Ninguno (A3): $($stats.Ninguno)

ARCHIVOS:
- tipologias-backup.json
- tipologias-backup.csv
- tables-metadata.json
- BACKUP_INFO.txt

Status: BACKUP READY FOR FASE 3 MIGRATION
=============================================================================
"@
    $info | Out-File -FilePath $infoPath -Encoding UTF8
    
    Write-Header "BACKUP COMPLETED SUCCESSFULLY"
    Write-Info "Directory: $backupSessionDir"
    Write-Info "Next: Execute Fase 3 Task 4 (Classification)"
    Write-Success "Backup ready for migration"
}
catch {
    Write-ErrorMsg "Backup failed: $_"
    exit 1
}

Write-Host ""
