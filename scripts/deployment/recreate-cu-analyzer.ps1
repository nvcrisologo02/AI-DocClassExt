<#
.SYNOPSIS
    Recrea (RECONSTRUYE / REENTRENA) un analyzer de Azure AI Content Understanding en
    OTRO recurso a partir de su DEFINICION, sin usar la Copy API cross-resource.

    Este es el metodo que funciona de verdad en este entorno para replicar de
    Sweden Central -> West Europe: la Copy API cross-resource (grantCopyAuthorization
    + :copy) esta BLOQUEADA aqui (el grant devuelve un cuerpo sin 'source' y el copy
    responde "has not granted the necessary permissions" incluso con la identidad
    administrada del destino con rol 'Cognitive Services User' sobre el origen).

    En su lugar, este script:
      1. Lee la definicion del analyzer del ORIGEN (GET) -- o de un export JSON local.
      2. Quita los campos de solo-lectura (status, createdAt, lastModifiedAt, warnings,
         supportedModels).
      3. Hace PUT (create-or-replace) en el DESTINO -> el servicio RECONSTRUYE el
         analyzer, REENTRENANDO desde 'knowledgeSources' (datos etiquetados en blob).
      4. Poll de la operacion hasta 'ready' / 'failed'.

    DIFERENCIA CLAVE FRENTE A LA COPY API:
      - La Copy API replica el ESTADO ENTRENADO (snapshot) sin reentrenar.
      - Este script REENTRENA desde los datos etiquetados: el resultado es
        funcionalmente equivalente, pero NO es un snapshot bit a bit. Requiere que el
        DESTINO pueda leer el blob de 'knowledgeSources' con SU identidad administrada
        (rol 'Storage Blob Data Reader' sobre la cuenta de storage del etiquetado).

.PARAMETER SourceAnalyzerId
    ID del analyzer origen (p. ej. CU_NS_1.6_0_GGAA).

.PARAMETER TargetAnalyzerId
    ID del analyzer destino. Por defecto = SourceAnalyzerId.

.PARAMETER FromExport
    Ruta a un JSON con la definicion del analyzer (p. ej. el export de
    scripts/arm/). Si se indica, NO se hace GET al origen.

.PARAMETER SyncDefaults
    Antes de recrear, alinea los defaults de modelo del destino con los del origen,
    mapeando cada alias al deployment del destino que sirve el MISMO modelo.

.PARAMETER Force
    Sobrescribe el analyzer destino si ya existe (PUT con allowReplace=true).

.PARAMETER SkipPreflight
    Omite validaciones previas. Solo depuracion.

.EXAMPLE
    ./recreate-cu-analyzer.ps1 -SourceAnalyzerId CU_NS_1.6_0_GGAA -ResourceGroup SRBRGDOCSAIPROD -SyncDefaults

