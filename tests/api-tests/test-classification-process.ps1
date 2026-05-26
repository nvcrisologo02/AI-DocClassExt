<#
.SYNOPSIS
    Bateria E2E de cobertura del proceso de clasificacion DocumentIA.

.DESCRIPTION
    Ejecuta todos los casos definidos en classification-process-cases.json cubriendo:
      Grupo A - Flujos por proveedor (auto, di, gpt)
      Grupo B - Niveles de clasificacion jerarquica GPT (D2/D7)
      Grupo C - Markdown pre-procesado (D4)
      Grupo D - ClassificationOnly y recorte de paginas
      Grupo E - Deduplicacion (skipDuplicate, forceReprocess, clave por nivel D7)
      Grupo F - Umbral de confianza
      Grupo G - Validacion de contrato HTTP 4xx
      Grupo H - Calidad del output (tipologia conocida vs virtual)

.PARAMETER CasesFile
    Ruta al JSON de casos. Por defecto: .\classification-process-cases.json

.PARAMETER Endpoint
    URL del endpoint IngestDocument. Por defecto: http://localhost:7071/api/IngestDocument

.PARAMETER MaxRetries
    Intentos maximos de polling por caso. Por defecto: 45

.PARAMETER DelaySeconds
    Segundos entre intentos de polling. Por defecto: 2

.PARAMETER Groups
    Filtro de grupos a ejecutar (A,B,C,D,E,F,G,H). Por defecto todos.

.PARAMETER ArtifactsDir
    Carpeta donde guardar los JSON de respuesta por caso.

.PARAMETER Strict
    Si se activa, exit code 2 cuando hay FAILs o SKIPs.

.PARAMETER PublishToAdo
    Si se activa, publica resultados en un Test Run de Azure DevOps (requiere AdoOrg, AdoPat, AdoProject, AdoTestPlanId).

.PARAMETER AdoOrg
    URL de organizacion ADO. Ej: https://sareb.visualstudio.com

.PARAMETER AdoProject
    Nombre del proyecto ADO. Ej: "AI DocClassExt"

.PARAMETER AdoPat
    Personal Access Token de ADO (con permiso Test Read/Write).

.PARAMETER AdoTestPlanId
    ID del Test Plan en ADO donde publicar resultados.

.EXAMPLE
    .\test-classification-process.ps1
    .\test-classification-process.ps1 -Groups A,B,G
    .\test-classification-process.ps1 -Strict -ArtifactsDir C:\temp\evidencias
    .\test-classification-process.ps1 -PublishToAdo -AdoOrg https://sareb.visualstudio.com -AdoProject "AI DocClassExt" -AdoPat $env:ADO_PAT -AdoTestPlanId 99581
#>

