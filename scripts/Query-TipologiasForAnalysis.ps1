# Script para consultar tipologías desde BD usando la conexión configurada
# Lee la cadena de conexión desde local.settings.json o appsettings.json

param(
    [string]$LocalSettingsPath = "${env:USERPROFILE}\.NET\UserSecrets\DocumentIA.Functions\local.settings.json",
    [string]$ConnectionStringOverride = ""
)

# Usar System.Data.SqlClient sin módulo externo
Add-Type -AssemblyName "System.Data"

# Función para leer JSON
function Get-JsonContent {
    param([string]$FilePath)
    if (Test-Path $FilePath) {
        try {
            return Get-Content $FilePath -Raw | ConvertFrom-Json
            } catch {
            Write-Host "⚠ No se puede parsear JSON en $FilePath" -ForegroundColor Yellow
            return $null
        }
    }
    return $null
}

Write-Host "=== BUSCANDO CONFIGURACIÓN DE CONEXIÓN ===" -ForegroundColor Cyan

# 1. Intentar leer local.settings.json si está en UserSecrets o en workspace
$connectionString = ""

# Intento 1: UserSecrets (.NET secrets)
$secretsPath = "${env:USERPROFILE}\.dotnet\user-secrets\DocumentIA.Functions\settings.json"
Write-Host "Buscando en secrets: $secretsPath" -ForegroundColor Gray
$secretsContent = Get-JsonContent $secretsPath
if ($secretsContent -and $secretsContent.Values.'ConnectionStrings:DocumentIA') {
    $connectionString = $secretsContent.Values.'ConnectionStrings:DocumentIA'
    Write-Host "✓ Encontrado en .NET secrets" -ForegroundColor Green
}

# Intento 2: Workspace local.settings.json
$localSettingsPath = "c:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Functions\local.settings.json"
if (!$connectionString -and (Test-Path $localSettingsPath)) {
    Write-Host "Buscando en: $localSettingsPath" -ForegroundColor Gray
    $localSettings = Get-JsonContent $localSettingsPath
    if ($localSettings -and $localSettings.Values.'ConnectionStrings:DocumentIA') {
        $connectionString = $localSettings.Values.'ConnectionStrings:DocumentIA'
        Write-Host "✓ Encontrado en local.settings.json" -ForegroundColor Green
    }
}

# Intento 3: Override si se proporciona
if ($ConnectionStringOverride) {
    $connectionString = $ConnectionStringOverride
    Write-Host "✓ Usando connection string de override" -ForegroundColor Green
}

# Fallback si no se encuentra
if (!$connectionString) {
    Write-Host "⚠ No se encontró connection string configurado" -ForegroundColor Yellow
    Write-Host "Usando fallback: localhost,1433 / sa / YourStrong@Passw0rd" -ForegroundColor Yellow
    $connectionString = "Server=localhost,1433;Database=DocumentIA;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
}

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
        Write-Host "❌ Error al ejecutar query: $_" -ForegroundColor Red
        return $null
    }
}

Write-Host "`n=== INICIANDO CONSULTA ===" -ForegroundColor Cyan
Write-Host "Cadena de conexión (primeros 80 caracteres): $($connectionString.Substring(0, [Math]::Min(80, $connectionString.Length)))..." -ForegroundColor Gray

try {
    # Query 1: Obtener tipologías específicas
    $queryTipologias = @"
    SELECT TOP 20
        Id,
        Codigo,
        Nombre,
        ConfiguracionJson,
        PublicadoEn,
        Estado
    FROM Tipologias
    WHERE LOWER(REPLACE(REPLACE(Codigo, '.', '-'), '_', '-')) IN ('escr-01', 'sere-24')
       OR Codigo IN ('ESCR-01', 'ESCR.01', 'SERE-24', 'SERE.24', 'escr-01', 'sere-24')
    ORDER BY Codigo;
"@

    Write-Host "`n=== BÚSQUEDA: Tipologías (ESCR-01, SERE-24) ===" -ForegroundColor Green
    $tipologias = Invoke-SqlQuery -Query $queryTipologias -ConnString $connectionString

    if ($tipologias -and $tipologias.Rows.Count -gt 0) {
        Write-Host "Se encontraron $($tipologias.Rows.Count) tipologías"
        foreach ($row in $tipologias.Rows) {
            Write-Host "`n--- Tipología: $($row['Codigo']) ---" -ForegroundColor Yellow
            Write-Host "Nombre: $($row['Nombre'])"
            Write-Host "ID: $($row['Id'])"
            Write-Host "Estado: $($row['Estado'])"
            Write-Host "ConfiguracionJson:" 
            Write-Host $row['ConfiguracionJson']
        }
    } else {
        Write-Host "⚠ No se encontraron tipologías con esos códigos (probe primero si la BD está disponible)" -ForegroundColor Yellow
    }

    # Query 2: Primero verificar si existen las tablas
    $queryTables = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('Tipologias', 'CatalogoTdn1', 'CatalogoTdn2')"
    Write-Host "`n=== VERIFICANDO TABLAS ===" -ForegroundColor Green
    $tables = Invoke-SqlQuery -Query $queryTables -ConnString $connectionString
    if ($tables -and $tables.Rows.Count -gt 0) {
        $tables.Rows | ForEach-Object { Write-Host "✓ Tabla: $($_['TABLE_NAME'])" }
    } else {
        Write-Host "❌ No se encontraron las tablas esperadas. Verifica las migraciones de BD." -ForegroundColor Red
    }

    # Query 3: Contar registros clave
    $queryCount = @"
    SELECT 'Tipologias' as Tabla, COUNT(*) as Total FROM Tipologias
    UNION ALL
    SELECT 'CatalogoTdn1' as Tabla, COUNT(*) as Total FROM CatalogoTdn1 WHERE 1=0
    UNION ALL
    SELECT 'CatalogoTdn2' as Tabla, COUNT(*) as Total FROM CatalogoTdn2 WHERE 1=0;
"@

    Write-Host "`n=== CONTEOS ===" -ForegroundColor Green
    $counts = Invoke-SqlQuery -Query $queryCount -ConnString $connectionString
    if ($counts -and $counts.Rows.Count -gt 0) {
        foreach ($row in $counts.Rows) {
            Write-Host "$($row['Tabla']): $($row['Total'])"
        }
    }

    Write-Host "`n✅ Consulta completada" -ForegroundColor Green

} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    Write-Host "Tip: Asegúrate de que:" -ForegroundColor Yellow
    Write-Host "  1. SQL Server está corriendo (docker-compose up sql-server)"
    Write-Host "  2. La BD 'DocumentIA' existe"
    Write-Host "  3. Las migraciones se han ejecutado (dotnet ef database update)"
    exit 1
}
