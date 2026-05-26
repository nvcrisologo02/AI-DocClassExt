<#
.SYNOPSIS
    Bateria E2E de Extraccion y Normalizacion con publicacion opcional en Azure DevOps Test Plans.

.EXAMPLE
    .\test-extraction-normalization-process.ps1
    .\test-extraction-normalization-process.ps1 -PublishToAdo -AdoPat $env:ADO_PAT
#>

param(
    [string]$CasesFile = (Join-Path $PSScriptRoot "extraction-normalization-cases.json"),
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",
    [int]$MaxRetries = 45,
    [int]$DelaySeconds = 2,
    [string[]]$Groups = @("A"),
    [string]$ArtifactsDir = (Join-Path $PSScriptRoot "artifacts\extraction-normalization"),
    [switch]$Strict,
    [switch]$PublishToAdo,
    [switch]$ValidateAdoOnly,
    [string]$AdoOrg = "https://sareb.visualstudio.com",
    [string]$AdoProject = "AI DocClassExt",
    [string]$AdoPat = $env:ADO_PAT,
    [int]$AdoTestPlanId = 99650,
    [int]$AdoRootSuiteId = 99651
)

. (Join-Path $PSScriptRoot "documentia-e2e-common.ps1")
. (Join-Path $PSScriptRoot "ado-testplans-common.ps1")
. (Join-Path $PSScriptRoot "documentia-e2e-runner.ps1")

Invoke-DocumentIAE2EBattery `
    -BatteryName "Extraccion y Normalizacion" `
    -CasesFile $CasesFile `
    -Endpoint $Endpoint `
    -MaxRetries $MaxRetries `
    -DelaySeconds $DelaySeconds `
    -Groups $Groups `
    -ArtifactsDir $ArtifactsDir `
    -Strict:$Strict `
    -PublishToAdo:$PublishToAdo `
    -ValidateAdoOnly:$ValidateAdoOnly `
    -AdoOrg $AdoOrg `
    -AdoProject $AdoProject `
    -AdoPat $AdoPat `
    -AdoTestPlanId $AdoTestPlanId `
    -AdoRootSuiteId $AdoRootSuiteId `
    -AdoPlanName "Cobertura E2E - Extracción y Normalización" `
    -ScriptName "test-extraction-normalization-process.ps1"
