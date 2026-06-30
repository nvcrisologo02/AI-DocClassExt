<#
.SYNOPSIS
  Verifica (solo lectura) que las Managed Identities de las apps tienen el rol
  'Key Vault Secrets User' sobre el Key Vault del entorno.

.DESCRIPTION
  Comprobacion idempotente y NO destructiva del prerrequisito RBAC. Para cada app
  (Function App, Admin, AssetResolver) resuelve el principalId de su System-Assigned
  Managed Identity y comprueba si tiene asignado 'Key Vault Secrets User' en el Key Vault.

  Util para confirmar, antes o despues de ejecutar el pipeline, que el prerrequisito
  RBAC esta cumplido (ver scripts/configuration/assign-keyvault-rbac.ps1).

  Solo requiere permisos de LECTURA (az role assignment list / identity show); no
  necesita rol elevado ni PIM.

.PARAMETER TargetEnvironment
  dev | pre | prod

.PARAMETER SubscriptionId
  Opcional. Si se indica, fija la suscripcion activa antes de comprobar.

.PARAMETER IncludeFunctions
  Por defecto $true. Incluir la Function App en la comprobacion.

.EXAMPLE
  pwsh ./scripts/configuration/verify-keyvault-rbac.ps1 -TargetEnvironment dev

.OUTPUTS
  Codigo de salida 0 si todas las apps tienen el rol; 1 si falta en alguna o no tiene MI.

.NOTES
  Mapeo de entorno alineado con azure-pipelines*.yml y con assign-keyvault-rbac.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'pre', 'prod')]
    [string]$TargetEnvironment,

    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $false)]
    [bool]$IncludeFunctions = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) no esta instalado o no esta en PATH."
}

# --- Mapeo de entorno (alineado con assign-keyvault-rbac.ps1) -------------------
$envMap = @{
    dev  = @{ Rg = 'SRBRGDEVDOCSAI';  Kv = 'srbkvdevdocai';  Func = 'srbappdevdocai';  Admin = 'srbwebadmindevdocai';  Asset = 'srbwebpluginassetresolverdev' }
    pre  = @{ Rg = 'SRBRGPREDOCSAI';  Kv = 'srbkvpredocai';  Func = 'srbapppredocai';  Admin = 'srbwebadminpredocai';  Asset = 'srbwebpluginassetresolverpre' }
    prod = @{ Rg = 'SRBRGDOCSAIPROD'; Kv = 'srbkvprodocai';  Func = 'srbappprodocai';  Admin = 'srbwebadminprodocai';  Asset = 'srbwebpluginassetresolver' }
}
$cfg = $envMap[$TargetEnvironment]

if ($SubscriptionId) {
    az account set --subscription $SubscriptionId | Out-Null
}

Write-Host "[STEP] Verificando RBAC Key Vault | Entorno: $TargetEnvironment | RG: $($cfg.Rg) | KV: $($cfg.Kv)" -ForegroundColor Cyan

$kvScope = az keyvault show --resource-group $cfg.Rg --name $cfg.Kv --query id -o tsv
if (-not $kvScope) { throw "No se pudo resolver el Key Vault $($cfg.Kv) en $($cfg.Rg)." }

$apps = @(
    @{ Name = $cfg.Admin; Kind = 'webapp' },
    @{ Name = $cfg.Asset; Kind = 'webapp' }
)
if ($IncludeFunctions) {
    $apps = @(@{ Name = $cfg.Func; Kind = 'functionapp' }) + $apps
}

$missing = 0
foreach ($app in $apps) {
    $name = $app.Name; $kind = $app.Kind
    $oid = az $kind identity show --resource-group $cfg.Rg --name $name --query principalId -o tsv 2>$null
    if (-not $oid) {
        Write-Host "[MI OFF ] $name -> sin Managed Identity" -ForegroundColor Yellow
        $missing++; continue
    }
    $has = az role assignment list --scope $kvScope --assignee $oid `
        --query "[?roleDefinitionName=='Key Vault Secrets User'] | length(@)" -o tsv
    if ($has -ne '0') {
        Write-Host "[  OK   ] $name ($oid)" -ForegroundColor Green
    }
    else {
        Write-Host "[MISSING] $name ($oid) -> falta 'Key Vault Secrets User'" -ForegroundColor Red
        $missing++
    }
}

Write-Host ""
if ($missing -gt 0) {
    Write-Host "[FAIL] $missing app(s) sin el rol/MI. Ejecuta: pwsh ./scripts/configuration/assign-keyvault-rbac.ps1 -TargetEnvironment $TargetEnvironment" -ForegroundColor Red
    exit 1
}
Write-Host "[ OK ] Todas las identidades tienen 'Key Vault Secrets User' sobre $($cfg.Kv)." -ForegroundColor Green
