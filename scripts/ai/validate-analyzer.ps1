#Requires -Version 7.0
<#
.SYNOPSIS
    Valida que los analyzers de Content Understanding reconstruidos en un
    entorno (DEV o PRE) devuelven los mismos campos que el original de PRO,
    analizando una muestra fija de PDF en ambos recursos y comparando valores.

.DESCRIPTION
    Es el paso 5 del flujo de promocion de infra/ai/README.md (Tarea 13 del
    plan "IA propia por entorno", AB#100315). Para cada definicion de
    infra/ai/analyzers/<id>.json:

      1. Resuelve el recurso origen (cu_primary de resources.prod.json) y el
         destino (cu_primary de resources.<env>.json, o cu_secondary con
         -Target) y comprueba con GET que el analyzer existe en los dos.
      2. Resuelve el dataset copiado al entorno
         (infra/ai/datasets/<id>@<version>.manifest.json, status "copied") y
         la muestra de validacion en infra/ai/validation/<id>.json: una lista
         de blobs PDF del dataset elegida de forma determinista (indices
         equiespaciados sobre los PDF del manifiesto ordenados por nombre).
         Con -WriteSelection se (re)genera ese fichero a partir del
         manifiesto; sin el, debe existir. No se versiona ningun PDF: la
         muestra son referencias al dataset del entorno.
      3. Descarga cada PDF del storage del entorno con token de
         "az account get-access-token --resource https://storage.azure.com/",
         verifica el MD5 contra el manifiesto, y lo envia en bytes a
         POST /contentunderstanding/analyzers/<id>:analyzeBinary en origen y
         destino (api-version 2025-11-01). Se envia en binario porque en esa
         version :analyze solo acepta URL, y la identidad del recurso de PRO
         no puede leer el storage de DEV/PRE (los roles cruzados van en el
         otro sentido).
      4. Sondea Operation-Location hasta Succeeded/Failed. Primero compara
         result.contents[0].markdown (salida de OCR + layout) de los dos
         lados: si es identico, la etapa de extraccion de contenido es la
         misma y cualquier diferencia de campos viene de la etapa LLM.
         Despues toma result.contents[0].fields y compara campo a campo (los del
         fieldSchema de la definicion) en forma canonica: cadenas sin
         espacios sobrantes e ignorando mayusculas, numeros redondeados a 6
         decimales, arrays y objetos por JSON canonico recursivo. Un campo
         ausente o vacio en los dos lados cuenta como igual.
      5. Calcula el acuerdo global (campos iguales / campos comparados) y el
         acuerdo restringido a campos con method "extract" (los "generate"
         tienen varianza propia del modelo incluso contra el mismo recurso).
         Si el acuerdo global de algun analyzer queda por debajo de
         -MinFieldsRatio (0,85 por defecto: linea base PRO/PRO menos margen),
         el script termina con error tras procesar todos.

    Cada :analyzeBinary cuesta dinero en los dos recursos (paginas de
    documento + tokens del modelo). Ensaya siempre con -DryRun, que solo
    hace los GET de comprobacion y no descarga ni analiza nada.

    Todas las llamadas al data-plane van con token fresco e Invoke-WebRequest
    decodificando UTF-8, nunca con "az rest" (recodifica la salida a la
    pagina de codigos de la consola y corrompe las tildes; ademas az.cmd
    pierde comillas y corta las URL en "&").

    El informe se escribe en -OutFile (por defecto
    docs/auxiliares/temps/<yyyy-MM-dd>/validacion-analyzers-<env>.txt,
    gitignored) y, si se indica -DumpDir, los resultados crudos de cada
    analisis en <DumpDir>/<id>/<blob>.<origen|destino>.json.

.PARAMETER Environment
    Entorno destino: dev o pre. Determina resources.<env>.json, el storage
    del dataset y el nombre del informe.
.PARAMETER Only
    Ids de analyzer a procesar (por defecto todos los <id>.json de la carpeta).
.PARAMETER Target
    Alias del recurso destino en resources.<env>.json: cu_primary (por
    defecto) o cu_secondary.
.PARAMETER SourceTarget
    Alias del recurso origen en resources.prod.json. Por defecto cu_primary.
.PARAMETER TargetSuffix
    Sufijo que se anade al id del analyzer en el DESTINO (por ejemplo
    "_copytest" para comparar CERA46 de PRO con CERA46_copytest del entorno).
    En origen se usa siempre el id sin sufijo. Vacio por defecto.
.PARAMETER SelfCheck
    Control de varianza: analiza cada PDF dos veces contra el recurso ORIGEN
    (PRO) y compara las dos respuestas entre si, sin tocar el destino. Mide
    cuanto difiere el propio analyzer original consigo mismo; el acuerdo
    origen/destino de la pasada normal solo es interpretable frente a esta
    linea base. El informe va a validacion-analyzers-<env>-selfcheck.txt.
.PARAMETER DatasetVersion
    Fija la version del manifiesto (<id>@<version>) en vez de usar la mas alta.
.PARAMETER SampleSize
    Numero de PDF por analyzer al generar la seleccion con -WriteSelection.
    Por defecto 5.
.PARAMETER WriteSelection
    (Re)genera infra/ai/validation/<id>.json a partir del manifiesto del
    dataset y sigue. Sin este switch el fichero debe existir ya.
.PARAMETER MinFieldsRatio
    Umbral minimo de acuerdo global por analyzer. Por defecto 0.85: la linea
    base PRO/PRO (-SelfCheck) del 2026-09-21 dio 0,86-0,91, y el criterio de
    aceptacion es "acuerdo >= linea base menos margen" con markdown identico.
.PARAMETER DryRun
    No descarga ni analiza nada: solo GET de los analyzers en origen y destino,
    resolucion de la muestra y del manifiesto, y el informe de lo que haria.
    Alias funcional: -WhatIf.
.PARAMETER WhatIf
    Alias de -DryRun.
.PARAMETER OutFile
    Ruta del informe. Por defecto
    docs/auxiliares/temps/<yyyy-MM-dd>/validacion-analyzers-<env>.txt.
.PARAMETER DumpDir
    Carpeta donde volcar los resultados crudos de cada analisis. No se
    escribe nada si se omite.
.PARAMETER TimeoutMinutes
    Tiempo maximo de sondeo por analisis. Por defecto 15.
.PARAMETER PollSeconds
    Intervalo de sondeo. Por defecto 5.
.EXAMPLE
    pwsh scripts/ai/validate-analyzer.ps1 -Environment dev -WriteSelection -DryRun
.EXAMPLE
    pwsh scripts/ai/validate-analyzer.ps1 -Environment dev -Only CERA44_vado -DumpDir docs/auxiliares/temps/2026-09-21/validate-analyzers
.EXAMPLE
    pwsh scripts/ai/validate-analyzer.ps1 -Environment dev
#>
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre')][string]$Environment,
    [string[]]$Only,
    [ValidateSet('cu_primary', 'cu_secondary')][string]$Target = 'cu_primary',
    [ValidateSet('cu_primary', 'cu_secondary')][string]$SourceTarget = 'cu_primary',
    [switch]$SelfCheck,
    [string]$TargetSuffix = '',
    [int]$DatasetVersion,
    [ValidateRange(1, 50)][int]$SampleSize = 5,
    [switch]$WriteSelection,
    [ValidateRange(0.0, 1.0)][double]$MinFieldsRatio = 0.85,
    [switch]$DryRun,
    [switch]$WhatIf,
    [string]$OutFile,
    [string]$DumpDir,
    [int]$TimeoutMinutes = 15,
    [int]$PollSeconds = 5,
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
$validationDir = Join-Path $repoRoot 'infra/ai/validation'
$sourceResourcesFile = Join-Path $repoRoot 'infra/ai/resources.prod.json'
$targetResourcesFile = Join-Path $repoRoot "infra/ai/resources.$Environment.json"
$storageApiVersion = "2021-08-06"
$runningStates = @('running', 'notstarted')
$successStates = @('succeeded')

if (-not $OutFile) {
    $suffix = if ($SelfCheck) { '-selfcheck' } else { '' }
    $OutFile = Join-Path $repoRoot ("docs/auxiliares/temps/{0}/validacion-analyzers-{1}{2}.txt" -f (Get-Date -Format 'yyyy-MM-dd'), $Environment, $suffix)
}

# -----------------------------------------------------------------------------
# Informe: todo lo que se muestra se guarda tambien en -OutFile
# -----------------------------------------------------------------------------
$report = [System.Collections.Generic.List[string]]::new()
function Out-Line {
    param([string]$Text = '', [ConsoleColor]$Color = [ConsoleColor]::Gray)
    Write-Host $Text -ForegroundColor $Color
    $report.Add($Text)
}
function Save-Report {
    $path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
    [IO.File]::WriteAllText($path, (($report -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
    Write-Host "informe: $path" -ForegroundColor DarkGray
}

# -----------------------------------------------------------------------------
# Helpers HTTP
# -----------------------------------------------------------------------------
function Get-Token {
    param([Parameter(Mandatory)][string]$Resource)
    $raw = az account get-access-token --resource $Resource --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $raw) {
        throw "no se pudo obtener el token de acceso para $Resource (az account get-access-token, exit $LASTEXITCODE). Ejecuta 'az login'."
    }
    return $raw.Trim()
}

function Invoke-Cu {
    # Devuelve @{ Status; Content (objeto o $null); Headers; Error } sin lanzar
    # en codigos 4xx/5xx: el llamador decide. Cuerpo JSON o bytes; respuesta en UTF-8.
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Token,
        [object]$Body,
        [byte[]]$Bytes,
        [string]$ContentType
    )
    $headers = @{ Authorization = "Bearer $Token" }
    $params = @{ Uri = $Url; Method = $Method; Headers = $headers; UseBasicParsing = $true; SkipCertificateCheck = $true }
    if ($null -ne $Bytes) {
        $params['Body'] = $Bytes
        $params['ContentType'] = if ($ContentType) { $ContentType } else { 'application/octet-stream' }
    } elseif ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 40
        $params['Body'] = [Text.Encoding]::UTF8.GetBytes($json)
        $params['ContentType'] = 'application/json; charset=utf-8'
    }
    try {
        $resp = Invoke-WebRequest @params
        $text = ''
        if ($resp.RawContentStream) { $text = [Text.Encoding]::UTF8.GetString($resp.RawContentStream.ToArray()) }
        $content = if ($text.Trim()) { $text | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Content = $content; Headers = $resp.Headers; Error = $null; Raw = $text }
    } catch {
        $status = 0
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        $err = ''
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $err = $_.ErrorDetails.Message } elseif ($_.Exception.Message) { $err = $_.Exception.Message }
        $content = $null
        try { if ($err.Trim().StartsWith('{')) { $content = $err | ConvertFrom-Json } } catch { }
        return [pscustomobject]@{ Status = $status; Content = $content; Headers = $null; Error = $err; Raw = $err }
    }
}

