<#
.SYNOPSIS
    Genera resumen.md del run de la pipeline Release a partir de las evidencias de cada stage.

.DESCRIPTION
    Busca de forma recursiva en -EvidenciasDir (cada stage descarga su artefacto en su propia
    subcarpeta) los ficheros de evidencia y escribe un markdown con: cabecera, stages, copias de
    BD y su restauracion, settings, codigo, pasos del manifiesto, altas, diff de configuracion,
    smoke y el aviso fijo de lo que no se deshace. Cada bloque dice "sin datos" si falta su
    fichero o esta mal formado: el script no falla por eso. Solo sale con 2 si no puede escribir
    -OutFile. Nunca publica valores de app settings: solo nombres y recuentos.

    Un fichero singular (foto-bd.json, restauracion-bd.json, foto-settings.json, foto-codigo.json,
    pasos.json, smoke-resumen.json) usa la primera coincidencia; los que llevan patron
    (config-apply-*/aplicacion.txt, diff-config-*.txt, report.md, ado-run-*.json) usan todas.

.PARAMETER Resultado
    Verde, Rojo o YaAplicada (en este ultimo caso no se toco nada).

.PARAMETER StageResults
    Cadena JSON: array de { nombre, resultado }. Vacia o invalida -> "sin datos". Con -Resultado Rojo,
    las filas cuyo nombre empieza por "VueltaAtras:" dicen si la vuelta atras fue completa o INCOMPLETA
    (cualquier fila que no sea Succeeded, SucceededWithIssues o Skipped la deja incompleta), y de ellas
    salen la reposicion de settings y el redespliegue de cada app: el resumen no lo infiere de ficheros.

.PARAMETER FotoSettingsPath
    Ruta opcional de foto-settings.json. Solo se leen los nombres de las claves y los recuentos, nunca los
    valores. Si no se indica (o no existe) se busca en -EvidenciasDir, de donde la pipeline lo excluye.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/New-ReleaseResumen.ps1 -Environment dev -Version v1.2.0 -Commit abc1234 -BuildId 123 -Resultado Verde -EvidenciasDir ./evidencias -StageResults '[{"nombre":"Fin","resultado":"succeeded"}]' -OutFile ./resumen.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Environment,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$BuildId,
    [Parameter(Mandatory)][ValidateSet('Verde', 'Rojo', 'YaAplicada')][string]$Resultado,
    [Parameter(Mandatory)][string]$EvidenciasDir,
    [string]$StageResults = '',
    [string]$FotoSettingsPath = '',
    [Parameter(Mandatory)][string]$OutFile
)

$ErrorActionPreference = 'Continue'
$sinDatos = 'sin datos'
$sb = [System.Text.StringBuilder]::new()
function Add-Linea { param([string]$Texto = ''); $null = $sb.AppendLine($Texto) }
function Protect-Celda { param($Valor) return ([string]$Valor -replace '\|', '/' -replace '\r?\n', ' ') }

function Find-Evidencia {
    param([string]$Filtro, [switch]$Todas, [string]$PadreLike)
    if (-not (Test-Path -LiteralPath $EvidenciasDir)) { return @() }
    $r = @(Get-ChildItem -LiteralPath $EvidenciasDir -Recurse -File -Filter $Filtro -ErrorAction SilentlyContinue |
            Where-Object { -not $PadreLike -or $_.Directory.Name -like $PadreLike } | Sort-Object FullName)
    if ($Todas) { return $r }
    return @($r | Select-Object -First 1)
}

function Read-Json {
    param([string]$Nombre)
    $f = Find-Evidencia -Filtro $Nombre
    if ($f.Count -eq 0) { return $null }
    try {
        $t = Get-Content -LiteralPath $f[0].FullName -Raw -ErrorAction Stop
        if ([string]::IsNullOrWhiteSpace($t)) { return $null }
        return , (ConvertFrom-Json -InputObject $t -ErrorAction Stop)
    }
    catch { return $null }
}

