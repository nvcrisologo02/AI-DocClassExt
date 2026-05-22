# test-ingest-blob-first.ps1
#
# Prueba el nuevo path Blob-First: envía el PDF como multipart/form-data
# (igual que hace el cliente WPF para ficheros grandes).
#
# INSTRUCCIONES DE USO:
#   .\test-ingest-blob-first.ps1 -DocumentPath "C:\temp\docs\ns01.pdf"
#   .\test-ingest-blob-first.ps1 -DocumentPath "C:\temp\docs\ns01.pdf" -ExpectedType "nota-simple@1.4" -Endpoint "http://localhost:7071"
#   .\test-ingest-blob-first.ps1 -DocumentPath "C:\temp\docs\ns01.pdf" -ForceReprocess
#
# DIFERENCIA CON test-ingest-notasimple1-4-from-path.ps1:
#   Este script envía multipart/form-data (file + metadata) en lugar de JSON+base64.
#   El servidor sube el blob en el Trigger antes de iniciar la orquestación,
#   y los providers usan SAS URL en lugar de base64. Esto soporta ficheros >25 MB.

param(
    [Parameter(Mandatory = $true)]
    [string]$DocumentPath,

    [Parameter(Mandatory = $false)]
    [string]$ExpectedType = "nota-simple@1.4",

    [Parameter(Mandatory = $false)]
    [string]$Endpoint = "http://localhost:7071",

    [Parameter(Mandatory = $false)]
    [string]$FunctionKey = "",

    [Parameter(Mandatory = $false)]
    [switch]$SkipDuplicateCheck,

    [Parameter(Mandatory = $false)]
    [switch]$ForceReprocess,

    [Parameter(Mandatory = $false)]
    [int]$MaxRetries = 40,

    [Parameter(Mandatory = $false)]
    [int]$DelaySeconds = 3
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

# --- Validar fichero ---
if (-not [System.IO.Path]::IsPathRooted($DocumentPath)) {
    throw "El parametro -DocumentPath debe ser una ruta absoluta. Valor: '$DocumentPath'"
}
try {
    $resolvedPath = (Resolve-Path -Path $DocumentPath -ErrorAction Stop).Path
} catch {
    throw "No se encontro el fichero: '$DocumentPath'"
}
$documentName = [System.IO.Path]::GetFileName($resolvedPath)
$fileSizeMB = [math]::Round((Get-Item $resolvedPath).Length / 1MB, 2)

# --- Construir metadata JSON (sin base64) ---
$correlationId = "BLOBFIRST-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$metadataObj = @{
    instrucciones = @{
        expectedType       = $ExpectedType
        skipDuplicateCheck = [bool]$SkipDuplicateCheck
        forceReprocess     = [bool]$ForceReprocess
        skipGDCUpload      = $false
        classification     = @{ provider = "auto"; model = "auto" }
        extraction         = @{ provider = "auto"; model = "auto" }
    }
    documento = @{
        name    = $documentName
        content = @{ base64 = "" }
    }
    trazabilidad = @{
        correlationId = $correlationId
        submittedBy   = "test-blob-first@sareb.es"
        idGDC         = $null
        idActivo      = $null
    }
}
$metadataJson = $metadataObj | ConvertTo-Json -Depth 10 -Compress

# --- Construir endpoint (prueba /api/IngestDocument, igual que los otros scripts) ---
$ingestUrl = $Endpoint.TrimEnd('/') + "/api/IngestDocument"

Write-Host ""
Write-Host "========================================"
Write-Host "  Test Blob-First (multipart/form-data)"
Write-Host "========================================"
Write-Host "Endpoint       : $ingestUrl"
Write-Host "Documento      : $resolvedPath"
Write-Host "Tamaño         : $fileSizeMB MB"
Write-Host "ExpectedType   : $ExpectedType"
Write-Host "CorrelationId  : $correlationId"
Write-Host ""

# --- Enviar como multipart usando .NET HttpClient ---
Write-Host "Enviando multipart/form-data..."
$httpClient = [System.Net.Http.HttpClient]::new()

if (-not [string]::IsNullOrWhiteSpace($FunctionKey)) {
    $httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-functions-key", $FunctionKey) | Out-Null
}

$multipart = [System.Net.Http.MultipartFormDataContent]::new()

# Parte metadata (JSON)
$metadataContent = [System.Net.Http.StringContent]::new(
    $metadataJson,
    [System.Text.Encoding]::UTF8,
    "application/json"
)
$multipart.Add($metadataContent, "metadata")

