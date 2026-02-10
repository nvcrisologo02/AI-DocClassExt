# rebuild-and-test.ps1
Write-Host "=== Rebuild Completo y Test de Validacion ===" -ForegroundColor Cyan

$ErrorActionPreference = "Continue"
$proyectoRoot = "C:\temp\MVP\documento-ia-clasificacion-mvp"

# Verificar que estamos en la carpeta correcta
if (-not (Test-Path $proyectoRoot)) {
    Write-Host "ERROR: Carpeta del proyecto no encontrada: $proyectoRoot" -ForegroundColor Red
    exit 1
}

Set-Location $proyectoRoot
Write-Host "Working directory: $proyectoRoot" -ForegroundColor Gray

# 1. LIMPIAR Y COMPILAR
Write-Host "`n[1/6] Limpiando y compilando..." -ForegroundColor Yellow

# Detener Functions
Get-Process -Name func -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Limpiar bins manualmente
$binPaths = @(
    "$proyectoRoot\src\backend\DocumentIA.Functions\bin",
    "$proyectoRoot\src\backend\DocumentIA.Functions\obj",
    "$proyectoRoot\src\backend\DocumentIA.Core\bin",
    "$proyectoRoot\src\backend\DocumentIA.Core\obj"
)

foreach ($path in $binPaths) {
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path -ErrorAction SilentlyContinue
        Write-Host "  Limpiado: $path" -ForegroundColor Gray
    }
}

# Compilar cada proyecto individualmente
Write-Host "  Compilando DocumentIA.Core..." -ForegroundColor Gray
Set-Location "$proyectoRoot\src\backend\DocumentIA.Core"
dotnet build --configuration Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Compilacion de Core fallo" -ForegroundColor Red
    exit 1
}

Write-Host "  Compilando DocumentIA.Data..." -ForegroundColor Gray
Set-Location "$proyectoRoot\src\backend\DocumentIA.Data"
dotnet build --configuration Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Compilacion de Data fallo" -ForegroundColor Red
    exit 1
}

Write-Host "  Compilando DocumentIA.Functions..." -ForegroundColor Gray
Set-Location "$proyectoRoot\src\backend\DocumentIA.Functions"
dotnet build --configuration Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Compilacion de Functions fallo" -ForegroundColor Red
    exit 1
}

Set-Location $proyectoRoot
Write-Host "  OK: Compilacion exitosa" -ForegroundColor Green

# 2. COPIAR CONFIGURACION JSON
Write-Host "`n[2/6] Configurando archivos JSON..." -ForegroundColor Yellow

$configSourceDir = "$proyectoRoot\src\backend\DocumentIA.Functions\config\tipologias"
$configSourceFile = "$configSourceDir\tasacion.validation.json"
$configDestDir = "$proyectoRoot\src\backend\DocumentIA.Functions\bin\Debug\net8.0\config\tipologias"
$configDestFile = "$configDestDir\tasacion.validation.json"

# Crear directorio origen si no existe
if (-not (Test-Path $configSourceDir)) {
    New-Item -ItemType Directory -Force -Path $configSourceDir | Out-Null
    Write-Host "  Creado directorio origen: $configSourceDir" -ForegroundColor Gray
}

