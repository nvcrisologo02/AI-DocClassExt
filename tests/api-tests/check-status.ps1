# Script para consultar el estado de una orquestación

param(
    [Parameter(Mandatory=$false)]
    [string]$InstanceId
)

# Si no se proporciona instanceId, leer del archivo
if ([string]::IsNullOrEmpty($InstanceId)) {
    if (Test-Path "last-instance-id-notasimple14.txt") {
        $InstanceId = Get-Content "last-instance-id-notasimple14.txt" -Raw
        $InstanceId = $InstanceId.Trim()
        Write-Host "Usando Instance ID del último test: $InstanceId`n" -ForegroundColor Gray
    } else {
        Write-Host "Error: No se proporcionó Instance ID y no se encontró last-instance-id.txt" -ForegroundColor Red
        Write-Host "Uso: .\check-status.ps1 -InstanceId <tu-instance-id>" -ForegroundColor Yellow
        exit 1
    }
}

$statusUri = "http://localhost:7071/runtime/webhooks/durabletask/instances/$InstanceId"

Write-Host "Consultando estado de: $InstanceId`n" -ForegroundColor Cyan

try {
    $status = Invoke-RestMethod -Uri $statusUri -Method Get
    
    Write-Host "Runtime Status: " -NoNewline -ForegroundColor Gray
    
    switch ($status.runtimeStatus) {
        "Running" { Write-Host "RUNNING" -ForegroundColor Yellow }
        "Completed" { Write-Host "COMPLETED" -ForegroundColor Green }
        "Failed" { Write-Host "FAILED" -ForegroundColor Red }
        "Pending" { Write-Host "PENDING" -ForegroundColor Blue }
        default { Write-Host $status.runtimeStatus -ForegroundColor White }
    }
    
    Write-Host "Created Time  : " -NoNewline -ForegroundColor Gray
    Write-Host $status.createdTime -ForegroundColor White
    
    Write-Host "Last Updated  : " -NoNewline -ForegroundColor Gray
    Write-Host $status.lastUpdatedTime -ForegroundColor White
    
    if ($status.output) {
        Write-Host "`n--- Resultado (Output) ---" -ForegroundColor Cyan
        $status.output | ConvertTo-Json -Depth 10 | Write-Host
    }
    
    if ($status.customStatus) {
        Write-Host "`n--- Custom Status ---" -ForegroundColor Cyan
        $status.customStatus | ConvertTo-Json -Depth 10 | Write-Host
    }

} catch {
    Write-Host "Error al consultar estado:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