function Get-SingleHeaderValue {
    param($HeaderValue)
    if ($null -eq $HeaderValue) { return $null }
    return ($HeaderValue | Select-Object -First 1)
}

function Get-BlobUrl {
    param([string]$ContainerUrl, [string]$RelativePath)
    $segments = $RelativePath -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }
    return "$ContainerUrl/" + ($segments -join '/')
}

function Get-BlobBytes {
    param([string]$BlobUrl, [string]$Token)
    $tempFile = [IO.Path]::GetTempFileName()
    try {
        $lastError = $null
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                Invoke-WebRequest -Uri $BlobUrl -Method Get -Headers @{
                    Authorization  = "Bearer $Token"
                    'x-ms-version' = $storageApiVersion
                } -OutFile $tempFile -UseBasicParsing -SkipCertificateCheck | Out-Null
                return [IO.File]::ReadAllBytes($tempFile)
            } catch {
                $lastError = $_
                if ($attempt -lt 3) { Start-Sleep -Seconds (2 * $attempt) }
            }
        }
        throw "fallo descargando $BlobUrl tras 3 intentos: $lastError"
    } finally {
        Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    }
}

function Write-JsonFile {
    param([string]$Path, [object]$Object, [int]$Depth = 40)
    # .NET resuelve una ruta relativa contra el cwd del proceso, no contra el
    # de PowerShell: sin esto, escribir con una ubicacion relativa puede acabar
    # en la carpeta equivocada.
    $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $json = $Object | ConvertTo-Json -Depth $Depth
    $json = $json -replace "`r`n", "`n"
    [IO.File]::WriteAllText($Path, ($json + "`n"), [Text.UTF8Encoding]::new($false))
}

