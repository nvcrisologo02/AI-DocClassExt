#Requires -Version 7.0
<#
.SYNOPSIS
    Rellena ClaveIntencion y ClaveIntencionJson en el historico de DocumentoEjecuciones. AB#100930.

.DESCRIPTION
    La migracion ClaveIntencionDedup deja las filas antiguas sin clave. La actividad de dedup las
    deriva al vuelo la primera vez que un documento se repite, pero ese camino carga y parsea los
    contratos del documento en la ruta de la peticion. Este script lo hace por adelantado, por
    lotes de Id, con la MISMA forma canonica y el MISMO hash que ClaveIntencionBuilder (Core):
        v1|<modo>|<nivel>|<universo>|<expectedType>|<modelo>   -> SHA-256 -> 32 hex minusculas
    Componentes leidos del contrato (System.Text.Json, PascalCase):
        modo        = solo-clasificacion si $.DetalleEjecucion.ClassificationOnly, si no completo
        nivel       = $.DetalleEjecucion.NivelClasificacion en mayusculas, o TDN1_TDN2 si vacio
        universo    = $.DetalleEjecucion.Clasificacion.RestriccionTipologias.Codigos en mayusculas,
                      sin duplicados, orden ordinal, unidos por coma; "*" sin restriccion
        expectedType= $.Identificacion.Tipologia en mayusculas solo si el modelo es expectedtype-input
        modelo      = $.DetalleEjecucion.Clasificacion.Modelo en minusculas (expectedtype-input con ExpectedType)
    Se saltan (y quedan a NULL, sin marcar): reutilizaciones (ReutilizadaPorDuplicado = 1, su
    peticion no se conoce), filas sin contrato, filas con Resultado.Estado = DUPLICADO (contrato
    vacio) y filas cuyo $.DetalleEjecucion no es un objeto JSON (una clave de valores por defecto
    no es una intencion; se derivan al vuelo, acotadas por MaxDerivacionAlVuelo).

    -SelfTest comprueba los vectores compartidos con ClaveIntencionBuilderTests y termina. Si falla,
    NO ejecutar el backfill: las claves escritas no coincidirian nunca con las vivas.

    Idempotente y reanudable: solo toca filas con ClaveIntencion NULL; -DesdeId reanuda.

.EXAMPLE
    ./backfill-clave-intencion.ps1 -SelfTest
    ./backfill-clave-intencion.ps1 -WhatIf
    ./backfill-clave-intencion.ps1 -Server srbsqlprodocai.database.windows.net -BatchSize 1000
#>
[CmdletBinding()]
param(
    [string]$Server   = "srbsqldevdocai.database.windows.net",
    [string]$Database = "DocumentIA",
    [int]$BatchSize   = 2000,
    [int]$MaxBatches  = 0,
    [int]$DesdeId     = 0,
    [switch]$WhatIf,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"
$NivelDefault = "TDN1_TDN2"
$ModeloExpectedType = "expectedtype-input"

function Get-FormaCanonica([string]$modo, [string]$nivel, [string]$universo, [string]$expectedType, [string]$modelo) {
    return "v1|$modo|$nivel|$universo|$expectedType|$modelo"
}

function Get-ClaveIntencion([string]$canonica) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($canonica)) }
    finally { $sha.Dispose() }
    return (($bytes | ForEach-Object { $_.ToString("x2") }) -join "").Substring(0, 32)
}

function Get-Universo($codigos) {
    $lista = @($codigos | ForEach-Object { "$_".Trim().ToUpperInvariant() } | Where-Object { $_.Length -gt 0 } | Sort-Object -Unique)
    # Sort-Object -Unique ordena por cultura; se reordena ordinal para coincidir con StringComparer.Ordinal.
    $lista = @([System.Linq.Enumerable]::OrderBy([string[]]$lista, [Func[string, string]]{ param($s) $s }, [System.StringComparer]::Ordinal))
    if ($lista.Count -eq 0) { return "*" }
    return ($lista -join ",")
}

function Get-Componentes([bool]$classificationOnly, [string]$nivel, $codigos, [string]$tipologia, [string]$modelo) {
    $modeloNorm = "$modelo".Trim().ToLowerInvariant()
    $expected = ""
    if ($modeloNorm -eq $ModeloExpectedType) {
        $expected = "$tipologia".Trim().ToUpperInvariant()
        if ($expected -eq "DESCONOCIDO") { $expected = "" }
    }
    $nivelNorm = if ([string]::IsNullOrWhiteSpace($nivel)) { $NivelDefault } else { $nivel.Trim().ToUpperInvariant() }
    return [pscustomobject]@{
        modo = $(if ($classificationOnly) { "solo-clasificacion" } else { "completo" })
        nivel = $nivelNorm
        universo = Get-Universo $codigos
        expectedType = $expected
        modeloClasificacion = $(if ($expected.Length -gt 0) { $ModeloExpectedType } else { $modeloNorm })
    }
}

