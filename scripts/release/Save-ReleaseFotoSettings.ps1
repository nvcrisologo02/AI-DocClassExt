<#
.SYNOPSIS
    Foto de los app settings que toca la release y evidencias previas del paso 1 de la guia.

.DESCRIPTION
    Lee con UNA llamada la lista de app settings de la Function App. La lista completa solo vive
    en memoria (lleva cadenas de conexion): nunca se escribe en disco ni se imprime.
    Escribe en -OutDir:
      - foto-settings.json: objeto { nombre: valor } con PromptDefaults__ModelKey,
        GDC__BypassSslValidation, AzureWebJobs.IngestDocument.Disabled y todas las
        AI__Resources__*__Endpoint. Una clave fija que no existe en la app se escribe con null
        (Restore-ReleaseFotoSettings.ps1 la borra al restaurar).
      - appsettings-ia-antes.txt: nombre=valor de los *__Endpoint salvo GDC__Endpoint.
      - appsettings-nombres-antes.txt: solo los nombres de todos los settings, ordenados.
    Codigo de salida 0 si todo va bien y 2 si falla az o la lista viene vacia.

.PARAMETER ResourceGroup
    Resource group de la Function App.

.PARAMETER FunctionApp
    Nombre de la Function App.

.PARAMETER OutDir
    Carpeta de salida (se crea si no existe).

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Save-ReleaseFotoSettings.ps1 -ResourceGroup SRBRGPREDOCSAI -FunctionApp srbapppredocai -OutDir ./evidencias
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][string]$FunctionApp,
    [Parameter(Mandatory)][string]$OutDir
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

$lista = Invoke-ReleaseAz -Arguments @('functionapp', 'config', 'appsettings', 'list', '-n', $FunctionApp, '-g', $ResourceGroup)
$codigo = $LASTEXITCODE
$lista = @($lista | Where-Object { $null -ne $_ })
if ($codigo -ne 0 -or $lista.Count -eq 0) {
    Write-Host "No se pudo leer la configuracion de $FunctionApp"
    exit 2
}

$clavesFijas = @('PromptDefaults__ModelKey', 'GDC__BypassSslValidation', 'AzureWebJobs.IngestDocument.Disabled')

$foto = [ordered]@{}
foreach ($k in $clavesFijas) { $foto[$k] = $null }
foreach ($s in $lista) {
    if ($clavesFijas -contains $s.name -or $s.name -match '^AI__Resources__.*__Endpoint$') {
        $foto[$s.name] = $s.value
    }
}

$lineasIa = @($lista | Where-Object { $_.name -match '__Endpoint$' -and $_.name -ne 'GDC__Endpoint' } |
        Sort-Object -Property name -CaseSensitive:$false |
        ForEach-Object { '{0}={1}' -f $_.name, $_.value })
$nombres = @($lista | ForEach-Object { [string]$_.name } | Sort-Object)

if (-not (Test-Path -LiteralPath $OutDir)) { $null = New-Item -ItemType Directory -Path $OutDir -Force }
$rutaFoto = Join-Path $OutDir 'foto-settings.json'
$rutaIa = Join-Path $OutDir 'appsettings-ia-antes.txt'
$rutaNombres = Join-Path $OutDir 'appsettings-nombres-antes.txt'

[IO.File]::WriteAllText($rutaFoto, (($foto | ConvertTo-Json -Depth 3) + "`n"))
[IO.File]::WriteAllText($rutaIa, (($lineasIa -join "`n") + "`n"))
[IO.File]::WriteAllText($rutaNombres, (($nombres -join "`n") + "`n"))

Write-Host "Foto de $($foto.Count) claves de $FunctionApp"
Write-Host $rutaFoto
Write-Host $rutaIa
Write-Host $rutaNombres
exit 0