# -----------------------------------------------------------------------------
# Manifiesto y seleccion de muestra
# -----------------------------------------------------------------------------
function Resolve-Manifest {
    param([string]$AnalyzerId)
    $candidates = @(Get-ChildItem -Path $datasetsDir -Filter "$AnalyzerId@*.manifest.json" -File | ForEach-Object {
            if ($_.Name -match '^(?<id>.+)@(?<v>\d+)\.manifest\.json$' -and $Matches.id -eq $AnalyzerId) {
                [pscustomobject]@{ File = $_; Version = [int]$Matches.v }
            }
        })
    if ($DatasetVersion) { $candidates = @($candidates | Where-Object { $_.Version -eq $DatasetVersion }) }
    if ($candidates.Count -eq 0) {
        $wanted = if ($DatasetVersion) { "$AnalyzerId@$DatasetVersion" } else { "$AnalyzerId@<version>" }
        throw "falta el manifiesto de dataset $wanted.manifest.json en $datasetsDir (Tarea 10 pendiente para este analyzer en $Environment)"
    }
    $pick = $candidates | Sort-Object Version -Descending | Select-Object -First 1
    $m = Get-Content -Raw -Path $pick.File.FullName -Encoding UTF8 | ConvertFrom-Json
    if ($m.analyzerId -ne $AnalyzerId) { throw "el manifiesto $($pick.File.Name) declara analyzerId '$($m.analyzerId)', no '$AnalyzerId'" }
    if ($m.environment -ne $Environment) { throw "el manifiesto $($pick.File.Name) es del entorno '$($m.environment)', no de '$Environment': relanza copy-labeling-dataset.ps1 -Environment $Environment" }
    if ($m.status -ne 'copied') { throw "el manifiesto $($pick.File.Name) tiene status '$($m.status)' (se exige 'copied'): el dataset no esta copiado en $Environment" }
    if (-not $m.containerUrl -or -not $m.prefix) { throw "el manifiesto $($pick.File.Name) no informa containerUrl/prefix" }
    return [pscustomobject]@{ Manifest = $m; File = $pick.File; Version = $pick.Version }
}

