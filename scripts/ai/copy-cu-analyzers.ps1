#Requires -Version 7.0
<#
.SYNOPSIS
    Copia analyzers de Azure AI Content Understanding desde el recurso Foundry
    de PRO al de un entorno (DEV o PRE) con la Copy API oficial, sin
    reentrenar: el destino recibe el analyzer entrenado tal cual esta en PRO.

.DESCRIPTION
    Sustituye a la reconstruccion desde dataset (build-analyzers.ps1) como
    mecanismo de promocion: la validacion de la Tarea 13 (AB#100315) demostro
    que el dataset actual de los labelingProjects de PRO ya no es el que
    entreno los analyzers de PRO, asi que reconstruir no reproduce PRO.
    build-analyzers.ps1 queda para reentrenar en el futuro.

    Para cada analyzer de infra/ai/cu-analyzers.json con "promote": true (o
    los indicados en -Only):

      1. Resuelve origen (cu_primary de resources.prod.json) y destino
         (cu_primary de resources.<env>.json, o cu_secondary con -Target).
         Ambos necesitan subscriptionId y location en el fichero de recursos:
         la Copy API trabaja con ids ARM y regiones, y admite distinta
         suscripcion y distinta region.
      2. GET del analyzer en origen (debe estar "ready") y en destino. Si ya
         existe en destino con la misma definicion (comparacion canonica de
         fieldSchema, config, models, baseAnalyzerId, description y tags) se
         salta; si existe y difiere, solo se sobrescribe con -Force
         (allowReplace=true); si no existe, se crea.
      3. POST {origen}/analyzers/{id}:grantCopyAuthorization con
         { targetAzureResourceId, targetRegion } (autorizacion con caducidad,
         sin token portable en api-version 2025-11-01).
      4. POST {destino}/analyzers/{id<sufijo>}:copy con
         { sourceAzureResourceId, sourceAnalyzerId, sourceRegion } y sondeo de
         Operation-Location hasta succeeded/ready.
      5. GET de verificacion en destino y comparacion canonica con el origen.

    Escribe infra/ai/cu-analyzers.<env>.manifest.json (fusionado por id) con
    el resultado por analyzer: copied / present / skipped / failed, fecha,
    operacion y si la definicion coincide con el origen.

    Requisito de permisos: la identidad que ejecuta el script necesita el rol
    Cognitive Services User en ORIGEN y en DESTINO (doc oficial "Copy custom
    analyzers"). No se necesita ningun rol cruzado entre los recursos.

    Todas las llamadas al data-plane van con token fresco e Invoke-WebRequest
    en UTF-8, nunca con "az rest" (corrompe comillas del cuerpo y tildes de la
    salida en Windows).

.PARAMETER Environment
    Entorno destino: dev o pre.
.PARAMETER Only
    Ids de analyzer a copiar (por defecto todos los "promote": true de
    infra/ai/cu-analyzers.json). Admite "-Only A,B" desde pwsh -File.
.PARAMETER Target
    Alias del recurso destino en resources.<env>.json (cu_primary por defecto).
.PARAMETER SourceTarget
    Alias del recurso origen en resources.prod.json (cu_primary por defecto).
.PARAMETER TargetSuffix
    Sufijo para el id en destino (por ejemplo "_copytest" en una prueba). Vacio
    por defecto: mismo id que en origen, que es lo que referencia ModeloConfigs.
.PARAMETER Force
    Sobrescribe en destino (allowReplace=true) aunque exista con otra definicion.
.PARAMETER DryRun
    Solo GET de origen y destino y el plan de lo que haria; no autoriza ni copia.
    Alias funcional: -WhatIf.
.PARAMETER WhatIf
    Alias de -DryRun.
.PARAMETER DumpDir
    Carpeta donde volcar el GET de origen y destino y las respuestas de
    grant/copy por analyzer. No se escribe nada si se omite.
.PARAMETER TimeoutMinutes
    Tiempo maximo de sondeo por copia. Por defecto 20.
.PARAMETER PollSeconds
    Intervalo de sondeo. Por defecto 5.
.EXAMPLE
    pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment dev -Only CERA46 -TargetSuffix _copytest -DryRun
.EXAMPLE
    pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment dev -Only CERA46 -TargetSuffix _copytest -DumpDir docs/auxiliares/temps/2026-09-21/copy-cu-dev
.EXAMPLE
    pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment dev -Force
#>
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre')][string]$Environment,
    [string[]]$Only,
    [ValidateSet('cu_primary', 'cu_secondary')][string]$Target = 'cu_primary',
    [ValidateSet('cu_primary', 'cu_secondary')][string]$SourceTarget = 'cu_primary',
    [string]$TargetSuffix = '',
    [switch]$Force,
    [switch]$DryRun,
    [switch]$WhatIf,
    [string]$DumpDir,
    [int]$TimeoutMinutes = 20,
    [int]$PollSeconds = 5,
    [string]$ApiVersion = "2025-11-01"
)
$ErrorActionPreference = "Stop"

# El data-plane en este entorno pasa por un proxy TLS; sin esto tanto az CLI
# como Invoke-WebRequest fallan la verificacion del certificado.
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"

$isDryRun = [bool]($DryRun -or $WhatIf)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$listFile = Join-Path $repoRoot 'infra/ai/cu-analyzers.json'
$sourceResourcesFile = Join-Path $repoRoot 'infra/ai/resources.prod.json'
$targetResourcesFile = Join-Path $repoRoot "infra/ai/resources.$Environment.json"
$manifestFile = Join-Path $repoRoot "infra/ai/cu-analyzers.$Environment.manifest.json"
$runningStates = @('running', 'notstarted', 'creating', 'inprogress')
$successStates = @('succeeded', 'ready')
# Claves de la definicion que se comparan entre origen y destino.
$definitionKeys = @('baseAnalyzerId', 'description', 'tags', 'config', 'fieldSchema', 'models', 'processingLocation', 'dynamicFieldSchema')

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
function Get-CuToken {
    $raw = az account get-access-token --resource https://cognitiveservices.azure.com --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $raw) {
        throw "no se pudo obtener el token de acceso (az account get-access-token, exit $LASTEXITCODE). Ejecuta 'az login'."
    }
    return $raw.Trim()
}