function Read-Texto {
    param([string]$Ruta)
    try { return @(Get-Content -LiteralPath $Ruta -ErrorAction Stop) } catch { return @() }
}

# --- Cabecera
Add-Linea '# Resumen del run de la release'
Add-Linea
Add-Linea "- Entorno: $Environment"
Add-Linea "- Version: $Version"
Add-Linea "- Commit: $Commit"
Add-Linea "- Build: $BuildId"
Add-Linea "- Resultado: $Resultado"
Add-Linea "- Hora UTC: $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss'))"
$stages = @()
if (-not [string]::IsNullOrWhiteSpace($StageResults)) {
    try { $stages = @(ConvertFrom-Json -InputObject $StageResults -ErrorAction Stop | Where-Object { $null -ne $_ }) } catch { $stages = @() }
}
# Vuelta atras: el resultado de sus jobs (filas "VueltaAtras: ...") decide si fue completa.
$okVuelta = @('succeeded', 'succeededwithissues', 'skipped')
$filasVuelta = @()
if ($Resultado -eq 'Rojo') { $filasVuelta = @($stages | Where-Object { ([string]$_.nombre).StartsWith('VueltaAtras:') }) }
function Get-ResultadoVuelta {
    param([string]$Nombre)
    $f = @($filasVuelta | Where-Object { [string]$_.nombre -ceq $Nombre } | Select-Object -First 1)
    if ($f.Count -eq 0) { return $null }
    return [string]$f[0].resultado
}
if ($filasVuelta.Count -gt 0) {
    $malas = @($filasVuelta | Where-Object { $okVuelta -notcontains ([string]$_.resultado).ToLowerInvariant() })
    if ($malas.Count -eq 0) { Add-Linea '- Vuelta atras: completa' }
    else {
        $detalle = ($malas | ForEach-Object { $r = if ([string]::IsNullOrWhiteSpace([string]$_.resultado)) { 'sin resultado' } else { [string]$_.resultado }; "$(([string]$_.nombre) -replace '^VueltaAtras:\s*', '') = $r" }) -join '; '
        Add-Linea "- Vuelta atras: INCOMPLETA ($detalle)"
    }
}
if ($Resultado -eq 'YaAplicada') {
    Add-Linea
    Add-Linea 'La release ya estaba aplicada en este entorno: no se toco nada en este run.'
}
Add-Linea

# --- Stages
Add-Linea '## Stages'
Add-Linea
if ($stages.Count -eq 0) { Add-Linea $sinDatos }
else {
    Add-Linea '| Stage | Resultado |'
    Add-Linea '|---|---|'
    foreach ($s in $stages) { Add-Linea "| $(Protect-Celda $s.nombre) | $(Protect-Celda $s.resultado) |" }
}
Add-Linea

# --- Copias de BD y restauracion
Add-Linea '## Copias de BD'
Add-Linea
$fotoBd = Read-Json 'foto-bd.json'
if ($null -eq $fotoBd) { Add-Linea $sinDatos }
else {
    Add-Linea '| Tabla | Copia | Filas |'
    Add-Linea '|---|---|---|'
    foreach ($c in @($fotoBd)) { Add-Linea "| $(Protect-Celda $c.tabla) | $(Protect-Celda $c.copia) | $(Protect-Celda $c.filas) |" }
}
Add-Linea
Add-Linea '## Restauracion de BD'
Add-Linea
$restBd = Read-Json 'restauracion-bd.json'
if ($null -eq $restBd) {
    if ($Resultado -eq 'Rojo') { Add-Linea 'Sin evidencia de vuelta atras: no hay restauracion-bd.json.' }
    else { Add-Linea "$sinDatos (no hubo restauracion de BD)" }
}
else {
    Add-Linea 'Estado: Revertido (BD restaurada desde las copias).'
    Add-Linea
    Add-Linea '| Tabla | Actualizadas | Insertadas | Borradas |'
    Add-Linea '|---|---|---|---|'
    foreach ($r in @($restBd)) { Add-Linea "| $(Protect-Celda $r.tabla) | $(Protect-Celda $r.actualizadas) | $(Protect-Celda $r.insertadas) | $(Protect-Celda $r.borradas) |" }
}
Add-Linea