function New-Selection {
    # Indices equiespaciados sobre los PDF del manifiesto ordenados por nombre:
    # determinista, reproducible y sin depender de un generador aleatorio.
    param([pscustomobject]$Resolved, [string]$AnalyzerId)
    $pdfs = @($Resolved.Manifest.files | Where-Object { $_.contentType -eq 'application/pdf' } | Sort-Object name)
    if ($pdfs.Count -eq 0) { throw "el manifiesto $($Resolved.File.Name) no tiene blobs application/pdf" }
    $k = [Math]::Min($SampleSize, $pdfs.Count)
    $docs = for ($i = 0; $i -lt $k; $i++) {
        $idx = [int][Math]::Floor($i * $pdfs.Count / $k)
        $p = $pdfs[$idx]
        [ordered]@{ name = $p.name; size = $p.size; md5 = $p.md5 }
    }
    return [ordered]@{
        analyzerId     = $AnalyzerId
        datasetVersion = $Resolved.Version
        manifest       = $Resolved.File.Name
        selection      = [ordered]@{ method = 'evenly-spaced-by-name'; sampleSize = $k; pdfCount = $pdfs.Count }
        documents      = @($docs)
    }
}

function Resolve-Selection {
    param([string]$AnalyzerId, [pscustomobject]$Resolved)
    $path = Join-Path $validationDir "$AnalyzerId.json"
    if ($WriteSelection) {
        $sel = New-Selection -Resolved $Resolved -AnalyzerId $AnalyzerId
        Write-JsonFile -Path $path -Object $sel
        Out-Line "      muestra  : generada en infra/ai/validation/$AnalyzerId.json ($($sel.selection.sampleSize) de $($sel.selection.pdfCount) PDF)" DarkYellow
        return (Get-Content -Raw -Path $path -Encoding UTF8 | ConvertFrom-Json)
    }
    if (-not (Test-Path $path)) { throw "falta infra/ai/validation/$AnalyzerId.json: genera la muestra con -WriteSelection" }
    $sel = Get-Content -Raw -Path $path -Encoding UTF8 | ConvertFrom-Json
    if ($sel.analyzerId -ne $AnalyzerId) { throw "infra/ai/validation/$AnalyzerId.json declara analyzerId '$($sel.analyzerId)'" }
    if ($sel.datasetVersion -ne $Resolved.Version) { throw "infra/ai/validation/$AnalyzerId.json es de la version de dataset $($sel.datasetVersion), no de la $($Resolved.Version): regenera con -WriteSelection" }
    if (-not $sel.documents -or @($sel.documents).Count -eq 0) { throw "infra/ai/validation/$AnalyzerId.json no tiene documentos" }
    # Cada documento de la muestra debe seguir en el manifiesto con el mismo MD5.
    $byName = @{}
    foreach ($f in $Resolved.Manifest.files) { $byName[$f.name] = $f }
    foreach ($d in $sel.documents) {
        if (-not $byName.ContainsKey($d.name)) { throw "el documento $($d.name) de la muestra no esta en $($Resolved.File.Name)" }
        if ($d.md5 -and $byName[$d.name].md5 -ne $d.md5) { throw "el documento $($d.name) cambio de MD5 respecto a $($Resolved.File.Name): regenera la muestra" }
    }
    return $sel
}

# -----------------------------------------------------------------------------
# Comparacion de campos
# -----------------------------------------------------------------------------
function ConvertTo-FieldValue {
    # Reduce un ContentField (type + valueXxx) a un valor plano y canonico.
    param($Field)
    if ($null -eq $Field) { return $null }
    $type = [string]$Field.type
    switch ($type.ToLowerInvariant()) {
        'string' { return (ConvertTo-CanonicalString $Field.valueString) }
        'date' { return (ConvertTo-CanonicalString $Field.valueDate) }
        'time' { return (ConvertTo-CanonicalString $Field.valueTime) }
        'number' { if ($null -eq $Field.valueNumber) { return $null }; return [Math]::Round([double]$Field.valueNumber, 6) }
        'integer' { if ($null -eq $Field.valueInteger) { return $null }; return [int64]$Field.valueInteger }
        'boolean' { if ($null -eq $Field.valueBoolean) { return $null }; return [bool]$Field.valueBoolean }
        'array' {
            if ($null -eq $Field.valueArray) { return $null }
            $list = [System.Collections.ArrayList]::new()
            foreach ($i in @($Field.valueArray)) { [void]$list.Add((ConvertTo-FieldValue $i)) }
            return , $list.ToArray()
        }
        'object' {
            if ($null -eq $Field.valueObject) { return $null }
            $o = [ordered]@{}
            foreach ($p in ($Field.valueObject.PSObject.Properties | Sort-Object Name)) { $o[$p.Name] = ConvertTo-FieldValue $p.Value }
            return $o
        }
        'json' { return $Field.valueJson }
        default {
            $vp = $Field.PSObject.Properties | Where-Object { $_.Name -like 'value*' } | Select-Object -First 1
            if ($vp) { return $vp.Value }
            return $null
        }
    }
}

