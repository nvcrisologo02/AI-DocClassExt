param(
    [Parameter(Mandatory = $true)]
    [string]$HostName,

    [Parameter(Mandatory = $false)]
    [int]$MaxAttempts = 12,

    [Parameter(Mandatory = $false)]
    [int]$DelaySeconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$tipologiasUri = "https://$HostName/api/tipologias"
$healthcheckUri = "https://$HostName/api/healthcheck"
Write-Host "== Smoke test Functions ==" -ForegroundColor Cyan
Write-Host "Tipologias endpoint: $tipologiasUri"
Write-Host "Healthcheck endpoint: $healthcheckUri"

for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    try {
        Write-Host "Intento $attempt/$MaxAttempts..." -ForegroundColor Yellow
        $response = Invoke-WebRequest -Uri $tipologiasUri -Method Get -TimeoutSec 30

        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] Tipologias smoke test superado con HTTP 200." -ForegroundColor Green
            if (-not [string]::IsNullOrWhiteSpace($response.Content)) {
                Write-Host "Respuesta recibida." -ForegroundColor Gray
            }
            break
        }

        Write-Warning "Respuesta inesperada: HTTP $($response.StatusCode)"
    }
    catch {
        Write-Warning $_.Exception.Message
    }

    if ($attempt -lt $MaxAttempts) {
        Start-Sleep -Seconds $DelaySeconds
    }
}

if ($attempt -gt $MaxAttempts) {
    throw "Smoke test fallido tras $MaxAttempts intentos contra $tipologiasUri"
}

Write-Host "Validando healthcheck y configuracion de AssetResolver..." -ForegroundColor Yellow
$healthResponse = Invoke-WebRequest -Uri $healthcheckUri -Method Post -TimeoutSec 45 -SkipHttpErrorCheck
if ($healthResponse.StatusCode -ne 200 -and $healthResponse.StatusCode -ne 503) {
    throw "Healthcheck devolvio HTTP $($healthResponse.StatusCode). Se esperaba 200 o 503 con payload JSON."
}

if ([string]::IsNullOrWhiteSpace($healthResponse.Content)) {
    throw "Healthcheck devolvio respuesta vacia."
}

$health = $healthResponse.Content | ConvertFrom-Json
$assetResolver = $health.components.assetResolver
if ($null -eq $assetResolver) {
    throw "Healthcheck no incluye components.assetResolver."
}

if ($assetResolver.status -eq "unconfigured") {
    throw "AssetResolver no esta configurado en la Function App. Mensaje: $($assetResolver.message)"
}

Write-Host "[OK] Healthcheck recibido. AssetResolver status: $($assetResolver.status)" -ForegroundColor Green
Write-Host "Smoke test completado." -ForegroundColor Green
