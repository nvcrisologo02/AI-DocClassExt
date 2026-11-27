<#
.SYNOPSIS
    Smoke E2E post-despliegue con clave maestra temporal y publicacion en el Test Plan 100069.

.DESCRIPTION
    1. Lee masterKey de la Function App (se usa la clave maestra porque la ingesta esta deshabilitada
       durante la release y cualquier otra clave da 404 en una funcion deshabilitada) (secreto: ##vso[task.setsecret], nunca se imprime
       ni va a -OutDir; exit 2 si esta vacia) y escribe tests/e2e-postdeploy/config/environments.json
       con { "<alias>": { baseUrl, functionKey, healthCheck: true } }. Si el fichero ya existia, se
       guarda su contenido y se repone al final; si no existia, se borra al final (bloque finally).
    2. Primera pasada: run-e2e-postdeploy.ps1 -Environment <alias> -Profile smoke. Si sale con 1 y el
       report.md tiene ERROR pero ningun FAIL (en la tabla de casos), repite la pasada hasta
       -MaxIntentos intentos en total (por defecto 2, es decir, un reintento).
    3. Segunda pasada: la misma suite con -CasesDir tests/e2e-postdeploy/cases-smk.
    4. publish-smk-run.ps1 con los dos directorios de informes: primero con -WhatIf y, si sale bien,
       de verdad con -Pat <AdoToken> (System.AccessToken por autenticacion basica con usuario vacio).
    5. Copia los dos directorios de informes (que incluyen ado-run-<id>.json) a -OutDir, sin
       environments.json, y escribe <OutDir>/smoke-resumen.json.
    Codigo de salida 0 solo con -PassEsperados (6) casos PASS en la primera pasada y run publicado;
    2 en cualquier otro caso.

.PARAMETER Environment
    Entorno: dev, pre o prod (el alias del runner es dev, pre o pro).

.PARAMETER ResourceGroup
    Resource group de la Function App.

.PARAMETER FunctionApp
    Nombre de la Function App de la que se lee la clave maestra.

.PARAMETER BaseUrl
    URL base de la Function App (https://...).

.PARAMETER RepoRoot
    Raiz del repo. Por defecto, la del script.

.PARAMETER Candidate
    Commit candidato (texto libre, va al nombre del run).

.PARAMETER Version
    Version de la release (v1.2.0).

.PARAMETER AdoToken
    System.AccessToken de la pipeline (secreto).

.PARAMETER OutDir
    Carpeta de evidencias (se crea si no existe).

.PARAMETER MaxIntentos
    Intentos totales de la primera pasada.

.PARAMETER PassEsperados
    Numero de PASS exigido en la primera pasada.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Invoke-ReleaseSmoke.ps1 -Environment pre -ResourceGroup SRBRGPREDOCSAI -FunctionApp srbapppredocai -BaseUrl https://srbapppredocai.azurewebsites.net -Candidate bc41f289 -Version v1.2.0 -AdoToken $env:SYSTEM_ACCESSTOKEN -OutDir ./evidencias-smoke
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][string]$FunctionApp,
    [Parameter(Mandatory)][string]$BaseUrl,
    [string]$RepoRoot,
    [Parameter(Mandatory)][string]$Candidate,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$AdoToken,
    [Parameter(Mandatory)][string]$OutDir,
    [int]$MaxIntentos = 2,
    [int]$PassEsperados = 6
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }
if ([string]::IsNullOrWhiteSpace($RepoRoot)) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path }

