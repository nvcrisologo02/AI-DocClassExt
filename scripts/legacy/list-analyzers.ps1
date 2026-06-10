<#
List and filter Azure Content Understanding analyzers.
Usage:
  .\list-analyzers.ps1 -Endpoint https://<your-endpoint> -Key <api-key>           # list all
  .\list-analyzers.ps1 -Endpoint https://<your-endpoint> -Key <api-key> -AnalyzerId CU_NS_1.4_2  # show specific
  .\list-analyzers.ps1 -Endpoint https://<your-endpoint> -Key <api-key> -Save    # also save to tmp_analyzers.json
#>
param(
    [Parameter(Mandatory=$false)][string]$Endpoint,
    [Parameter(Mandatory=$false)][string]$Key,
    [Parameter(Mandatory=$false)][string]$AnalyzerId,
    [switch]$Save
)

if (-not $Endpoint) { $Endpoint = Read-Host "Endpoint (e.g. https://srbdiprodocai.cognitiveservices.azure.com)" }
if (-not $Key) { $Key = Read-Host "API Key (leave empty if using DefaultAzureCredential)" }

$apiVersion = "2025-11-01"
$uri = "$Endpoint/contentunderstanding/analyzers?api-version=$apiVersion"

try {
    if ($Key) {
        $res = Invoke-RestMethod -Uri $uri -Method Get -Headers @{ "Ocp-Apim-Subscription-Key" = $Key } -ErrorAction Stop
    } else {
        # If no key provided, attempt anonymous GET (may fail if auth required)
        $res = Invoke-RestMethod -Uri $uri -Method Get -ErrorAction Stop
    }
} catch {
    Write-Error "Request failed: $($_.Exception.Message)"
    exit 1
}

if ($AnalyzerId) {
    $filtered = $res.value | Where-Object { $_.analyzerId -eq $AnalyzerId }
    if (-not $filtered) { Write-Output "No analyzer found with analyzerId '$AnalyzerId'" }
    $filtered | ConvertTo-Json -Depth 10 | Write-Output
    if ($Save) { $filtered | ConvertTo-Json -Depth 10 | Out-File tmp_analyzers.json -Encoding utf8 }
} else {
    $res | ConvertTo-Json -Depth 10 | Write-Output
    if ($Save) { $res | ConvertTo-Json -Depth 10 | Out-File tmp_analyzers.json -Encoding utf8 }
}