param(
    [string]$CasesFile = ".\classification-process-cases.json",
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",
    [int]$MaxRetries = 45,
    [int]$DelaySeconds = 2,
    [string[]]$Groups = @("A","B","C","D","E","F","G","H"),
    [string]$ArtifactsDir = ".\artifacts\classification-process",
    [switch]$Strict,
    [switch]$PublishToAdo,
    [string]$AdoOrg = "https://sareb.visualstudio.com",
    [string]$AdoProject = "AI DocClassExt",
    [string]$AdoPat = "",
    [int]$AdoTestPlanId = 0
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# ─── Helpers ─────────────────────────────────────────────────────────────────

function Get-FieldValue {
    param([object]$Object, [string[]]$Names)
    if ($null -eq $Object) { return $null }
    foreach ($name in $Names) {
        $prop = $Object.PSObject.Properties[$name]
        if ($null -ne $prop) { return $prop.Value }
    }
    return $null
}

function Resolve-StatusUri {
    param([string]$StatusUri)
    if ($StatusUri -match "http://localhost/") {
        return ($StatusUri -replace "http://localhost/", "http://localhost:7071/")
    }
    return $StatusUri
}

function Write-CaseHeader {
    param([string]$Group, [string]$Id, [string]$Name)
    Write-Host ""
    Write-Host "  [$Group-$Id] $Name" -ForegroundColor Cyan
}

function Write-CaseResult {
    param([string]$Status, [string]$Reason)
    $color = switch ($Status) {
        "PASS"  { "Green" }
        "FAIL"  { "Red" }
        "SKIP"  { "Yellow" }
        default { "Gray" }
    }
    Write-Host "  --> $Status : $Reason" -ForegroundColor $color
}

# ─── Polling ─────────────────────────────────────────────────────────────────

function Wait-ForOrchestration {
    param([string]$StatusUri)
    $uri = Resolve-StatusUri -StatusUri $StatusUri
    $retries = 0
    do {
        Start-Sleep -Seconds $DelaySeconds
        $status = Invoke-RestMethod -Uri $uri -Method Get -ErrorAction Stop
        $retries++
    } while (($status.runtimeStatus -eq "Running" -or $status.runtimeStatus -eq "Pending") -and $retries -lt $MaxRetries)
    return $status
}

# ─── Construccion de body ─────────────────────────────────────────────────────

function Build-RequestBody {
    param([pscustomobject]$Case, [string]$DocumentBase64, [string]$DocumentName)

    $umbral = if ($null -ne $Case.umbral) { [double]$Case.umbral } else { 0.50 }
    $maxPages = if ($null -ne $Case.maxPagesForClassificationOnly) { [int]$Case.maxPagesForClassificationOnly } else { 3 }
    $nivelClasificacion = if (-not [string]::IsNullOrWhiteSpace($Case.nivelClasificacion)) { $Case.nivelClasificacion } else { $null }
    $markdown = if (-not [string]::IsNullOrWhiteSpace($Case.markdown)) { $Case.markdown } else { $null }
    $executeIntegrar = if ($null -ne $Case.executeIntegrarWhenClassificationOnly) { [bool]$Case.executeIntegrarWhenClassificationOnly } else { $false }
    $idActivo = if (-not [string]::IsNullOrWhiteSpace($Case.idActivo)) { $Case.idActivo } else { "354937" }

    $classification = @{
        provider = if (-not [string]::IsNullOrWhiteSpace($Case.classificationProvider)) { $Case.classificationProvider } else { "auto" }
        model    = "auto"
        umbral   = $umbral
    }
    if ($null -ne $nivelClasificacion) { $classification["nivelClasificacion"] = $nivelClasificacion }
    if ($null -ne $markdown)           { $classification["markdown"] = $markdown }

    # expectedType solo se incluye si el caso lo define (para el caso G1 que espera HTTP 400)
    $instrucciones = @{
        skipDuplicateCheck                  = [bool]$Case.skipDuplicateCheck
        forceReprocess                      = [bool]$Case.forceReprocess
        skipGDCUpload                       = [bool]$Case.skipGDCUpload
        classificationOnly                  = [bool]$Case.classificationOnly
        maxPagesForClassificationOnly       = $maxPages
        executeIntegrarWhenClassificationOnly = $executeIntegrar
        classification                      = $classification
        extraction                          = @{ model = "auto"; umbral = 0.80 }
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
            correlationId = "PROCTEST-$($Case.id)-$(Get-Date -Format 'yyyyMMddHHmmss')"
            submittedBy   = "test-classification-process@sareb.es"
            idGDC         = $null
            idActivo      = $idActivo
        }
    }
    return $body | ConvertTo-Json -Depth 15
}

# ─── Ejecucion de caso ────────────────────────────────────────────────────────

