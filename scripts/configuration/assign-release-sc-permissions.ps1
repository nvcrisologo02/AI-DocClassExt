<#
.SYNOPSIS
  Asigna a las service connections de Azure DevOps los roles que faltan para la pipeline Release
  (AB#101583). Lo ejecuta el usuario a mano; inventario de permisos y resultado en
  docs/procedimientos/PERMISOS_SPN_RELEASE.md. Ejecutado el 2026-11-27 (las seis asignaciones estan presentes).

.DESCRIPTION
  ESCRIBE asignaciones de rol en Azure (plano de control). Usa object id y principal-type para no
  depender de Graph. 'az role assignment create' es idempotente: si la asignacion ya existe la
  devuelve sin error. Las asignaciones sobre PRO (y la lectura de la SC PRO sobre el storage de PRE)
  exigen elevacion PIM (User Access Administrator u Owner sobre el recurso) antes de ejecutar.

  Con -WhatIf solo imprime lo que haria. Sin -WhatIf asigna y al final lista lo asignado.

.EXAMPLE
  pwsh -NoProfile -File ./scripts/configuration/assign-release-sc-permissions.ps1 -WhatIf
  pwsh -NoProfile -File ./scripts/configuration/assign-release-sc-permissions.ps1
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('todos', 'dev', 'pre', 'prod')][string]$Entorno = 'todos'
)
$ErrorActionPreference = 'Continue'

$subDev = '8764f9ff-fe37-4c03-bde9-6294622bef6d'
$subPre = 'a4f6b357-8f13-4488-9ee8-b9f635426f91'
$subPro = '647c7246-54bc-4d31-b909-431cacf03272'

# object id de los SPN de las service connections (verificados el 2026-11-27 con az ad sp show)
$scDev = 'cea262dd-ba86-4647-9245-45b75ee357f3'   # AI DocClassExt DEV
$scPre = '3fc220ce-15f0-4818-b64f-c2cbcba3d7b7'   # AI DocClassExt PRE
$scPro = '4c989e87-cddd-4fea-8ac7-42a3526173ff'   # AI DocClassExt PRO

function Scope-Storage([string]$sub, [string]$rg, [string]$cuenta) {
    "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.Storage/storageAccounts/$cuenta"
}
function Scope-KeyVault([string]$sub, [string]$rg, [string]$vault) {
    "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.KeyVault/vaults/$vault"
}

# Una fila por asignacion: entorno de la SC, object id, rol, scope, motivo.
$asignaciones = @(
    # DEV: ensayo de la pipeline; copy-embeddings-artifacts.ps1 -DryRun lista blobs del origen (DEV).
    @{ env = 'dev';  oid = $scDev; rol = 'Storage Blob Data Reader';      scope = (Scope-Storage $subDev 'SRBRGDEVDOCSAI' 'srbstgdevdocai');  motivo = 'SC DEV lee el artefacto de embeddings en DEV (ensayo, solo DryRun)' },
    # PRE: copia DEV -> PRE.
    @{ env = 'pre';  oid = $scPre; rol = 'Storage Blob Data Reader';      scope = (Scope-Storage $subDev 'SRBRGDEVDOCSAI' 'srbstgdevdocai');  motivo = 'SC PRE lee el artefacto en DEV (origen de la copia DEV->PRE)' },
    @{ env = 'pre';  oid = $scPre; rol = 'Storage Blob Data Contributor'; scope = (Scope-Storage $subPre 'SRBRGPREDOCSAI' 'srbstgpredocai');  motivo = 'SC PRE escribe el artefacto en PRE (destino)' },
    # PRO: copia PRE -> PRO y borrado de secretos heredados de Key Vault.
    @{ env = 'prod'; oid = $scPro; rol = 'Storage Blob Data Reader';      scope = (Scope-Storage $subPre 'SRBRGPREDOCSAI' 'srbstgpredocai');  motivo = 'SC PRO lee el artefacto en PRE (origen de la copia PRE->PRO)' },
    @{ env = 'prod'; oid = $scPro; rol = 'Storage Blob Data Contributor'; scope = (Scope-Storage $subPro 'SRBRGDOCSAIPROD' 'srbstgprodocai'); motivo = 'SC PRO escribe el artefacto en PRO (destino)' },
    @{ env = 'prod'; oid = $scPro; rol = 'Key Vault Secrets Officer';     scope = (Scope-KeyVault $subPro 'SRBRGDOCSAIPROD' 'srbkvprodocai'); motivo = 'SC PRO borra los secretos heredados (remove-legacy-keyvault-secrets.ps1); hoy solo Secrets User' }
)

$seleccion = @($asignaciones | Where-Object { $Entorno -eq 'todos' -or $_.env -eq $Entorno })
Write-Host ("Asignaciones a aplicar: " + $seleccion.Count)
$fallos = 0
foreach ($a in $seleccion) {
    $desc = "{0} -> {1} sobre {2}" -f $a.oid, $a.rol, $a.scope
    Write-Host ""
    Write-Host ("[" + $a.env + "] " + $a.motivo)
    if (-not $PSCmdlet.ShouldProcess($desc, 'az role assignment create')) { continue }
    az role assignment create --assignee-object-id $a.oid --assignee-principal-type ServicePrincipal --role $a.rol --scope $a.scope -o none 2>&1 | Where-Object { $_ -notmatch 'urllib3|InsecureRequest' }
    if ($LASTEXITCODE -ne 0) { Write-Host "  FALLO (az exit $LASTEXITCODE). Si es AuthorizationFailed, falta elevacion PIM sobre el recurso." ; $fallos++ } else { Write-Host "  OK" }
}

if (-not $WhatIfPreference) {
    Write-Host ""
    Write-Host "Verificacion (una linea por asignacion esperada):"
    foreach ($a in $seleccion) {
        $r = az role assignment list --assignee-object-id $a.oid --scope $a.scope --query "[?roleDefinitionName=='$($a.rol)'].roleDefinitionName | [0]" -o tsv 2>$null
        $estado = if ($r) { 'PRESENTE' } else { 'AUSENTE' }
        Write-Host ("  {0,-8} {1,-30} {2}" -f $estado, $a.rol, ($a.scope -replace '^/subscriptions/[^/]+/', ''))
    }
}
if ($fallos -gt 0) { exit 2 }
