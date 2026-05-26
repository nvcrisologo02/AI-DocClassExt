[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

function New-AdoAuthHeaders {
    param([string]$Pat, [string]$ContentType = "application/json")
    if ([string]::IsNullOrWhiteSpace($Pat)) {
        throw "AdoPat no informado. Define -AdoPat o `$env:ADO_PAT con permisos Test Read/Write."
    }
    $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$Pat"))
    return @{
        Authorization  = "Basic $base64Auth"
        Accept         = "application/json"
        "Content-Type" = $ContentType
    }
}

function New-AdoTestManagementHeaders {
    param([string]$Pat)
    $headers = New-AdoAuthHeaders -Pat $Pat
    $headers.Accept = "application/json; api-version=7.0"
    return $headers
}

function Get-AdoProjectPath {
    param([string]$Org, [string]$Project)
    return "$Org/$([Uri]::EscapeDataString($Project))"
}

function Assert-AdoCanonicalPlan {
    param(
        [string]$Org,
        [string]$Project,
        [string]$Pat,
        [int]$PlanId,
        [int]$RootSuiteId,
        [string]$ExpectedName
    )

    $headers = New-AdoAuthHeaders -Pat $Pat
    $projectPath = Get-AdoProjectPath -Org $Org -Project $Project
    $planUri = "$projectPath/_apis/testplan/plans/${PlanId}?api-version=7.0"
    $plan = Invoke-RestMethod -Method Get -Uri $planUri -Headers $headers -ErrorAction Stop

    if ($plan.id -ne $PlanId) { throw "ADO: plan ${PlanId} no coincide con respuesta id=$($plan.id)." }
    if ($RootSuiteId -gt 0 -and [int]$plan.rootSuite.id -ne $RootSuiteId) {
        throw "ADO: rootSuite esperado ${RootSuiteId}, recibido $($plan.rootSuite.id)."
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedName) -and $plan.name -ne $ExpectedName) {
        throw "ADO: nombre de plan esperado '$ExpectedName', recibido '$($plan.name)'."
    }

    return $plan
}

function Get-AdoPlanSuites {
    param([string]$Org, [string]$Project, [string]$Pat, [int]$PlanId)

    $headers = New-AdoAuthHeaders -Pat $Pat
    $projectPath = Get-AdoProjectPath -Org $Org -Project $Project
    $uri = "$projectPath/_apis/testplan/Plans/${PlanId}/suites?api-version=7.0"
    return (Invoke-RestMethod -Method Get -Uri $uri -Headers $headers -ErrorAction Stop).value
}

function Get-AdoSuiteTestCases {
    param([string]$Org, [string]$Project, [string]$Pat, [int]$PlanId, [int]$SuiteId)

    $headers = New-AdoTestManagementHeaders -Pat $Pat
    $projectPath = Get-AdoProjectPath -Org $Org -Project $Project
    $uri = "$projectPath/_apis/test/Plans/${PlanId}/suites/${SuiteId}/testcases"
    return (Invoke-RestMethod -Method Get -Uri $uri -Headers $headers -ErrorAction Stop).value
}

function Get-AdoPlanPointsByTestCase {
    param([string]$Org, [string]$Project, [string]$Pat, [int]$PlanId)

    $headers = New-AdoAuthHeaders -Pat $Pat
    $projectPath = Get-AdoProjectPath -Org $Org -Project $Project
    $apiBase = "$projectPath/_apis/test"
    $suites = Get-AdoPlanSuites -Org $Org -Project $Project -Pat $Pat -PlanId $PlanId
    $pointByTestCase = @{}

    foreach ($suite in @($suites)) {
        if ($null -eq $suite.parentSuite) { continue }
        $pointsUri = "$apiBase/Plans/${PlanId}/Suites/$($suite.id)/points?api-version=7.1"
        $points = Invoke-RestMethod -Uri $pointsUri -Method Get -Headers $headers -ErrorAction Stop
        foreach ($point in @($points.value)) {
            if ($null -ne $point.testCase -and $null -ne $point.testCase.id) {
                $testCaseId = [int]$point.testCase.id
                if (-not $pointByTestCase.ContainsKey($testCaseId)) {
                    $pointByTestCase[$testCaseId] = [pscustomobject]@{
                        TestPointId = [int]$point.id
                        SuiteId     = [int]$suite.id
                        SuiteName   = $suite.name
                    }
                }
            }
        }
    }

    return $pointByTestCase
}

