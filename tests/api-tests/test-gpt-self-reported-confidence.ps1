# Test E2E para validar confianza dinámica self-reported por GPT
# AB#99727 - Tests E2E con respuestas reales de GPT
#
# Propósito:
#   Ejecutar múltiples clasificaciones GPT y validar que:
#   1. El campo Confianza está presente en la respuesta
#   2. Los valores de confianza varían (no siempre 0.9)
#   3. Todos los valores están en el rango 0.0-1.0
#   4. Se registra en logs la confianza self-reported
#
# Uso:
#   .\test-gpt-self-reported-confidence.ps1 -TestDocumentsPath "C:\temp\test-docs"

param(
    [Parameter(Mandatory = $false)]
    [string]$TestDocumentsPath = "C:\temp\test-docs",
    
    [Parameter(Mandatory = $false)]
    [int]$MinDocuments = 5,
    
    [Parameter(Mandatory = $false)]
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument"
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "================================================================="
Write-Host "  Test E2E - GPT Self-Reported Confidence (AB#99727)"
Write-Host "================================================================="
Write-Host ""

# Validar que existe el directorio de documentos
if (-not (Test-Path $TestDocumentsPath)) {
    Write-Host "[ERROR] No existe el directorio: $TestDocumentsPath" -ForegroundColor Red
    exit 1
}

# Buscar documentos PDF en el directorio
$documentos = Get-ChildItem -Path $TestDocumentsPath -Filter "*.pdf" | Select-Object -First 10

if ($documentos.Count -lt $MinDocuments) {
    Write-Host "[ERROR] Se requieren al menos $MinDocuments documentos PDF en $TestDocumentsPath" -ForegroundColor Red
    Write-Host "        Encontrados: $($documentos.Count)" -ForegroundColor Red
    exit 1
}

Write-Host "[INFO] Documentos encontrados: $($documentos.Count)" -ForegroundColor Cyan
Write-Host "[INFO] Endpoint: $Endpoint" -ForegroundColor Cyan
Write-Host ""

$resultados = @()

foreach ($doc in $documentos) {
    $documentPath = $doc.FullName
    $documentName = $doc.Name
    
    Write-Host "----------------------------------------" -ForegroundColor Yellow
    Write-Host "Procesando: $documentName" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor Yellow
    
    try {
        # Leer documento y convertir a base64
        $documentBytes = [System.IO.File]::ReadAllBytes($documentPath)
        $documentBase64 = [Convert]::ToBase64String($documentBytes)
        
        # Crear request body
        $body = @{
            documento = @{
                name = $documentName
                content = @{
                    base64 = $documentBase64
                }
            }
            trazabilidad = @{
                correlationId = "GPT-CONF-TEST-$(Get-Date -Format 'yyyyMMddHHmmss')-$($doc.BaseName)"
                submittedBy = "test.confidence@sareb.es"
                idGDC = $null
                idActivo = "TEST-CONF"
            }
        } | ConvertTo-Json -Depth 10
        
        # Invocar función
        $response = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $body -ContentType "application/json"
        
        Write-Host "  [✓] Instance ID: $($response.instanceId)" -ForegroundColor Green
        
        # Construir status URI
        $statusUri = $response.statusQueryUri
        if ($statusUri -match "http://localhost/") {
            $statusUri = $statusUri -replace "http://localhost/", "http://localhost:7071/"
        }
        
        # Esperar a que complete
        $maxRetries = 30
        $retryCount = 0
        $delaySeconds = 2
        $status = $null
        
        do {
            Start-Sleep -Seconds $delaySeconds
            
            try {
                $status = Invoke-RestMethod -Uri $statusUri -Method Get
                $retryCount++
                
                Write-Host "  [$retryCount/$maxRetries] Estado: $($status.runtimeStatus)" -NoNewline
                
                if ($status.runtimeStatus -eq "Running" -and $status.customStatus) {
                    $currentActivity = $status.customStatus.actividadActual ?? $status.customStatus.ActividadActual ?? $status.customStatus.currentActivity
                    if ($currentActivity) {
                        Write-Host " | Actual: $currentActivity" -NoNewline
                    }
                }
                
                Write-Host ""
            } catch {
                Write-Host "  [$retryCount/$maxRetries] Error consultando estado, reintentando..." -ForegroundColor Yellow
            }
        } while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retryCount -lt $maxRetries)
        
        if ($status.runtimeStatus -ne "Completed") {
            Write-Host "  [✗] Estado final: $($status.runtimeStatus)" -ForegroundColor Red
            $resultados += @{
                Documento = $documentName
                Estado = $status.runtimeStatus
                Confianza = $null
                Tipologia = $null
                Valido = $false
                Razon = "Procesamiento no completado: $($status.runtimeStatus)"
            }
            continue
        }
        
        # Extraer confianza de la respuesta
        $output = $status.output
        $confianza = $output.DetalleEjecucion.Clasificacion.Confianza
        $tipologia = $output.Identificacion.Tipologia
        $modelo = $output.DetalleEjecucion.Clasificacion.Modelo
        
        Write-Host "  [✓] Completado" -ForegroundColor Green
        Write-Host "    Tipologia: $tipologia" -ForegroundColor Cyan
        Write-Host "    Modelo   : $modelo" -ForegroundColor Cyan
        Write-Host "    Confianza: $confianza" -ForegroundColor Cyan
        
        # Validar confianza
        $valido = $true
        $razon = "OK"
        
        if ($null -eq $confianza) {
            $valido = $false
            $razon = "Confianza es NULL"
        }
        elseif ($confianza -lt 0.0 -or $confianza -gt 1.0) {
            $valido = $false
            $razon = "Confianza fuera de rango [0.0-1.0]: $confianza"
        }
        
        if ($valido) {
            Write-Host "    [✓] Validación: OK" -ForegroundColor Green
        } else {
            Write-Host "    [✗] Validación: $razon" -ForegroundColor Red
        }
        
        $resultados += @{
            Documento = $documentName
            Estado = "Completed"
            Confianza = $confianza
            Tipologia = $tipologia
            Modelo = $modelo
            Valido = $valido
            Razon = $razon
        }
        
    } catch {
        Write-Host "  [✗] Error: $($_.Exception.Message)" -ForegroundColor Red
        $resultados += @{
            Documento = $documentName
            Estado = "Error"
            Confianza = $null
            Tipologia = $null
            Valido = $false
            Razon = $_.Exception.Message
        }
    }
    
    Write-Host ""
}

