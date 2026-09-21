#Requires -Version 7.0
<#
.SYNOPSIS
    Copia un prefijo de dataset de etiquetado de Content Understanding desde el
    storage de PRO al storage del entorno destino, y genera un manifiesto por
    analyzer en infra/ai/datasets.
.DESCRIPTION
    Copia por cliente (descarga cada blob a un temporal y lo sube al destino),
    no servidor a servidor: az CLI de data-plane (az storage) y az rest con
    cuerpo fallan en este entorno por el proxy TLS y por perdida de comillas al
    invocar az.cmd. Todo el trafico HTTP se hace con Invoke-WebRequest
    -SkipCertificateCheck y un token de "az account get-access-token --resource
    https://storage.azure.com/", con la cabecera x-ms-version 2021-08-06.

    Trampa verificada: la respuesta XML del List Blobs llega con BOM y un
    caracter previo; antes de castear a [xml] hay que hacer
    .Content.TrimStart([char]0xFEFF).TrimStart('?'). La paginacion usa
    NextMarker con maxresults=5000.

    Por cada blob se calcula el MD5 de los bytes descargados (no se confia en
    el Content-MD5 que pueda traer el origen) y se sube con
    x-ms-blob-content-md5; tras subir se relee la propiedad Content-MD5 del
    destino (HEAD) y se compara. Reintenta hasta 3 veces por blob con espera
    creciente. Es idempotente: si el blob destino ya existe con el mismo MD5,
    se salta (usa -Force para sobrescribir de todas formas).

    El prefijo destino es siempre "labeling/<AnalyzerId>@<Version>/" mas la
    ruta relativa del blob origen respecto a su prefijo. Varios analyzers
    pueden compartir el mismo prefijo origen (por ejemplo CU_NS_1.4_3,
    CU_NS_1.5_0 y CU_NS_1.6_0_GGAA): la copia y el manifiesto son siempre por
    analyzer, cada uno con su propio prefijo destino, aunque el origen se lea
    varias veces.
.PARAMETER AnalyzerId
    Id del analyzer de Content Understanding (infra/ai/analyzers/<AnalyzerId>.json).
    Fija tambien el nombre del prefijo destino y del fichero de manifiesto.
.PARAMETER Environment
    Entorno destino: "dev" o "pre". Fija la cuenta de storage destino
    (dev -> srbstgdevdocai, pre -> srbstgpredocai).
.PARAMETER TargetContainer
    Contenedor destino en la cuenta de storage del entorno. Por defecto "documentai".
.PARAMETER Version
    Version del dataset. Por defecto 1. Forma el prefijo destino
    "labeling/<AnalyzerId>@<Version>/" y el nombre del manifiesto.
