param(
    [Parameter(Mandatory = $true)]
    [string]$DocumentPath,
    [string]$ExpectedType = "nota.simple.1_4",
    [double]$ExtractionUmbral = 0.80
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$endpoint = "http://localhost:7071/api/IngestDocument"

function Get-FieldValue {
    param(
        [Parameter(Mandatory = $false)]
        [object]$Object,
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    if ($null -eq $Object) {
        return $null
    }

    foreach ($name in $Names) {
        $prop = $Object.PSObject.Properties[$name]
        if ($null -ne $prop) {
            return $prop.Value
        }
    }

    return $null
}

if (-not [System.IO.Path]::IsPathRooted($DocumentPath)) {
    throw "El parametro -DocumentPath debe ser una ruta absoluta. Valor recibido: '$DocumentPath'"
}

try {
    $resolvedDocumentPath = (Resolve-Path -Path $DocumentPath -ErrorAction Stop).Path
}
catch {
    throw "No se encontro el fichero en la ruta indicada: '$DocumentPath'"
}

$documentBytes = [System.IO.File]::ReadAllBytes($resolvedDocumentPath)
$documentBase64 = [System.Convert]::ToBase64String($documentBytes)
$documentName = [System.IO.Path]::GetFileName($resolvedDocumentPath)

$body = @{
    instrucciones = @{
        expectedType = $ExpectedType
        skipDuplicateCheck = $true
        forceReprocess = $true
        skipGDCUpload = $true
        classification = @{
            provider = "auto"
            model = "auto"
            umbral = 0.5
        }
        extraction = @{
            model = "auto"
            umbral = $ExtractionUmbral
        }
    }
    documento = @{
        name = $documentName
        content = @{
            base64 = $documentBase64
        }
    }
    trazabilidad = @{
        correlationId = "EXTRACT-FALLBACK-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        submittedBy = "usuario.prueba@sareb.es"
        idGDC = $null
        idActivo = "354937"
    }
} | ConvertTo-Json -Depth 12

Write-Host ""
Write-Host "========================================"
Write-Host "  Prueba Fallback Extraccion GPT"
Write-Host "========================================"
Write-Host ""
Write-Host "Documento local : $resolvedDocumentPath"
Write-Host "ExpectedType    : $ExpectedType"
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

    $response.instanceId | Out-File "last-instance-id-notasimple14-extract-fallback.txt" -Encoding UTF8
    Write-Host "[OK] Instance ID guardado en last-instance-id-notasimple14-extract-fallback.txt"

    Write-Host ""
    Write-Host "Esperando finalizacion..."
    $maxRetries = 40
    $retryCount = 0
    $delaySeconds = 2
    $status = $null

    do {
        Start-Sleep -Seconds $delaySeconds

        try {
            $status = Invoke-RestMethod -Uri $statusUri -Method Get
            $retryCount++
            Write-Host "[$retryCount/$maxRetries] Estado: $($status.runtimeStatus)"
        }
        catch {
            Write-Host "[$retryCount/$maxRetries] Error consultando estado, reintentando..."
        }

    } while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retryCount -lt $maxRetries)

    Write-Host ""
    if ($status.runtimeStatus -eq "Completed") {
        Write-Host "========================================"
        Write-Host "[OK] PROCESAMIENTO COMPLETADO"
        Write-Host "========================================"

        $output = $status.output
        $extraccion = $output.DetalleEjecucion.Extraccion

        Write-Host "Tipologia                  : $($output.Identificacion.Tipologia)"
        Write-Host "Modelo extraccion          : $($extraccion.Modelo)"
        Write-Host "Fallback extraccion usado  : $($extraccion.FallbackUsado)"
        Write-Host "Fallback extraccion razon  : $($extraccion.FallbackRazon)"

        $seguimiento = $output.DetalleEjecucion.Seguimiento
        if ($seguimiento -and $seguimiento.Actividades) {
            $trazaExtraer = $seguimiento.Actividades | Where-Object { $_.Nombre -eq "Extraer" -or $_.nombre -eq "Extraer" } | Select-Object -First 1
            if ($null -ne $trazaExtraer) {
                $fallbackActivado = Get-FieldValue -Object $trazaExtraer -Names @("FallbackActivado", "fallbackActivado")
                $fallbackRazon = Get-FieldValue -Object $trazaExtraer -Names @("FallbackRazon", "fallbackRazon")
                $mensaje = Get-FieldValue -Object $trazaExtraer -Names @("Mensaje", "mensaje")

                Write-Host ""
                Write-Host "Seguimiento.Extraer.fallbackActivado : $fallbackActivado"
                Write-Host "Seguimiento.Extraer.fallbackRazon    : $fallbackRazon"
                Write-Host "Seguimiento.Extraer.mensaje          : $mensaje"
            }
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
    Write-Host "[ERROR] Fallo en la llamada"
    Write-Host $_.Exception.Message
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
        Write-Host $_.ErrorDetails.Message
    }
    throw
}
