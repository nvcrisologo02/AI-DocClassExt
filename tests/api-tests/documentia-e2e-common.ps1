[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

function Get-FieldValue {
    param([object]$Object, [string[]]$Names)
    if ($null -eq $Object) { return $null }
    foreach ($name in $Names) {
        $prop = $Object.PSObject.Properties[$name]
        if ($null -ne $prop) { return $prop.Value }
    }
    return $null
}

function Get-PathValue {
    param([object]$Object, [string]$Path)
    if ($null -eq $Object -or [string]::IsNullOrWhiteSpace($Path)) { return $null }

    $current = $Object
    foreach ($part in ($Path -split '\.')) {
        if ($null -eq $current) { return $null }
        if ($current -is [System.Array]) {
            if ($part -match '^\d+$') {
                $index = [int]$part
                if ($index -ge $current.Count) { return $null }
                $current = $current[$index]
                continue
            }
            return $null
        }

        $prop = $current.PSObject.Properties[$part]
        if ($null -eq $prop) {
            $prop = $current.PSObject.Properties | Where-Object { $_.Name -ieq $part } | Select-Object -First 1
        }
        if ($null -eq $prop) { return $null }
        $current = $prop.Value
    }
    return $current
}

function Resolve-StatusUri {
    param([string]$StatusUri)
    if ($StatusUri -match "http://localhost/") {
        return ($StatusUri -replace "http://localhost/", "http://localhost:7071/")
    }
    return $StatusUri
}

function Wait-ForDocumentIAOrchestration {
    param(
        [string]$StatusUri,
        [int]$MaxRetries,
        [int]$DelaySeconds
    )

    $uri = Resolve-StatusUri -StatusUri $StatusUri
    $history = @()
    $retries = 0
    do {
        Start-Sleep -Seconds $DelaySeconds
        $status = Invoke-RestMethod -Uri $uri -Method Get -ErrorAction Stop
        $history += [pscustomobject]@{
            Attempt       = $retries + 1
            RuntimeStatus = $status.runtimeStatus
            CustomStatus  = $status.customStatus
            CapturedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        }
        $retries++
    } while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retries -lt $MaxRetries)

    return [pscustomobject]@{
        FinalStatus = $status
        History     = $history
    }
}

function New-DocumentIARequestBody {
    param([pscustomobject]$Case, [string]$DocumentBase64, [string]$DocumentName)

    $umbral = if ($null -ne $Case.umbral) { [double]$Case.umbral } else { 0.50 }
    $maxPages = if ($null -ne $Case.maxPagesForClassificationOnly) { [int]$Case.maxPagesForClassificationOnly } else { 3 }
    $nivelClasificacion = if (-not [string]::IsNullOrWhiteSpace($Case.nivelClasificacion)) { $Case.nivelClasificacion } else { $null }
    $markdown = if (-not [string]::IsNullOrWhiteSpace($Case.markdown)) { $Case.markdown } else { $null }
    $executeIntegrar = if ($null -ne $Case.executeIntegrarWhenClassificationOnly) { [bool]$Case.executeIntegrarWhenClassificationOnly } else { $false }
    $idActivo = if (-not [string]::IsNullOrWhiteSpace($Case.idActivo)) { $Case.idActivo } else { "354937" }

    $classification = @{
        provider = if (-not [string]::IsNullOrWhiteSpace($Case.classificationProvider)) { $Case.classificationProvider } else { "auto" }
        model    = if (-not [string]::IsNullOrWhiteSpace($Case.classificationModel)) { $Case.classificationModel } else { "auto" }
        umbral   = $umbral
    }
    if ($null -ne $nivelClasificacion) { $classification["nivelClasificacion"] = $nivelClasificacion }
    if ($null -ne $markdown) { $classification["markdown"] = $markdown }

    $instrucciones = @{
        skipDuplicateCheck                    = [bool]$Case.skipDuplicateCheck
        forceReprocess                        = [bool]$Case.forceReprocess
        skipGDCUpload                         = [bool]$Case.skipGDCUpload
        classificationOnly                    = [bool]$Case.classificationOnly
        maxPagesForClassificationOnly         = $maxPages
        executeIntegrarWhenClassificationOnly = $executeIntegrar
        classification                        = $classification
        extraction                            = @{ model = "auto"; umbral = 0.80 }
    }
    if (-not [string]::IsNullOrWhiteSpace($Case.expectedType)) {
        $instrucciones["expectedType"] = $Case.expectedType
    }

    $body = @{
        instrucciones = $instrucciones
        documento     = @{
            name    = $DocumentName
            content = @{ base64 = $DocumentBase64 }
        }
        trazabilidad  = @{
            correlationId = "E2E-$($Case.domain)-$($Case.id)-$(Get-Date -Format 'yyyyMMddHHmmss')"
            submittedBy   = "documentia-e2e@sareb.es"
            idGDC         = $null
            idActivo      = $idActivo
        }
    }

    switch ($Case.payloadMode) {
        "missingDocumento" { $body.Remove("documento") }
        "missingBase64" { $body.documento.content.Remove("base64") }
        "invalidBase64" { $body.documento.content.base64 = "@@base64-invalido@@" }
        "invalidFlagType" { $body.instrucciones.classificationOnly = "no-es-boolean" }
    }

    return $body | ConvertTo-Json -Depth 15
}