.PARAMETER SourceContainerUrl
    URL del contenedor origen (https://<cuenta>.blob.core.windows.net/<contenedor>).
    Si se omite junto con -SourcePrefix, se lee de
    infra/ai/analyzers/<AnalyzerId>.json -> knowledgeSources[0].containerUrl,
    resuelto desde $PSScriptRoot/../../infra/ai.
.PARAMETER SourcePrefix
    Prefijo origen dentro del contenedor (por ejemplo
    labelingProjects/<guid>/train). Si se omite junto con -SourceContainerUrl,
    se lee de infra/ai/analyzers/<AnalyzerId>.json -> knowledgeSources[0].prefix.
.PARAMETER ManifestDir
    Carpeta donde se escribe <AnalyzerId>@<Version>.manifest.json. Por defecto
    infra/ai/datasets (relativo al directorio desde el que se invoca el script,
    igual que -OutDir en export-analyzer-definitions.ps1).
.PARAMETER DryRun
    No descarga ni sube nada: solo lista el origen, calcula fileCount y
    totalBytes a partir del listado, y escribe el manifiesto con
    "status": "dry-run" (los ficheros quedan sin md5, porque calcularlo exige
    descargar). Alias funcional: -WhatIf hace lo mismo.
.PARAMETER WhatIf
    Alias de -DryRun.
.PARAMETER Force
    Sobrescribe un blob destino aunque ya exista con el mismo MD5 (por defecto
    se salta).
.EXAMPLE
    pwsh scripts/ai/copy-labeling-dataset.ps1 -AnalyzerId CERA16_v1 -Environment dev -DryRun
.EXAMPLE
    pwsh scripts/ai/copy-labeling-dataset.ps1 -AnalyzerId CU_NS_1.4_3 -Environment dev
#>
param(
    [Parameter(Mandatory)][string]$AnalyzerId,
    [Parameter(Mandatory)][ValidateSet('dev', 'pre')][string]$Environment,
    [string]$TargetContainer = "documentai",
    [int]$Version = 1,
    [string]$SourceContainerUrl,
    [string]$SourcePrefix,
    [string]$ManifestDir = "infra/ai/datasets",
    [switch]$DryRun,
    [switch]$WhatIf,
    [switch]$Force
)
$ErrorActionPreference = "Stop"

# El data-plane en este entorno pasa por un proxy TLS; sin esto tanto az CLI
# como Invoke-WebRequest fallan la verificacion del certificado.
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"

$isDryRun = [bool]($DryRun -or $WhatIf)
$apiVersion = "2021-08-06"

$targetAccounts = @{ dev = "srbstgdevdocai"; pre = "srbstgpredocai" }
$TargetAccount = $targetAccounts[$Environment]

function Write-JsonFile {
    param([string]$Path, [object]$Object, [int]$Depth = 10)
    # .NET resuelve una ruta relativa contra el cwd del proceso, no contra el
    # de PowerShell: sin esto, escribir con una ubicacion relativa puede acabar
    # en la carpeta equivocada.
    $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    $json = $Object | ConvertTo-Json -Depth $Depth
    # LF sin BOM y con newline final, igual que el resto de infra/ai.
    $json = $json -replace "`r`n", "`n"
    [IO.File]::WriteAllText($Path, ($json + "`n"), [Text.UTF8Encoding]::new($false))
}

function Get-StorageToken {
    $tokenRaw = az account get-access-token --resource https://storage.azure.com/ --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $tokenRaw) {
        throw "no se pudo obtener el token de acceso para storage (az account get-access-token, exit $LASTEXITCODE)"
    }
    return $tokenRaw.Trim()
}

function Get-SingleHeaderValue {
    param($HeaderValue)
    if ($null -eq $HeaderValue) { return $null }
    return ($HeaderValue | Select-Object -First 1)
}

function Get-BlobListPage {
    param([string]$ContainerUrl, [string]$Prefix, [string]$Token, [string]$Marker)
    $prefixEncoded = [Uri]::EscapeDataString($Prefix)
    $uri = "$ContainerUrl`?restype=container&comp=list&prefix=$prefixEncoded&maxresults=5000"
    if ($Marker) { $uri += "&marker=" + [Uri]::EscapeDataString($Marker) }
    try {
        $resp = Invoke-WebRequest -Uri $uri -Method Get -Headers @{
            Authorization  = "Bearer $Token"
            'x-ms-version' = $apiVersion
        } -UseBasicParsing -SkipCertificateCheck
    } catch {
        $status = "sin respuesta"
        $body = ""
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $body = $_.ErrorDetails.Message }
        elseif ($_.Exception.Message) { $body = $_.Exception.Message }
        throw "GET $uri -> $status`: $body"
    }
    # La respuesta XML llega con BOM y un caracter previo; hay que limpiarlo
    # antes de castear a [xml] (verificado contra este storage).
    $text = $resp.Content.TrimStart([char]0xFEFF).TrimStart('?')
    return [xml]$text
}

function Get-AllSourceBlobs {
    param([string]$ContainerUrl, [string]$Prefix, [string]$Token)
    $blobs = @()
    $marker = $null
    do {
        $xml = Get-BlobListPage -ContainerUrl $ContainerUrl -Prefix $Prefix -Token $Token -Marker $marker
        foreach ($b in $xml.EnumerationResults.Blobs.Blob) {
            $blobs += [pscustomobject]@{
                Name        = $b.Name
                Size        = [int64]$b.Properties.'Content-Length'
                ContentType = $b.Properties.'Content-Type'
            }
        }
        $marker = $xml.EnumerationResults.NextMarker
    } while ($marker)
    return $blobs
}

function Get-BlobUrl {
    param([string]$ContainerUrl, [string]$RelativePath)
    $segments = $RelativePath -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }
    return "$ContainerUrl/" + ($segments -join '/')
}

