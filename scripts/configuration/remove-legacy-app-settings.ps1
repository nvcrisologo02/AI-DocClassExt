<#
.SYNOPSIS
    Borra de una Function App los app settings heredados que el codigo ya no lee (AB#101450).

.DESCRIPTION
    ensure-app-settings.ps1 solo crea los settings que faltan y nunca borra. Este script
    elimina, de forma idempotente, los 27 settings de conexion de IA que quedaron sin efecto
    (la conexion sale de la tabla ModeloConfigs). Si no hay ninguno presente no hace nada.
    Un unico 'az functionapp config appsettings delete' con todos los presentes.

.EXAMPLE
    .\remove-legacy-app-settings.ps1 -ResourceGroup SRBRGDEVDOCSAI -Name srbappdevdocai -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory)]
    [string]$ResourceGroup,

    [Parameter(Mandatory)]
    [string]$Name,

    [string]$SubscriptionId = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$LegacySettings = @(
    'Extraction__AzureContentUnderstanding__Endpoint',
    'Extraction__AzureContentUnderstanding__AuthMode',
    'Extraction__AzureContentUnderstanding__ApiKey',
    'Extraction__AzureContentUnderstanding__DefaultProcessingLocation',
    'Extraction__GptFallback__Enabled',
    'Extraction__GptFallback__Endpoint',
    'Extraction__GptFallback__AuthMode',
    'Extraction__GptFallback__ApiKey',
    'Extraction__GptFallback__DeploymentName',
    'Extraction__GptFallback__MinFieldsRatio',
    'Extraction__GptFallback__Temperature',
    'Extraction__GptFallback__MaxTokens',
    'Extraction__GptFallback__TimeoutSeconds',
    'Classification__DefaultModelKey',
    'Classification__AzureDocumentIntelligence__Endpoint',
    'Classification__AzureDocumentIntelligence__AuthMode',
    'Classification__AzureDocumentIntelligence__ApiKey',
    'Classification__AzureDocumentIntelligence__ApiVersion',
    'Classification__GptFallback__Enabled',
    'Classification__GptFallback__Endpoint',
    'Classification__GptFallback__AuthMode',
    'Classification__GptFallback__ApiKey',
    'Classification__GptFallback__DeploymentName',
    'Classification__GptFallback__FallbackThreshold',
    'Classification__GptFallback__Temperature',
    'Classification__GptFallback__MaxTokens',
    'Classification__GptFallback__TimeoutSeconds'
)

function Invoke-AzJson {
    param([string[]]$AzArgs)

    $result = & az @AzArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($AzArgs -join ' ')`n$result"
    }

    $lines = @($result | ForEach-Object { [string]$_ })
    $jsonStartIndex = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $trimmed = $lines[$index].TrimStart()
        if ($trimmed.StartsWith('{') -or $trimmed.StartsWith('[')) {
            $jsonStartIndex = $index
            break
        }
    }

    $text = if ($jsonStartIndex -ge 0) {
        ($lines[$jsonStartIndex..($lines.Count - 1)] -join [Environment]::NewLine).Trim()
    }
    else {
        ($lines | Out-String).Trim()
    }

    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text | ConvertFrom-Json
}

$subscriptionArgs = @()
if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $subscriptionArgs = @('--subscription', $SubscriptionId)
}

$existing = @(Invoke-AzJson -AzArgs (@(
        'functionapp', 'config', 'appsettings', 'list',
        '--resource-group', $ResourceGroup,
        '--name', $Name,
        '--only-show-errors',
        '--query', '[].name',
        '-o', 'json') + $subscriptionArgs))

$present = @($LegacySettings | Where-Object { $_ -in $existing })

if ($present.Count -eq 0) {
    Write-Host "[OK] Sin settings heredados en $Name"
    Write-Host "Settings heredados borrados: 0 de $($LegacySettings.Count)"
    exit 0
}

$deleted = 0
if ($PSCmdlet.ShouldProcess($Name, "Borrar $($present.Count) app settings heredados")) {
    $deleteArgs = @(
        'functionapp', 'config', 'appsettings', 'delete',
        '--resource-group', $ResourceGroup,
        '--name', $Name,
        '--only-show-errors',
        '--setting-names') + $present + $subscriptionArgs + @('-o', 'none')
    $deleteOutput = & az @deleteArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($deleteArgs -join ' ')`n$deleteOutput"
    }
    foreach ($s in $present) { Write-Host "[BORRADO] $s" }
    $deleted = $present.Count
}
else {
    foreach ($s in $present) { Write-Host "[WHATIF] $s" }
}

Write-Host "Settings heredados borrados: $deleted de $($LegacySettings.Count)"
