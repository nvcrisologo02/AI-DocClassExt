<#
.SYNOPSIS
    Publica el smoke pre-release (SMK-1..8) como Test Run en el Test Plan 100069
    a partir de los informes del runner E2E post-despliegue.
.DESCRIPTION
    La relacion SMK <-> caso del runner vive solo en cases/smk-mapping.json
    (plan, suite, testCaseId y caseKey de cada SMK). El script lee la tabla
    "## Casos" del report.md y los result-*.json de cada directorio de
    -ReportDirs y deriva el resultado de cada SMK:
      - Passed:         el caso aparece en algun informe y esta en PASS.
      - Failed:         el caso aparece y esta en FAIL (un FAIL prevalece sobre
                        un PASS si el caso sale en varios informes).
      - NotApplicable:  SMK sin caso automatizado, SMK solo de otro entorno
                        (soloEntornos) o caso ausente de todos los informes
                        ("sin resultado en los informes"). Un caso ausente
                        nunca cuenta como Passed.
    El PASS/FAIL sale de la tabla del report.md; los result-*.json aportan el
    detalle (runtimeStatus, instanceId) para el comentario.
    Credencial: -Pat > $env:ADO_PAT > "adoPat" en environments.json > token AAD
    de la sesion 'az' (Bearer), igual que publish-results.ps1.
    Con -WhatIf imprime la tabla y termina sin ninguna llamada HTTP.
    Salida distinta de 0 si un directorio de -ReportDirs no existe.