function Get-DestBlobProperties {
    param([string]$BlobUrl, [string]$Token)
    try {
        $resp = Invoke-WebRequest -Uri $BlobUrl -Method Head -Headers @{
            Authorization  = "Bearer $Token"
            'x-ms-version' = $apiVersion
        } -UseBasicParsing -SkipCertificateCheck
        return @{ Exists = $true; Md5 = (Get-SingleHeaderValue $resp.Headers['Content-MD5']) }
    } catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -eq 404) { return @{ Exists = $false; Md5 = $null } }
        throw "HEAD $BlobUrl -> $status`: $($_.Exception.Message)"
    }
}

function Copy-OneBlob {
    param([string]$SourceUrl, [string]$DestUrl, [string]$ContentType, [string]$Token, [bool]$Force)
    $existing = Get-DestBlobProperties -BlobUrl $DestUrl -Token $Token
    $tempFile = [IO.Path]::GetTempFileName()
    try {
        $lastError = $null
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                Invoke-WebRequest -Uri $SourceUrl -Method Get -Headers @{
                    Authorization  = "Bearer $Token"
                    'x-ms-version' = $apiVersion
                } -OutFile $tempFile -UseBasicParsing -SkipCertificateCheck | Out-Null

                $bytes = [IO.File]::ReadAllBytes($tempFile)
                $md5Bytes = [Security.Cryptography.MD5]::Create().ComputeHash($bytes)
                $md5Base64 = [Convert]::ToBase64String($md5Bytes)

                if (-not $Force -and $existing.Exists -and $existing.Md5 -eq $md5Base64) {
                    return [pscustomobject]@{ Skipped = $true; Md5 = $md5Base64; Size = $bytes.Length }
                }

                Invoke-WebRequest -Uri $DestUrl -Method Put -Headers @{
                    Authorization           = "Bearer $Token"
                    'x-ms-version'          = $apiVersion
                    'x-ms-blob-type'        = 'BlockBlob'
                    'x-ms-blob-content-md5' = $md5Base64
                } -ContentType $ContentType -InFile $tempFile -UseBasicParsing -SkipCertificateCheck | Out-Null

                $verify = Get-DestBlobProperties -BlobUrl $DestUrl -Token $Token
                if (-not $verify.Exists -or $verify.Md5 -ne $md5Base64) {
                    throw "verificacion MD5 fallida tras subir $DestUrl (esperado $md5Base64, obtenido $($verify.Md5))"
                }
                return [pscustomobject]@{ Skipped = $false; Md5 = $md5Base64; Size = $bytes.Length }
            } catch {
                $lastError = $_
                if ($attempt -lt 3) { Start-Sleep -Seconds (2 * $attempt) }
            }
        }
        throw "fallo copiando $SourceUrl -> $DestUrl tras 3 intentos: $lastError"
    } finally {
        Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    }
}

# --- Resolver origen ---
if (-not $SourceContainerUrl -or -not $SourcePrefix) {
    $analyzerJsonPath = Join-Path $PSScriptRoot "../../infra/ai/analyzers/$AnalyzerId.json"
    $analyzerJsonPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($analyzerJsonPath)
    if (-not (Test-Path $analyzerJsonPath)) {
        throw "no se especifico -SourceContainerUrl/-SourcePrefix y no existe $analyzerJsonPath"
    }
    $analyzerDef = Get-Content -Raw -Path $analyzerJsonPath | ConvertFrom-Json
    if (-not $analyzerDef.knowledgeSources -or $analyzerDef.knowledgeSources.Count -eq 0) {
        throw "$analyzerJsonPath no tiene knowledgeSources"
    }
    if (-not $SourceContainerUrl) { $SourceContainerUrl = $analyzerDef.knowledgeSources[0].containerUrl }
    if (-not $SourcePrefix) { $SourcePrefix = $analyzerDef.knowledgeSources[0].prefix }
}
$SourceContainerUrl = $SourceContainerUrl.TrimEnd('/')
$SourcePrefix = $SourcePrefix.Trim('/')

$targetContainerUrl = "https://$TargetAccount.blob.core.windows.net/$TargetContainer"
$targetPrefix = "labeling/$AnalyzerId@$Version"