function Invoke-Cu {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Token,
        [object]$Body
    )
    $headers = @{ Authorization = "Bearer $Token" }
    $params = @{ Uri = $Url; Method = $Method; Headers = $headers; UseBasicParsing = $true; SkipCertificateCheck = $true }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 40
        $params['Body'] = [Text.Encoding]::UTF8.GetBytes($json)
        $params['ContentType'] = 'application/json; charset=utf-8'
    }
    try {
        $resp = Invoke-WebRequest @params
        $text = ''
        if ($resp.RawContentStream) { $text = [Text.Encoding]::UTF8.GetString($resp.RawContentStream.ToArray()) }
        $content = if ($text.Trim()) { $text | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Content = $content; Headers = $resp.Headers; Error = $null }
    } catch {
        $status = 0
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        $err = ''
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $err = $_.ErrorDetails.Message } elseif ($_.Exception.Message) { $err = $_.Exception.Message }
        $content = $null
        try { if ($err.Trim().StartsWith('{')) { $content = $err | ConvertFrom-Json } } catch { }
        return [pscustomobject]@{ Status = $status; Content = $content; Headers = $null; Error = $err }
    }
}

function Get-SingleHeaderValue {
    param($HeaderValue)
    if ($null -eq $HeaderValue) { return $null }
    return ($HeaderValue | Select-Object -First 1)
}

function ConvertTo-Canonical {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value }
    if ($Value -is [System.Collections.IDictionary]) {
        $o = [ordered]@{}
        foreach ($k in ($Value.Keys | Sort-Object)) { $o[$k] = ConvertTo-Canonical $Value[$k] }
        return $o
    }
    if ($Value -is [pscustomobject]) {
        $o = [ordered]@{}
        foreach ($p in ($Value.PSObject.Properties | Sort-Object Name)) { $o[$p.Name] = ConvertTo-Canonical $p.Value }
        return $o
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        $list = [System.Collections.ArrayList]::new()
        foreach ($i in $Value) { [void]$list.Add((ConvertTo-Canonical $i)) }
        return , $list.ToArray()
    }
    return $Value
}

function ConvertTo-CanonicalJson {
    param($Value)
    if ($null -eq $Value) { return 'null' }
    return (ConvertTo-Json -InputObject (ConvertTo-Canonical $Value) -Depth 40 -Compress)
}

function Get-DefinitionDiff {
    # Claves de $definitionKeys cuyo valor canonico difiere entre dos analyzers.
    param($A, $B)
    $diff = @()
    foreach ($k in $definitionKeys) {
        $va = if ($A.PSObject.Properties.Name -contains $k) { $A.$k } else { $null }
        $vb = if ($B.PSObject.Properties.Name -contains $k) { $B.$k } else { $null }
        if ((ConvertTo-CanonicalJson $va) -ne (ConvertTo-CanonicalJson $vb)) { $diff += $k }
    }
    return $diff
}

