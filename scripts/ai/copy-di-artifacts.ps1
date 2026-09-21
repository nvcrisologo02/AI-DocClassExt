#Requires -Version 7.0
<#
.SYNOPSIS
    Copia los clasificadores y modelos personalizados de Azure Document
    Intelligence declarados en infra/ai/di-artifacts.json (promote: true)
    desde el recurso origen de PRO al recurso DI de un entorno (DEV o PRE)
    con la Copy API oficial, y deja un manifiesto por entorno.

.DESCRIPTION
    Es el paso 4 del flujo de promocion de infra/ai/README.md (Tarea 14 del
    plan "IA propia por entorno", AB#100316). Para cada artefacto con
    promote: true:

      1. Resuelve origen y destino en infra/ai/resources.prod.json y
         infra/ai/resources.<env>.json (alias "di"). Exige que la cuenta
         origen coincida con sourceAccount de di-artifacts.json y que el
         destino sea una cuenta distinta.
      2. GET del artefacto en origen (debe existir) y en destino.
      3. Si ya existe en destino con la misma firma (docTypes y, en modelos,
         nombres de campo por docType) se salta. Si existe con otra firma se
         marca como conflicto y no se toca, salvo -Force, que lo borra en
         destino y lo vuelve a copiar. Los ids no se renombran: el
         clasificador se llama igual en todos los entornos porque es lo que
         referencia ModeloConfigs.
      4. POST {destino}/documentintelligence/<kind>:authorizeCopy con el id y
         una descripcion de procedencia (cuenta origen, fecha de creacion
         en origen y fecha de copia). El destino devuelve la autorizacion
         (targetResourceId, region, accessToken con caducidad). Verificado
         en DEV: la copia conserva la description del origen, no la de la
         autorizacion; la procedencia queda en el manifiesto.
      5. POST {origen}/documentintelligence/<kind>/<id>:copyTo con esa
         autorizacion como cuerpo. El origen responde 202 con
         Operation-Location; se sondea hasta succeeded (o failed/canceled o
         -TimeoutMinutes). Es una lectura sobre el origen: PRO no cambia.
      6. GET de verificacion en destino y comparacion de firma con el origen.
      7. Manifiesto infra/ai/di-artifacts.<env>.manifest.json: una entrada por
         artefacto (copied / present / not-promoted / conflict) con fechas,
         id de operacion y docTypes de origen y destino. Se fusiona con el
         manifiesto anterior por (kind, id).

    Los artefactos con promote: false se listan y se anotan en el manifiesto
    como not-promoted, sin tocar nada.

    La copia de clasificadores v4.0 (2024-11-30) solo esta soportada entre
    recursos de East US, West US 2 y West Europe (las tres cuentas de DI del
    proyecto estan en West Europe). El artefacto origen debe haberse
    entrenado con la api-version 2024-11-30; los de 2023-07-31 no se pueden
    copiar y hay que reentrenarlos.

    Todas las llamadas al data-plane van con un token fresco de
    "az account get-access-token" e Invoke-WebRequest decodificando UTF-8,
    nunca con "az rest" (pierde comillas del cuerpo y corta las URLs en "&"
    al invocar az.cmd). El accessToken de la autorizacion no se imprime ni
    se vuelca en -DumpDir.

.PARAMETER Environment
    Entorno destino: dev o pre. Determina resources.<env>.json y el manifiesto.
.PARAMETER Only
    Ids de artefacto a procesar (por defecto todos los que tengan promote: true).
.PARAMETER Kind
    Tipos a procesar: classifiers y/o models. Por defecto ambos.
.PARAMETER Force
    Si el artefacto existe en destino con otra firma, lo borra en destino
    (DELETE) y lo vuelve a copiar. Nunca toca el origen.
.PARAMETER DryRun
    No escribe nada en Azure ni en infra/ai: solo lee, resuelve y muestra que
    haria. Con -DumpDir escribe alli el manifiesto que resultaria.
    Alias funcional: -WhatIf.
.PARAMETER WhatIf
    Alias de -DryRun.
.PARAMETER DumpDir
    Carpeta donde volcar las respuestas de origen y destino (<kind>.<id>.*.json)
    y, en dry-run, el manifiesto. No se escribe nada si se omite.
.PARAMETER TimeoutMinutes
    Tiempo maximo de sondeo por copia. Por defecto 30.
.PARAMETER PollSeconds
    Intervalo de sondeo. Por defecto 5.
.EXAMPLE
    pwsh scripts/ai/copy-di-artifacts.ps1 -Environment dev -DryRun -DumpDir docs/auxiliares/temps/2026-09-21/copy-di-dev
.EXAMPLE
    pwsh scripts/ai/copy-di-artifacts.ps1 -Environment dev -Only DocumentAICC_v1
.EXAMPLE
    pwsh scripts/ai/copy-di-artifacts.ps1 -Environment pre
#>
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre')][string]$Environment,
    [string[]]$Only,
    [ValidateSet('classifiers', 'models')][string[]]$Kind = @('classifiers', 'models'),
    [switch]$Force,
    [switch]$DryRun,
    [switch]$WhatIf,
    [string]$DumpDir,
    [int]$TimeoutMinutes = 30,
    [int]$PollSeconds = 5,
    [string]$ApiVersion = "2024-11-30"
)
$ErrorActionPreference = "Stop"

