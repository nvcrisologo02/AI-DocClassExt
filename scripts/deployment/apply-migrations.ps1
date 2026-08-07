[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SqlServerFqdn,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$ScriptPath,
    [Parameter(Mandatory = $true)][string]$MigrationsDir,
    [switch]$SkipApply
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ScriptPath)) { throw "No existe el script de migrations: $ScriptPath" }
if (-not (Test-Path $MigrationsDir)) { throw "No existe el directorio de migrations: $MigrationsDir" }

# Ultima migration del repo: los nombres empiezan por timestamp yyyyMMddHHmmss,
# el orden lexicografico coincide con el cronologico.
$expected = Get-ChildItem -Path $MigrationsDir -Filter '*.Designer.cs' |
    ForEach-Object { $_.BaseName -replace '\.Designer$', '' } |
    Sort-Object |
    Select-Object -Last 1
if (-not $expected) { throw "No se han encontrado migrations en $MigrationsDir" }
Write-Host "Ultima migration del repo: $expected"

if (-not (Get-Module -ListAvailable -Name SqlServer)) {
    Write-Host "Instalando modulo SqlServer (CurrentUser)..."
    Install-Module -Name SqlServer -Scope CurrentUser -Force -AllowClobber
}
Import-Module SqlServer

$token = az account get-access-token --resource "https://database.windows.net/" --query accessToken -o tsv
if (-not $token) { throw "No se pudo obtener token Entra para Azure SQL (az account get-access-token)" }

function Get-LastAppliedMigration {
    $rows = @(Invoke-Sqlcmd -ServerInstance $SqlServerFqdn -Database $Database -AccessToken $token `
        -Query "SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC")
    if ($rows.Count -eq 0) { return $null }
    return $rows[0].MigrationId
}

$before = Get-LastAppliedMigration
Write-Host "Ultima migration aplicada en ${Database}@${SqlServerFqdn}: $before"

if ($before -eq $expected) {
    Write-Host "La BD ya esta al dia. No hay nada que aplicar."
    exit 0
}

if ($SkipApply) {
    Write-Host "SkipApply activo: hay migrations pendientes pero no se aplica nada (solo pre-check)."
    exit 0
}

Write-Host "Aplicando script idempotente: $ScriptPath"
Invoke-Sqlcmd -ServerInstance $SqlServerFqdn -Database $Database -AccessToken $token `
    -InputFile $ScriptPath -QueryTimeout 3600

$after = Get-LastAppliedMigration
Write-Host "Ultima migration tras aplicar: $after"
if ($after -ne $expected) {
    Write-Error "Verificacion fallida: la BD quedo en '$after' pero el repo espera '$expected'"
    exit 1
}
Write-Host "Migrations aplicadas y verificadas correctamente."
