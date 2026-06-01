param(
    [string]$SubscriptionId = "647c7246-54bc-4d31-b909-431cacf03272",
    [string]$ResourceGroup = "SRBRGDOCSAIPROD",
    [string]$AppInsightsName = "srbappiprodocai",
    [string]$OutputDir = ".\artifacts\reports\cu-performance",
    [string]$Offset = "24h",
    # Rango absoluto (ISO 8601). Si se proveen, se ignora $Offset.
    # Ejemplo: -StartTime "2026-05-29T08:10:00Z" -EndTime "2026-05-29T20:10:00Z"
    [string]$StartTime = "",
    [string]$EndTime = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message"
}

function Convert-OffsetToTimespan {
    param([string]$Value)

    if ($Value -match '^(?<amount>\d+)h$') {
        return "PT$($Matches.amount)H"
    }

    if ($Value -match '^(?<amount>\d+)m$') {
        return "PT$($Matches.amount)M"
    }

    if ($Value -match '^(?<amount>\d+)d$') {
        return "P$($Matches.amount)D"
    }

    return $Value
}

function Get-AppInsightsAppId {
    $resourceId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/microsoft.insights/components/$AppInsightsName"
    $url = "https://management.azure.com${resourceId}?api-version=2020-02-02"

    $appId = az rest --method get --url $url --query "properties.AppId" -o tsv
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($appId)) {
        throw "No se pudo resolver AppId de Application Insights $AppInsightsName."
    }

    return $appId.Trim()
}

function Get-AppInsightsAccessToken {
    $token = az account get-access-token --resource "https://api.applicationinsights.io" --query "accessToken" -o tsv
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
        throw "No se pudo obtener token para Application Insights API."
    }

    return $token.Trim()
}

function Get-EffectiveTimespan {
    if (-not [string]::IsNullOrWhiteSpace($script:StartTime) -and -not [string]::IsNullOrWhiteSpace($script:EndTime)) {
        return "$($script:StartTime)/$($script:EndTime)"
    }
    return Convert-OffsetToTimespan -Value $Offset
}

function Invoke-AppInsightsRestQuery {
    param(
        [Parameter(Mandatory = $true)][string]$AppId,
        [Parameter(Mandatory = $true)][string]$AccessToken,
        [Parameter(Mandatory = $true)][string]$Query
    )

    $uri = "https://api.applicationinsights.io/v1/apps/$AppId/query"
    $body = @{
        query = $Query
        timespan = Get-EffectiveTimespan
    } | ConvertTo-Json -Depth 4

    $headers = @{
        Authorization = "Bearer $AccessToken"
        "Content-Type" = "application/json"
    }

    return Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body
}

