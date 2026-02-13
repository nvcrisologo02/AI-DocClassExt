# Test del sistema completo de 3 plugins

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TEST SISTEMA MULTI-PLUGIN" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Verificar que los servidores mock estan corriendo
# Verificar que los servidores mock estan corriendo
Write-Host "Verificando servidores mock..." -ForegroundColor Yellow

$restOk = $false
$soapOk = $false

try {
    # Cambiar a llamar directamente a la raiz
    $response = Invoke-WebRequest -Uri "http://localhost:8080" -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
    $restOk = $true
    Write-Host "  [OK] Mock REST Server (puerto 8080)" -ForegroundColor Green
} catch {
    # Si falla con GET, intentar verificar que el puerto esta escuchando
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.Connect("localhost", 8080)
        $tcpClient.Close()
        $restOk = $true
        Write-Host "  [OK] Mock REST Server (puerto 8080)" -ForegroundColor Green
    } catch {
        Write-Host "  [ERROR] Mock REST Server no responde (puerto 8080)" -ForegroundColor Red
        Write-Host "  Ejecuta: python scripts\mock-enrichment-server.py" -ForegroundColor Yellow
    }
}

try {
    # Test simple de conectividad TCP para SOAP
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("localhost", 8081)
    $tcpClient.Close()
    $soapOk = $true
    Write-Host "  [OK] Mock SOAP Server (puerto 8081)" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Mock SOAP Server no responde (puerto 8081)" -ForegroundColor Red
    Write-Host "  Ejecuta: python scripts\mock-soap-server.py" -ForegroundColor Yellow
}

if (-not ($restOk -and $soapOk)) {
    Write-Host "`n[ADVERTENCIA] Algunos servidores no responden, pero continuando..." -ForegroundColor Yellow
    # No salir, continuar de todas formas
}


# Verificar DLL custom
Write-Host "`nVerificando enriquecedor custom..." -ForegroundColor Yellow
$dllPath = "C:\temp\MVP\documento-ia-clasificacion-mvp\plugins\SarebEnrichments.dll"
if (Test-Path $dllPath) {
    Write-Host "  [OK] SarebEnrichments.dll encontrado" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] SarebEnrichments.dll no encontrado" -ForegroundColor Red
    Write-Host "  Ejecuta: .\scripts\compile-all-plugins.ps1" -ForegroundColor Yellow
    exit 1
}

# Payload de test
Write-Host "`nEnviando documento de prueba..." -ForegroundColor Yellow

$endpoint = "http://localhost:7271/api/IngestDocument"

