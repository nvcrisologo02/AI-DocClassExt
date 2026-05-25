param(
    [string]$CasesFile = ".\\critical-classification-cases.json",
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",
    [int]$MaxRetries = 45,
    [int]$DelaySeconds = 2,
    [switch]$Strict
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

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

function Resolve-TdnFromTipologia {
    param([Parameter(Mandatory = $false)][string]$Tipologia)

    if ([string]::IsNullOrWhiteSpace($Tipologia)) {
        return @{
            Tdn1 = $null
            Tdn2 = $null
        }
    }

    $normalized = $Tipologia.Trim().ToUpperInvariant()

    # Expected technical formats: nots.01 / nots.01@1.0
    $basePart = $normalized
    if ($basePart.Contains("@")) {
        $basePart = $basePart.Split("@")[0]
    }

    if ($basePart -match "^([A-Z0-9]{4})\.([A-Z0-9]{2,3})$") {
        $tdn1 = $matches[1]
        $suffix = $matches[2]
        return @{
            Tdn1 = $tdn1
            Tdn2 = "$tdn1-$suffix"
        }
    }

    # Also accept ESCR-10 style values directly.
    if ($basePart -match "^([A-Z0-9]{4})-([A-Z0-9]{2,3})$") {
        $tdn1 = $matches[1]
        $suffix = $matches[2]
        return @{
            Tdn1 = $tdn1
            Tdn2 = "$tdn1-$suffix"
        }
    }

    # Plain TDN1-only code (e.g. "NOTS", "ESCR") — clasificación parcial sin TDN2.
    if ($basePart -match "^([A-Z0-9]{4})$") {
        return @{
            Tdn1 = $matches[1]
            Tdn2 = $null
        }
    }

    return @{
        Tdn1 = $null
        Tdn2 = $null
    }
}

function Invoke-IngestCase {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Case,
        [Parameter(Mandatory = $true)][string]$Endpoint,
        [Parameter(Mandatory = $true)][int]$MaxRetries,
        [Parameter(Mandatory = $true)][int]$DelaySeconds
    )

    $documentPath = $Case.documentPath
    if ([string]::IsNullOrWhiteSpace($documentPath)) {
        return [pscustomobject]@{
            Id = $Case.id
            Name = $Case.name
            Status = "SKIP"
            Reason = "documentPath vacio"
        }
    }

    if (-not [System.IO.Path]::IsPathRooted($documentPath)) {
        return [pscustomobject]@{
            Id = $Case.id
            Name = $Case.name
            Status = "SKIP"
            Reason = "documentPath no es ruta absoluta"
        }
    }

    if (-not (Test-Path -Path $documentPath)) {
        return [pscustomobject]@{
            Id = $Case.id
            Name = $Case.name
            Status = "SKIP"
            Reason = "documentPath no existe"
        }
    }

    $resolvedPath = (Resolve-Path -Path $documentPath).Path
    $documentBytes = [System.IO.File]::ReadAllBytes($resolvedPath)
    $documentBase64 = [System.Convert]::ToBase64String($documentBytes)
    $documentName = [System.IO.Path]::GetFileName($resolvedPath)
    $classificationProvider = if ([string]::IsNullOrWhiteSpace($Case.classificationProvider)) { "auto" } else { "$($Case.classificationProvider)" }

    # nivelClasificacion: usar el del caso si existe; si solo hay expectedTdn1 sin expectedTdn2, inferir TDN1.
    $nivelClasificacion = if (-not [string]::IsNullOrWhiteSpace($Case.nivelClasificacion)) {
        $Case.nivelClasificacion
    } elseif (-not [string]::IsNullOrWhiteSpace($Case.expectedTdn1) -and [string]::IsNullOrWhiteSpace($Case.expectedTdn2)) {
        "TDN1"
    } else {
        "TDN1_TDN2"
    }

    $body = @{
        instrucciones = @{
            skipDuplicateCheck = $true
            forceReprocess = $true
            skipGDCUpload = $true
            classificationOnly = $true
            maxPagesForClassificationOnly = 3
            executeIntegrarWhenClassificationOnly = $false
            classification = @{
                provider = $classificationProvider
                model = "auto"
                umbral = 0.50
                nivelClasificacion = $nivelClasificacion
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
            correlationId = "CRITICAL-FAMILY-$($Case.id)-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
            submittedBy = "usuario.prueba@sareb.es"
            idGDC = $null
            idActivo = "354937"
        }
    } | ConvertTo-Json -Depth 12

    try {
        $response = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $body -ContentType "application/json"
        $statusUri = Resolve-StatusUri -StatusUri $response.statusQueryUri

        $status = $null
        $retryCount = 0
        do {
            Start-Sleep -Seconds $DelaySeconds
            $status = Invoke-RestMethod -Uri $statusUri -Method Get
            $retryCount++
        } while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retryCount -lt $MaxRetries)

        if ($status.runtimeStatus -ne "Completed") {
            return [pscustomobject]@{
                Id = $Case.id
                Name = $Case.name
                Status = "FAIL"
                Reason = "runtimeStatus=$($status.runtimeStatus)"
                InstanceId = $response.instanceId
            }
        }

        $output = $status.output
        $identificacion = Get-FieldValue -Object $output -Names @("Identificacion", "identificacion")
        $detalleEjecucion = Get-FieldValue -Object $output -Names @("DetalleEjecucion", "detalleEjecucion")
        $clasificacion = Get-FieldValue -Object $detalleEjecucion -Names @("Clasificacion", "clasificacion")

        $tipologia = Get-FieldValue -Object $identificacion -Names @("Tipologia", "tipologia")
        if ([string]::IsNullOrWhiteSpace($tipologia)) {
            $tipologia = Get-FieldValue -Object $clasificacion -Names @("TipologiaDetectada", "tipologiaDetectada", "Tipologia", "tipologia")
        }

        $tdn1 = Get-FieldValue -Object $identificacion -Names @("Tdn1", "tdn1")
        if ([string]::IsNullOrWhiteSpace($tdn1)) {
            $tdn1 = Get-FieldValue -Object $clasificacion -Names @("Tdn1", "tdn1")
        }

        $tdn2 = Get-FieldValue -Object $identificacion -Names @("Tdn2", "tdn2")
        if ([string]::IsNullOrWhiteSpace($tdn2)) {
            $tdn2 = Get-FieldValue -Object $clasificacion -Names @("Tdn2", "tdn2")
        }

        $inferred = Resolve-TdnFromTipologia -Tipologia $tipologia
        if ([string]::IsNullOrWhiteSpace($tdn1) -and -not [string]::IsNullOrWhiteSpace($inferred.Tdn1)) {
            $tdn1 = $inferred.Tdn1
        }
        if ([string]::IsNullOrWhiteSpace($tdn2) -and -not [string]::IsNullOrWhiteSpace($inferred.Tdn2)) {
            $tdn2 = $inferred.Tdn2
        }

        $expectedTdn1 = $Case.expectedTdn1
        $expectedTdn2 = $Case.expectedTdn2

        $errors = @()
        if ([string]::IsNullOrWhiteSpace($tipologia)) { $errors += "Tipologia vacia" }
        if ([string]::IsNullOrWhiteSpace($tdn1)) { $errors += "Tdn1 vacio" }
        if ($nivelClasificacion -ne "TDN1" -and [string]::IsNullOrWhiteSpace($tdn2)) { $errors += "Tdn2 vacio" }

        if (-not [string]::IsNullOrWhiteSpace($expectedTdn1) -and $expectedTdn1 -ne $tdn1) {
            $errors += "Tdn1 esperado='$expectedTdn1' real='$tdn1'"
        }

        if (-not [string]::IsNullOrWhiteSpace($expectedTdn2) -and $expectedTdn2 -ne $tdn2) {
            $errors += "Tdn2 esperado='$expectedTdn2' real='$tdn2'"
        }

        if ($errors.Count -gt 0) {
            return [pscustomobject]@{
                Id = $Case.id
                Name = $Case.name
                Status = "FAIL"
                Reason = ($errors -join " | ")
                InstanceId = $response.instanceId
                Tipologia = $tipologia
                Tdn1 = $tdn1
                Tdn2 = $tdn2
            }
        }

        return [pscustomobject]@{
            Id = $Case.id
            Name = $Case.name
            Status = "PASS"
            Reason = "OK"
            Provider = $classificationProvider
            InstanceId = $response.instanceId
            Tipologia = $tipologia
            Tdn1 = $tdn1
            Tdn2 = $tdn2
        }
    }
    catch {
        return [pscustomobject]@{
            Id = $Case.id
            Name = $Case.name
            Status = "FAIL"
            Reason = $_.Exception.Message
            Provider = $classificationProvider
        }
    }
}