function Invoke-AppInsightsQuery {
    param(
        [Parameter(Mandatory = $true)][string]$Query,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $jsonPath = Join-Path $OutputDir "$Name.json"
    $csvPath = Join-Path $OutputDir "$Name.csv"

    Write-Info "Ejecutando $Name"
    $result = Invoke-AppInsightsRestQuery -AppId $script:AppInsightsAppId -AccessToken $script:AppInsightsAccessToken -Query $Query
    $raw = $result | ConvertTo-Json -Depth 100

    $raw | Out-File -FilePath $jsonPath -Encoding utf8

    if ($null -ne $result.tables -and $result.tables.Count -gt 0) {
        $table = $result.tables[0]
        $columns = @($table.columns | ForEach-Object { $_.name })
        $rows = foreach ($row in $table.rows) {
            $obj = [ordered]@{}
            for ($i = 0; $i -lt $columns.Count; $i++) {
                $obj[$columns[$i]] = $row[$i]
            }
            [pscustomobject]$obj
        }

        if ($rows) {
            $rows | Export-Csv -Path $csvPath -NoTypeInformation -Encoding utf8
        }
        else {
            "No rows" | Out-File -FilePath $csvPath -Encoding utf8
        }
    }

    return [pscustomobject]@{
        Name = $Name
        Json = $jsonPath
        Csv = $csvPath
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$script:AppInsightsAppId = Get-AppInsightsAppId
$script:AppInsightsAccessToken = Get-AppInsightsAccessToken
$script:StartTime = $StartTime
$script:EndTime = $EndTime
Write-Info "Application Insights AppId: $script:AppInsightsAppId"

$queries = [ordered]@{}

$queries["01-cu-metrics-summary"] = @"
customMetrics
| where name startswith "CU."
| summarize eventos=count(), avg_ms=round(avg(value), 1), p50_ms=round(percentile(value, 50), 1), p95_ms=round(percentile(value, 95), 1), p99_ms=round(percentile(value, 99), 1), max_ms=max(value) by name
| order by name asc
"@

$queries["02-cu-subphase-timeseries"] = @"
customMetrics
| where name in ("CU.PrepareMs", "CU.LimiterWaitMs", "CU.AnalysisMs", "CU.ParseMs")
| extend tipologia = tostring(customDimensions["Tipologia"])
| summarize p50_ms=percentile(value, 50), p95_ms=percentile(value, 95), p99_ms=percentile(value, 99) by bin(timestamp, 5m), name, tipologia
| order by timestamp asc, name asc
"@

$queries["03-cu-wait-vs-analysis-diagnosis"] = @"
customMetrics
| where name in ("CU.LimiterWaitMs", "CU.AnalysisMs")
| summarize p50_ms=percentile(value, 50), p95_ms=percentile(value, 95), p99_ms=percentile(value, 99), max_ms=max(value) by name
| extend diagnostico = case(
    name == "CU.LimiterWaitMs" and p95_ms > 10000, "Cola local/backpressure",
    name == "CU.AnalysisMs" and p95_ms > 60000, "Azure CU lento o saturado",
    "Normal o revisar junto a la otra metrica")
| order by name asc
"@

$queries["04-cu-top-waiting-operations"] = @"
customMetrics
| where name in ("CU.PrepareMs", "CU.LimiterWaitMs", "CU.AnalysisMs", "CU.ParseMs", "CU.Attempts")
| extend tipologia = tostring(customDimensions["Tipologia"])
| summarize ts=min(timestamp),
          prepare_ms=maxif(value, name == "CU.PrepareMs"),
          wait_ms=maxif(value, name == "CU.LimiterWaitMs"),
          analysis_ms=maxif(value, name == "CU.AnalysisMs"),
          parse_ms=maxif(value, name == "CU.ParseMs"),
          attempts=maxif(value, name == "CU.Attempts")
  by operation_Id, tipologia
| extend total_observado_ms = prepare_ms + wait_ms + analysis_ms + parse_ms
| extend wait_pct = round(100.0 * wait_ms / total_observado_ms, 1)
| order by wait_ms desc
| take 100
"@

$queries["05-cu-transient-errors"] = @"
customEvents
| where name == "CU.TransientError"
| extend tipologia = tostring(customDimensions["tipologia"]),
         attempt = tostring(customDimensions["attempt"]),
         statusCode = tostring(customDimensions["statusCode"])
| summarize eventos=count() by bin(timestamp, 5m), tipologia, statusCode, attempt
| order by timestamp desc
"@

$summary = @()
foreach ($entry in $queries.GetEnumerator()) {
    $summary += Invoke-AppInsightsQuery -Query $entry.Value -Name $entry.Key
}

$summaryPath = Join-Path $OutputDir "README.md"
$lines = @()
$lines += "# Reporte CU Performance"
$lines += ""
$lines += "Generado: $(Get-Date -Format s)"
$lines += "Resource group: $ResourceGroup"
$lines += "Application Insights: $AppInsightsName"
if (-not [string]::IsNullOrWhiteSpace($StartTime) -and -not [string]::IsNullOrWhiteSpace($EndTime)) {
    $lines += "Ventana: $StartTime / $EndTime"
} else {
    $lines += "Ventana: $Offset"
}
$lines += ""
$lines += "## Archivos"
$lines += ""
foreach ($item in $summary) {
    $lines += "- $($item.Name): $($item.Csv) / $($item.Json)"
}
$lines += ""
$lines += "## Lectura rapida"
$lines += ""
$lines += '- CU.LimiterWaitMs alto: espera local/backpressure antes de llamar a Content Understanding.'
$lines += '- CU.AnalysisMs alto: Azure Content Understanding tarda en analizar.'
$lines += '- CU.Attempts > 1 o CU.TransientError: reintentos/errores transitorios.'

$lines | Out-File -FilePath $summaryPath -Encoding utf8

Write-Info "Reporte generado en $OutputDir"
Write-Info "Resumen: $summaryPath"
