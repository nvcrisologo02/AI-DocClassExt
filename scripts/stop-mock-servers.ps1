$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$pidFile = Join-Path $scriptDir ".mock-servers.pids.json"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DETENIENDO MOCK SERVERS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$stopped = 0

if (Test-Path $pidFile) {
    try {
        $pidInfo = Get-Content -Raw -Path $pidFile | ConvertFrom-Json
        foreach ($proc in $pidInfo.processes) {
            if ($null -eq $proc.pid) { continue }

            $running = Get-Process -Id ([int]$proc.pid) -ErrorAction SilentlyContinue
            if ($running) {
                Stop-Process -Id ([int]$proc.pid) -Force -ErrorAction SilentlyContinue
                Write-Host "[OK] Detenido $($proc.name) (PID $($proc.pid))" -ForegroundColor Green
                $stopped++
            }
        }
    } catch {
        Write-Host "[WARN] No se pudo leer PID file, aplicando fallback por patron." -ForegroundColor Yellow
    }
}

if ($stopped -eq 0) {
    $patterns = @(
        "mock-enrichment-server.py",
        "mock-soap-server.py",
        "-m uvicorn ActivoEnrichment:app"
    )

    $processes = Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe' OR Name = 'pwsh.exe'" |
        Where-Object {
            $cmd = $_.CommandLine
            if ([string]::IsNullOrEmpty($cmd)) { return $false }
            foreach ($p in $patterns) {
                if ($cmd -like "*$p*") { return $true }
            }
            return $false
        }

    foreach ($p in $processes) {
        try {
            Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
            Write-Host "[OK] Detenido proceso PID $($p.ProcessId)" -ForegroundColor Green
            $stopped++
        } catch {
        }
    }
}

if (Test-Path $pidFile) {
    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
}

if ($stopped -eq 0) {
    Write-Host "[INFO] No se encontraron procesos activos de mock servers." -ForegroundColor Yellow
} else {
    Write-Host "[OK] Procesos detenidos: $stopped" -ForegroundColor Green
}

Write-Host "========================================" -ForegroundColor Cyan