function Assert-AdoCaseMappings {
    param(
        [array]$Cases,
        [string]$Org,
        [string]$Project,
        [string]$Pat,
        [int]$PlanId,
        [int]$RootSuiteId,
        [string]$ExpectedPlanName
    )

    $plan = Assert-AdoCanonicalPlan -Org $Org -Project $Project -Pat $Pat -PlanId $PlanId -RootSuiteId $RootSuiteId -ExpectedName $ExpectedPlanName
    $suites = Get-AdoPlanSuites -Org $Org -Project $Project -Pat $Pat -PlanId $PlanId
    $suiteById = @{}
    foreach ($suite in @($suites)) { $suiteById[[int]$suite.id] = $suite }

    $pointByTestCase = Get-AdoPlanPointsByTestCase -Org $Org -Project $Project -Pat $Pat -PlanId $PlanId
    $errors = @()

    foreach ($case in @($Cases)) {
        $caseKey = if (-not [string]::IsNullOrWhiteSpace($case.caseKey)) { $case.caseKey } else { "$($case.group)-$($case.id)" }
        if ($null -eq $case.suiteId -or [int]$case.suiteId -le 0) { $errors += "${caseKey}: suiteId no informado"; continue }
        if ($null -eq $case.testCaseId -or [int]$case.testCaseId -le 0) { $errors += "${caseKey}: testCaseId no informado"; continue }
        if (-not $suiteById.ContainsKey([int]$case.suiteId)) { $errors += "${caseKey}: suiteId $($case.suiteId) no existe en plan ${PlanId}" }
        if (-not $pointByTestCase.ContainsKey([int]$case.testCaseId)) { $errors += "${caseKey}: testCaseId $($case.testCaseId) no tiene test point en plan ${PlanId}" }
    }

    if ($errors.Count -gt 0) {
        throw "ADO: mapeo invalido.`n - $($errors -join "`n - ")"
    }

    return [pscustomobject]@{
        Plan            = $plan
        Suites          = $suites
        PointByTestCase = $pointByTestCase
    }
}

function Publish-AdoTestPlanResults {
    param(
        [array]$Results,
        [string]$Org,
        [string]$Project,
        [string]$Pat,
        [int]$PlanId,
        [int]$RootSuiteId,
        [string]$ExpectedPlanName,
        [string]$RunName,
        [string]$AutomatedTestStorage,
        [string]$ArtifactsDir
    )

    $casesForValidation = @($Results | ForEach-Object {
        [pscustomobject]@{ caseKey = $_.CaseKey; group = $_.Group; id = $_.Id; suiteId = $_.SuiteId; testCaseId = $_.TestCaseId }
    })
    $validation = Assert-AdoCaseMappings -Cases $casesForValidation -Org $Org -Project $Project -Pat $Pat -PlanId $PlanId -RootSuiteId $RootSuiteId -ExpectedPlanName $ExpectedPlanName
    $pointByTestCase = $validation.PointByTestCase

    $headers = New-AdoAuthHeaders -Pat $Pat
    $projectPath = Get-AdoProjectPath -Org $Org -Project $Project
    $apiBase = "$projectPath/_apis/test"
    $apiVer = "?api-version=7.1"

    $linkedResults = @()
    foreach ($result in @($Results)) {
        $testCaseId = [int]$result.TestCaseId
        $point = $pointByTestCase[$testCaseId]
        $linkedResults += [pscustomobject]@{
            RawResult   = $result
            TestCaseId  = $testCaseId
            TestPointId = [int]$point.TestPointId
        }
    }

    $pointIds = @($linkedResults | ForEach-Object { $_.TestPointId } | Sort-Object -Unique)
    $runBody = @{
        name        = $RunName
        plan        = @{ id = $PlanId }
        pointIds    = $pointIds
        isAutomated = $true
    } | ConvertTo-Json -Depth 6

    $run = Invoke-RestMethod -Uri "$apiBase/runs$apiVer" -Method Post -Headers $headers -Body $runBody -ErrorAction Stop
    $runId = [int]$run.id

    $existingResults = Invoke-RestMethod -Uri "$apiBase/runs/${runId}/results?`$top=1000&api-version=7.1" -Method Get -Headers $headers -ErrorAction Stop
    $resultByPoint = @{}
    foreach ($existingResult in @($existingResults.value)) {
        if ($null -ne $existingResult.testPoint -and $null -ne $existingResult.testPoint.id) {
            $resultByPoint[[int]$existingResult.testPoint.id] = [int]$existingResult.id
        }
    }

    $missing = @()
    $patchPayload = @()
    foreach ($linked in @($linkedResults)) {
        $raw = $linked.RawResult
        $pointId = [int]$linked.TestPointId
        if (-not $resultByPoint.ContainsKey($pointId)) {
            $missing += "Sin result precreado para pointId=${pointId} (caso $($raw.CaseKey))"
            continue
        }

        $outcome = switch ($raw.Status) {
            "PASS" { "Passed" }
            "FAIL" { "Failed" }
            "SKIP" { "NotApplicable" }
            default { "Unspecified" }
        }

        $patchPayload += @{
            id                   = [int]$resultByPoint[$pointId]
            outcome              = $outcome
            state                = "Completed"
            comment              = if ($raw.Reason) { "$($raw.Reason)" } else { "" }
            durationInMs         = if ($raw.ElapsedSec) { [int]($raw.ElapsedSec * 1000) } else { 0 }
            errorMessage         = if ($raw.Status -eq "FAIL") { $raw.Reason } else { $null }
            automatedTestName    = "$ExpectedPlanName.$($raw.CaseKey)"
            automatedTestStorage = $AutomatedTestStorage
        }
    }

    if ($missing.Count -gt 0) {
        throw "ADO: faltan resultados precreados.`n - $($missing -join "`n - ")"
    }

    $resultsBody = ConvertTo-Json -InputObject @($patchPayload) -Depth 10
    Invoke-RestMethod -Uri "$apiBase/runs/${runId}/results$apiVer" -Method Patch -Headers $headers -Body $resultsBody -ErrorAction Stop | Out-Null
    Invoke-RestMethod -Uri "$apiBase/runs/${runId}$apiVer" -Method Patch -Headers $headers -Body (@{ state = "Completed" } | ConvertTo-Json) -ErrorAction Stop | Out-Null

    $runSummary = [pscustomobject]@{
        runId     = $runId
        planId    = $PlanId
        rootSuite = $RootSuiteId
        pointIds  = $pointIds
        runUrl    = "$Org/$([Uri]::EscapeDataString($Project))/_testManagement/runs?runId=$runId"
        results   = $linkedResults | ForEach-Object {
            [pscustomobject]@{
                caseKey     = $_.RawResult.CaseKey
                testCaseId  = $_.TestCaseId
                testPointId = $_.TestPointId
                status      = $_.RawResult.Status
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ArtifactsDir)) {
        $runPath = Join-Path $ArtifactsDir "ado-run-${runId}.json"
        $runSummary | ConvertTo-Json -Depth 10 | Out-File -FilePath $runPath -Encoding UTF8 -Force
    }

    return $runSummary
}
