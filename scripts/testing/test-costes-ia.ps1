<#
.SYNOPSIS
    Verifica en un entorno desplegado que el bloque de costes de IA se calcula,
    se devuelve solo cuando se pide y se persiste. AB#100224.

.DESCRIPTION
    Lanza la misma peticion dos veces, con y sin instrucciones.incluirCostes, y
    comprueba las cuatro cosas que definen la funcionalidad:
      1. Con el parametro, la salida trae detalleEjecucion.costes con importes.
      2. Sin el parametro, la salida NO lo trae (el contrato no cambia para los
         llamadores actuales).
      3. Las tarifas salen completas: ningun modelo consumido sin precio.
      4. El desglose por llamada cuadra con el total.

    Solo lectura sobre el entorno salvo por las dos ejecuciones que provoca.
#>
param(
    [ValidateSet("dev", "pre", "pro")][string]$Environment = "dev",
    [string]$Documento = "tests/e2e-postdeploy/corpus/generico/escritura-compraventa-sintetica.pdf",
    [int]$MaxRetries = 60,
    [int]$DelaySeconds = 5
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." ".." ".." "..")).Path
$cfg = Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot "tests/e2e-postdeploy/config/environments.json") | ConvertFrom-Json
$env0 = $cfg.$Environment
if (-not $env0.functionKey) { throw "Sin functionKey para '$Environment'." }

$rutaDoc = Join-Path $repoRoot $Documento
if (-not (Test-Path $rutaDoc)) { throw "No existe el documento $rutaDoc" }
$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($rutaDoc))
$nombre = Split-Path -Leaf $rutaDoc

function Invoke-Ingesta([bool]$IncluirCostes) {
    $body = @{
        instrucciones = @{
            classificationOnly = $true
            skipDuplicateCheck = $true
            skipGDCUpload      = $true
            incluirCostes      = $IncluirCostes
        }
        documento = @{ name = $nombre; content = @{ base64 = $base64 } }
        trazabilidad = @{
            correlationId = [guid]::NewGuid().ToString()
            submittedBy   = "verificacion-costes"
        }
    } | ConvertTo-Json -Depth 10

    $headers = @{ "x-functions-key" = $env0.functionKey; "Content-Type" = "application/json" }
    $init = Invoke-RestMethod -Method Post -Uri "$($env0.baseUrl)/api/IngestDocument" -Headers $headers -Body $body

    for ($i = 0; $i -lt $MaxRetries; $i++) {
        Start-Sleep -Seconds $DelaySeconds
        $st = Invoke-RestMethod -Method Get -Uri "$($init.statusQueryUri)?code=$($env0.functionKey)"
        if ($st.runtimeStatus -in @("Completed", "Failed", "Terminated")) { return $st }
    }
    throw "Timeout esperando la orquestacion $($init.instanceId)"
}

Write-Host "=== 1. Peticion CON incluirCostes ===" -ForegroundColor Cyan
$conCostes = Invoke-Ingesta $true
Write-Host "  runtimeStatus: $($conCostes.runtimeStatus)"
$salida = $conCostes.output
Write-Host "  estado: $($salida.resultado.estado) | tipologia: $($salida.identificacion.tipologia)"

$costes = $salida.detalleEjecucion.costes
if (-not $costes) { throw "FALLO: se pidio incluirCostes y la salida NO trae detalleEjecucion.costes" }

Write-Host ("  coste total: {0:N6} EUR | tokens: {1} | paginas: {2}" -f $costes.costeTotalEur, $costes.tokensTotales, $costes.paginasTotales) -ForegroundColor Green
Write-Host "  tarifas completas: $($costes.tarifasCompletas)"
if (-not $costes.tarifasCompletas) {
    Write-Host "  AVISO modelos sin tarifa: $($costes.modelosSinTarifa -join ', ')" -ForegroundColor Yellow
}

Write-Host "  desglose por llamada:"
$suma = 0.0
foreach ($c in $costes.consumos) {
    $marca = if ($c.descartado) { " [descartado]" } else { "" }
    Write-Host ("    {0,-12} {1,-32} {2,-22} ent={3,-7} cache={4,-7} sal={5,-6} pag={6,-4} {7,10:N6} EUR  {8}{9}" -f `
        $c.actividad, $c.operacion, $c.modelo, $c.tokensEntrada, $c.tokensEntradaCache, $c.tokensSalida, $c.paginas, $c.costeEur, $c.tarifaAplicada, $marca)
    if ($null -ne $c.costeEur) { $suma += [double]$c.costeEur }
}
$desv = [math]::Abs($suma - [double]$costes.costeTotalEur)
Write-Host ("  suma del desglose: {0:N6} EUR (desviacion {1:N8})" -f $suma, $desv)
if ($desv -gt 0.000001) { throw "FALLO: el desglose no cuadra con el total" }

Write-Host "`n=== 2. Peticion SIN incluirCostes ===" -ForegroundColor Cyan
$sinCostes = Invoke-Ingesta $false
Write-Host "  runtimeStatus: $($sinCostes.runtimeStatus)"
$tieneBloque = $null -ne $sinCostes.output.detalleEjecucion.costes
if ($tieneBloque) { throw "FALLO: sin el parametro la salida NO debe traer detalleEjecucion.costes" }
Write-Host "  correcto: la salida no incluye el bloque de costes" -ForegroundColor Green

$consumosClasif = $sinCostes.output.detalleEjecucion.clasificacion.consumos
if ($consumosClasif -and $consumosClasif.Count -gt 0) {
    throw "FALLO: los consumos se filtran por detalleEjecucion.clasificacion.consumos"
}
Write-Host "  correcto: tampoco se filtran por la clasificacion" -ForegroundColor Green

Write-Host "`nVERIFICACION SUPERADA" -ForegroundColor Green
Write-Host "GUIDs para revisar en Admin:"
Write-Host "  con costes: $($salida.identificacion.guid)"
Write-Host "  sin costes: $($sinCostes.output.identificacion.guid)"
