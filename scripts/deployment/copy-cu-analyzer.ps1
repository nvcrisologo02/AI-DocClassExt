<#
.SYNOPSIS
    ADVERTENCIA (verificado 2026-07-17): el modo CROSS-RESOURCE de este script NO
    funciona en este entorno. La Copy API cross-resource de Content Understanding
    (grantCopyAuthorization + :copy) responde "has not granted the necessary
    permissions" incluso con la identidad administrada del destino con rol
    'Cognitive Services User' sobre el origen; el grant devuelve un cuerpo sin
    'source' y el flujo con token (:getCopyAuthorization) da 404. Probable
    limitacion de servicio con analizadores project-scoped de Foundry.
    -> Para replicar entre regiones usa 'recreate-cu-analyzer.ps1' (reconstruye/
       reentrena en el destino). El modo -SameResource (snapshot/rollback) de ESTE
       script SI funciona porque no cruza recursos.

    Copia un analyzer de Azure AI Content Understanding SIN reentrenar ni reconstruir
    el knowledge. Soporta dos modos:

      - Cross-resource (por defecto): replica un analyzer de un recurso a otro
        (Sweden Central -> West Europe) para failover / reparto de carga.
      - Same-resource (-SameResource): clona un analyzer dentro del MISMO recurso con
        un ID nuevo, como snapshot / punto de rollback.

    LO QUE ESTE SCRIPT *NO* HACE: cambiar el schema.
    La copia replica el fieldSchema CONGELADO, y un analyzer construido es INMUTABLE
    (la API GA no expone PATCH/update de fieldSchema). Para anadir o quitar campos hay
    que editar el schema en el PROYECTO de Content Understanding Studio del que se
    construyo el analyzer y volver a construir (Build analyzer) con un ID nuevo.
    Ver la seccion 12.7 de docs/guias/GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING.md.

