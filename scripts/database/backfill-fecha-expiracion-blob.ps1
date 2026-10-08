<#
.SYNOPSIS
    Rellena Documentos.FechaExpiracionBlob en las filas historicas sin fecha. AB#100881.

.DESCRIPTION
    Desde que la retencion de blobs (AB#98692) esta desplegada, PersistirActivity fija
    FechaExpiracionBlob = FechaProceso + BlobRetention:DefaultDays (2 dias) salvo politica
    por tipologia. Los documentos anteriores al despliegue (y los migrados desde DEV el
    2026-08-04) quedaron con NULL y el cron de limpieza nunca los tocara.

    Este script sustituye a scripts/migrations/backfill-documentos-fecha-expiracion-blob-2-dias.sql
    para PRO: aquel hace un unico UPDATE en una transaccion sobre toda la tabla, que en la BD
    S0 bloquea Documentos y hace crecer el log. Aqui se va por lotes con marca de agua sobre
    Id (leccion de AB#100165): cada lote avanza siempre y solo escribe donde falta la fecha.

    Idempotente y reanudable: solo toca filas con blob informado y FechaExpiracionBlob nulo.
    Se puede cortar y volver a lanzar. El borrado real lo hace BlobCleanupTimerTrigger la
    noche siguiente, a BlobRetention:BatchSize blobs por ciclo.

.PARAMETER Server
    FQDN del servidor SQL. Por defecto el de DEV.

.PARAMETER Database
    Base de datos. Por defecto DocumentIA.

.PARAMETER RetentionDays
    Dias de retencion a aplicar sobre COALESCE(FechaProceso, FechaCreacion). Por defecto 2.

.PARAMETER HastaFechaUtc
    Si se indica, solo se rellenan documentos con FechaCreacion anterior a esta fecha (UTC).
    Sirve para no tocar documentos posteriores al despliegue de la retencion cuya tipologia
    tenga politica -1 (sin expiracion), que llevan NULL de forma legitima.

.PARAMETER BatchSize
    Filas por lote. Por defecto 2000.

.PARAMETER MaxBatches
    Numero maximo de lotes por ejecucion (0 = sin limite). Permite ir por tandas.

.PARAMETER CountOnly
    Solo cuenta los candidatos y termina. No escribe nada.

.EXAMPLE
    ./backfill-fecha-expiracion-blob.ps1 -CountOnly
    ./backfill-fecha-expiracion-blob.ps1 -Server srbsqlprodocai.database.windows.net -HastaFechaUtc 2026-08-14 -CountOnly
    ./backfill-fecha-expiracion-blob.ps1 -Server srbsqlprodocai.database.windows.net -HastaFechaUtc 2026-08-14 -BatchSize 2000
#>
[CmdletBinding()]
param(
    [string]$Server   = "srbsqldevdocai.database.windows.net",
    [string]$Database = "DocumentIA",
    [int]$RetentionDays = 2,
    [datetime]$HastaFechaUtc,
    [int]$BatchSize   = 2000,
    [int]$MaxBatches  = 0,
    [switch]$CountOnly
)

$ErrorActionPreference = "Stop"

if ($RetentionDays -le 0) { throw "RetentionDays debe ser mayor que 0." }

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

if (-not (Invoke-Scalar "SELECT COL_LENGTH('dbo.Documentos','FechaExpiracionBlob');")) {
    throw "La columna FechaExpiracionBlob no existe. Aplica antes la migracion de retencion de blobs (AB#98692)."
}

$filtroFecha = ""
if ($PSBoundParameters.ContainsKey('HastaFechaUtc')) {
    # La fecha se interpreta tal cual, como UTC: sin esto, "2026-08-14" se tomaria como hora local
    # y se convertiria restando el huso horario.
    $fechaSql = [DateTime]::SpecifyKind($HastaFechaUtc, [DateTimeKind]::Utc).ToString("yyyy-MM-ddTHH:mm:ss")
    $filtroFecha = "AND FechaCreacion < '$fechaSql'"
    Write-Host "Solo documentos con FechaCreacion anterior a $fechaSql UTC"
} else {
    Write-Warning "Sin -HastaFechaUtc: se rellenaran tambien documentos posteriores al despliegue cuya tipologia tenga retencion -1."
}

$filtroCandidato = "ISNULL(RutaBlobStorage, '') <> '' AND FechaExpiracionBlob IS NULL $filtroFecha"

$candidatos = [int](Invoke-Scalar "SELECT COUNT(*) FROM dbo.Documentos WHERE $filtroCandidato;")
$minId = Invoke-Scalar "SELECT ISNULL(MIN(Id), 0) FROM dbo.Documentos WHERE $filtroCandidato;"
$maxId = Invoke-Scalar "SELECT ISNULL(MAX(Id), 0) FROM dbo.Documentos WHERE $filtroCandidato;"
Write-Host "Candidatos: $candidatos documentos (Id entre $minId y $maxId)"

if ($CountOnly) {
    Write-Host "Modo CountOnly: no se escribe nada."
    $conn.Close()
    return
}

if ($candidatos -eq 0) {
    Write-Host "Nada que rellenar."
    $conn.Close()
    return
}

# El recorrido avanza por marca de agua sobre Id, no por 'FechaExpiracionBlob IS NULL' a secas:
# con la marca de agua cada lote avanza siempre, el UPDATE toca pocas filas y no mantiene
# bloqueos largos sobre Documentos.
$lote = 0
$desde = [int]$minId - 1
$totalEscritas = 0
while ($desde -lt $maxId) {
    if ($MaxBatches -gt 0 -and $lote -ge $MaxBatches) {
        Write-Host "Limite de lotes alcanzado ($MaxBatches). Vuelve a ejecutar para continuar."
        break
    }

    $hasta = $desde + $BatchSize

    $escritas = Invoke-Scalar @"
UPDATE dbo.Documentos
SET FechaExpiracionBlob = DATEADD(DAY, $RetentionDays, COALESCE(FechaProceso, FechaCreacion, SYSUTCDATETIME())),
    FechaActualizacion = SYSUTCDATETIME()
WHERE Id > $desde AND Id <= $hasta
  AND $filtroCandidato;
SELECT @@ROWCOUNT;
"@

    $lote++
    $desde = $hasta
    $totalEscritas += [int]$escritas
    Write-Host ("Lote {0} (Id <= {1}): {2} filas rellenadas (acumulado {3})" -f $lote, $hasta, $escritas, $totalEscritas)
    Start-Sleep -Milliseconds 200
}

$restantes = Invoke-Scalar "SELECT COUNT(*) FROM dbo.Documentos WHERE $filtroCandidato;"
Write-Host "Candidatos restantes sin fecha: $restantes"

if ($desde -ge $maxId) {
    Write-Host "Backfill completado: recorrido todo el rango."
} else {
    Write-Host "Backfill parcial: reanudar en la proxima ejecucion (vuelve a calcular el rango por si mismo)."
}

$conn.Close()