# Resumen final
Write-Host "=================================================================" -ForegroundColor Yellow
Write-Host "  RESUMEN DE VALIDACIÓN" -ForegroundColor Yellow
Write-Host "=================================================================" -ForegroundColor Yellow
Write-Host ""

$totalProcesados = $resultados.Count
$totalValidos = ($resultados | Where-Object { $_.Valido -eq $true }).Count
$totalInvalidos = $totalProcesados - $totalValidos

Write-Host "Total procesados       : $totalProcesados" -ForegroundColor Cyan
Write-Host "Total válidos          : $totalValidos" -ForegroundColor Green
Write-Host "Total inválidos        : $totalInvalidos" -ForegroundColor $(if ($totalInvalidos -gt 0) { "Red" } else { "Gray" })
Write-Host ""

if ($totalInvalidos -gt 0) {
    Write-Host "Casos inválidos:" -ForegroundColor Red
    foreach ($resultado in ($resultados | Where-Object { $_.Valido -eq $false })) {
        Write-Host "  - $($resultado.Documento): $($resultado.Razon)" -ForegroundColor Red
    }
    Write-Host ""
}

# Análisis de variabilidad de confianza
$confianzas = $resultados | Where-Object { $null -ne $_.Confianza } | ForEach-Object { $_.Confianza }

if ($confianzas.Count -ge 2) {
    $confianzaMin = ($confianzas | Measure-Object -Minimum).Minimum
    $confianzaMax = ($confianzas | Measure-Object -Maximum).Maximum
    $confianzaAvg = ($confianzas | Measure-Object -Average).Average
    $confianzaUnicos = ($confianzas | Sort-Object -Unique).Count
    
    Write-Host "Análisis de confianza:" -ForegroundColor Yellow
    Write-Host "  Mínima    : $($confianzaMin.ToString("F3"))" -ForegroundColor Cyan
    Write-Host "  Máxima    : $($confianzaMax.ToString("F3"))" -ForegroundColor Cyan
    Write-Host "  Promedio  : $($confianzaAvg.ToString("F3"))" -ForegroundColor Cyan
    Write-Host "  Valores únicos: $confianzaUnicos de $($confianzas.Count)" -ForegroundColor Cyan
    Write-Host ""
    
    # Validación de variabilidad: al menos 2 valores distintos
    $hayVariabilidad = $confianzaUnicos -ge 2
    
    if ($hayVariabilidad) {
        Write-Host "  [✓] VALIDACIÓN VARIABILIDAD: Detectada variabilidad en confianzas (no siempre 0.9)" -ForegroundColor Green
    } else {
        Write-Host "  [✗] VALIDACIÓN VARIABILIDAD: Todos los valores son iguales ($($confianzaMin.ToString("F3")))" -ForegroundColor Red
        Write-Host "      ⚠ POSIBLE PROBLEMA: GPT no está reportando confianza dinámica" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Resultado final
$exito = ($totalInvalidos -eq 0 -and $hayVariabilidad)

if ($exito) {
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "  ✓ TEST PASADO: Confianza self-reported funcionando correctamente" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    exit 0
} else {
    Write-Host "=================================================================" -ForegroundColor Red
    Write-Host "  ✗ TEST FALLIDO: Ver detalles arriba" -ForegroundColor Red
    Write-Host "=================================================================" -ForegroundColor Red
    exit 1
}
