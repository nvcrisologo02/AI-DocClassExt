<#
.SYNOPSIS
    Valida un manifiesto releases/<version>/release.json (contrato: releases/release.schema.json).

.DESCRIPTION
    Comprueba en PowerShell, sin dependencias externas, lo que dice el esquema: campos,
    tipos y enumeraciones; ademas la version coincide con la carpeta, cada ruta existe bajo
    RepoRoot, cada .ps1 admite -DryRun o -WhatIf y lleva <env> en argumentos, y _ensayo
    solo se declara para dev. No accede a red ni a BD.

    Codigo de salida: 0 si es valido; 2 con la lista de errores (un error por linea).

.PARAMETER ManifestPath
    Ruta del release.json a validar.

.PARAMETER RepoRoot
    Raiz del repo contra la que se resuelven las rutas de los pasos. Por defecto, la del script.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Test-ReleaseManifest.ps1 -ManifestPath ./releases/v1.2.0/release.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path }

$errores = [System.Collections.Generic.List[string]]::new()
$entornosValidos = @('dev', 'pre', 'prod')
$modosValidos = @('off', 'sombra', 'hibrido')

function Test-Propiedades($Objeto, [string[]]$Permitidas, [string[]]$Obligatorias, [string]$Donde) {
    $presentes = @($Objeto.PSObject.Properties.Name)
    foreach ($p in $presentes) {
        if ($Permitidas -cnotcontains $p) { $errores.Add("${Donde}: propiedad no permitida '$p'") }
    }
    foreach ($o in $Obligatorias) {
        if ($presentes -cnotcontains $o) { $errores.Add("${Donde}: falta la propiedad obligatoria '$o'") }
    }
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    Write-Host "ERROR: no existe el manifiesto '$ManifestPath'"
    exit 2
}

try {
    $m = Get-Content -LiteralPath $ManifestPath -Raw -Encoding utf8 | ConvertFrom-Json
}
catch {
    Write-Host "ERROR: el manifiesto no es JSON valido: $($_.Exception.Message)"
    exit 2
}

if ($m -isnot [pscustomobject]) {
    Write-Host 'ERROR: la raiz del manifiesto debe ser un objeto'
    exit 2
}

Test-Propiedades $m @('version', 'embeddings', 'pasos') @('version', 'pasos') 'manifiesto'

# version
if ($m.PSObject.Properties.Name -contains 'version') {
    if ($m.version -isnot [string] -or $m.version -cnotmatch '^(v\d+\.\d+\.\d+|_ensayo)$') {
        $errores.Add("version: '$($m.version)' no cumple vX.Y.Z ni _ensayo")
    }
    else {
        $carpeta = Split-Path (Split-Path (Resolve-Path -LiteralPath $ManifestPath).Path) -Leaf
        if ($m.version -cne $carpeta) {
            $errores.Add("version: '$($m.version)' no coincide con la carpeta '$carpeta'")
        }
    }
}

# embeddings
if ($m.PSObject.Properties.Name -contains 'embeddings') {
    $e = $m.embeddings
    if ($e -isnot [pscustomobject]) {
        $errores.Add('embeddings: debe ser un objeto')
    }
    else {
        Test-Propiedades $e @('version', 'modo', 'restringidoModo') @('version', 'modo', 'restringidoModo') 'embeddings'
        if ($e.PSObject.Properties.Name -contains 'version' -and ($e.version -isnot [string] -or $e.version -cnotmatch '^v\d+$')) {
            $errores.Add("embeddings.version: '$($e.version)' no cumple vN")
        }
        foreach ($campo in 'modo', 'restringidoModo') {
            if ($e.PSObject.Properties.Name -contains $campo -and ($e.$campo -isnot [string] -or $modosValidos -cnotcontains $e.$campo)) {
                $errores.Add("embeddings.${campo}: '$($e.$campo)' no es uno de: $($modosValidos -join ', ')")
            }
        }
    }
}

# pasos
if ($m.PSObject.Properties.Name -contains 'pasos') {
    if ($m.pasos -isnot [array]) {
        $errores.Add('pasos: debe ser una lista')
    }
    else {
        $i = 0
        foreach ($p in $m.pasos) {
            $i++
            $donde = "pasos[$i]"
            if ($p -isnot [pscustomobject]) { $errores.Add("${donde}: debe ser un objeto"); continue }
            Test-Propiedades $p @('tipo', 'ruta', 'argumentos', 'entornos') @('tipo', 'ruta', 'entornos') $donde
            $props = @($p.PSObject.Properties.Name)

            $tipoOk = $props -contains 'tipo' -and $p.tipo -is [string] -and @('sql', 'ps1') -ccontains $p.tipo
            if ($props -contains 'tipo' -and -not $tipoOk) { $errores.Add("${donde}.tipo: '$($p.tipo)' no es sql ni ps1") }

            if ($props -contains 'ruta') {
                if ($p.ruta -isnot [string] -or [string]::IsNullOrWhiteSpace($p.ruta)) {
                    $errores.Add("${donde}.ruta: debe ser un texto no vacio")
                }
                elseif ([System.IO.Path]::IsPathRooted($p.ruta) -or $p.ruta -match '(^|[\\/])\.\.([\\/]|$)') {
                    $errores.Add("${donde}.ruta: '$($p.ruta)' debe ser relativa a la raiz del repo y sin '..'")
                }
                else {
                    $abs = Join-Path $RepoRoot $p.ruta
                    if (-not (Test-Path -LiteralPath $abs -PathType Leaf)) {
                        $errores.Add("${donde}.ruta: no existe '$($p.ruta)' bajo la raiz del repo")
                    }
                    elseif ($tipoOk -and $p.tipo -ceq 'ps1') {
                        $contenido = Get-Content -LiteralPath $abs -Raw
                        if ($contenido -notmatch '(?i)\[switch\]\s*\$(DryRun|WhatIf)\b' -and $contenido -notmatch '(?i)SupportsShouldProcess') {
                            $errores.Add("${donde}.ruta: '$($p.ruta)' no admite -DryRun ni -WhatIf")
                        }
                    }
                }
            }

            if ($props -contains 'argumentos' -and $p.argumentos -isnot [string]) {
                $errores.Add("${donde}.argumentos: debe ser un texto")
            }
            if ($tipoOk -and $p.tipo -ceq 'ps1' -and ($props -cnotcontains 'argumentos' -or $p.argumentos -isnot [string] -or $p.argumentos -cnotlike '*<env>*')) {
                $errores.Add("${donde}.argumentos: los pasos ps1 deben llevar el marcador <env>")
            }

            if ($props -contains 'entornos') {
                if ($p.entornos -isnot [array] -or $p.entornos.Count -eq 0) {
                    $errores.Add("${donde}.entornos: debe ser una lista no vacia")
                }
                else {
                    foreach ($en in $p.entornos) {
                        if ($en -isnot [string] -or $entornosValidos -cnotcontains $en) {
                            $errores.Add("${donde}.entornos: '$en' no es uno de: $($entornosValidos -join ', ')")
                        }
                    }
                    if ($m.version -ceq '_ensayo' -and (@($p.entornos) | Where-Object { $_ -cne 'dev' })) {
                        $errores.Add("${donde}.entornos: _ensayo solo puede declarar ['dev']")
                    }
                }
            }
        }
    }
}

if ($errores.Count -gt 0) {
    Write-Host "Manifiesto no valido: $ManifestPath"
    foreach ($err in $errores) { Write-Host "  - $err" }
    exit 2
}

Write-Host "Manifiesto valido: $ManifestPath"
exit 0