.DESCRIPTION
    Usa la Copy API de Content Understanding (api-version 2025-11-01).

    MODO CROSS-RESOURCE (2 pasos, porque la credencial puede no tener permisos en ambos):
      1. grantCopyAuthorization  -> sobre el analyzer ORIGEN.
      2. :copy                   -> sobre el recurso DESTINO (operacion de larga
                                    duracion que se poll-ea hasta 'succeeded').

    MODO SAME-RESOURCE (1 paso):
      1. :copy con { sourceAnalyzerId } -> no requiere grantCopyAuthorization.

    En ambos casos la copia replica schema + configuracion + estado entrenado
    ("knowledge"): el analyzer destino queda igual de entrenado que el origen.
    Es un snapshot puntual: si el origen se reentrena, hay que volver a copiar.

    PERMISOS (data plane, no control plane):
    La credencial necesita el rol 'Cognitive Services User' en AMBOS recursos.
    OJO: 'Contributor' y 'Cognitive Services Contributor' NO sirven: su lista de
    dataActions esta VACIA, solo cubren control plane. Sin el rol correcto, las
    llamadas a /contentunderstanding/* devuelven 401 PermissionDenied.
    El preflight detecta esto y muestra el comando exacto para corregirlo.

    MODEL DEPLOYMENTS:
    Los analyzers referencian modelos por ALIAS (p. ej. models.completion = 'gpt-4.1'),
    no por nombre de deployment. El alias lo resuelve /contentunderstanding/defaults a
    nivel de recurso. Por eso el destino puede tener deployments con OTROS nombres,
    siempre que sus defaults mapeen los mismos alias. Si no los mapea, la copia se crea
    pero falla al analizar. Usa -SyncDefaults para replicar el mapeo del origen.

.PARAMETER SourceAnalyzerId
    ID del analyzer origen (p. ej. CU_NS_1.5_0).

.PARAMETER TargetAnalyzerId
    ID del analyzer destino. En cross-resource, por defecto = SourceAnalyzerId.
    En -SameResource es OBLIGATORIO y debe ser distinto del origen.

.PARAMETER SameResource
    Clona dentro del mismo recurso como snapshot / rollback. No usa ResourceGroup ni
    endpoint destino. No permite cambiar el schema: la copia sale con los mismos campos.

.PARAMETER SyncDefaults
    Antes de copiar, replica los defaults de modelo del origen en el destino, mapeando
    cada alias al deployment del destino que sirve el MISMO modelo (los nombres de
    deployment suelen diferir entre recursos). Solo cross-resource.

.PARAMETER SkipPreflight
    Omite las validaciones previas. Solo para depuracion.

.EXAMPLE
    # Snapshot de rollback dentro del mismo recurso (mismos campos que el origen)
    ./copy-cu-analyzer.ps1 -SameResource -SourceAnalyzerId CU_NS_1.5_0 -TargetAnalyzerId CU_NS_1.5_0_backup

.EXAMPLE
    # Replicar Sweden -> West Europe con Entra ID (az login previo)
    ./copy-cu-analyzer.ps1 -SourceAnalyzerId CU_NS_1.5_0 -ResourceGroup <RG>

.EXAMPLE
    # Replicar alineando primero los defaults de modelo del destino
    ./copy-cu-analyzer.ps1 -SourceAnalyzerId CU_NS_1.5_0 -ResourceGroup <RG> -SyncDefaults

.EXAMPLE
    # Replicar Sweden -> West Europe con API keys explicitas
    ./copy-cu-analyzer.ps1 -SourceAnalyzerId CU_NS_1.5_0 -ResourceGroup <RG> `
        -SourceKey <key-sweden> -TargetKey <key-westeurope>

.NOTES
    Si el analyzer usa clasificacion/segmentacion que referencia a otros analyzers,
    copia tambien esos analyzers referenciados.
#>
[CmdletBinding(DefaultParameterSetName = 'CrossResource')]
param(
    [Parameter(Mandatory = $true)][string]$SourceAnalyzerId,
    [Parameter(Mandatory = $false)][string]$TargetAnalyzerId,

    # --- Modo versionado intra-recurso ---
    [Parameter(Mandatory = $true, ParameterSetName = 'SameResource')][switch]$SameResource,

    # --- Recurso ORIGEN (Sweden Central, CU primario) ---
    [string]$SourceResourceName = "upe48-mm2avmdm-swedencentral",
    [string]$SourceEndpoint     = "https://upe48-mm2avmdm-swedencentral.services.ai.azure.com",
    [string]$SourceRegion       = "swedencentral",

    # --- Recurso DESTINO (West Europe, CU secundario). Solo cross-resource ---
    [Parameter(ParameterSetName = 'CrossResource')][string]$TargetResourceName = "srbaisrv-westeurope",
    [Parameter(ParameterSetName = 'CrossResource')][string]$TargetEndpoint     = "https://srbaisrv-westeurope.services.ai.azure.com/",
    [Parameter(ParameterSetName = 'CrossResource')][string]$TargetRegion       = "westeurope",

    # --- Identidad ARM de los recursos. Solo cross-resource ---
    [Parameter(ParameterSetName = 'CrossResource')][string]$SubscriptionId = "647c7246-54bc-4d31-b909-431cacf03272",
    # NOTA: confirma el resource group real de los recursos.
    #       Descubrelo con:  az cognitiveservices account list -o table
    [Parameter(Mandatory = $true, ParameterSetName = 'CrossResource')][string]$ResourceGroup,
    [Parameter(ParameterSetName = 'CrossResource')][string]$SourceResourceGroup,   # si origen y destino estan en RG distintos
    [Parameter(ParameterSetName = 'CrossResource')][string]$TargetResourceGroup,

    # --- Alineacion de model deployments. Solo cross-resource ---
    [Parameter(ParameterSetName = 'CrossResource')][switch]$SyncDefaults,

    # --- Autenticacion (si no se pasan keys, se usa el token de 'az login') ---
    [string]$SourceKey,
    [Parameter(ParameterSetName = 'CrossResource')][string]$TargetKey,

    [switch]$SkipPreflight,
    [string]$ApiVersion = "2025-11-01"
)

$ErrorActionPreference = "Stop"

$isSameResource = $PSCmdlet.ParameterSetName -eq 'SameResource'

# -----------------------------------------------------------------------------
# Normalizacion de parametros segun el modo
# -----------------------------------------------------------------------------
if ($isSameResource) {
    if (-not $TargetAnalyzerId) {
        throw "-TargetAnalyzerId es obligatorio en modo -SameResource: es el ID del clon (p. ej. CU_NS_1.5_0_backup)."
    }
    if ($TargetAnalyzerId -eq $SourceAnalyzerId) {
        throw "-TargetAnalyzerId debe ser distinto de -SourceAnalyzerId: la copia intra-recurso crea un analyzer nuevo."
    }
    # El destino es el propio recurso origen.
    $TargetEndpoint     = $SourceEndpoint
    $TargetResourceName = $SourceResourceName
    $TargetRegion       = $SourceRegion
    $TargetKey          = $SourceKey
}
else {
    if (-not $TargetAnalyzerId)    { $TargetAnalyzerId = $SourceAnalyzerId }
    if (-not $SourceResourceGroup) { $SourceResourceGroup = $ResourceGroup }
    if (-not $TargetResourceGroup) { $TargetResourceGroup = $ResourceGroup }

    # La Copy API de CU compara los resource IDs de forma CASE-SENSITIVE, y ARM
    # puede almacenar el nombre del RG con distinto casing por recurso (p. ej.
    # 'srbrgdocsaiprod' en el origen vs 'SRBRGDOCSAIPROD' en el destino). Si
    # construimos el ID por interpolacion con el RG que pasa el usuario, el casing
    # puede no coincidir con el que la grantCopyAuthorization registra en el origen
    # y la copia falla con "has not granted the necessary permissions".
    # Por eso resolvemos el ID CANONICO via ARM (az es case-insensitive al buscar
    # y devuelve el casing real almacenado).
    $sourceResourceId = (az cognitiveservices account show -n $SourceResourceName -g $SourceResourceGroup --query id -o tsv 2>$null)
    $targetResourceId = (az cognitiveservices account show -n $TargetResourceName -g $TargetResourceGroup --query id -o tsv 2>$null)
    if (-not $sourceResourceId) {
        $sourceResourceId = "/subscriptions/$SubscriptionId/resourceGroups/$SourceResourceGroup/providers/Microsoft.CognitiveServices/accounts/$SourceResourceName"
        Write-Host "AVISO: no se pudo resolver el resource ID canonico del ORIGEN via ARM; usando el construido. Si la copia falla con 'has not granted...', revisa el casing del RG." -ForegroundColor DarkYellow
    }
    if (-not $targetResourceId) {
        $targetResourceId = "/subscriptions/$SubscriptionId/resourceGroups/$TargetResourceGroup/providers/Microsoft.CognitiveServices/accounts/$TargetResourceName"
        Write-Host "AVISO: no se pudo resolver el resource ID canonico del DESTINO via ARM; usando el construido. Si la copia falla con 'has not granted...', revisa el casing del RG." -ForegroundColor DarkYellow
    }
    $sourceResourceId = $sourceResourceId.Trim()
    $targetResourceId = $targetResourceId.Trim()
}

$SourceEndpoint = $SourceEndpoint.TrimEnd('/')
$TargetEndpoint = $TargetEndpoint.TrimEnd('/')

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
$script:CachedToken = $null

function Get-AuthHeaders {
    param([string]$Key)
    if ($Key) {
        return @{ "Ocp-Apim-Subscription-Key" = $Key }
    }
    if (-not $script:CachedToken) {
        Write-Verbose "Sin API key: obteniendo token de Entra ID via 'az account get-access-token'..."
        $script:CachedToken = (az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv)
        if (-not $script:CachedToken) { throw "No se pudo obtener token de Entra ID. Ejecuta 'az login' o pasa -SourceKey/-TargetKey." }
    }
    return @{ "Authorization" = "Bearer $($script:CachedToken)" }
}

# Invoca la API de CU sin lanzar excepcion: devuelve { Ok, Status, Content }.
function Invoke-CuApi {
    param(
        [string]$Uri,
        [string]$Method = 'Get',
        [string]$Key,
        [string]$Body
    )
    $headers = Get-AuthHeaders -Key $Key
    if ($Body) { $headers["Content-Type"] = "application/json" }

    $params = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = 'Stop' }
    if ($Body) { $params["Body"] = $Body }

    try {
        $resp    = Invoke-WebRequest @params
        $content = if ($resp.Content) { $resp.Content | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Ok = $true; Status = [int]$resp.StatusCode; Content = $content }
    }
    catch {
        $status = 0
        if ($_.Exception.PSObject.Properties.Name -contains 'Response' -and $_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        return [pscustomobject]@{ Ok = $false; Status = $status; Content = $null; Error = $_.Exception.Message }
    }
}

function Get-RoleAssignmentHint {
    param([string]$ResourceId)
    return @"
      Concede el rol de DATA PLANE sobre ese recurso y reintenta:

        az role assignment create ``
          --assignee `$(az ad signed-in-user show --query id -o tsv) ``
          --role "Cognitive Services User" ``
          --scope "$ResourceId"

      OJO: 'Contributor' y 'Cognitive Services Contributor' NO valen aqui: sus
      dataActions estan vacias (solo control plane). Hace falta 'Cognitive Services User'.
"@
}

function Assert-DataPlane {
    param([string]$Endpoint, [string]$Key, [string]$ResourceId, [string]$Label)

    $r = Invoke-CuApi -Uri "$Endpoint/contentunderstanding/defaults?api-version=$ApiVersion" -Key $Key
    if ($r.Status -in @(401, 403)) {
        $hint = if ($ResourceId) { Get-RoleAssignmentHint -ResourceId $ResourceId } else { "" }
        throw @"
Sin acceso al data plane de Content Understanding en el $Label ($Endpoint).
      La credencial actual no puede leer /contentunderstanding/defaults (HTTP $($r.Status)).
$hint
"@
    }
    if (-not $r.Ok) {
        throw "Fallo consultando defaults del $Label (HTTP $($r.Status)): $($r.Error)"
    }
    return $r.Content.modelDeployments
}

# Lista los deployments de un recurso via ARM (nombre -> modelo).
function Get-Deployments {
    param([string]$ResourceName, [string]$Rg)
    $json = az cognitiveservices account deployment list -n $ResourceName -g $Rg -o json 2>$null
    if (-not $json) {
        throw "No se pudieron listar los deployments de '$ResourceName' (RG '$Rg'). Revisa el nombre/RG y que 'az login' tenga lectura ARM."
    }
    return ($json | ConvertFrom-Json)
}

# Dado el mapa de defaults del origen, construye el equivalente para el destino
# resolviendo por MODELO subyacente (los nombres de deployment difieren entre recursos).
function Build-TargetDefaults {
    param($SourceDefaults, $SourceDeployments, $TargetDeployments)

    $map      = @{}
    $unmapped = @()

    foreach ($alias in $SourceDefaults.PSObject.Properties.Name) {
        $srcDepName = $SourceDefaults.$alias
        $srcDep     = $SourceDeployments | Where-Object { $_.name -eq $srcDepName } | Select-Object -First 1
        if (-not $srcDep) {
            $unmapped += "$alias (deployment origen '$srcDepName' no encontrado)"
            continue
        }

        $model      = $srcDep.properties.model.name
        $candidates = @($TargetDeployments | Where-Object { $_.properties.model.name -eq $model })
        if ($candidates.Count -eq 0) {
            $unmapped += "$alias -> modelo '$model' NO desplegado en el destino"
            continue
        }

        # Preferencia: mismo nombre que el origen > mayor capacidad.
        $pick = $candidates |
            Sort-Object @{ Expression = { $_.name -eq $srcDepName }; Descending = $true },
                        @{ Expression = { [int]$_.sku.capacity };    Descending = $true } |
            Select-Object -First 1

        $map[$alias] = $pick.name
    }

    return [pscustomobject]@{ Map = $map; Unmapped = $unmapped }
}

function Wait-CopyOperation {
    param(
        [Microsoft.PowerShell.Commands.WebResponseObject]$Response,
        [string]$Key
    )
    $opLocation = $Response.Headers["Operation-Location"]
    if ($opLocation -is [array]) { $opLocation = $opLocation[0] }

    if (-not $opLocation) {
        Write-Host "      Respondio $($Response.StatusCode) sin Operation-Location; se asume sincrona." -ForegroundColor Green
        return
    }

    Write-Host "      Operacion aceptada. Poll: $opLocation" -ForegroundColor Green
    $pollHeaders = Get-AuthHeaders -Key $Key
    do {
        Start-Sleep -Seconds 5
        $op = Invoke-RestMethod -Uri $opLocation -Method Get -Headers $pollHeaders
        $status = $op.status
        Write-Host "      status = $status"
    } while ($status -in @("running", "notStarted", "Running", "NotStarted"))

    if ($status -notin @("succeeded", "Succeeded")) {
        throw "La copia termino en estado '$status'. Detalle: $($op | ConvertTo-Json -Depth 6)"
    }
}

# -----------------------------------------------------------------------------
# Cabecera
# -----------------------------------------------------------------------------
$mode        = if ($isSameResource) { "SAME-RESOURCE (snapshot / rollback)" } else { "CROSS-RESOURCE (replica)" }
$doSync      = $SyncDefaults -and -not $isSameResource
$totalSteps  = 2                                          # copy + verify
if (-not $SkipPreflight) { $totalSteps++ }                # preflight
if ($doSync)             { $totalSteps++ }                # sync defaults
if (-not $isSameResource){ $totalSteps++ }                # grant
$step = 1

Write-Host "== Copy CU analyzer -- $mode ==" -ForegroundColor Cyan
Write-Host "  Origen : $SourceAnalyzerId @ $SourceResourceName ($SourceRegion)"
Write-Host "  Destino: $TargetAnalyzerId @ $TargetResourceName ($TargetRegion)"
Write-Host ""

# -----------------------------------------------------------------------------
# Paso 0: Preflight -- falla temprano y con un mensaje accionable
# -----------------------------------------------------------------------------
$sourceDefaults = $null
$targetDefaults = $null

if (-not $SkipPreflight) {
    Write-Host "[$step/$totalSteps] Preflight..." -ForegroundColor Yellow

    # 1. Data plane en el origen.
    $sourceDefaults = Assert-DataPlane -Endpoint $SourceEndpoint -Key $SourceKey `
                                       -ResourceId $sourceResourceId -Label "ORIGEN"
    Write-Host "      Origen : data plane OK" -ForegroundColor Green

    # 2. El analyzer origen existe -> de paso, sus alias de modelo.
    $srcAnalyzer = Invoke-CuApi -Uri "$SourceEndpoint/contentunderstanding/analyzers/$SourceAnalyzerId`?api-version=$ApiVersion" -Key $SourceKey
    if ($srcAnalyzer.Status -eq 404) {
        throw "El analyzer origen '$SourceAnalyzerId' no existe en $SourceResourceName."
    }
    if (-not $srcAnalyzer.Ok) {
        throw "No se pudo leer el analyzer origen '$SourceAnalyzerId' (HTTP $($srcAnalyzer.Status)): $($srcAnalyzer.Error)"
    }

    $requiredAliases = @()
    if ($srcAnalyzer.Content.models) {
        $requiredAliases = @($srcAnalyzer.Content.models.PSObject.Properties.Value | Where-Object { $_ }) | Select-Object -Unique
    }
    Write-Host "      Analyzer '$SourceAnalyzerId' OK. Alias de modelo: $($requiredAliases -join ', ')" -ForegroundColor Green

    # 3. Data plane en el destino (en same-resource es el mismo recurso: ya validado).
    if (-not $isSameResource) {
        $targetDefaults = Assert-DataPlane -Endpoint $TargetEndpoint -Key $TargetKey `
                                           -ResourceId $targetResourceId -Label "DESTINO"
        Write-Host "      Destino: data plane OK" -ForegroundColor Green

        # 4. El destino resuelve los alias que el analyzer necesita.
        $missing = @()
        foreach ($alias in $requiredAliases) {
            if (-not $targetDefaults -or -not $targetDefaults.PSObject.Properties.Name.Contains($alias)) {
                $missing += $alias
            }
        }
        if ($missing.Count -gt 0) {
            if ($doSync) {
                Write-Host "      Destino: alias sin mapear ($($missing -join ', ')). -SyncDefaults los corregira." -ForegroundColor DarkYellow
            }
            else {
                throw @"
Los defaults del DESTINO no mapean los alias que usa '$SourceAnalyzerId': $($missing -join ', ').
      La copia se crearia, pero fallaria al analizar (el alias no resuelve a ningun deployment).
      Relanza con -SyncDefaults para replicar el mapeo del origen en el destino.
"@
            }
        }
        else {
            Write-Host "      Destino: todos los alias resueltos" -ForegroundColor Green
        }
    }
    $step++
}

# -----------------------------------------------------------------------------
# Paso 0b: Sincronizar defaults de modelo origen -> destino
# -----------------------------------------------------------------------------
if ($doSync) {
    Write-Host "[$step/$totalSteps] Sincronizando model deployments (origen -> destino)..." -ForegroundColor Yellow

    if (-not $sourceDefaults) {
        $sourceDefaults = Assert-DataPlane -Endpoint $SourceEndpoint -Key $SourceKey `
                                           -ResourceId $sourceResourceId -Label "ORIGEN"
    }
    if (-not $sourceDefaults) {
        throw "El ORIGEN no tiene defaults de modelo configurados: no hay nada que replicar."
    }

    $srcDeps = Get-Deployments -ResourceName $SourceResourceName -Rg $SourceResourceGroup
    $tgtDeps = Get-Deployments -ResourceName $TargetResourceName -Rg $TargetResourceGroup

    $built = Build-TargetDefaults -SourceDefaults $sourceDefaults -SourceDeployments $srcDeps -TargetDeployments $tgtDeps

    foreach ($u in $built.Unmapped) {
        Write-Host "      AVISO: $u" -ForegroundColor Red
    }
    if ($built.Map.Count -eq 0) {
        throw "No se pudo mapear ningun alias al destino. Despliega en '$TargetResourceName' los modelos que usa el origen."
    }

    foreach ($alias in ($built.Map.Keys | Sort-Object)) {
        Write-Host ("      {0,-36} {1} -> {2}" -f $alias, $sourceDefaults.$alias, $built.Map[$alias])
    }

    $patchBody = @{ modelDeployments = $built.Map } | ConvertTo-Json -Depth 5
    $patch = Invoke-CuApi -Uri "$TargetEndpoint/contentunderstanding/defaults?api-version=$ApiVersion" `
                          -Method Patch -Key $TargetKey -Body $patchBody
    if (-not $patch.Ok) {
        throw "Fallo el PATCH de defaults en el destino (HTTP $($patch.Status)): $($patch.Error)"
    }
    Write-Host "      Defaults del destino actualizados." -ForegroundColor Green

    if ($built.Unmapped.Count -gt 0) {
        Write-Host "      Revisa los avisos: hay alias sin equivalencia en el destino." -ForegroundColor DarkYellow
    }
    $step++
}

# -----------------------------------------------------------------------------
# Paso 1 (solo cross-resource): grantCopyAuthorization sobre el ORIGEN
# -----------------------------------------------------------------------------
if (-not $isSameResource) {
    Write-Host "[$step/$totalSteps] Grant copy authorization en el origen..." -ForegroundColor Yellow
    $grantUri  = "$SourceEndpoint/contentunderstanding/analyzers/$($SourceAnalyzerId):grantCopyAuthorization?api-version=$ApiVersion"
    $grantBody = @{
        targetAzureResourceId = $targetResourceId
        targetRegion          = $TargetRegion
    } | ConvertTo-Json

    $grantHeaders = Get-AuthHeaders -Key $SourceKey
    $grantHeaders["Content-Type"] = "application/json"

    $auth = Invoke-RestMethod -Uri $grantUri -Method Post -Headers $grantHeaders -Body $grantBody
    Write-Host "      OK. Autorizacion emitida (expira: $($auth.expiresAt))" -ForegroundColor Green
    # Diagnostico: que registro exactamente el origen (para cotejar con el copy).
    Write-Host "      grant.source           : $($auth.source)" -ForegroundColor DarkGray
    Write-Host "      grant.targetResourceId : $($auth.targetAzureResourceId)" -ForegroundColor DarkGray
    $step++
}

# -----------------------------------------------------------------------------
# Paso 2: :copy (cross -> sobre el DESTINO / same -> sobre el propio recurso)
# -----------------------------------------------------------------------------
Write-Host "[$step/$totalSteps] Lanzando copia..." -ForegroundColor Yellow
$copyUri = "$TargetEndpoint/contentunderstanding/analyzers/$($TargetAnalyzerId):copy?api-version=$ApiVersion"

$copyBody = if ($isSameResource) {
    @{ sourceAnalyzerId = $SourceAnalyzerId } | ConvertTo-Json
} else {
    @{
        sourceAzureResourceId = $sourceResourceId
        sourceAnalyzerId      = $SourceAnalyzerId
        sourceRegion          = $SourceRegion
    } | ConvertTo-Json
}

$copyHeaders = Get-AuthHeaders -Key $TargetKey
$copyHeaders["Content-Type"] = "application/json"

# Diagnostico: cuerpo exacto que enviamos al destino.
Write-Host "      copy URI : $copyUri" -ForegroundColor DarkGray
Write-Host "      copy body: $copyBody" -ForegroundColor DarkGray

# La autorizacion de copia emitida en el origen puede tardar unos segundos en
# propagarse al plano donde el destino la valida al hacer el pull. Reintentamos
# el copy ante el error transitorio 'ModelNotFound / has not granted...' con
# backoff creciente antes de rendirnos.
$maxAttempts = 6
$resp = $null
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    try {
        $resp = Invoke-WebRequest -Uri $copyUri -Method Post -Headers $copyHeaders -Body $copyBody -ErrorAction Stop
        break
    }
    catch {
        $errBody = ""
        try { $errBody = $_.ErrorDetails.Message } catch { }
        if (-not $errBody) { $errBody = $_.Exception.Message }
        $isGrantLag = ($errBody -match 'has not granted') -or ($errBody -match 'ModelNotFound')
        if ($isGrantLag -and $attempt -lt $maxAttempts) {
            $wait = 15 * $attempt
            Write-Host "      Intento $attempt/${maxAttempts}: la autorizacion aun no se valida en el destino. Reintento en ${wait}s..." -ForegroundColor DarkYellow
            Start-Sleep -Seconds $wait
            continue
        }
        Write-Host "      Copy fallo tras $attempt intento(s). Respuesta del servicio:" -ForegroundColor Red
        Write-Host "      $errBody" -ForegroundColor Red
        throw
    }
}
Wait-CopyOperation -Response $resp -Key $TargetKey
$step++

# -----------------------------------------------------------------------------
# Paso 3: verificar
# -----------------------------------------------------------------------------
Write-Host "[$step/$totalSteps] Verificando el analyzer destino..." -ForegroundColor Yellow
$getUri = "$TargetEndpoint/contentunderstanding/analyzers/$TargetAnalyzerId`?api-version=$ApiVersion"
$verify = Invoke-RestMethod -Uri $getUri -Method Get -Headers (Get-AuthHeaders -Key $TargetKey)

Write-Host "      Analyzer '$($verify.analyzerId)' presente en $TargetResourceName." -ForegroundColor Green
Write-Host "      status      : $($verify.status)"
Write-Host "      description : $($verify.description)"
Write-Host ""

# -----------------------------------------------------------------------------
# Cierre
# -----------------------------------------------------------------------------
Write-Host "Copia completada. El analyzer destino parte del mismo estado entrenado que el origen." -ForegroundColor Cyan
if ($isSameResource) {
    Write-Host "'$TargetAnalyzerId' es un CLON CONGELADO de '$SourceAnalyzerId': mismos campos, mismo schema." -ForegroundColor DarkYellow
    Write-Host "Sirve como backup / rollback. NO puedes anadir ni quitar campos sobre el: un analyzer" -ForegroundColor DarkYellow
    Write-Host "construido es inmutable (no hay PATCH de fieldSchema en la API)." -ForegroundColor DarkYellow
    Write-Host "Para cambiar campos: edita el schema en el PROYECTO de CU Studio y reconstruye con un ID" -ForegroundColor DarkYellow
    Write-Host "nuevo. Ver 12.7 en docs/guias/GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING.md" -ForegroundColor DarkYellow
} else {
    Write-Host "Recuerda: es un snapshot. Si reentrenas el origen, vuelve a ejecutar esta copia." -ForegroundColor DarkYellow
    Write-Host "La copia NO cambia el schema: el destino tiene exactamente los mismos campos que el origen." -ForegroundColor DarkYellow
}
