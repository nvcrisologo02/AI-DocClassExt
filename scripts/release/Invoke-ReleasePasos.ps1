<#
.SYNOPSIS
    Ejecuta los pasos del manifiesto de la release (stage Configuracion), en orden y con ensayo previo.

.DESCRIPTION
    Lee los pasos del entorno con Get-ReleaseManifest.ps1 y, por cada uno:
      1. Calcula el contenido efectivo: ps1 = texto del fichero + una linea con los argumentos
         resueltos; sql sin plantilla = texto del fichero; sql con plantilla embeddings = SQL
         generado por New-EmbeddingsSql con el bloque embeddings del manifiesto, escrito en
         <OutDir>/generado/<nombre>.sql. Los saltos de linea se normalizan a LF antes del hash.
      2. Calcula el hash SHA-256 y, si el registro (salvo -IgnorarRegistro) tiene ese Paso con ese
         hash en Aplicado, lo salta ("SALTADO (ya aplicado en <FechaUtc>)"). Un hash distinto
         vuelve a aplicar el paso.
      3. ps1: lanza el script con -DryRun (o -WhatIf segun admita); si falla, sale con 2 sin
         ejecutar lo real. Despues lo lanza de verdad; si falla, sale con 2.
         sql: apply-sql-dev.ps1 -ScriptPath ... -Server <fqdn> -Database ... -PermitirNoDev.
         Los scripts se lanzan con pwsh -File: los tokens de 'argumentos' llegan como argumentos
         del proceso y nunca se evaluan (sin Invoke-Expression ni scriptblock dinamico).
      4. Registra el paso aplicado con Register-ReleasePaso (Paso = ruta del manifiesto).
    La salida de cada paso va a <OutDir>/paso-<n>-<nombre>.txt (n de 1, en orden del manifiesto) y
    el resumen a <OutDir>/pasos.json (ruta, hash, resultado Aplicado|Saltado). Un paso que falla
    no se registra. Codigo de salida 0 si todo va bien y 2 ante cualquier fallo.

.PARAMETER Environment
    Entorno: dev, pre o prod.

.PARAMETER RepoRoot
    Raiz del repo. Por defecto, la del script.

.PARAMETER SqlServer
    Servidor SQL de la release (nombre corto o fqdn); lleva el registro ReleaseAplicada.

.PARAMETER SqlDatabase
    Base de datos de la release.

.PARAMETER BuildId
    Identificador de la ejecucion de la pipeline, para el registro.

.PARAMETER OutDir
    Carpeta de salida (se crea si no existe).

.PARAMETER IgnorarRegistro
    No consulta el registro: todos los pasos se aplican (release de ensayo).

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Invoke-ReleasePasos.ps1 -Environment pre -SqlServer srbsqlpredocai -SqlDatabase DocumentIA -BuildId 1234 -OutDir ./evidencias
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
    [string]$RepoRoot,
    [Parameter(Mandatory)][string]$SqlServer,
    [Parameter(Mandatory)][string]$SqlDatabase,
    [Parameter(Mandatory)][string]$BuildId,
    [Parameter(Mandatory)][string]$OutDir,
    [switch]$IgnorarRegistro
)

$ErrorActionPreference = 'Continue'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }
if (-not (Get-Module ReleaseRegistro)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseRegistro.psm1') }
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path }

$resumen = [System.Collections.Generic.List[object]]::new()

function Stop-Pasos {
    param([string]$Mensaje)
    Write-Host $Mensaje
    # Resumen parcial, en la medida de lo posible; el exit 2 no depende de que se escriba.
    try { ConvertTo-Json -InputObject @($resumen) -Depth 4 | Set-Content -LiteralPath (Join-Path $OutDir 'pasos.json') -Encoding utf8 -ErrorAction Stop } catch { Write-Verbose $_.Exception.Message }
    exit 2
}

function Get-TextoNormalizado([string]$Ruta) {
    return ([System.IO.File]::ReadAllText($Ruta) -replace "`r`n", "`n")
}