function ConvertTo-CanonicalString {
    param($Value)
    if ($null -eq $Value) { return $null }
    $s = ([string]$Value) -replace '\s+', ' '
    $s = $s.Trim().ToLowerInvariant()
    if ($s -eq '') { return $null }
    return $s
}

function Test-EmptyValue {
    param($Value)
    if ($null -eq $Value) { return $true }
    if ($Value -is [string]) { return ($Value -eq '') }
    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($k in $Value.Keys) { if (-not (Test-EmptyValue $Value[$k])) { return $false } }
        return $true
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        foreach ($i in $Value) { if (-not (Test-EmptyValue $i)) { return $false } }
        return $true
    }
    return $false
}

function ConvertTo-CanonicalJson {
    param($Value)
    if ($null -eq $Value) { return 'null' }
    # -InputObject y no tuberia: la tuberia desenrolla un array de un elemento
    # y lo serializaria como objeto suelto.
    return (ConvertTo-Json -InputObject $Value -Depth 40 -Compress)
}

function Format-Short {
    param($Value, [int]$Max = 60)
    $s = if ($null -eq $Value) { '<vacio>' } else { ConvertTo-CanonicalJson $Value }
    if ($s.Length -gt $Max) { return $s.Substring(0, $Max - 3) + '...' }
    return $s
}

function Compare-Fields {
    # Devuelve una fila por campo del fieldSchema con Equal/Method/valores.
    param([pscustomobject]$Schema, $SourceFields, $TargetFields)
    $rows = foreach ($p in $Schema.fields.PSObject.Properties) {
        $name = $p.Name
        $method = if ($p.Value.method) { [string]$p.Value.method } else { 'auto' }
        $a = $null; $b = $null
        if ($SourceFields -and $SourceFields.PSObject.Properties.Name -contains $name) { $a = ConvertTo-FieldValue $SourceFields.$name }
        if ($TargetFields -and $TargetFields.PSObject.Properties.Name -contains $name) { $b = ConvertTo-FieldValue $TargetFields.$name }
        $emptyA = Test-EmptyValue $a
        $emptyB = Test-EmptyValue $b
        $equal = if ($emptyA -and $emptyB) { $true } elseif ($emptyA -ne $emptyB) { $false } else { (ConvertTo-CanonicalJson $a) -eq (ConvertTo-CanonicalJson $b) }
        [pscustomobject]@{ Field = $name; Method = $method; Equal = $equal; BothEmpty = ($emptyA -and $emptyB); Source = $a; Target = $b }
    }
    return @($rows)
}

# -----------------------------------------------------------------------------
# Analisis
# -----------------------------------------------------------------------------
function Start-Analysis {
    param([string]$Endpoint, [string]$AnalyzerId, [byte[]]$Bytes, [string]$Token, [string]$Label)
    $url = "$Endpoint/contentunderstanding/analyzers/$AnalyzerId`:analyzeBinary?api-version=$ApiVersion"
    $r = Invoke-Cu -Method Post -Url $url -Token $Token -Bytes $Bytes -ContentType 'application/pdf'
    if ($r.Status -notin 200, 201, 202) { throw "POST analyzeBinary $Label -> HTTP $($r.Status): $($r.Error)" }
    $op = Get-SingleHeaderValue $r.Headers['Operation-Location']
    if (-not $op) { throw "POST analyzeBinary $Label -> HTTP $($r.Status) sin Operation-Location" }
    return $op
}

function Wait-Analysis {
    param([string]$OperationUrl, [string]$Token, [string]$Label)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    do {
        Start-Sleep -Seconds $PollSeconds
        $op = Invoke-Cu -Method Get -Url $OperationUrl -Token $Token
        if ($op.Status -ne 200) { throw "sondeo de $Label -> HTTP $($op.Status): $($op.Error)" }
        $status = [string]$op.Content.status
        if ((Get-Date) -gt $deadline) { throw "el analisis $Label supera $TimeoutMinutes minutos (ultimo estado: '$status'); sigue en $OperationUrl" }
    } while ($status.ToLowerInvariant() -in $runningStates)
    if ($status.ToLowerInvariant() -notin $successStates) {
        throw "el analisis $Label termino en '$status'. Detalle: $($op.Content.error | ConvertTo-Json -Depth 10 -Compress)"
    }
    return $op.Content
}

function Get-UsageSummary {
    param($Usage)
    if (-not $Usage) { return '' }
    $parts = @()
    foreach ($k in 'documentPagesMinimal', 'documentPagesBasic', 'documentPagesStandard') {
        if ($Usage.$k) { $parts += "$($k -replace 'documentPages', 'pag') $($Usage.$k)" }
    }
    if ($Usage.contextualizationTokens) { $parts += "ctx $($Usage.contextualizationTokens)" }
    if ($Usage.tokens) {
        # usage.tokens es plano: { "gpt-4.1-input": 20694, "gpt-4.1-output": 2334, ... }
        foreach ($m in $Usage.tokens.PSObject.Properties) { $parts += "$($m.Name)=$($m.Value)" }
    }
    return ($parts -join ', ')
}