$payload = @{
    instrucciones = @{
        expectedType = "nota.simple.1_3"
        skipDuplicateCheck = $true
        forceReprocess = $false
    }
    documento = @{
        name = "nota-simple-test-multi-plugin.pdf"
        content = @{
            base64 = "JVBERi0xLjQKJeLjz9MKMSAwIG9iajw8L1R5cGUvQ2F0YWxvZy9QYWdlcyAyIDAgUj4+ZW5kb2JqCjIgMCBvYmo8PC9UeXBlL1BhZ2VzL0NvdW50IDEvS2lkc1szIDAgUl0+PmVuZG9iagozIDAgb2JqPDwvVHlwZS9QYWdlL01lZGlhQm94WzAgMCA2MTIgNzkyXS9QYXJlbnQgMiAwIFIvUmVzb3VyY2VzPDw+Pj4+ZW5kb2JqCnhyZWYKMCA0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAxNSAwMDAwMCBuIAowMDAwMDAwMDY0IDAwMDAwIG4gCjAwMDAwMDAxMjEgMDAwMDAgbiAKdHJhaWxlcjw8L1NpemUgNC9Sb290IDEgMCBSPj4Kc3RhcnR4cmVmCjE5NAolJUVPRg=="
        }
    }
    trazabilidad = @{
        correlationId = [Guid]::NewGuid().ToString()
        submittedBy = "test-multi-plugin"
        idGDC = "TEST-MULTI-001"
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $payload -ContentType "application/json"
    
    Write-Host "  [OK] Documento enviado" -ForegroundColor Green
    Write-Host "  Instance ID: $($response.instanceId)" -ForegroundColor Gray
    
    # Debuggear la respuesta
    Write-Host "`nDetalles de respuesta:" -ForegroundColor Gray
    Write-Host ($response | ConvertTo-Json -Depth 3) -ForegroundColor Gray
    
    # Construir status URI correctamente
    $instanceId = $response.instanceId
    $statusUri = "http://localhost:7271/runtime/webhooks/durabletask/instances/$instanceId"
    
    if ($instanceId) {
        Write-Host "`nMonitoreando ejecucion..." -ForegroundColor Yellow
        Write-Host "Status URI: $statusUri" -ForegroundColor Gray
        
        $maxRetries = 30
        $retryCount = 0
        $completed = $false
        
        while ($retryCount -lt $maxRetries -and -not $completed) {
            Start-Sleep -Seconds 2
            
            try {
                $status = Invoke-RestMethod -Uri $statusUri -Method Get -ErrorAction Stop
                Write-Host "  [$($retryCount + 1)/$maxRetries] Estado: $($status.runtimeStatus)" -ForegroundColor Cyan
                
                if ($status.runtimeStatus -eq "Completed") {
                    $completed = $true
                    Write-Host "`n========================================" -ForegroundColor Green
                    Write-Host "  ORQUESTACION COMPLETADA" -ForegroundColor Green
                    Write-Host "========================================`n" -ForegroundColor Green
                    
                    # Mostrar resultado completo
                    Write-Host "Resultado completo:" -ForegroundColor White
                    Write-Host ($status.output | ConvertTo-Json -Depth 5) -ForegroundColor Green
                    
                    # Analizar si hay integracion
                    if ($status.output.detalleEjecucion.integracion) {
                        $integracion = $status.output.detalleEjecucion.integracion
                        
                        Write-Host "`n========================================" -ForegroundColor Cyan
                        Write-Host "DETALLES DE INTEGRACION" -ForegroundColor Cyan
                        Write-Host "========================================`n" -ForegroundColor Cyan
                        
                        Write-Host "Estado Integracion: $($integracion.Estado)" -ForegroundColor White
                        Write-Host "Mensaje: $($integracion.Mensaje)" -ForegroundColor White
                        Write-Host "Plugins ejecutados: $($integracion.Plugins.Count)" -ForegroundColor White
                        Write-Host ""
                        
                        foreach ($plugin in $integracion.Plugins) {
                            $color = if ($plugin.Success) { "Green" } else { "Red" }
                            $statusText = if ($plugin.Success) { "OK" } else { "ERROR" }
                            
                            Write-Host "[$statusText] Plugin: $($plugin.PluginKey)" -ForegroundColor $color
                            Write-Host "    Prioridad: $($plugin.Priority)" -ForegroundColor Gray
                            Write-Host "    Mensaje: $($plugin.Mensaje)" -ForegroundColor Gray
                            Write-Host "    Duracion: $($plugin.DurationMs)ms" -ForegroundColor Gray
                            
                            if ($plugin.DatosEnriquecidos) {
                                $camposCount = ($plugin.DatosEnriquecidos | Get-Member -MemberType NoteProperty).Count
                                Write-Host "    Campos devueltos: $camposCount" -ForegroundColor Gray
                            }
                            
                            if ($plugin.Error) {
                                Write-Host "    Error: $($plugin.Error)" -ForegroundColor Red
                            }
                            Write-Host ""
                        }
                    }
                }
                elseif ($status.runtimeStatus -eq "Failed") {
                    $completed = $true
                    Write-Host "`n[ERROR] Orquestacion fallo" -ForegroundColor Red
                    Write-Host ($status.output | ConvertTo-Json -Depth 5) -ForegroundColor Red
                    break
                }
                elseif ($status.runtimeStatus -eq "Pending") {
                    Write-Host "  [$($retryCount + 1)/$maxRetries] Estado: PENDIENTE..." -ForegroundColor Yellow
                }
            }
            catch {
                Write-Host "  [$($retryCount + 1)/$maxRetries] Esperando... (Error temporal)" -ForegroundColor Yellow
            }
            
            $retryCount++
        }
        
        if (-not $completed) {
            Write-Host "`n[TIMEOUT] La orquestacion no completo en 60 segundos ($maxRetries intentos)" -ForegroundColor Yellow
            Write-Host "Verifica que:" -ForegroundColor Yellow
            Write-Host "  1. Azure Functions esta ejecutandose: func host start" -ForegroundColor Yellow
            Write-Host "  2. Los servidores mock estan activos (puerto 8080 y 8081)" -ForegroundColor Yellow
            Write-Host "  3. Instance ID: $instanceId" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "`n[ERROR] No se recibio Instance ID" -ForegroundColor Red
    }
    
} catch {
    Write-Host "`n[ERROR] Fallo la solicitud inicial" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $responseBody = $reader.ReadToEnd()
        Write-Host "Respuesta servidor: $responseBody" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  FIN DEL TEST" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
