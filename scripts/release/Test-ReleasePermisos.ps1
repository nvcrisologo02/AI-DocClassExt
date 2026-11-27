<#
.SYNOPSIS
    Comprueba con lecturas de solo lectura los permisos de la service connection del entorno.

.DESCRIPTION
    Ejecuta exactamente las lecturas de las tablas 3.1 a 3.3 de docs/procedimientos/PERMISOS_SPN_RELEASE.md
    (Key Vault, storage de documentos de destino y de origen de la copia, claves de la cuenta del task hub,
    claves y estado de la Function App, credenciales de publicacion de las tres apps) mas dos
    comprobaciones nuevas: Kudu alcanzable (GET https://<app>.scm.azurewebsites.net/api/ con las
    credenciales de publicacion, las tres apps) y acceso SQL (SELECT 1).

    Nunca imprime secretos: de las credenciales de publicacion solo se consulta el nombre de usuario;
    la clave se usa unicamente para cabecera Basic de la peticion a Kudu.

    Por cada lectura fallida escribe una linea "<recurso> - <rol que falta> - PERMISOS_SPN_RELEASE.md, apartado 3.x"
    (el 3.x es 3.1 dev, 3.2 pre y 3.3 prod). Codigo de salida 0 si todo pasa y 2 si falla alguna.

.PARAMETER Environment
    Entorno: dev, pre o prod.

.PARAMETER SqlServer
    Nombre corto del servidor SQL del entorno (SQL_SERVER_NAME).

.PARAMETER SqlDatabase
    Base de datos. Por defecto DocumentIA.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Test-ReleasePermisos.ps1 -Environment pre -SqlServer srbsqlpredocai
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
    [Parameter(Mandatory)][string]$SqlServer,
    [string]$SqlDatabase = 'DocumentIA'
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

# Recursos por entorno (apartado 2 de PERMISOS_SPN_RELEASE.md).
$tabla = @{
    dev  = @{
        rg = 'SRBRGDEVDOCSAI'; kv = 'srbkvdevdocai'; taskhub = 'srbstgdevappdocai'
        stg = 'srbstgdevdocai'; origen = $null
        func = 'srbappdevdocai'; admin = 'srbwebadmindevdocai'; resolver = 'srbwebpluginassetresolverdev'
    }
    pre  = @{
        rg = 'SRBRGPREDOCSAI'; kv = 'srbkvpredocai'; taskhub = 'srbstgpreappdocai'
        stg = 'srbstgpredocai'; origen = 'srbstgdevdocai'
        func = 'srbapppredocai'; admin = 'srbwebadminpredocai'; resolver = 'srbwebpluginassetresolverpre'
    }
    prod = @{
        rg = 'SRBRGDOCSAIPROD'; kv = 'srbkvprodocai'; taskhub = 'srbstgproapppdocai'
        stg = 'srbstgprodocai'; origen = 'srbstgpredocai'
        func = 'srbappprodocai'; admin = 'srbwebadminprodocai'; resolver = 'srbwebpluginassetresolver'
    }
}
$r = $tabla[$Environment]
$sec = 'apartado 3.' + @{ dev = '1'; pre = '2'; prod = '3' }[$Environment]
$fallos = [System.Collections.Generic.List[string]]::new()

function Add-Fallo([string]$Recurso, [string]$Rol, [string]$Seccion) {
    $linea = "$Recurso - $Rol - PERMISOS_SPN_RELEASE.md, $Seccion"
    $fallos.Add($linea)
    Write-Host $linea
}

# Ejecuta una lectura az y devuelve $true si termino bien y devolvio valor.
function Test-Lectura([string[]]$Arguments) {
    $ErrorActionPreference = 'Continue'
    $res = Invoke-ReleaseAz -Arguments $Arguments
    return ($LASTEXITCODE -eq 0)
}