# -----------------------------------------------------------------------------
# Carga de definiciones
# -----------------------------------------------------------------------------
foreach ($f in $sourceResourcesFile, $targetResourcesFile) { if (-not (Test-Path $f)) { throw "no existe $f" } }
$srcRes = (Get-Content -Raw -Path $sourceResourcesFile -Encoding UTF8 | ConvertFrom-Json).resources.$SourceTarget
$dstRes = (Get-Content -Raw -Path $targetResourcesFile -Encoding UTF8 | ConvertFrom-Json).resources.$Target
if (-not $srcRes) { throw "resources.prod.json no define el alias '$SourceTarget'" }
if (-not $dstRes) { throw "resources.$Environment.json no define el alias '$Target'" }
$source = [pscustomobject]@{ Label = 'origen'; Account = $srcRes.account; Endpoint = $srcRes.endpoint.TrimEnd('/') }
$dest = [pscustomobject]@{ Label = 'destino'; Account = $dstRes.account; Endpoint = $dstRes.endpoint.TrimEnd('/') }
if ($SelfCheck) {
    # Control: la "segunda pasada" es el mismo recurso origen.
    $dest = [pscustomobject]@{ Label = 'origen-2'; Account = $srcRes.account; Endpoint = $srcRes.endpoint.TrimEnd('/') }
} elseif ($source.Endpoint -eq $dest.Endpoint) {
    throw "origen y destino son el mismo recurso ($($source.Endpoint))"
}

