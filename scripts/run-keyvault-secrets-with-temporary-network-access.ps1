param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId = "647c7246-54bc-4d31-b909-431cacf03272",

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "SRBRGDOCSAIPROD",

    [Parameter(Mandatory = $false)]
    [string]$KeyVaultName = "srbkvprodocai",

    [Parameter(Mandatory = $false)]
    [string]$SecretsScriptPath = "set-keyvault-secrets.ps1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "No se encontro el comando requerido: $Name"
    }
}

Require-Command -Name az
Require-Command -Name pwsh

if (-not (Test-Path -LiteralPath $SecretsScriptPath)) {
    throw "No se encuentra el script de secretos: $SecretsScriptPath"
}

Write-Host "== Key Vault temporary network access wrapper ==" -ForegroundColor Cyan
Write-Host "Vault: $KeyVaultName"
Write-Host "RG: $ResourceGroup"
Write-Host "Secrets script: $SecretsScriptPath"

az account set --subscription $SubscriptionId --only-show-errors | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "No se pudo seleccionar la suscripcion $SubscriptionId"
}

$kv = az keyvault show --name $KeyVaultName --resource-group $ResourceGroup --only-show-errors -o json | ConvertFrom-Json
$originalPublicNetworkAccess = [string]$kv.properties.publicNetworkAccess

# Intenta detectar bypass actual; puede venir null en algunos escenarios
$originalBypass = ""
if ($kv.properties.networkAcls -and $kv.properties.networkAcls.bypass) {
    $originalBypass = [string]$kv.properties.networkAcls.bypass
}

$ip = (Invoke-RestMethod "https://api.ipify.org").Trim()
if ([string]::IsNullOrWhiteSpace($ip)) {
    throw "No se pudo obtener la IP publica actual."
}

$ipCidr = "$ip/32"
Write-Host "IP publica detectada: $ipCidr"

$opened = $false
$ruleAdded = $false

try {
    Write-Host ""
    Write-Host "[1/4] Habilitando acceso publico temporal en Key Vault..." -ForegroundColor Yellow
    az keyvault update --name $KeyVaultName --resource-group $ResourceGroup --public-network-access Enabled --only-show-errors --output none
    if ($LASTEXITCODE -ne 0) { throw "Fallo al habilitar public network access." }
    $opened = $true

    Write-Host "[2/4] Agregando regla de red para tu IP..." -ForegroundColor Yellow
    az keyvault network-rule add --name $KeyVaultName --resource-group $ResourceGroup --ip-address $ipCidr --only-show-errors --output none
    if ($LASTEXITCODE -ne 0) { throw "Fallo al agregar regla de red para $ipCidr." }
    $ruleAdded = $true

    Start-Sleep -Seconds 10

    Write-Host "[3/4] Ejecutando carga de secretos..." -ForegroundColor Yellow
    pwsh -File $SecretsScriptPath -SubscriptionId $SubscriptionId -ResourceGroup $ResourceGroup -KeyVaultName $KeyVaultName
    if ($LASTEXITCODE -ne 0) {
        throw "El script de secretos devolvio error."
    }

    Write-Host "[4/4] Carga completada correctamente." -ForegroundColor Green
}
finally {
    Write-Host ""
    Write-Host "== Restaurando configuracion de red del Key Vault ==" -ForegroundColor Cyan

    if ($ruleAdded) {
        az keyvault network-rule remove --name $KeyVaultName --resource-group $ResourceGroup --ip-address $ipCidr --only-show-errors --output none | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Regla IP eliminada: $ipCidr"
        }
        else {
            Write-Warning "No se pudo eliminar la regla IP $ipCidr. Revisar manualmente."
        }
    }

    if ($opened) {
        az keyvault update --name $KeyVaultName --resource-group $ResourceGroup --public-network-access $originalPublicNetworkAccess --only-show-errors --output none | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "publicNetworkAccess restaurado a: $originalPublicNetworkAccess"
        }
        else {
            Write-Warning "No se pudo restaurar publicNetworkAccess. Revisar manualmente."
        }

        if (-not [string]::IsNullOrWhiteSpace($originalBypass)) {
            az keyvault update --name $KeyVaultName --resource-group $ResourceGroup --bypass $originalBypass --only-show-errors --output none | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "bypass restaurado a: $originalBypass"
            }
            else {
                Write-Warning "No se pudo restaurar bypass. Revisar manualmente."
            }
        }
    }
}

Write-Host ""
Write-Host "Proceso finalizado." -ForegroundColor Green