try {
    New-Item -ItemType Directory -Force -Path $OutDir -ErrorAction Stop | Out-Null
}
catch {
    Write-Host "No se pudo crear OutDir '$OutDir': $($_.Exception.Message)"
    exit 2
}

# Manifiesto del entorno (ya validado y filtrado por entorno).
try {
    $json = & (Join-Path $PSScriptRoot 'Get-ReleaseManifest.ps1') -Environment $Environment -RepoRoot $RepoRoot -AsJson | Out-String
    $manifiesto = $json | ConvertFrom-Json
}
catch { Stop-Pasos "No se pudo leer el manifiesto: $($_.Exception.Message)" }

$version = [string]$manifiesto.version
$pasos = @($manifiesto.pasos | Where-Object { $null -ne $_ -and @($_.entornos) -contains $Environment })
$servidorFqdn = if ($SqlServer -match '\.') { $SqlServer } else { "$SqlServer.database.windows.net" }

try {
    $estado = Get-ReleaseEstado -Server $SqlServer -Database $SqlDatabase -Version $version -IgnorarRegistro:$IgnorarRegistro
}
catch { Stop-Pasos "No se pudo leer el registro ReleaseAplicada: $($_.Exception.Message)" }
$aplicados = @($estado.PasosAplicados | Where-Object { $null -ne $_ })

function Invoke-ProcesoPwsh {
    # Lanza un .ps1 como proceso hijo; devuelve @{ Codigo; Salida }. Los argumentos van como tokens, sin evaluar.
    param([string]$Ruta, [string[]]$Argumentos)
    $global:LASTEXITCODE = 0
    $salida = & pwsh -NoProfile -NonInteractive -File $Ruta @Argumentos 2>&1 | Out-String
    return @{ Codigo = $LASTEXITCODE; Salida = $salida }
}

