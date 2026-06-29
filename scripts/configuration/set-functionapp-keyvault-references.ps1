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

$tempJsonPath = Join-Path ([System.IO.Path]::GetTempPath()) ("appsettings-" + [guid]::NewGuid().ToString("N") + ".json")
try {
    # Convertir array a hash y luego a JSON para evitar problemas con caracteres especiales en cmd.
    $settingsHash = @{}
    foreach ($setting in $settings) {
        $parts = $setting -split "=", 2
        $key = $parts[0]
        $value = if ($parts.Count -gt 1) { $parts[1] } else { "" }
        $settingsHash[$key] = $value
    }

    $settingsJson = $settingsHash | ConvertTo-Json -Depth 10 -Compress
    Set-Content -LiteralPath $tempJsonPath -Value $settingsJson -Encoding utf8NoBOM -NoNewline

    # Pasar settings por JSON file evita rotura en cmd con caracteres especiales.
    az functionapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $FunctionAppName `
        --settings "@$tempJsonPath" `
        --only-show-errors `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Fallo al aplicar el modo Key Vault en $FunctionAppName"
    }
}
finally {
    if (Test-Path -LiteralPath $tempJsonPath) {
        Remove-Item -LiteralPath $tempJsonPath -Force -ErrorAction SilentlyContinue
    }
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
