<#
.SYNOPSIS
    Comprueba con lecturas de solo lectura los permisos de la service connection del entorno.

.DESCRIPTION
    Ejecuta exactamente las lecturas de las tablas 3.1 a 3.3 de docs/procedimientos/PERMISOS_SPN_RELEASE.md
    (Key Vault, storage de documentos de destino y de origen de la copia, claves de la cuenta del task hub,
    claves y estado de la Function App) mas dos comprobaciones nuevas: Kudu alcanzable (GET
    https://<app>.scm.azurewebsites.net/api/settings con Bearer del token de ARM de la cuenta
    activa de az, las tres apps; la autenticacion basica de SCM esta deshabilitada y no se
    reactiva, por eso ya no se leen credenciales de publicacion) y acceso SQL (SELECT 1).

    Nunca imprime secretos: el token de Entra solo se usa en la cabecera Authorization de la peticion a
    Kudu. Si Kudu falla escribe "Kudu <app>: HTTP <estado>" o "Kudu <app>: <mensaje>".

    Por cada lectura fallida escribe una linea "<recurso> - <rol que falta> - PERMISOS_SPN_RELEASE.md, apartado 3.x"
    (el 3.x es 3.1 dev, 3.2 pre y 3.3 prod). Codigo de salida 0 si todo pasa y 2 si falla alguna.

.PARAMETER Environment
    Entorno: dev, pre o prod.

.PARAMETER SqlServer
    Nombre corto del servidor SQL del entorno (SQL_SERVER_NAME).

.PARAMETER SqlDatabase
    Base de datos. Por defecto DocumentIA.

.PARAMETER SinKudu
    Salta las comprobaciones de Kudu. Es lo que usa Guard en el pool privado, desde el que el scm de
    las apps no resuelve (endpoint privado sin enlace DNS a la VNet del pool).

.PARAMETER SoloKudu
    Hace solo las comprobaciones de Kudu (las tres apps) y sale con 0 o con 2. Es lo que usa el job
    GuardKudu desde un agente alojado. Incompatible con -SinKudu.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Test-ReleasePermisos.ps1 -Environment pre -SqlServer srbsqlpredocai
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
    [Parameter(Mandatory)][string]$SqlServer,
    [string]$SqlDatabase = 'DocumentIA',
    [switch]$SinKudu,
    [switch]$SoloKudu
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }
if ($SinKudu -and $SoloKudu) {
    Write-Host '-SinKudu y -SoloKudu son incompatibles.'
    exit 2
}

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

$script:TokenKudu = $null

function Test-KuduAlcanzable([string]$App) {
    $ErrorActionPreference = 'Continue'
    if ($null -eq $script:TokenKudu) { $script:TokenKudu = Get-ReleaseKuduToken }
    if ([string]::IsNullOrEmpty($script:TokenKudu)) {
        Write-Host "Kudu ${App}: sin token de Entra"
        return $false
    }
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -Method Get -TimeoutSec 30 -SkipHttpErrorCheck -Uri "https://$App.scm.azurewebsites.net/api/settings" -Headers @{ Authorization = "Bearer $($script:TokenKudu)" }
        if ([int]$resp.StatusCode -eq 200) { return $true }
        Write-Host "Kudu ${App}: HTTP $([int]$resp.StatusCode)"
        return $false
    }
    catch {
        $m = [string]$_.Exception.Message
        if ($m.Length -gt 200) { $m = $m.Substring(0, 200) }
        Write-Host "Kudu ${App}: $m"
        return $false
    }
}

if (-not $SoloKudu) {
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
    # Function App: claves y estado.
    if (-not (Test-Lectura @('functionapp', 'keys', 'list', '-n', $r.func, '-g', $r.rg, '--query', 'length(functionKeys)'))) {
        Add-Fallo $r.func 'lectura de claves de funcion' $sec
    }
    if (-not (Test-Lectura @('functionapp', 'show', '-n', $r.func, '-g', $r.rg, '--query', 'state', '-o', 'tsv'))) {
        Add-Fallo $r.func 'lectura y reinicio de la Function App' $sec
    }
    # Acceso SQL.
    try {
        $uno = Invoke-ReleaseSql -Server $SqlServer -Database $SqlDatabase -Query 'SELECT 1' -Scalar
        if ([int]$uno -ne 1) { throw 'resultado inesperado' }
    }
    catch {
        Add-Fallo "$SqlServer/$SqlDatabase" 'acceso SQL (db_datareader y db_datawriter)' 'apartado 1 (grant-pipeline-sql-user.sql)'
    }
}
# Kudu alcanzable con token de Entra (las tres apps).
if (-not $SinKudu) {
    foreach ($par in @(@($r.func), @($r.admin), @($r.resolver))) {
        if (-not (Test-KuduAlcanzable -App $par[0])) {
            Add-Fallo $par[0] 'Kudu alcanzable con token de Entra (Contributor sobre el RG)' $sec
        }
    }
}

if ($fallos.Count -gt 0) {
    Write-Host "Permisos insuficientes en '$Environment': $($fallos.Count) lectura(s) fallida(s)."
    exit 2
}
Write-Host "Permisos de '$Environment' comprobados: todas las lecturas correctas."
exit 0