# Parte file (PDF binario)
$fileStream = [System.IO.FileStream]::new($resolvedPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
$fileContent = [System.Net.Http.StreamContent]::new($fileStream)
$fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new("application/pdf")
$multipart.Add($fileContent, "file", $documentName)

try {
    $response = $httpClient.PostAsync($ingestUrl, $multipart).GetAwaiter().GetResult()
    $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    if (-not $response.IsSuccessStatusCode) {
        Write-Host "[ERROR] HTTP $([int]$response.StatusCode) $($response.ReasonPhrase)"
        Write-Host $responseBody
        exit 1
    }

    $ingestResult = $responseBody | ConvertFrom-Json

    Write-Host ""
    Write-Host "[OK] Respuesta recibida (HTTP $([int]$response.StatusCode))"
    Write-Host ""
    Write-Host "--- Respuesta Inicial ---"
    Write-Host "Instance ID    : $($ingestResult.instanceId)"
    Write-Host "Correlation ID : $($ingestResult.correlationId)"

    $statusUri = $ingestResult.statusQueryUri
    if ($statusUri -match "http://localhost/") {
        $statusUri = $statusUri -replace "http://localhost/", "http://localhost:7071/"
    }
    Write-Host "Status URI     : $statusUri"

    # Guardar instance ID
    $ingestResult.instanceId | Out-File "last-instance-id.txt" -Encoding UTF8
    Write-Host "[OK] Instance ID guardado en last-instance-id.txt"

} finally {
    $fileStream.Dispose()
    $httpClient.Dispose()
}

# --- Polling de estado ---
Write-Host ""
Write-Host "========================================"
Write-Host "  Esperando resultado..."
Write-Host "========================================"
Write-Host ""

$retryCount = 0
$status = $null

do {
    Start-Sleep -Seconds $DelaySeconds

    try {
        $status = Invoke-RestMethod -Uri $statusUri -Method Get -ErrorAction Stop
        $retryCount++

        $icon = switch ($status.runtimeStatus) {
            "Running" { "[>]" }
            "Pending" { "[~]" }
            "Completed" { "[OK]" }
            "Failed" { "[X]" }
            default { "[*]" }
        }

        Write-Host "[$retryCount/$MaxRetries] $icon $($status.runtimeStatus)" -NoNewline

        if ($status.customStatus -and $status.customStatus.Actividad) {
            Write-Host " | $($status.customStatus.Actividad)" -NoNewline
        }

        if ($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") {
            Write-Host " - esperando $DelaySeconds s..."
        }
        else {
            Write-Host ""
        }
    }
    catch {
        Write-Host "[$retryCount/$MaxRetries] Error consultando estado: $($_.Exception.Message)"
    }

} while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retryCount -lt $MaxRetries)

Write-Host ""
Write-Host "========================================"

if ($status.runtimeStatus -eq "Completed") {
    Write-Host "  [OK] PROCESAMIENTO COMPLETADO"
    Write-Host "========================================"
    Write-Host ""
    Write-Host "Creado    : $($status.createdTime)"
    Write-Host "Actualiz. : $($status.lastUpdatedTime)"
    Write-Host ""

    $out = $status.output
    if ($out) {
        Write-Host "--- Identificacion ---"
        Write-Host "Documento   : $($out.Identificacion.Documento)"
        Write-Host "Tipologia   : $($out.Identificacion.Tipologia)"
        Write-Host "Paginas     : $($out.Identificacion.Paginas)"
        Write-Host ""
        Write-Host "--- Resultado ---"
        Write-Host "Estado      : $($out.Resultado.Estado)"
        Write-Host "Confianza   : $($out.Resultado.ConfianzaGlobal)"
        Write-Host ""
        Write-Host "--- Integridad ---"
        Write-Host "SHA256         : $($out.Integridad.SHA256)"
        Write-Host "MD5            : $($out.Integridad.MD5)"
        $tamanoBytes = $null
        if ($out.Integridad.PSObject.Properties.Name -contains 'TamanoBytes') {
            $tamanoBytes = $out.Integridad.TamanoBytes
        }
        elseif ($out.Integridad.PSObject.Properties.Name -contains 'TamañoBytes') {
            $tamanoBytes = $out.Integridad.'TamañoBytes'
        }
        Write-Host "Tamano bytes   : $tamanoBytes"
        Write-Host "RutaBlobStorage: $($out.Integridad.RutaBlobStorage)"
        Write-Host ""

        if ($out.DatosExtraidos -and ($out.DatosExtraidos | Get-Member -MemberType NoteProperty).Count -gt 0) {
            Write-Host "--- Datos Extraidos ---"
            $out.DatosExtraidos | ConvertTo-Json -Depth 5
        }
    }

    Write-Host ""
    Write-Host "[OK] Test blob-first completado correctamente."
}
elseif ($status.runtimeStatus -eq "Failed") {
    Write-Host "  [X] PROCESAMIENTO FALLIDO"
    Write-Host "========================================"
    Write-Host ""
    Write-Host "Output:"
    $status.output | ConvertTo-Json -Depth 10
    exit 1
}
else {
    Write-Host "  [?] TIMEOUT - estado final: $($status.runtimeStatus)"
    Write-Host "========================================"
    Write-Host "Status URI para consulta manual: $statusUri"
    exit 2
}
