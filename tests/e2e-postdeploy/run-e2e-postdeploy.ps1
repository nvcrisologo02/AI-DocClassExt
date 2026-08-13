<#
.SYNOPSIS
    Test E2E post-despliegue de DocumentIA (clasificacion y extraccion).
.DESCRIPTION
    Ejecuta la bateria de casos del perfil indicado contra el entorno indicado,
    calcula cobertura funcional contra la matriz versionada y genera report.md
    + coverage.json en artifacts/<timestamp>/.
.EXAMPLE
    pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment dev -Profile smoke
    pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile full -Parallel 2
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet("dev", "pro", "local")][string]$Environment,
    [ValidateSet("smoke", "full")][string]$Profile = "smoke",
    [switch]$IncludeGdc,
    [int]$Parallel = 1,
    [int]$MaxRetries = 60,
    [int]$DelaySeconds = 10,
    [switch]$PublishToAdo,
    [string]$CasesDir = ""
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$repoRoot   = (Resolve-Path (Join-Path $scriptRoot ".." "..")).Path

. (Join-Path $repoRoot "tests" "api-tests" "documentia-e2e-common.ps1")
. (Join-Path $scriptRoot "lib" "postdeploy-config.ps1")
. (Join-Path $scriptRoot "lib" "postdeploy-coverage.ps1")
. (Join-Path $scriptRoot "lib" "postdeploy-report.ps1")

# Las librerias anteriores activan Set-StrictMode -Version Latest en este scope
# (dot-sourcing). documentia-e2e-common.ps1 (Tarea 2) usa acceso dinamico a
# propiedades opcionales (p.ej. $assertions.expectHttpStatus) asumiendo modo
# no estricto; se desactiva aqui para no romper su comportamiento existente.
Set-StrictMode -Off

# ── Configuracion ─────────────────────────────────────────────────────────────
try {
    $envConfig = Get-E2EEnvironment -ConfigPath (Join-Path $scriptRoot "config" "environments.json") -Environment $Environment
}
catch { Write-Host "[CONFIG] $($_.Exception.Message)" -ForegroundColor Red; exit 2 }

if ($Environment -in @("dev", "pro") -and [string]::IsNullOrWhiteSpace($envConfig.FunctionKey)) {
    Write-Host "[CONFIG] functionKey vacia para '$Environment' en environments.json" -ForegroundColor Red; exit 2
}

if ([string]::IsNullOrWhiteSpace($CasesDir)) { $CasesDir = Join-Path $scriptRoot "cases" }
$startedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
$runDir = Join-Path $scriptRoot "artifacts" ("{0}-{1}-{2}" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $Environment, $Profile)
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

# ── Carga de casos ────────────────────────────────────────────────────────────
$allCases = @(Get-ChildItem -Path $CasesDir -Filter "*-cases.json" | ForEach-Object {
    Get-Content -Raw -Path $_.FullName | ConvertFrom-Json
} | ForEach-Object { $_ })

$matrix = Get-E2ECoverageMatrix -MatrixPath (Join-Path $scriptRoot "coverage" "functional-matrix.json")
try { Assert-E2ECoverageRefs -Cases $allCases -Matrix $matrix }
catch { Write-Host "[CONFIG] $($_.Exception.Message)" -ForegroundColor Red; exit 2 }

$profileCases = @($allCases | Where-Object { @($_.profiles) -contains $Profile })
$activeConditions = @(); if ($IncludeGdc) { $activeConditions += "gdc" }

# Preparar casos: ruta absoluta + marcador de trazabilidad
foreach ($case in $profileCases) {
    if ($case.PSObject.Properties['documentPath'] -and -not [string]::IsNullOrWhiteSpace($case.documentPath)) {
        $case.documentPath = Join-Path $repoRoot $case.documentPath
    }
    if ($null -eq $case.PSObject.Properties['submittedBy']) {
        $case | Add-Member -NotePropertyName submittedBy -NotePropertyValue "e2e-postdeploy"
    }
}

$endpoint = "$($envConfig.BaseUrl)/api/IngestDocument"
Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  E2E post-despliegue DocumentIA" -ForegroundColor Cyan
Write-Host "  Entorno: $Environment | Perfil: $Profile | GDC: $(if ($IncludeGdc) { 'ON' } else { 'OFF' }) | Parallel: $Parallel" -ForegroundColor Cyan
Write-Host "  Endpoint: $endpoint" -ForegroundColor Cyan
Write-Host "  Casos: $($profileCases.Count) | Artifacts: $runDir" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$results = @()

# ── Paso 0: health check (cubre OPS-01) ──────────────────────────────────────
$healthCase = [pscustomobject]@{ caseKey = "OPS-HC"; covers = @("OPS-01"); profiles = @("smoke", "full") }
if ($envConfig.HealthCheck) {
    $hostName = ([Uri]$envConfig.BaseUrl).Host
    try {
        & (Join-Path $repoRoot "scripts" "testing" "smoke-test-functions.ps1") -HostName $hostName -MaxAttempts 6 -DelaySeconds 10
        $results += [pscustomobject]@{ CaseKey = "OPS-HC"; Name = "Health check"; Status = "PASS"; Reason = "OK" }
    }
    catch {
        $results += [pscustomobject]@{ CaseKey = "OPS-HC"; Name = "Health check"; Status = "FAIL"; Reason = $_.Exception.Message }
    }
}
else {
    $results += [pscustomobject]@{ CaseKey = "OPS-HC"; Name = "Health check"; Status = "SKIP"; Reason = "healthCheck=false para $Environment" }
}

