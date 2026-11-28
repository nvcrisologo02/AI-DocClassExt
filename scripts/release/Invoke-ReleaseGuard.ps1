<#
.SYNOPSIS
    Stage Guard de la pipeline Release: valida rama, manifiesto, aprobadores, simulacion, registro y permisos.

.DESCRIPTION
    Comprobaciones, en orden: pwsh >= 7 y az presentes; rama esperada por entorno
    (Get-ReleaseBranchEsperada; dev admite cualquiera); manifiesto valido (Get-ReleaseManifest.ps1);
    aprobadores.yml con aprobador1 y aprobador2 no vacios; SimularFalloEn vacio salvo en dev; estado del
    registro ReleaseAplicada (Cerrada => yaAplicada=true, el resto de stages se saltan); permisos
    (Test-ReleasePermisos.ps1, salvo -SinPermisos, que solo usan los tests).

    Se recogen todos los fallos: cada uno sale como ##vso[task.logissue type=error] y, si hay alguno,
    el script termina con 2. Si no, emite las variables de salida version, manifestPath, yaAplicada e
    ignorarRegistro (true solo para la version _ensayo) y termina con 0.

    aprobadores.yml se lee con una expresion regular, sin libreria YAML. Formato esperado, por cada N:
        - name: aprobadorN
          value: <correo>

.PARAMETER Environment
    Entorno: dev, pre o prod.

.PARAMETER SourceBranch
    Rama de origen del run (Build.SourceBranch, p. ej. refs/heads/develop). Llega por parametro para poder probarla.

.PARAMETER RepoRoot
    Raiz del repo. Por defecto, la del script.

.PARAMETER SqlServer
    Nombre corto del servidor SQL del entorno.

.PARAMETER SqlDatabase
    Base de datos. Por defecto DocumentIA.

.PARAMETER AprobadoresPath
    Ruta de pipelines/templates/variables/aprobadores.yml.

.PARAMETER SimularFalloEn
    Punto de fallo simulado del ensayo; solo se admite vacio fuera de dev.

.PARAMETER SinPermisos
    Omite la comprobacion de permisos (solo para tests).

.PARAMETER SinKudu
    Se pasa a Test-ReleasePermisos.ps1: salta las comprobaciones de Kudu (el scm de las apps no resuelve
    desde el pool privado). Las hace el job GuardKudu desde un agente alojado.

.PARAMETER SoloKudu
    Solo ejecuta Test-ReleasePermisos.ps1 -SoloKudu (sin manifiesto, rama, aprobadores ni registro) y sale con 0
    o con 2, sin variables de salida. Incompatible con -SinKudu.

.PARAMETER PermisosScriptPath
    Ruta del script de permisos. Por defecto, Test-ReleasePermisos.ps1 junto a este (el parametro es para tests).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
    [Parameter(Mandatory)][AllowEmptyString()][string]$SourceBranch,
    [string]$RepoRoot,
    [Parameter(Mandatory)][string]$SqlServer,
    [string]$SqlDatabase = 'DocumentIA',
    [Parameter(Mandatory)][string]$AprobadoresPath,
    [AllowEmptyString()][string]$SimularFalloEn = '',
    [switch]$SinPermisos,
    [switch]$SinKudu,
    [switch]$SoloKudu,
    [string]$PermisosScriptPath
)

