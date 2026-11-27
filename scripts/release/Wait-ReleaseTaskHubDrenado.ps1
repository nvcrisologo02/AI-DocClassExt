<#
.SYNOPSIS
    Espera a que el task hub de Durable Functions no tenga orquestaciones Running ni Pending.

.DESCRIPTION
    Lee la clave de la cuenta de storage del task hub (secreto: se marca con ##vso[task.setsecret] y no
    se imprime en ningun otro sitio) y consulta la tabla <TaskHub>Instances hasta que no quedan filas con
    RuntimeStatus Running o Pending. Cualquier fallo de lectura (clave o tabla, tambien en iteraciones
    posteriores) termina con exit 2; el tiempo agotado es exit 3.
    Codigos de salida: 0 drenado, 2 no se pudo leer clave o tabla, 3 tiempo agotado.

.PARAMETER StorageAccount
    Cuenta de storage del task hub.

.PARAMETER TaskHub
    Nombre del task hub (host.json). Por defecto DocumentIAHub.

.PARAMETER TimeoutMinutos
    Tope de espera en minutos. Por defecto 30.

.PARAMETER IntervaloSegundos
    Pausa entre consultas en segundos. Por defecto 30.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Wait-ReleaseTaskHubDrenado.ps1 -StorageAccount srbstgpreappdocai
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$StorageAccount,
    [string]$TaskHub = 'DocumentIAHub',
    [double]$TimeoutMinutos = 30,
    [double]$IntervaloSegundos = 30
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

$clave = [string](Invoke-ReleaseAz -Arguments @('storage', 'account', 'keys', 'list', '--account-name', $StorageAccount, '--query', '[0].value', '-o', 'tsv'))
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($clave)) {
    Write-Host "No se pudo leer la clave de la cuenta $StorageAccount"
    exit 2
}
$clave = $clave.Trim()
Write-Host "##vso[task.setsecret]$clave"

$tabla = "$($TaskHub)Instances"
$inicio = [DateTime]::UtcNow
$limite = $inicio.AddMinutes($TimeoutMinutos)

while ($true) {
    $res = Invoke-ReleaseAz -Arguments @('storage', 'entity', 'query', '--table-name', $tabla, '--account-name', $StorageAccount, '--account-key', $clave, '--auth-mode', 'key', '--filter', "RuntimeStatus eq 'Running' or RuntimeStatus eq 'Pending'", '--select', 'RuntimeStatus')
    if ($LASTEXITCODE -ne 0) {
        Write-Host "No se pudo leer la tabla $tabla"
        exit 2
    }
    $items = if ($null -ne $res -and $null -ne $res.PSObject.Properties['items']) { @($res.items | Where-Object { $null -ne $_ }) } else { @() }
    $transcurrido = [int][Math]::Round(([DateTime]::UtcNow - $inicio).TotalSeconds)

    if ($items.Count -eq 0) {
        Write-Host "Task hub drenado tras $transcurrido segundos"
        exit 0
    }
    Write-Host "Quedan $($items.Count) instancias (Running/Pending)"

    if ([DateTime]::UtcNow -ge $limite) {
        $minutos = [Math]::Round(([DateTime]::UtcNow - $inicio).TotalMinutes, 2)
        Write-Host "Tiempo agotado: quedan $($items.Count) instancias tras $minutos minutos"
        exit 3
    }
    Start-Sleep -Seconds $IntervaloSegundos
}