.EXAMPLE
    # Desde un export local, sobrescribiendo el destino si existe
    ./recreate-cu-analyzer.ps1 -FromExport scripts/arm/analyzer-CU_NS_1.5_0-export.json `
        -SourceAnalyzerId CU_NS_1.5_0 -ResourceGroup SRBRGDOCSAIPROD -SyncDefaults -Force

.NOTES
    Ver docs/guias/GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING.md (replica a West Europe).
#>
param(
    [Parameter(Mandatory = $true)][string]$SourceAnalyzerId,
    [string]$TargetAnalyzerId,
    [string]$FromExport,

    # --- Recurso ORIGEN (Sweden Central, CU primario) ---
    [string]$SourceResourceName = "upe48-mm2avmdm-swedencentral",
    [string]$SourceEndpoint     = "https://upe48-mm2avmdm-swedencentral.services.ai.azure.com",
    [string]$SourceRegion       = "swedencentral",

    # --- Recurso DESTINO (West Europe, CU secundario) ---
    [string]$TargetResourceName = "srbaisrv-westeurope",
    [string]$TargetEndpoint     = "https://srbaisrv-westeurope.services.ai.azure.com/",
    [string]$TargetRegion       = "westeurope",

    # --- Identidad ARM (para -SyncDefaults) ---
    [string]$SubscriptionId = "647c7246-54bc-4d31-b909-431cacf03272",
    [Parameter(Mandatory = $true)][string]$ResourceGroup,
    [string]$SourceResourceGroup,
    [string]$TargetResourceGroup,

    [switch]$SyncDefaults,
    [switch]$Force,

    # --- Autenticacion (si no se pasan keys, se usa el token de 'az login') ---
    [string]$SourceKey,
    [string]$TargetKey,

    [switch]$SkipPreflight,
    [string]$ApiVersion = "2025-11-01"
)

$ErrorActionPreference = "Stop"

if (-not $TargetAnalyzerId)     { $TargetAnalyzerId = $SourceAnalyzerId }
if (-not $SourceResourceGroup)  { $SourceResourceGroup = $ResourceGroup }
if (-not $TargetResourceGroup)  { $TargetResourceGroup = $ResourceGroup }

$SourceEndpoint = $SourceEndpoint.TrimEnd('/')
$TargetEndpoint = $TargetEndpoint.TrimEnd('/')

# Campos de la definicion que son de SOLO LECTURA: no se envian en el PUT.
$ReadOnlyFields = @('analyzerId', 'status', 'createdAt', 'lastModifiedAt', 'warnings', 'supportedModels')

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
$script:CachedToken = $null

function Get-AuthHeaders {
    param([string]$Key)
    if ($Key) { return @{ "Ocp-Apim-Subscription-Key" = $Key } }
    if (-not $script:CachedToken) {
        $script:CachedToken = (az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv)
        if (-not $script:CachedToken) { throw "No se pudo obtener token de Entra ID. Ejecuta 'az login' o pasa -SourceKey/-TargetKey." }
    }
    return @{ "Authorization" = "Bearer $($script:CachedToken)" }
}

function Invoke-CuApi {
    param([string]$Uri, [string]$Method = 'Get', [string]$Key, [string]$Body)
    $headers = Get-AuthHeaders -Key $Key
    if ($Body) { $headers["Content-Type"] = "application/json" }
    $params = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = 'Stop' }
    if ($Body) { $params["Body"] = $Body }
    try {
        $resp    = Invoke-WebRequest @params
        $content = if ($resp.Content) { $resp.Content | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Ok = $true; Status = [int]$resp.StatusCode; Content = $content; Raw = $resp }
    }
    catch {
        $status = 0
        if ($_.Exception.PSObject.Properties.Name -contains 'Response' -and $_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        $err = ""
        try { $err = $_.ErrorDetails.Message } catch { }
        if (-not $err) { $err = $_.Exception.Message }
        return [pscustomobject]@{ Ok = $false; Status = $status; Content = $null; Error = $err }
    }
}

function Assert-DataPlane {
    param([string]$Endpoint, [string]$Key, [string]$Label)
    $r = Invoke-CuApi -Uri "$Endpoint/contentunderstanding/defaults?api-version=$ApiVersion" -Key $Key
    if ($r.Status -in @(401, 403)) {
        throw "Sin acceso al data plane de Content Understanding en el $Label ($Endpoint) (HTTP $($r.Status)). Falta rol 'Cognitive Services User'."
    }
    if (-not $r.Ok) { throw "Fallo consultando defaults del $Label (HTTP $($r.Status)): $($r.Error)" }
    return $r.Content.modelDeployments
}

function Get-Deployments {
    param([string]$ResourceName, [string]$Rg)
    $json = az cognitiveservices account deployment list -n $ResourceName -g $Rg -o json 2>$null
    if (-not $json) { throw "No se pudieron listar los deployments de '$ResourceName' (RG '$Rg')." }
    return ($json | ConvertFrom-Json)
}

function Build-TargetDefaults {
    param($SourceDefaults, $SourceDeployments, $TargetDeployments)
    $map = @{}; $unmapped = @()
    foreach ($alias in $SourceDefaults.PSObject.Properties.Name) {
        $srcDepName = $SourceDefaults.$alias
        $srcDep     = $SourceDeployments | Where-Object { $_.name -eq $srcDepName } | Select-Object -First 1
        if (-not $srcDep) { $unmapped += "$alias (deployment origen '$srcDepName' no encontrado)"; continue }
        $model      = $srcDep.properties.model.name
        $candidates = @($TargetDeployments | Where-Object { $_.properties.model.name -eq $model })
        if ($candidates.Count -eq 0) { $unmapped += "$alias -> modelo '$model' NO desplegado en el destino"; continue }
        $pick = $candidates |
            Sort-Object @{ Expression = { $_.name -eq $srcDepName }; Descending = $true },
                        @{ Expression = { [int]$_.sku.capacity };    Descending = $true } |
            Select-Object -First 1
        $map[$alias] = $pick.name
    }
    return [pscustomobject]@{ Map = $map; Unmapped = $unmapped }
}

# Poll de la operacion de build hasta estado terminal.
function Wait-BuildOperation {
    param([Microsoft.PowerShell.Commands.WebResponseObject]$Response, [string]$Key)
    $opLocation = $Response.Headers["Operation-Location"]
    if ($opLocation -is [array]) { $opLocation = $opLocation[0] }
    if (-not $opLocation) {
        Write-Host "      Respondio $($Response.StatusCode) sin Operation-Location; se asume sincrono." -ForegroundColor Green
        return
    }
    Write-Host "      Build aceptado. Poll: $opLocation" -ForegroundColor Green
    $pollHeaders = Get-AuthHeaders -Key $Key
    $running = @("running", "notStarted", "creating", "Running", "NotStarted", "Creating")
    do {
        Start-Sleep -Seconds 5
        $op = Invoke-RestMethod -Uri $opLocation -Method Get -Headers $pollHeaders
        $status = if ($op.status) { $op.status } else { $op.result.status }
        Write-Host "      status = $status"
    } while ($status -in $running)
    if ($status -notin @("succeeded", "Succeeded", "ready", "Ready")) {
        throw "El build termino en estado '$status'. Detalle: $($op | ConvertTo-Json -Depth 8)"
    }
}

# -----------------------------------------------------------------------------
# Cabecera
# -----------------------------------------------------------------------------
Write-Host "== Recreate CU analyzer (reconstruir / reentrenar en destino) ==" -ForegroundColor Cyan
Write-Host "  Origen : $SourceAnalyzerId @ $SourceResourceName ($SourceRegion)"
Write-Host "  Destino: $TargetAnalyzerId @ $TargetResourceName ($TargetRegion)"
if ($FromExport) { Write-Host "  Fuente de la definicion: export local '$FromExport'" }
Write-Host ""

$totalSteps = 2                                  # definicion + PUT/build
if (-not $SkipPreflight) { $totalSteps++ }        # preflight destino
if ($SyncDefaults)       { $totalSteps++ }        # sync defaults
$step = 1

# -----------------------------------------------------------------------------
# Paso 1: obtener la DEFINICION del analyzer (origen o export)
# -----------------------------------------------------------------------------
Write-Host "[$step/$totalSteps] Obteniendo definicion del analyzer..." -ForegroundColor Yellow
if ($FromExport) {
    if (-not (Test-Path $FromExport)) { throw "No existe el export '$FromExport'." }
    $srcDef = Get-Content -Raw -Path $FromExport | ConvertFrom-Json
} else {
    $g = Invoke-CuApi -Uri "$SourceEndpoint/contentunderstanding/analyzers/$SourceAnalyzerId`?api-version=$ApiVersion" -Key $SourceKey
    if ($g.Status -eq 404) { throw "El analyzer origen '$SourceAnalyzerId' no existe en $SourceResourceName." }
    if (-not $g.Ok)        { throw "No se pudo leer el analyzer origen (HTTP $($g.Status)): $($g.Error)" }
    $srcDef = $g.Content
}
$requiredAliases = @()
if ($srcDef.models) { $requiredAliases = @($srcDef.models.PSObject.Properties.Value | Where-Object { $_ }) | Select-Object -Unique }
$hasKnowledge = $srcDef.knowledgeSources -and @($srcDef.knowledgeSources).Count -gt 0
Write-Host "      Definicion OK. status origen: $($srcDef.status). Alias de modelo: $($requiredAliases -join ', ')" -ForegroundColor Green
if ($hasKnowledge) {
    foreach ($ks in $srcDef.knowledgeSources) {
        Write-Host "      knowledgeSource: $($ks.kind) @ $($ks.containerUrl) (prefix: $($ks.prefix))" -ForegroundColor Green
    }
    Write-Host "      -> Se REENTRENARA desde estos datos etiquetados. El destino debe poder leer el blob con su identidad administrada." -ForegroundColor DarkYellow
} else {
    Write-Host "      Sin knowledgeSources: se reconstruira solo desde el schema (sin reentrenar con datos etiquetados)." -ForegroundColor DarkGray
}
$step++