.EXAMPLE
    pwsh ./tests/e2e-postdeploy/ado/publish-smk-run.ps1 -Environment dev -Candidate bc41f289 `
        -ReportDirs tests/e2e-postdeploy/artifacts/20261126-100941-dev-smoke,tests/e2e-postdeploy/artifacts/20261126-101546-dev-smoke -WhatIf
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet("dev", "pre", "pro")][string]$Environment,
    [Parameter(Mandatory = $true)][string]$Candidate,
    [Parameter(Mandatory = $true)][string[]]$ReportDirs,
    [string]$Version = "v1.2.0",
    [string]$Org = "https://sareb.visualstudio.com",
    [string]$Project = "AI DocClassExt",
    [string]$Pat = "",
    [string]$ConfigPath = "",
    [switch]$WhatIf
)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

function Get-AdoBearerToken {
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $token = az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv 2>$null
    }
    catch { $token = $null }
    finally { $ErrorActionPreference = $prevEap }
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) { return $null }
    return $token.Trim()
}

function Get-AdoPatFromConfig {
    param([string]$ConfigPath)
    if ([string]::IsNullOrWhiteSpace($ConfigPath) -or -not (Test-Path -Path $ConfigPath)) { return "" }
    try { $cfg = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json }
    catch { return "" }
    if ($null -ne $cfg.PSObject.Properties['adoPat']) { return [string]$cfg.adoPat }
    return ""
}

# --- Directorios de informes (validar antes de nada) -------------------------
$dirs = @($ReportDirs | ForEach-Object { $_ -split "," } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
foreach ($d in $dirs) {
    if (-not (Test-Path -Path $d -PathType Container)) {
        Write-Host "[SMK] No existe el directorio de informes: $d" -ForegroundColor Red
        exit 2
    }
}

$mappingPath = Join-Path $PSScriptRoot ".." "cases" "smk-mapping.json"
if (-not (Test-Path $mappingPath)) {
    Write-Host "[SMK] Falta cases/smk-mapping.json." -ForegroundColor Red
    exit 2
}
$mapping = Get-Content -Raw $mappingPath | ConvertFrom-Json

# --- Resultados del runner: caseKey -> @{ Status; Detail } -------------------
$found = @{}
foreach ($d in $dirs) {
    $reportPath = Join-Path $d "report.md"
    if (-not (Test-Path $reportPath)) {
        Write-Host "[SMK] Falta report.md en $d" -ForegroundColor Red
        exit 2
    }
    $inCasos = $false
    foreach ($line in (Get-Content -Path $reportPath -Encoding UTF8)) {
        if ($line -match '^## Casos\s*$') { $inCasos = $true; continue }
        if ($inCasos -and $line -match '^## ') { break }
        if (-not $inCasos -or $line -notmatch '^\|') { continue }
        $cols = @($line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
        if ($cols.Count -lt 3) { continue }
        $key = $cols[0]
        $status = $cols[2]
        if ($key -eq "Caso" -or $key -like "---*" -or @("PASS", "FAIL", "SKIP", "NA") -notcontains $status) { continue }
        $detail = "$status en $(Split-Path $d -Leaf)"
        $jsonPath = Join-Path $d "result-$key.json"
        if (Test-Path $jsonPath) {
            try {
                $j = Get-Content -Raw -Path $jsonPath -Encoding UTF8 | ConvertFrom-Json
                if ($null -ne $j.status) {
                    $detail += " (runtimeStatus=$($j.status.runtimeStatus), instancia $($j.status.instanceId))"
                }
            }
            catch { Write-Verbose "No se pudo leer $jsonPath" }
        }
        # Un FAIL prevalece sobre un PASS si el caso sale en varios informes.
        if ($found.ContainsKey($key) -and $found[$key].Status -eq "FAIL" -and $status -ne "FAIL") { continue }
        $found[$key] = @{ Status = $status; Detail = $detail }
    }
}

# --- Mapeo SMK -> resultado --------------------------------------------------
# Status usa los tokens que entiende Publish-AdoTestPlanResults (PASS/FAIL/SKIP);
# SKIP se publica como NotApplicable. Outcome es solo la etiqueta legible.
$rows = @()
foreach ($m in $mapping.cases) {
    $status = "SKIP"
    $reason = ""
    if ($null -eq $m.caseKey) {
        $reason = [string]$m.motivo
    }
    elseif ($null -ne $m.PSObject.Properties['soloEntornos'] -and @($m.soloEntornos) -notcontains $Environment) {
        $reason = "no aplica fuera de PRO"
    }
    elseif (-not $found.ContainsKey([string]$m.caseKey)) {
        $reason = "sin resultado en los informes"
    }
    else {
        $f = $found[[string]$m.caseKey]
        $status = if (@("PASS", "FAIL") -contains $f.Status) { $f.Status } else { "SKIP" }
        $reason = "$($m.caseKey): $($f.Detail)"
    }
    $outcome = switch ($status) { "PASS" { "Passed" } "FAIL" { "Failed" } default { "NotApplicable" } }
    $rows += [pscustomobject]@{
        Smk        = $m.smk
        TestCaseId = [int]$m.testCaseId
        CaseKey    = $m.caseKey
        Status     = $status
        Outcome    = $outcome
        Comment    = $reason
    }
}

$runName = "Smoke pre-release $Version ($($Environment.ToUpper()), $Candidate)"
Write-Host "[SMK] Run: $runName" -ForegroundColor Cyan
Write-Host "[SMK] Plan $($mapping.planId) '$($mapping.planName)', suite $($mapping.rootSuiteId)" -ForegroundColor Cyan
$rows | Select-Object Smk, TestCaseId, CaseKey, Outcome, Comment | Format-Table -AutoSize -Wrap | Out-String -Width 250 | Write-Host
$summary = ($rows | Group-Object Outcome | ForEach-Object { "$($_.Count) $($_.Name)" }) -join ", "
Write-Host "[SMK] Resumen: $summary"

if ($WhatIf) {
    Write-Host "[SMK] -WhatIf: no se publica nada en Azure DevOps." -ForegroundColor Yellow
    exit 0
}

# --- Credencial y publicacion ------------------------------------------------
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot ".." "config" "environments.json"
}
$resolvedPat = $Pat
if ([string]::IsNullOrWhiteSpace($resolvedPat)) { $resolvedPat = $env:ADO_PAT }
if ([string]::IsNullOrWhiteSpace($resolvedPat)) { $resolvedPat = Get-AdoPatFromConfig -ConfigPath $ConfigPath }
$resolvedBearer = ""
if ([string]::IsNullOrWhiteSpace($resolvedPat)) { $resolvedBearer = Get-AdoBearerToken }
if ([string]::IsNullOrWhiteSpace($resolvedPat) -and [string]::IsNullOrWhiteSpace($resolvedBearer)) {
    Write-Host "[SMK] Falta credencial ADO (Pat/ADO_PAT/adoPat/sesion 'az'); no se publica." -ForegroundColor Red
    exit 2
}

. (Join-Path $PSScriptRoot ".." ".." "api-tests" "ado-testplans-common.ps1")

$publishable = @($rows | ForEach-Object {
    [pscustomobject]@{
        CaseKey    = if ($_.CaseKey) { $_.CaseKey } else { $_.Smk }
        Group      = "SMK"
        Id         = $_.Smk
        Status     = $_.Status
        Reason     = $_.Comment
        ElapsedSec = $null
        TestCaseId = $_.TestCaseId
        SuiteId    = [int]$mapping.suiteId
    }
})

$artifactsDir = $dirs[-1]
$run = Publish-AdoTestPlanResults -Results $publishable -Org $Org -Project $Project -Pat $resolvedPat -BearerToken $resolvedBearer `
    -PlanId $mapping.planId -RootSuiteId $mapping.rootSuiteId -ExpectedPlanName $mapping.planName `
    -RunName $runName -AutomatedTestStorage "run-e2e-postdeploy.ps1" -ArtifactsDir $artifactsDir
Write-Host "[SMK] Run publicado: $($run.runId) - $($run.runUrl)" -ForegroundColor Green
