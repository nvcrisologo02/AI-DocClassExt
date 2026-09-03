<#
.SYNOPSIS
    Copia Documentos.NormalizacionMarkdownCompressed (Base64 de GZip) a
    Documentos.NormalizacionMarkdownGzip (GZip binario). AB#100169.

.DESCRIPTION
    Es una copia ADITIVA: no borra ni modifica la columna Base64. Por eso es segura y
    reversible - si hay que volver atras, basta con borrar la columna binaria (el Down de
    la migracion) y no se pierde nada, porque el codigo sigue escribiendo las dos columnas.

    La conversion es Base64 -> bytes, sin descomprimir ni recomprimir, asi que no puede
    alterar el contenido. El test unitario
    MarkdownCompressionBinarioTests.Compress_ProduceLosMismosBytesQueDecodificarLaVarianteBase64
    fija esa equivalencia.

    Va por lotes con marca de agua sobre Id, no filtrando por 'columna nueva IS NULL': asi
    cada lote avanza siempre y el recorrido converge aunque haya filas que no se puedan
    convertir. Idempotente: solo escribe donde la columna binaria esta vacia.

.PARAMETER Server
    FQDN del servidor SQL. Por defecto el de DEV.

.PARAMETER Database
    Base de datos. Por defecto DocumentIA.

.PARAMETER BatchSize
    Filas por lote. Por defecto 500 (el markdown es un LOB: lotes pequenos).

.PARAMETER MaxBatches
    Numero maximo de lotes por ejecucion (0 = sin limite). Permite ir por tandas.

.EXAMPLE
    ./migrar-markdown-a-binario.ps1
    ./migrar-markdown-a-binario.ps1 -Server srbsqlprodocai.database.windows.net -BatchSize 250
#>
[CmdletBinding()]
param(
    [string]$Server   = "srbsqldevdocai.database.windows.net",
    [string]$Database = "DocumentIA",
    [int]$BatchSize   = 500,
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

if (-not (Invoke-Scalar "SELECT COL_LENGTH('dbo.Documentos','NormalizacionMarkdownGzip');")) {
    throw "La columna NormalizacionMarkdownGzip no existe. Aplica antes la migracion MarkdownBinario."
}

$maxId = [int](Invoke-Scalar "SELECT ISNULL(MAX(Id), 0) FROM dbo.Documentos;")
$pendientes = Invoke-Scalar @"
SELECT COUNT(*) FROM dbo.Documentos
WHERE NormalizacionMarkdownGzip IS NULL AND NormalizacionMarkdownCompressed IS NOT NULL;
"@
Write-Host "Documentos a recorrer: hasta Id $maxId"
Write-Host "Filas pendientes de copiar: $pendientes"

$lote = 0
$desde = 0
$totalCopiadas = 0
while ($desde -lt $maxId) {
    if ($MaxBatches -gt 0 -and $lote -ge $MaxBatches) {
        Write-Host "Limite de lotes alcanzado ($MaxBatches). Vuelve a ejecutar para continuar."
        break
    }

    $hasta = $desde + $BatchSize

    $copiadas = Invoke-Scalar @"
UPDATE dbo.Documentos
SET NormalizacionMarkdownGzip =
    CAST(N'' AS XML).value('xs:base64Binary(sql:column("NormalizacionMarkdownCompressed"))', 'varbinary(max)')
WHERE Id > $desde AND Id <= $hasta
  AND NormalizacionMarkdownGzip IS NULL
  AND NormalizacionMarkdownCompressed IS NOT NULL;
SELECT @@ROWCOUNT;
"@

    $lote++
    $desde = $hasta
    $totalCopiadas += [int]$copiadas
    Write-Host ("Lote {0} (Id <= {1}): {2} filas copiadas (acumulado {3})" -f $lote, $hasta, $copiadas, $totalCopiadas)
    Start-Sleep -Milliseconds 300
}

# Comprobacion de integridad: los bytes de la columna nueva deben coincidir con la
# decodificacion Base64 de la antigua en TODAS las filas migradas. Va por tramos:
# en una sola consulta agota el CommandTimeout en un S0 (le paso a PRO el 03/09,
# decodificando 640 MB de Base64 de una vez).
$discrepancias = 0
$desdeCheck = 0
while ($desdeCheck -lt $maxId) {
    $hastaCheck = $desdeCheck + 10000
    $discrepancias += [int](Invoke-Scalar @"
SELECT COUNT(*) FROM dbo.Documentos
WHERE Id > $desdeCheck AND Id <= $hastaCheck
  AND NormalizacionMarkdownGzip IS NOT NULL
  AND NormalizacionMarkdownCompressed IS NOT NULL
  AND NormalizacionMarkdownGzip <>
      CAST(N'' AS XML).value('xs:base64Binary(sql:column("NormalizacionMarkdownCompressed"))', 'varbinary(max)');
"@)
    $desdeCheck = $hastaCheck
}

$restantes = Invoke-Scalar @"
SELECT COUNT(*) FROM dbo.Documentos
WHERE NormalizacionMarkdownGzip IS NULL AND NormalizacionMarkdownCompressed IS NOT NULL;
"@

Write-Host ""
Write-Host "Filas pendientes tras la ejecucion: $restantes"
Write-Host "Discrepancias binario vs Base64: $discrepancias"

if ([int]$discrepancias -gt 0) {
    throw "INTEGRIDAD: $discrepancias filas no coinciden con su origen Base64. Revisar antes de continuar."
}

Write-Host "[OK] Todas las filas migradas coinciden byte a byte con su origen."

if ($desde -ge $maxId) {
    Write-Host "Recorrido completo de la tabla."
} else {
    Write-Host "Recorrido parcial: reanudar desde Id $desde en la proxima ejecucion."
}

$conn.Close()
