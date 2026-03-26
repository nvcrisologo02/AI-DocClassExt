# Script de prueba para invocar la funcion con la tipologia Resumen Documental
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$endpoint = "http://localhost:7071/api/IngestDocument"

$body = @{
    instrucciones = @{
        expectedType = "resumen-documental"
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
        name = "resumen_documental_test_001.pdf"
        content = @{
            # Placeholder de contenido para pruebas locales. Sustituir por un PDF real en base64.
            base64 = "SGVsbG8gd29ybGQh"
        }
    }
    trazabilidad = @{
        correlationId = "RESUMEN-DOC-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        submittedBy = "usuario.prueba@sareb.es"
        idGDC = $null
        idActivo = "RES-001-2026"
    }
} | ConvertTo-Json -Depth 10

Write-Host ""
Write-Host "========================================"
Write-Host "  Prueba Resumen Documental"
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

    $statusUri = $response.statusQueryUri
    if ($statusUri -match "http://localhost/") {
        $statusUri = $statusUri -replace "http://localhost/", "http://localhost:7071/"
    }

    Write-Host "Status URI     : $statusUri"

    $response.instanceId | Out-File "last-instance-id-resumen-documental.txt" -Encoding UTF8
    Write-Host ""
    Write-Host "[OK] Instance ID guardado en last-instance-id-resumen-documental.txt"

    Write-Host ""
    Write-Host "========================================"
    Write-Host "  Esperando a que complete..."
    Write-Host "========================================"
    Write-Host ""

    $maxRetries = 30
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

        if ($status.output) {
            Write-Host "--- Resumen ---"
            Write-Host "Documento      : $($status.output.Identificacion.Documento)"
            Write-Host "Tipologia      : $($status.output.Identificacion.Tipologia)"
            Write-Host "Estado         : $($status.output.Resultado.Estado)"
            Write-Host "Confianza      : $($status.output.Resultado.ConfianzaGlobal)"
            Write-Host ""

            if ($status.output.DetalleEjecucion -and $status.output.DetalleEjecucion.Prompt) {
                Write-Host "--- Ejecucion Prompt ---"
                Write-Host "Modelo               : $($status.output.DetalleEjecucion.Prompt.Modelo)"
                Write-Host "TiempoMs             : $($status.output.DetalleEjecucion.Prompt.TiempoMs)"
                Write-Host "CombinedWithFallback : $($status.output.DetalleEjecucion.Prompt.CombinedWithFallback)"
                Write-Host "Error                : $($status.output.DetalleEjecucion.Prompt.Error)"
                Write-Host ""
            }

            if ($status.output.ResultadoPrompt) {
                Write-Host "--- Resultado Prompt (texto) ---"
                Write-Host "Modelo               : $($status.output.ResultadoPrompt.Modelo)"
                Write-Host "TiempoMs             : $($status.output.ResultadoPrompt.TiempoMs)"
                Write-Host "CombinedWithFallback : $($status.output.ResultadoPrompt.CombinedWithFallback)"
                Write-Host "Error                : $($status.output.ResultadoPrompt.Error)"
                Write-Host ""
                Write-Host "Respuesta:"
                Write-Host "----------------------------------------"
                Write-Host $status.output.ResultadoPrompt.Resultado
                Write-Host "----------------------------------------"
                Write-Host ""
            } else {
                Write-Host "[WARN] No se recibio ResultadoPrompt en la salida."
                Write-Host ""
            }
        }

        Write-Host "--- Output completo ---"
        $status.output | ConvertTo-Json -Depth 12
        Write-Host ""

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
        Write-Host "  .\check-status.ps1 -InstanceId $($response.instanceId)"
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