function Invoke-TestCase {
    param([pscustomobject]$Case)

    $documentPath = $Case.documentPath
    if ([string]::IsNullOrWhiteSpace($documentPath) -or -not (Test-Path -Path $documentPath)) {
        return [pscustomobject]@{
            Group      = $Case.group; Id = $Case.id; Name = $Case.name
            Status     = "SKIP"; Reason = "documentPath no existe: $documentPath"
        }
    }

    $resolvedPath  = (Resolve-Path -Path $documentPath).Path
    $documentBytes  = [System.IO.File]::ReadAllBytes($resolvedPath)
    $documentBase64 = [System.Convert]::ToBase64String($documentBytes)
    $documentName   = [System.IO.Path]::GetFileName($resolvedPath)

    $assertions = $Case.assertions
    $expectHttp4xx = $null -ne $assertions.expectHttpStatus

    try {
        $body = Build-RequestBody -Case $Case -DocumentBase64 $documentBase64 -DocumentName $documentName

        # ── Casos que esperan HTTP 4xx ────────────────────────────────────────
        if ($expectHttp4xx) {
            $expectedCode = [int]$assertions.expectHttpStatus
            try {
                $response = Invoke-WebRequest -Uri $Endpoint -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
                # Si llego aqui, no se produjo error HTTP
                return [pscustomobject]@{
                    Group  = $Case.group; Id = $Case.id; Name = $Case.name
                    Status = "FAIL"
                    Reason = "Esperaba HTTP $expectedCode pero obtuvo HTTP $($response.StatusCode)"
                }
            }
            catch {
                $sc = $_.Exception.Response.StatusCode.value__
                if ($sc -eq $expectedCode) {
                    return [pscustomobject]@{
                        Group  = $Case.group; Id = $Case.id; Name = $Case.name
                        Status = "PASS"; Reason = "HTTP $expectedCode recibido correctamente"
                        HttpStatus = $sc
                    }
                } else {
                    return [pscustomobject]@{
                        Group  = $Case.group; Id = $Case.id; Name = $Case.name
                        Status = "FAIL"
                        Reason = "Esperaba HTTP $expectedCode pero obtuvo HTTP $sc : $($_.Exception.Message)"
                    }
                }
            }
        }

        # ── Casos normales (orquestacion) ─────────────────────────────────────
        $startTime = Get-Date
        $initResponse = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
        $status = Wait-ForOrchestration -StatusUri $initResponse.statusQueryUri
        $elapsed = ((Get-Date) - $startTime).TotalSeconds

        # Guardar artefacto
        $artifactPath = Join-Path $ArtifactsDir "result-$($Case.group)-$($Case.id).json"
        $status | ConvertTo-Json -Depth 20 | Out-File -FilePath $artifactPath -Encoding UTF8 -Force

        if ($status.runtimeStatus -ne "Completed") {
            return [pscustomobject]@{
                Group      = $Case.group; Id = $Case.id; Name = $Case.name
                Status     = "FAIL"
                Reason     = "runtimeStatus=$($status.runtimeStatus) tras $MaxRetries intentos"
                InstanceId = $initResponse.instanceId
                ElapsedSec = [math]::Round($elapsed, 1)
            }
        }

        # ── Extraer campos del output ─────────────────────────────────────────
        $output          = $status.output
        $resultado       = Get-FieldValue -Object $output -Names @("Resultado","resultado")
        $identificacion  = Get-FieldValue -Object $output -Names @("Identificacion","identificacion")
        $detalleEjecucion = Get-FieldValue -Object $output -Names @("DetalleEjecucion","detalleEjecucion")
        $clasificacion   = Get-FieldValue -Object $detalleEjecucion -Names @("Clasificacion","clasificacion")
        $seguimiento     = Get-FieldValue -Object $detalleEjecucion -Names @("Seguimiento","seguimiento")

        $estado              = Get-FieldValue -Object $resultado -Names @("Estado","estado")
        $reutilizada         = Get-FieldValue -Object $resultado -Names @("ReutilizadaPorDuplicado","reutilizadaPorDuplicado")
        $mensajeReutilizacion = Get-FieldValue -Object $resultado -Names @("MensajeReutilizacion","mensajeReutilizacion")
        $mensajeError        = Get-FieldValue -Object $resultado -Names @("MensajeError","mensajeError")

        $tdn1                = Get-FieldValue -Object $identificacion -Names @("Tdn1","tdn1")
        $tdn2                = Get-FieldValue -Object $identificacion -Names @("Tdn2","tdn2")
        $tipologia           = Get-FieldValue -Object $identificacion -Names @("Tipologia","tipologia")
        $propuestaTipologia  = Get-FieldValue -Object $identificacion -Names @("PropuestaTipologia","propuestaTipologia")

        $nivelEjecucion      = Get-FieldValue -Object $detalleEjecucion -Names @("NivelClasificacion","nivelClasificacion")
        $classOnlyFlag       = Get-FieldValue -Object $detalleEjecucion -Names @("ClassificationOnly","classificationOnly")
        $recorteAplicado     = Get-FieldValue -Object $detalleEjecucion -Names @("RecorteAplicado","recorteAplicado")
        $paginasIncluidas    = Get-FieldValue -Object $detalleEjecucion -Names @("PaginasIncluidas","paginasIncluidas")
        $origenMarkdown      = Get-FieldValue -Object $detalleEjecucion -Names @("OrigenMarkdown","origenMarkdown")
        $markdownGenerado    = Get-FieldValue -Object $detalleEjecucion -Names @("MarkdownGenerado","markdownGenerado")

        $proveedorClasif     = Get-FieldValue -Object $clasificacion -Names @("ProveedorClasif","proveedorClasif")

        # ── Evaluacion de assertions ──────────────────────────────────────────
        $errors = @()

        # expectedStatus
        if ($null -ne $assertions.expectedStatus) {
            $allowedStatuses = @($assertions.expectedStatus)
            if ($allowedStatuses -notcontains $estado) {
                $errors += "estado='$estado' no esta en esperado: [$($allowedStatuses -join ',')]"
            }
        }

        # expectStatusNot
        if (-not [string]::IsNullOrWhiteSpace($assertions.expectStatusNot)) {
            if ($estado -eq $assertions.expectStatusNot) {
                $errors += "estado='$estado' no deberia ser '$($assertions.expectStatusNot)'"
            }
        }

        # expectTdn1NotEmpty
        if ($assertions.expectTdn1NotEmpty -eq $true -and [string]::IsNullOrWhiteSpace($tdn1)) {
            $errors += "tdn1 esta vacio"
        }

        # expectTdn2Empty
        if ($assertions.expectTdn2Empty -eq $true -and -not [string]::IsNullOrWhiteSpace($tdn2)) {
            $errors += "tdn2 deberia estar vacio pero tiene valor='$tdn2'"
        }

        # expectNivelClasificacion
        if (-not [string]::IsNullOrWhiteSpace($assertions.expectNivelClasificacion)) {
            if ($nivelEjecucion -ne $assertions.expectNivelClasificacion) {
                $errors += "nivelClasificacion='$nivelEjecucion' esperado='$($assertions.expectNivelClasificacion)'"
            }
        }

        # expectProveedorContains
        if (-not [string]::IsNullOrWhiteSpace($assertions.expectProveedorContains)) {
            $needle = $assertions.expectProveedorContains
            if (-not ($proveedorClasif -like "*$needle*")) {
                $errors += "proveedorClasif='$proveedorClasif' no contiene '$needle'"
            }
        }

        # expectReutilizadaPorDuplicado
        if ($null -ne $assertions.expectReutilizadaPorDuplicado) {
            $expected = [bool]$assertions.expectReutilizadaPorDuplicado
            $actual   = if ($null -ne $reutilizada) { [bool]$reutilizada } else { $false }
            if ($actual -ne $expected) {
                $errors += "reutilizadaPorDuplicado='$actual' esperado='$expected'"
            }
        }

        # expectClassificationOnly
        if ($assertions.expectClassificationOnly -eq $true) {
            if ($classOnlyFlag -ne $true) {
                $errors += "detalleEjecucion.classificationOnly='$classOnlyFlag' esperado=true"
            }
        }

        # expectRecorteAplicado
        if ($null -ne $assertions.expectRecorteAplicado) {
            $expectedRecorte = [bool]$assertions.expectRecorteAplicado
            $actualRecorte   = if ($null -ne $recorteAplicado) { [bool]$recorteAplicado } else { $false }
            if ($actualRecorte -ne $expectedRecorte) {
                $errors += "recorteAplicado='$actualRecorte' esperado='$expectedRecorte'"
            }
        }

        # expectPaginasIncluidasMax
        if ($null -ne $assertions.expectPaginasIncluidasMax) {
            $maxPags = [int]$assertions.expectPaginasIncluidasMax
            $actualPags = if ($null -ne $paginasIncluidas) { [int]$paginasIncluidas } else { 0 }
            if ($actualPags -gt $maxPags) {
                $errors += "paginasIncluidas=$actualPags > max permitido=$maxPags"
            }
        }

        # expectOrigenMarkdown
        if (-not [string]::IsNullOrWhiteSpace($assertions.expectOrigenMarkdown)) {
            if ($origenMarkdown -ne $assertions.expectOrigenMarkdown) {
                $errors += "origenMarkdown='$origenMarkdown' esperado='$($assertions.expectOrigenMarkdown)'"
            }
        }

        # expectOrigenMarkdownNot
        if (-not [string]::IsNullOrWhiteSpace($assertions.expectOrigenMarkdownNot)) {
            if ($origenMarkdown -eq $assertions.expectOrigenMarkdownNot) {
                $errors += "origenMarkdown='$origenMarkdown' NO deberia ser '$($assertions.expectOrigenMarkdownNot)'"
            }
        }

        # expectTipologiaNotDesconocido
        if ($assertions.expectTipologiaNotDesconocido -eq $true) {
            if ($tipologia -eq "Desconocido" -or [string]::IsNullOrWhiteSpace($tipologia)) {
                $errors += "tipologia='$tipologia' (Desconocido o vacia)"
            }
        }

        # expectTipologiaNotEmpty — tipologia conocida (no null ni vacia), puede ser Desconocido
        if ($assertions.expectTipologiaNotEmpty -eq $true) {
            if ([string]::IsNullOrWhiteSpace($tipologia)) {
                $errors += "Identificacion.Tipologia esta vacia (esperado: tipologia asignada)"
            }
        }

        # expectDiWasTried — DocumentIntelligence debe aparecer en DetalleProveedores
        if ($assertions.expectDiWasTried -eq $true) {
            $detalleProvs = Get-FieldValue -Object $clasificacion -Names @("DetalleProveedores", "detalleProveedores")
            $diEntry = $null
            if ($null -ne $detalleProvs) {
                $diEntry = @($detalleProvs) | Where-Object { $_.Proveedor -eq "DocumentIntelligence" -or $_.Proveedor -like "*DI*" }
            }
            if ($null -eq $diEntry -or @($diEntry).Count -eq 0) {
                $errors += "DocumentIntelligence no aparece en DetalleProveedores (DI no fue intentado)"
            }
        }

        # ifDesconocidoExpectPropuestaTipologia
        if ($assertions.ifDesconocidoExpectPropuestaTipologia -eq $true) {
            if ($tipologia -eq "Desconocido" -and [string]::IsNullOrWhiteSpace($propuestaTipologia)) {
                $errors += "tipologia=Desconocido pero propuestaTipologia esta vacia (D6)"
            }
        }

        # expectNoTechnicalError — estado no debe ser ERROR
        if ($assertions.expectNoTechnicalError -eq $true) {
            if ($estado -eq "ERROR") {
                $errors += "estado=ERROR (error tecnico inesperado): $mensajeError"
            }
        }

        # ── Resultado ─────────────────────────────────────────────────────────
        if ($errors.Count -gt 0) {
            return [pscustomobject]@{
                Group              = $Case.group
                Id                 = $Case.id
                Name               = $Case.name
                Status             = "FAIL"
                Reason             = ($errors -join " | ")
                Estado             = $estado
                Tdn1               = $tdn1
                Tdn2               = $tdn2
                Nivel              = $nivelEjecucion
                Proveedor          = $proveedorClasif
                Reutilizada        = $reutilizada
                RecorteAplicado    = $recorteAplicado
                PaginasIncluidas   = $paginasIncluidas
                OrigenMarkdown     = $origenMarkdown
                PropuestaTipologia = $propuestaTipologia
                InstanceId         = $initResponse.instanceId
                ElapsedSec         = [math]::Round($elapsed, 1)
                ArtifactPath       = $artifactPath
            }
        }

        return [pscustomobject]@{
            Group              = $Case.group
            Id                 = $Case.id
            Name               = $Case.name
            Status             = "PASS"
            Reason             = "OK"
            Estado             = $estado
            Tdn1               = $tdn1
            Tdn2               = $tdn2
            Nivel              = $nivelEjecucion
            Proveedor          = $proveedorClasif
            Reutilizada        = $reutilizada
            RecorteAplicado    = $recorteAplicado
            PaginasIncluidas   = $paginasIncluidas
            OrigenMarkdown     = $origenMarkdown
            PropuestaTipologia = $propuestaTipologia
            InstanceId         = $initResponse.instanceId
            ElapsedSec         = [math]::Round($elapsed, 1)
            ArtifactPath       = $artifactPath
        }
    }
    catch {
        return [pscustomobject]@{
            Group  = $Case.group; Id = $Case.id; Name = $Case.name
            Status = "FAIL"
            Reason = "Excepcion: $($_.Exception.Message)"
        }
    }
}

