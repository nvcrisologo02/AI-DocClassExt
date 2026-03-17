# Script de prueba para invocar la funcion con Nota Simple 1.4 usando clasificacion real
# (sin ExpectedType, para que entre en el proveedor de clasificacion configurado)
#
# Uso:
#   .\test-ingest-notasimple1-4-classify.ps1 -DocumentPath "C:\\temp\\docs\\30000876_NS_30000876.pdf"

param(
    [Parameter(Mandatory = $true)]
    [string]$DocumentPath
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$endpoint = "http://localhost:7071/api/IngestDocument"

if (-not [System.IO.Path]::IsPathRooted($DocumentPath)) {
    throw "El parametro -DocumentPath debe ser una ruta absoluta. Valor recibido: '$DocumentPath'"
}

try {
    $resolvedDocumentPath = (Resolve-Path -Path $DocumentPath -ErrorAction Stop).Path
} catch {
    throw "No se encontro el fichero en la ruta indicada: '$DocumentPath'"
}

$documentBytes = [System.IO.File]::ReadAllBytes($resolvedDocumentPath)
$documentBase64 = [System.Convert]::ToBase64String($documentBytes)
$documentName = [System.IO.Path]::GetFileName($resolvedDocumentPath)

$body = @{
    instrucciones = @{
        skipDuplicateCheck = $true
        forceReprocess = $true
        SkipGDCUpload = $true
        classification = @{
            provider = "auto"
            model = "auto"
            umbral = 0.5
        }
        extraction = @{
            model = "auto"
            umbral = 0.80
        }
    }
    documento = @{
        name = $documentName
        content = @{
            base64 = $documentBase64
        }
    }
    trazabilidad = @{
        correlationId = "NOTASIMPLE14-CLASSIFY-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        submittedBy = "usuario.prueba@sareb.es"
        idGDC = $null
        idActivo = "NT-14-001-2026"
    }
} | ConvertTo-Json -Depth 10

Write-Host ""
Write-Host "========================================"
Write-Host "  Prueba Nota Simple 1.4 (Clasificacion Real)"
Write-Host "========================================"
Write-Host ""
Write-Host "Documento local : $resolvedDocumentPath"
Write-Host "Nombre enviado  : $documentName"
Write-Host ""
Write-Host "Enviando request a $endpoint..."

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $body -ContentType "application/json"

    Write-Host ""
    Write-Host "[OK] Respuesta recibida correctamente"
    Write-Host "Instance ID    : $($response.instanceId)"
    Write-Host "Correlation ID : $($response.correlationId)"

    $statusUri = $response.statusQueryUri
    if ($statusUri -match "http://localhost/") {
        $statusUri = $statusUri -replace "http://localhost/", "http://localhost:7071/"
    }

    Write-Host "Status URI     : $statusUri"

    $response.instanceId | Out-File "last-instance-id-notasimple14-classify.txt" -Encoding UTF8
    Write-Host "[OK] Instance ID guardado en last-instance-id-notasimple14-classify.txt"

    Write-Host ""
    Write-Host "Esperando finalizacion..."
    $maxRetries = 30
    $retryCount = 0
    $delaySeconds = 2
    $status = $null

    do {
        Start-Sleep -Seconds $delaySeconds

        try {
            $status = Invoke-RestMethod -Uri $statusUri -Method Get
            $retryCount++
            Write-Host "[$retryCount/$maxRetries] Estado: $($status.runtimeStatus)"
        } catch {
            Write-Host "[$retryCount/$maxRetries] Error consultando estado, reintentando..."
        }

    } while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retryCount -lt $maxRetries)

    Write-Host ""
    if ($status.runtimeStatus -eq "Completed") {
        Write-Host "========================================"
        Write-Host "[OK] PROCESAMIENTO COMPLETADO"
        Write-Host "========================================"

        $output = $status.output
        Write-Host "Tipologia detectada      : $($output.Identificacion.Tipologia)"
        Write-Host "Estado final             : $($output.Resultado.Estado)"
        Write-Host "Modelo clasificacion     : $($output.DetalleEjecucion.Clasificacion.Modelo)"
        Write-Host "Confianza clasificacion  : $($output.DetalleEjecucion.Clasificacion.Confianza)"

        if ($output.DetalleEjecucion.Clasificacion.Modelo -eq "expectedtype-input") {
            Write-Host "[WARN] Se detecto expectedtype-input; revisa que no se este enviando ExpectedType en otro punto."
        }
    }
    elseif ($status.runtimeStatus -eq "Failed") {
        Write-Host "========================================"
        Write-Host "[ERROR] PROCESAMIENTO FALLIDO"
        Write-Host "========================================"
        Write-Host "$($status.output)"
    }
    else {
        Write-Host "========================================"
        Write-Host "[TIMEOUT] El proceso sigue en curso"
        Write-Host "========================================"
        Write-Host "Usa .\check-status.ps1 -InstanceId $($response.instanceId)"
    }
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Fallo al invocar la API: $($_.Exception.Message)"
    exit 1
}
