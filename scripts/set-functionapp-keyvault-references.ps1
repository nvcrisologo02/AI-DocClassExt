param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "SRBRGDOCSAIPROD",

    [Parameter(Mandatory = $false)]
    [string]$FunctionAppName = "srbappprodocai",

    [Parameter(Mandatory = $false)]
    [string]$KeyVaultName = "srbkvprodocai"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) no esta instalado o no esta en PATH."
}

Write-Host "== Configurando referencias Key Vault en Function App ==" -ForegroundColor Cyan
Write-Host "Subscription: $SubscriptionId"
Write-Host "ResourceGroup: $ResourceGroup"
Write-Host "FunctionApp: $FunctionAppName"
Write-Host "KeyVault: $KeyVaultName"

az account set --subscription $SubscriptionId --only-show-errors | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "No se pudo seleccionar la suscripcion $SubscriptionId"
}

$settings = @(
    "SecretsSource=AzureVault",
    "KeyVaultName=$KeyVaultName",
    "AzureWebJobsStorage=@Microsoft.KeyVault(VaultName=$KeyVaultName;SecretName=AzureWebJobsStorage)"
)

az functionapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --settings $settings `
    --only-show-errors `
    --output none

if ($LASTEXITCODE -ne 0) {
    throw "Fallo al aplicar el modo Key Vault en $FunctionAppName"
}

Write-Host "[OK] Modo Key Vault aplicado." -ForegroundColor Green

Write-Host "Eliminando settings conflictivos del host de Functions..." -ForegroundColor Yellow
az functionapp config appsettings delete `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --setting-names AzureWebJobsStorage__accountName AzureWebJobsStorage__credential `
    --only-show-errors `
    --output none

if ($LASTEXITCODE -ne 0) {
    throw "Fallo al eliminar settings conflictivos en $FunctionAppName"
}

Write-Host "[OK] Settings conflictivos eliminados." -ForegroundColor Green

Write-Host "Reiniciando Function App..." -ForegroundColor Yellow
az functionapp restart --resource-group $ResourceGroup --name $FunctionAppName --only-show-errors --output none
if ($LASTEXITCODE -ne 0) {
    throw "Fallo al reiniciar $FunctionAppName"
}

Write-Host "[OK] Function App reiniciada." -ForegroundColor Green

Write-Host ""
Write-Host "Siguiente validacion recomendada:" -ForegroundColor Cyan
Write-Host "0) Confirmar que AzureWebJobsStorage__accountName y __credential ya no existen"
Write-Host "1) GET /api/tipologias"
Write-Host "2) POST /api/IngestDocument"
Write-Host "3) Revisar App Insights por errores de resolucion de secretos"
