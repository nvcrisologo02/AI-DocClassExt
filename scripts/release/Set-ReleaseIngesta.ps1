<#
.SYNOPSIS
    Pausa o reactiva la ingesta HTTP (funcion IngestDocument) de la Function App.

.DESCRIPTION
    Pausada: fija el app setting estandar AzureWebJobs.IngestDocument.Disabled=true.
    Activa: borra ese app setting. IngestDocument es el nombre de [Function("IngestDocument")]
    en src/backend/DocumentIA.Functions/Triggers/IngestAPITrigger.cs.
    Tras el cambio relee el setting y comprueba que quedo como se pidio.
    Codigo de salida 0 si el estado final es el pedido y 2 si falla az o la relectura no coincide.

.PARAMETER ResourceGroup
    Resource group de la Function App.

.PARAMETER FunctionApp
    Nombre de la Function App.

.PARAMETER Estado
    Pausada o Activa.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Set-ReleaseIngesta.ps1 -ResourceGroup SRBRGPREDOCSAI -FunctionApp srbapppredocai -Estado Pausada
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][string]$FunctionApp,
    [Parameter(Mandatory)][ValidateSet('Pausada', 'Activa')][string]$Estado
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

$setting = 'AzureWebJobs.IngestDocument.Disabled'

if ($Estado -eq 'Pausada') {
    $argumentos = @('functionapp', 'config', 'appsettings', 'set', '-n', $FunctionApp, '-g', $ResourceGroup, '--settings', "$setting=true")
}
else {
    $argumentos = @('functionapp', 'config', 'appsettings', 'delete', '-n', $FunctionApp, '-g', $ResourceGroup, '--setting-names', $setting)
}
$null = Invoke-ReleaseAz -Arguments $argumentos
if ($LASTEXITCODE -ne 0) {
    Write-Host "No se pudo cambiar el estado de la ingesta a $Estado en $FunctionApp"
    exit 2
}

# Relectura: el resultado puede ser $null, un escalar o un array (Invoke-ReleaseAz desenrolla arrays de un elemento).
$valores = Invoke-ReleaseAz -Arguments @('functionapp', 'config', 'appsettings', 'list', '-n', $FunctionApp, '-g', $ResourceGroup, '--query', "[?name=='$setting'].value")
if ($LASTEXITCODE -ne 0) {
    Write-Host "No se pudo releer el setting $setting en $FunctionApp"
    exit 2
}
$valores = @($valores | Where-Object { $null -ne $_ })

if ($Estado -eq 'Pausada') {
    $ok = ($valores.Count -eq 1) -and ([string]$valores[0] -ieq 'true')
}
else {
    $ok = ($valores.Count -eq 0)
}
if (-not $ok) {
    Write-Host "La relectura de $setting no coincide con el estado $Estado en $FunctionApp"
    exit 2
}

Write-Host "Ingesta $Estado en $FunctionApp"
exit 0