# -----------------------------------------------------------------------------
# Paso 2 (opcional): preflight destino
# -----------------------------------------------------------------------------
if (-not $SkipPreflight) {
    Write-Host "[$step/$totalSteps] Preflight destino..." -ForegroundColor Yellow
    $targetDefaults = Assert-DataPlane -Endpoint $TargetEndpoint -Key $TargetKey -Label "DESTINO"
    Write-Host "      Destino: data plane OK" -ForegroundColor Green

    $missing = @()
    foreach ($alias in $requiredAliases) {
        if (-not $targetDefaults -or -not $targetDefaults.PSObject.Properties.Name.Contains($alias)) { $missing += $alias }
    }
    if ($missing.Count -gt 0 -and -not $SyncDefaults) {
        throw @"
Los defaults del DESTINO no mapean los alias que usa '$SourceAnalyzerId': $($missing -join ', ').
      Relanza con -SyncDefaults para replicar el mapeo del origen en el destino.
"@
    } elseif ($missing.Count -gt 0) {
        Write-Host "      Alias sin mapear ($($missing -join ', ')). -SyncDefaults los corregira." -ForegroundColor DarkYellow
    } else {
        Write-Host "      Destino: todos los alias resueltos" -ForegroundColor Green
    }

    # Comprobar existencia del destino
    $existing = Invoke-CuApi -Uri "$TargetEndpoint/contentunderstanding/analyzers/$TargetAnalyzerId`?api-version=$ApiVersion" -Key $TargetKey
    if ($existing.Ok -and $existing.Status -eq 200) {
        if (-not $Force) {
            throw "El analyzer '$TargetAnalyzerId' YA existe en $TargetResourceName (status: $($existing.Content.status)). Usa -Force para sobrescribirlo (allowReplace=true)."
        }
        Write-Host "      El destino ya existe; -Force -> se sobrescribira (allowReplace=true)." -ForegroundColor DarkYellow
    }
    $step++
}

