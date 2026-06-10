param(
    [string]$ResourceGroup = "",
    [string]$AppInsightsName = "",
    [string]$Subscription = "",
    [string]$OutputDir = "",
    [string]$DatasetCsv = "",
    [string]$MatchingEventName = "AssetResolverMatchEvaluated"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-WarnMsg {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Require-AzCli {
    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) {
        throw "Azure CLI (az) no esta instalado o no esta en PATH."
    }
}

function Invoke-AiQueryJson {
    param(
        [string]$Rg,
        [string]$App,
        [string]$Query,
        [string]$Offset = "7d"
    )

    $args = @(
        "monitor", "app-insights", "query",
        "--resource-group", $Rg,
        "--app", $App,
        "--analytics-query", $Query,
        "--offset", $Offset,
        "--output", "json"
    )

    $raw = & az @args
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo ejecutando query App Insights."
    }

    return ($raw | ConvertFrom-Json)
}

function Save-QueryResult {
    param(
        [psobject]$Result,
        [string]$BasePath
    )

    $jsonPath = "$BasePath.json"
    $csvPath = "$BasePath.csv"

    $Result | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonPath -Encoding utf8

    if ($Result.tables -and $Result.tables.Count -gt 0) {
        $table = $Result.tables[0]
        $columns = @($table.columns.name)
        $rows = @($table.rows)

        $objects = foreach ($row in $rows) {
            $obj = [ordered]@{}
            for ($i = 0; $i -lt $columns.Count; $i++) {
                $obj[$columns[$i]] = $row[$i]
            }
            [pscustomobject]$obj
        }

        if ($objects.Count -gt 0) {
            $objects | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
        }
        else {
            "(sin filas)" | Out-File -FilePath $csvPath -Encoding utf8
        }
    }
    else {
        "(sin tablas)" | Out-File -FilePath $csvPath -Encoding utf8
    }
}

function Get-PrecisionRecall {
    param([string]$CsvPath)

    if ([string]::IsNullOrWhiteSpace($CsvPath)) {
        return $null
    }

    if (-not (Test-Path -LiteralPath $CsvPath)) {
        throw "No existe el dataset CSV: $CsvPath"
    }

    $rows = Import-Csv -LiteralPath $CsvPath
    if ($rows.Count -eq 0) {
        throw "El dataset CSV esta vacio: $CsvPath"
    }

    $required = @("IdActivoEsperado", "IdActivoObtenido")
    foreach ($col in $required) {
        if (-not ($rows[0].PSObject.Properties.Name -contains $col)) {
            throw "El dataset CSV debe incluir columna '$col'."
        }
    }

    $tp = 0
    $fp = 0
    $fn = 0

    foreach ($r in $rows) {
        $expected = [string]$r.IdActivoEsperado
        $obtained = [string]$r.IdActivoObtenido

        $hasExpected = -not [string]::IsNullOrWhiteSpace($expected)
        $hasObtained = -not [string]::IsNullOrWhiteSpace($obtained)

        if ($hasExpected -and $hasObtained -and $expected -eq $obtained) {
            $tp++
            continue
        }

        if ($hasObtained -and (($hasExpected -and $expected -ne $obtained) -or (-not $hasExpected))) {
            $fp++
        }

        if ($hasExpected -and (($hasObtained -and $expected -ne $obtained) -or (-not $hasObtained))) {
            $fn++
        }
    }

    $precision = if (($tp + $fp) -gt 0) { [math]::Round(($tp / ($tp + $fp)), 4) } else { 0.0 }
    $recall = if (($tp + $fn) -gt 0) { [math]::Round(($tp / ($tp + $fn)), 4) } else { 0.0 }

    return [pscustomobject]@{
        TotalRegistros = $rows.Count
        TP = $tp
        FP = $fp
        FN = $fn
        Precision = $precision
        Recall = $recall
    }
}

Require-AzCli

