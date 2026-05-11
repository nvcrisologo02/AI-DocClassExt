# Test del sistema de plugins con documento de prueba

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TEST DE INTEGRACION DE PLUGINS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$endpoint = "http://localhost:7071/api/IngestDocument"

# Documento de prueba para tipologia tasacion
$testPayload = @{
    instrucciones = @{
        expectedType = "nota.simple.1_3"
        skipDuplicateCheck = $true
        forceReprocess = $true
        classification = @{
            model = "auto"
            umbral = 0.85
        }
        extraction = @{
            model = "auto"
            umbral = 0.80
        }
    }
    documento = @{
        name = "tasacion-test-plugins-001.pdf"
        content = @{
            base64 = "JVBERi0xLjQKJeLjz9MKMSAwIG9iajw8L1R5cGUvQ2F0YWxvZy9QYWdlcyAyIDAgUj4+ZW5kb2JqCjIgMCBvYmo8PC9UeXBlL1BhZ2VzL0NvdW50IDEvS2lkc1szIDAgUl0+PmVuZG9iagozIDAgb2JqPDwvVHlwZS9QYWdlL01lZGlhQm94WzAgMCA2MTIgNzkyXS9QYXJlbnQgMiAwIFIvUmVzb3VyY2VzPDw+Pj4+ZW5kb2JqCnhyZWYKMCA0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAxNSAwMDAwMCBuIAowMDAwMDAwMDY0IDAwMDAwIG4gCjAwMDAwMDAxMjEgMDAwMDAgbiAKdHJhaWxlcjw8L1NpemUgNC9Sb290IDEgMCBSPj4Kc3RhcnR4cmVmCjE5NAolJUVPRg=="
        }
    }
    trazabilidad = @{
        correlationId = [Guid]::NewGuid().ToString()
        submittedBy = "test-script-plugins"
        idGDC = "TEST-PLUGINS-001"
        idActivo = "ACT-TEST-001"
    }
} | ConvertTo-Json -Depth 10

Write-Host "Enviando documento de prueba..." -ForegroundColor Yellow
Write-Host "Endpoint: $endpoint" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $testPayload -ContentType "application/json"
    
    Write-Host "[OK] Documento enviado correctamente" -ForegroundColor Green
    Write-Host ""
    Write-Host "Instance ID: $($response.instanceId)" -ForegroundColor White
    Write-Host "Correlation ID: $($response.correlationId)" -ForegroundColor White
    Write-Host ""
    
    if ($response.statusQueryUri) {
        Write-Host "Consultando estado de la orquestacion..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
        
        $maxRetries = 10
        $retryCount = 0
        
        while ($retryCount -lt $maxRetries) {
            $status = Invoke-RestMethod -Uri $response.statusQueryUri -Method Get
            
            Write-Host "[$($retryCount + 1)/$maxRetries] Estado: $($status.runtimeStatus)" -ForegroundColor Cyan
            
            if ($status.runtimeStatus -eq "Completed") {
                Write-Host "`n[OK] Orquestacion completada exitosamente" -ForegroundColor Green
                Write-Host ""
                Write-Host "Resultado:" -ForegroundColor Yellow
                $status.output | ConvertTo-Json -Depth 10
                
                # Verificar si se ejecutaron plugins
                if ($status.output.detalleEjecucion.integracion) {
                    Write-Host "`nPlugins ejecutados:" -ForegroundColor Yellow
                    $status.output.detalleEjecucion.integracion | ConvertTo-Json -Depth 5
                }
                
                break
            }
            elseif ($status.runtimeStatus -eq "Failed") {
                Write-Host "`n[ERROR] Orquestacion fallo" -ForegroundColor Red
                Write-Host "Error: $($status.output)" -ForegroundColor Red
                break
            }
            
            Start-Sleep -Seconds 3
            $retryCount++
        }
        
        if ($retryCount -eq $maxRetries) {
            Write-Host "`n[TIMEOUT] La orquestacion no completo en el tiempo esperado" -ForegroundColor Yellow
        }
    }
    
} catch {
    Write-Host "[ERROR] Fallo la solicitud" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "`nDetalle del error:" -ForegroundColor Yellow
        Write-Host $errorBody -ForegroundColor Gray
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  FIN DEL TEST" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
