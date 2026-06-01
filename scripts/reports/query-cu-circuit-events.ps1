param(
    [string]$SubscriptionId = "647c7246-54bc-4d31-b909-431cacf03272",
    [string]$WorkspaceId = "44455fee-6a6f-4255-bfe0-3d259abddbbf",
    [int]$Hours = 24,
    [string]$Tipologia = "nota.simple_bal"
)

$ErrorActionPreference = "Stop"

$tokenJson = az account get-access-token --resource "https://api.loganalytics.io" --subscription $SubscriptionId -o json | ConvertFrom-Json
$headers = @{ Authorization = "Bearer $($tokenJson.accessToken)" }

function Invoke-AiQuery {
    param([string]$Query)

    $body = @{ query = $Query; timespan = "PT$($Hours)H" } | ConvertTo-Json -Depth 5
    Invoke-RestMethod -Method Post -Uri "https://api.loganalytics.io/v1/workspaces/$WorkspaceId/query" -Headers $headers -ContentType "application/json" -Body $body
}

Write-Host "=== Eventos CU.Circuit* y CU.HardTimeout (ultimas $Hours h) ==="
$queryEvents = @"
AppEvents
| where TimeGenerated > ago(${Hours}h)
| where Name in ('CU.CircuitOpen','CU.CircuitClosed','CU.CircuitFailover','CU.CircuitRejected','CU.HardTimeout')
| extend tipologia = tostring(Properties['tipologia']), Tipologia = tostring(Properties['Tipologia'])
| where isempty('$Tipologia') or tipologia == '$Tipologia' or Tipologia == '$Tipologia'
| project TimeGenerated, Name, Properties
| order by TimeGenerated desc
"@

$eventsResult = Invoke-AiQuery $queryEvents
$eventsResult | ConvertTo-Json -Depth 10

Write-Host "=== Conteo por evento ==="
$queryEventCount = @"
AppEvents
| where TimeGenerated > ago(${Hours}h)
| where Name in ('CU.CircuitOpen','CU.CircuitClosed','CU.CircuitFailover','CU.CircuitRejected','CU.HardTimeout')
| summarize Total=count() by Name
| order by Total desc
"@

$eventCountResult = Invoke-AiQuery $queryEventCount
$eventCountResult | ConvertTo-Json -Depth 10

Write-Host "=== CU.AnalysisMs por ModelKey (ultimas $Hours h) ==="
$queryMetrics = @"
AppMetrics
| where TimeGenerated > ago(${Hours}h)
| where Name == 'CU.AnalysisMs'
| extend ModelKey=tostring(Properties['ModelKey']), Tipologia=tostring(Properties['Tipologia']), DurationMs=todouble(Sum)
| where isempty('$Tipologia') or Tipologia == '$Tipologia'
| summarize Calls=count(), P50=round(percentile(DurationMs, 50),0), P95=round(percentile(DurationMs, 95),0), Avg=round(avg(DurationMs),0) by ModelKey
| order by Calls desc
"@

$metricsResult = Invoke-AiQuery $queryMetrics
$metricsResult | ConvertTo-Json -Depth 10
