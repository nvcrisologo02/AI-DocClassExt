<#
.SYNOPSIS
    Anade (o actualiza) la regla de ciclo de vida que borra los recortes de clasificacion
    del contenedor documents-clasif con mas de N dias (AB#100814).

.DESCRIPTION
    Plano de gestion con az rest (la cuenta de desarrollo falla en TLS con el data-plane
    de az CLI). Lee la management policy "default" de la cuenta de documentos del entorno,
    sustituye o anade la regla "documents-clasif-7d" sin tocar las demas reglas y la escribe.
    Idempotente: ejecutarlo dos veces deja la misma politica.

    Cuentas por entorno (verificadas el 2026-10-07 con az rest):
      dev  srbstgdevdocai  SRBRGDEVDOCSAI  8764f9ff-fe37-4c03-bde9-6294622bef6d
      pre  srbstgpredocai  SRBRGPREDOCSAI  a4f6b357-8f13-4488-9ee8-b9f635426f91
      pro  srbstgprodocai  SRBRGDOCSAIPROD 647c7246-54bc-4d31-b909-431cacf03272

.PARAMETER Environment
    dev, pre o pro.
.PARAMETER Days
    Dias desde la ultima modificacion para borrar el blob. Por defecto 7.
.PARAMETER WhatIf
    Muestra la politica resultante sin escribirla.

.EXAMPLE
    pwsh ./scripts/storage/set-lifecycle-documents-clasif.ps1 -Environment dev -WhatIf
    pwsh ./scripts/storage/set-lifecycle-documents-clasif.ps1 -Environment dev
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][ValidateSet("dev", "pre", "pro")][string]$Environment,
    [int]$Days = 7
)

$ErrorActionPreference = "Stop"

$cuentas = @{
    dev = @{ Sub = "8764f9ff-fe37-4c03-bde9-6294622bef6d"; Rg = "SRBRGDEVDOCSAI"; Cuenta = "srbstgdevdocai" }
    pre = @{ Sub = "a4f6b357-8f13-4488-9ee8-b9f635426f91"; Rg = "SRBRGPREDOCSAI"; Cuenta = "srbstgpredocai" }
    pro = @{ Sub = "647c7246-54bc-4d31-b909-431cacf03272"; Rg = "SRBRGDOCSAIPROD"; Cuenta = "srbstgprodocai" }
}
$c = $cuentas[$Environment]
$nombreRegla = "documents-clasif-${Days}d"
$url = "https://management.azure.com/subscriptions/$($c.Sub)/resourceGroups/$($c.Rg)/providers/Microsoft.Storage/storageAccounts/$($c.Cuenta)/managementPolicies/default?api-version=2023-05-01"

Write-Host "Cuenta: $($c.Cuenta) ($Environment). Regla: $nombreRegla"

$reglasExistentes = @()
try {
    $actual = az rest --method get --url $url 2>$null | ConvertFrom-Json
    if ($actual -and $actual.properties.policy.rules) {
        $reglasExistentes = @($actual.properties.policy.rules | Where-Object { $_.name -ne $nombreRegla -and $_.name -notlike "documents-clasif-*" })
    }
} catch {
    Write-Host "Sin politica previa (se crea nueva)."
}

$reglaNueva = [ordered]@{
    enabled = $true
    name    = $nombreRegla
    type    = "Lifecycle"
    definition = [ordered]@{
        filters = [ordered]@{
            blobTypes   = @("blockBlob")
            prefixMatch = @("documents-clasif/")
        }
        actions = [ordered]@{
            baseBlob = [ordered]@{
                delete = [ordered]@{ daysAfterModificationGreaterThan = $Days }
            }
        }
    }
}

$cuerpo = [ordered]@{
    properties = [ordered]@{
        policy = [ordered]@{
            rules = @($reglasExistentes + $reglaNueva)
        }
    }
} | ConvertTo-Json -Depth 10

$ficheroCuerpo = Join-Path ([System.IO.Path]::GetTempPath()) "lifecycle-$($c.Cuenta).json"
[System.IO.File]::WriteAllText($ficheroCuerpo, $cuerpo, (New-Object System.Text.UTF8Encoding $false))

if ($PSCmdlet.ShouldProcess($c.Cuenta, "PUT managementPolicies/default con la regla $nombreRegla")) {
    $resultado = az rest --method put --url $url --body "@$ficheroCuerpo" | ConvertFrom-Json
    $nombres = @($resultado.properties.policy.rules | ForEach-Object { $_.name })
    Write-Host "Politica escrita. Reglas: $($nombres -join ', ')"
    if ($nombres -notcontains $nombreRegla) { throw "La regla $nombreRegla no aparece tras el PUT." }
} else {
    Write-Host "Politica resultante (no escrita):"
    Write-Host $cuerpo
}
Remove-Item $ficheroCuerpo -ErrorAction SilentlyContinue