function Test-Vectores {
    # Mismos vectores que ClaveIntencionBuilderTests.FormaCanonica_EsLaQueReplicaElBackfill y
    # ClaveIntencionBuilderTests.VectorReferencia.
    $VECTOR_REFERENCIA = "ae16c9e79cf0dac934cb460c80397c65"
    $c1 = Get-Componentes $false "TDN1_TDN2" @() "" "gpt-4o-mini"
    $c2 = Get-Componentes $true "TDN1" @("tasacion", "Nota.Simple", "tasacion") "" "GPT-4O-MINI"
    $c3 = Get-Componentes $false "" @() "nota.simple" "expectedtype-input"
    $fallos = @()
    if ((Get-FormaCanonica $c1.modo $c1.nivel $c1.universo $c1.expectedType $c1.modeloClasificacion) -ne "v1|completo|TDN1_TDN2|*||gpt-4o-mini") { $fallos += "canonica 1" }
    if ((Get-FormaCanonica $c2.modo $c2.nivel $c2.universo $c2.expectedType $c2.modeloClasificacion) -ne "v1|solo-clasificacion|TDN1|NOTA.SIMPLE,TASACION||gpt-4o-mini") { $fallos += "canonica 2" }
    if ((Get-FormaCanonica $c3.modo $c3.nivel $c3.universo $c3.expectedType $c3.modeloClasificacion) -ne "v1|completo|TDN1_TDN2|*|NOTA.SIMPLE|expectedtype-input") { $fallos += "canonica 3" }
    if ((Get-ClaveIntencion "v1|completo|TDN1_TDN2|*||gpt-4o-mini") -ne $VECTOR_REFERENCIA) { $fallos += "hash de referencia" }
    if ((Get-ClaveIntencion "v1|solo-clasificacion|TDN1|NOTA.SIMPLE,TASACION||gpt-4o-mini") -ne "4fc9510621a5ba0162368b8f72f869cc") { $fallos += "hash 2" }
    if ((Get-ClaveIntencion "v1|completo|TDN1_TDN2|*|NOTA.SIMPLE|expectedtype-input") -ne "ee58c9392080836d32c11865bd8b8f53") { $fallos += "hash 3" }
    if (($c1 | ConvertTo-Json -Compress) -ne '{"modo":"completo","nivel":"TDN1_TDN2","universo":"*","expectedType":"","modeloClasificacion":"gpt-4o-mini"}') { $fallos += "json de componentes" }
    if ($fallos.Count -gt 0) { throw "SelfTest fallido: $($fallos -join ', '). No ejecutar el backfill." }
    Write-Host "SelfTest OK: forma canonica, hash y json de componentes coinciden con ClaveIntencionBuilder."
}

if ($SelfTest) { Test-Vectores; return }
Test-Vectores

$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if (-not $token) { throw "No se pudo obtener token de Entra. Ejecuta 'az login' primero." }

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = "Server=tcp:$Server,1433;Database=$Database;Encrypt=True;"
$conn.AccessToken = $token
$conn.Open()

function Invoke-Scalar([string]$sql) {
    $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 600
    return $cmd.ExecuteScalar()
}

if (-not (Invoke-Scalar "SELECT COL_LENGTH('dbo.DocumentoEjecuciones','ClaveIntencion');")) {
    throw "La columna ClaveIntencion no existe. Aplica antes la migracion ClaveIntencionDedup (o el script indice-documento-clave-pro.sql)."
}

# Las filas candidatas se filtran por columnas escalares (Id, ClaveIntencion, ReutilizadaPorDuplicado);
# el JSON solo se abre dentro del lote. Nunca JSON_VALUE sobre la tabla entera.
$whereCandidatas = "e.ClaveIntencion IS NULL AND e.ReutilizadaPorDuplicado = 0 AND e.ContratoSalidaCompletoJson IS NOT NULL"
$maxId = [int](Invoke-Scalar "SELECT ISNULL(MAX(Id), 0) FROM dbo.DocumentoEjecuciones;")
Write-Host "Ejecuciones a recorrer: desde Id $DesdeId hasta Id $maxId (lotes de $BatchSize)"

if ($WhatIf) {
    $pendientes = [int](Invoke-Scalar "SELECT COUNT(*) FROM dbo.DocumentoEjecuciones e WHERE $whereCandidatas AND e.Id > $DesdeId;")
    Write-Host "Filas sin clave que se rellenarian: $pendientes"
    Write-Host "Es un tope: incluye filas que luego se saltan (DUPLICADO o sin DetalleEjecucion) y quedan a NULL."
    Write-Host "WhatIf: no se escribe nada."
    $conn.Close(); return
}

