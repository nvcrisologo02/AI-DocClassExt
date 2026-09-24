<#
.SYNOPSIS
  Ejecuta una consulta contra una BD Azure SQL de DocumentIA con token de Entra ID y
  muestra las filas en tabla.

.DESCRIPTION
  Pensado para las consultas de solo lectura del runbook de release
  (docs/procedimientos/RELEASE_MANAGEMENT.md: 0.5, 4.1 y comprobaciones de 2.4.5).
  `sqlcmd -G` falla por MFA en este entorno, asi que el token se pide a az CLI
  (recurso https://database.windows.net/) y se pasa a la conexion con AccessToken,
  igual que replicate-config-data.ps1 -EntraAuth. Requiere `az login` previo.

  El script ejecuta lo que reciba en -Query: no lo uses para escrituras salvo que el
  paso del runbook lo indique. No parte por GO: los .sql con lotes GO no van aqui.

.PARAMETER Query
  Texto SQL a ejecutar (una sola sentencia o lote sin GO).

.PARAMETER Server
  FQDN del servidor, p. ej. srbsqlpredocai.database.windows.net. Obligatorio: no hay
  valor por defecto para no apuntar a un entorno por descuido.

.PARAMETER Database
  Base de datos. Por defecto DocumentIA.

.EXAMPLE
  az login
  pwsh ./scripts/database/query-sql.ps1 -Server srbsqlprodocai.database.windows.net `
    -Query "SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC"
#>
param(
    [Parameter(Mandatory)][string]$Query,
    [Parameter(Mandatory)][string]$Server,
    [string]$Database = "DocumentIA"
)
$ErrorActionPreference = "Stop"
$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if (-not $token) { throw "Sin token. Ejecuta az login." }
$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = "Server=tcp:$Server,1433;Database=$Database;Encrypt=True;"
$conn.AccessToken = $token
$conn.Open()
$cmd = $conn.CreateCommand(); $cmd.CommandText = $Query; $cmd.CommandTimeout = 300
$da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
$dt = New-Object System.Data.DataTable
$null = $da.Fill($dt)
$dt | Format-Table -AutoSize | Out-String -Width 400
$conn.Close()
