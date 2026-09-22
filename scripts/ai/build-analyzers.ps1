<#
.SYNOPSIS
    Reconstruye los analyzers de Azure AI Content Understanding de
    infra/ai/analyzers/*.json en el recurso Foundry de un entorno (DEV o PRE),
    reentrenando desde el dataset de etiquetado copiado a ese entorno.

.DESCRIPTION
    Es el paso 3 del flujo de promocion de infra/ai/README.md (Tarea 12 del
    plan "IA propia por entorno", AB#100314). Para cada definicion exportada:

      1. Resuelve el recurso destino en infra/ai/resources.<env>.json
         (cu_primary por defecto; cu_secondary solo si se pide con -Target).
      2. Resuelve el dataset del entorno en
         infra/ai/datasets/<id>@<version>.<env>.manifest.json (la version mas alta,
         salvo -DatasetVersion) y exige que el manifiesto sea del mismo entorno
         y tenga "status": "copied".
      3. Construye el cuerpo del PUT a partir del export: quita analyzerId y
         los campos de solo lectura (status, createdAt, lastModifiedAt,
         warnings, supportedModels), quita cualquier clave que empiece por "_"
         (_origin) y reescribe knowledgeSources[].containerUrl / prefix al
         dataset del entorno. El resto (description, tags, baseAnalyzerId,
         config, fieldSchema, processingLocation, models) viaja tal cual.
      4. Antes del primer PUT en cada cuenta, comprueba los defaults de
         Content Understanding (GET /contentunderstanding/defaults). Sin
         defaults el servicio rechaza cualquier build (DefaultsNotSet). El
         mapeo alias -> deployment deseado vive en
         infra/ai/deployments.<env>.json, clave contentUnderstandingDefaults
         por cuenta; si falta algun alias, se hace PATCH con el mapeo
         (fusionando con lo que ya hubiera). -SkipDefaults omite este paso.
      5. Si el analyzer ya existe con la misma definicion (comparacion
         canonica de las claves que se envian) y esta en "ready", se salta;
         con -Force se reconstruye igualmente. Si existe pero difiere, el PUT
         lleva allowReplace=true.
      6. PUT create-or-replace y sondeo de Operation-Location hasta
         succeeded/ready (o failed, o -TimeoutMinutes).

    Cada build reentrena desde el dataset: cuesta dinero (del orden de 20 EUR
    por analyzer segun el plan) y varios minutos. Ensaya siempre con -DryRun
    primero, que no escribe nada en Azure.

    Los ficheros infra/ai/analyzers/<id>@<cuenta>.json (copias de cuentas no
    primarias de PRO) se ignoran: la fuente de verdad es <id>.json. Para
    CU_NS_1.5_0 las dos copias solo difieren en tags.

    El recurso destino debe poder leer el dataset con su identidad
    administrada (Storage Blob Data Reader sobre la cuenta de storage del
    entorno); si no, el build termina en "failed".

    Todas las llamadas al data-plane van con un token fresco de
    "az account get-access-token" e Invoke-WebRequest decodificando UTF-8,
    nunca con "az rest" (recodifica la salida a la pagina de codigos de la
    consola y corrompe las tildes de las descripciones).

.PARAMETER Environment
    Entorno destino: dev o pre. Determina resources, deployments y manifiestos.
.PARAMETER Only
    Ids de analyzer a procesar (por defecto todos los <id>.json de la carpeta).
.PARAMETER Target
    Alias de recurso destino de resources.<env>.json: cu_primary (por defecto)
    y/o cu_secondary.
.PARAMETER DatasetVersion
    Fija la version del manifiesto (<id>@<version>) en vez de usar la mas alta.
.PARAMETER Force
    Reconstruye aunque el destino ya tenga la misma definicion.
.PARAMETER DryRun
    No escribe nada en Azure: solo lee, resuelve y muestra que haria (y vuelca
    los cuerpos si se indica -DumpDir). Alias funcional: -WhatIf.
.PARAMETER WhatIf
    Alias de -DryRun.
.PARAMETER SkipDefaults
    No comprueba ni corrige los defaults de Content Understanding del destino.
.PARAMETER DumpDir
    Carpeta donde escribir <id>.body.json con el cuerpo exacto del PUT (util
    para revisar el ensayo). No se escribe nada si se omite.
.PARAMETER TimeoutMinutes
    Tiempo maximo de sondeo por build. Por defecto 40.
.PARAMETER PollSeconds
    Intervalo de sondeo. Por defecto 10.
.EXAMPLE
    pwsh scripts/ai/build-analyzers.ps1 -Environment dev -Only CERA44_vado -DryRun -DumpDir docs/auxiliares/temps/2026-09-21/build-analyzers
.EXAMPLE
    pwsh scripts/ai/build-analyzers.ps1 -Environment dev -Only CERA44_vado
.EXAMPLE
    pwsh scripts/ai/build-analyzers.ps1 -Environment dev
#>
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre')][string]$Environment,
    [string[]]$Only,
    [ValidateSet('cu_primary', 'cu_secondary')][string[]]$Target = @('cu_primary'),
    [int]$DatasetVersion,
    [switch]$Force,
    [switch]$DryRun,
    [switch]$WhatIf,
    [switch]$SkipDefaults,
    [string]$DumpDir,
    [int]$TimeoutMinutes = 40,
    [int]$PollSeconds = 10,
    [string]$ApiVersion = "2025-11-01"
)
$ErrorActionPreference = "Stop"

# El data-plane en este entorno pasa por un proxy TLS; sin esto tanto az CLI
# como Invoke-WebRequest fallan la verificacion del certificado.
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"

$isDryRun = [bool]($DryRun -or $WhatIf)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$analyzersDir = Join-Path $repoRoot 'infra/ai/analyzers'
$datasetsDir = Join-Path $repoRoot 'infra/ai/datasets'
$resourcesFile = Join-Path $repoRoot "infra/ai/resources.$Environment.json"
$deploymentsFile = Join-Path $repoRoot "infra/ai/deployments.$Environment.json"

# Campos del export que NO se envian en el PUT (solo lectura o metadatos del repo).
$readOnlyFields = @('analyzerId', 'status', 'createdAt', 'lastModifiedAt', 'warnings', 'supportedModels')
$runningStates = @('running', 'notstarted', 'creating', 'inprogress')
$successStates = @('succeeded', 'ready')

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

function ConvertTo-Canonical {
    # Ordena las claves de forma recursiva para comparar definiciones sin que
    # el orden de propiedades del GET o del export cuente como diferencia.
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
    return ((ConvertTo-Canonical $Value) | ConvertTo-Json -Depth 40 -Compress)
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

function Resolve-Manifest {
    param([string]$AnalyzerId)
    # Un manifiesto por entorno: <id>@<version>.<env>.manifest.json.
    $candidates = @(Get-ChildItem -Path $datasetsDir -Filter "$AnalyzerId@*.$Environment.manifest.json" -File | ForEach-Object {
            if ($_.Name -match '^(?<id>.+)@(?<v>\d+)\.(?<env>[a-z]+)\.manifest\.json$' -and $Matches.id -eq $AnalyzerId -and $Matches.env -eq $Environment) {
                [pscustomobject]@{ File = $_; Version = [int]$Matches.v }
            }
        })
    if ($DatasetVersion) { $candidates = @($candidates | Where-Object { $_.Version -eq $DatasetVersion }) }
    if ($candidates.Count -eq 0) {
        $wanted = if ($DatasetVersion) { "$AnalyzerId@$DatasetVersion" } else { "$AnalyzerId@<version>" }
        throw "falta el manifiesto de dataset $wanted.$Environment.manifest.json en $datasetsDir (Tarea 10 pendiente para este analyzer en $Environment)"
    }
    $pick = $candidates | Sort-Object Version -Descending | Select-Object -First 1
    $m = Get-Content -Raw -Path $pick.File.FullName -Encoding UTF8 | ConvertFrom-Json
    if ($m.analyzerId -ne $AnalyzerId) { throw "el manifiesto $($pick.File.Name) declara analyzerId '$($m.analyzerId)', no '$AnalyzerId'" }
    if ($m.environment -ne $Environment) { throw "el manifiesto $($pick.File.Name) es del entorno '$($m.environment)', no de '$Environment': relanza copy-labeling-dataset.ps1 -Environment $Environment" }
    if ($m.status -ne 'copied') { throw "el manifiesto $($pick.File.Name) tiene status '$($m.status)' (se exige 'copied'): el dataset no esta copiado en $Environment" }
    if (-not $m.containerUrl -or -not $m.prefix) { throw "el manifiesto $($pick.File.Name) no informa containerUrl/prefix" }
    return [pscustomobject]@{ Manifest = $m; File = $pick.File; Version = $pick.Version }
}

function New-PutBody {
    param([pscustomobject]$Export, [pscustomobject]$Manifest)
    $body = [ordered]@{}
    foreach ($p in $Export.PSObject.Properties) {
        if ($readOnlyFields -contains $p.Name) { continue }
        if ($p.Name.StartsWith('_')) { continue }
        if ($null -eq $p.Value) { continue }
        $body[$p.Name] = $p.Value
    }
    if ($body.Contains('knowledgeSources') -and $body['knowledgeSources']) {
        foreach ($ks in @($body['knowledgeSources'])) {
            $ks.containerUrl = $Manifest.containerUrl
            $ks.prefix = $Manifest.prefix
        }
    }
    return $body
}

function Get-RequiredAliases {
    param([object]$Body)
    if (-not $Body.Contains('models') -or -not $Body['models']) { return @() }
    return @($Body['models'].PSObject.Properties.Value | Where-Object { $_ } | Select-Object -Unique)
}

function Ensure-Defaults {
    # Comprueba (y corrige si hace falta) los defaults de CU de la cuenta.
    # Devuelve el mapeo alias -> deployment que quedara vigente.
    param([string]$Endpoint, [string]$Account, [string]$Token, [string[]]$RequiredAliases)
    $url = "$Endpoint/contentunderstanding/defaults?api-version=$ApiVersion"
    $r = Invoke-Cu -Method Get -Url $url -Token $Token
    $current = @{}
    if ($r.Status -eq 200 -and $r.Content.modelDeployments) {
        foreach ($p in $r.Content.modelDeployments.PSObject.Properties) { $current[$p.Name] = $p.Value }
    } elseif ($r.Status -in 400, 404 -and $r.Content.error.innererror.code -eq 'DefaultsNotSet') {
        Write-Host "      defaults: no fijados todavia (DefaultsNotSet)" -ForegroundColor DarkYellow
    } elseif ($r.Status -in 401, 403) {
        throw "sin acceso al data plane de $Account (HTTP $($r.Status)): falta el rol Cognitive Services User"
    } else {
        throw "GET defaults de $Account -> HTTP $($r.Status): $($r.Error)"
    }

    $desired = @{}
    $decl = $deployments.contentUnderstandingDefaults
    if ($decl -and $decl.PSObject.Properties.Name -contains $Account) {
        foreach ($p in $decl.$Account.PSObject.Properties) { $desired[$p.Name] = $p.Value }
    }
    # Consistencia declarativa: cada deployment del mapeo debe existir en la lista de la cuenta.
    $declaredDeployments = @()
    if ($deployments.accounts.PSObject.Properties.Name -contains $Account) { $declaredDeployments = @($deployments.accounts.$Account | ForEach-Object { $_.name }) }
    foreach ($alias in $desired.Keys) {
        if ($declaredDeployments -notcontains $desired[$alias]) {
            throw "contentUnderstandingDefaults.$Account.$alias apunta al deployment '$($desired[$alias])', que no figura en accounts.$Account de $deploymentsFile"
        }
    }

    $merged = @{} + $current
    $changes = @()
    foreach ($alias in ($desired.Keys | Sort-Object)) {
        if ($current[$alias] -ne $desired[$alias]) {
            $changes += "$alias : '$($current[$alias])' -> '$($desired[$alias])'"
            $merged[$alias] = $desired[$alias]
        }
    }
    $missing = @($RequiredAliases | Where-Object { -not $merged.ContainsKey($_) })
    if ($missing.Count -gt 0) {
        throw "los analyzers necesitan los alias [$($missing -join ', ')] y ni los defaults actuales de $Account ni contentUnderstandingDefaults.$Account en $deploymentsFile los mapean"
    }
    if ($changes.Count -eq 0) {
        Write-Host "      defaults: OK ($($merged.Count) alias)" -ForegroundColor Green
        return $merged
    }
    Write-Host "      defaults: PATCH necesario" -ForegroundColor Yellow
    foreach ($c in $changes) { Write-Host "        $c" }
    if ($isDryRun) {
        Write-Host "      [dry-run] no se hace PATCH /contentunderstanding/defaults" -ForegroundColor DarkGray
        return $merged
    }
    $patch = Invoke-Cu -Method Patch -Url $url -Token $Token -Body @{ modelDeployments = $merged }
    if ($patch.Status -notin 200, 201, 204) { throw "PATCH defaults de $Account -> HTTP $($patch.Status): $($patch.Error)" }
    Write-Host "      defaults: actualizados" -ForegroundColor Green
    return $merged
}

function Wait-Build {
    param([string]$OperationUrl, [string]$Token, [string]$Label)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $last = ''
    do {
        Start-Sleep -Seconds $PollSeconds
        $op = Invoke-Cu -Method Get -Url $OperationUrl -Token $Token
        if ($op.Status -ne 200) { throw "sondeo de $Label -> HTTP $($op.Status): $($op.Error)" }
        $status = if ($op.Content.status) { [string]$op.Content.status } elseif ($op.Content.result.status) { [string]$op.Content.result.status } else { '' }
        if ($status -ne $last) { Write-Host "      $(Get-Date -Format HH:mm:ss) status = $status"; $last = $status }
        if ((Get-Date) -gt $deadline) { throw "el build de $Label supera $TimeoutMinutes minutos (ultimo estado: '$status'); sigue en $OperationUrl" }
    } while ($status.ToLowerInvariant() -in $runningStates)
    if ($status.ToLowerInvariant() -notin $successStates) {
        throw "el build de $Label termino en '$status'. Detalle: $($op.Content | ConvertTo-Json -Depth 10)"
    }
    return $op.Content
}

# -----------------------------------------------------------------------------
# Carga de definiciones
# -----------------------------------------------------------------------------
foreach ($f in $resourcesFile, $deploymentsFile) { if (-not (Test-Path $f)) { throw "no existe $f" } }
$resources = (Get-Content -Raw -Path $resourcesFile -Encoding UTF8 | ConvertFrom-Json).resources
$deployments = Get-Content -Raw -Path $deploymentsFile -Encoding UTF8 | ConvertFrom-Json

$targets = foreach ($alias in $Target) {
    $t = $resources.$alias
    if (-not $t) { throw "resources.$Environment.json no define el alias '$alias'" }
    [pscustomobject]@{ Alias = $alias; Account = $t.account; Endpoint = $t.endpoint.TrimEnd('/') }
}

$files = @(Get-ChildItem -Path $analyzersDir -Filter '*.json' -File | Sort-Object Name)
$skippedCopies = @($files | Where-Object { $_.BaseName -like '*@*' })
$files = @($files | Where-Object { $_.BaseName -notlike '*@*' })
# Con "pwsh -File", -Only A,B llega como una sola cadena "A,B": se admiten las dos formas.
if ($Only) { $Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
if ($Only) {
    $unknown = @($Only | Where-Object { $_ -notin $files.BaseName })
    if ($unknown.Count -gt 0) { throw "no existe infra/ai/analyzers/<id>.json para: $($unknown -join ', ')" }
    $files = @($files | Where-Object { $Only -contains $_.BaseName })
}
if ($files.Count -eq 0) { throw "no hay definiciones que procesar en $analyzersDir" }

$modeLabel = if ($isDryRun) { 'DRY-RUN (sin escrituras)' } else { 'REAL (PUT + build; cuesta dinero)' }
Write-Host "== build-analyzers: $Environment  [$modeLabel] ==" -ForegroundColor Cyan
Write-Host "  destinos   : $(($targets | ForEach-Object { "$($_.Alias)=$($_.Account)" }) -join ', ')"
Write-Host "  analyzers  : $(($files | ForEach-Object { $_.BaseName }) -join ', ')"
if ($skippedCopies.Count -gt 0) { Write-Host "  ignorados  : $(($skippedCopies | ForEach-Object { $_.Name }) -join ', ') (copias de cuentas no primarias)" -ForegroundColor DarkGray }
if ($DumpDir) { New-Item -ItemType Directory -Force -Path $DumpDir | Out-Null; Write-Host "  volcado    : $DumpDir" }
Write-Host ""

# Resolver export + manifiesto + cuerpo de todos antes de tocar nada.
$items = foreach ($file in $files) {
    $id = $file.BaseName
    $export = Get-Content -Raw -Path $file.FullName -Encoding UTF8 | ConvertFrom-Json
    if ($export.analyzerId -and $export.analyzerId -ne $id) { throw "$($file.Name) declara analyzerId '$($export.analyzerId)', no '$id'" }
    $res = Resolve-Manifest -AnalyzerId $id
    $body = New-PutBody -Export $export -Manifest $res.Manifest
    Write-Host "  $id" -ForegroundColor White
    Write-Host "      dataset  : $($res.File.Name) -> $($res.Manifest.containerUrl) / $($res.Manifest.prefix) ($($res.Manifest.fileCount) ficheros)"
    Write-Host "      modelos  : $((Get-RequiredAliases $body) -join ', ')"
    Write-Host "      claves   : $($body.Keys -join ', ')"
    if ($DumpDir) { Write-JsonFile -Path (Join-Path $DumpDir "$id.body.json") -Object $body }
    [pscustomobject]@{ Id = $id; File = $file; Body = $body; Manifest = $res }
}
Write-Host ""

$token = Get-CuToken
$tokenIssuedAt = Get-Date
$requiredAliases = @($items | ForEach-Object { Get-RequiredAliases $_.Body } | Select-Object -Unique)

# -----------------------------------------------------------------------------
# Por destino: defaults y despues cada analyzer
# -----------------------------------------------------------------------------
$results = [System.Collections.Generic.List[object]]::new()
foreach ($t in $targets) {
    Write-Host "== destino $($t.Alias): $($t.Account) ($($t.Endpoint)) ==" -ForegroundColor Cyan
    if (-not $SkipDefaults) {
        [void](Ensure-Defaults -Endpoint $t.Endpoint -Account $t.Account -Token $token -RequiredAliases $requiredAliases)
    }

    foreach ($item in $items) {
        $id = $item.Id
        $started = Get-Date
        # Los builds largos pueden agotar el token: renovar si lleva mas de 40 minutos.
        if (((Get-Date) - $tokenIssuedAt).TotalMinutes -gt 40) { $token = Get-CuToken; $tokenIssuedAt = Get-Date }
        $getUrl = "$($t.Endpoint)/contentunderstanding/analyzers/$id`?api-version=$ApiVersion"
        $g = Invoke-Cu -Method Get -Url $getUrl -Token $token
        $exists = $false
        $action = 'create'
        $diffKeys = @()
        if ($g.Status -eq 200) {
            $exists = $true
            $action = 'replace'
            foreach ($k in $item.Body.Keys) {
                $cur = if ($g.Content.PSObject.Properties.Name -contains $k) { $g.Content.$k } else { $null }
                if ((ConvertTo-CanonicalJson $cur) -ne (ConvertTo-CanonicalJson $item.Body[$k])) { $diffKeys += $k }
            }
            if ($diffKeys.Count -eq 0 -and $g.Content.status -eq 'ready' -and -not $Force) { $action = 'skip' }
        } elseif ($g.Status -ne 404) {
            throw "GET $id en $($t.Account) -> HTTP $($g.Status): $($g.Error)"
        }

        $state = if ($exists) { "existe (status $($g.Content.status))" } else { 'no existe' }
        $why = if ($diffKeys.Count -gt 0) { " difiere en: $($diffKeys -join ', ')" } elseif ($exists -and $Force) { ' (-Force)' } else { '' }
        Write-Host "  $id : $state -> $action$why" -ForegroundColor $(if ($action -eq 'skip') { 'Green' } else { 'Yellow' })

        if ($action -eq 'skip') {
            $results.Add([pscustomobject]@{ Target = $t.Account; Analyzer = $id; Action = 'skip'; Status = $g.Content.status; Seconds = 0 })
            continue
        }
        if ($isDryRun) {
            $results.Add([pscustomobject]@{ Target = $t.Account; Analyzer = $id; Action = "dry-run ($action)"; Status = $(if ($exists) { $g.Content.status } else { '-' }); Seconds = 0 })
            continue
        }

        $putUrl = $getUrl
        if ($exists) { $putUrl += '&allowReplace=true' }
        $put = Invoke-Cu -Method Put -Url $putUrl -Token $token -Body $item.Body
        if ($put.Status -notin 200, 201, 202) { throw "PUT $id en $($t.Account) -> HTTP $($put.Status): $($put.Error)" }
        $opUrl = Get-SingleHeaderValue $put.Headers['Operation-Location']
        if ($opUrl) {
            Write-Host "      build aceptado (HTTP $($put.Status)); sondeando $opUrl" -ForegroundColor DarkGray
            [void](Wait-Build -OperationUrl $opUrl -Token $token -Label "$id@$($t.Account)")
        } else {
            Write-Host "      HTTP $($put.Status) sin Operation-Location; se asume sincrono" -ForegroundColor DarkGray
        }
        $v = Invoke-Cu -Method Get -Url $getUrl -Token $token
        if ($v.Status -ne 200) { throw "verificacion de $id en $($t.Account) -> HTTP $($v.Status): $($v.Error)" }
        $secs = [int]((Get-Date) - $started).TotalSeconds
        Write-Host "      $id : status $($v.Content.status) en $secs s" -ForegroundColor Green
        if ($v.Content.warnings) { Write-Host "      warnings: $($v.Content.warnings | ConvertTo-Json -Depth 5 -Compress)" -ForegroundColor DarkYellow }
        if ($v.Content.status -ne 'ready') { throw "$id en $($t.Account) quedo en status '$($v.Content.status)', no 'ready'" }
        $results.Add([pscustomobject]@{ Target = $t.Account; Analyzer = $id; Action = $action; Status = $v.Content.status; Seconds = $secs })
    }
    Write-Host ""
}

Write-Host "== resumen ==" -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
