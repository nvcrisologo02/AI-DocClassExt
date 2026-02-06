# Script de prueba para invocar la funcion
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8


$endpoint = "http://localhost:7071/api/IngestDocument"


$body = @{
    instrucciones = @{
        expectedType = "Tasacion"
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
        name = "tasacion_test_001.pdf"
        content = @{
            base64 = "SGVsbG8gd29ybGQh"
        }
    }
    trazabilidad = @{
        correlationId = "TEST-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
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
    Write-Host "--- Informacion Inicial ---"
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


    # Esperar y consultar estado hasta que complete
    Write-Host ""
    Write-Host "========================================"
    Write-Host "  Esperando a que complete..."
    Write-Host "========================================"
    Write-Host ""
    
    $maxRetries = 20
    $retryCount = 0
    $delaySeconds = 2
    $status = $null


    do {
        Start-Sleep -Seconds $delaySeconds
        
        try {
            $status = Invoke-RestMethod -Uri $statusUri -Method Get
            $retryCount++
            
            $statusEmoji = switch ($status.runtimeStatus) {
                "Running" { "[>]" }
                "Pending" { "[~]" }
                "Completed" { "[OK]" }
                "Failed" { "[X]" }
                default { "[*]" }
            }
            
            Write-Host "[$retryCount/$maxRetries] $statusEmoji Estado: $($status.runtimeStatus)" -NoNewline
            
            if ($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") {
                Write-Host " - Esperando $delaySeconds segundos..."
            } else {
                Write-Host ""
            }
            
        } catch {
            Write-Host "[$retryCount/$maxRetries] Error consultando estado, reintentando..."
        }
        
    } while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retryCount -lt $maxRetries)


    Write-Host ""
    Write-Host "========================================"
    
    if ($status.runtimeStatus -eq "Completed") {
        Write-Host "  [OK] PROCESAMIENTO COMPLETADO"
        Write-Host "========================================"
        Write-Host ""
        Write-Host "--- Estado Final ---"
        Write-Host "Runtime Status : $($status.runtimeStatus)"
        Write-Host "Created Time   : $($status.createdTime)"
        Write-Host "Last Updated   : $($status.lastUpdatedTime)"
        Write-Host ""
        Write-Host "--- Resultado del Procesamiento ---"
        $status.output | ConvertTo-Json -Depth 10
        Write-Host ""
        
        # Mostrar resumen
        if ($status.output.Identificacion) {
            Write-Host "========================================"
            Write-Host "  RESUMEN"
            Write-Host "========================================"
            Write-Host "Documento      : $($status.output.Identificacion.Documento)"
            Write-Host "Tipologia      : $($status.output.Identificacion.Tipologia)"
            Write-Host "Estado         : $($status.output.Resultado.Estado)"
            Write-Host "Confianza      : $($status.output.Resultado.ConfianzaGlobal)"
            Write-Host "SHA256         : $($status.output.Integridad.SHA256)"
            Write-Host "Modelo Clasif. : $($status.output.DetalleEjecucion.Clasificacion.Modelo)"
            Write-Host "Confianza Cls. : $($status.output.DetalleEjecucion.Clasificacion.Confianza)"
            Write-Host ""
        }
        
    } elseif ($status.runtimeStatus -eq "Failed") {
        Write-Host "  [ERROR] PROCESAMIENTO FALLIDO"
        Write-Host "========================================"
        Write-Host ""
        Write-Host "Error: $($status.output)"
        Write-Host ""
        
    } else {
        Write-Host "  [TIMEOUT] TIMEOUT"
        Write-Host "========================================"
        Write-Host ""
        Write-Host "El procesamiento aun esta en curso despues de $($maxRetries * $delaySeconds) segundos."
        Write-Host "Estado actual: $($status.runtimeStatus)"
        Write-Host ""
        Write-Host "Puedes consultar el estado manualmente con:"
        Write-Host "  .\check-status.ps1"
        Write-Host ""
    }


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
