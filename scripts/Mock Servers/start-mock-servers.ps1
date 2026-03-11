param(
    [string]$PythonCommand = "python"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

$enrichmentScript = Join-Path $scriptDir "mock-enrichment-server.py"
$soapScript = Join-Path $scriptDir "mock-soap-server.py"
$gdcScript = Join-Path $scriptDir "mock-gdc-server.py"
$activoEnrichmentDir = Join-Path $projectRoot "..\src\enrichments\ActivoEnrichment"
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

function Start-ServerWindow {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    $wrappedCommand = "try { `$Host.UI.RawUI.WindowTitle = 'Mock - $Name' } catch { }; $Command"
    return Start-Process -FilePath "powershell" -ArgumentList @(
        "-NoExit",
        "-ExecutionPolicy", "Bypass",
        "-Command", $wrappedCommand
    ) -WorkingDirectory $WorkingDirectory -PassThru
}

function Test-PortOpen {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [string]$HostName = "127.0.0.1",
        [int]$TimeoutMs = 500
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $iar = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $iar.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) {
            return $false
        }
        $client.EndConnect($iar)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Wait-PortReady {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$MaxAttempts = 15,
        [int]$DelayMs = 300
    )

    for ($i = 1; $i -le $MaxAttempts; $i++) {
        if (Test-PortOpen -Port $Port) {
            return $true
        }
        Start-Sleep -Milliseconds $DelayMs
    }
    return $false
}

try {
    # Start enrichment mock in a dedicated PowerShell window
    $cmdEnrichment = "& '$PythonCommand' '$enrichmentScript'"
    $procEnrichment = Start-ServerWindow -Name "mock-enrichment-server" -Command $cmdEnrichment -WorkingDirectory $projectRoot
    Start-Sleep -Milliseconds 400

    # Start SOAP mock in a dedicated PowerShell window
    $cmdSoap = "& '$PythonCommand' '$soapScript'"
    $procSoap = Start-ServerWindow -Name "mock-soap-server" -Command $cmdSoap -WorkingDirectory $projectRoot
    Start-Sleep -Milliseconds 400

    # Start ActivoEnrichment via uvicorn in a dedicated PowerShell window
    $cmdActivo = "& '$PythonCommand' -m uvicorn ActivoEnrichment:app --host 0.0.0.0 --port 8082"
    $procActivo = Start-ServerWindow -Name "activo-enrichment" -Command $cmdActivo -WorkingDirectory $activoEnrichmentDir
    Start-Sleep -Milliseconds 400

    # Start GDC mock in a dedicated PowerShell window (if present)
    if (Test-Path $gdcScript) {
        $cmdGdc = "& '$PythonCommand' '$gdcScript'"
        $procGdc = Start-ServerWindow -Name "mock-gdc-server" -Command $cmdGdc -WorkingDirectory $projectRoot
    }
}
catch {
    Write-Host "[ERROR] Fallo al iniciar procesos: $_" -ForegroundColor Red
    exit 1
}

$pidInfo = @{
    startedAt = (Get-Date).ToString("o")
    processes = @(
        @{ name = "mock-enrichment-server"; pid = $procEnrichment.Id },
        @{ name = "mock-soap-server"; pid = $procSoap.Id },
        @{ name = "activo-enrichment"; pid = $procActivo.Id }
    )
}

if ($procGdc -ne $null) {
    $pidInfo.processes += ,@{ name = "mock-gdc-server"; pid = $procGdc.Id }
}

$pidInfo | ConvertTo-Json -Depth 5 | Set-Content -Path $pidFile -Encoding UTF8

Write-Host "[OK] Servidores lanzados en ventanas separadas:" -ForegroundColor Green
Write-Host "     - Enrichment: http://localhost:8080" -ForegroundColor Gray
Write-Host "     - SOAP:       http://localhost:8081" -ForegroundColor Gray
Write-Host "     - Activo API: http://localhost:8082" -ForegroundColor Gray
Write-Host "     - GDC SOAP:   http://localhost:8083" -ForegroundColor Gray
Write-Host "     - PID file:   $pidFile" -ForegroundColor DarkGray
Write-Host "" 
Write-Host "Comprobando estado de puertos..." -ForegroundColor Cyan

$checks = @(
    @{ name = "Enrichment"; port = 8080 },
    @{ name = "SOAP"; port = 8081 },
    @{ name = "Activo API"; port = 8082 }
)

if ($procGdc -ne $null) {
    $checks += ,@{ name = "GDC SOAP"; port = 8083 }
}

foreach ($check in $checks) {
    if (Wait-PortReady -Port $check.port) {
        Write-Host ("[OK] {0} disponible en http://localhost:{1}" -f $check.name, $check.port) -ForegroundColor Green
    }
    else {
        Write-Host ("[WARN] {0} no responde en puerto {1}" -f $check.name, $check.port) -ForegroundColor Yellow
    }
}
Write-Host "" 
Write-Host "Para detenerlos, cierra las ventanas o presiona Ctrl+C en cada una." -ForegroundColor Yellow
