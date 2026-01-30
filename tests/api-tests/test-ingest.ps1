# Script de prueba para invocar la función
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$endpoint = "http://localhost:7071/api/IngestDocument"

$body = @{
    instrucciones = @{
        expectedType = "Tasacion"
        skipDuplicateCheck = $false
        forceReprocess = $false
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
        name = "tasacion_test_001.pdf"
        content = @{
            base64 = "JVBERi0xLjQKJeLjz9MKMSAwIG9iago8PC9UeXBlL0NhdGFsb2cvUGFnZXMgMiAwIFI+PgplbmRvYmoKMiAwIG9iago8PC9UeXBlL1BhZ2VzL0NvdW50IDEvS2lkc1szIDAgUl0+PgplbmRvYmoKMyAwIG9iago8PC9UeXBlL1BhZ2UvTWVkaWFCb3hbMCAwIDYxMiA3OTJdL1BhcmVudCAyIDAgUi9SZXNvdXJjZXM8PD4+Pj4KZW5kb2JqCnhyZWYKMCA0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAxNSAwMDAwMCBuIAowMDAwMDAwMDY0IDAwMDAwIG4gCjAwMDAwMDAxMjEgMDAwMDAgbiAKdHJhaWxlcgo8PC9TaXplIDQvUm9vdCAxIDAgUj4+CnN0YXJ0eHJlZgoyMDIKJSVFT0Y="
        }
    }
    trazabilidad = @{
        correlationId = "TEST-001-2026"
        submittedBy = "usuario.prueba@sareb.es"
        idGDC = $null
        idActivo = "ACT-12345"
    }
} | ConvertTo-Json -Depth 10

Write-Host ""
Write-Host "========================================"
Write-Host "  Probando Azure Functions MVP"
Write-Host "========================================"
Write-Host ""

Write-Host "Enviando request a $endpoint..."

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $body -ContentType "application/json"

    Write-Host ""
    Write-Host "[OK] Respuesta recibida correctamente!"
    Write-Host ""
    Write-Host "--- Detalles de la Orquestacion ---"
    $response | ConvertTo-Json -Depth 10
    
    Write-Host ""
    Write-Host "--- Informacion Clave ---"
    Write-Host "Instance ID    : $($response.instanceId)"
    Write-Host "Correlation ID : $($response.correlationId)"
    
    # Corregir la URL de estado (agregar puerto si falta)
    $statusUri = $response.statusQueryUri
    if ($statusUri -match "http://localhost/") {
        $statusUri = $statusUri -replace "http://localhost/", "http://localhost:7071/"
    }
    
    Write-Host "Status URI     : $statusUri"

    # Guardar instance ID
    $response.instanceId | Out-File "last-instance-id.txt" -Encoding UTF8
    Write-Host ""
    Write-Host "[OK] Instance ID guardado en last-instance-id.txt"

    # Esperar un poco y consultar estado
    Write-Host ""
    Write-Host "Esperando 3 segundos para consultar estado..."
    Start-Sleep -Seconds 3

    Write-Host ""
    Write-Host "Consultando estado de la orquestacion..."
    $status = Invoke-RestMethod -Uri $statusUri -Method Get
    
    Write-Host ""
    Write-Host "--- Estado de la Orquestacion ---"
    Write-Host "Runtime Status : $($status.runtimeStatus)"
    Write-Host "Created Time   : $($status.createdTime)"
    Write-Host "Last Updated   : $($status.lastUpdatedTime)"
    
    Write-Host ""
    Write-Host "--- Output Completo ---"
    $status | ConvertTo-Json -Depth 10

    Write-Host ""
    Write-Host "========================================"
    Write-Host "  Prueba completada exitosamente!"
    Write-Host "========================================"
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "[ERROR] Error durante la prueba:"
    Write-Host $_.Exception.Message
    
    if ($_.ErrorDetails.Message) {
        Write-Host ""
        Write-Host "Detalles:"
        Write-Host $_.ErrorDetails.Message
    }
}
