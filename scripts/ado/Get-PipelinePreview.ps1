<#
.SYNOPSIS
  Obtiene el YAML expandido de un pipeline de Azure DevOps con la API de preview.

.DESCRIPTION
  Llama a POST https://dev.azure.com/sareb/AI%20DocClassExt/_apis/pipelines/<id>/preview
  (api-version 7.1-preview.1) con previewRun = true y escribe finalYaml en OutFile
  (UTF-8 sin BOM). Sirve para comparar el YAML final antes y despues de refactorizar
  un pipeline sobre templates. NO ejecuta nada en Azure DevOps.

  La rama debe estar subida al remoto: la API resuelve los templates desde alli.

  Token: por defecto `az account get-access-token` (recurso de Azure DevOps). El
  parametro opcional -AccessToken permite inyectarlo (tests, o System.AccessToken en
  un pipeline). El token nunca se imprime, tampoco con -WhatIf.

  Con -WhatIf imprime la URL y el cuerpo JSON, sale con 0 y no llama ni crea fichero.

  Codigos de salida: 0 exito; 2 error de la API, respuesta sin finalYaml o fallo al
  obtener el token.

.EXAMPLE
  pwsh -NoProfile -File scripts/ado/Get-PipelinePreview.ps1 -PipelineId 807 `
    -Branch feature/101570-pipeline-release-hito1 `
    -TemplateParameters @{ targetEnvironment = 'dev' } `
    -OutFile docs/auxiliares/temps/2026-11-27/preview-antes/807-dev.yml
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][int]$PipelineId,
    [Parameter(Mandatory)][string]$Branch,
    [hashtable]$TemplateParameters = @{},
    [Parameter(Mandatory)][string]$OutFile,
    [string]$AccessToken
)

$ErrorActionPreference = 'Stop'
$adoResource = '499b84ac-1321-427f-aa17-267ca6975798'
$uri = "https://dev.azure.com/sareb/AI%20DocClassExt/_apis/pipelines/$PipelineId/preview?api-version=7.1-preview.1"

# Los valores de los parametros se envian como cadenas ('true', 'dev').
$params = [ordered]@{}
foreach ($k in $TemplateParameters.Keys) { $params[$k] = [string]$TemplateParameters[$k] }

$body = [ordered]@{
    previewRun         = $true
    templateParameters = $params
    resources          = [ordered]@{
        repositories = [ordered]@{
            self = [ordered]@{ refName = "refs/heads/$Branch" }
        }
    }
} | ConvertTo-Json -Depth 5

function Write-Fail([string]$Message) {
    Write-Error "Get-PipelinePreview: $Message" -ErrorAction Continue
    exit 2
}

function Get-AdoAccessToken {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $t = az account get-access-token --resource $adoResource --query accessToken -o tsv 2>$null
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    if ($code -ne 0 -or [string]::IsNullOrWhiteSpace($t)) { return $null }
    return ($t | Out-String).Trim()
}

if ($WhatIfPreference) {
    Write-Output "POST $uri"
    Write-Output $body
    exit 0
}

if (-not $AccessToken) { $AccessToken = Get-AdoAccessToken }
if (-not $AccessToken) { Write-Fail 'no se pudo obtener el token (az account get-access-token); ejecuta az login con una cuenta con acceso a la organizacion.' }

$headers = @{ Authorization = "Bearer $AccessToken"; 'Content-Type' = 'application/json' }

try {
    $resp = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body -ContentType 'application/json'
} catch {
    $msg = $_.Exception.Message
    $details = $_.ErrorDetails.Message
    foreach ($candidate in @($details, $msg)) {
        if ($candidate -and $candidate.TrimStart().StartsWith('{')) {
            try {
                $j = $candidate | ConvertFrom-Json
                if ($j.message) { $msg = [string]$j.message; break }
            } catch { }
        }
    }
    $status = $null
    if ($_.Exception.Response) { try { $status = [int]$_.Exception.Response.StatusCode } catch { } }
    if ($status -eq 403) { $msg = "$msg (403: posible falta de permiso de preview/ejecucion sobre el pipeline $PipelineId)" }
    Write-Fail "la API respondio con error: $msg"
}

if (-not $resp -or -not $resp.finalYaml) { Write-Fail "la respuesta no trae finalYaml (pipeline $PipelineId, rama $Branch)." }

$full = [System.IO.Path]::GetFullPath($OutFile, (Get-Location).Path)
$dir = Split-Path -Parent $full
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
[System.IO.File]::WriteAllText($full, [string]$resp.finalYaml, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "YAML expandido escrito en $full"
exit 0
