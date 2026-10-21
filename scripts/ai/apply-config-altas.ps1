#Requires -Version 7.0
<#
.SYNOPSIS
  Aplica en la BD de un entorno el SQL de altas de configuracion generado por
  scripts/database/diff-config-exports.py --emit-inserts (AB#101075).

.DESCRIPTION
  Solo ejecuta un fichero de altas por clave natural: INSERT ... WHERE NOT EXISTS en una
  transaccion, sin UPDATE, DELETE ni Id (ver ADR de ConfigSeed y RELEASE_MANAGEMENT.md,
  regla fija 4). Antes de ejecutar:
    - rechaza el fichero si contiene algo distinto de INSERT INTO sobre las 6 tablas de
      configuracion (UPDATE, DELETE, MERGE, DROP, ALTER, TRUNCATE, IDENTITY_INSERT);
    - copia cada tabla que recibe altas en [dbo].[<Tabla>__bak_<BackupSuffix>]
      (SELECT INTO; si la copia ya existe no se repite, para que un reintento conserve
      la copia anterior a la primera ejecucion).
  Imprime el numero de filas de cada tabla antes y despues. Sin INSERT no conecta.

.PARAMETER Server
  FQDN del servidor Azure SQL del destino, p. ej. srbsqlpredocai.database.windows.net

.PARAMETER SqlFile
  Fichero generado por diff-config-exports.py --emit-inserts.

.PARAMETER BackupSuffix
  Sufijo de las copias __bak, p. ej. configseed_build12345.

.EXAMPLE
  az login
  pwsh ./scripts/ai/apply-config-altas.ps1 -Server srbsqlpredocai.database.windows.net -SqlFile altas.sql -BackupSuffix configseed_manual
#>
param(
    [Parameter(Mandatory)][string]$Server,
    [Parameter(Mandatory)][string]$SqlFile,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_]+$')][string]$BackupSuffix
)
$ErrorActionPreference = "Stop"

$allowed = @('ModeloConfigs', 'CatalogoTdn1', 'CatalogoTdn2', 'PromptTemplates', 'Tipologias', 'PluginTipologiaConfigs')
$sql = Get-Content -Raw -Encoding utf8 -LiteralPath $SqlFile

$tables = @([regex]::Matches($sql, '(?m)^INSERT INTO \[dbo\]\.\[(\w+)\]') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
if ($tables.Count -eq 0) {
    Write-Host "Sin altas en $SqlFile; no se conecta a $Server."
    exit 0
}
$unknown = @($tables | Where-Object { $allowed -notcontains $_ })
if ($unknown.Count -gt 0) { throw "El fichero da altas en tablas fuera de configuracion: $($unknown -join ', ')" }

# El generador escribe cada sentencia en mayusculas a principio de linea; dentro de los
# literales N'...' (JSON, prompts) las palabras pueden aparecer, por eso se mira solo el
# inicio de linea y con mayusculas exactas.
$forbidden = [regex]::Matches($sql, '(?m)^\s*(UPDATE|DELETE|MERGE|DROP|ALTER|TRUNCATE|SET\s+IDENTITY_INSERT)\b')
if ($forbidden.Count -gt 0) { throw "El fichero contiene sentencias no permitidas: $(($forbidden | ForEach-Object { $_.Value.Trim() } | Select-Object -Unique) -join ', ')" }

Write-Host "[STEP] Obteniendo token de Entra ID para Azure SQL ..."
$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if ([string]::IsNullOrWhiteSpace($token)) { throw "No se pudo obtener el token de Azure SQL." }

$cs = "Server=tcp:$Server,1433;Database=DocumentIA;Encrypt=True;"
try {
    $conn = New-Object Microsoft.Data.SqlClient.SqlConnection($cs)
} catch {
    $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
}
$conn.AccessToken = $token
$conn.Open()

function Invoke-Scalar([string]$text) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 0
    $cmd.CommandText = $text
    return $cmd.ExecuteScalar()
}

try {
    $before = @{}
    foreach ($t in $tables) {
        $before[$t] = Invoke-Scalar "SELECT COUNT(*) FROM [dbo].[$t]"
        $bak = "$($t)__bak_$BackupSuffix"
        if (Invoke-Scalar "SELECT OBJECT_ID(N'[dbo].[$bak]', N'U')") {
            Write-Host "[INFO] La copia [dbo].[$bak] ya existe; se conserva."
        } else {
            [void](Invoke-Scalar "SELECT * INTO [dbo].[$bak] FROM [dbo].[$t]; SELECT 1")
            Write-Host "[OK] Copia [dbo].[$bak] ($($before[$t]) filas)"
        }
    }

    Write-Host "[STEP] Ejecutando $SqlFile en $Server ..."
    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 0
    $cmd.CommandText = $sql
    [void]$cmd.ExecuteNonQuery()

    foreach ($t in $tables) {
        $after = Invoke-Scalar "SELECT COUNT(*) FROM [dbo].[$t]"
        Write-Host ("{0}: {1} -> {2} filas (+{3})" -f $t, $before[$t], $after, ($after - $before[$t]))
    }
} finally {
    $conn.Close()
}