# --- Settings (solo nombres y recuentos; nunca valores)
Add-Linea '## App settings'
Add-Linea
$fotoSet = $null
if ($FotoSettingsPath -and (Test-Path -LiteralPath $FotoSettingsPath -PathType Leaf)) {
    try {
        $t = Get-Content -LiteralPath $FotoSettingsPath -Raw -ErrorAction Stop
        if (-not [string]::IsNullOrWhiteSpace($t)) { $fotoSet = ConvertFrom-Json -InputObject $t -ErrorAction Stop }
    }
    catch { $fotoSet = $null }
}
if ($null -eq $fotoSet) { $fotoSet = Read-Json 'foto-settings.json' }
if ($null -eq $fotoSet) { Add-Linea $sinDatos }
else {
    $props = @($fotoSet.PSObject.Properties)
    $conValor = @($props | Where-Object { $null -ne $_.Value })
    $sinValor = @($props | Where-Object { $null -eq $_.Value })
    Add-Linea "Foto previa de $($props.Count) settings ($($conValor.Count) con valor a reponer, $($sinValor.Count) ausentes antes, que la vuelta atras borraria). Los valores no se publican."
    if ($props.Count -gt 0) { Add-Linea "Nombres: $((@($props | ForEach-Object { $_.Name }) | Sort-Object) -join ', ')" }
    $rSet = Get-ResultadoVuelta 'VueltaAtras: Restaurar BD y settings'
    if ($null -ne $rSet) {
        if ($okVuelta -contains $rSet.ToLowerInvariant()) { Add-Linea "La vuelta atras repuso la foto de settings (job de restauracion: $rSet)." }
        else { Add-Linea "No se confirmo la reposicion de la foto de settings: el job de restauracion de BD y settings acabo en $rSet." }
    }
    elseif ($filasVuelta.Count -gt 0) { Add-Linea 'Reposicion de settings: sin dato del resultado del job de restauracion.' }
}
Add-Linea

# --- Codigo
Add-Linea '## Codigo'
Add-Linea
$fotoCod = Read-Json 'foto-codigo.json'
if ($null -eq $fotoCod) { Add-Linea "$sinDatos (no tocado)" }
else {
    Add-Linea '| App | Zip | Bytes | SHA256 |'
    Add-Linea '|---|---|---|---|'
    foreach ($c in @($fotoCod)) { Add-Linea "| $(Protect-Celda $c.app) | $(Protect-Celda $c.zip) | $(Protect-Celda $c.bytes) | $(Protect-Celda $c.sha256) |" }
    Add-Linea
    $filasRev = @($filasVuelta | Where-Object { ([string]$_.nombre).StartsWith('VueltaAtras: Revertir ') })
    if ($filasRev.Count -gt 0) {
        foreach ($f in $filasRev) {
            $app = ([string]$f.nombre) -replace '^VueltaAtras: Revertir\s+', ''
            $res = [string]$f.resultado
            switch ($res.ToLowerInvariant()) {
                'skipped' { Add-Linea "- ${app}: no se redesplego: su job de Codigo no llego a correr (Skipped)." }
                { $_ -in 'succeeded', 'succeededwithissues' } { Add-Linea "- ${app}: redesplegado desde la foto ($res)." }
                default { Add-Linea "- ${app}: NO se redesplego desde la foto (job de reversion: $(if ($res) { $res } else { 'sin resultado' }))." }
            }
        }
    }
    elseif ($filasVuelta.Count -gt 0) { Add-Linea 'Redespliegue del codigo: sin dato del resultado de los jobs de reversion.' }
    else { Add-Linea 'Codigo no tocado: solo se guardo la foto.' }
}
Add-Linea

