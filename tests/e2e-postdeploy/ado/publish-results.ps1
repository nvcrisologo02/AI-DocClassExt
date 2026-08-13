<#
.SYNOPSIS
    Puente entre run-e2e-postdeploy.ps1 y ado-testplans-common.ps1: publica
    los resultados de una ejecucion como Test Run en el plan espejo de ADO.
.DESCRIPTION
    Requiere que exista cases/ado-mapping.json (generado por
    bootstrap-testplan.ps1) y $env:ADO_PAT. La libreria comun
    (Publish-AdoTestPlanResults) autentica siempre con PAT/Basic; no admite
    token AAD/Bearer. Si no hay ADO_PAT, la publicacion se omite (no es un
    fallo del run) y queda documentada como paso manual/PAT-only.
.NOTES
    No falla el run si la publicacion no procede: solo escribe un aviso y
    retorna, para no penalizar la bateria E2E por falta de credenciales ADO.
#>
param(
    [Parameter(Mandatory = $true)][array]$Results,
    [Parameter(Mandatory = $true)][string]$ArtifactsDir,
    [string]$Org = "https://sareb.visualstudio.com",
    [string]$Project = "AI DocClassExt",
    [string]$Pat = $env:ADO_PAT
)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# Fallback AAD duplicado desde bootstrap-testplan.ps1 (scripts hermanos, sin
# dependencia cruzada). Solo se usa para detectar si hay sesion 'az' util y
# poder dar un mensaje preciso: Publish-AdoTestPlanResults exige PAT (Basic)
# internamente (New-AdoAuthHeaders), por lo que un token AAD no sirve aqui.
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

if ([string]::IsNullOrWhiteSpace($Pat)) {
    $tieneAad = -not [string]::IsNullOrWhiteSpace((Get-AdoBearerToken))
    if ($tieneAad) {
        Write-Host "[ADO] Falta ADO_PAT; hay sesion 'az' pero Publish-AdoTestPlanResults solo admite PAT (Basic). No se publica." -ForegroundColor Yellow
    }
    else {
        Write-Host "[ADO] Falta ADO_PAT; no se publica." -ForegroundColor Yellow
    }
    return
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
$run = Publish-AdoTestPlanResults -Results $publishable -Org $Org -Project $Project -Pat $Pat `
    -PlanId $mapping.planId -RootSuiteId $mapping.rootSuiteId -ExpectedPlanName $mapping.planName `
    -RunName $runName -AutomatedTestStorage "run-e2e-postdeploy.ps1" -ArtifactsDir $ArtifactsDir
Write-Host "[ADO] Run publicado: $($run.runId) - $($run.runUrl)" -ForegroundColor Green
