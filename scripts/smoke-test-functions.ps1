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

$uri = "https://$HostName/api/tipologias"
Write-Host "== Smoke test Functions ==" -ForegroundColor Cyan
Write-Host "Endpoint: $uri"

for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    try {
        Write-Host "Intento $attempt/$MaxAttempts..." -ForegroundColor Yellow
        $response = Invoke-WebRequest -Uri $uri -Method Get -TimeoutSec 30

        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] Smoke test superado con HTTP 200." -ForegroundColor Green
            if (-not [string]::IsNullOrWhiteSpace($response.Content)) {
                Write-Host "Respuesta recibida." -ForegroundColor Gray
            }
            exit 0
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

throw "Smoke test fallido tras $MaxAttempts intentos contra $uri"