# --- Pasos
Add-Linea '## Pasos del manifiesto'
Add-Linea
$pasos = Read-Json 'pasos.json'
if ($null -eq $pasos -or @($pasos).Count -eq 0) { Add-Linea $sinDatos }
else {
    Add-Linea '| Paso | Hash | Resultado |'
    Add-Linea '|---|---|---|'
    foreach ($p in @($pasos)) { Add-Linea "| $(Protect-Celda $p.ruta) | $(Protect-Celda $p.hash) | $(Protect-Celda $p.resultado) |" }
}
Add-Linea

# --- Altas
Add-Linea '## Altas de configuracion'
Add-Linea
$altas = Find-Evidencia -Filtro 'aplicacion.txt' -Todas -PadreLike 'config-apply-*'
if ($altas.Count -eq 0) { Add-Linea $sinDatos; Add-Linea }
else {
    foreach ($f in $altas) {
        $l = @(Read-Texto $f.FullName | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        Add-Linea "### $($f.Directory.Name)"
        Add-Linea
        if ($l.Count -eq 0) { Add-Linea $sinDatos }
        else {
            Add-Linea "- Primera linea: $($l[0])"
            Add-Linea "- Ultima linea: $($l[-1])"
        }
        Add-Linea
    }
}

# --- Diff de configuracion
Add-Linea '## Diff de configuracion'
Add-Linea
$diffs = Find-Evidencia -Filtro 'diff-config-*.txt' -Todas
if ($diffs.Count -eq 0) { Add-Linea $sinDatos; Add-Linea }
else {
    foreach ($f in $diffs) {
        Add-Linea "### $($f.Name)"
        Add-Linea
        Add-Linea '```'
        foreach ($l in (Read-Texto $f.FullName)) { Add-Linea $l }
        Add-Linea '```'
        Add-Linea
    }
}

# --- Smoke
Add-Linea '## Smoke'
Add-Linea
$smk = Read-Json 'smoke-resumen.json'
$informes = Find-Evidencia -Filtro 'report.md' -Todas
$runs = Find-Evidencia -Filtro 'ado-run-*.json' -Todas
if ($null -eq $smk -and $informes.Count -eq 0 -and $runs.Count -eq 0) { Add-Linea $sinDatos; Add-Linea }
else {
    if ($null -ne $smk) {
        Add-Linea "- Primera pasada: $($smk.passPrimeraPasada) PASS de $($smk.passEsperados) esperados ($($smk.intentosPrimeraPasada) intentos)"
        Add-Linea "- Segunda pasada: exit $($smk.exitSegundaPasada)"
        Add-Linea "- Publicado en el Test Plan: $($smk.publicado)"
        Add-Linea
    }
    foreach ($f in $informes) {
        Add-Linea "### Informe $($f.Directory.Name)"
        Add-Linea
        $dentro = $false
        $casos = @()
        foreach ($l in (Read-Texto $f.FullName)) {
            if ($l -match '^##\s+Casos\s*$') { $dentro = $true; continue }
            if ($dentro -and $l -match '^##\s') { break }
            if ($dentro) { $casos += $l }
        }
        $casos = @($casos | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($casos.Count -eq 0) { Add-Linea $sinDatos } else { foreach ($l in $casos) { Add-Linea $l } }
        Add-Linea
    }
    foreach ($f in $runs) {
        try {
            $j = ConvertFrom-Json -InputObject (Get-Content -LiteralPath $f.FullName -Raw -ErrorAction Stop) -ErrorAction Stop
            Add-Linea "- Run del Test Plan: $($j.runId) $($j.runUrl)"
        }
        catch { Add-Linea "- $($f.Name): $sinDatos" }
    }
    Add-Linea
}

# --- Aviso fijo
Add-Linea '## Aviso'
Add-Linea
Add-Linea 'Ojo: los analyzers de CU y las migraciones EF no se deshacen (ADR-007).'

try {
    $dir = Split-Path -Parent $OutFile
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force -ErrorAction Stop }
    [IO.File]::WriteAllText($OutFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
}
catch {
    Write-Host "No se pudo escribir '$OutFile': $($_.Exception.Message)"
    exit 2
}
Write-Host "Resumen escrito en $OutFile"
exit 0
