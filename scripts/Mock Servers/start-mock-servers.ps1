param(
    [string]$PythonCommand = "python"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

$enrichmentScript = Join-Path $scriptDir "mock-enrichment-server.py"
$soapScript = Join-Path $scriptDir "mock-soap-server.py"
$activoEnrichmentDir = Join-Path $projectRoot "src\enrichments\ActivoEnrichment"
$activoEnrichmentApp = Join-Path $activoEnrichmentDir "ActivoEnrichment.py"
$pidFile = Join-Path $scriptDir ".mock-servers.pids.json"

if (-not (Test-Path $enrichmentScript)) {
    Write-Host "[ERROR] No se encontro: $enrichmentScript" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $soapScript)) {
    Write-Host "[ERROR] No se encontro: $soapScript" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $activoEnrichmentApp)) {
    Write-Host "[ERROR] No se encontro: $activoEnrichmentApp" -ForegroundColor Red
    exit 1
}

try {
    & $PythonCommand --version | Out-Null
} catch {
    Write-Host "[ERROR] No se pudo ejecutar '$PythonCommand'." -ForegroundColor Red
    Write-Host "        Asegurate de tener Python en PATH o usa -PythonCommand con ruta completa." -ForegroundColor Yellow
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "INICIANDO MOCK SERVERS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$psCommandEnrichment = "Set-Location '$projectRoot'; & '$PythonCommand' '$enrichmentScript'"
$psCommandSoap = "Set-Location '$projectRoot'; & '$PythonCommand' '$soapScript'"
$psCommandActivo = "Set-Location '$activoEnrichmentDir'; & '$PythonCommand' -m uvicorn ActivoEnrichment:app --host 0.0.0.0 --port 8082"

$procEnrichment = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $psCommandEnrichment -PassThru
Start-Sleep -Milliseconds 400
$procSoap = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $psCommandSoap -PassThru
Start-Sleep -Milliseconds 400
$procActivo = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $psCommandActivo -PassThru

$pidInfo = @{
    startedAt = (Get-Date).ToString("o")
    processes = @(
        @{ name = "mock-enrichment-server"; pid = $procEnrichment.Id },
        @{ name = "mock-soap-server"; pid = $procSoap.Id },
        @{ name = "activo-enrichment"; pid = $procActivo.Id }
    )
}

$pidInfo | ConvertTo-Json -Depth 5 | Set-Content -Path $pidFile -Encoding UTF8

Write-Host "[OK] Servidores lanzados en ventanas separadas:" -ForegroundColor Green
Write-Host "     - Enrichment: http://localhost:8080" -ForegroundColor Gray
Write-Host "     - SOAP:       http://localhost:8081" -ForegroundColor Gray
Write-Host "     - Activo API: http://localhost:8082" -ForegroundColor Gray
Write-Host "     - PID file:   $pidFile" -ForegroundColor DarkGray
Write-Host "" 
Write-Host "Para detenerlos, cierra las ventanas o presiona Ctrl+C en cada una." -ForegroundColor Yellow
