<#
.SYNOPSIS
    Copia verificada de los artefactos del clasificador por embeddings entre los storages
    de documentos de dos entornos (AB#101570).

.DESCRIPTION
    El clasificador por embeddings carga su artefacto desde el contenedor documentai del
    storage de documentos de cada entorno, bajo el prefijo
    modelos/clasificador-embeddings/<Version>/. Este script lista los blobs de ese prefijo en
    el origen, consulta cada uno en el destino y los copia con comprobacion de MD5.

    Estados por blob:
      copy      no existe en el destino; se copia.
      present   existe en el destino con el mismo contentMd5; no se toca.
      conflict  existe en el destino con OTRO contentMd5 (solo aparece con MD5 distinto);
                solo se sobrescribe con -Force. Sin -Force el script termina con codigo 1.

    Mecanica de copia: descarga del origen a un directorio temporal y subida al destino, ambas
    con az storage blob y --auth-mode login. No se usa az storage blob copy start porque con
    --auth-mode login solo se autentica el destino y para un origen privado exige SAS o clave
    de cuenta. Tras subir se compara el contentMd5 del destino con el del origen (si el destino
    no informa MD5, se calcula el MD5 del fichero temporal). El temporal se borra siempre.

    Con -DryRun solo se lista y se consulta: no se descarga ni se sube nada.

    Roles necesarios (RBAC de datos, sin claves de cuenta):
      Origen   Storage Blob Data Reader (o superior) en la cuenta de origen.
      Destino  Storage Blob Data Contributor en la cuenta de destino.

    Codigo de salida: 0 si todo queda present o copiado con MD5 igual; 1 si queda algun
    conflict sin -Force, algun MD5 distinto tras copiar, el origen no tiene blobs o az falla.

    Cuentas (contenedor documentai en todas):
      dev  srbstgdevdocai
      pre  srbstgpredocai
      prod srbstgprodocai

.PARAMETER Version
    Version del artefacto, con formato vN (por ejemplo v5).
.PARAMETER TargetEnvironment
    Entorno destino: pre o prod. dev solo se admite con -DryRun (solo lectura).
.PARAMETER SourceEnvironment
    Entorno origen: dev (por defecto) o pre. No puede coincidir con el destino salvo con -DryRun.
.PARAMETER DryRun
    Solo lectura: lista, consulta y muestra la tabla; 0 escrituras.
.PARAMETER Force
    Sobrescribe los blobs en estado conflict.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/storage/copy-embeddings-artifacts.ps1 -Version v5 -TargetEnvironment pre -DryRun
    pwsh -NoProfile -File ./scripts/storage/copy-embeddings-artifacts.ps1 -Version v5 -TargetEnvironment pre
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^v\d+$')][string]$Version,
    [Parameter(Mandatory = $true)][ValidateSet('dev', 'pre', 'prod')][string]$TargetEnvironment,
    [ValidateSet('dev', 'pre')][string]$SourceEnvironment = 'dev',
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$cuentas = @{
    dev  = 'srbstgdevdocai'
    pre  = 'srbstgpredocai'
    prod = 'srbstgprodocai'
}
$contenedor = 'documentai'
$prefijo = "modelos/clasificador-embeddings/$Version/"

# Ejecuta az capturando stdout y stderr por separado (los avisos de az van por stderr).
function Invoke-AzCapturado {
    param([string[]]$Argumentos)
    $errFile = [IO.Path]::GetTempFileName()
    try {
        $out = & az @Argumentos 2> $errFile
        $codigo = $LASTEXITCODE
        $err = Get-Content -Raw -LiteralPath $errFile -ErrorAction SilentlyContinue
        if ($null -eq $err) { $err = '' }
    } finally {
        Remove-Item -LiteralPath $errFile -Force -ErrorAction SilentlyContinue
    }
    [pscustomobject]@{ Codigo = $codigo; Salida = (($out | Out-String).Trim()); Error = $err.Trim() }
}

function Get-Md5Blob {
    param($Blob)
    if ($Blob.properties -and $Blob.properties.contentSettings) { return $Blob.properties.contentSettings.contentMd5 }
    return $null
}

function Get-Md5Fichero {
    param([string]$Ruta)
    return [Convert]::ToBase64String([Security.Cryptography.MD5]::HashData([IO.File]::ReadAllBytes($Ruta)))
}

$tmpDir = $null
try {
    if ($SourceEnvironment -eq $TargetEnvironment -and -not $DryRun) {
        throw "Origen y destino son el mismo entorno ($SourceEnvironment); solo se admite con -DryRun."
    }
    if ($TargetEnvironment -eq 'dev' -and -not $DryRun) {
        throw "El entorno dev no es un destino de copia; solo se admite como destino con -DryRun."
    }
    $origen = $cuentas[$SourceEnvironment]
    $destino = $cuentas[$TargetEnvironment]
    Write-Host "Origen: $origen ($SourceEnvironment)  Destino: $destino ($TargetEnvironment)  Prefijo: $prefijo"

    $r = Invoke-AzCapturado @('storage', 'blob', 'list', '--account-name', $origen, '--container-name', $contenedor,
        '--prefix', $prefijo, '--auth-mode', 'login', '-o', 'json')
    if ($r.Codigo -ne 0) { throw "Fallo al listar el origen $origen (az codigo $($r.Codigo)): $($r.Error)" }
    $blobs = @($r.Salida | ConvertFrom-Json)
    if ($blobs.Count -eq 0) { throw "El origen $origen no tiene ningun blob con el prefijo $prefijo" }

    $filas = @()
    foreach ($b in $blobs) {
        $md5Origen = Get-Md5Blob $b
        $s = Invoke-AzCapturado @('storage', 'blob', 'show', '--account-name', $destino, '--container-name', $contenedor,
            '--name', $b.name, '--auth-mode', 'login', '-o', 'json')
        $md5Destino = $null
        if ($s.Codigo -eq 0) {
            $md5Destino = Get-Md5Blob ($s.Salida | ConvertFrom-Json)
            if ($md5Origen -and $md5Destino -and $md5Origen -eq $md5Destino) { $estado = 'present' } else { $estado = 'conflict' }
        } elseif ($s.Error -match 'BlobNotFound|ResourceNotFound|does not exist') {
            $estado = 'copy'
        } else {
            throw "Fallo al consultar el destino $destino para $($b.name) (az codigo $($s.Codigo)): $($s.Error)"
        }
        $filas += [pscustomobject]@{ Blob = $b.name; Estado = $estado; Md5Origen = $md5Origen; Md5Destino = $md5Destino }
    }

    if ($DryRun) {
        Write-Host "DryRun: 0 escrituras"
    } else {
        $tmpDir = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())
        New-Item -ItemType Directory -Path $tmpDir | Out-Null
        foreach ($f in $filas) {
            $copiar = ($f.Estado -eq 'copy') -or ($f.Estado -eq 'conflict' -and $Force)
            if (-not $copiar) { continue }
            $fichero = Join-Path $tmpDir ([guid]::NewGuid().ToString())
            $d = Invoke-AzCapturado @('storage', 'blob', 'download', '--account-name', $origen, '--container-name', $contenedor,
                '--name', $f.Blob, '--file', $fichero, '--auth-mode', 'login', '-o', 'none')
            if ($d.Codigo -ne 0) { throw "Fallo al descargar $($f.Blob) de $origen (az codigo $($d.Codigo)): $($d.Error)" }
            $argsSubida = @('storage', 'blob', 'upload', '--account-name', $destino, '--container-name', $contenedor,
                '--name', $f.Blob, '--file', $fichero, '--auth-mode', 'login', '-o', 'none')
            if ($f.Estado -eq 'conflict') { $argsSubida += '--overwrite' }
            $u = Invoke-AzCapturado $argsSubida
            if ($u.Codigo -ne 0) { throw "Fallo al subir $($f.Blob) a $destino (az codigo $($u.Codigo)): $($u.Error)" }
            $v = Invoke-AzCapturado @('storage', 'blob', 'show', '--account-name', $destino, '--container-name', $contenedor,
                '--name', $f.Blob, '--auth-mode', 'login', '-o', 'json')
            if ($v.Codigo -ne 0) { throw "Fallo al verificar $($f.Blob) en $destino (az codigo $($v.Codigo)): $($v.Error)" }
            $md5Nuevo = Get-Md5Blob ($v.Salida | ConvertFrom-Json)
            if (-not $md5Nuevo) { $md5Nuevo = Get-Md5Fichero $fichero }
            if (-not $f.Md5Origen) { $f.Md5Origen = Get-Md5Fichero $fichero }
            $f.Md5Destino = $md5Nuevo
            if ($f.Md5Origen -eq $md5Nuevo) { $f.Estado = 'copiado' } else { $f.Estado = 'md5-distinto' }
            Remove-Item -LiteralPath $fichero -Force -ErrorAction SilentlyContinue
        }
    }

    $filas | Format-Table Blob, Estado, Md5Origen, Md5Destino -AutoSize | Out-String -Width 300 | Write-Host

    $fallos = @($filas | Where-Object { $_.Estado -in @('conflict', 'md5-distinto') })
    if ($fallos.Count -gt 0) {
        Write-Host "Quedan $($fallos.Count) blob(s) en conflict o con MD5 distinto (conflict requiere -Force)."
        exit 1
    }
    exit 0
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
    exit 1
} finally {
    if ($tmpDir -and (Test-Path -LiteralPath $tmpDir)) { Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue }
}
