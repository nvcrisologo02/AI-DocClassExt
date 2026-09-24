# Comprueba que las rutas de repo citadas en un Markdown existen.
# Uso: pwsh ./scripts/docs/verificar-rutas-doc.ps1 -Path docs/procedimientos/RELEASE_MANAGEMENT.md
param(
    [Parameter(Mandatory)][string]$Path,
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
)
$ErrorActionPreference = 'Stop'
$texto = Get-Content -Path $Path -Raw
$rutas = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in [regex]::Matches($texto, '`((?:docs|scripts|tests|infra|src)/[^`\s]+|azure-pipelines[^`\s]*\.yml)`')) {
    [void]$rutas.Add($m.Groups[1].Value)
}
foreach ($m in [regex]::Matches($texto, '\[[^\]]*\]\(([^)#\s]+)\)')) {
    $r = $m.Groups[1].Value
    if ($r -notmatch '^(https?:|mailto:)') { [void]$rutas.Add($r) }
}
$docDir = Split-Path -Parent (Resolve-Path $Path).Path
$faltan = @()
$comprobadas = 0
foreach ($r in $rutas) {
    if ($r -match 'vX\.Y\.Z|[<>…]') { continue }
    $limpia = $r.TrimEnd('.', ',', ';', ':')
    $candidatos = @((Join-Path $RepoRoot $limpia), (Join-Path $docDir $limpia))
    $comprobadas++
    if (-not ($candidatos | Where-Object { Test-Path $_ })) { $faltan += $limpia }
}
foreach ($f in $faltan) { Write-Output "NO EXISTE: $f" }
if ($faltan.Count -gt 0) { exit 1 }
Write-Output "OK: $comprobadas rutas comprobadas"
exit 0
