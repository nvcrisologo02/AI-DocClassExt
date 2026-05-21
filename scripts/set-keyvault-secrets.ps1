param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId = "647c7246-54bc-4d31-b909-431cacf03272",

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "SRBRGDOCSAIPROD",

    [Parameter(Mandatory = $false)]
    [string]$KeyVaultName = "srbkvprodocai",

    [Parameter(Mandatory = $false)]
    [string]$LocalSettingsPath = "",

    [Parameter(Mandatory = $false)]
    [switch]$PreferLocalSettings,

    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "No se encuentra el archivo: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-DefaultValues {
    # No hay secretos integrados en el repositorio. Los valores se toman de
    # variables de entorno DOCIA_SECRET_<nombre> o de local.settings.json.
    return @{}
}

function Convert-KeyToEnvironmentName {
    param([string]$Key)

    return "DOCIA_SECRET_" + ($Key -replace '[:\-]', '_' -replace '[^A-Za-z0-9_]', '_').ToUpperInvariant()
}

function Get-EnvironmentSecretValue {
    param([string]$Key)

    $envName = Convert-KeyToEnvironmentName -Key $Key
    $value = [Environment]::GetEnvironmentVariable($envName)
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value
}

function Get-ConfigValue {
    param(
        [object]$Config,
        [string]$Key
    )

    # 1) Prefer Values (formato local.settings estándar)
    if ($Config.Values -and $Config.Values.PSObject.Properties.Name -contains $Key) {
        $v = $Config.Values.$Key
        if (-not [string]::IsNullOrWhiteSpace([string]$v)) {
            return [string]$v
        }
    }

    # 2) Fallback a estructura anidada (secciones legacy del archivo)
    $segments = $Key.Split(':')
    $current = $Config
    foreach ($segment in $segments) {
        if (-not $current) { return $null }
        $prop = $current.PSObject.Properties[$segment]
        if (-not $prop) { return $null }
        $current = $prop.Value
    }

    if ($null -eq $current) { return $null }
    $text = [string]$current
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text
}

function Convert-KeyToSecretName {
    param([string]$Key)

    # Key Vault no admite ':'; usamos '--' para mapeo compatible con App Settings
    return ($Key -replace ':', '--' -replace '__', '--')
}

function Set-SecretSafe {
    param(
        [string]$VaultName,
        [string]$SecretName,
        [string]$SecretValue,
        [switch]$DryRun
    )

    if ($DryRun) {
        Write-Host "[WHATIF] $SecretName"
        return
    }

    & az keyvault secret set `
        --vault-name $VaultName `
        --name $SecretName `
        --value $SecretValue `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Error creando/actualizando secreto: $SecretName"
    }

    Write-Host "[OK] $SecretName"
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) no esta instalado o no esta en PATH."
}

Write-Host "== Preparando carga de secretos en Key Vault ==" -ForegroundColor Cyan
Write-Host "Vault: $KeyVaultName"
Write-Host "RG: $ResourceGroup"
Write-Host "Modo base: variables de entorno DOCIA_SECRET_* o local.settings"
if (-not [string]::IsNullOrWhiteSpace($LocalSettingsPath)) {
    Write-Host "Local settings opcional: $LocalSettingsPath"
}

$defaults = Get-DefaultValues
$cfg = $null
if ($PreferLocalSettings -and -not [string]::IsNullOrWhiteSpace($LocalSettingsPath) -and (Test-Path -LiteralPath $LocalSettingsPath)) {
    $cfg = Get-JsonFile -Path $LocalSettingsPath
    Write-Host "Usando local.settings como prioridad para sobreescribir valores por defecto." -ForegroundColor Yellow
}

# Claves sensibles a publicar en Key Vault
$keysToPublish = @(
    "SqlConnectionString",
    "AzureWebJobsStorage",
    "AzureStorageConnectionString",
    "Extraction:AzureContentUnderstanding:ApiKey",
    "Extraction:GptFallback:ApiKey",
    "Classification:AzureDocumentIntelligence:ApiKey",
    "Classification:GptFallback:ApiKey",
    "AssetResolverApiKey",
    "FunctionsAdminApiFunctionKey",
    "GDC:Password",
    "GDC:HttpBasicPassword",
    "GDC:Username",
    "GDC:HttpBasicUsername"
)

Write-Host ""
Write-Host "== Login/Subscription ==" -ForegroundColor Cyan
& az account set --subscription $SubscriptionId
if ($LASTEXITCODE -ne 0) {
    throw "No se pudo seleccionar la suscripcion: $SubscriptionId"
}

$account = (& az account show --query "{name:name,id:id,user:user.name}" -o json) | ConvertFrom-Json
Write-Host "Suscripcion: $($account.name) ($($account.id))"
Write-Host "Usuario: $($account.user)"

Write-Host ""
Write-Host "== Publicando secretos ==" -ForegroundColor Cyan

$published = 0
$skipped = 0

foreach ($key in $keysToPublish) {
    $value = $defaults[$key]
    $environmentValue = Get-EnvironmentSecretValue -Key $key
    if (-not [string]::IsNullOrWhiteSpace($environmentValue)) {
        $value = $environmentValue
    }

    if ($cfg) {
        $localValue = Get-ConfigValue -Config $cfg -Key $key
        if (-not [string]::IsNullOrWhiteSpace($localValue)) {
            $value = $localValue
        }
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        Write-Host "[SKIP] $key (sin valor)" -ForegroundColor Yellow
        $skipped++
        continue
    }

    $secretName = Convert-KeyToSecretName -Key $key
    Set-SecretSafe -VaultName $KeyVaultName -SecretName $secretName -SecretValue $value -DryRun:$WhatIf
    $published++
}

Write-Host ""
Write-Host "== Resumen ==" -ForegroundColor Green
Write-Host "Publicados: $published"
Write-Host "Saltados (sin valor): $skipped"

Write-Host ""
Write-Host "Configuracion recomendada para Function App:" -ForegroundColor Cyan
Write-Host "  - SecretsSource=AzureVault"
Write-Host "  - KeyVaultName=$KeyVaultName"
Write-Host "  - AzureWebJobsStorage como App Setting/KV reference para el host de Functions"
Write-Host "  - Resto de secretos cargados directamente desde Key Vault por Program.cs"

Write-Host ""
Write-Host "Ejemplo de ejecucion:" -ForegroundColor Cyan
Write-Host "  .\scripts\set-keyvault-secrets.ps1 -SubscriptionId $SubscriptionId"
Write-Host ""
Write-Host "Si quieres que local.settings sobreescriba los defaults:" -ForegroundColor Cyan
Write-Host "  .\scripts\set-keyvault-secrets.ps1 -SubscriptionId $SubscriptionId -PreferLocalSettings -LocalSettingsPath src/backend/DocumentIA.Functions/local.settings.json"
Write-Host ""
Write-Host "Para aportar secretos sin local.settings, usa variables DOCIA_SECRET_*; ejemplo:" -ForegroundColor Cyan
Write-Host "  `$env:DOCIA_SECRET_SQLCONNECTIONSTRING = '<valor>'"
Write-Host "  `$env:DOCIA_SECRET_FUNCTIONSADMINAPIFUNCTIONKEY = '<valor>'"
Write-Host ""
Write-Host "Modo simulacion (sin escribir):" -ForegroundColor Cyan
Write-Host "  .\scripts\set-keyvault-secrets.ps1 -SubscriptionId $SubscriptionId -WhatIf"
