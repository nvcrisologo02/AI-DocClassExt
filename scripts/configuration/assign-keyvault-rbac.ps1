<#
.SYNOPSIS
  Prerrequisito RBAC: habilita las Managed Identities de las apps y les asigna el rol
  'Key Vault Secrets User' sobre el Key Vault del entorno.

.DESCRIPTION
  El Service Principal del pipeline (service connection) tiene permiso para desplegar
  pero NO para crear role assignments (Microsoft.Authorization/roleAssignments/write).
  Por eso la asignacion del rol 'Key Vault Secrets User' a las identidades administradas
  de las web/function apps se hace UNA VEZ por entorno con este script, ejecutado por una
  persona con rol elevado (Owner / User Access Administrator / RBAC Administrator, via PIM).

  Para cada app del entorno:
    1. Habilita la System-Assigned Managed Identity (idempotente).
    2. Obtiene el principalId de la identidad.
    3. Si no la tiene ya, le asigna 'Key Vault Secrets User' sobre el Key Vault.

  Apps cubiertas (todas consumen secretos via Key Vault references):
    - Function App  (srbapp<env>docai)
    - Admin Web App (srbwebadmin<env>docai)
    - AssetResolver Web App (srbwebpluginassetresolver[<env>])

  Tras ejecutarlo, el paso 'Ensure ... Key Vault RBAC' del pipeline solo VERIFICA que el
  rol existe (no intenta crearlo) y pasa correctamente.

.PARAMETER TargetEnvironment
  dev | pre | prod

.PARAMETER SubscriptionId
  Opcional. Si se indica, fija la suscripcion activa antes de operar.

.PARAMETER IncludeFunctions
  Por defecto $true. Incluir la Function App en la asignacion.

.EXAMPLE
  # 1) Iniciar sesion y activar rol elevado (PIM) si procede
  az login
  # 2) Asignar RBAC para DEV
  pwsh ./scripts/configuration/assign-keyvault-rbac.ps1 -TargetEnvironment dev

.EXAMPLE
  pwsh ./scripts/configuration/assign-keyvault-rbac.ps1 -TargetEnvironment prod -SubscriptionId 647c7246-54bc-4d31-b909-431cacf03272

.NOTES
  Requiere Azure CLI (az) autenticado con permisos de RBAC sobre el Resource Group / Key Vault.
  Idempotente: re-ejecutarlo no crea asignaciones duplicadas.
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

function Write-Step { param([string]$m) Write-Host "[STEP] $m" -ForegroundColor Cyan }
function Write-Info { param([string]$m) Write-Host "[INFO] $m" -ForegroundColor Gray }
function Write-Ok   { param([string]$m) Write-Host "[ OK ] $m" -ForegroundColor Green }
function Write-Err2 { param([string]$m) Write-Host "[FAIL] $m" -ForegroundColor Red }

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) no esta instalado o no esta en PATH."
}

# --- Mapeo de entorno (alineado con azure-pipelines*.yml) -----------------------
$envMap = @{
    dev  = @{ Rg = 'SRBRGDEVDOCSAI';  Kv = 'srbkvdevdocai';  Func = 'srbappdevdocai';  Admin = 'srbwebadmindevdocai';  Asset = 'srbwebpluginassetresolverdev' }
    pre  = @{ Rg = 'SRBRGPREDOCSAI';  Kv = 'srbkvpredocai';  Func = 'srbapppredocai';  Admin = 'srbwebadminpredocai';  Asset = 'srbwebpluginassetresolverpre' }
    prod = @{ Rg = 'SRBRGDOCSAIPROD'; Kv = 'srbkvprodocai';  Func = 'srbappprodocai';  Admin = 'srbwebadminprodocai';  Asset = 'srbwebpluginassetresolver' }
}
$cfg = $envMap[$TargetEnvironment]

if ($SubscriptionId) {
    Write-Step "Fijando suscripcion $SubscriptionId ..."
    az account set --subscription $SubscriptionId | Out-Null
}

Write-Step "Entorno: $TargetEnvironment | RG: $($cfg.Rg) | Key Vault: $($cfg.Kv)"

$kvScope = az keyvault show --resource-group $cfg.Rg --name $cfg.Kv --query id -o tsv
if (-not $kvScope) { throw "No se pudo resolver el Key Vault $($cfg.Kv) en $($cfg.Rg)." }

# --- Lista de apps a procesar ---------------------------------------------------
$apps = @(
    @{ Name = $cfg.Admin; Kind = 'webapp' },
    @{ Name = $cfg.Asset; Kind = 'webapp' }
)
if ($IncludeFunctions) {
    $apps = @(@{ Name = $cfg.Func; Kind = 'functionapp' }) + $apps
}

$failures = 0
foreach ($app in $apps) {
    $name = $app.Name; $kind = $app.Kind
    Write-Step "Procesando $kind '$name' ..."

    # 1) Habilitar System-Assigned Managed Identity (idempotente)
    az $kind identity assign --resource-group $cfg.Rg --name $name --only-show-errors --output none
    if ($LASTEXITCODE -ne 0) {
        Write-Err2 "No se pudo habilitar la identidad de $name (revisa que el recurso existe)."
        $failures++; continue
    }

    # 2) Obtener principalId
    $principalId = az $kind identity show --resource-group $cfg.Rg --name $name --query principalId -o tsv
    if (-not $principalId) {
        Write-Err2 "No se pudo resolver el principalId de $name."
        $failures++; continue
    }
    Write-Info "principalId = $principalId"

    # 3) Asignar 'Key Vault Secrets User' si no lo tiene ya
    $existing = az role assignment list --scope $kvScope --assignee $principalId `
        --query "[?roleDefinitionName=='Key Vault Secrets User'] | length(@)" -o tsv
    if ($existing -ne '0') {
        Write-Ok "$name ya tiene 'Key Vault Secrets User' sobre $($cfg.Kv)."
        continue
    }

    az role assignment create --role "Key Vault Secrets User" `
        --assignee-object-id $principalId --assignee-principal-type ServicePrincipal `
        --scope $kvScope --only-show-errors --output none
    if ($LASTEXITCODE -ne 0) {
        Write-Err2 "No se pudo asignar el rol a $name. Necesitas Owner / User Access Administrator / RBAC Administrator sobre $($cfg.Rg) (activa PIM)."
        $failures++; continue
    }
    Write-Ok "Asignado 'Key Vault Secrets User' a $name sobre $($cfg.Kv)."
}

Write-Host ""
if ($failures -gt 0) {
    Write-Err2 "Completado con $failures error(es). Revisa los mensajes anteriores."
    exit 1
}
Write-Ok "RBAC de Key Vault asignado correctamente para el entorno $TargetEnvironment."
Write-Info "El pipeline ya solo VERIFICARA estas asignaciones (manageKeyVaultRbac=false)."