# El data-plane en este entorno pasa por un proxy TLS; sin esto tanto az CLI
# como Invoke-WebRequest fallan la verificacion del certificado.
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"

$isDryRun = [bool]($DryRun -or $WhatIf)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$artifactsFile = Join-Path $repoRoot 'infra/ai/di-artifacts.json'
$sourceResourcesFile = Join-Path $repoRoot 'infra/ai/resources.prod.json'
$targetResourcesFile = Join-Path $repoRoot "infra/ai/resources.$Environment.json"
$manifestFile = Join-Path $repoRoot "infra/ai/di-artifacts.$Environment.manifest.json"

# Tipos de artefacto: ruta del data-plane, nombre del campo id en authorizeCopy
# y clave de la lista en di-artifacts.json.
$kinds = [ordered]@{
    classifiers = [pscustomobject]@{ Path = 'documentClassifiers'; IdField = 'classifierId'; ListKey = 'classifiers'; Label = 'classifier' }
    models      = [pscustomobject]@{ Path = 'documentModels';      IdField = 'modelId';      ListKey = 'models';      Label = 'model' }
}
$runningStates = @('notstarted', 'running')
$successStates = @('succeeded')

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
function Get-DiToken {
    $raw = az account get-access-token --resource https://cognitiveservices.azure.com --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $raw) {
        throw "no se pudo obtener el token de acceso (az account get-access-token, exit $LASTEXITCODE). Ejecuta 'az login'."
    }
    return $raw.Trim()
}