function Write-JsonFile {
    param([string]$Path, [object]$Object, [int]$Depth = 40)
    $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $json = $Object | ConvertTo-Json -Depth $Depth
    $json = $json -replace "`r`n", "`n"
    [IO.File]::WriteAllText($Path, ($json + "`n"), [Text.UTF8Encoding]::new($false))
}

function Resolve-Resource {
    param([string]$File, [string]$Alias, [string]$Label)
    if (-not (Test-Path $File)) { throw "no existe $File" }
    $r = (Get-Content -Raw -Path $File -Encoding UTF8 | ConvertFrom-Json).resources.$Alias
    if (-not $r) { throw "$(Split-Path -Leaf $File) no define el alias '$Alias'" }
    foreach ($k in 'account', 'resourceGroup', 'endpoint', 'subscriptionId', 'location') {
        if (-not $r.$k) { throw "$(Split-Path -Leaf $File) -> $Alias no informa '$k' (la Copy API necesita subscriptionId y location ademas de account/resourceGroup/endpoint)" }
    }
    return [pscustomobject]@{
        Label      = $Label
        Alias      = $Alias
        Account    = $r.account
        Endpoint   = $r.endpoint.TrimEnd('/')
        Region     = $r.location
        ResourceId = "/subscriptions/$($r.subscriptionId)/resourceGroups/$($r.resourceGroup)/providers/Microsoft.CognitiveServices/accounts/$($r.account)"
    }
}

function Wait-Operation {
    param([string]$OperationUrl, [string]$Token, [string]$Label)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $last = ''
    do {
        Start-Sleep -Seconds $PollSeconds
        $op = Invoke-Cu -Method Get -Url $OperationUrl -Token $Token
        if ($op.Status -ne 200) { throw "sondeo de $Label -> HTTP $($op.Status): $($op.Error)" }
        $status = if ($op.Content.status) { [string]$op.Content.status } elseif ($op.Content.result.status) { [string]$op.Content.result.status } else { '' }
        if ($status -ne $last) { Write-Host "      $(Get-Date -Format HH:mm:ss) status = $status"; $last = $status }
        if ((Get-Date) -gt $deadline) { throw "la copia de $Label supera $TimeoutMinutes minutos (ultimo estado: '$status'); sigue en $OperationUrl" }
    } while ($status.ToLowerInvariant() -in $runningStates)
    if ($status.ToLowerInvariant() -notin $successStates) {
        throw "la copia de $Label termino en '$status'. Detalle: $($op.Content | ConvertTo-Json -Depth 10 -Compress)"
    }
    return $op.Content
}

# -----------------------------------------------------------------------------
# Carga
# -----------------------------------------------------------------------------
$source = Resolve-Resource -File $sourceResourcesFile -Alias $SourceTarget -Label 'origen'
$dest = Resolve-Resource -File $targetResourcesFile -Alias $Target -Label 'destino'
if ($source.ResourceId -eq $dest.ResourceId) { throw "origen y destino son el mismo recurso ($($source.ResourceId))" }

