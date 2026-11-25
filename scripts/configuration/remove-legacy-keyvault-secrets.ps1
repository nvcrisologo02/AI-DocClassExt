<#
.SYNOPSIS
    Borra de un Key Vault los 4 secretos de conexion de IA heredados (AB#101450).

.DESCRIPTION
    Los app settings que referenciaban estos secretos ya se han retirado (ver
    remove-legacy-app-settings.ps1) y el codigo no los lee: la conexion sale de ModeloConfigs.
    Este script borra, de forma idempotente, los que sigan presentes en el vault del entorno.
    Un secreto ausente se informa y no se toca. El borrado es soft delete: es recuperable
    mientras dure la retencion del vault.

.EXAMPLE
    .\remove-legacy-keyvault-secrets.ps1 -TargetEnvironment dev -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'pre', 'prod')]
    [string]$TargetEnvironment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Vaults = @{
    dev  = 'srbkvdevdocai'
    pre  = 'srbkvpredocai'
    prod = 'srbkvprodocai'
}

$LegacySecrets = @(
    'Extraction--AzureContentUnderstanding--ApiKey',
    'Extraction--GptFallback--ApiKey',
    'Classification--AzureDocumentIntelligence--ApiKey',
    'Classification--GptFallback--ApiKey'
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

$vault = $Vaults[$TargetEnvironment]

$existing = @(Invoke-AzJson -AzArgs @(
        'keyvault', 'secret', 'list',
        '--vault-name', $vault,
        '--only-show-errors',
        '--query', '[].name',
        '-o', 'json'))

$deleted = 0
foreach ($secret in $LegacySecrets) {
    if ($secret -notin $existing) {
        Write-Host "[AUSENTE] $secret"
        continue
    }

    if ($PSCmdlet.ShouldProcess("$vault/$secret", 'Borrar secreto heredado')) {
        $deleteOutput = & az keyvault secret delete --vault-name $vault --name $secret --only-show-errors -o none 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "az keyvault secret delete --vault-name $vault --name $secret`n$deleteOutput"
        }
        Write-Host "[BORRADO] $secret"
        $deleted++
    }
    else {
        Write-Host "[WHATIF] $secret"
    }
}

Write-Host "Secretos heredados borrados: $deleted de $($LegacySecrets.Count) en $vault"
Write-Host "El borrado es soft delete: se recupera con 'az keyvault secret recover --vault-name $vault --name <secreto>'."