$n = 0
foreach ($p in $pasos) {
    $n++
    $ruta = [string]$p.ruta
    $rutaAbs = Join-Path $RepoRoot $ruta
    $nombre = [System.IO.Path]::GetFileNameWithoutExtension($ruta)
    $fichSalida = Join-Path $OutDir "paso-$n-$nombre.txt"
    Write-Host "Paso ${n}: [$($p.tipo)] $ruta"

    # 1. Contenido efectivo.
    $argumentos = @()
    $rutaSql = $rutaAbs
    try {
        if ($p.tipo -eq 'ps1') {
            $argumentos = @(ConvertTo-ReleaseArgumentList -Argumentos $p.argumentos -Environment $Environment)
            $contenido = (Get-TextoNormalizado $rutaAbs) + "`nARGS: " + ($argumentos -join ' ')
        }
        elseif ($p.plantilla -eq 'embeddings') {
            if ($null -eq $manifiesto.embeddings) { throw 'El paso usa la plantilla embeddings pero el manifiesto no tiene bloque embeddings.' }
            $e = $manifiesto.embeddings
            $contenido = New-EmbeddingsSql -TemplatePath $rutaAbs -Version $e.version -Modo $e.modo -RestringidoModo $e.restringidoModo
            $contenido = $contenido -replace "`r`n", "`n"
            $dirGenerado = Join-Path $OutDir 'generado'
            New-Item -ItemType Directory -Force -Path $dirGenerado -ErrorAction Stop | Out-Null
            $rutaSql = Join-Path $dirGenerado "$nombre.sql"
            [System.IO.File]::WriteAllText($rutaSql, $contenido, [System.Text.UTF8Encoding]::new($false))
        }
        else {
            $contenido = Get-TextoNormalizado $rutaAbs
        }
        $hash = Get-ReleaseContentHash -Content $contenido
    }
    catch { Stop-Pasos "Paso ${n} ($ruta): no se pudo preparar el contenido: $($_.Exception.Message)" }

    # 2. Salto si ya esta aplicado con el mismo hash.
    $previo = $aplicados | Where-Object { $_.Paso -ceq $ruta -and $_.HashContenido -ceq $hash } | Select-Object -First 1
    if ($previo) {
        $msg = "SALTADO (ya aplicado en $($previo.FechaUtc))"
        Write-Host $msg
        try { Set-Content -LiteralPath $fichSalida -Value $msg -Encoding utf8 -ErrorAction Stop }
        catch { Stop-Pasos "No se pudo escribir '$fichSalida': $($_.Exception.Message)" }
        $resumen.Add([pscustomobject]@{ ruta = $ruta; hash = $hash; resultado = 'Saltado' })
        continue
    }

    # 3. Ejecucion.
    $salidaPaso = [System.Text.StringBuilder]::new()
    if ($p.tipo -eq 'ps1') {
        try { $conmutador = Get-DryRunSwitchName -ScriptPath $rutaAbs }
        catch { Stop-Pasos "Paso ${n} ($ruta): $($_.Exception.Message)" }

        $ensayo = Invoke-ProcesoPwsh -Ruta $rutaAbs -Argumentos ($argumentos + "-$conmutador")
        [void]$salidaPaso.AppendLine("== Ensayo (-$conmutador), codigo $($ensayo.Codigo) ==").AppendLine($ensayo.Salida)
        if ($ensayo.Codigo -ne 0) {
            try { Set-Content -LiteralPath $fichSalida -Value $salidaPaso.ToString() -Encoding utf8 -ErrorAction Stop } catch { Write-Host "No se pudo escribir '$fichSalida'." }
            Stop-Pasos "Paso ${n} ($ruta): el ensayo (-$conmutador) fallo con codigo $($ensayo.Codigo); no se ejecuta lo real."
        }

        $real = Invoke-ProcesoPwsh -Ruta $rutaAbs -Argumentos $argumentos
        [void]$salidaPaso.AppendLine("== Real, codigo $($real.Codigo) ==").AppendLine($real.Salida)
        if ($real.Codigo -ne 0) {
            try { Set-Content -LiteralPath $fichSalida -Value $salidaPaso.ToString() -Encoding utf8 -ErrorAction Stop } catch { Write-Host "No se pudo escribir '$fichSalida'." }
            Stop-Pasos "Paso ${n} ($ruta): la ejecucion real fallo con codigo $($real.Codigo)."
        }
    }
    else {
        $aplicar = Join-Path $RepoRoot 'scripts/database/apply-sql-dev.ps1'
        $res = Invoke-ProcesoPwsh -Ruta $aplicar -Argumentos @('-ScriptPath', $rutaSql, '-Server', $servidorFqdn, '-Database', $SqlDatabase, '-PermitirNoDev')
        [void]$salidaPaso.AppendLine("== apply-sql-dev, codigo $($res.Codigo) ==").AppendLine($res.Salida)
        if ($res.Codigo -ne 0) {
            try { Set-Content -LiteralPath $fichSalida -Value $salidaPaso.ToString() -Encoding utf8 -ErrorAction Stop } catch { Write-Host "No se pudo escribir '$fichSalida'." }
            Stop-Pasos "Paso ${n} ($ruta): apply-sql-dev fallo con codigo $($res.Codigo)."
        }
    }

    try { Set-Content -LiteralPath $fichSalida -Value $salidaPaso.ToString() -Encoding utf8 -ErrorAction Stop }
    catch { Stop-Pasos "No se pudo escribir '$fichSalida': $($_.Exception.Message)" }

    # 4. Registro.
    try {
        Register-ReleasePaso -Server $SqlServer -Database $SqlDatabase -Version $version -Paso $ruta -HashContenido $hash -BuildId $BuildId
    }
    catch { Stop-Pasos "Paso ${n} ($ruta): aplicado pero no se pudo registrar: $($_.Exception.Message)" }
    $resumen.Add([pscustomobject]@{ ruta = $ruta; hash = $hash; resultado = 'Aplicado' })
    Write-Host "Paso ${n} aplicado y registrado."
}

try { ConvertTo-Json -InputObject @($resumen) -Depth 4 | Set-Content -LiteralPath (Join-Path $OutDir 'pasos.json') -Encoding utf8 -ErrorAction Stop }
catch {
    Write-Host "No se pudo escribir pasos.json: $($_.Exception.Message)"
    exit 2
}
Write-Host "Pasos completados: $($resumen.Count)"
exit 0