# ── Particion: N/A por requires, serial y paralelizables ─────────────────────
$naCases = @(); $runnableCases = @()
foreach ($case in $profileCases) {
    $requires = @(); if ($case.PSObject.Properties['requires']) { $requires = @($case.requires) }
    $unmet = @($requires | Where-Object { $activeConditions -notcontains $_ })
    if ($unmet.Count -gt 0) { $naCases += $case } else { $runnableCases += $case }
}
foreach ($case in $naCases) {
    Write-Host "  [$($case.caseKey)] N/A (requiere: $($case.requires -join ', '))" -ForegroundColor DarkYellow
    $results += [pscustomobject]@{ CaseKey = $case.caseKey; Name = $case.name; Status = "NA"; Reason = "requiere condicion no activa: $($case.requires -join ', ')" }
}

$serialCases   = @($runnableCases | Where-Object { $_.PSObject.Properties['serial'] -and $_.serial -eq $true })
$parallelCases = @($runnableCases | Where-Object { -not ($_.PSObject.Properties['serial'] -and $_.serial -eq $true) })

# ── Ejecucion paralela (o secuencial si -Parallel 1) ─────────────────────────
$commonPath  = Join-Path $repoRoot "tests" "api-tests" "documentia-e2e-common.ps1"
$functionKey = $envConfig.FunctionKey
if ($Parallel -gt 1 -and $parallelCases.Count -gt 1) {
    $parallelResults = $parallelCases | ForEach-Object -ThrottleLimit $Parallel -Parallel {
        . $using:commonPath
        $case = $_
        Invoke-DocumentIAE2ECase -Case $case -Endpoint $using:endpoint -ArtifactsDir $using:runDir `
            -MaxRetries $using:MaxRetries -DelaySeconds $using:DelaySeconds -FunctionKey $using:functionKey
    }
    foreach ($r in @($parallelResults)) {
        Write-Host "  [$($r.CaseKey)] $($r.Status) : $($r.Reason)" -ForegroundColor $(if ($r.Status -eq "PASS") { "Green" } elseif ($r.Status -eq "FAIL") { "Red" } else { "Yellow" })
        $results += $r
    }
}
else {
    foreach ($case in $parallelCases) {
        Write-Host ""
        Write-Host "  [$($case.caseKey)] $($case.name)" -ForegroundColor Cyan
        $r = Invoke-DocumentIAE2ECase -Case $case -Endpoint $endpoint -ArtifactsDir $runDir `
            -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds -FunctionKey $envConfig.FunctionKey
        Write-Host "  --> $($r.Status) : $($r.Reason)" -ForegroundColor $(if ($r.Status -eq "PASS") { "Green" } elseif ($r.Status -eq "FAIL") { "Red" } else { "Yellow" })
        $results += $r
    }
}

# ── Ejecucion serial (dedup y dependencias de orden) ─────────────────────────
foreach ($case in $serialCases) {
    Write-Host ""
    Write-Host "  [$($case.caseKey)] $($case.name) (serial)" -ForegroundColor Cyan
    $r = Invoke-DocumentIAE2ECase -Case $case -Endpoint $endpoint -ArtifactsDir $runDir `
        -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds -FunctionKey $envConfig.FunctionKey
    Write-Host "  --> $($r.Status) : $($r.Reason)" -ForegroundColor $(if ($r.Status -eq "PASS") { "Green" } elseif ($r.Status -eq "FAIL") { "Red" } else { "Yellow" })
    $results += $r
}

# ── Cobertura y reporte ──────────────────────────────────────────────────────
$coverageCases = @($profileCases) + $healthCase
$coverage = Get-E2ECoverage -Matrix $matrix -Cases $coverageCases -Results $results -ActiveConditions $activeConditions
$finishedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
$runInfo = [pscustomobject]@{
    Environment = $Environment; Profile = $Profile; IncludeGdc = [bool]$IncludeGdc
    StartedAtUtc = $startedAtUtc; FinishedAtUtc = $finishedAtUtc
}
$out = New-E2EReport -RunInfo $runInfo -Results $results -Coverage $coverage -OutDir $runDir

$pass = @($results | Where-Object Status -eq "PASS").Count
$fail = @($results | Where-Object Status -eq "FAIL").Count
$skip = @($results | Where-Object Status -eq "SKIP").Count
$na   = @($results | Where-Object Status -eq "NA").Count
Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  RESUMEN: Total=$($results.Count) PASS=$pass FAIL=$fail SKIP=$skip N/A=$na" -ForegroundColor Cyan
Write-Host "  COBERTURA FUNCIONAL ($Profile): $($coverage.PorcentajeCobertura)% ($($coverage.Cubiertos)/$($coverage.Aplicables) aplicables)" -ForegroundColor Cyan
Write-Host "  Reporte : $($out.ReportPath)" -ForegroundColor Gray
Write-Host "  Cobertura JSON: $($out.CoverageJsonPath)" -ForegroundColor Gray
Write-Host "========================================================" -ForegroundColor Cyan

# ── Publicacion opcional a ADO (Tarea 10 conecta esto) ───────────────────────
if ($PublishToAdo) {
    $adoScript = Join-Path $scriptRoot "ado" "publish-results.ps1"
    if (Test-Path $adoScript) {
        & $adoScript -Results $results -ArtifactsDir $runDir
    }
    else {
        Write-Host "[ADO] Publicacion no configurada todavia (falta ado/publish-results.ps1 y ado-mapping.json)." -ForegroundColor Yellow
    }
}

if ($fail -gt 0) { exit 1 }
exit 0
