<#
.SYNOPSIS
    Puente entre run-e2e-postdeploy.ps1 y ado-testplans-common.ps1: publica
    los resultados de una ejecucion como Test Run en el plan espejo de ADO.
.DESCRIPTION
    Requiere que exista cases/ado-mapping.json (generado por
    bootstrap-testplan.ps1) y una credencial ADO. La credencial se resuelve
    con precedencia: -Pat > $env:ADO_PAT > "adoPat" en environments.json >
    token AAD de la sesion 'az' activa (mismo patron que bootstrap-testplan.ps1,
    resource 499b84ac-1321-427f-aa17-267ca6975798). Si no hay Pat pero si hay
    token AAD, se publica igualmente con Bearer (ado-testplans-common.ps1
    admite ambos). Si no hay ninguna credencial disponible, la publicacion se
    omite (no es un fallo del run).
.NOTES
    No falla el run si la publicacion no procede: solo escribe un aviso y
    retorna, para no penalizar la bateria E2E por falta de credenciales ADO.
#>
param(
    [Parameter(Mandatory = $true)][array]$Results,
    [Parameter(Mandatory = $true)][string]$ArtifactsDir,
    [string]$Org = "https://sareb.visualstudio.com",
    [string]$Project = "AI DocClassExt",
    [string]$Pat = "",
    [string]$ConfigPath = ""
)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# Fallback AAD duplicado desde bootstrap-testplan.ps1 (scripts hermanos, sin
# dependencia cruzada).
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

# adoPat es un campo top-level opcional de environments.json (no por entorno).
function Get-AdoPatFromConfig {
    param([string]$ConfigPath)
    if ([string]::IsNullOrWhiteSpace($ConfigPath) -or -not (Test-Path -Path $ConfigPath)) { return "" }
    try {
        $cfg = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
    }
    catch { return "" }
    if ($null -ne $cfg.PSObject.Properties['adoPat']) { return [string]$cfg.adoPat }
    return ""
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot ".." "config" "environments.json"
}

# Precedencia: -Pat > $env:ADO_PAT > adoPat (environments.json) > token AAD ('az').
$resolvedPat = $Pat
if ([string]::IsNullOrWhiteSpace($resolvedPat)) { $resolvedPat = $env:ADO_PAT }
if ([string]::IsNullOrWhiteSpace($resolvedPat)) { $resolvedPat = Get-AdoPatFromConfig -ConfigPath $ConfigPath }

$resolvedBearer = ""
if ([string]::IsNullOrWhiteSpace($resolvedPat)) {
    $resolvedBearer = Get-AdoBearerToken
}

if ([string]::IsNullOrWhiteSpace($resolvedPat) -and [string]::IsNullOrWhiteSpace($resolvedBearer)) {
    Write-Host "[ADO] Falta credencial ADO (Pat/ADO_PAT/adoPat/sesion 'az'); no se publica." -ForegroundColor Yellow
    return
}
if (-not [string]::IsNullOrWhiteSpace($resolvedBearer)) {
    Write-Host "[ADO] No hay Pat; publicando con token AAD de la sesion 'az' (Bearer)." -ForegroundColor DarkGray
}

. (Join-Path $PSScriptRoot ".." ".." "api-tests" "ado-testplans-common.ps1")

$mappingPath = Join-Path $PSScriptRoot ".." "cases" "ado-mapping.json"
if (-not (Test-Path $mappingPath)) {
    Write-Host "[ADO] Falta ado-mapping.json (ejecutar bootstrap-testplan.ps1)." -ForegroundColor Yellow
    return
}
$mapping = Get-Content -Raw $mappingPath | ConvertFrom-Json

# Enriquecer resultados con TestCaseId/SuiteId a partir del mapeo (excluye el
# pseudo-caso de health -no forma parte de la bateria mapeada- y cualquier
# caso sin entrada en el mapping). Se copian a objetos nuevos (no se muta
# $Results) porque Publish-AdoTestPlanResults solo reconoce "PASS"/"FAIL"/
# "SKIP" (SKIP -> NotApplicable); el estado "NA" que usa el runner para casos
# con requisito no activo (p.ej. gdc) se normaliza aqui a "SKIP" para que
# tambien se registre como NotApplicable en vez de caer en el "Unspecified"
# por defecto de la libreria comun.
$publishable = @()
foreach ($r in $Results) {
    $entry = $mapping.cases.PSObject.Properties[$r.CaseKey]
    if ($null -eq $entry) { continue }
    $status = if ($r.Status -eq "NA") { "SKIP" } else { $r.Status }
    $publishable += [pscustomobject]@{
        CaseKey    = $r.CaseKey
        Group      = $r.Group
        Id         = $r.Id
        Status     = $status
        Reason     = $r.Reason
        ElapsedSec = if ($r.PSObject.Properties['ElapsedSec']) { $r.ElapsedSec } else { $null }
        TestCaseId = [int]$entry.Value.testCaseId
        SuiteId    = [int]$entry.Value.suiteId
    }
}
if ($publishable.Count -eq 0) {
    Write-Host "[ADO] Nada que publicar (ningun resultado tiene mapeo en ado-mapping.json)." -ForegroundColor Yellow
    return
}

$runName = "E2E-PostDeploy - $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
$run = Publish-AdoTestPlanResults -Results $publishable -Org $Org -Project $Project -Pat $resolvedPat -BearerToken $resolvedBearer `
    -PlanId $mapping.planId -RootSuiteId $mapping.rootSuiteId -ExpectedPlanName $mapping.planName `
    -RunName $runName -AutomatedTestStorage "run-e2e-postdeploy.ps1" -ArtifactsDir $ArtifactsDir
Write-Host "[ADO] Run publicado: $($run.runId) - $($run.runUrl)" -ForegroundColor Green