function Test-CaseAssertions {
    param([pscustomobject]$Case, [object]$Status, [array]$History)

    $assertions = $Case.assertions
    $errors = @()
    $output = $Status.output
    $resultado = Get-FieldValue -Object $output -Names @("Resultado", "resultado")
    $detalle = Get-FieldValue -Object $output -Names @("DetalleEjecucion", "detalleEjecucion")
    $estado = Get-FieldValue -Object $resultado -Names @("Estado", "estado")

    if ($null -ne $assertions.expectedRuntimeStatus -and $Status.runtimeStatus -ne $assertions.expectedRuntimeStatus) {
        $errors += "runtimeStatus='$($Status.runtimeStatus)' esperado='$($assertions.expectedRuntimeStatus)'"
    }

    if ($null -ne $assertions.expectedStatus) {
        $allowed = @($assertions.expectedStatus)
        if ($allowed -notcontains $estado) {
            $errors += "Resultado.Estado='$estado' no esta en esperado: [$($allowed -join ',')]"
        }
    }

    if ($assertions.expectNoTechnicalError -eq $true -and $estado -eq "ERROR") {
        $mensaje = Get-FieldValue -Object $resultado -Names @("MensajeError", "mensajeError")
        $errors += "estado=ERROR inesperado: $mensaje"
    }

    if ($null -ne $assertions.expectOutputPathsNotEmpty) {
        foreach ($path in @($assertions.expectOutputPathsNotEmpty)) {
            if ([string]::IsNullOrWhiteSpace($path)) { continue }
            $value = Get-PathValue -Object $output -Path $path
            if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
                $errors += "output.$path vacio o ausente"
            }
        }
    }

    if ($null -ne $assertions.expectOutputPathEquals) {
        foreach ($rule in @($assertions.expectOutputPathEquals)) {
            if ($null -eq $rule -or [string]::IsNullOrWhiteSpace($rule.path)) { continue }
            $value = Get-PathValue -Object $output -Path $rule.path
            if ([string]$value -ne [string]$rule.value) {
                $errors += "output.$($rule.path)='$value' esperado='$($rule.value)'"
            }
        }
    }

    $outputJson = $output | ConvertTo-Json -Depth 30 -Compress
    if ($null -ne $assertions.expectOutputJsonContains) {
        foreach ($needle in @($assertions.expectOutputJsonContains)) {
            if ([string]::IsNullOrWhiteSpace($needle)) { continue }
            if ($outputJson -notlike "*$needle*") {
                $errors += "output no contiene '$needle'"
            }
        }
    }
    if ($null -ne $assertions.expectOutputJsonNotContains) {
        foreach ($needle in @($assertions.expectOutputJsonNotContains)) {
            if ([string]::IsNullOrWhiteSpace($needle)) { continue }
            if ($outputJson -like "*$needle*") {
                $errors += "output no deberia contener '$needle'"
            }
        }
    }

    $historyStatuses = @($History | ForEach-Object { $_.RuntimeStatus } | Select-Object -Unique)
    if ($null -ne $assertions.expectObservedRuntimeStatuses) {
        foreach ($expectedHistoryStatus in @($assertions.expectObservedRuntimeStatuses)) {
            if ([string]::IsNullOrWhiteSpace($expectedHistoryStatus)) { continue }
            if ($historyStatuses -notcontains $expectedHistoryStatus) {
                $errors += "runtimeStatus '$expectedHistoryStatus' no observado en polling"
            }
        }
    }

    return [pscustomobject]@{
        Success = ($errors.Count -eq 0)
        Errors  = $errors
        Estado  = $estado
        Detalle = $detalle
    }
}