# Verificar/crear archivo fuente
if (-not (Test-Path $configSourceFile)) {
    Write-Host "  Creando archivo de configuracion en origen..." -ForegroundColor Yellow
    
    $jsonContent = @'
{
  "tipologiaId": "tasacion",
  "tipologiaNombre": "Tasacion",
  "version": "1.0",
  "fields": [
    {
      "name": "ValorTasado",
      "type": "decimal",
      "required": true,
      "rules": [
        {
          "ruleType": "range",
          "severity": "Error",
          "parameters": {
            "min": 1000,
            "max": 2000000
          }
        }
      ]
    },
    {
      "name": "FechaTasacion",
      "type": "date",
      "required": true,
      "rules": [
        {
          "ruleType": "date",
          "severity": "Error",
          "parameters": {
            "formats": ["dd/MM/yyyy", "yyyy-MM-dd"],
            "allowFuture": false,
            "allowPast": true
          }
        }
      ]
    },
    {
      "name": "NIF",
      "type": "string",
      "required": false,
      "rules": [
        {
          "ruleType": "nif",
          "severity": "Warning",
          "parameters": {}
        }
      ]
    },
    {
      "name": "ReferenciaCatastral",
      "type": "string",
      "required": false,
      "rules": [
        {
          "ruleType": "catastral",
          "severity": "Warning",
          "parameters": {}
        }
      ]
    }
  ]
}
'@
    
    Set-Content -Path $configSourceFile -Value $jsonContent
    Write-Host "  Creado: $configSourceFile" -ForegroundColor Green
}

# Crear directorio destino
if (-not (Test-Path $configDestDir)) {
    New-Item -ItemType Directory -Force -Path $configDestDir | Out-Null
    Write-Host "  Creado directorio destino: $configDestDir" -ForegroundColor Gray
}

# Copiar archivo
Copy-Item $configSourceFile -Destination $configDestFile -Force
Write-Host "  OK: JSON copiado" -ForegroundColor Green

# Verificar copia
if (Test-Path $configDestFile) {
    $fileSize = (Get-Item $configDestFile).Length
    Write-Host "  Verificado: JSON en output ($fileSize bytes)" -ForegroundColor Green
} else {
    Write-Host "  ERROR: JSON no se copio correctamente" -ForegroundColor Red
    exit 1
}

# 3. VERIFICAR CONTENEDORES
Write-Host "`n[3/6] Verificando contenedores Docker..." -ForegroundColor Yellow

Set-Location $proyectoRoot
docker-compose up -d
Start-Sleep -Seconds 5

$containers = docker ps --format "{{.Names}}"
if ($containers -match "documentia-sql" -and $containers -match "azurite") {
    Write-Host "  OK: Contenedores activos" -ForegroundColor Green
} else {
    Write-Host "  ERROR: Contenedores no iniciados" -ForegroundColor Red
    exit 1
}

# 4. LIMPIAR BASE DE DATOS
Write-Host "`n[4/6] Limpiando base de datos..." -ForegroundColor Yellow

$sqlCmd = "USE DocumentIA; DELETE FROM ResultadosProcesamiento; DELETE FROM Auditoria; DELETE FROM Documentos;"
docker exec documentia-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "COMPLETAR_SQL_PASSWORD" -C -Q $sqlCmd 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "  OK: Base de datos limpiada" -ForegroundColor Green
} else {
    Write-Host "  WARNING: No se pudo limpiar BD" -ForegroundColor Yellow
}

# 5. INICIAR FUNCTIONS
Write-Host "`n[5/6] Iniciando Azure Functions..." -ForegroundColor Yellow

$functionsDir = "$proyectoRoot\src\backend\DocumentIA.Functions"
Set-Location $functionsDir

$funcJob = Start-Job -ScriptBlock { 
    param($dir)
    Set-Location $dir
    func start --port 7071
} -ArgumentList $functionsDir

Write-Host "  Functions iniciadas (Job: $($funcJob.Id))" -ForegroundColor Gray
Write-Host "  Esperando 25 segundos..." -ForegroundColor Gray
Start-Sleep -Seconds 25

try {
    $null = Invoke-WebRequest -Uri "http://localhost:7071/COMPLETAR_GDC_HTTP_BASIC_USERNAME/host/status" -Method Get -TimeoutSec 5 -ErrorAction Stop
    Write-Host "  OK: Functions corriendo" -ForegroundColor Green
} catch {
    Write-Host "  WARNING: No se pudo verificar health check" -ForegroundColor Yellow
}

Set-Location $proyectoRoot

