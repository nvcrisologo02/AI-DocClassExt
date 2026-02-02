# clean-and-test.ps1
Write-Host "=== Limpieza Total y Test ===" -ForegroundColor Cyan

$proyectoRoot = "C:\temp\MVP\documento-ia-clasificacion-mvp"
Set-Location $proyectoRoot

# 1. DETENER FUNCTIONS
Write-Host "`n[1/5] Deteniendo Functions..." -ForegroundColor Yellow
Get-Process -Name func -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3
Write-Host "  OK" -ForegroundColor Green

# 2. DETENER Y LIMPIAR CONTENEDORES
Write-Host "`n[2/5] Limpiando contenedores..." -ForegroundColor Yellow
docker-compose down -v
Start-Sleep -Seconds 5
docker-compose up -d
Start-Sleep -Seconds 10
Write-Host "  OK" -ForegroundColor Green

# 3. LIMPIAR BD
Write-Host "`n[3/5] Limpiando base de datos..." -ForegroundColor Yellow
$sqlCmds = @(
    "DROP DATABASE IF EXISTS DocumentIA;",
    "CREATE DATABASE DocumentIA;",
    "USE DocumentIA;"
)

foreach ($cmd in $sqlCmds) {
    docker exec documentia-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "COMPLETAR_SQL_PASSWORD" -C -Q $cmd 2>$null
}
Write-Host "  OK: BD recreada" -ForegroundColor Green

# 4. RECOMPILAR
Write-Host "`n[4/5] Recompilando..." -ForegroundColor Yellow

Remove-Item -Recurse -Force "$proyectoRoot\src\backend\DocumentIA.Functions\bin" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$proyectoRoot\src\backend\DocumentIA.Functions\obj" -ErrorAction SilentlyContinue

Set-Location "$proyectoRoot\src\backend\DocumentIA.Core"
dotnet build -q

Set-Location "$proyectoRoot\src\backend\DocumentIA.Data"
dotnet build -q

Set-Location "$proyectoRoot\src\backend\DocumentIA.Functions"
dotnet build -q

# Copiar JSON
$jsonSource = "config\tipologias\tasacion.validation.json"
$jsonDest = "bin\Debug\net8.0\config\tipologias\tasacion.validation.json"
New-Item -ItemType Directory -Force -Path (Split-Path $jsonDest) | Out-Null
Copy-Item $jsonSource -Destination $jsonDest -Force

Write-Host "  OK: Compilado" -ForegroundColor Green

# 5. INICIAR FUNCTIONS Y PROBAR
Write-Host "`n[5/5] Iniciando Functions..." -ForegroundColor Yellow

$funcJob = Start-Job -ScriptBlock {
    Set-Location "C:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Functions"
    func start --port 7071
}

Write-Host "  Esperando 30 segundos..." -ForegroundColor Gray
Start-Sleep -Seconds 30

Set-Location $proyectoRoot

# TEST
Write-Host "`n=== EJECUTANDO TEST ===" -ForegroundColor Magenta

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$uniqueContent = "Test documento unico: $timestamp - Hash: $([guid]::NewGuid())"
$base64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($uniqueContent))

$body = @{
    instrucciones = @{
        expectedType = "Tasacion"
        skipDuplicateCheck = $true  # IMPORTANTE
        forceReprocess = $true
        classification = @{
            model = "DI"
            umbral = 0.85
        }
        extraction = @{
            model = "DI"
            umbral = 0.80
        }
    }
    documento = @{
        name = "tasacion-$timestamp.pdf"
        content = @{
            base64 = $base64
        }
    }
    trazabilidad = @{
        correlationId = "CLEAN-TEST-$timestamp"
        submittedBy = "test@sareb.es"
        idGDC = "GDC-$timestamp"
        idActivo = "ACT-$timestamp"
    }
} | ConvertTo-Json -Depth 10

Write-Host "Enviando documento..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod `
        -Uri "http://localhost:7071/api/IngestDocument" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -TimeoutSec 30
    
    $instanceId = $response.id
    Write-Host "InstanceId: $instanceId" -ForegroundColor Green
    
    # Esperar
    $maxWait = 60
    $waited = 0
    
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 3
        $waited += 3
        
        try {
            $statusUrl = "http://localhost:7071/runtime/webhooks/durabletask/instances/$instanceId"
            $status = Invoke-RestMethod -Uri $statusUrl -Method Get
            
            # VERIFICAR SI ES ARRAY
            if ($status -is [array]) {
                Write-Host "  WARNING: Status retorno array con $($status.Count) elementos" -ForegroundColor Yellow
                $status = $status[0]  # Tomar el primero
            }
            
            Write-Host "  Status: $($status.runtimeStatus) (${waited}s)" -ForegroundColor Gray
            
            if ($status.runtimeStatus -eq "Completed") {
                $output = $status.output
                
                Write-Host "`n=== RESULTADO ===" -ForegroundColor Green
                Write-Host "Estado: $($output.Resultado.Estado)" -ForegroundColor $(if($output.Resultado.Estado -eq "OK"){"Green"}else{"Yellow"})
                Write-Host "Tipologia: $($output.Identificacion.Tipologia)"
                Write-Host "Confianza: $($output.Resultado.ConfianzaGlobal)"
                
                Write-Host "`n=== VALIDACION ===" -ForegroundColor Magenta
                $pp = $output.DetalleEjecucion.Postproceso
                
                Write-Host "Normalizaciones:" -ForegroundColor Cyan
                if ($pp.Normalizaciones.Count -gt 0) {
                    $pp.Normalizaciones | ForEach-Object { Write-Host "  $_" }
                } else {
                    Write-Host "  (ninguna)" -ForegroundColor Gray
                }
                
                Write-Host "Validaciones:" -ForegroundColor Cyan
                if ($pp.Validaciones.Count -gt 0) {
                    $pp.Validaciones | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
                } else {
                    Write-Host "  (ninguna)" -ForegroundColor Gray
                }
                
                Write-Host "Inconsistencias:" -ForegroundColor Cyan
                if ($pp.Inconsistencias.Count -gt 0) {
                    $pp.Inconsistencias | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
                } else {
                    Write-Host "  (ninguna)" -ForegroundColor Green
                }
                
                # VERIFICAR MOTOR REAL
                $hayReglas = ($pp.Normalizaciones -join " ") -match "reglas de validacion|Confianza de validacion"
                $noHayMock = ($pp.Normalizaciones -join " ") -notmatch "Dirección normalizada"
                
                Write-Host ""
                if ($hayReglas -and $noHayMock) {
                    Write-Host "*** MOTOR DE VALIDACION REAL ACTIVO ***" -ForegroundColor Green -BackgroundColor DarkGreen
                } else {
                    Write-Host "*** WARNING: Usando Mock ***" -ForegroundColor Yellow -BackgroundColor DarkRed
                    Write-Host "hayReglas: $hayReglas, noHayMock: $noHayMock" -ForegroundColor Gray
                }
                
                break
            }
            
            if ($status.runtimeStatus -eq "Failed") {
                Write-Host "`nERROR:" -ForegroundColor Red
                $status | ConvertTo-Json -Depth 10
                break
            }
        } catch {
            Write-Host "  Esperando..." -ForegroundColor DarkGray
        }
    }
    
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}

# Cleanup
Write-Host "`n[Cleanup]" -ForegroundColor Yellow
Stop-Job $funcJob -ErrorAction SilentlyContinue
Remove-Job $funcJob -Force -ErrorAction SilentlyContinue

Write-Host "Test completado" -ForegroundColor Cyan
