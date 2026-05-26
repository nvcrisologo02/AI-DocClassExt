<#
.SYNOPSIS
    Bateria E2E de Contrato HTTP y Validaciones con publicacion opcional en Azure DevOps Test Plans.

.EXAMPLE
    .\test-http-contract-validation.ps1
    .\test-http-contract-validation.ps1 -PublishToAdo -AdoPat $env:ADO_PAT
#>

param(
    [string]$CasesFile = (Join-Path $PSScriptRoot "http-contract-validation-cases.json"),
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",
    [int]$MaxRetries = 10,
    [int]$DelaySeconds = 1,
    [string[]]$Groups = @("C"),
    [string]$ArtifactsDir = (Join-Path $PSScriptRoot "artifacts\http-contract-validation"),
    [switch]$Strict,
    [switch]$PublishToAdo,
    [switch]$ValidateAdoOnly,
    [string]$AdoOrg = "https://sareb.visualstudio.com",
    [string]$AdoProject = "AI DocClassExt",
    [string]$AdoPat = $env:ADO_PAT,
    [int]$AdoTestPlanId = 99652,
    [int]$AdoRootSuiteId = 99653
)

. (Join-Path $PSScriptRoot "documentia-e2e-common.ps1")
. (Join-Path $PSScriptRoot "ado-testplans-common.ps1")
. (Join-Path $PSScriptRoot "documentia-e2e-runner.ps1")

Invoke-DocumentIAE2EBattery `
    -BatteryName "Contrato HTTP y Validaciones" `
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
    -AdoPlanName "Cobertura E2E - Contrato HTTP y Validaciones" `
    -ScriptName "test-http-contract-validation.ps1"
