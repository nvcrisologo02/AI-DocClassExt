<#
.SYNOPSIS
    Estima el coste de IA de las ejecuciones anteriores a la funcionalidad. AB#100240.

.DESCRIPTION
    Las ejecuciones nuevas registran su consumo real; las historicas no guardaron tokens
    ni paginas de extraccion, asi que lo que se puede hacer es ESTIMAR y dejarlo marcado.
    Cada fila que rellena este script queda con CosteEstimado = 1 y la seccion de costes
    de Admin la presenta aparte y la excluye de los importes salvo que se pida.

    Que se estima y como:
      - Layout (DI prebuilt-layout, el 94 % del gasto real): paginas incluidas del contrato
        por la tarifa por pagina, si el contrato indica que se genero markdown. Si el
        recorte no informo paginas incluidas, se usan las paginas totales.
      - Clasificacion generativa: no hay tokens, asi que se aplica un coste medio por
        documento (parametro). El valor por defecto sale del lote controlado de julio de
        2026: 0,0065 EUR por documento, tanto para gpt-4o-mini como para gpt-5-mini.
        Solo si el modelo de clasificacion es generativo; las forzadas por tipo esperado
        y las del clasificador DI no consumieron tokens.
      - Extraccion: no se puede estimar, no hay paginas ni tokens. Queda a nulo.

    Escribe SOLO columnas escalares, nunca el contrato JSON: reescribir 72.000 LOB es caro
    y arriesgado, y el contrato debe seguir siendo lo que se devolvio al llamador.
    TokensIA queda a nulo: no se conoce.

    Idempotente y reanudable: solo toca filas con CosteIAEur nulo, asi que nunca pisa un
    coste medido ni una estimacion previa. Va por marca de agua sobre Id, igual que
    backfill-idactivo.ps1 y por los mismos motivos.

    IMPORTANTE: la factura del grupo de recursos mezcla desarrollo y preproduccion, que
    consumen la misma cuenta de IA. La suma de estas estimaciones NO es comparable con
    la factura.

.PARAMETER Server
    FQDN del servidor SQL. Por defecto el de DEV.

.PARAMETER Database
    Base de datos. Por defecto DocumentIA.

.PARAMETER TarifaLayoutEurPorPagina
    Euros por pagina de DI prebuilt-layout. Por defecto el precio efectivo facturado en
    westeurope el 2026-09-07 (S0 Pre-built Pages).

.PARAMETER CosteMedioClasificacionGptEur
    Euros por documento para la clasificacion generativa. Por defecto 0,0065 (lote
    controlado de julio de 2026).

.PARAMETER BatchSize
    Filas por lote. Por defecto 2000.

.PARAMETER MaxBatches
    Numero maximo de lotes por ejecucion (0 = sin limite). Permite ir por tandas.

.PARAMETER WhatIf
    Solo cuenta cuantas filas se rellenarian, sin escribir.

.EXAMPLE
    ./backfill-costes-estimados.ps1 -WhatIf
    ./backfill-costes-estimados.ps1 -Server srbsqlprodocai.database.windows.net -BatchSize 1000
#>
[CmdletBinding()]
param(
    [string]$Server   = "srbsqldevdocai.database.windows.net",
    [string]$Database = "DocumentIA",
    [decimal]$TarifaLayoutEurPorPagina = 0.008586639,
    [decimal]$CosteMedioClasificacionGptEur = 0.0065,
    [int]$BatchSize   = 2000,
    [int]$MaxBatches  = 0,
    [switch]$WhatIf
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

foreach ($col in @('CosteIAEur','CosteLayoutEur','CosteClasificacionEur','CosteEstimado')) {
    if (-not (Invoke-Scalar "SELECT COL_LENGTH('dbo.DocumentoEjecuciones','$col');")) {
        throw "La columna $col no existe. Aplica antes las migraciones AddCostesIAToEjecuciones y AddCostesPorActividadYEstimado."
    }
}

# Los decimales viajan con punto: la cultura del proceso puede ser la espanola.
$inv = [System.Globalization.CultureInfo]::InvariantCulture
$tarifaLayout = $TarifaLayoutEurPorPagina.ToString($inv)
$costeClasif  = $CosteMedioClasificacionGptEur.ToString($inv)

$pendientes = [int](Invoke-Scalar "SELECT COUNT(*) FROM dbo.DocumentoEjecuciones WHERE CosteIAEur IS NULL;")
$maxId = [int](Invoke-Scalar "SELECT ISNULL(MAX(Id), 0) FROM dbo.DocumentoEjecuciones;")
Write-Host "Ejecuciones sin coste: $pendientes (hasta Id $maxId). Tarifa layout $tarifaLayout EUR/pag, clasificacion GPT $costeClasif EUR/doc."

if ($WhatIf) {
    Write-Host "WhatIf: no se escribe nada."
    $conn.Close()
    return
}

# Expresiones de estimacion. Las paginas de layout solo cuentan si el contrato dice que
# se genero markdown; si el recorte no informo paginas incluidas se usan las totales.
$layoutExpr = @"
CASE WHEN JSON_VALUE(ContratoSalidaCompletoJson, '`$.DetalleEjecucion.MarkdownGenerado') = 'true'
     THEN CAST(ISNULL(NULLIF(TRY_CAST(JSON_VALUE(ContratoSalidaCompletoJson, '`$.DetalleEjecucion.PaginasIncluidas') AS int), 0),
                      TRY_CAST(JSON_VALUE(ContratoSalidaCompletoJson, '`$.Identificacion.Paginas') AS int)) AS decimal(18,6))
          * CAST($tarifaLayout AS decimal(18,9))
END
"@
$clasifExpr = @"
CASE WHEN ModeloClasificacion IN ('gpt-4o-mini', 'gpt-5-mini', 'gpt-4.1-mini')
     THEN CAST($costeClasif AS decimal(18,6))
END
"@

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
SET CosteLayoutEur        = ROUND($layoutExpr, 6),
    CosteClasificacionEur = $clasifExpr,
    CosteIAEur            = ROUND(ISNULL($layoutExpr, 0) + ISNULL($clasifExpr, 0), 6),
    CosteEstimado         = 1
WHERE Id > $desde AND Id <= $hasta
  AND CosteIAEur IS NULL
  AND ContratoSalidaCompletoJson IS NOT NULL;
SELECT @@ROWCOUNT;
"@

    $lote++
    $desde = $hasta
    $totalEscritas += [int]$escritas
    Write-Host ("Lote {0} (Id <= {1}): {2} filas estimadas (acumulado {3})" -f $lote, $hasta, $escritas, $totalEscritas)
    Start-Sleep -Milliseconds 200
}

$resumen = $conn.CreateCommand()
$resumen.CommandText = @"
SELECT COUNT(*) AS Estimadas, SUM(CosteIAEur) AS TotalEur, SUM(CosteLayoutEur) AS LayoutEur, SUM(CosteClasificacionEur) AS ClasifEur
FROM dbo.DocumentoEjecuciones WHERE CosteEstimado = 1;
"@
$r = $resumen.ExecuteReader()
if ($r.Read()) {
    Write-Host ("Estimadas en total: {0} filas, {1:N2} EUR (layout {2:N2}, clasificacion {3:N2})" -f $r[0], $r[1], $r[2], $r[3])
}
$r.Close()
$conn.Close()
Write-Host "Terminado."
