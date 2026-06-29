<#
.SYNOPSIS
  Valida App Settings Azure contra el contrato canonico del repositorio sin imprimir valores.

.DESCRIPTION
  Comprueba que las aplicaciones declaradas en scripts/config/azure-appsettings-contract.json
  tienen los settings obligatorios, que las referencias Key Vault tienen forma esperada y que
  no existen settings prohibidos. La salida solo muestra nombres de settings y tipos; nunca
  muestra valores.

.EXAMPLE
  pwsh ./scripts/validate-azure-appsettings-contract.ps1

.EXAMPLE
  pwsh ./scripts/validate-azure-appsettings-contract.ps1 -SubscriptionId <id> -ResourceGroup SRBRGDOCSAIPROD
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId = "",

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "SRBRGDOCSAIPROD",

    [Parameter(Mandatory = $false)]
    [string]$FunctionsAppName = "",

    [Parameter(Mandatory = $false)]
    [string]$AdminWebAppName = "",

    [Parameter(Mandatory = $false)]
    [string]$AssetResolverWebAppName = "",

    [Parameter(Mandatory = $false)]
    [string]$ContractPath = ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\config\azure-appsettings-contract.json"))),

    [Parameter(Mandatory = $false)]
    [switch]$SkipAzureLoginCheck,
    
    [Parameter(Mandatory = $false)]
    [switch]$BasicValidationOnly
)

# Detectar environment del ResourceGroup
function Get-EnvironmentFromResourceGroup {
    param([string]$ResourceGroup)
    
    if ($ResourceGroup -like "*DEV*") { return "dev" }
    if ($ResourceGroup -like "*PRE*") { return "pre" }
    if ($ResourceGroup -like "*PROD*") { return "prod" }
    return "prod"  # default
}

# Detectar nombres de apps si no se proporcionan
$environment = Get-EnvironmentFromResourceGroup -ResourceGroup $ResourceGroup
if ([string]::IsNullOrWhiteSpace($FunctionsAppName)) {
    $FunctionsAppName = "srbappdev docai"       # pattern: srba pp [dev|pre|prod] docai
    switch ($environment) {
        "dev"  { $FunctionsAppName = "srbappdevdocai" }
        "pre"  { $FunctionsAppName = "srbapppredocai" }
        "prod" { $FunctionsAppName = "srbappprodocai" }
    }
}

if ([string]::IsNullOrWhiteSpace($AdminWebAppName)) {
    switch ($environment) {
        "dev"  { $AdminWebAppName = "srbwebadmindevdocai" }
        "pre"  { $AdminWebAppName = "srbwebadminpredocai" }
        "prod" { $AdminWebAppName = "srbwebadminprodocai" }
    }
}

if ([string]::IsNullOrWhiteSpace($AssetResolverWebAppName)) {
    switch ($environment) {
        "dev"  { $AssetResolverWebAppName = "srbwebpluginassetresolverdev" }
        "pre"  { $AssetResolverWebAppName = "srbwebpluginassetresolverpre" }
        "prod" { $AssetResolverWebAppName = "srbwebpluginassetresolver" }
    }
}

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) no esta instalado o no esta en PATH."
}

if (-not (Test-Path -LiteralPath $ContractPath)) {
    throw "No se encontro el contrato de settings: $ContractPath"
}

function Invoke-AzJson {
    param([string[]]$AzArgs)

    $previousOnlyShowErrors = $env:AZURE_CORE_ONLY_SHOW_ERRORS
    $env:AZURE_CORE_ONLY_SHOW_ERRORS = "1"
    try {
        $result = & az @AzArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "az $($AzArgs -join ' ')`n$result"
        }

        $lines = @($result | ForEach-Object { [string]$_ })
        $jsonStartIndex = -1
        for ($index = 0; $index -lt $lines.Count; $index++) {
            $trimmed = $lines[$index].TrimStart()
            if ($trimmed.StartsWith("{") -or $trimmed.StartsWith("[")) {
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
    finally {
        $env:AZURE_CORE_ONLY_SHOW_ERRORS = $previousOnlyShowErrors
    }
}

function Get-SettingKind {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "Empty"
    }

    if ($Value -like "@Microsoft.KeyVault(*)") {
        return "KeyVaultRef"
    }

    return "LiteralOrNonSecret"
}

function Test-KeyVaultSecretName {
    param(
        [string]$Value,
        [string]$SecretName
    )

    if ([string]::IsNullOrWhiteSpace($SecretName)) {
        return $true
    }

    return $Value -match "(?i)(^|[;])\s*SecretName\s*=\s*$([regex]::Escape($SecretName))\s*([;)]|$)"
}

function Test-AllowedValue {
    param(
        [AllowNull()][string]$Value,
        [AllowNull()]$AllowedValues
    )

    if ($null -eq $AllowedValues) {
        return $true
    }

    $allowed = @($AllowedValues | ForEach-Object { [string]$_ })
    return $allowed -contains [string]$Value
}

function Get-AppName {
    param([string]$AppId, [string]$DefaultName)

    switch ($AppId) {
        "functions"     { return $FunctionsAppName }
        "admin"         { return $AdminWebAppName }
        "assetresolver" { return $AssetResolverWebAppName }
        default          { return $DefaultName }
    }
}

if (-not $SkipAzureLoginCheck) {
    if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
        az account set --subscription $SubscriptionId --only-show-errors | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "No se pudo seleccionar la suscripcion $SubscriptionId"
        }
    }
    else {
        Invoke-AzJson -AzArgs @("account", "show", "-o", "json") | Out-Null
    }
}