# -----------------------------------------------------------------------------
# Paso 3 (opcional): sincronizar defaults de modelo origen -> destino
# -----------------------------------------------------------------------------
if ($SyncDefaults) {
    Write-Host "[$step/$totalSteps] Sincronizando model deployments (origen -> destino)..." -ForegroundColor Yellow
    $sourceDefaults = Assert-DataPlane -Endpoint $SourceEndpoint -Key $SourceKey -Label "ORIGEN"
    if (-not $sourceDefaults) { throw "El ORIGEN no tiene defaults de modelo configurados." }
    $srcDeps = Get-Deployments -ResourceName $SourceResourceName -Rg $SourceResourceGroup
    $tgtDeps = Get-Deployments -ResourceName $TargetResourceName -Rg $TargetResourceGroup
    $built   = Build-TargetDefaults -SourceDefaults $sourceDefaults -SourceDeployments $srcDeps -TargetDeployments $tgtDeps
    foreach ($u in $built.Unmapped) { Write-Host "      AVISO: $u" -ForegroundColor Red }
    if ($built.Map.Count -eq 0) { throw "No se pudo mapear ningun alias al destino." }
    foreach ($alias in ($built.Map.Keys | Sort-Object)) {
        Write-Host ("      {0,-36} {1} -> {2}" -f $alias, $sourceDefaults.$alias, $built.Map[$alias])
    }
    $patchBody = @{ modelDeployments = $built.Map } | ConvertTo-Json -Depth 5
    $patch = Invoke-CuApi -Uri "$TargetEndpoint/contentunderstanding/defaults?api-version=$ApiVersion" -Method Patch -Key $TargetKey -Body $patchBody
    if (-not $patch.Ok) { throw "Fallo el PATCH de defaults en el destino (HTTP $($patch.Status)): $($patch.Error)" }
    Write-Host "      Defaults del destino actualizados." -ForegroundColor Green
    $step++
}