if (-not (Test-Path $listFile)) { throw "no existe $listFile" }
$list = Get-Content -Raw -Path $listFile -Encoding UTF8 | ConvertFrom-Json
$ids = @($list.analyzers | Where-Object { $_.promote } | ForEach-Object { $_.analyzerId })
# Con "pwsh -File", -Only A,B llega como una sola cadena "A,B": se admiten las dos formas.
if ($Only) { $Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
if ($Only) {
    $known = @($list.analyzers | ForEach-Object { $_.analyzerId })
    $unknown = @($Only | Where-Object { $_ -notin $known })
    if ($unknown.Count -gt 0) { throw "infra/ai/cu-analyzers.json no declara: $($unknown -join ', ')" }
    $ids = @($Only)
}
if ($ids.Count -eq 0) { throw "no hay analyzers que copiar" }

$modeLabel = if ($isDryRun) { 'DRY-RUN (solo GET; sin autorizar ni copiar)' } else { 'REAL (grantCopyAuthorization en origen + :copy en destino)' }
Write-Host "== copy-cu-analyzers: $Environment  [$modeLabel] ==" -ForegroundColor Cyan
Write-Host "  origen     : $($source.Alias)=$($source.Account) ($($source.Region)) $($source.ResourceId)"
Write-Host "  destino    : $($dest.Alias)=$($dest.Account) ($($dest.Region)) $($dest.ResourceId)"
Write-Host "  analyzers  : $($ids -join ', ')"
if ($TargetSuffix) { Write-Host "  sufijo     : en destino se crea <id>$TargetSuffix" -ForegroundColor DarkYellow }
if ($DumpDir) { New-Item -ItemType Directory -Force -Path $DumpDir | Out-Null; Write-Host "  volcado    : $DumpDir" }
Write-Host ""

$token = Get-CuToken
$tokenIssuedAt = Get-Date
$results = [System.Collections.Generic.List[object]]::new()
$entries = @{}

foreach ($id in $ids) {
    $dstId = "$id$TargetSuffix"
    $started = Get-Date
    if (((Get-Date) - $tokenIssuedAt).TotalMinutes -gt 40) { $token = Get-CuToken; $tokenIssuedAt = Get-Date }
    Write-Host "  $id -> $dstId" -ForegroundColor White

    $srcUrl = "$($source.Endpoint)/contentunderstanding/analyzers/$id`?api-version=$ApiVersion"
    $dstUrl = "$($dest.Endpoint)/contentunderstanding/analyzers/$dstId`?api-version=$ApiVersion"
    $s = Invoke-Cu -Method Get -Url $srcUrl -Token $token
    if ($s.Status -in 401, 403) { throw "sin acceso al data plane de $($source.Account) (HTTP $($s.Status)): falta el rol Cognitive Services User" }
    if ($s.Status -eq 404) { throw "$id no existe en origen $($source.Account)" }
    if ($s.Status -ne 200) { throw "GET $id en origen -> HTTP $($s.Status): $($s.Error)" }
    if ($s.Content.status -ne 'ready') { throw "$id en origen esta en status '$($s.Content.status)', no 'ready'; no se copia" }
    if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$id.origen.json") -Object $s.Content }

    $d = Invoke-Cu -Method Get -Url $dstUrl -Token $token
    if ($d.Status -in 401, 403) { throw "sin acceso al data plane de $($dest.Account) (HTTP $($d.Status)): falta el rol Cognitive Services User" }
    if ($d.Status -notin 200, 404) { throw "GET $dstId en destino -> HTTP $($d.Status): $($d.Error)" }
    $exists = ($d.Status -eq 200)
    $action = 'create'
    $diff = @()
    if ($exists) {
        $diff = Get-DefinitionDiff -A $s.Content -B $d.Content
        if ($diff.Count -eq 0 -and $d.Content.status -eq 'ready' -and -not $Force) { $action = 'skip' }
        elseif ($Force) { $action = 'replace' }
        else { $action = 'conflict' }
    }
    $state = if ($exists) { "existe en destino (status $($d.Content.status))" } else { 'no existe en destino' }
    $why = if ($diff.Count -gt 0) { " difiere en: $($diff -join ', ')" } elseif ($exists -and $Force) { ' (-Force)' } else { '' }
    Write-Host "      $state -> $action$why" -ForegroundColor $(switch ($action) { 'skip' { 'Green' } 'conflict' { 'Red' } default { 'Yellow' } })

    $entry = [ordered]@{
        analyzerId      = $id
        targetAnalyzerId = $dstId
        sourceAccount   = $source.Account
        targetAccount   = $dest.Account
        status          = $action
        sourceCreatedAt = $s.Content.createdAt
        definitionMatches = $null
        copiedAtUtc     = $null
        operation       = $null
    }

    if ($action -eq 'skip') {
        $entry.status = 'present'; $entry.definitionMatches = $true
        $entries[$dstId] = $entry
        $results.Add([pscustomobject]@{ Analyzer = $id; Destino = $dstId; Action = 'skip'; Status = $d.Content.status; Seconds = 0 })
        continue
    }
    if ($action -eq 'conflict') {
        $entry.definitionMatches = $false
        $entries[$dstId] = $entry
        $results.Add([pscustomobject]@{ Analyzer = $id; Destino = $dstId; Action = 'conflict'; Status = $d.Content.status; Seconds = 0 })
        continue
    }
    if ($isDryRun) {
        $results.Add([pscustomobject]@{ Analyzer = $id; Destino = $dstId; Action = "dry-run ($action)"; Status = $(if ($exists) { $d.Content.status } else { '-' }); Seconds = 0 })
        continue
    }

    # 3) autorizacion en origen
    $grantUrl = "$($source.Endpoint)/contentunderstanding/analyzers/$id`:grantCopyAuthorization?api-version=$ApiVersion"
    $grant = Invoke-Cu -Method Post -Url $grantUrl -Token $token -Body @{ targetAzureResourceId = $dest.ResourceId; targetRegion = $dest.Region }
    if ($grant.Status -notin 200, 201) { throw "grantCopyAuthorization de $id en origen -> HTTP $($grant.Status): $($grant.Error)" }
    Write-Host "      autorizacion concedida (expira $($grant.Content.expiresAt))" -ForegroundColor DarkGray
    if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$id.grant.json") -Object $grant.Content }

    # 4) copia en destino
    $copyUrl = "$($dest.Endpoint)/contentunderstanding/analyzers/$dstId`:copy?api-version=$ApiVersion"
    if ($exists) { $copyUrl += '&allowReplace=true' }
    $copy = Invoke-Cu -Method Post -Url $copyUrl -Token $token -Body @{ sourceAzureResourceId = $source.ResourceId; sourceAnalyzerId = $id; sourceRegion = $source.Region }
    if ($copy.Status -notin 200, 201, 202) { throw "copy de $id -> $dstId en destino -> HTTP $($copy.Status): $($copy.Error)" }
    if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$id.copy.json") -Object $copy.Content }
    $opUrl = Get-SingleHeaderValue $copy.Headers['Operation-Location']
    if ($opUrl) {
        Write-Host "      copia aceptada (HTTP $($copy.Status)); sondeando $opUrl" -ForegroundColor DarkGray
        $op = Wait-Operation -OperationUrl $opUrl -Token $token -Label "$id -> $dstId"
        $entry.operation = $opUrl
    } else {
        Write-Host "      HTTP $($copy.Status) sin Operation-Location; se asume sincrono" -ForegroundColor DarkGray
    }

    # 5) verificacion
    $v = Invoke-Cu -Method Get -Url $dstUrl -Token $token
    if ($v.Status -ne 200) { throw "verificacion de $dstId en destino -> HTTP $($v.Status): $($v.Error)" }
    if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$id.destino.json") -Object $v.Content }
    if ($v.Content.status -ne 'ready') { throw "$dstId en destino quedo en status '$($v.Content.status)', no 'ready'" }
    $vdiff = Get-DefinitionDiff -A $s.Content -B $v.Content
    $secs = [int]((Get-Date) - $started).TotalSeconds
    if ($vdiff.Count -eq 0) {
        Write-Host "      $dstId : ready en $secs s, definicion identica al origen" -ForegroundColor Green
    } else {
        Write-Host "      $dstId : ready en $secs s, pero difiere del origen en: $($vdiff -join ', ')" -ForegroundColor Yellow
    }
    if ($v.Content.warnings) { Write-Host "      warnings: $($v.Content.warnings | ConvertTo-Json -Depth 5 -Compress)" -ForegroundColor DarkYellow }
    $entry.status = 'copied'; $entry.definitionMatches = ($vdiff.Count -eq 0); $entry.copiedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    $entries[$dstId] = $entry
    $results.Add([pscustomobject]@{ Analyzer = $id; Destino = $dstId; Action = $action; Status = $v.Content.status; Seconds = $secs })
}

Write-Host ""
Write-Host "== resumen ==" -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host

if (-not $isDryRun -and $entries.Count -gt 0) {
    $manifest = [ordered]@{ environment = $Environment; sourceAccount = $source.Account; targetAccount = $dest.Account; apiVersion = $ApiVersion; updatedAtUtc = (Get-Date).ToUniversalTime().ToString('o'); analyzers = @() }
    $existing = @{}
    if (Test-Path $manifestFile) {
        $prev = Get-Content -Raw -Path $manifestFile -Encoding UTF8 | ConvertFrom-Json
        foreach ($e in @($prev.analyzers)) { $existing[$e.targetAnalyzerId] = $e }
    }
    foreach ($k in $entries.Keys) { $existing[$k] = [pscustomobject]$entries[$k] }
    $manifest.analyzers = @($existing.Keys | Sort-Object | ForEach-Object { $existing[$_] })
    Write-JsonFile -Path $manifestFile -Object $manifest
    Write-Host "manifiesto: $manifestFile" -ForegroundColor DarkGray
}

$conflicts = @($results | Where-Object { $_.Action -eq 'conflict' })
if ($conflicts.Count -gt 0) { throw "$($conflicts.Count) analyzer(s) existen en destino con otra definicion: $(($conflicts | ForEach-Object { $_.Destino }) -join ', '). Usa -Force para sobrescribir." }