$contract = Get-Content -Raw -LiteralPath $ContractPath | ConvertFrom-Json
$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

Write-Host "== Validando contrato de App Settings Azure ==" -ForegroundColor Cyan
Write-Host "ResourceGroup: $ResourceGroup"
Write-Host "Contrato: $ContractPath"
Write-Host ""

foreach ($app in @($contract.apps)) {
    $appName = Get-AppName -AppId $app.id -DefaultName $app.defaultName
    Write-Host "[$($app.id)] $($app.displayName): $appName" -ForegroundColor Yellow

    if ($app.kind -eq "functionapp") {
        $settings = Invoke-AzJson -AzArgs @(
            "functionapp", "config", "appsettings", "list",
            "--resource-group", $ResourceGroup,
            "--name", $appName,
            "--only-show-errors",
            "-o", "json"
        )
    }
    elseif ($app.kind -eq "webapp") {
        $settings = Invoke-AzJson -AzArgs @(
            "webapp", "config", "appsettings", "list",
            "--resource-group", $ResourceGroup,
            "--name", $appName,
            "--only-show-errors",
            "-o", "json"
        )
    }
    else {
        throw "Tipo de app no soportado en contrato: $($app.kind)"
    }

    $settingsByName = @{}
    foreach ($setting in @($settings)) {
        $settingsByName[[string]$setting.name] = [string]$setting.value
    }

    # Si es BasicValidationOnly y es Functions, solo valida settings básicos (KV references y config)
    $requiredToValidate = @($app.requiredSettings)
    if ($BasicValidationOnly -and $app.id -eq "functions") {
        $basicFunctionSettings = @(
            "SecretsSource",
            "KeyVaultName",
            "AzureWebJobsStorage",
            "RunDatabaseMigrationsOnStartup"
        )
        $requiredToValidate = @($app.requiredSettings | Where-Object { $_.name -in $basicFunctionSettings })
        Write-Host "  [INFO] BasicValidationOnly: validando solo $($basicFunctionSettings -join ', ')" -ForegroundColor Cyan
    }

    foreach ($required in @($requiredToValidate)) {
        $name = [string]$required.name
        if (-not $settingsByName.ContainsKey($name)) {
            $failures.Add("[$($app.id)] Falta setting obligatorio: $name")
            Write-Host "  [FALTA] $name" -ForegroundColor Red
            continue
        }

        $value = [string]$settingsByName[$name]
        $actualKind = Get-SettingKind -Value $value
        $expectedKind = [string]$required.kind

        if ($actualKind -ne $expectedKind) {
            $failures.Add("[$($app.id)] $name tiene tipo $actualKind y se esperaba $expectedKind")
            Write-Host "  [ERROR] $name ($actualKind, esperado $expectedKind)" -ForegroundColor Red
            continue
        }

        if ($expectedKind -eq "KeyVaultRef") {
            $secretName = if ($required.PSObject.Properties.Name -contains "secretName") { [string]$required.secretName } else { "" }
            if (-not (Test-KeyVaultSecretName -Value $value -SecretName $secretName)) {
                $failures.Add("[$($app.id)] $name es KeyVaultRef pero no referencia el secreto esperado: $secretName")
                Write-Host "  [ERROR] $name (KeyVaultRef con secreto inesperado)" -ForegroundColor Red
                continue
            }
        }

        if ($required.PSObject.Properties.Name -contains "requiredValue") {
            # Si es BasicValidationOnly o si estamos en un environment no-prod, skip requiredValue check para valores env-específicos
            if ($BasicValidationOnly -or ($environment -ne "prod" -and $name -eq "KeyVaultName")) {
                Write-Host "  [OK] $name (requiredValue check skipped)" -ForegroundColor Green
                continue
            }
            
            $expectedValue = [string]$required.requiredValue
            if ($value -ne $expectedValue) {
                $failures.Add("[$($app.id)] $name no coincide con el valor canonico esperado")
                Write-Host "  [ERROR] $name (valor no canonico)" -ForegroundColor Red
                continue
            }
        }

        if ($required.PSObject.Properties.Name -contains "allowedValues") {
            if (-not (Test-AllowedValue -Value $value -AllowedValues $required.allowedValues)) {
                $failures.Add("[$($app.id)] $name no esta dentro de los valores permitidos")
                Write-Host "  [ERROR] $name (valor fuera de catalogo)" -ForegroundColor Red
                continue
            }
        }

        Write-Host "  [OK] $name ($actualKind)" -ForegroundColor Green
    }

    foreach ($forbidden in @($app.forbiddenSettings)) {
        $name = [string]$forbidden.name
        if ($settingsByName.ContainsKey($name)) {
            $reason = if ($forbidden.PSObject.Properties.Name -contains "reason") { [string]$forbidden.reason } else { "Setting prohibido" }
            $failures.Add("[$($app.id)] Existe setting prohibido: $name. $reason")
            Write-Host "  [PROHIBIDO] $name" -ForegroundColor Red
        }
        else {
            Write-Host "  [OK] Ausente setting prohibido: $name" -ForegroundColor Green
        }
    }

    Write-Host ""
}

if ($warnings.Count -gt 0) {
    Write-Host "Advertencias:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  - $warning" -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($failures.Count -gt 0) {
    Write-Host "Contrato NO cumplido:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Contrato de App Settings cumplido." -ForegroundColor Green