Write-Host "origen: $SourceContainerUrl/$SourcePrefix"
Write-Host "destino: $targetContainerUrl/$targetPrefix ($Environment)"
if ($isDryRun) { Write-Host "modo: dry-run (solo lectura, no copia nada)" }

$token = Get-StorageToken
$sourceBlobs = Get-AllSourceBlobs -ContainerUrl $SourceContainerUrl -Prefix $SourcePrefix -Token $token

if ($sourceBlobs.Count -eq 0) {
    Write-Warning "no se encontraron blobs bajo $SourceContainerUrl/$SourcePrefix"
}

$files = @()
$copiedCount = 0
$skippedCount = 0
$totalBytes = 0
$expectedBytes = ($sourceBlobs | Measure-Object -Property Size -Sum).Sum
$doneBytes = 0
$index = 0
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
Write-Host ("{0} blobs en origen, {1:N1} MB" -f $sourceBlobs.Count, ($expectedBytes / 1MB))

foreach ($blob in $sourceBlobs) {
    $relativeName = $blob.Name.Substring($SourcePrefix.Length).TrimStart('/')
    $totalBytes += $blob.Size
    $index++
    $pct = [int](100 * ($index - 1) / [Math]::Max(1, $sourceBlobs.Count))
    Write-Progress -Activity "Copiando $AnalyzerId a $Environment" `
        -Status ("{0}/{1} blobs, {2:N1}/{3:N1} MB, {4:mm\:ss} transcurrido" -f ($index - 1), $sourceBlobs.Count, ($doneBytes / 1MB), ($expectedBytes / 1MB), $stopwatch.Elapsed) `
        -CurrentOperation $relativeName -PercentComplete $pct

    if ($isDryRun) {
        $files += [pscustomobject]@{
            name        = $relativeName
            size        = $blob.Size
            md5         = $null
            contentType = $blob.ContentType
        }
        continue
    }

    $sourceUrl = Get-BlobUrl -ContainerUrl $SourceContainerUrl -RelativePath $blob.Name
    $destUrl = Get-BlobUrl -ContainerUrl $targetContainerUrl -RelativePath "$targetPrefix/$relativeName"
    $contentType = if ($blob.ContentType) { $blob.ContentType } else { "application/octet-stream" }

    $result = Copy-OneBlob -SourceUrl $sourceUrl -DestUrl $destUrl -ContentType $contentType -Token $token -Force $Force.IsPresent
    if ($result.Skipped) { $skippedCount++ } else { $copiedCount++ }

    $files += [pscustomobject]@{
        name        = $relativeName
        size        = $result.Size
        md5         = $result.Md5
        contentType = $contentType
    }
    $doneBytes += $result.Size
    $verb = if ($result.Skipped) { "saltado" } else { "copiado" }
    Write-Host ("[{0,4}/{1}] {2} {3} ({4:N0} KB) - {5:N1} MB, {6:mm\:ss}" -f $index, $sourceBlobs.Count, $verb, $relativeName, ($result.Size / 1KB), ($doneBytes / 1MB), $stopwatch.Elapsed)
}
Write-Progress -Activity "Copiando $AnalyzerId a $Environment" -Completed

$files = @($files | Sort-Object name)

$manifest = [ordered]@{
    analyzerId    = $AnalyzerId
    version       = $Version
    environment   = $Environment
    containerUrl  = $targetContainerUrl
    prefix        = $targetPrefix
    source        = [ordered]@{
        containerUrl = $SourceContainerUrl
        prefix       = $SourcePrefix
    }
    cutoffDateUtc = (Get-Date).ToUniversalTime().ToString("o")
    fileCount     = $files.Count
    totalBytes    = $totalBytes
    status        = if ($isDryRun) { "dry-run" } else { "copied" }
    files         = $files
}

New-Item -ItemType Directory -Force -Path $ManifestDir | Out-Null
$manifestPath = Join-Path $ManifestDir "$AnalyzerId@$Version.manifest.json"
Write-JsonFile -Path $manifestPath -Object $manifest -Depth 10

Write-Host ""
Write-Host "resumen: $($sourceBlobs.Count) blobs listados, $copiedCount copiados, $skippedCount saltados, $totalBytes bytes"
Write-Host "manifiesto: $manifestPath"
