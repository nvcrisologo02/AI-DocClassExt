param(
    [Parameter(Mandatory = $true)]
    [string]$DocumentPath,
    [double]$ClassificationUmbral = 0.50,
    [double]$ExtractionUmbral = 0.80,
    [switch]$EnableGdcUpload
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

function Resolve-StatusUri {
    param([Parameter(Mandatory = $true)][string]$StatusUri)

    if ($StatusUri -match "http://localhost/") {
        return ($StatusUri -replace "http://localhost/", "http://localhost:7071/")
    }

    return $StatusUri
}

function Resolve-InitialMarkdownProvider {
    $functionsRoot = Join-Path $PSScriptRoot "..\..\src\backend\DocumentIA.Functions"
    $localSettingsPath = Join-Path $functionsRoot "local.settings.json"
    $appSettingsPath = Join-Path $functionsRoot "appsettings.json"

    if (Test-Path $localSettingsPath) {
        try {
            $localSettings = Get-Content -Raw -Path $localSettingsPath | ConvertFrom-Json
            $provider = $localSettings.Values.Extraction__InitialMarkdown__Provider
            if (-not [string]::IsNullOrWhiteSpace($provider)) {
                return "$provider (local.settings.json)"
            }
        }
        catch {
            # noop: if local settings cannot be parsed, fallback to appsettings
        }
    }

    if (Test-Path $appSettingsPath) {
        try {
            $appSettings = Get-Content -Raw -Path $appSettingsPath | ConvertFrom-Json
            $provider = $appSettings.Extraction.InitialMarkdown.Provider
            if (-not [string]::IsNullOrWhiteSpace($provider)) {
                return "$provider (appsettings.json)"
            }
        }
        catch {
            # noop: let caller show unknown
        }
    }

    return "unknown"
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
$initialMarkdownProvider = Resolve-InitialMarkdownProvider

$body = @{
    instrucciones = @{
        skipDuplicateCheck = $true
        forceReprocess = $true
        skipGDCUpload = -not $EnableGdcUpload.IsPresent
        classification = @{
            provider = "hybrid-tdn"
            model = "auto"
            umbral = $ClassificationUmbral
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
        correlationId = "HYBRIDTDN-SMOKE-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        submittedBy = "usuario.prueba@sareb.es"
        idGDC = $null
        idActivo = "354937"
    }
} | ConvertTo-Json -Depth 12

Write-Host ""
Write-Host "========================================"
Write-Host "  Smoke Ingest HybridTDN"
Write-Host "========================================"
Write-Host ""
Write-Host "Documento local : $resolvedDocumentPath"
Write-Host "Provider        : hybrid-tdn"
Write-Host "Markdown inicial: $initialMarkdownProvider"
Write-Host "Endpoint        : $endpoint"
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $body -ContentType "application/json"

    Write-Host "[OK] Request aceptada"
    Write-Host "Instance ID    : $($response.instanceId)"
    Write-Host "Correlation ID : $($response.correlationId)"

    $statusUri = Resolve-StatusUri -StatusUri $response.statusQueryUri
    Write-Host "Status URI     : $statusUri"

    $response.instanceId | Out-File "last-instance-id-hybridtdn-smoke.txt" -Encoding UTF8
    Write-Host "[OK] Instance ID guardado en last-instance-id-hybridtdn-smoke.txt"

    $maxRetries = 40
    $retryCount = 0
    $delaySeconds = 2
    $status = $null

    Write-Host ""
    Write-Host "Esperando finalizacion..."
    do {
        Start-Sleep -Seconds $delaySeconds
        try {
            $status = Invoke-RestMethod -Uri $statusUri -Method Get
            $retryCount++

            $runtime = Get-FieldValue -Object $status -Names @("runtimeStatus", "RuntimeStatus")
            Write-Host "[$retryCount/$maxRetries] Estado: $runtime"
        }
        catch {
            Write-Host "[$retryCount/$maxRetries] Error consultando estado, reintentando..."
        }
    } while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retryCount -lt $maxRetries)

    Write-Host ""
    if ($status.runtimeStatus -eq "Completed") {
        $output = $status.output
        $identificacion = Get-FieldValue -Object $output -Names @("Identificacion", "identificacion")
        $resultado = Get-FieldValue -Object $output -Names @("Resultado", "resultado")
        $detalle = Get-FieldValue -Object $output -Names @("DetalleEjecucion", "detalleEjecucion")
        $clasificacion = Get-FieldValue -Object $detalle -Names @("Clasificacion", "clasificacion")
        $extraccion = Get-FieldValue -Object $detalle -Names @("Extraccion", "extraccion")
        $postproceso = Get-FieldValue -Object $detalle -Names @("Postproceso", "postproceso")

        $tipologia = Get-FieldValue -Object $identificacion -Names @("Tipologia", "tipologia")
        $tdn1 = Get-FieldValue -Object $identificacion -Names @("Tdn1", "tdn1")
        $tdn2 = Get-FieldValue -Object $identificacion -Names @("Tdn2", "tdn2")
        $matricula = Get-FieldValue -Object $identificacion -Names @("Matricula", "matricula")

        $estadoFinal = Get-FieldValue -Object $resultado -Names @("Estado", "estado")
        $clasificador = Get-FieldValue -Object $clasificacion -Names @("Clasificador", "clasificador", "Modelo", "modelo")
        $confianzaClasificacion = Get-FieldValue -Object $clasificacion -Names @("Confianza", "confianza")
        $modeloExtraccion = Get-FieldValue -Object $extraccion -Names @("Modelo", "modelo")
        $proveedorExtraccion = Get-FieldValue -Object $extraccion -Names @("ProveedorExtrac", "proveedorExtrac", "Proveedor", "proveedor")
        $fallbackExtraccion = Get-FieldValue -Object $extraccion -Names @("FallbackUsado", "fallbackUsado")
        $fallbackExtraccionRazon = Get-FieldValue -Object $extraccion -Names @("FallbackRazon", "fallbackRazon")
        $markdownPostproceso = Get-FieldValue -Object $postproceso -Names @("Markdown", "markdown")
        $normalizaciones = Get-FieldValue -Object $postproceso -Names @("Normalizaciones", "normalizaciones")

        $markdownPresente = $false
        if (-not [string]::IsNullOrWhiteSpace($markdownPostproceso)) {
            $markdownPresente = $true
        }
        elseif ($normalizaciones -is [System.Collections.IEnumerable]) {
            foreach ($item in $normalizaciones) {
                if ("$item" -eq "Markdown") {
                    $markdownPresente = $true
                    break
                }
            }
        }

        $markdownLength = if ([string]::IsNullOrWhiteSpace($markdownPostproceso)) { 0 } else { $markdownPostproceso.Length }

        Write-Host "========================================"
        Write-Host "[OK] PROCESAMIENTO COMPLETADO"
        Write-Host "========================================"
        Write-Host "Estado final             : $estadoFinal"
        Write-Host "Tipologia                : $tipologia"
        Write-Host "Tdn1                     : $tdn1"
        Write-Host "Tdn2                     : $tdn2"
        Write-Host "Matricula                : $matricula"
        Write-Host "Clasificador             : $clasificador"
        Write-Host "Confianza clasificacion  : $confianzaClasificacion"
        Write-Host "Proveedor extraccion     : $proveedorExtraccion"
        Write-Host "Modelo extraccion        : $modeloExtraccion"
        Write-Host "Fallback extraccion      : $fallbackExtraccion"
        Write-Host "Fallback razon extraccion: $fallbackExtraccionRazon"
        Write-Host "Markdown presente        : $markdownPresente"
        Write-Host "Markdown longitud        : $markdownLength"

        $warnings = @()
        if ([string]::IsNullOrWhiteSpace($tdn1)) { $warnings += "Tdn1 vacio" }
        if ([string]::IsNullOrWhiteSpace($tdn2)) { $warnings += "Tdn2 vacio" }
        if ([string]::IsNullOrWhiteSpace($clasificador)) { $warnings += "Clasificador no informado" }

        if ($warnings.Count -gt 0) {
            Write-Host ""
            Write-Host "[WARN] Validaciones pendientes:"
            foreach ($w in $warnings) {
                Write-Host " - $w"
            }
            exit 2
        }

        Write-Host ""
        Write-Host "[OK] Validaciones HybridTDN minimas superadas."
        exit 0
    }
    elseif ($status.runtimeStatus -eq "Failed") {
        Write-Host "========================================"
        Write-Host "[ERROR] PROCESAMIENTO FALLIDO"
        Write-Host "========================================"
        Write-Host "$($status.output)"
        exit 1
    }
    else {
        Write-Host "========================================"
        Write-Host "[TIMEOUT] El proceso sigue en curso"
        Write-Host "========================================"
        Write-Host "Usa .\\check-status.ps1 -InstanceId $($response.instanceId)"
        exit 3
    }
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Fallo al invocar la API: $($_.Exception.Message)"
    exit 1
}