$lote = 0; $desde = $DesdeId; $totalEscritas = 0; $totalSaltadas = 0; $totalSinDetalle = 0
while ($desde -lt $maxId) {
    if ($MaxBatches -gt 0 -and $lote -ge $MaxBatches) {
        Write-Host "Limite de lotes alcanzado ($MaxBatches). Reanudar con -DesdeId $desde"
        break
    }
    $hasta = $desde + $BatchSize

    # Dos rutas por campo (PascalCase y camelCase completo): el historico mezcla ambos casings
    # (ver ClaveIntencionDerivador). Casing mixto dentro de un mismo objeto NO se cubre.
    $lectura = $conn.CreateCommand()
    $lectura.CommandTimeout = 600
    $lectura.CommandText = @"
SELECT e.Id,
       CASE WHEN COALESCE(JSON_QUERY(x.c, '`$.DetalleEjecucion'), JSON_QUERY(x.c, '`$.detalleEjecucion')) IS NULL THEN 0 ELSE 1 END AS TieneDetalle,
       COALESCE(JSON_VALUE(x.c, '`$.DetalleEjecucion.ClassificationOnly'), JSON_VALUE(x.c, '`$.detalleEjecucion.classificationOnly')) AS ClassificationOnly,
       COALESCE(JSON_VALUE(x.c, '`$.DetalleEjecucion.NivelClasificacion'), JSON_VALUE(x.c, '`$.detalleEjecucion.nivelClasificacion')) AS Nivel,
       COALESCE(JSON_QUERY(x.c, '`$.DetalleEjecucion.Clasificacion.RestriccionTipologias.Codigos'), JSON_QUERY(x.c, '`$.detalleEjecucion.clasificacion.restriccionTipologias.codigos')) AS Codigos,
       COALESCE(JSON_VALUE(x.c, '`$.DetalleEjecucion.Clasificacion.Modelo'), JSON_VALUE(x.c, '`$.detalleEjecucion.clasificacion.modelo')) AS Modelo,
       COALESCE(JSON_VALUE(x.c, '`$.Identificacion.Tipologia'), JSON_VALUE(x.c, '`$.identificacion.tipologia')) AS Tipologia,
       COALESCE(JSON_VALUE(x.c, '`$.Resultado.Estado'), JSON_VALUE(x.c, '`$.resultado.estado')) AS Estado
FROM dbo.DocumentoEjecuciones e
CROSS APPLY (SELECT e.ContratoSalidaCompletoJson AS c) AS x
WHERE e.Id > $desde AND e.Id <= $hasta AND $whereCandidatas;
"@
    $filas = @()
    $lector = $lectura.ExecuteReader()
    try {
        while ($lector.Read()) {
            $filas += [pscustomobject]@{
                Id = [int]$lector["Id"]
                TieneDetalle = ([int]$lector["TieneDetalle"] -eq 1)
                ClassificationOnly = ("$($lector["ClassificationOnly"])".ToLowerInvariant() -eq "true")
                Nivel = "$($lector["Nivel"])"
                Codigos = $(if ($lector["Codigos"] -is [System.DBNull]) { @() } else { @("$($lector["Codigos"])" | ConvertFrom-Json) })
                Modelo = "$($lector["Modelo"])"
                Tipologia = "$($lector["Tipologia"])"
                Estado = "$($lector["Estado"])"
            }
        }
    }
    finally { $lector.Close() }

    $tx = $conn.BeginTransaction()
    try {
        foreach ($f in $filas) {
            if ($f.Estado -eq "DUPLICADO") { $totalSaltadas++; continue }
            if (-not $f.TieneDetalle) { $totalSinDetalle++; continue }
            $c = Get-Componentes $f.ClassificationOnly $f.Nivel $f.Codigos $f.Tipologia $f.Modelo
            $clave = Get-ClaveIntencion (Get-FormaCanonica $c.modo $c.nivel $c.universo $c.expectedType $c.modeloClasificacion)
            $json = ($c | ConvertTo-Json -Compress)
            $upd = $conn.CreateCommand(); $upd.Transaction = $tx
            $upd.CommandText = "UPDATE dbo.DocumentoEjecuciones SET ClaveIntencion = @c, ClaveIntencionJson = @j WHERE Id = @id AND ClaveIntencion IS NULL;"
            [void]$upd.Parameters.AddWithValue("@c", $clave)
            [void]$upd.Parameters.AddWithValue("@j", $json)
            [void]$upd.Parameters.AddWithValue("@id", $f.Id)
            $totalEscritas += $upd.ExecuteNonQuery()
        }
        $tx.Commit()
    }
    catch { $tx.Rollback(); throw }

    $lote++
    Write-Host ("Lote {0}: Id ({1}, {2}] -> {3} candidatas, {4} escritas acumuladas, {5} saltadas (DUPLICADO vacio), {6} saltadas (sin DetalleEjecucion)" -f $lote, $desde, $hasta, $filas.Count, $totalEscritas, $totalSaltadas, $totalSinDetalle)
    $desde = $hasta
}

Write-Host "Total escritas: $totalEscritas. Saltadas por DUPLICADO vacio: $totalSaltadas. Saltadas sin DetalleEjecucion: $totalSinDetalle. Ultimo Id procesado: $desde"
$conn.Close()
