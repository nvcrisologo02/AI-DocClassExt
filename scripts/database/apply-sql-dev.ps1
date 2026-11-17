<#
.SYNOPSIS
  Aplica un script .sql (con lotes GO) contra la BD de DEV de DocumentIA con token de
  Entra ID. Muestra los PRINT y los conjuntos de resultados.

.DESCRIPTION
  ESTE SCRIPT ESCRIBE EN LA BASE DE DATOS. Es la pareja de escritura de query-sql.ps1
  (solo lectura). Pensado para las migraciones numeradas de scripts/migrations/ que ya
  llevan su backup e idempotencia dentro; no anade nada de eso por su cuenta.

  sqlcmd -G falla por MFA en este entorno, asi que el token se pide a az CLI (recurso
  https://database.windows.net/) y se pasa a la conexion con AccessToken. Requiere
  az login previo con una cuenta con permisos de escritura en la BD.

  Parte el fichero por lineas que solo contengan GO y ejecuta cada lote en orden. Los
  mensajes PRINT informativos salen con el prefijo [sql]; los SELECT de verificacion de
  cada lote se muestran en tabla. Un error en un lote detiene la ejecucion (los lotes
  anteriores quedan aplicados: cada script debe ser transaccional por si mismo).

.PARAMETER ScriptPath
  Ruta al .sql a aplicar.

.PARAMETER Server
  FQDN del servidor. Por defecto el de DEV (srbsqldevdocai.database.windows.net). Para
  otro entorno hay que indicarlo a proposito; este script no esta pensado para PRO.

.PARAMETER Database
  Base de datos. Por defecto DocumentIA.

.EXAMPLE
  az login
  pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/01-dev-insert-gpt56-luna-rows.sql
#>
param(
    [Parameter(Mandatory)][string]$ScriptPath,
    [string]$Server = "srbsqldevdocai.database.windows.net",
    [string]$Database = "DocumentIA"
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ScriptPath)) { throw "No existe el fichero: $ScriptPath" }

$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if (-not $token) { throw "Sin token. Ejecuta az login." }

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = "Server=tcp:$Server,1433;Database=$Database;Encrypt=True;"
$conn.AccessToken = $token
$conn.FireInfoMessageEventOnUserErrors = $false
$conn.add_InfoMessage({ param($s, $e) Write-Host ("  [sql] " + $e.Message) })
$conn.Open()

$sql = Get-Content -Raw -Encoding UTF8 $ScriptPath
$lotes = [regex]::Split($sql, "(?im)^\s*GO\s*$") | Where-Object { $_.Trim().Length -gt 0 }
Write-Host ("Aplicando " + $lotes.Count + " lote(s) de " + (Split-Path -Leaf $ScriptPath) + " contra " + $Server + "/" + $Database)

$i = 0
try {
    foreach ($lote in $lotes) {
        $i++
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $lote
        $cmd.CommandTimeout = 600
        $da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
        $ds = New-Object System.Data.DataSet
        $null = $da.Fill($ds)
        # Format-Table recorta columnas al ancho del host (120 en pwsh no interactivo) y
        # redondea decimales; se imprime cada conjunto como texto plano, columna a columna.
        foreach ($dt in $ds.Tables) {
            if ($dt.Columns.Count -eq 0) { continue }
            $cols = @($dt.Columns | ForEach-Object { $_.ColumnName })
            Write-Host ""
            Write-Host ("  " + ($cols -join " | "))
            foreach ($row in $dt.Rows) {
                $vals = foreach ($c in $cols) {
                    $v = $row[$c]
                    if ($v -is [System.DBNull]) { "NULL" } else { [string]$v }
                }
                Write-Host ("  " + ($vals -join " | "))
            }
            Write-Host ("  (" + $dt.Rows.Count + " fila(s))")
        }
    }
    Write-Host "Lotes aplicados: $i"
}
finally {
    $conn.Close()
}
