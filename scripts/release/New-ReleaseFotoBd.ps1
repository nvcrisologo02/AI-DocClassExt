<#
.SYNOPSIS
    Foto de las tablas de configuracion: copia cada tabla a <Tabla>__rel_<BuildId>.

.DESCRIPTION
    Por cada tabla ejecuta SELECT * INTO [dbo].[<Tabla>__rel_<BuildId>] FROM [dbo].[<Tabla>].
    Si alguna copia ya existe, no copia nada y sale con codigo 2 (nunca se sobrescribe).
    Escribe foto-bd.json (tabla, copia, filas) en -OutDir. Los nombres se validan con
    ^[A-Za-z0-9_]+$ antes de entrar en SQL. Codigo 0 si todo va bien y 2 si falla una validacion.

.PARAMETER Server
    Servidor SQL (con o sin .database.windows.net).

.PARAMETER Database
    Base de datos.

.PARAMETER BuildId
    Identificador de la build; forma parte del nombre de la copia.

.PARAMETER OutDir
    Carpeta donde se escribe foto-bd.json (se crea si no existe).

.PARAMETER Tablas
    Tablas a copiar. Por defecto CatalogoTdn1, CatalogoTdn2, Tipologias, ModeloConfigs, PromptTemplates.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/New-ReleaseFotoBd.ps1 -Server srbsqlpredocai -Database DocumentIA -BuildId 12345 -OutDir ./out
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Server,
    [Parameter(Mandatory)][string]$Database,
    [Parameter(Mandatory)][string]$BuildId,
    [Parameter(Mandatory)][string]$OutDir,
    [string[]]$Tablas = @('CatalogoTdn1', 'CatalogoTdn2', 'Tipologias', 'ModeloConfigs', 'PromptTemplates')
)

$ErrorActionPreference = 'Stop'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

foreach ($n in @($BuildId) + @($Tablas)) {
    if ($n -cnotmatch '^[A-Za-z0-9_]+$') {
        Write-Host "Nombre no valido (solo letras, digitos y guion bajo): '$n'"
        exit 2
    }
}

$copias = foreach ($t in $Tablas) { [pscustomobject]@{ Tabla = $t; Copia = "${t}__rel_$BuildId" } }

# Primero se comprueba todo: si alguna copia existe no se copia nada.
$existentes = @()
foreach ($c in $copias) {
    $id = Invoke-ReleaseSql -Server $Server -Database $Database -Scalar -Query 'SELECT OBJECT_ID(@Nombre)' -Parameters @{ Nombre = "dbo.[$($c.Copia)]" }
    if ($null -ne $id -and $id -isnot [System.DBNull]) { $existentes += $c.Copia }
}
if ($existentes.Count -gt 0) {
    Write-Host "Ya existen copias, no se sobrescribe nada: $($existentes -join ', ')"
    exit 2
}

$resultado = @()
foreach ($c in $copias) {
    $null = Invoke-ReleaseSql -Server $Server -Database $Database -Query "SELECT * INTO [dbo].[$($c.Copia)] FROM [dbo].[$($c.Tabla)]"
    $filas = [int](Invoke-ReleaseSql -Server $Server -Database $Database -Scalar -Query "SELECT COUNT(*) FROM [dbo].[$($c.Copia)]")
    Write-Host "Foto $($c.Tabla) -> $($c.Copia): $filas filas"
    $resultado += [pscustomobject]@{ tabla = $c.Tabla; copia = $c.Copia; filas = $filas }
}

if (-not (Test-Path -LiteralPath $OutDir)) { $null = New-Item -ItemType Directory -Path $OutDir -Force }
ConvertTo-Json -InputObject @($resultado) -Depth 3 | Set-Content -LiteralPath (Join-Path $OutDir 'foto-bd.json') -Encoding utf8
exit 0