if ([string]::IsNullOrWhiteSpace($ResourceGroup) -or [string]::IsNullOrWhiteSpace($AppInsightsName)) {
    Write-WarnMsg "Debes informar -ResourceGroup y -AppInsightsName."
    Write-Host ""
    Write-Host "Uso recomendado:" -ForegroundColor Cyan
    Write-Host "  powershell -ExecutionPolicy Bypass -File .\\scripts\\collect-tandac-evidence.ps1 -ResourceGroup \"SRBRGDOCSAIPROD\" -AppInsightsName \"srbappiprodocai\" -Subscription \"<subscription-id-o-nombre>\" -DatasetCsv .\\artifacts\\dataset-matching.csv"
    Write-Host ""
    Write-Host "Columnas obligatorias en -DatasetCsv: IdActivoEsperado, IdActivoObtenido"
    return
}

if (-not [string]::IsNullOrWhiteSpace($Subscription)) {
    Write-Info "Seleccionando suscripcion: $Subscription"
    & az account set --subscription $Subscription | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo seleccionar la suscripcion $Subscription"
    }
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path -Path (Get-Location) -ChildPath ("artifacts/tandac-evidence-" + $stamp)
}

New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null
Write-Info "Salida en: $OutputDir"

$q1 = @"
customEvents
| where timestamp > ago(24h)
| where name == "DocumentProcessed"
| extend tipologia   = tostring(customDimensions["Tipologia"])
       , estado      = tostring(customDimensions["EstadoFinal"])
       , duracion_ms = toint(customDimensions["DuracionTotalMs"])
       , fallback    = tobool(customDimensions["UseFallbackLLM"])
| project timestamp, tipologia, estado, duracion_ms, fallback
| order by timestamp desc
| take 100
"@

$q2 = @"
customEvents
| where timestamp > ago(7d)
| where name == "DocumentProcessed"
| extend estado = tostring(customDimensions["EstadoFinal"])
| summarize total      = count()
          , errores    = countif(estado == "Error")
          , tasa_error = round(100.0 * countif(estado == "Error") / count(), 1)
  by bin(timestamp, 1h)
| order by timestamp desc
"@

$q3 = @"
customEvents
| where timestamp > ago(7d)
| where name == "DocumentProcessed"
| extend tipologia   = tostring(customDimensions["Tipologia"])
       , duracion_ms = toint(customDimensions["DuracionTotalMs"])
| summarize p50 = percentile(duracion_ms, 50)
          , p95 = percentile(duracion_ms, 95)
          , p99 = percentile(duracion_ms, 99)
  by tipologia
"@

$q4 = @"
customEvents
| where timestamp > ago(7d)
| where name == "DocumentProcessed"
| extend tipologia   = tostring(customDimensions["Tipologia"])
       , fallback    = tobool(customDimensions["UseFallbackLLM"])
| summarize total        = count()
          , con_fallback = countif(fallback == true)
          , pct_fallback = round(100.0 * countif(fallback == true) / count(), 1)
  by tipologia
"@

$q5 = @"
customEvents
| where timestamp > ago(7d)
| where name contains "AssetResolver"
| summarize eventos = count() by name
| order by eventos desc
"@

$q6 = @"
customEvents
| where timestamp > ago(7d)
| where name == "$MatchingEventName"
| extend criterio = tostring(customDimensions["CriterioAplicado"])
       , score = todouble(customDimensions["Score"])
       , evaluados = toint(customDimensions["CandidatosEvaluados"])
       , descartados = toint(customDimensions["CandidatosDescartados"])
       , razon = tostring(customDimensions["Razon"])
| project timestamp, criterio, score, evaluados, descartados, razon
| order by timestamp desc
| take 500
"@

Write-Info "Ejecutando Q1"
$r1 = Invoke-AiQueryJson -Rg $ResourceGroup -App $AppInsightsName -Query $q1 -Offset "7d"
Save-QueryResult -Result $r1 -BasePath (Join-Path $OutputDir "Q1-EjecucionesRecientes")