function Test-KuduAlcanzable([string]$App, [string]$Grupo, [string]$Tipo) {
    $ErrorActionPreference = 'Continue'
    $az = if ($Tipo -eq 'functionapp') { 'functionapp' } else { 'webapp' }
    $cred = Invoke-ReleaseAz -Arguments @($az, 'deployment', 'list-publishing-credentials', '-n', $App, '-g', $Grupo, '--query', '{u:publishingUserName,p:publishingPassword}')
    if ($LASTEXITCODE -ne 0 -or $null -eq $cred) { return $false }
    $par = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("$($cred.u):$($cred.p)"))
    try {
        $null = Invoke-WebRequest -UseBasicParsing -Method Get -TimeoutSec 30 -Uri "https://$App.scm.azurewebsites.net/api/" -Headers @{ Authorization = "Basic $par" }
        return $true
    }
    catch { return $false }
}

# 3.x: Key Vault.
if (-not (Test-Lectura @('keyvault', 'secret', 'list', '--vault-name', $r.kv, '--query', 'length(@)'))) {
    Add-Fallo $r.kv 'Key Vault Secrets Officer' $sec
}
# Storage de documentos de destino y origen de la copia (contenedor documentai).
foreach ($stg in @($r.stg, $r.origen) | Where-Object { $_ }) {
    $rol = if ($stg -eq $r.stg) { 'Storage Blob Data Reader/Contributor' } else { 'Storage Blob Data Reader (origen de la copia)' }
    if (-not (Test-Lectura @('storage', 'blob', 'list', '--account-name', $stg, '--container-name', 'documentai', '--auth-mode', 'login', '--num-results', '1'))) {
        Add-Fallo $stg $rol $sec
    }
}
# Storage del task hub.
if (-not (Test-Lectura @('storage', 'account', 'keys', 'list', '--account-name', $r.taskhub, '--query', '[0].keyName', '-o', 'tsv'))) {
    Add-Fallo $r.taskhub 'Contributor (listKeys) o Storage Table Data Reader' $sec
}
# Function App: claves, estado y credenciales de publicacion.
if (-not (Test-Lectura @('functionapp', 'keys', 'list', '-n', $r.func, '-g', $r.rg, '--query', 'length(functionKeys)'))) {
    Add-Fallo $r.func 'lectura de claves de funcion' $sec
}
if (-not (Test-Lectura @('functionapp', 'show', '-n', $r.func, '-g', $r.rg, '--query', 'state', '-o', 'tsv'))) {
    Add-Fallo $r.func 'lectura y reinicio de la Function App' $sec
}
if (-not (Test-Lectura @('functionapp', 'deployment', 'list-publishing-credentials', '-n', $r.func, '-g', $r.rg, '--query', 'publishingUserName', '-o', 'tsv'))) {
    Add-Fallo $r.func 'credenciales de publicacion (Kudu)' $sec
}
# Admin y AssetResolver: credenciales de publicacion.
foreach ($app in @($r.admin, $r.resolver)) {
    if (-not (Test-Lectura @('webapp', 'deployment', 'list-publishing-credentials', '-n', $app, '-g', $r.rg, '--query', 'publishingUserName', '-o', 'tsv'))) {
        Add-Fallo $app 'credenciales de publicacion (Kudu)' $sec
    }
}
# Nuevas: Kudu alcanzable (las tres apps) y acceso SQL.
foreach ($par in @(@($r.func, 'functionapp'), @($r.admin, 'webapp'), @($r.resolver, 'webapp'))) {
    if (-not (Test-KuduAlcanzable -App $par[0] -Grupo $r.rg -Tipo $par[1])) {
        Add-Fallo $par[0] 'Kudu alcanzable con las credenciales de publicacion' $sec
    }
}
try {
    $uno = Invoke-ReleaseSql -Server $SqlServer -Database $SqlDatabase -Query 'SELECT 1' -Scalar
    if ([int]$uno -ne 1) { throw 'resultado inesperado' }
}
catch {
    Add-Fallo "$SqlServer/$SqlDatabase" 'acceso SQL (db_datareader y db_datawriter)' 'apartado 1 (grant-pipeline-sql-user.sql)'
}

if ($fallos.Count -gt 0) {
    Write-Host "Permisos insuficientes en '$Environment': $($fallos.Count) lectura(s) fallida(s)."
    exit 2
}
Write-Host "Permisos de '$Environment' comprobados: todas las lecturas correctas."
exit 0