function Invoke-Di {
    # Devuelve @{ Status; Content (objeto o $null); Headers; Error } sin lanzar
    # en codigos 4xx/5xx: el llamador decide. Cuerpo y respuesta en UTF-8 explicito.
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

function Write-JsonFile {
    param([string]$Path, [object]$Object, [int]$Depth = 40)
    # .NET resuelve una ruta relativa contra el cwd del proceso, no contra el
    # de PowerShell: sin esto, escribir con una ubicacion relativa puede acabar
    # en la carpeta equivocada.
    $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    $json = $Object | ConvertTo-Json -Depth $Depth
    $json = $json -replace "`r`n", "`n"
    [IO.File]::WriteAllText($Path, ($json + "`n"), [Text.UTF8Encoding]::new($false))
}

function Format-Utc {
    # ConvertFrom-Json convierte las fechas ISO en DateTime; en consola se
    # muestran siempre en ISO UTC, no en el formato de la cultura local.
    param($Value)
    if ($null -eq $Value) { return '' }
    if ($Value -is [datetime]) { return $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ') }
    return [string]$Value
}

function Get-ArtifactUrl {
    param([string]$Endpoint, [string]$Path, [string]$Id, [string]$Suffix = '')
    return "$Endpoint/documentintelligence/$Path/$Id$Suffix" + "?api-version=$ApiVersion"
}

function Get-DocTypeNames {
    param($Details)
    if (-not $Details -or -not $Details.docTypes) { return @() }
    return @($Details.docTypes.PSObject.Properties.Name | Sort-Object)
}

function Get-Signature {
    # Firma comparable de un clasificador/modelo: docTypes ordenados y, si el
    # docType lleva fieldSchema (modelos de extraccion), sus nombres de campo.
    param($Details)
    $sig = [ordered]@{}
    if (-not $Details -or -not $Details.docTypes) { return ($sig | ConvertTo-Json -Compress) }
    foreach ($name in (Get-DocTypeNames $Details)) {
        $dt = $Details.docTypes.$name
        $fields = @()
        if ($dt.fieldSchema) { $fields = @($dt.fieldSchema.PSObject.Properties.Name | Sort-Object) }
        $sig[$name] = $fields
    }
    return ($sig | ConvertTo-Json -Depth 5 -Compress)
}

function Get-Summary {
    # Subconjunto del GET que va al manifiesto (sin cuerpos grandes).
    param($Details)
    if (-not $Details) { return $null }
    $o = [ordered]@{}
    foreach ($k in 'createdDateTime', 'modifiedDateTime', 'expirationDateTime', 'apiVersion', 'description', 'baseClassifierId') {
        if ($Details.PSObject.Properties.Name -contains $k -and $null -ne $Details.$k -and "$($Details.$k)" -ne '') { $o[$k] = $Details.$k }
    }
    $o['docTypes'] = @(Get-DocTypeNames $Details)
    return $o
}

function Wait-Copy {
    param([string]$OperationUrl, [string]$Token, [string]$Label)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $last = ''
    do {
        Start-Sleep -Seconds $PollSeconds
        $op = Invoke-Di -Method Get -Url $OperationUrl -Token $Token
        if ($op.Status -ne 200) { throw "sondeo de $Label -> HTTP $($op.Status): $($op.Error)" }
        $status = [string]$op.Content.status
        $pct = if ($null -ne $op.Content.percentCompleted) { " ($($op.Content.percentCompleted)%)" } else { '' }
        if ($status -ne $last) { Write-Host "      $(Get-Date -Format HH:mm:ss) status = $status$pct"; $last = $status }
        if ((Get-Date) -gt $deadline) { throw "la copia de $Label supera $TimeoutMinutes minutos (ultimo estado: '$status'); sigue en $OperationUrl" }
    } while ($status.ToLowerInvariant() -in $runningStates)
    if ($status.ToLowerInvariant() -notin $successStates) {
        throw "la copia de $Label termino en '$status'. Detalle: $($op.Content | ConvertTo-Json -Depth 10)"
    }
    return $op.Content
}

function Read-Manifest {
    if (-not (Test-Path $manifestFile)) { return $null }
    return Get-Content -Raw -Path $manifestFile -Encoding UTF8 | ConvertFrom-Json
}

# -----------------------------------------------------------------------------
# Carga de definiciones
# -----------------------------------------------------------------------------
foreach ($f in $artifactsFile, $sourceResourcesFile, $targetResourcesFile) { if (-not (Test-Path $f)) { throw "no existe $f" } }
$artifacts = Get-Content -Raw -Path $artifactsFile -Encoding UTF8 | ConvertFrom-Json
$sourceDi = (Get-Content -Raw -Path $sourceResourcesFile -Encoding UTF8 | ConvertFrom-Json).resources.di
$targetDi = (Get-Content -Raw -Path $targetResourcesFile -Encoding UTF8 | ConvertFrom-Json).resources.di
if (-not $sourceDi) { throw "resources.prod.json no define el alias 'di'" }
if (-not $targetDi) { throw "resources.$Environment.json no define el alias 'di'" }
if ($sourceDi.account -ne $artifacts.sourceAccount) {
    throw "di-artifacts.json declara sourceAccount '$($artifacts.sourceAccount)' pero resources.prod.json tiene di.account '$($sourceDi.account)'"
}
if ($targetDi.account -eq $sourceDi.account) { throw "el destino ($($targetDi.account)) es la misma cuenta que el origen" }
$src = [pscustomobject]@{ Account = $sourceDi.account; Endpoint = $sourceDi.endpoint.TrimEnd('/') }
$dst = [pscustomobject]@{ Account = $targetDi.account; Endpoint = $targetDi.endpoint.TrimEnd('/') }

# Lista de trabajo: (kind, id, promote, entrada de di-artifacts.json)
$items = foreach ($k in $Kind) {
    $meta = $kinds[$k]
    foreach ($entry in @($artifacts.($meta.ListKey))) {
        if ($Only -and $entry.id -notin $Only) { continue }
        [pscustomobject]@{ Kind = $meta.Label; Meta = $meta; Id = $entry.id; Promote = [bool]$entry.promote; Entry = $entry }
    }
}
$items = @($items)
if ($Only) {
    $unknown = @($Only | Where-Object { $_ -notin $items.Id })
    if ($unknown.Count -gt 0) { throw "di-artifacts.json no declara (en los tipos $($Kind -join '/')): $($unknown -join ', ')" }
}
if ($items.Count -eq 0) { throw "no hay artefactos que procesar en $artifactsFile" }

$modeLabel = if ($isDryRun) { 'DRY-RUN (sin escrituras)' } else { 'REAL (authorizeCopy en destino + copyTo desde origen)' }
Write-Host "== copy-di-artifacts: $Environment  [$modeLabel] ==" -ForegroundColor Cyan
Write-Host "  origen     : $($src.Account) ($($src.Endpoint))"
Write-Host "  destino    : $($dst.Account) ($($dst.Endpoint))"
Write-Host "  artefactos : $(($items | ForEach-Object { "$($_.Kind)/$($_.Id)$(if (-not $_.Promote) { ' (promote:false)' })" }) -join ', ')"
Write-Host "  manifiesto : $manifestFile"
if ($DumpDir) { New-Item -ItemType Directory -Force -Path $DumpDir | Out-Null; Write-Host "  volcado    : $DumpDir" }
Write-Host ""

$token = Get-DiToken
$tokenIssuedAt = Get-Date

# -----------------------------------------------------------------------------
# Por artefacto
# -----------------------------------------------------------------------------
$results = [System.Collections.Generic.List[object]]::new()
$manifestEntries = [System.Collections.Generic.List[object]]::new()
$conflicts = [System.Collections.Generic.List[string]]::new()

foreach ($item in $items) {
    $id = $item.Id
    $meta = $item.Meta
    $label = "$($item.Kind)/$id"
    $started = Get-Date
    if (((Get-Date) - $tokenIssuedAt).TotalMinutes -gt 40) { $token = Get-DiToken; $tokenIssuedAt = Get-Date }

    if (-not $item.Promote) {
        $reason = if ($item.Entry.pendingDataFix) { 'promote:false, pendingDataFix:true' } else { 'promote:false' }
        Write-Host "  $label : no se promociona ($reason)" -ForegroundColor DarkGray
        $results.Add([pscustomobject]@{ Kind = $item.Kind; Id = $id; Action = 'not-promoted'; Seconds = 0 })
        $manifestEntries.Add([ordered]@{ kind = $item.Kind; id = $id; status = 'not-promoted'; reason = $reason })
        continue
    }

    # Origen: debe existir.
    $srcUrl = Get-ArtifactUrl -Endpoint $src.Endpoint -Path $meta.Path -Id $id
    $s = Invoke-Di -Method Get -Url $srcUrl -Token $token
    if ($s.Status -eq 404) { throw "$label no existe en el origen $($src.Account)" }
    if ($s.Status -ne 200) { throw "GET $label en $($src.Account) -> HTTP $($s.Status): $($s.Error)" }
    if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$($item.Kind).$id.source.json") -Object $s.Content }
    $srcSig = Get-Signature $s.Content
    $srcSummary = Get-Summary $s.Content
    Write-Host "  $label" -ForegroundColor White
    Write-Host "      origen   : creado $(Format-Utc $s.Content.createdDateTime), api $($s.Content.apiVersion), $((Get-DocTypeNames $s.Content).Count) docTypes, caduca $(Format-Utc $s.Content.expirationDateTime)"
    if ($s.Content.apiVersion -and $s.Content.apiVersion -ne $ApiVersion) {
        Write-Host "      aviso    : entrenado con api-version $($s.Content.apiVersion); la Copy API exige $ApiVersion" -ForegroundColor DarkYellow
    }

    # Destino: existe / no existe / difiere.
    $dstUrl = Get-ArtifactUrl -Endpoint $dst.Endpoint -Path $meta.Path -Id $id
    $d = Invoke-Di -Method Get -Url $dstUrl -Token $token
    $exists = $false
    $action = 'copy'
    if ($d.Status -eq 200) {
        $exists = $true
        if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$($item.Kind).$id.target.before.json") -Object $d.Content }
        if ((Get-Signature $d.Content) -eq $srcSig) { $action = 'skip' }
        elseif ($Force) { $action = 'replace' }
        else { $action = 'conflict' }
    } elseif ($d.Status -ne 404) {
        throw "GET $label en $($dst.Account) -> HTTP $($d.Status): $($d.Error)"
    }

    $state = if ($exists) { "existe en destino (creado $(Format-Utc $d.Content.createdDateTime))" } else { 'no existe en destino' }
    $why = switch ($action) {
        'skip' { ' misma firma (docTypes)' }
        'replace' { ' firma distinta (-Force: DELETE + copia)' }
        'conflict' { ' firma distinta; usa -Force para reemplazarlo' }
        default { '' }
    }
    $color = switch ($action) { 'skip' { 'Green' } 'conflict' { 'Red' } default { 'Yellow' } }
    Write-Host "  $label : $state -> $action$why" -ForegroundColor $color

    if ($action -eq 'skip') {
        $results.Add([pscustomobject]@{ Kind = $item.Kind; Id = $id; Action = 'skip'; Seconds = 0 })
        $manifestEntries.Add([ordered]@{ kind = $item.Kind; id = $id; status = 'present'; verifiedAtUtc = (Get-Date).ToUniversalTime().ToString('o'); source = $srcSummary; target = (Get-Summary $d.Content) })
        continue
    }
    if ($action -eq 'conflict') {
        $conflicts.Add($label)
        $results.Add([pscustomobject]@{ Kind = $item.Kind; Id = $id; Action = 'conflict'; Seconds = 0 })
        $manifestEntries.Add([ordered]@{ kind = $item.Kind; id = $id; status = 'conflict'; verifiedAtUtc = (Get-Date).ToUniversalTime().ToString('o'); source = $srcSummary; target = (Get-Summary $d.Content) })
        continue
    }
    if ($isDryRun) {
        $results.Add([pscustomobject]@{ Kind = $item.Kind; Id = $id; Action = "dry-run ($action)"; Seconds = 0 })
        $manifestEntries.Add([ordered]@{ kind = $item.Kind; id = $id; status = 'dry-run'; source = $srcSummary })
        continue
    }

    if ($action -eq 'replace') {
        $del = Invoke-Di -Method Delete -Url $dstUrl -Token $token
        if ($del.Status -notin 200, 202, 204) { throw "DELETE $label en $($dst.Account) -> HTTP $($del.Status): $($del.Error)" }
        Write-Host "      borrado en $($dst.Account) (HTTP $($del.Status))" -ForegroundColor DarkGray
    }

    # 1) Autorizacion en destino.
    $copiedAt = (Get-Date).ToUniversalTime()
    $description = "Copia de $id desde $($src.Account) (creado en origen $(Format-Utc $s.Content.createdDateTime)) el $($copiedAt.ToString('yyyy-MM-dd')) por scripts/ai/copy-di-artifacts.ps1"
    $authUrl = "$($dst.Endpoint)/documentintelligence/$($meta.Path):authorizeCopy" + "?api-version=$ApiVersion"
    $authBody = [ordered]@{ $meta.IdField = $id; description = $description }
    $auth = Invoke-Di -Method Post -Url $authUrl -Token $token -Body $authBody
    if ($auth.Status -ne 200) { throw "authorizeCopy de $label en $($dst.Account) -> HTTP $($auth.Status): $($auth.Error)" }
    Write-Host "      autorizacion: $($auth.Content.targetResourceId) [$($auth.Content.targetResourceRegion)], caduca $(Format-Utc $auth.Content.expirationDateTime)" -ForegroundColor DarkGray

    # 2) copyTo desde origen con la autorizacion como cuerpo.
    $copyUrl = Get-ArtifactUrl -Endpoint $src.Endpoint -Path $meta.Path -Id $id -Suffix ':copyTo'
    $copy = Invoke-Di -Method Post -Url $copyUrl -Token $token -Body $auth.Content
    if ($copy.Status -notin 200, 201, 202) { throw "copyTo de $label desde $($src.Account) -> HTTP $($copy.Status): $($copy.Error)" }
    $opUrl = Get-SingleHeaderValue $copy.Headers['Operation-Location']
    $opId = $null
    if ($opUrl) {
        $opId = ($opUrl -split '/operations/')[-1] -replace '\?.*$', ''
        Write-Host "      copia aceptada (HTTP $($copy.Status)); operacion $opId" -ForegroundColor DarkGray
        $opResult = Wait-Copy -OperationUrl $opUrl -Token $token -Label $label
        if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$($item.Kind).$id.operation.json") -Object $opResult }
    } else {
        Write-Host "      HTTP $($copy.Status) sin Operation-Location; se asume sincrono" -ForegroundColor DarkGray
    }

    # 3) Verificacion en destino.
    $v = Invoke-Di -Method Get -Url $dstUrl -Token $token
    if ($v.Status -ne 200) { throw "verificacion de $label en $($dst.Account) -> HTTP $($v.Status): $($v.Error)" }
    if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$($item.Kind).$id.target.json") -Object $v.Content }
    if ((Get-Signature $v.Content) -ne $srcSig) {
        throw "$label copiado pero la firma en $($dst.Account) no coincide con el origen. Origen: $srcSig ; destino: $(Get-Signature $v.Content)"
    }
    $secs = [int]((Get-Date) - $started).TotalSeconds
    Write-Host "      $label : copiado en $secs s ($((Get-DocTypeNames $v.Content).Count) docTypes, caduca $(Format-Utc $v.Content.expirationDateTime))" -ForegroundColor Green
    $results.Add([pscustomobject]@{ Kind = $item.Kind; Id = $id; Action = $action; Seconds = $secs })
    $manifestEntries.Add([ordered]@{
            kind = $item.Kind; id = $id; status = 'copied'; copiedAtUtc = $copiedAt.ToString('o'); operationId = $opId
            source = $srcSummary; target = (Get-Summary $v.Content)
        })
}
Write-Host ""

# -----------------------------------------------------------------------------
# Manifiesto (fusion por kind+id con el anterior)
# -----------------------------------------------------------------------------
$previous = Read-Manifest
$merged = [System.Collections.Generic.List[object]]::new()
if ($previous -and $previous.artifacts) {
    foreach ($p in $previous.artifacts) {
        $hit = $manifestEntries | Where-Object { $_.kind -eq $p.kind -and $_.id -eq $p.id } | Select-Object -First 1
        if (-not $hit) { $merged.Add($p) }
    }
}
foreach ($e in $manifestEntries) { $merged.Add($e) }
$manifest = [ordered]@{
    environment    = $Environment
    sourceAccount  = $src.Account
    sourceEndpoint = $src.Endpoint
    targetAccount  = $dst.Account
    targetEndpoint = $dst.Endpoint
    apiVersion     = $ApiVersion
    updatedAtUtc   = (Get-Date).ToUniversalTime().ToString('o')
    artifacts      = @($merged | Sort-Object { $_.kind }, { $_.id })
}
if ($isDryRun) {
    if ($DumpDir) {
        $p = Join-Path $DumpDir "di-artifacts.$Environment.manifest.dry-run.json"
        Write-JsonFile -Path $p -Object $manifest -Depth 10
        Write-Host "[dry-run] manifiesto que resultaria: $p" -ForegroundColor DarkGray
    } else {
        Write-Host "[dry-run] no se escribe $manifestFile" -ForegroundColor DarkGray
    }
} else {
    Write-JsonFile -Path $manifestFile -Object $manifest -Depth 10
    Write-Host "manifiesto: $manifestFile"
}

Write-Host "== resumen ==" -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
if ($conflicts.Count -gt 0) {
    throw "artefactos en conflicto (existen en $($dst.Account) con otra firma): $($conflicts -join ', '). Revisa el destino o relanza con -Force."
}