$ErrorActionPreference = 'Continue'
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path }
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }
if (-not (Get-Module ReleaseRegistro)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseRegistro.psm1') }

$fallos = [System.Collections.Generic.List[string]]::new()
function Add-Fallo([string]$Mensaje) {
    $fallos.Add($Mensaje)
    Write-Host "##vso[task.logissue type=error]$Mensaje"
    Write-Host $Mensaje
}

# Permisos: Test-ReleasePermisos.ps1 con los switches de Kudu que correspondan.
function Test-PermisosGuard {
    if (-not $PermisosScriptPath) { $PermisosScriptPath = Join-Path $PSScriptRoot 'Test-ReleasePermisos.ps1' }
    if (-not (Test-Path -LiteralPath $PermisosScriptPath -PathType Leaf)) {
        Add-Fallo "No existe el script de permisos '$PermisosScriptPath'; no se puede comprobar."
    }
    else {
        $parametros = @{ Environment = $Environment; SqlServer = $SqlServer; SqlDatabase = $SqlDatabase }
        if ($SinKudu) { $parametros['SinKudu'] = $true }
        if ($SoloKudu) { $parametros['SoloKudu'] = $true }
        $global:LASTEXITCODE = 1
        $salida = ''
        try {
            $salida = & $PermisosScriptPath @parametros 6>&1 | Out-String
        }
        catch {
            $global:LASTEXITCODE = 1
            $salida = "El script de permisos fallo: $($_.Exception.Message)"
        }
        if ($LASTEXITCODE -ne 0) {
            Add-Fallo 'Permisos insuficientes o no comprobables:'
            foreach ($linea in ($salida -split '\r?\n' | Where-Object { $_.Trim() })) {
                Write-Host "##vso[task.logissue type=error]$linea"
            }
        }
    }
}

# Modo -SoloKudu: solo la comprobacion de Kudu (job GuardKudu, agente alojado); sin salidas.
if ($SinKudu -and $SoloKudu) { Add-Fallo '-SinKudu y -SoloKudu son incompatibles.'; exit 2 }
if ($SoloKudu) {
    Test-PermisosGuard
    if ($fallos.Count -gt 0) { exit 2 }
    exit 0
}

# 1. pwsh >= 7 y az.
if ($PSVersionTable.PSVersion.Major -lt 7) { Add-Fallo "Se requiere pwsh 7 o superior (version actual: $($PSVersionTable.PSVersion))." }
if (-not (Get-Command az -ErrorAction SilentlyContinue)) { Add-Fallo 'No se encuentra az en el PATH.' }

# 2. Rama esperada.
$esperada = Get-ReleaseBranchEsperada -Environment $Environment
if ($esperada -and $SourceBranch -cne $esperada) {
    Add-Fallo "Rama '$SourceBranch' no valida para '$Environment': se esperaba '$esperada'."
}

# 3. Manifiesto.
$version = $null
$manifestPath = $null
try {
    $m = & (Join-Path $PSScriptRoot 'Get-ReleaseManifest.ps1') -Environment $Environment -RepoRoot $RepoRoot
    $version = $m.version
    $manifestPath = $m.manifestPath
}
catch { Add-Fallo "Manifiesto de release no valido: $($_.Exception.Message)" }

# 4. Aprobadores.
if (-not (Test-Path -LiteralPath $AprobadoresPath -PathType Leaf)) {
    Add-Fallo "No existe aprobadores.yml en '$AprobadoresPath'."
}
else {
    $yml = [System.IO.File]::ReadAllText($AprobadoresPath)
    foreach ($n in 1, 2) {
        $mt = [regex]::Match($yml, "-\s*name:\s*aprobador$n\s*\r?\n\s*value:[ \t]*(\S*)")
        $valor = if ($mt.Success) { $mt.Groups[1].Value.Trim("'", '"') } else { '' }
        if ([string]::IsNullOrWhiteSpace($valor)) { Add-Fallo "aprobador$n vacio o ausente en aprobadores.yml." }
    }
}

# 5. SimularFalloEn solo en dev.
if ($Environment -ne 'dev' -and -not [string]::IsNullOrEmpty($SimularFalloEn)) {
    Add-Fallo "SimularFalloEn ('$SimularFalloEn') solo se admite en dev."
}

# 6. Registro.
$ignorar = ($version -eq '_ensayo')
$yaAplicada = $false
if ($version) {
    try {
        $estado = Get-ReleaseEstado -Server $SqlServer -Database $SqlDatabase -Version $version -IgnorarRegistro:$ignorar
        $yaAplicada = ($estado.Estado -eq 'Cerrada')
    }
    catch { Add-Fallo "No se pudo leer el registro ReleaseAplicada: $($_.Exception.Message)" }
}

# 7. Permisos.
if (-not $SinPermisos) { Test-PermisosGuard }

if ($fallos.Count -gt 0) { exit 2 }

if ($yaAplicada) {
    # Spec: la version ya aplicada termina en verde sin hacer nada y lo dice (se ve en el resumen del run).
    Write-Host "##vso[task.logissue type=warning]La version $version ya esta cerrada en ${Environment}: el run no toca nada."
}
Write-Host "##vso[task.setvariable variable=version;isOutput=true]$version"
Write-Host "##vso[task.setvariable variable=manifestPath;isOutput=true]$manifestPath"
Write-Host "##vso[task.setvariable variable=yaAplicada;isOutput=true]$($yaAplicada.ToString().ToLowerInvariant())"
Write-Host "##vso[task.setvariable variable=ignorarRegistro;isOutput=true]$($ignorar.ToString().ToLowerInvariant())"
exit 0