# ─── Publicacion ADO ──────────────────────────────────────────────────────────

function Publish-AdoTestRun {
    param([array]$Results, [int]$TestPlanId, [string]$Org, [string]$Project, [string]$Pat)

    if ([string]::IsNullOrWhiteSpace($Pat)) {
        Write-Host "[ADO] Pat no informado. Omitiendo publicacion." -ForegroundColor Yellow
        return
    }

    $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$Pat"))
    $headers = @{
        Authorization  = "Basic $base64Auth"
        "Content-Type" = "application/json"
    }
    $apiBase = "$Org/$([Uri]::EscapeDataString($Project))/_apis/test"
    $apiVer  = "?api-version=7.1"

    # Mapeo estable de casos E2E -> Test Case WI en ADO
    $caseToTestCase = @{
        "A-A1" = 99558; "A-A2" = 99561; "A-A3" = 99555
        "B-B1" = 99554; "B-B2" = 99559; "B-B3" = 99556
        "C-C1" = 99557; "C-C2" = 99560
        "D-D1" = 99565; "D-D2" = 99562; "D-D3" = 99568
        "E-E1" = 99566; "E-E2" = 99574; "E-E3" = 99567; "E-E4" = 99563; "E-E5a" = 99564; "E-E5b" = 99572
        "F-F1" = 99569; "F-F2" = 99576
        "G-G1" = 99570; "G-G2" = 99575
        "H-H1" = 99571; "H-H2" = 99573
    }

    # Obtener points reales del plan/suites para enlazar cada resultado a su punto de prueba
    $pointByTestCase = @{}
    try {
        $suitesApi = "$Org/$([Uri]::EscapeDataString($Project))/_apis/testplan/plans/$TestPlanId/suites?api-version=7.1"
        $suites = Invoke-RestMethod -Uri $suitesApi -Method Get -Headers $headers -ErrorAction Stop
        foreach ($suite in @($suites.value)) {
            # Ignorar root suite; no contiene los casos vinculados
            if ($suite.name -eq "Cobertura E2E - Proceso Clasificacion D1-D7") { continue }

            $pointsApi = "$apiBase/Plans/$TestPlanId/Suites/$($suite.id)/points$apiVer"
            $points = Invoke-RestMethod -Uri $pointsApi -Method Get -Headers $headers -ErrorAction Stop
            foreach ($p in @($points.value)) {
                $tcId = $null
                if ($null -ne $p.testCase -and $null -ne $p.testCase.id) {
                    $tcId = [int]$p.testCase.id
                }
                if ($null -ne $tcId -and -not $pointByTestCase.ContainsKey($tcId)) {
                    $pointByTestCase[$tcId] = [int]$p.id
                }
            }
        }
    }
    catch {
        Write-Host "[ADO] Error obteniendo test points del plan ${TestPlanId}: $($_.Exception.Message)" -ForegroundColor Red
        return
    }

    # Enriquecer resultados con testCaseId + testPointId y validar cobertura total
    $linkedResults = @()
    $missing = @()
    foreach ($r in $Results) {
        $key = "$($r.Group)-$($r.Id)"
        $tcId = $null
        if ($caseToTestCase.ContainsKey($key)) {
            $tcId = [int]$caseToTestCase[$key]
        }
        if ($null -eq $tcId) {
            $missing += "Sin mapeo WI para caso $key"
            continue
        }

        $tpId = $null
        if ($pointByTestCase.ContainsKey($tcId)) {
            $tpId = [int]$pointByTestCase[$tcId]
        }
        if ($null -eq $tpId) {
            $missing += "Sin test point para caso $key (TC=$tcId)"
            continue
        }

        $linkedResults += [pscustomobject]@{
            RawResult   = $r
            Key         = $key
            TestCaseId  = $tcId
            TestPointId = $tpId
        }
    }

    if ($missing.Count -gt 0) {
        Write-Host "[ADO] No se publica: hay casos sin vinculacion completa (testCase/testPoint)." -ForegroundColor Red
        foreach ($m in $missing) { Write-Host "  - $m" -ForegroundColor Yellow }
        return
    }

    $pointIds = @($linkedResults | ForEach-Object { $_.TestPointId } | Sort-Object -Unique)

    # Crear Test Run
    $runBody = @{
        name        = "Clasificacion-Proceso-E2E-$(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        plan        = @{ id = $TestPlanId }
        pointIds    = $pointIds
        isAutomated = $true
    } | ConvertTo-Json

    try {
        $run = Invoke-RestMethod -Uri "$apiBase/runs$apiVer" -Method Post -Headers $headers -Body $runBody -ErrorAction Stop
        $runId = $run.id
        Write-Host "[ADO] Test Run creado: $runId" -ForegroundColor Cyan

        # En un run planificado (pointIds), Azure crea resultados vacios por point.
        # Debemos actualizarlos por PATCH (no POST).
        $existingResults = Invoke-RestMethod -Uri "$apiBase/runs/$runId/results?`$top=1000&api-version=7.1" -Method Get -Headers $headers -ErrorAction Stop
        $resultByPoint = @{}
        foreach ($er in @($existingResults.value)) {
            if ($null -ne $er.testPoint -and $null -ne $er.testPoint.id) {
                $resultByPoint[[int]$er.testPoint.id] = [int]$er.id
            }
        }

        $missingPointResults = @()
        $patchPayload = @()
        foreach ($lr in $linkedResults) {
            $r = $lr.RawResult
            $tpId = [int]$lr.TestPointId
            if (-not $resultByPoint.ContainsKey($tpId)) {
                $missingPointResults += "Sin result precreado para pointId=$tpId (caso $($lr.Key))"
                continue
            }

            $outcome = switch ($r.Status) {
                "PASS" { "Passed" }
                "FAIL" { "Failed" }
                "SKIP" { "NotApplicable" }
                default { "Unspecified" }
            }

            $patchPayload += @{
                id                   = [int]$resultByPoint[$tpId]
                outcome              = $outcome
                state                = "Completed"
                comment              = if ($r.Reason) { "$($r.Reason)" } else { "" }
                durationInMs         = if ($r.ElapsedSec) { [int]($r.ElapsedSec * 1000) } else { 0 }
                errorMessage         = if ($r.Status -eq "FAIL") { $r.Reason } else { $null }
                automatedTestName    = "ClassificationProcess.$($r.Group).$($r.Id)"
                automatedTestStorage = "test-classification-process.ps1"
            }
        }

        if ($missingPointResults.Count -gt 0) {
            Write-Host "[ADO] No se publica: faltan resultados precreados para algunos test points." -ForegroundColor Red
            foreach ($m in $missingPointResults) { Write-Host "  - $m" -ForegroundColor Yellow }
            return
        }

        $resultsBody = ConvertTo-Json -InputObject @($patchPayload) -Depth 10
        Invoke-RestMethod -Uri "$apiBase/runs/$runId/results$apiVer" -Method Patch -Headers $headers -Body $resultsBody -ErrorAction Stop | Out-Null
        Write-Host "[ADO] Resultados vinculados y publicados en Run $runId ($($linkedResults.Count) casos)" -ForegroundColor Cyan

        # Completar Run
        $updateBody = @{ state = "Completed" } | ConvertTo-Json
        Invoke-RestMethod -Uri "$apiBase/runs/$runId$apiVer" -Method Patch -Headers $headers -Body $updateBody -ErrorAction Stop | Out-Null
        Write-Host "[ADO] Run $runId marcado como Completed. URL: $Org/$Project/_testManagement/runs?runId=$runId" -ForegroundColor Green
    }
    catch {
        Write-Host "[ADO] Error publicando resultados: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response -and $_.Exception.Response.GetResponseStream) {
            try {
                $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
                $body = $reader.ReadToEnd()
                if (-not [string]::IsNullOrWhiteSpace($body)) {
                    Write-Host "[ADO] Detalle: $body" -ForegroundColor DarkYellow
                }
            }
            catch { }
        }
    }
}