# 6. EJECUTAR TEST
Write-Host "`n[6/6] Ejecutando test..." -ForegroundColor Yellow

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$correlationId = "TEST-VAL-$timestamp"
$uniqueContent = "Test documento: $timestamp"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($uniqueContent)
$base64 = [Convert]::ToBase64String($bytes)

Write-Host "  CorrelationId: $correlationId" -ForegroundColor Cyan

$body = @{
    instrucciones = @{
        expectedType = "Tasacion"
        skipDuplicateCheck = $true
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
        correlationId = $correlationId
        submittedBy = "test@sareb.es"
        idGDC = "GDC-$timestamp"
        idActivo = "ACT-$timestamp"
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod `
        -Uri "http://localhost:7071/api/IngestDocument" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -TimeoutSec 30
    
    $instanceId = $response.id
    Write-Host "  InstanceId: $instanceId" -ForegroundColor Green
    
    $maxWait = 60
    $waited = 0
    
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 3
        $waited += 3
        
        try {
            $status = Invoke-RestMethod `
                -Uri "http://localhost:7071/runtime/webhooks/durabletask/instances/$instanceId" `
                -Method Get
            
            Write-Host "    Status: $($status.runtimeStatus) (${waited}s)" -ForegroundColor Gray
            
            if ($status.runtimeStatus -eq "Completed") {
                $output = $status.output
                
                Write-Host "`n  === RESULTADO ===" -ForegroundColor Green
                Write-Host "  Estado: $($output.Resultado.Estado)" -ForegroundColor $(if($output.Resultado.Estado -eq "OK"){"Green"}else{"Yellow"})
                Write-Host "  Tipologia: $($output.Identificacion.Tipologia)"
                Write-Host "  Confianza: $($output.Resultado.ConfianzaGlobal)"
                
                Write-Host "`n  === VALIDACION (Motor de Reglas) ===" -ForegroundColor Magenta
                $pp = $output.DetalleEjecucion.Postproceso
                
                Write-Host "  Normalizaciones:" -ForegroundColor Yellow
                if ($pp.Normalizaciones.Count -gt 0) {
                    $pp.Normalizaciones | ForEach-Object { Write-Host "    - $_" -ForegroundColor White }
                } else {
                    Write-Host "    (ninguna)" -ForegroundColor Gray
                }
                
                Write-Host "  Validaciones:" -ForegroundColor Yellow
                if ($pp.Validaciones.Count -gt 0) {
                    $pp.Validaciones | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
                } else {
                    Write-Host "    (ninguna)" -ForegroundColor Gray
                }
                
                Write-Host "  Inconsistencias:" -ForegroundColor Yellow
                if ($pp.Inconsistencias.Count -gt 0) {
                    $pp.Inconsistencias | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
                } else {
                    Write-Host "    (ninguna)" -ForegroundColor Green
                }
                
                $motorReal = ($pp.Normalizaciones -join " ") -match "reglas de validacion|Confianza de validacion"
                
                Write-Host ""
                if ($motorReal) {
                    Write-Host "  *** MOTOR DE VALIDACION REAL ACTIVO ***" -ForegroundColor Green -BackgroundColor DarkGreen
                } else {
                    Write-Host "  *** WARNING: Mock en uso ***" -ForegroundColor Yellow -BackgroundColor DarkRed
                }
                
                break
            }
            
            if ($status.runtimeStatus -eq "Failed") {
                Write-Host "`n  ERROR:" -ForegroundColor Red
                $status.output
                break
            }
        } catch {
            # Esperando
        }
    }
    
} catch {
    Write-Host "`n  ERROR: $_" -ForegroundColor Red
}

Write-Host "`n[Cleanup] Deteniendo Functions..." -ForegroundColor Yellow
Stop-Job $funcJob -ErrorAction SilentlyContinue
Remove-Job $funcJob -Force -ErrorAction SilentlyContinue

Set-Location $proyectoRoot
Write-Host "`nTest completado" -ForegroundColor Cyan
