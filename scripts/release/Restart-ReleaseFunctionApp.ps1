<#
.SYNOPSIS
    Reinicia la Function App y espera a que el healthcheck responda sano.

.DESCRIPTION
    Lanza az functionapp restart, espera -EsperaInicialSegundos y hace POST a -HealthUrl hasta
    -Intentos veces (con -IntervaloSegundos entre intentos). Un intento es correcto solo con HTTP 200
    y un cuerpo que no contiene la palabra "unhealthy" (sin distinguir mayusculas). Un error HTTP o
    de red cuenta como intento fallido. La ultima respuesta (cuerpo) se guarda en -OutFile; si no se
    puede escribir, el script sale con 2. La URL puede llevar la clave en la query: no se imprime.
    Codigo de salida 0 si el health es correcto y 2 en cualquier otro caso.

.PARAMETER ResourceGroup
    Resource group de la Function App.

.PARAMETER FunctionApp
    Nombre de la Function App.

.PARAMETER HealthUrl
    URL del healthcheck (se llama con POST).

.PARAMETER OutFile
    Fichero donde se guarda el cuerpo de la ultima respuesta.

.PARAMETER EsperaInicialSegundos
    Espera tras el reinicio, antes del primer intento.

.PARAMETER Intentos
    Numero maximo de intentos.

.PARAMETER IntervaloSegundos
    Espera entre intentos.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Restart-ReleaseFunctionApp.ps1 -ResourceGroup SRBRGPREDOCSAI -FunctionApp srbapppredocai -HealthUrl https://srbapppredocai.azurewebsites.net/api/health -OutFile ./health.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][string]$FunctionApp,
    [Parameter(Mandatory)][string]$HealthUrl,
    [Parameter(Mandatory)][string]$OutFile,
    [int]$EsperaInicialSegundos = 90,
    [int]$Intentos = 6,
    [int]$IntervaloSegundos = 30
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

# La URL puede llevar la clave de funcion; se oculta de los logs del agente.
Write-Host "##vso[task.setsecret]$HealthUrl"

$null = Invoke-ReleaseAz -Arguments @('functionapp', 'restart', '-n', $FunctionApp, '-g', $ResourceGroup)
if ($LASTEXITCODE -ne 0) {
    Write-Host "No se pudo reiniciar la Function App $FunctionApp"
    exit 2
}
Write-Host "Function App $FunctionApp reiniciada; espera inicial de $EsperaInicialSegundos s"
if ($EsperaInicialSegundos -gt 0) { Start-Sleep -Seconds $EsperaInicialSegundos }

$sano = $false
$cuerpo = ''
for ($i = 1; $i -le $Intentos; $i++) {
    $codigo = 0
    $cuerpo = ''
    try {
        $r = Invoke-WebRequest -Uri $HealthUrl -Method Post -UseBasicParsing -SkipHttpErrorCheck -ErrorAction Stop
        $codigo = [int]$r.StatusCode
        $cuerpo = [string]$r.Content
    }
    catch {
        $cuerpo = "ERROR: $($_.Exception.Message)"
    }
    $sano = ($codigo -eq 200) -and ($cuerpo -notmatch '(?i)unhealthy')
    Write-Host "Intento ${i}/${Intentos}: HTTP $codigo, sano=$sano"
    if ($sano) { break }
    if ($i -lt $Intentos -and $IntervaloSegundos -gt 0) { Start-Sleep -Seconds $IntervaloSegundos }
}

try {
    $dir = Split-Path -Parent $OutFile
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force -ErrorAction Stop }
    [IO.File]::WriteAllText($OutFile, $cuerpo)
}
catch {
    Write-Host "No se pudo escribir ${OutFile}: $($_.Exception.Message)"
    exit 2
}

if ($sano) {
    Write-Host "Healthcheck correcto; respuesta en $OutFile"
    exit 0
}
Write-Host "El healthcheck no responde sano tras $Intentos intentos"
exit 2