# ─── MAIN ─────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Bateria E2E - Cobertura Proceso de Clasificacion"       -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  CasesFile : $CasesFile"
Write-Host "  Endpoint  : $Endpoint"
Write-Host "  Grupos    : $($Groups -join ', ')"
Write-Host "  Artifacts : $ArtifactsDir"
Write-Host ""

# Validar fichero de casos
if (-not (Test-Path -Path $CasesFile)) {
    Write-Host "[ERROR] No existe $CasesFile" -ForegroundColor Red
    exit 1
}

$allCases = Get-Content -Raw -Path $CasesFile | ConvertFrom-Json
$selectedCases = @($allCases | Where-Object { $Groups -contains $_.group })

if ($selectedCases.Count -eq 0) {
    Write-Host "[WARN] Sin casos para los grupos: $($Groups -join ', ')" -ForegroundColor Yellow
    exit 0
}

# Crear carpeta de artefactos
if (-not (Test-Path -Path $ArtifactsDir)) {
    New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null
}

Write-Host "  Casos seleccionados: $($selectedCases.Count)" -ForegroundColor White
Write-Host ""

# ── Cabeceras de grupos ───────────────────────────────────────────────────────
$groupLabels = @{
    "A" = "Flujos por Proveedor"
    "B" = "Niveles Clasificacion Jerarquica GPT (D2/D7)"
    "C" = "Markdown Pre-procesado (D4)"
    "D" = "ClassificationOnly y Recorte de Paginas"
    "E" = "Deduplicacion"
    "F" = "Umbral de Confianza"
    "G" = "Validacion Contrato HTTP 4xx"
    "H" = "Calidad del Output"
}