# -----------------------------------------------------------------------------
# Paso 4: PUT (create-or-replace) en el DESTINO
# -----------------------------------------------------------------------------
Write-Host "[$step/$totalSteps] Reconstruyendo el analyzer en el destino (PUT + build)..." -ForegroundColor Yellow

# Construir el cuerpo con SOLO los campos escribibles presentes en la definicion.
$body = [ordered]@{}
foreach ($p in $srcDef.PSObject.Properties) {
    if ($ReadOnlyFields -contains $p.Name) { continue }
    if ($null -eq $p.Value) { continue }
    $body[$p.Name] = $p.Value
}
$bodyJson = $body | ConvertTo-Json -Depth 40

$putUri = "$TargetEndpoint/contentunderstanding/analyzers/$TargetAnalyzerId`?api-version=$ApiVersion"
if ($Force) { $putUri += "&allowReplace=true" }

$putHeaders = Get-AuthHeaders -Key $TargetKey
$putHeaders["Content-Type"] = "application/json"

try {
    $resp = Invoke-WebRequest -Uri $putUri -Method Put -Headers $putHeaders -Body $bodyJson -ErrorAction Stop
}
catch {
    $errBody = ""
    try { $errBody = $_.ErrorDetails.Message } catch { }
    if (-not $errBody) { $errBody = $_.Exception.Message }
    Write-Host "      PUT fallo. Respuesta del servicio:" -ForegroundColor Red
    Write-Host "      $errBody" -ForegroundColor Red
    throw
}
Wait-BuildOperation -Response $resp -Key $TargetKey
$step++

# -----------------------------------------------------------------------------
# Verificacion
# -----------------------------------------------------------------------------
$verify = Invoke-RestMethod -Uri "$TargetEndpoint/contentunderstanding/analyzers/$TargetAnalyzerId`?api-version=$ApiVersion" -Method Get -Headers (Get-AuthHeaders -Key $TargetKey)
Write-Host ""
Write-Host "Analyzer '$($verify.analyzerId)' reconstruido en $TargetResourceName." -ForegroundColor Cyan
Write-Host "  status      : $($verify.status)"
Write-Host "  description : $($verify.description)"
Write-Host ""
Write-Host "NOTA: es una RECONSTRUCCION reentrenada desde los datos etiquetados, no un snapshot" -ForegroundColor DarkYellow
Write-Host "      bit a bit del origen. Valida con documentos de prueba antes de repuntar el registro." -ForegroundColor DarkYellow