function Get-SmokeCasos {
    # Filas de la tabla "## Casos" del report.md: Status (columna 3) y texto completo de la fila.
    param([string]$ReportPath)
    $filas = @()
    $enCasos = $false
    foreach ($linea in (Get-Content -LiteralPath $ReportPath -Encoding UTF8)) {
        if ($linea -match '^##\s+Casos\s*$') { $enCasos = $true; continue }
        if (-not $enCasos) { continue }
        if ($linea -notmatch '^\|') { continue }
        $celdas = @($linea.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
        if ($celdas.Count -lt 3 -or $celdas[0] -eq 'Caso' -or $celdas[0] -match '^-+$') { continue }
        $filas += [pscustomobject]@{ Caso = $celdas[0]; Status = $celdas[2]; Texto = $linea }
    }
    return , $filas
}

function Invoke-SmokePasada {
    # Lanza el runner y devuelve el codigo de salida y el directorio de informe nuevo.
    param([string]$Runner, [string]$Alias, [string]$ArtifactsDir, [string]$CasesDir)
    $antes = @()
    if (Test-Path -LiteralPath $ArtifactsDir) { $antes = @(Get-ChildItem -LiteralPath $ArtifactsDir -Directory | ForEach-Object { $_.Name }) }
    $parametros = @{ Environment = $Alias; Profile = 'smoke' }
    if ($CasesDir) { $parametros['CasesDir'] = $CasesDir }
    $null = & $Runner @parametros
    $codigo = $LASTEXITCODE
    $nuevo = $null
    if (Test-Path -LiteralPath $ArtifactsDir) {
        $nuevo = @(Get-ChildItem -LiteralPath $ArtifactsDir -Directory | Where-Object { $antes -notcontains $_.Name } | Sort-Object LastWriteTime, Name) | Select-Object -Last 1
    }
    return [pscustomobject]@{ Exit = $codigo; Dir = $(if ($nuevo) { $nuevo.FullName } else { $null }) }
}

$alias = ConvertTo-EnvironmentAlias -Environment $Environment
$e2eDir = Join-Path $RepoRoot 'tests' 'e2e-postdeploy'
$runner = Join-Path $e2eDir 'run-e2e-postdeploy.ps1'
$publicador = Join-Path $e2eDir 'ado' 'publish-smk-run.ps1'
$artifactsDir = Join-Path $e2eDir 'artifacts'
$configDir = Join-Path $e2eDir 'config'
$envJson = Join-Path $configDir 'environments.json'

Write-Host "##vso[task.setsecret]$AdoToken"

$clave = Invoke-ReleaseAz -Arguments @('functionapp', 'keys', 'list', '-n', $FunctionApp, '-g', $ResourceGroup, '--query', 'masterKey')
$claveCodigo = $LASTEXITCODE
if ($claveCodigo -ne 0 -or $null -eq $clave -or [string]::IsNullOrWhiteSpace([string]$clave)) {
    Write-Host "No se pudo leer masterKey de $FunctionApp"
    exit 2
}
$clave = [string]$clave
Write-Host "##vso[task.setsecret]$clave"

try {
    if (-not (Test-Path -LiteralPath $OutDir)) { $null = New-Item -ItemType Directory -Path $OutDir -Force -ErrorAction Stop }
}
catch {
    Write-Host "No se pudo crear ${OutDir}: $($_.Exception.Message)"
    exit 2
}

function Invoke-SmokeCuerpo {
    # Primera pasada (smoke), con un reintento si solo hay ERROR y ningun FAIL.
    $pasada1 = $null
    $casos1 = @()
    $intentos = 0
    while ($true) {
        $intentos++
        Write-Host "Smoke primera pasada, intento $intentos de $MaxIntentos"
        $pasada1 = Invoke-SmokePasada -Runner $runner -Alias $alias -ArtifactsDir $artifactsDir -CasesDir ''
        if ($pasada1.Exit -notin 0, 1 -or -not $pasada1.Dir) {
            Write-Host "El runner E2E fallo (exit $($pasada1.Exit)) o no genero informe"
            return 2
        }
        $informe1 = Join-Path $pasada1.Dir 'report.md'
        if (-not (Test-Path -LiteralPath $informe1)) { Write-Host "Falta report.md en $($pasada1.Dir)"; return 2 }
        $casos1 = Get-SmokeCasos -ReportPath $informe1
        $hayFail = @($casos1 | Where-Object { $_.Status -eq 'FAIL' }).Count -gt 0
        $hayError = @($casos1 | Where-Object { $_.Texto -cmatch '\bERROR\b' }).Count -gt 0
        if ($pasada1.Exit -eq 1 -and -not $hayFail -and $hayError -and $intentos -lt $MaxIntentos) {
            Write-Host 'La pasada tiene ERROR y ningun FAIL: se repite'
            continue
        }
        break
    }
    $pass1 = @($casos1 | Where-Object { $_.Status -eq 'PASS' }).Count
    Write-Host "Primera pasada: $pass1 PASS (se esperan $PassEsperados)"

    # Segunda pasada: casos extra del smoke de release.
    $pasada2 = Invoke-SmokePasada -Runner $runner -Alias $alias -ArtifactsDir $artifactsDir -CasesDir (Join-Path $e2eDir 'cases-smk')
    if ($pasada2.Exit -notin 0, 1 -or -not $pasada2.Dir) {
        Write-Host "La segunda pasada fallo (exit $($pasada2.Exit)) o no genero informe"
        return 2
    }

    # Publicacion en el Test Plan: ensayo y despues real.
    $dirs = @($pasada1.Dir, $pasada2.Dir)
    $null = & $publicador -Environment $alias -Candidate $Candidate -ReportDirs $dirs -Version $Version -Pat $AdoToken -WhatIf
    if ($LASTEXITCODE -ne 0) { Write-Host "El ensayo de publicacion (-WhatIf) fallo (exit $LASTEXITCODE)"; return 2 }
    $null = & $publicador -Environment $alias -Candidate $Candidate -ReportDirs $dirs -Version $Version -Pat $AdoToken
    $publicado = ($LASTEXITCODE -eq 0)
    if (-not $publicado) { Write-Host "La publicacion en el Test Plan fallo (exit $LASTEXITCODE)" }

    # Evidencias: los dos directorios de informes (con ado-run-<id>.json), sin environments.json.
    foreach ($d in $dirs) {
        $destino = Join-Path $OutDir (Split-Path $d -Leaf)
        Copy-Item -LiteralPath $d -Destination $destino -Recurse -Force
        Get-ChildItem -LiteralPath $destino -Recurse -File -Filter 'environments*.json' | Remove-Item -Force
    }
    $resumen = [ordered]@{
        entorno = $alias; candidato = $Candidate; version = $Version
        intentosPrimeraPasada = $intentos; passPrimeraPasada = $pass1; passEsperados = $PassEsperados
        exitSegundaPasada = $pasada2.Exit; publicado = $publicado
        informes = @($dirs | ForEach-Object { Split-Path $_ -Leaf })
    }
    [IO.File]::WriteAllText((Join-Path $OutDir 'smoke-resumen.json'), (ConvertTo-Json -InputObject $resumen -Depth 4))

    if ($pass1 -ne $PassEsperados) {
        Write-Host "La primera pasada no tiene $PassEsperados PASS (tiene $pass1)"
        return 2
    }
    if (-not $publicado) { return 2 }
    return 0
}

$codigoSalida = 2
$existia = Test-Path -LiteralPath $envJson
$respaldo = $null
try {
    if ($existia) { $respaldo = [IO.File]::ReadAllBytes($envJson) }
    if (-not (Test-Path -LiteralPath $configDir)) { $null = New-Item -ItemType Directory -Path $configDir -Force -ErrorAction Stop }
    $config = [ordered]@{ $alias = [ordered]@{ baseUrl = $BaseUrl; functionKey = $clave; healthCheck = $true } }
    [IO.File]::WriteAllText($envJson, (ConvertTo-Json -InputObject $config -Depth 4))

    $resultadoCuerpo = @(Invoke-SmokeCuerpo)
    $codigoSalida = [int]$resultadoCuerpo[-1]
}
catch {
    Write-Host "Fallo inesperado en el smoke: $($_.Exception.Message)"
    $codigoSalida = 2
}
finally {
    try {
        if ($existia) { [IO.File]::WriteAllBytes($envJson, $respaldo) }
        elseif (Test-Path -LiteralPath $envJson) { Remove-Item -LiteralPath $envJson -Force }
    }
    catch { Write-Host "No se pudo reponer environments.json: $($_.Exception.Message)" }
}
exit $codigoSalida
