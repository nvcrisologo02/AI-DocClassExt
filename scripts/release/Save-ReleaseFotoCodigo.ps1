<#
.SYNOPSIS
    Foto del codigo desplegado de las tres apps (Functions, Admin, AssetResolver) por Kudu.

.DESCRIPTION
    Por cada app lee las credenciales de publicacion (secretos: se marcan con
    ##vso[task.setsecret] y no se imprimen ni se guardan), descarga
    https://<app>.scm.azurewebsites.net/api/zip/site/wwwroot/ a <OutDir>/<clave>.zip, comprueba que
    el zip no esta vacio y tiene al menos una entrada, y escribe <OutDir>/foto-codigo.json con una
    entrada por app (app, zip, bytes, sha256). La vuelta atras redespliega esos zips.
    Codigo de salida 0 si todo va bien y 2 si falla cualquier app o la escritura.

.PARAMETER ResourceGroup
    Resource group de las tres apps.

.PARAMETER Apps
    Hashtable clave -> nombre de app: @{ functions = '...'; admin = '...'; assetresolver = '...' }.
    La clave 'functions' usa az functionapp; el resto, az webapp.

.PARAMETER OutDir
    Carpeta de salida (se crea si no existe).

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Save-ReleaseFotoCodigo.ps1 -ResourceGroup SRBRGDEVDOCSAI -Apps @{ functions = 'srbappdevdocai'; admin = 'srbwebadmindevdocai'; assetresolver = 'srbwebpluginassetresolverdev' } -OutDir ./foto-codigo
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][hashtable]$Apps,
    [Parameter(Mandatory)][string]$OutDir
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }
Add-Type -AssemblyName System.IO.Compression.FileSystem

try {
    if (-not (Test-Path -LiteralPath $OutDir)) { $null = New-Item -ItemType Directory -Path $OutDir -Force -ErrorAction Stop }
}
catch {
    Write-Host "No se pudo crear ${OutDir}: $($_.Exception.Message)"
    exit 2
}

$foto = @()
foreach ($clave in @($Apps.Keys | Sort-Object { if ($_ -eq 'functions') { 0 } elseif ($_ -eq 'admin') { 1 } else { 2 } })) {
    $app = [string]$Apps[$clave]
    $tipo = if ($clave -eq 'functions') { 'functionapp' } else { 'webapp' }

    $cred = Invoke-ReleaseAz -Arguments @($tipo, 'deployment', 'list-publishing-credentials', '-n', $app, '-g', $ResourceGroup, '--query', '{u:publishingUserName,p:publishingPassword}')
    $codigo = $LASTEXITCODE
    if ($codigo -ne 0 -or $null -eq $cred -or [string]::IsNullOrEmpty($cred.u) -or [string]::IsNullOrEmpty($cred.p)) {
        Write-Host "No se pudieron leer las credenciales de publicacion de $app"
        exit 2
    }
    $usuario = [string]$cred.u
    $clavePub = [string]$cred.p
    Write-Host "##vso[task.setsecret]$clavePub"
    $basic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${usuario}:${clavePub}"))
    Write-Host "##vso[task.setsecret]$basic"

    $zip = Join-Path $OutDir "$clave.zip"
    try {
        Invoke-WebRequest -Uri "https://$app.scm.azurewebsites.net/api/zip/site/wwwroot/" -Headers @{ Authorization = "Basic $basic" } -OutFile $zip -ErrorAction Stop | Out-Null
    }
    catch {
        Write-Host "Kudu no devolvio el codigo de ${app}: $($_.Exception.Message)"
        exit 2
    }

    $bytes = if (Test-Path -LiteralPath $zip) { (Get-Item -LiteralPath $zip).Length } else { 0 }
    if ($bytes -le 0) {
        Write-Host "El zip del codigo de $app esta vacio"
        exit 2
    }
    $entradas = 0
    try {
        $z = [IO.Compression.ZipFile]::OpenRead($zip)
        try { $entradas = $z.Entries.Count } finally { $z.Dispose() }
    }
    catch {
        Write-Host "El zip del codigo de $app no es valido: $($_.Exception.Message)"
        exit 2
    }
    if ($entradas -lt 1) {
        Write-Host "El zip del codigo de $app no tiene entradas"
        exit 2
    }

    $foto += [pscustomobject][ordered]@{
        app    = $app
        zip    = "$clave.zip"
        bytes  = [long]$bytes
        sha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    Write-Host "Codigo de $app guardado en $zip ($bytes bytes, $entradas entradas)"
}

# Un fallo de escritura debe parar el script: la vuelta atras depende de foto-codigo.json.
try {
    $json = ConvertTo-Json -InputObject @($foto) -Depth 3
    [IO.File]::WriteAllText((Join-Path $OutDir 'foto-codigo.json'), $json + "`n")
}
catch {
    Write-Host "No se pudo escribir foto-codigo.json en ${OutDir}: $($_.Exception.Message)"
    exit 2
}
Write-Host (Join-Path $OutDir 'foto-codigo.json')
exit 0
