<#
.SYNOPSIS
    Restaura los app settings desde la foto de Save-ReleaseFotoSettings.ps1.

.DESCRIPTION
    Lee foto-settings.json. Salta AzureWebJobs.IngestDocument.Disabled (lo gestiona
    Set-ReleaseIngesta.ps1). Las claves con valor se fijan con UN appsettings set; las claves con
    null (no existian al hacer la foto) se borran con UN appsettings delete. Despues relee la lista
    (una llamada) y compara: cada clave con valor debe coincidir y cada clave null debe faltar.
    La lista completa nunca se imprime ni se escribe.
    Codigo de salida 0 si la relectura coincide y 2 si falla el fichero, az o la comparacion.

.PARAMETER ResourceGroup
    Resource group de la Function App.

.PARAMETER FunctionApp
    Nombre de la Function App.

.PARAMETER InFile
    Ruta de foto-settings.json.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Restore-ReleaseFotoSettings.ps1 -ResourceGroup SRBRGPREDOCSAI -FunctionApp srbapppredocai -InFile ./evidencias/foto-settings.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][string]$FunctionApp,
    [Parameter(Mandatory)][string]$InFile
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

if (-not (Test-Path -LiteralPath $InFile -PathType Leaf)) {
    Write-Host "No existe la foto $InFile"
    exit 2
}
try {
    $foto = Get-Content -LiteralPath $InFile -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
}
catch {
    Write-Host "No se pudo leer la foto $InFile"
    exit 2
}
if ($null -eq $foto) {
    Write-Host "La foto $InFile esta vacia"
    exit 2
}

$gestionada = 'AzureWebJobs.IngestDocument.Disabled'
$conValor = [ordered]@{}
$nulas = @()
foreach ($p in $foto.PSObject.Properties) {
    if ($p.Name -eq $gestionada) { continue }
    if ($null -eq $p.Value) { $nulas += $p.Name } else { $conValor[$p.Name] = [string]$p.Value }
}

if ($conValor.Count -gt 0) {
    $pares = @($conValor.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value })
    # La salida de set es la lista completa de settings: se descarta, nunca se imprime.
    $null = Invoke-ReleaseAz -Arguments (@('functionapp', 'config', 'appsettings', 'set', '-n', $FunctionApp, '-g', $ResourceGroup, '--settings') + $pares)
    if ($LASTEXITCODE -ne 0) {
        Write-Host "No se pudieron restaurar los app settings de $FunctionApp"
        exit 2
    }
}
if ($nulas.Count -gt 0) {
    $null = Invoke-ReleaseAz -Arguments (@('functionapp', 'config', 'appsettings', 'delete', '-n', $FunctionApp, '-g', $ResourceGroup, '--setting-names') + $nulas)
    if ($LASTEXITCODE -ne 0) {
        Write-Host "No se pudieron borrar los app settings nuevos de $FunctionApp"
        exit 2
    }
}

$lista = Invoke-ReleaseAz -Arguments @('functionapp', 'config', 'appsettings', 'list', '-n', $FunctionApp, '-g', $ResourceGroup)
$codigo = $LASTEXITCODE
$lista = @($lista | Where-Object { $null -ne $_ })
if ($codigo -ne 0 -or $lista.Count -eq 0) {
    Write-Host "No se pudo releer la configuracion de $FunctionApp"
    exit 2
}

$actual = @{}
foreach ($s in $lista) { $actual[[string]$s.name] = [string]$s.value }

$diferencias = @()
foreach ($k in $conValor.Keys) {
    if (-not $actual.ContainsKey($k)) { $diferencias += "$k : falta (esperado '$($conValor[$k])')" }
    elseif (-not [string]::Equals($actual[$k], $conValor[$k], [StringComparison]::Ordinal)) { $diferencias += "$k : difiere (esperado '$($conValor[$k])', actual '$($actual[$k])')" }
}
foreach ($k in $nulas) {
    if ($actual.ContainsKey($k)) { $diferencias += "$k : sigue presente (debia borrarse)" }
}

if ($diferencias.Count -gt 0) {
    Write-Host "La relectura no coincide con la foto en $FunctionApp"
    $diferencias | ForEach-Object { Write-Host $_ }
    exit 2
}

Write-Host "Restaurados $($conValor.Count) settings y borrados $($nulas.Count) en $FunctionApp"
exit 0
