# Script para consultar tipologias desde BD usando la conexion configurada
# Lee la cadena de conexion desde local.settings.json

Add-Type -AssemblyName "System.Data"

# Funcion para leer JSON
function Get-JsonContent {
    param([string]$FilePath)
    if (Test-Path $FilePath) {
        try {
            return Get-Content $FilePath -Raw | ConvertFrom-Json
        } catch {
            Write-Host "Aviso: No se puede parsear JSON en $FilePath" -ForegroundColor Yellow
            return $null
        }
    }
    return $null
}

Write-Host "=== BUSCANDO CONFIGURACION DE CONEXION ===" -ForegroundColor Cyan

# Intentar leer local.settings.json
$connectionString = ""
$localSettingsPath = "c:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Functions\local.settings.json"

if (Test-Path $localSettingsPath) {
    Write-Host "Leyendo: $localSettingsPath" -ForegroundColor Gray
    $localSettings = Get-JsonContent $localSettingsPath
    if ($localSettings -and $localSettings.Values.'ConnectionStrings:DocumentIA') {
        $connectionString = $localSettings.Values.'ConnectionStrings:DocumentIA'
        Write-Host "OK: Encontrado en local.settings.json" -ForegroundColor Green
    }
}

if (!$connectionString) {
    Write-Host "Usando fallback: localhost,1433 (sa / YourStrong@Passw0rd)" -ForegroundColor Yellow
    $connectionString = "Server=localhost,1433;Database=DocumentIA;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
}

Write-Host "Cadena de conexion: $($connectionString.Substring(0, 60))..." -ForegroundColor Gray

# Funcion para ejecutar queries
function Invoke-SqlQuery {
    param([string]$Query, [string]$ConnString)
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection($ConnString)
        $conn.Open()
        $cmd = New-Object System.Data.SqlClient.SqlCommand($Query, $conn)
        $cmd.CommandTimeout = 30
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $dataset = New-Object System.Data.DataSet
        [void]$adapter.Fill($dataset)
        $conn.Close()
        return $dataset.Tables[0]
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
        return $null
    }
}

Write-Host "`n=== INICIANDO CONSULTA ===" -ForegroundColor Cyan

try {
    # Query 1: Verificar tablas
    $queryTables = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('Tipologias', 'CatalogoTdn1', 'CatalogoTdn2')"
    Write-Host "`nVerificando tablas..." -ForegroundColor Green
    $tables = Invoke-SqlQuery -Query $queryTables -ConnString $connectionString
    
    if ($tables -and $tables.Rows.Count -gt 0) {
        Write-Host "Tablas encontradas:"
        foreach ($row in $tables.Rows) {
            Write-Host "  - $($row[0])"
        }
    } else {
        Write-Host "Aviso: No se encontraron las tablas esperadas" -ForegroundColor Yellow
    }

    # Query 2: Obtener tipologias
    $queryTipologias = @"
SELECT 
    Id,
    Codigo,
    Nombre,
    ConfiguracionJson,
    Estado
FROM Tipologias
WHERE Codigo IN ('ESCR-01', 'ESCR.01', 'SERE-24', 'SERE.24')
   OR LOWER(REPLACE(Codigo, '.', '-')) IN ('escr-01', 'sere-24')
ORDER BY Codigo;
"@

    Write-Host "`nBuscando tipologias (ESCR-01, SERE-24)..." -ForegroundColor Green
    $tipologias = Invoke-SqlQuery -Query $queryTipologias -ConnString $connectionString
    
    if ($tipologias -and $tipologias.Rows.Count -gt 0) {
        Write-Host "Encontradas $($tipologias.Rows.Count) tipologias:`n"
        $tipologias | Format-Table -AutoSize
        Write-Host ""
        foreach ($row in $tipologias.Rows) {
            $codigo = $row[0]
            $nombre = $row[1]
            $id = $row[2]
            $estado = $row[3]
            $configJson = $row[4]
            Write-Host "  Codigo: $codigo" -ForegroundColor Yellow
            Write-Host "  Nombre: $nombre"
            Write-Host "  ID: $id"
            Write-Host "  Estado: $estado"
            Write-Host "  Config JSON:"
            Write-Host "    $configJson" -ForegroundColor Gray
            Write-Host ""
        }
    } else {
        Write-Host "No se encontraron tipologias" -ForegroundColor Yellow
    }

    # Query 3: Contar registros
    $queryCount = "SELECT COUNT(*) as Total FROM Tipologias"
    Write-Host "`nConteo de registros..." -ForegroundColor Green
    $count = Invoke-SqlQuery -Query $queryCount -ConnString $connectionString
    if ($count -and $count.Rows.Count -gt 0) {
        Write-Host "Total tipologias en BD: $($count.Rows[0][0])"
    }

    Write-Host "`nOK: Consulta completada" -ForegroundColor Green

} catch {
    Write-Host "Error al consultar: $_" -ForegroundColor Red
    exit 1
}
