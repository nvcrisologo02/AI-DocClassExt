# Aplica el script idempotente de migrations EF Core con pre-check y verificacion.
# Exit codes: 0 = BD al dia (aplicado o ya estaba); 2 = pendientes sin aplicar (solo con -SkipApply); otro = fallo
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

# Migrations del repo: los nombres empiezan por timestamp yyyyMMddHHmmss,
# el orden lexicografico coincide con el cronologico.
$repoMigrations = @(Get-ChildItem -Path $MigrationsDir -Filter '*.Designer.cs' |
    ForEach-Object { $_.BaseName -replace '\.Designer$', '' } |
    Sort-Object)
if ($repoMigrations.Count -eq 0) { throw "No se han encontrado migrations en $MigrationsDir" }
$expected = $repoMigrations[-1]
Write-Host "Migrations en el repo: $($repoMigrations.Count). Ultima: $expected"

$sqlServerModule = Get-Module -ListAvailable -Name SqlServer | Where-Object { $_.Version -ge [version]'21.1.18256' }
if (-not $sqlServerModule) {
    Write-Host "Instalando modulo SqlServer (CurrentUser)..."
    Install-Module -Name SqlServer -Scope CurrentUser -Force -AllowClobber -MinimumVersion 21.1.18256
}
Import-Module SqlServer

$token = az account get-access-token --resource "https://database.windows.net/" --query accessToken -o tsv
if (-not $token) { throw "No se pudo obtener token Entra para Azure SQL (az account get-access-token)" }

# Devuelve el conjunto de migrations registradas en la BD (vacio si no hay tabla).
# Se compara el CONJUNTO y no solo la ultima: una migration registrada a mano fuera de
# EF (caso del indice del Monitor en PRO, que se aplica con ONLINE = ON por script propio)
# puede ser la ultima del repo mientras siguen faltando otras anteriores.
function Get-AppliedMigrations {
    $exists = @(Invoke-Sqlcmd -ServerInstance $SqlServerFqdn -Database $Database -AccessToken $token `
        -Query "SELECT 1 AS T FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = '__EFMigrationsHistory'")
    if ($exists.Count -eq 0) {
        Write-Host "La tabla __EFMigrationsHistory no existe (BD sin migrar): todo pendiente."
        return @()
    }
    $rows = @(Invoke-Sqlcmd -ServerInstance $SqlServerFqdn -Database $Database -AccessToken $token `
        -Query "SELECT MigrationId FROM __EFMigrationsHistory")
    return @($rows | ForEach-Object { $_.MigrationId })
}

$applied = Get-AppliedMigrations
$pending = @($repoMigrations | Where-Object { $applied -notcontains $_ })
Write-Host "Migrations aplicadas en ${Database}@${SqlServerFqdn}: $($applied.Count). Pendientes: $($pending.Count)"
$pending | ForEach-Object { Write-Host "  - $_" }

if ($pending.Count -eq 0) {
    Write-Host "La BD ya esta al dia. No hay nada que aplicar."
    exit 0
}

if ($SkipApply) {
    Write-Host "SkipApply activo: hay migrations pendientes pero no se aplica nada (solo pre-check)."
    exit 2
}

Write-Host "Aplicando script idempotente: $ScriptPath"
Invoke-Sqlcmd -ServerInstance $SqlServerFqdn -Database $Database -AccessToken $token `
    -InputFile $ScriptPath -QueryTimeout 3600 -AbortOnError

$applied = Get-AppliedMigrations
$stillPending = @($repoMigrations | Where-Object { $applied -notcontains $_ })
if ($stillPending.Count -gt 0) {
    Write-Error "Verificacion fallida: siguen sin registrarse en la BD: $($stillPending -join ', ')"
    exit 1
}
Write-Host "Migrations aplicadas y verificadas correctamente. Ultima: $expected"
