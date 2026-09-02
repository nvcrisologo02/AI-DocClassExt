<#
.SYNOPSIS
    Rellena DocumentoEjecuciones.IdActivo en las filas historicas. AB#100168.

.DESCRIPTION
    La columna escalar IdActivo sustituye a la columna calculada IdActivoNormalizado, que se
    derivaba de DatosFinalesJson (columna que dejo de grabarse en AB#100166). Las filas nuevas
    la rellena PersistirActivity; las historicas se rellenan con este script.

    Va por lotes a proposito y NO forma parte de la migracion EF: recorrer JSON_VALUE sobre el
    contrato (LOB de ~14 KB de media) en toda la tabla agota el timeout ya con 8.000 filas, y
    dentro de la transaccion de una migracion mantendria bloqueos sobre una tabla de 1,7 GB
    en PRO.

    Idempotente y reanudable: solo toca filas con IdActivo aun nulo. Se puede cortar y volver
    a lanzar sin duplicar trabajo. Mientras no termine, el SP de consulta por activo no
    encuentra las filas historicas todavia sin rellenar (no tiene consumidores activos:
    cero ejecuciones en Query Store en PRO).

.PARAMETER Server
    FQDN del servidor SQL. Por defecto el de DEV.

.PARAMETER Database
    Base de datos. Por defecto DocumentIA.

.PARAMETER BatchSize
    Filas por lote. Por defecto 2000.

.PARAMETER MaxBatches
    Numero maximo de lotes por ejecucion (0 = sin limite). Permite ir por tandas.

.EXAMPLE
    ./backfill-idactivo.ps1
    ./backfill-idactivo.ps1 -Server srbsqlprodocai.database.windows.net -BatchSize 1000
#>
[CmdletBinding()]
param(
    [string]$Server   = "srbsqldevdocai.database.windows.net",
    [string]$Database = "DocumentIA",
    [int]$BatchSize   = 2000,
    [int]$MaxBatches  = 0
)

$ErrorActionPreference = "Stop"

$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if (-not $token) { throw "No se pudo obtener token de Entra. Ejecuta 'az login' primero." }

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = "Server=tcp:$Server,1433;Database=$Database;Encrypt=True;"
$conn.AccessToken = $token
$conn.Open()

function Invoke-Scalar([string]$sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 600
    return $cmd.ExecuteScalar()
}

if (-not (Invoke-Scalar "SELECT COL_LENGTH('dbo.DocumentoEjecuciones','IdActivo');")) {
    throw "La columna IdActivo no existe. Aplica antes la migracion IdActivoEscalarYSpPorIdActivo."
}

$maxId = [int](Invoke-Scalar "SELECT ISNULL(MAX(Id), 0) FROM dbo.DocumentoEjecuciones;")
Write-Host "Ejecuciones a recorrer: hasta Id $maxId"

# El recorrido avanza por marca de agua sobre Id, no por 'IdActivo IS NULL'. Las filas sin
# activo se quedan en NULL de forma legitima, asi que un filtro por nulos nunca convergeria:
# cada pasada volveria a mirarlas y en PRO reescribiria decenas de miles de filas por nada.
# Con la marca de agua cada lote avanza siempre y solo se escribe donde hay activo real.
$lote = 0
$desde = 0
$totalEscritas = 0
while ($desde -lt $maxId) {
    if ($MaxBatches -gt 0 -and $lote -ge $MaxBatches) {
        Write-Host "Limite de lotes alcanzado ($MaxBatches). Vuelve a ejecutar para continuar."
        break
    }

    $hasta = $desde + $BatchSize

    $escritas = Invoke-Scalar @"
UPDATE dbo.DocumentoEjecuciones
SET IdActivo = UPPER(LTRIM(RTRIM(COALESCE(
        JSON_VALUE(ContratoSalidaCompletoJson, '`$.Integridad.IdActivo'),
        JSON_VALUE(DatosFinalesJson, '`$.IdActivo'),
        JSON_VALUE(DatosFinalesJson, '`$.idActivo'),
        JSON_VALUE(DatosFinalesJson, '`$.id_activo'),
        JSON_VALUE(DatosFinalesJson, '`$.id_activo_sareb')
    ))))
WHERE Id > $desde AND Id <= $hasta
  AND IdActivo IS NULL
  AND NULLIF(LTRIM(RTRIM(COALESCE(
        JSON_VALUE(ContratoSalidaCompletoJson, '`$.Integridad.IdActivo'),
        JSON_VALUE(DatosFinalesJson, '`$.IdActivo'),
        JSON_VALUE(DatosFinalesJson, '`$.idActivo'),
        JSON_VALUE(DatosFinalesJson, '`$.id_activo'),
        JSON_VALUE(DatosFinalesJson, '`$.id_activo_sareb'),
        ''
    ))), '') IS NOT NULL;
SELECT @@ROWCOUNT;
"@

    $lote++
    $desde = $hasta
    $totalEscritas += [int]$escritas
    Write-Host ("Lote {0} (Id <= {1}): {2} filas con activo rellenadas (acumulado {3})" -f $lote, $hasta, $escritas, $totalEscritas)
    Start-Sleep -Milliseconds 200
}

$conActivo = Invoke-Scalar "SELECT COUNT(*) FROM dbo.DocumentoEjecuciones WHERE IdActivo IS NOT NULL;"
Write-Host "Ejecuciones con IdActivo informado: $conActivo"

if ($desde -ge $maxId) {
    Write-Host "Backfill completado: recorrida toda la tabla."
} else {
    Write-Host "Backfill parcial: reanudar desde Id $desde en la proxima ejecucion."
}

$conn.Close()