Write-Info "Ejecutando Q2"
$r2 = Invoke-AiQueryJson -Rg $ResourceGroup -App $AppInsightsName -Query $q2 -Offset "7d"
Save-QueryResult -Result $r2 -BasePath (Join-Path $OutputDir "Q2-TasaError")

Write-Info "Ejecutando Q3"
$r3 = Invoke-AiQueryJson -Rg $ResourceGroup -App $AppInsightsName -Query $q3 -Offset "7d"
Save-QueryResult -Result $r3 -BasePath (Join-Path $OutputDir "Q3-LatenciaP50P95P99")

Write-Info "Ejecutando Q4"
$r4 = Invoke-AiQueryJson -Rg $ResourceGroup -App $AppInsightsName -Query $q4 -Offset "7d"
Save-QueryResult -Result $r4 -BasePath (Join-Path $OutputDir "Q4-Fallback")

Write-Info "Ejecutando Q5"
$r5 = Invoke-AiQueryJson -Rg $ResourceGroup -App $AppInsightsName -Query $q5 -Offset "7d"
Save-QueryResult -Result $r5 -BasePath (Join-Path $OutputDir "Q5-EventosAssetResolver")

Write-Info "Ejecutando Q6"
$r6 = Invoke-AiQueryJson -Rg $ResourceGroup -App $AppInsightsName -Query $q6 -Offset "7d"
Save-QueryResult -Result $r6 -BasePath (Join-Path $OutputDir "Q6-MatchingDetallado")

$quality = $null
if (-not [string]::IsNullOrWhiteSpace($DatasetCsv)) {
    Write-Info "Calculando precision/recall desde dataset CSV"
    $quality = Get-PrecisionRecall -CsvPath $DatasetCsv
    $quality | ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $OutputDir "Q7-PrecisionRecall.json") -Encoding utf8
    $quality | Export-Csv -Path (Join-Path $OutputDir "Q7-PrecisionRecall.csv") -NoTypeInformation -Encoding UTF8
}
else {
    Write-WarnMsg "No se informo -DatasetCsv. Se omite calculo de precision/recall (Q7)."
}

$summaryPath = Join-Path $OutputDir "Resumen-TandaC.txt"
$lines = @()
$lines += "PAQUETE EVIDENCIA TANDA C"
$lines += "Generado: $(Get-Date -Format s)"
$lines += "ResourceGroup: $ResourceGroup"
$lines += "AppInsights: $AppInsightsName"
$lines += ""
$lines += "Archivos generados:"
$lines += "- Q1-EjecucionesRecientes.(json/csv)"
$lines += "- Q2-TasaError.(json/csv)"
$lines += "- Q3-LatenciaP50P95P99.(json/csv)"
$lines += "- Q4-Fallback.(json/csv)"
$lines += "- Q5-EventosAssetResolver.(json/csv)"
$lines += "- Q6-MatchingDetallado.(json/csv)"
if ($quality -ne $null) {
    $lines += "- Q7-PrecisionRecall.(json/csv)"
    $lines += ""
    $lines += "Matriz calidad:"
    $lines += "TP=$($quality.TP), FP=$($quality.FP), FN=$($quality.FN)"
    $lines += "precision=$($quality.Precision), recall=$($quality.Recall)"
}
$lines += ""
$lines += "Checklist cierre WI:"
$lines += "- 99101: adjuntar Q3 + Q7 + decision de umbral."
$lines += "- 99103: adjuntar Q5 + Q6 + evidencia de alertas (regla/umbral/accion)."

$lines | Out-File -FilePath $summaryPath -Encoding utf8

Write-Host ""
Write-Host "Evidencia generada correctamente." -ForegroundColor Green
Write-Host "Salida: $OutputDir" -ForegroundColor Green
