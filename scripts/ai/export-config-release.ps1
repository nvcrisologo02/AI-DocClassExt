<#
.SYNOPSIS
  Genera el export idempotente de configuracion (.sql) y el fichero de hashes de
  deriva por tabla para un release de DocumentIA.

.DESCRIPTION
  Se apoya en scripts/database/replicate-config-data.ps1 (modo Export, EntraAuth)
  para generar el .sql idempotente con las 6 tablas de configuracion (ModeloConfigs,
  PromptTemplates, Tipologias, CatalogoTdn1, CatalogoTdn2, PluginTipologiaConfigs), y
  en scripts/ai/config-hash.sql para calcular un hash SHA2_256 por tabla (5 tablas;
  PluginTipologiaConfigs queda fuera del hash a proposito) que el pipeline
  config-seed usara para detectar deriva de configuracion entre releases.

  No modifica ninguna base de datos: tanto el export como el calculo de hashes son
  solo lectura (SELECT).

.PARAMETER SourceServer
  FQDN del servidor Azure SQL de origen, p. ej. srbsqldevdocai.database.windows.net

.PARAMETER ReleaseTag
  Etiqueta del release, p. ej. release-2026-09-15

.PARAMETER OutDir
  Carpeta de salida. Por defecto artifacts/db-config (esta en .gitignore).

.EXAMPLE
  az login
  pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqldevdocai.database.windows.net -ReleaseTag release-2026-09-15
#>
param(
    [Parameter(Mandatory)][string]$SourceServer,     # p. ej. srbsqldevdocai.database.windows.net
    [Parameter(Mandatory)][string]$ReleaseTag,       # p. ej. release-2026-09-15
    [string]$OutDir = "artifacts/db-config"
)
$ErrorActionPreference = "Stop"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Se requiere Azure CLI (az) autenticado ('az login')."
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$cs = "Server=tcp:$SourceServer,1433;Database=DocumentIA;Encrypt=True;"
$sqlOut = Join-Path $OutDir "config-$ReleaseTag.sql"

Write-Host "[STEP] Obteniendo token de Entra ID para Azure SQL ..."
$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "No se pudo obtener el token de Azure SQL. Ejecuta 'az login' e intentalo de nuevo."
}

Write-Host "[STEP] Generando export idempotente desde $SourceServer ..."
# Se pasa el token ya obtenido via -SourceAccessToken para que replicate-config-data.ps1
# no repita la llamada a az ni dependa de ningun flujo interactivo.
pwsh ./scripts/database/replicate-config-data.ps1 -Mode Export -EntraAuth -SourceAccessToken $token -SourceConnectionString $cs -OutputFile $sqlOut
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "replicate-config-data.ps1 (Export) fallo con codigo $LASTEXITCODE."
}

Write-Host "[STEP] Calculando hashes de deriva (config-hash.sql) ..."
# Preferir Microsoft.Data.SqlClient (PS7) y caer en System.Data.SqlClient, igual que
# hace replicate-config-data.ps1, porque System.Data.SqlClient puede no estar cargado.
try {
    $conn = New-Object Microsoft.Data.SqlClient.SqlConnection($cs)
} catch {
    $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
}
$conn.AccessToken = $token
$conn.Open()
$hashes = @{}
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 0
    $cmd.CommandText = Get-Content -Raw scripts/ai/config-hash.sql
    $r = $cmd.ExecuteReader()
    try {
        do { while ($r.Read()) { $hashes[[string]$r["Tabla"]] = [string]$r["Hash"] } } while ($r.NextResult())
    } finally { $r.Close() }
} finally {
    $conn.Close()
}

[pscustomobject]@{
    release        = $ReleaseTag
    source         = $SourceServer
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    hashes         = $hashes
} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $OutDir "config-$ReleaseTag.hashes.json")

Write-Host "generado $sqlOut y su fichero de hashes"
