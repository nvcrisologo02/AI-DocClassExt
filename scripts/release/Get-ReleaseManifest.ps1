<#
.SYNOPSIS
    Resuelve el manifiesto de release de un entorno y devuelve sus pasos ya filtrados.

.DESCRIPTION
    dev usa releases/_ensayo/release.json; pre y prod usan releases/<CURRENT>/release.json.
    Antes de devolver nada valida el manifiesto con Test-ReleaseManifest.ps1 y, si falla,
    termina con error mostrando su salida. Devuelve un objeto con version, manifestPath,
    embeddings (puede ser $null) y pasos (tipo, ruta, argumentos, plantilla, entornos) del entorno pedido.
    No accede a red ni a BD.

.PARAMETER Environment
    Entorno: dev, pre o prod.

.PARAMETER RepoRoot
    Raiz del repo. Por defecto, la del script.

.PARAMETER AsJson
    Devuelve el resultado como JSON en lugar de objeto.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Get-ReleaseManifest.ps1 -Environment pre -AsJson
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
    [string]$RepoRoot,
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path }

if ($Environment -eq 'dev') {
    $manifestPath = Join-Path $RepoRoot 'releases/_ensayo/release.json'
}
else {
    $currentPath = Join-Path $RepoRoot 'releases/CURRENT'
    if (-not (Test-Path -LiteralPath $currentPath -PathType Leaf)) {
        throw "No existe releases/CURRENT en '$RepoRoot'."
    }
    $version = (Get-Content -LiteralPath $currentPath -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw 'releases/CURRENT esta vacio: debe contener la version en curso (por ejemplo v1.2.0).'
    }
    if ($version -cnotmatch '^v\d+\.\d+\.\d+$') {
        throw "CURRENT debe ser una version vX.Y.Z; valor leido: '$version'"
    }
    $manifestPath = Join-Path $RepoRoot "releases/$version/release.json"
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "No existe el manifiesto '$manifestPath'."
}

$salida = (& (Join-Path $PSScriptRoot 'Test-ReleaseManifest.ps1') -ManifestPath $manifestPath -RepoRoot $RepoRoot 6>&1 | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw "El manifiesto no supera la validacion:`n$salida"
}

$m = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json

$pasos = @(foreach ($p in $m.pasos) {
        if (@($p.entornos) -contains $Environment) {
            [pscustomobject]@{
                tipo       = $p.tipo
                ruta       = $p.ruta
                argumentos = if ($p.PSObject.Properties.Name -contains 'argumentos') { $p.argumentos } else { $null }
                plantilla  = if ($p.PSObject.Properties.Name -contains 'plantilla') { $p.plantilla } else { $null }
                entornos   = @($p.entornos)
            }
        }
    })

$embeddings = if ($m.PSObject.Properties.Name -contains 'embeddings') { $m.embeddings } else { $null }

$resultado = [pscustomobject]@{
    version      = $m.version
    manifestPath = (Resolve-Path -LiteralPath $manifestPath).Path
    embeddings   = $embeddings
    pasos        = $pasos
}

if ($AsJson) { $resultado | ConvertTo-Json -Depth 6 } else { $resultado }
