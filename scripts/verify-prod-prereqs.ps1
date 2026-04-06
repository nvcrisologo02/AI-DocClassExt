param(
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId = "",

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "SRBRGDOCSAIPROD",

    [Parameter(Mandatory = $false)]
    [string]$FunctionAppName = "srbappprodocai",

    [Parameter(Mandatory = $false)]
    [string]$KeyVaultName = "srbkvprodocai",

    [Parameter(Mandatory = $false)]
    [string]$StorageAccountName = "srbstgproapppdocai"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "No se encontro el comando requerido: az"
}

function Invoke-AzJson {
    param([string[]]$AzArgs)

    $result = & az @AzArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($AzArgs -join ' ')`n$result"
    }

    $text = ($result | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text | ConvertFrom-Json
}

function Test-SecretExists {
    param(
        [string]$VaultName,
        [string]$SecretName
    )

    $result = & az keyvault secret show --vault-name $VaultName --name $SecretName --query id -o tsv 2>$null
    return ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($result | Out-String).Trim()))
}

$requiredSecrets = @(
    "AzureWebJobsStorage",
    "AzureStorageConnectionString",
    "SqlConnectionString",
    "Extraction--AzureContentUnderstanding--ApiKey",
    "Extraction--GptFallback--ApiKey",
    "Classification--AzureDocumentIntelligence--ApiKey",
    "Classification--GptFallback--ApiKey",
    "GDC--Username",
    "GDC--Password",
    "GDC--HttpBasicUsername",
    "GDC--HttpBasicPassword"
)

Write-Host "== Verificando prerrequisitos de despliegue PROD ==" -ForegroundColor Cyan

if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    az account set --subscription $SubscriptionId --only-show-errors | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo seleccionar la suscripcion $SubscriptionId"
    }
}

$currentAccount = Invoke-AzJson -AzArgs @("account", "show", "-o", "json")
if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $SubscriptionId = [string]$currentAccount.id
}

Write-Host "Subscription: $SubscriptionId"
Write-Host "ResourceGroup: $ResourceGroup"
Write-Host "FunctionApp: $FunctionAppName"
Write-Host "KeyVault: $KeyVaultName"
Write-Host "Storage: $StorageAccountName"

Write-Host ""
Write-Host "[1/5] Verificando Function App..." -ForegroundColor Yellow
$functionApp = Invoke-AzJson -AzArgs @(
    "functionapp", "show",
    "--resource-group", $ResourceGroup,
    "--name", $FunctionAppName,
    "-o", "json"
)
Write-Host "  [OK] $($functionApp.name)"

Write-Host "[2/5] Verificando Key Vault..." -ForegroundColor Yellow
$keyVault = Invoke-AzJson -AzArgs @(
    "keyvault", "show",
    "--resource-group", $ResourceGroup,
    "--name", $KeyVaultName,
    "-o", "json"
)
Write-Host "  [OK] $($keyVault.name)"

Write-Host "[3/5] Verificando Storage Account..." -ForegroundColor Yellow
$storage = Invoke-AzJson -AzArgs @(
    "storage", "account", "show",
    "--resource-group", $ResourceGroup,
    "--name", $StorageAccountName,
    "-o", "json"
)
Write-Host "  [OK] $($storage.name)"

Write-Host "[4/5] Verificando secretos obligatorios en Key Vault..." -ForegroundColor Yellow
$missingSecrets = @()
foreach ($secretName in $requiredSecrets) {
    if (Test-SecretExists -VaultName $KeyVaultName -SecretName $secretName) {
        Write-Host "  [OK] $secretName" -ForegroundColor Green
    }
    else {
        Write-Host "  [FALTA] $secretName" -ForegroundColor Red
        $missingSecrets += $secretName
    }
}

if ($missingSecrets.Count -gt 0) {
    throw "Faltan secretos obligatorios en Key Vault: $($missingSecrets -join ', ')"
}

Write-Host "[5/5] Verificando app settings conflictivos del host..." -ForegroundColor Yellow
$appSettings = Invoke-AzJson -AzArgs @(
    "functionapp", "config", "appsettings", "list",
    "--resource-group", $ResourceGroup,
    "--name", $FunctionAppName,
    "-o", "json"
)

$conflictingNames = @("AzureWebJobsStorage__accountName", "AzureWebJobsStorage__credential")
$conflicts = @($appSettings | Where-Object { $conflictingNames -contains $_.name })
if ($conflicts.Count -gt 0) {
    Write-Warning "Se han detectado settings conflictivos del host: $($conflicts.name -join ', ')"
    Write-Warning "El pipeline los eliminara en el paso de configuracion posterior al deploy."
}
else {
    Write-Host "  [OK] Sin settings conflictivos detectados" -ForegroundColor Green
}

Write-Host ""
Write-Host "Prerrequisitos verificados correctamente." -ForegroundColor Green