$files = @(Get-ChildItem -Path $analyzersDir -Filter '*.json' -File | Where-Object { $_.BaseName -notlike '*@*' } | Sort-Object Name)
# Con "pwsh -File", -Only A,B llega como una sola cadena "A,B": se admiten las dos formas.
if ($Only) { $Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
if ($Only) {
    $unknown = @($Only | Where-Object { $_ -notin $files.BaseName })
    if ($unknown.Count -gt 0) { throw "no existe infra/ai/analyzers/<id>.json para: $($unknown -join ', ')" }
    $files = @($files | Where-Object { $Only -contains $_.BaseName })
}
if ($files.Count -eq 0) { throw "no hay definiciones que procesar en $analyzersDir" }

$modeLabel = if ($isDryRun) { 'DRY-RUN (solo GET; sin descargas ni analisis)' } elseif ($SelfCheck) { 'SELF-CHECK (analyzeBinary dos veces en origen; cuesta dinero)' } else { 'REAL (analyzeBinary en origen y destino; cuesta dinero)' }
Out-Line "== validate-analyzer: $Environment  [$modeLabel] ==" Cyan
Out-Line "  fecha      : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Out-Line "  origen     : $SourceTarget=$($source.Account) ($($source.Endpoint))"
if ($SelfCheck) { Out-Line "  destino    : (self-check) el mismo recurso origen, segunda pasada" } else { Out-Line "  destino    : $Target=$($dest.Account) ($($dest.Endpoint))" }
Out-Line "  analyzers  : $(($files | ForEach-Object { $_.BaseName }) -join ', ')"
Out-Line "  umbral     : acuerdo global >= $($MinFieldsRatio.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture))"
Out-Line "  api        : $ApiVersion"
if ($DumpDir) { New-Item -ItemType Directory -Force -Path $DumpDir | Out-Null; Out-Line "  volcado    : $DumpDir" }
Out-Line ''

# Resolver definicion + manifiesto + muestra de todos antes de tocar nada.
$items = foreach ($file in $files) {
    $id = $file.BaseName
    $export = Get-Content -Raw -Path $file.FullName -Encoding UTF8 | ConvertFrom-Json
    if ($export.analyzerId -and $export.analyzerId -ne $id) { throw "$($file.Name) declara analyzerId '$($export.analyzerId)', no '$id'" }
    if (-not $export.fieldSchema -or -not $export.fieldSchema.fields) { throw "$($file.Name) no tiene fieldSchema.fields; no hay nada que comparar" }
    $res = Resolve-Manifest -AnalyzerId $id
    Out-Line "  $id" White
    Out-Line "      dataset  : $($res.File.Name) -> $($res.Manifest.containerUrl) / $($res.Manifest.prefix)"
    $sel = Resolve-Selection -AnalyzerId $id -Resolved $res
    $fieldCount = @($export.fieldSchema.fields.PSObject.Properties).Count
    $extractCount = @($export.fieldSchema.fields.PSObject.Properties | Where-Object { $_.Value.method -eq 'extract' }).Count
    Out-Line "      campos   : $fieldCount ($extractCount extract)"
    Out-Line "      muestra  : $(@($sel.documents).Count) PDF -> $(($sel.documents | ForEach-Object { $_.name }) -join ', ')"
    [pscustomobject]@{ Id = $id; Schema = $export.fieldSchema; Manifest = $res; Selection = $sel }
}
Out-Line ''

$cuToken = Get-Token -Resource 'https://cognitiveservices.azure.com'
$tokenIssuedAt = Get-Date

# Comprobar que el analyzer existe (y esta ready) en origen y destino.
Out-Line "== analyzers en origen y destino ==" Cyan
$sides = if ($SelfCheck) { @($source) } else { @($source, $dest) }
if ($TargetSuffix) { Out-Line "  sufijo     : en destino se valida <id>$TargetSuffix" }
foreach ($item in $items) {
    foreach ($side in $sides) {
        $sideId = if ($side.Label -eq 'destino') { "$($item.Id)$TargetSuffix" } else { $item.Id }
        $g = Invoke-Cu -Method Get -Url "$($side.Endpoint)/contentunderstanding/analyzers/$sideId`?api-version=$ApiVersion" -Token $cuToken
        if ($g.Status -in 401, 403) { throw "sin acceso al data plane de $($side.Account) (HTTP $($g.Status)): falta el rol Cognitive Services User" }
        if ($g.Status -eq 404) { throw "$($item.Id) no existe en $($side.Label) $($side.Account); en destino, lanza build-analyzers.ps1 -Environment $Environment" }
        if ($g.Status -ne 200) { throw "GET $($item.Id) en $($side.Account) -> HTTP $($g.Status): $($g.Error)" }
        $st = [string]$g.Content.status
        $color = if ($st -eq 'ready') { 'Green' } else { 'Yellow' }
        Out-Line ("  {0,-18} {1,-8} {2,-32} status {3}" -f $sideId, $side.Label, $side.Account, $st) $color
        if ($st -ne 'ready') { throw "$($item.Id) en $($side.Label) $($side.Account) esta en status '$st', no 'ready'" }
    }
}
Out-Line ''

if ($isDryRun) {
    $calls = ($items | ForEach-Object { @($_.Selection.documents).Count } | Measure-Object -Sum).Sum * 2
    Out-Line "[dry-run] no se descarga ni analiza nada. La pasada real haria $calls llamadas :analyzeBinary ($($calls / 2) en $($source.Account), $($calls / 2) en $($dest.Account))." DarkGray
    Save-Report
    return
}

# -----------------------------------------------------------------------------
# Pasada real
# -----------------------------------------------------------------------------
$storageToken = Get-Token -Resource 'https://storage.azure.com/'
$summary = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($item in $items) {
    $id = $item.Id
    Out-Line "== $id ==" Cyan
    $rowsAll = [System.Collections.Generic.List[object]]::new()
    $docIndex = 0
    $mdSameCount = 0
    foreach ($doc in $item.Selection.documents) {
        $docIndex++
        if (((Get-Date) - $tokenIssuedAt).TotalMinutes -gt 40) {
            $cuToken = Get-Token -Resource 'https://cognitiveservices.azure.com'
            $storageToken = Get-Token -Resource 'https://storage.azure.com/'
            $tokenIssuedAt = Get-Date
        }
        $started = Get-Date
        $blobUrl = Get-BlobUrl -ContainerUrl $item.Manifest.Manifest.containerUrl -RelativePath "$($item.Manifest.Manifest.prefix)/$($doc.name)"
        $bytes = Get-BlobBytes -BlobUrl $blobUrl -Token $storageToken
        if ($doc.md5) {
            $md5 = [Convert]::ToBase64String([Security.Cryptography.MD5]::Create().ComputeHash($bytes))
            if ($md5 -ne $doc.md5) { throw "MD5 de $($doc.name) no coincide con el manifiesto (esperado $($doc.md5), obtenido $md5)" }
        }
        Out-Line ("  [{0}/{1}] {2} ({3:N0} KB)" -f $docIndex, @($item.Selection.documents).Count, $doc.name, ($bytes.Length / 1KB)) White

        # Lanzar en los dos recursos y sondear despues: ahorra la mitad del tiempo de espera.
        $opSrc = Start-Analysis -Endpoint $source.Endpoint -AnalyzerId $id -Bytes $bytes -Token $cuToken -Label "$id/$($doc.name) en $($source.Account)"
        $dstId = if ($dest.Label -eq 'destino') { "$id$TargetSuffix" } else { $id }
        $opDst = Start-Analysis -Endpoint $dest.Endpoint -AnalyzerId $dstId -Bytes $bytes -Token $cuToken -Label "$dstId/$($doc.name) en $($dest.Account)"
        $resSrc = Wait-Analysis -OperationUrl $opSrc -Token $cuToken -Label "$id/$($doc.name) en $($source.Account)"
        $resDst = Wait-Analysis -OperationUrl $opDst -Token $cuToken -Label "$dstId/$($doc.name) en $($dest.Account)"
        if ($DumpDir) {
            Write-JsonFile -Path (Join-Path $DumpDir $id "$($doc.name).origen.json") -Object $resSrc
            Write-JsonFile -Path (Join-Path $DumpDir $id "$($doc.name).$($dest.Label).json") -Object $resDst
        }
        $fieldsSrc = $null; $fieldsDst = $null; $mdSrc = ''; $mdDst = ''
        if ($resSrc.result.contents) { $fieldsSrc = @($resSrc.result.contents)[0].fields; $mdSrc = [string]@($resSrc.result.contents)[0].markdown }
        if ($resDst.result.contents) { $fieldsDst = @($resDst.result.contents)[0].fields; $mdDst = [string]@($resDst.result.contents)[0].markdown }
        $mdSame = ($mdSrc -eq $mdDst) -and $mdSrc.Length -gt 0
        if ($mdSame) { $mdSameCount++ }
        $mdLabel = if ($mdSame) { "identico ($($mdSrc.Length) chars)" } else { "DIFIERE ($($mdSrc.Length) vs $($mdDst.Length) chars)" }
        foreach ($w in @($resSrc.result.warnings) + @($resDst.result.warnings)) { if ($w) { Out-Line "      warning: $($w | ConvertTo-Json -Depth 5 -Compress)" DarkYellow } }

        $rows = Compare-Fields -Schema $item.Schema -SourceFields $fieldsSrc -TargetFields $fieldsDst
        foreach ($r in $rows) { $rowsAll.Add([pscustomobject]@{ Doc = $doc.name; Field = $r.Field; Method = $r.Method; Equal = $r.Equal; BothEmpty = $r.BothEmpty; Source = $r.Source; Target = $r.Target }) }
        $eq = @($rows | Where-Object Equal).Count
        $secs = [int]((Get-Date) - $started).TotalSeconds
        Out-Line ("      markdown {0}" -f $mdLabel) $(if ($mdSame) { 'Green' } else { 'Red' })
        Out-Line ("      iguales {0}/{1} en {2} s" -f $eq, $rows.Count, $secs) $(if ($eq -eq $rows.Count) { 'Green' } else { 'Yellow' })
        $uSrc = Get-UsageSummary $resSrc.usage
        $uDst = Get-UsageSummary $resDst.usage
        if ($uSrc) { Out-Line "      uso origen : $uSrc" DarkGray }
        if ($uDst) { Out-Line "      uso destino: $uDst" DarkGray }
        foreach ($r in ($rows | Where-Object { -not $_.Equal })) {
            Out-Line ("      difiere {0} [{1}]: origen {2} | destino {3}" -f $r.Field, $r.Method, (Format-Short $r.Source), (Format-Short $r.Target)) Yellow
        }
    }

    $total = $rowsAll.Count
    $equal = @($rowsAll | Where-Object Equal).Count
    $bothEmpty = @($rowsAll | Where-Object BothEmpty).Count
    $extractRows = @($rowsAll | Where-Object { $_.Method -eq 'extract' })
    $extractEqual = @($extractRows | Where-Object Equal).Count
    $ratio = if ($total) { $equal / $total } else { 0 }
    $extractRatio = if ($extractRows.Count) { $extractEqual / $extractRows.Count } else { [double]::NaN }
    $ok = $ratio -ge $MinFieldsRatio
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $docCount = @($item.Selection.documents).Count
    Out-Line ("  markdown igual  : {0}/{1} documentos" -f $mdSameCount, $docCount) $(if ($mdSameCount -eq $docCount) { 'Green' } else { 'Red' })
    Out-Line ("  acuerdo global  : {0} ({1}/{2}; {3} vacios en ambos)" -f $ratio.ToString('P1', $inv), $equal, $total, $bothEmpty) $(if ($ok) { 'Green' } else { 'Red' })
    if ($extractRows.Count) { Out-Line ("  acuerdo extract : {0} ({1}/{2})" -f $extractRatio.ToString('P1', $inv), $extractEqual, $extractRows.Count) }
    $byField = $rowsAll | Where-Object { -not $_.Equal } | Group-Object Field | Sort-Object Count -Descending | Select-Object -First 8
    if ($byField) { Out-Line "  campos con mas diferencias: $(($byField | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ', ')" }
    if (-not $ok) { $failures.Add("$id : acuerdo $($ratio.ToString('P1', $inv)) < umbral $($MinFieldsRatio.ToString('P1', $inv))") }
    $summary.Add([pscustomobject]@{
            Analyzer = $id
            Docs     = @($item.Selection.documents).Count
            Campos   = $total
            Iguales  = $equal
            Markdown = "$mdSameCount/$docCount"
            Global   = $ratio.ToString('P1', $inv)
            Extract  = if ($extractRows.Count) { $extractRatio.ToString('P1', $inv) } else { '-' }
            Resultado = if ($ok) { 'OK' } else { 'FALLA' }
        })
    Out-Line ''
}

Out-Line "== resumen ==" Cyan
foreach ($l in ($summary | Format-Table -AutoSize | Out-String -Width 200).TrimEnd() -split "`n") { Out-Line $l.TrimEnd() }
Out-Line ''
Save-Report
if ($failures.Count -gt 0) {
    throw "validacion fallida en $($failures.Count) analyzer(s): $($failures -join '; ')"
}
