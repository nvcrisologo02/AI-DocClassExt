<#
Send a local file to an Azure Content Understanding analyzer (binary analyze) and print/save the response.
Usage:
  .\run-analyze.ps1 -Endpoint "https://<ENDPOINT>" -Key "<API_KEY>" -FilePath .\sample.pdf
  .\run-analyze.ps1 -Endpoint "https://<ENDPOINT>" -Key "<API_KEY>" -FilePath .\sample.pdf -AnalyzerId CU_NS_1.4_2 -ProcessingLocation geography -Save
#>
param(
    [Parameter(Mandatory=$false)][string]$Endpoint,
    [Parameter(Mandatory=$false)][string]$Key,
    [Parameter(Mandatory=$true)][string]$FilePath,
    [Parameter(Mandatory=$false)][string]$AnalyzerId = "CU_NS_1.4_2",
    [Parameter(Mandatory=$false)][string]$ProcessingLocation = "geography",
    [switch]$Save
)

if (-not (Test-Path $FilePath)) { Write-Error "File not found: $FilePath"; exit 1 }
if (-not $Endpoint) { $Endpoint = Read-Host "Endpoint (e.g. https://srbdiprodocai.cognitiveservices.azure.com)" }
# normalize endpoint (remove trailing slash) to avoid double-slash in URL
$Endpoint = $Endpoint.TrimEnd('/')
if (-not $Key) { $Key = Read-Host "API Key" }

$apiVersion = "2025-11-01"
# Try two endpoint patterns: legacy (query param) and analyzers/{id}/analyze
$uriQuery = "$Endpoint/contentunderstanding/analyze?api-version=$apiVersion&analyzerId=$AnalyzerId&processingLocation=$ProcessingLocation"
$uriResource = "$Endpoint/contentunderstanding/analyzers/$AnalyzerId/analyze?api-version=$apiVersion&processingLocation=$ProcessingLocation"
$uriColon = "$Endpoint/contentunderstanding/analyzers/$AnalyzerId:analyze?api-version=$apiVersion&processingLocation=$ProcessingLocation"
$uriAlt = "$Endpoint/contentunderstanding/analyze/$AnalyzerId?api-version=$apiVersion&processingLocation=$ProcessingLocation"

$headers = @{ "Ocp-Apim-Subscription-Key" = $Key }

function Try-Analyze($uri) {
    Write-Output "POST $uri`nUploading file: $FilePath"
    try {
        return Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -InFile $FilePath -ContentType 'application/octet-stream' -ErrorAction Stop
    } catch {
        $ex = $_.Exception
        Write-Output "Request to $uri failed: $($ex.Message)"
        if ($ex.Response -ne $null) {
            try {
                $resp = $ex.Response
                $status = $resp.StatusCode
                $stream = $resp.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
                Write-Output "Response status: $status"
                Write-Output "Response body:`n$body"
            } catch {
                Write-Output "Failed to read response body: $($_.Exception.Message)"
            }
        }
        return $null
    }
}

# First try query style
Write-Output "Trying query-style endpoint..."
$response = Try-Analyze $uriQuery
if (-not $response) {
    Write-Output "Query-style analyze failed; trying resource-style endpoint..."
    $response = Try-Analyze $uriResource
}

if (-not $response) {
    Write-Output "Resource-style failed; trying colon-style endpoint..."
    $response = Try-Analyze $uriColon
}

if (-not $response) {
    Write-Output "Colon-style failed; trying alternate path..."
    $response = Try-Analyze $uriAlt
}

if (-not $response) { Write-Error "Both analyze endpoints failed."; exit 1 }

# Pretty-print and optionally save
$response | ConvertTo-Json -Depth 20 | Write-Output
if ($Save) { $response | ConvertTo-Json -Depth 20 | Out-File tmp_analyze_result.json -Encoding utf8; Write-Output "Saved to tmp_analyze_result.json" }
