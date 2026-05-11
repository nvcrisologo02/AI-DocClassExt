# Script de prueba para invocar la funcion con Nota Simple 1.4
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8


$endpoint = "http://localhost:7071/api/IngestDocument"


$body = @{
    instrucciones = @{
        expectedType = "nota.simple.1_4"
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
        name = "NT_notasimple14_001.pdf"
        content = @{
            base64 = "SGVsbG8gd29ybGQh"
        }
    }
    trazabilidad = @{
        correlationId = "NOTASIMPLE14-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        submittedBy = "usuario.prueba@sareb.es"
        idGDC = $null
        idActivo = "NT-14-001-2026"
    }
} | ConvertTo-Json -Depth 10


Write-Host ""
Write-Host "========================================"
Write-Host "  Prueba Nota Simple 1.4"
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
    $response.instanceId | Out-File "last-instance-id-notasimple14.txt" -Encoding UTF8
    Write-Host ""
    Write-Host "[OK] Instance ID guardado en last-instance-id-notasimple14.txt"


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
        
        # Mostrar resumen específico para Nota Simple 1.4
        if ($status.output.Identificacion) {
            Write-Host "========================================"
            Write-Host "  RESUMEN NOTA SIMPLE 1.4"
            Write-Host "========================================"
            Write-Host "Documento      : $($status.output.Identificacion.Documento)"
            Write-Host "Tipologia      : $($status.output.Identificacion.Tipologia)"
            Write-Host "Estado         : $($status.output.Resultado.Estado)"
            Write-Host "Confianza      : $($status.output.Resultado.ConfianzaGlobal)"
            Write-Host "SHA256         : $($status.output.Integridad.SHA256)"
            Write-Host ""
            
            # Si hay datos extraídos, mostrar informacion específica de Nota Simple 1.4
            if ($status.output.DatosExtraidos) {
                Write-Host "--- Datos Extraidos ---"
                Write-Host "Finca Registral      : $($status.output.DatosExtraidos.FincaRegistral)"
                Write-Host "Registro Propiedad   : $($status.output.DatosExtraidos.RegistroPropiedad)"
                Write-Host "Municipio Registro   : $($status.output.DatosExtraidos.MunicipioRegistro)"
                Write-Host "Registrador          : $($status.output.DatosExtraidos.Registrador)"
                Write-Host "Fecha Documento      : $($status.output.DatosExtraidos.FechaDocumento)"
                Write-Host "Direccion            : $($status.output.DatosExtraidos.Direccion)"
                Write-Host "Referencia Catastral : $($status.output.DatosExtraidos.ReferenciaCatastral)"
                Write-Host "Tipologia Inmueble   : $($status.output.DatosExtraidos.TipologiaInmueble)"
                if ($status.output.DatosExtraidos.superficies -and $status.output.DatosExtraidos.superficies.Count -gt 0) {
                    Write-Host "Superficies          :"
                    foreach ($superficie in $status.output.DatosExtraidos.superficies) {
                        Write-Host "  - $($superficie.valor) $($superficie.UnidadSuperficie)"
                    }
                } else {
                    Write-Host "Superficies          : (sin datos)"
                }
                Write-Host "Titular              : $($status.output.DatosExtraidos.Titular)"
                Write-Host "NIF                  : $($status.output.DatosExtraidos.NIF)"
                Write-Host "Cuota Participacion  : $($status.output.DatosExtraidos.CuotaParticipacion)"
                Write-Host "Ocupacion            : $($status.output.DatosExtraidos.Ocupacion)"
                Write-Host ""
                
                # Mostrar Anejos si existen
                if ($status.output.DatosExtraidos.Anejos -and $status.output.DatosExtraidos.Anejos.Count -gt 0) {
                    Write-Host "--- Anejos ---"
                    foreach ($idx in 0..($status.output.DatosExtraidos.Anejos.Count - 1)) {
                        $anejo = $status.output.DatosExtraidos.Anejos[$idx]
                        Write-Host "  Anejo $($idx + 1):"
                        Write-Host "    Tipo        : $($anejo.tipo)"
                        Write-Host "    Descripcion : $($anejo.descripcion)"
                        Write-Host "    Superficie  : $($anejo.superficie)"
                    }
                    Write-Host ""
                }
                
                # Mostrar Cargas si existen
                if ($status.output.DatosExtraidos.Cargas -and $status.output.DatosExtraidos.Cargas.Count -gt 0) {
                    Write-Host "--- Cargas ---"
                    foreach ($idx in 0..($status.output.DatosExtraidos.Cargas.Count - 1)) {
                        $carga = $status.output.DatosExtraidos.Cargas[$idx]
                        Write-Host "  Carga $($idx + 1):"
                        Write-Host "    Tipo                    : $($carga.tipo)"
                        Write-Host "    Descripcion             : $($carga.descripcion)"
                        Write-Host "    Importe Max Responsabil.: $($carga.importeMaxResponsabilidad)"
                        Write-Host "    Fecha Inscripcion       : $($carga.fechaInscripcion)"
                        Write-Host "    Acreedor                : $($carga.acreedor)"
                    }
                    Write-Host ""
                }
            }
            
            Write-Host "Modelo Clasif.       : $($status.output.DetalleEjecucion.Clasificacion.Modelo)"
            Write-Host "Confianza Cls.       : $($status.output.DetalleEjecucion.Clasificacion.Confianza)"
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