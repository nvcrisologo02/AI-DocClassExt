param(
    [int[]]$Ports = @(20000, 20001, 20002)
)

$processIds = @()

foreach ($port in $Ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($connections) {
        $processIds += $connections | Select-Object -ExpandProperty OwningProcess
    }
}

$processIds = $processIds | Sort-Object -Unique

if (-not $processIds -or $processIds.Count -eq 0) {
    Write-Host "No hay proceso escuchando en los puertos de Azurite ($($Ports -join '/'))." -ForegroundColor Yellow
    exit 0
}

foreach ($processId in $processIds) {
    try {
        $proc = Get-Process -Id $processId -ErrorAction Stop
        Stop-Process -Id $processId -Force -ErrorAction Stop
        Write-Host "Detenido proceso $($proc.ProcessName) (PID $processId)" -ForegroundColor Green
    }
    catch {
        Write-Host "No se pudo detener el PID ${processId}: $_" -ForegroundColor Yellow
    }
}