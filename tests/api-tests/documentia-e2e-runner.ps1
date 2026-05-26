function Write-E2ECaseHeader {
    param([string]$CaseKey, [string]$Name)
    Write-Host ""
    Write-Host "  [$CaseKey] $Name" -ForegroundColor Cyan
}

function Write-E2ECaseResult {
    param([string]$Status, [string]$Reason)
    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        default { "Gray" }
    }
    Write-Host "  --> $Status : $Reason" -ForegroundColor $color
}

function Invoke-DocumentIAE2EBattery {
    param(
        [string]$BatteryName,
        [string]$CasesFile,
        [string]$Endpoint,
        [int]$MaxRetries,
        [int]$DelaySeconds,
        [string[]]$Groups,
        [string]$ArtifactsDir,
        [switch]$Strict,
        [switch]$PublishToAdo,
        [switch]$ValidateAdoOnly,
        [string]$AdoOrg,
        [string]$AdoProject,
        [string]$AdoPat,
        [int]$AdoTestPlanId,
        [int]$AdoRootSuiteId,
        [string]$AdoPlanName,
        [string]$ScriptName
    )

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "  Bateria E2E - $BatteryName" -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "  CasesFile : $CasesFile"
    Write-Host "  Endpoint  : $Endpoint"
    Write-Host "  Grupos    : $($Groups -join ', ')"
    Write-Host "  Artifacts : $ArtifactsDir"
    Write-Host ""

    if (-not (Test-Path -Path $CasesFile)) {
        Write-Host "[ERROR] No existe $CasesFile" -ForegroundColor Red
        exit 1
    }

    if (-not (Test-Path -Path $ArtifactsDir)) {
        New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null
    }

    $allCases = Get-Content -Raw -Path $CasesFile | ConvertFrom-Json
    $selectedCases = @($allCases | Where-Object { $Groups -contains $_.group })
    if ($selectedCases.Count -eq 0) {
        Write-Host "[WARN] Sin casos para los grupos: $($Groups -join ', ')" -ForegroundColor Yellow
        exit 0
    }

    if ($PublishToAdo -or $ValidateAdoOnly) {
        Write-Host "[ADO] Validando plan canonico y mapeos antes de ejecutar..." -ForegroundColor Cyan
        Assert-AdoCaseMappings -Cases $selectedCases -Org $AdoOrg -Project $AdoProject -Pat $AdoPat -PlanId $AdoTestPlanId -RootSuiteId $AdoRootSuiteId -ExpectedPlanName $AdoPlanName | Out-Null
        Write-Host "[ADO] Plan, suites y test points validados." -ForegroundColor Green
        if ($ValidateAdoOnly) {
            Write-Host "[ADO] ValidateAdoOnly activo: no se ejecutan casos funcionales ni se publica run." -ForegroundColor Yellow
            return
        }
    }

    $results = @()
    $currentGroup = ""
    foreach ($case in $selectedCases) {
        if ($case.group -ne $currentGroup) {
            $currentGroup = $case.group
            Write-Host ""
            Write-Host "--- Grupo $currentGroup ---" -ForegroundColor Magenta
        }

        $caseKey = if (-not [string]::IsNullOrWhiteSpace($case.caseKey)) { $case.caseKey } else { "$($case.group)-$($case.id)" }
        Write-E2ECaseHeader -CaseKey $caseKey -Name $case.name
        $result = Invoke-DocumentIAE2ECase -Case $case -Endpoint $Endpoint -ArtifactsDir $ArtifactsDir -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds
        $results += $result
        Write-E2ECaseResult -Status $result.Status -Reason $result.Reason
    }

    $pass = @($results | Where-Object { $_.Status -eq "PASS" }).Count
    $fail = @($results | Where-Object { $_.Status -eq "FAIL" }).Count
    $skip = @($results | Where-Object { $_.Status -eq "SKIP" }).Count
    $total = $results.Count

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "  RESUMEN: Total=$total  PASS=$pass  FAIL=$fail  SKIP=$skip" -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host ""

    $results | Format-Table -AutoSize -Property `
        @{L="Caso";E={$_.CaseKey}}, `
        @{L="Status";E={$_.Status}}, `
        @{L="Estado";E={$_.Estado}}, `
        @{L="TC";E={$_.TestCaseId}}, `
        @{L="Suite";E={$_.SuiteId}}, `
        @{L="s";E={$_.ElapsedSec}}, `
        @{L="Razon";E={if($_.Status -ne "PASS"){$_.Reason}else{""}}}

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $csvPath = Join-Path $ArtifactsDir "summary-$timestamp.csv"
    $jsonPath = Join-Path $ArtifactsDir "summary-$timestamp.json"
    $results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8 -Force
    $results | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonPath -Encoding UTF8 -Force
    Write-Host "  Resumen CSV : $csvPath" -ForegroundColor Gray
    Write-Host "  Resumen JSON: $jsonPath" -ForegroundColor Gray

    $failedCases = @($results | Where-Object { $_.Status -eq "FAIL" })
    if ($failedCases.Count -gt 0) {
        Write-Host ""
        Write-Host "  FALLOS DETALLADOS:" -ForegroundColor Red
        foreach ($failed in $failedCases) {
            Write-Host "  [$($failed.CaseKey)] $($failed.Name)" -ForegroundColor Red
            Write-Host "    Razon : $($failed.Reason)" -ForegroundColor Yellow
            if ($failed.ArtifactPath) { Write-Host "    Artefacto: $($failed.ArtifactPath)" -ForegroundColor Gray }
        }
    }

    if ($PublishToAdo) {
        Write-Host ""
        Write-Host "[ADO] Publicando resultados en Test Plan $AdoTestPlanId..." -ForegroundColor Cyan
        $runName = "$BatteryName E2E - $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        $run = Publish-AdoTestPlanResults -Results $results -Org $AdoOrg -Project $AdoProject -Pat $AdoPat -PlanId $AdoTestPlanId -RootSuiteId $AdoRootSuiteId -ExpectedPlanName $AdoPlanName -RunName $runName -AutomatedTestStorage $ScriptName -ArtifactsDir $ArtifactsDir
        Write-Host "[ADO] Run publicado: $($run.runId)" -ForegroundColor Green
        Write-Host "[ADO] URL: $($run.runUrl)" -ForegroundColor Green
    }

    if ($Strict.IsPresent -and ($fail -gt 0 -or $skip -gt 0)) { exit 2 }
    if ($fail -gt 0) { exit 1 }
}