$results = @()
$currentGroup = ""

foreach ($case in $selectedCases) {
    if ($case.group -ne $currentGroup) {
        $currentGroup = $case.group
        Write-Host ""
        Write-Host "─── Grupo $currentGroup : $($groupLabels[$currentGroup]) ───" -ForegroundColor Magenta
    }

    Write-CaseHeader -Group $case.group -Id $case.id -Name $case.name
    $result = Invoke-TestCase -Case $case
    $results += $result
    Write-CaseResult -Status $result.Status -Reason $result.Reason
}

# ── Resumen ───────────────────────────────────────────────────────────────────
$pass  = @($results | Where-Object { $_.Status -eq "PASS" }).Count
$fail  = @($results | Where-Object { $_.Status -eq "FAIL" }).Count
$skip  = @($results | Where-Object { $_.Status -eq "SKIP" }).Count
$total = $results.Count

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  RESUMEN: Total=$total  PASS=$pass  FAIL=$fail  SKIP=$skip" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

$results | Format-Table -AutoSize -Property `
    @{L="Grupo";E={$_.Group}}, `
    @{L="ID";E={$_.Id}}, `
    @{L="Status";E={$_.Status}}, `
    @{L="Estado";E={$_.Estado}}, `
    @{L="Tdn1";E={$_.Tdn1}}, `
    @{L="Tdn2";E={$_.Tdn2}}, `
    @{L="Nivel";E={$_.Nivel}}, `
    @{L="Proveedor";E={$_.Proveedor}}, `
    @{L="RecorteApl";E={$_.RecorteAplicado}}, `
    @{L="Pags";E={$_.PaginasIncluidas}}, `
    @{L="Reutilizada";E={$_.Reutilizada}}, `
    @{L="s";E={$_.ElapsedSec}}, `
    @{L="Razon";E={if($_.Status -ne "PASS"){$_.Reason}else{""}}}

# Guardar resumen CSV
$csvPath = Join-Path $ArtifactsDir "summary-$(Get-Date -Format 'yyyyMMdd-HHmmss').csv"
$results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8 -Force
Write-Host "  Resumen CSV: $csvPath" -ForegroundColor Gray

# FAILs detallados
$failedCases = @($results | Where-Object { $_.Status -eq "FAIL" })
if ($failedCases.Count -gt 0) {
    Write-Host ""
    Write-Host "  FALLOS DETALLADOS:" -ForegroundColor Red
    foreach ($f in $failedCases) {
        Write-Host "  [$($f.Group)-$($f.Id)] $($f.Name)" -ForegroundColor Red
        Write-Host "    Razon : $($f.Reason)" -ForegroundColor Yellow
        if ($f.ArtifactPath) {
            Write-Host "    Artefacto: $($f.ArtifactPath)" -ForegroundColor Gray
        }
    }
}

# Publicar en ADO si se solicito
if ($PublishToAdo) {
    Write-Host ""
    Write-Host "  Publicando en ADO Test Plan $AdoTestPlanId..." -ForegroundColor Cyan
    Publish-AdoTestRun -Results $results -TestPlanId $AdoTestPlanId -Org $AdoOrg -Project $AdoProject -Pat $AdoPat
}

# Exit code
if ($Strict.IsPresent -and ($fail -gt 0 -or $skip -gt 0)) { exit 2 }
if ($fail -gt 0) { exit 1 }
exit 0