function Invoke-DocumentIAE2ECase {
    param(
        [pscustomobject]$Case,
        [string]$Endpoint,
        [string]$ArtifactsDir,
        [int]$MaxRetries,
        [int]$DelaySeconds
    )

    $caseKey = if (-not [string]::IsNullOrWhiteSpace($Case.caseKey)) { $Case.caseKey } else { "$($Case.group)-$($Case.id)" }
    $assertions = $Case.assertions
    $expectHttp4xx = $null -ne $assertions.expectHttpStatus
    $startTime = Get-Date

    try {
        $body = $null
        if ($Case.payloadMode -eq "malformedJson") {
            $body = '{ "documento": '
        }
        else {
            $documentPath = $Case.documentPath
            if ([string]::IsNullOrWhiteSpace($documentPath) -or -not (Test-Path -Path $documentPath)) {
                return [pscustomobject]@{
                    Domain     = $Case.domain; Group = $Case.group; Id = $Case.id; CaseKey = $caseKey; Name = $Case.name
                    Status     = "SKIP"; Reason = "documentPath no existe: $documentPath"
                    TestCaseId = $Case.testCaseId; SuiteId = $Case.suiteId
                }
            }
            $resolvedPath = (Resolve-Path -Path $documentPath).Path
            $documentBytes = [System.IO.File]::ReadAllBytes($resolvedPath)
            $documentBase64 = [System.Convert]::ToBase64String($documentBytes)
            $documentName = [System.IO.Path]::GetFileName($resolvedPath)
            $body = New-DocumentIARequestBody -Case $Case -DocumentBase64 $documentBase64 -DocumentName $documentName
        }

        if ($expectHttp4xx) {
            $expectedCode = [int]$assertions.expectHttpStatus
            try {
                $response = Invoke-WebRequest -Uri $Endpoint -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
                return [pscustomobject]@{
                    Domain = $Case.domain; Group = $Case.group; Id = $Case.id; CaseKey = $caseKey; Name = $Case.name
                    Status = "FAIL"; Reason = "Esperaba HTTP $expectedCode pero obtuvo HTTP $($response.StatusCode)"
                    TestCaseId = $Case.testCaseId; SuiteId = $Case.suiteId
                }
            }
            catch {
                $statusCode = $null
                if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
                    $statusCode = [int]$_.Exception.Response.StatusCode.value__
                }
                if ($statusCode -eq $expectedCode) {
                    return [pscustomobject]@{
                        Domain = $Case.domain; Group = $Case.group; Id = $Case.id; CaseKey = $caseKey; Name = $Case.name
                        Status = "PASS"; Reason = "HTTP $expectedCode recibido correctamente"; HttpStatus = $statusCode
                        TestCaseId = $Case.testCaseId; SuiteId = $Case.suiteId
                    }
                }
                return [pscustomobject]@{
                    Domain = $Case.domain; Group = $Case.group; Id = $Case.id; CaseKey = $caseKey; Name = $Case.name
                    Status = "FAIL"; Reason = "Esperaba HTTP $expectedCode pero obtuvo HTTP $statusCode : $($_.Exception.Message)"
                    TestCaseId = $Case.testCaseId; SuiteId = $Case.suiteId
                }
            }
        }

        $initResponse = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
        $wait = Wait-ForDocumentIAOrchestration -StatusUri $initResponse.statusQueryUri -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds
        $elapsed = ((Get-Date) - $startTime).TotalSeconds
        $status = $wait.FinalStatus

        $artifactPath = Join-Path $ArtifactsDir "result-$caseKey.json"
        [pscustomobject]@{
            caseKey  = $caseKey
            caseName = $Case.name
            status   = $status
            history  = $wait.History
        } | ConvertTo-Json -Depth 30 | Out-File -FilePath $artifactPath -Encoding UTF8 -Force

        if ($status.runtimeStatus -ne "Completed") {
            return [pscustomobject]@{
                Domain = $Case.domain; Group = $Case.group; Id = $Case.id; CaseKey = $caseKey; Name = $Case.name
                Status = "FAIL"; Reason = "runtimeStatus=$($status.runtimeStatus) tras $MaxRetries intentos"
                InstanceId = $initResponse.instanceId; ElapsedSec = [math]::Round($elapsed, 1); ArtifactPath = $artifactPath
                TestCaseId = $Case.testCaseId; SuiteId = $Case.suiteId
            }
        }

        $assertionResult = Test-CaseAssertions -Case $Case -Status $status -History $wait.History
        if (-not $assertionResult.Success) {
            return [pscustomobject]@{
                Domain = $Case.domain; Group = $Case.group; Id = $Case.id; CaseKey = $caseKey; Name = $Case.name
                Status = "FAIL"; Reason = ($assertionResult.Errors -join " | "); Estado = $assertionResult.Estado
                InstanceId = $initResponse.instanceId; ElapsedSec = [math]::Round($elapsed, 1); ArtifactPath = $artifactPath
                TestCaseId = $Case.testCaseId; SuiteId = $Case.suiteId
            }
        }

        return [pscustomobject]@{
            Domain = $Case.domain; Group = $Case.group; Id = $Case.id; CaseKey = $caseKey; Name = $Case.name
            Status = "PASS"; Reason = "OK"; Estado = $assertionResult.Estado
            InstanceId = $initResponse.instanceId; ElapsedSec = [math]::Round($elapsed, 1); ArtifactPath = $artifactPath
            TestCaseId = $Case.testCaseId; SuiteId = $Case.suiteId
        }
    }
    catch {
        return [pscustomobject]@{
            Domain = $Case.domain; Group = $Case.group; Id = $Case.id; CaseKey = $caseKey; Name = $Case.name
            Status = "FAIL"; Reason = "Excepcion: $($_.Exception.Message)"
            TestCaseId = $Case.testCaseId; SuiteId = $Case.suiteId
        }
    }
}
