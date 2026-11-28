<#
.SYNOPSIS
    Paso ps1 de ensayo de la pipeline Release: imprime el entorno y la suscripcion. Sin efectos.

.PARAMETER Environment
    Entorno: dev, pre o prod.

.PARAMETER DryRun
    Admitido por contrato con la pipeline; el script no tiene efectos en ningun modo.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
    [switch]$DryRun
)

$ErrorActionPreference = 'Continue'
Write-Host "Ensayo de la pipeline Release en '$Environment' (DryRun=$([bool]$DryRun))"
$suscripcion = az account show --query name -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($suscripcion)) { $suscripcion = 'suscripcion no disponible' }
Write-Host "Suscripcion: $suscripcion"
exit 0