Write-Host ""
Write-Host "========================================"
Write-Host "  Bateria Clasificacion Familias Criticas"
Write-Host "========================================"
Write-Host "CasesFile: $CasesFile"
Write-Host "Endpoint : $Endpoint"
Write-Host ""

if (-not (Test-Path -Path $CasesFile)) {
    Write-Host "[ERROR] No existe $CasesFile"
    Write-Host "Copia el template critical-classification-cases.sample.json como critical-classification-cases.json y rellena rutas reales."
    exit 1
}

try {
    $cases = Get-Content -Raw -Path $CasesFile | ConvertFrom-Json
}
catch {
    Write-Host "[ERROR] CasesFile invalido: $($_.Exception.Message)"
    exit 1
}

$caseList = @($cases)
if ($caseList.Count -eq 0) {
    Write-Host "[WARN] No hay casos para ejecutar."
    exit 0
}

$results = @()
foreach ($case in $caseList) {
    Write-Host "Ejecutando caso $($case.id): $($case.name)"
    $result = Invoke-IngestCase -Case $case -Endpoint $Endpoint -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds
    $results += $result

    $line = "[$($result.Status)] $($result.Id) - $($result.Name)"
    if (-not [string]::IsNullOrWhiteSpace($result.Provider)) {
        $line += " | provider=$($result.Provider)"
    }
    if (-not [string]::IsNullOrWhiteSpace($result.Reason)) {
        $line += " | $($result.Reason)"
    }
    Write-Host $line
}

$pass = @($results | Where-Object { $_.Status -eq "PASS" }).Count
$fail = @($results | Where-Object { $_.Status -eq "FAIL" }).Count
$skip = @($results | Where-Object { $_.Status -eq "SKIP" }).Count
$total = $results.Count

Write-Host ""
Write-Host "Resumen: Total=$total PASS=$pass FAIL=$fail SKIP=$skip"
$results | Format-Table -AutoSize Id, Name, Status, Provider, InstanceId, Tdn1, Tdn2, Tipologia, Reason

if ($Strict.IsPresent -and ($fail -gt 0 -or $skip -gt 0)) {
    exit 2
}

if ($fail -gt 0) {
    exit 1
}

exit 0
