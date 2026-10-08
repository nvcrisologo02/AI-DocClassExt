#Requires -Version 7.0
<#
.SYNOPSIS
    Aplica de forma idempotente los deployments de modelo declarados en
    infra/ai/deployments.<env>.json sobre las cuentas OpenAI/Foundry del
    entorno: crea los que faltan y nunca borra, renombra ni modifica los que
    ya existen.

.DESCRIPTION
    Es el paso 2 ("Deployments") del flujo de infra/ai/README.md. Para cada
    cuenta de "accounts" del fichero deseado:

      1. Resuelve subscriptionId y resourceGroup de la cuenta en
         infra/ai/resources.<env>.json (cualquier alias cuyo "account" coincida
         y tenga subscriptionId; el RG de DEV no esta en la suscripcion por
         defecto de az, asi que no se usa "az account show").
      2. GET de los deployments existentes por ARM
         (Microsoft.CognitiveServices/accounts/{cuenta}/deployments).
      3. Para cada deployment declarado:
           - no existe            -> create (PUT) y sondeo de provisioningState
                                     hasta Succeeded/Failed o -TimeoutMinutes.
           - existe e identico    -> ok (modelo, version, SKU y capacidad).
           - existe y difiere     -> differs: se informa y NO se toca; alinear a
                                     mano o cambiar el fichero (nunca se hace
                                     PUT sobre uno existente).
      4. Los deployments de la cuenta que no estan en el fichero se listan
         como "unmanaged" a titulo informativo; nunca se borran.

    Antes de tocar nada valida la coherencia del fichero: "intent" debe ser
    "desired" (deployments.prod.json es "observed" y no se aplica) y cada
    nombre de contentUnderstandingDefaults debe existir en "accounts" de la
    misma cuenta. Los defaults de Content Understanding en si (PATCH
    /contentunderstanding/defaults) los fija build-analyzers.ps1, no este
    script.

    Todas las llamadas a ARM van con token fresco e Invoke-WebRequest, no con
    az.cmd (pierde comillas y corta las URLs en "&" y "?" en Windows).

    Requisito de permisos: Cognitive Services Contributor (o equivalente con
    Microsoft.CognitiveServices/accounts/deployments/write) sobre cada
    cuenta. Si la cuota del SKU/modelo no esta concedida en la suscripcion
    (p. ej. DataZoneStandard para gpt-5-mini en DEV, peticion P1 de AB#100311),
    el PUT falla con InsufficientQuota y el deployment queda como "failed".

.PARAMETER Environment
    Entorno destino: dev o pre.
.PARAMETER Account
    Nombres de cuenta a procesar (por defecto todas las de "accounts").
.PARAMETER DryRun
    Solo GET y el plan de lo que haria; no crea nada. Alias funcional: -WhatIf.
.PARAMETER WhatIf
    Alias de -DryRun.
.PARAMETER TimeoutMinutes
    Tiempo maximo de sondeo por deployment. Por defecto 10.
.PARAMETER PollSeconds
    Intervalo de sondeo. Por defecto 5.
.PARAMETER ApiVersion
    api-version de ARM para Microsoft.CognitiveServices. Por defecto 2025-06-01.
.EXAMPLE
    pwsh scripts/ai/apply-deployments.ps1 -Environment dev -DryRun
.EXAMPLE
    pwsh scripts/ai/apply-deployments.ps1 -Environment dev
.EXAMPLE
    pwsh scripts/ai/apply-deployments.ps1 -Environment pre -Account srbaisrv01predocai -DryRun
#>
param(
    [Parameter(Mandatory)][ValidateSet('dev', 'pre')][string]$Environment,
    [string[]]$Account,
    [switch]$DryRun,
    [switch]$WhatIf,
    [int]$TimeoutMinutes = 10,
    [int]$PollSeconds = 5,
    [string]$ApiVersion = '2025-06-01'
)
$ErrorActionPreference = 'Stop'

# ARM en este entorno pasa por un proxy TLS; sin esto tanto az CLI como
# Invoke-WebRequest fallan la verificacion del certificado.
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = '1'

$isDryRun = [bool]($DryRun -or $WhatIf)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$deploymentsFile = Join-Path $repoRoot "infra/ai/deployments.$Environment.json"
$resourcesFile = Join-Path $repoRoot "infra/ai/resources.$Environment.json"
$armBase = 'https://management.azure.com'
$runningStates = @('accepted', 'creating', 'updating', 'running', 'notstarted', 'inprogress', 'scaling')
$failedStates = @('failed', 'canceled', 'cancelled', 'deleting', 'disabled')

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
function Get-ArmToken {
    $raw = az account get-access-token --resource https://management.azure.com --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $raw) {
        throw "no se pudo obtener el token de ARM (az account get-access-token, exit $LASTEXITCODE). Ejecuta 'az login'."
    }
    return $raw.Trim()
}

function Invoke-Arm {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Token,
        [object]$Body
    )
    $headers = @{ Authorization = "Bearer $Token" }
    $params = @{ Uri = $Url; Method = $Method; Headers = $headers; UseBasicParsing = $true; SkipCertificateCheck = $true }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 20
        $params['Body'] = [Text.Encoding]::UTF8.GetBytes($json)
        $params['ContentType'] = 'application/json; charset=utf-8'
    }
    try {
        $resp = Invoke-WebRequest @params
        $text = ''
        if ($resp.RawContentStream) { $text = [Text.Encoding]::UTF8.GetString($resp.RawContentStream.ToArray()) }
        $content = if ($text.Trim()) { $text | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Content = $content; Error = $null }
    } catch {
        $status = 0
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        $err = ''
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $err = $_.ErrorDetails.Message } elseif ($_.Exception.Message) { $err = $_.Exception.Message }
        $content = $null
        try { if ($err.Trim().StartsWith('{')) { $content = $err | ConvertFrom-Json } } catch { }
        return [pscustomobject]@{ Status = $status; Content = $content; Error = $err }
    }
}

function Get-ArmErrorText {
    param($Response)
    if ($Response.Content -and $Response.Content.error) {
        $e = $Response.Content.error
        return ("{0}: {1}" -f $e.code, $e.message)
    }
    if ($Response.Error) { return $Response.Error }
    return "HTTP $($Response.Status)"
}

function Resolve-AccountScope {
    # Devuelve subscriptionId y resourceGroup de una cuenta a partir de
    # resources.<env>.json (primer alias con ese account y subscriptionId).
    param($Resources, [string]$AccountName)
    $withSub = $null
    $anyRg = $null
    foreach ($p in $Resources.PSObject.Properties) {
        $r = $p.Value
        if ($r.account -ne $AccountName) { continue }
        if (-not $anyRg -and $r.resourceGroup) { $anyRg = $r.resourceGroup }
        if ($r.subscriptionId -and $r.resourceGroup) { $withSub = $r; break }
    }
    if (-not $withSub) {
        $hint = if ($anyRg) { "tiene resourceGroup '$anyRg' pero ningun alias con subscriptionId" } else { 'no aparece en ningun alias' }
        throw "la cuenta '$AccountName' $hint en $resourcesFile; anade subscriptionId al alias correspondiente."
    }
    return [pscustomobject]@{ SubscriptionId = $withSub.subscriptionId; ResourceGroup = $withSub.resourceGroup }
}

function Get-DeploymentUrl {
    param($Scope, [string]$AccountName, [string]$DeploymentName)
    $path = "/subscriptions/$($Scope.SubscriptionId)/resourceGroups/$($Scope.ResourceGroup)/providers/Microsoft.CognitiveServices/accounts/$AccountName/deployments"
    if ($DeploymentName) { $path += "/$DeploymentName" }
    return "$armBase$path`?api-version=$ApiVersion"
}

function Get-DeploymentDiff {
    # Claves en las que el deployment existente difiere del declarado.
    param($Existing, $Desired)
    $diff = @()
    if ($Existing.properties.model.name -ne $Desired.model) { $diff += 'model' }
    if ([string]$Existing.properties.model.version -ne [string]$Desired.version) { $diff += 'version' }
    if ($Existing.sku.name -ne $Desired.sku) { $diff += 'sku' }
    if ([int]$Existing.sku.capacity -ne [int]$Desired.capacity) { $diff += 'capacity' }
    return $diff
}

function Format-Desired {
    param($Desired)
    return ("{0} {1}, {2} x{3}" -f $Desired.model, $Desired.version, $Desired.sku, $Desired.capacity)
}

function Format-Existing {
    param($Existing)
    return ("{0} {1}, {2} x{3}" -f $Existing.properties.model.name, $Existing.properties.model.version, $Existing.sku.name, $Existing.sku.capacity)
}

# -----------------------------------------------------------------------------
# Carga y validacion del fichero deseado
# -----------------------------------------------------------------------------
if (-not (Test-Path $deploymentsFile)) { throw "no existe $deploymentsFile" }
if (-not (Test-Path $resourcesFile)) { throw "no existe $resourcesFile" }
$desired = Get-Content -Raw $deploymentsFile | ConvertFrom-Json
$resources = (Get-Content -Raw $resourcesFile | ConvertFrom-Json).resources

if ($desired.intent -ne 'desired') {
    throw "$deploymentsFile tiene intent '$($desired.intent)'; solo se aplican ficheros con intent 'desired'."
}
if ($desired.environment -ne $Environment) {
    throw "$deploymentsFile declara environment '$($desired.environment)' y se ha pedido '$Environment'."
}

$accountNames = @($desired.accounts.PSObject.Properties.Name)
if ($Account) {
    $unknown = @($Account | Where-Object { $accountNames -notcontains $_ })
    if ($unknown.Count) { throw "cuentas no declaradas en $deploymentsFile : $($unknown -join ', ')" }
    $accountNames = @($accountNames | Where-Object { $Account -contains $_ })
}

# Coherencia interna: nombres duplicados y defaults de CU que apunten a
# deployments no declarados.
$fileErrors = @()
foreach ($name in $desired.accounts.PSObject.Properties.Name) {
    $list = @($desired.accounts.$name)
    $dups = @($list | Group-Object name | Where-Object Count -gt 1 | ForEach-Object Name)
    if ($dups.Count) { $fileErrors += "cuenta $name : deployment repetido $($dups -join ', ')" }
    foreach ($d in $list) {
        foreach ($k in 'name', 'model', 'version', 'sku', 'capacity') {
            if ($null -eq $d.$k -or [string]$d.$k -eq '') { $fileErrors += "cuenta $name : deployment '$($d.name)' sin '$k'" }
        }
    }
    if ($desired.contentUnderstandingDefaults -and $desired.contentUnderstandingDefaults.$name) {
        foreach ($p in $desired.contentUnderstandingDefaults.$name.PSObject.Properties) {
            if ($list.name -notcontains $p.Value) {
                $fileErrors += "cuenta $name : contentUnderstandingDefaults.$($p.Name) apunta a '$($p.Value)', que no esta en accounts"
            }
        }
    }
}
if ($fileErrors.Count) {
    $fileErrors | ForEach-Object { Write-Host "  error  $_" -ForegroundColor Red }
    throw "$deploymentsFile no es coherente; corrige el fichero antes de aplicar."
}

$modeText = if ($isDryRun) { 'DRY-RUN (sin escrituras)' } else { 'APLICAR' }
Write-Host "== apply-deployments: entorno $Environment, modo $modeText ==" -ForegroundColor Cyan
Write-Host "   fichero: $deploymentsFile"

# -----------------------------------------------------------------------------
# Proceso por cuenta
# -----------------------------------------------------------------------------
$token = Get-ArmToken
$results = [System.Collections.Generic.List[object]]::new()

foreach ($accountName in $accountNames) {
    $scope = Resolve-AccountScope -Resources $resources -AccountName $accountName
    Write-Host ""
    Write-Host "-- $accountName (rg $($scope.ResourceGroup), sub $($scope.SubscriptionId.Substring(0, 8))...)" -ForegroundColor Yellow

    $listResp = Invoke-Arm -Method GET -Url (Get-DeploymentUrl -Scope $scope -AccountName $accountName) -Token $token
    if ($listResp.Status -ne 200) {
        throw "no se pudieron listar los deployments de $accountName : $(Get-ArmErrorText $listResp)"
    }
    $existing = @($listResp.Content.value)
    $existingByName = @{}
    foreach ($e in $existing) { $existingByName[$e.name] = $e }

    foreach ($d in @($desired.accounts.$accountName)) {
        $row = [ordered]@{ Account = $accountName; Deployment = $d.name; Desired = (Format-Desired $d); Action = ''; State = ''; Detail = '' }
        $cur = $existingByName[$d.name]
        if ($cur) {
            $diff = Get-DeploymentDiff -Existing $cur -Desired $d
            $row.State = $cur.properties.provisioningState
            if ($diff.Count -eq 0) {
                $row.Action = 'ok'
                Write-Host "  ok        $($d.name)  ($(Format-Desired $d))"
            } else {
                $row.Action = 'differs'
                $row.Detail = "existente: $(Format-Existing $cur); difiere en $($diff -join ', ')"
                Write-Host "  differs   $($d.name)  declarado $(Format-Desired $d) / existente $(Format-Existing $cur) [$($diff -join ', ')]; no se modifica" -ForegroundColor DarkYellow
            }
            $results.Add([pscustomobject]$row)
            continue
        }

        Write-Host "  create    $($d.name)  ($(Format-Desired $d))" -ForegroundColor Green
        if ($isDryRun) {
            $row.Action = 'create (dry-run)'
            $results.Add([pscustomobject]$row)
            continue
        }

        $body = @{
            sku        = @{ name = $d.sku; capacity = [int]$d.capacity }
            properties = @{ model = @{ format = 'OpenAI'; name = $d.model; version = [string]$d.version } }
        }
        $url = Get-DeploymentUrl -Scope $scope -AccountName $accountName -DeploymentName $d.name
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $put = Invoke-Arm -Method PUT -Url $url -Token $token -Body $body
        if ($put.Status -notin 200, 201, 202) {
            $row.Action = 'failed'
            $row.Detail = Get-ArmErrorText $put
            Write-Host "  failed    $($d.name)  $($row.Detail)" -ForegroundColor Red
            $results.Add([pscustomobject]$row)
            continue
        }

        # Sondeo del provisioningState del propio recurso.
        $state = [string]$put.Content.properties.provisioningState
        $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
        while ($state.ToLowerInvariant() -in $runningStates -or -not $state) {
            if ((Get-Date) -gt $deadline) { break }
            Start-Sleep -Seconds $PollSeconds
            $get = Invoke-Arm -Method GET -Url $url -Token $token
            if ($get.Status -ne 200) { $row.Detail = Get-ArmErrorText $get; break }
            $state = [string]$get.Content.properties.provisioningState
        }
        $sw.Stop()
        $row.State = $state
        $lower = $state.ToLowerInvariant()
        if ($lower -eq 'succeeded') {
            $row.Action = 'created'
            Write-Host "  created   $($d.name)  en $([int]$sw.Elapsed.TotalSeconds) s" -ForegroundColor Green
        } elseif ($lower -in $failedStates) {
            $row.Action = 'failed'
            if (-not $row.Detail) { $row.Detail = "provisioningState $state" }
            Write-Host "  failed    $($d.name)  $($row.Detail)" -ForegroundColor Red
        } else {
            $row.Action = 'timeout'
            if (-not $row.Detail) { $row.Detail = "provisioningState '$state' tras $TimeoutMinutes min; revisar en el portal" }
            Write-Host "  timeout   $($d.name)  $($row.Detail)" -ForegroundColor DarkYellow
        }
        $results.Add([pscustomobject]$row)
    }

    $declaredNames = @($desired.accounts.$accountName | ForEach-Object name)
    foreach ($e in $existing) {
        if ($declaredNames -contains $e.name) { continue }
        Write-Host "  unmanaged $($e.name)  ($(Format-Existing $e)); no esta en el fichero, no se toca" -ForegroundColor DarkGray
        $results.Add([pscustomobject]@{ Account = $accountName; Deployment = $e.name; Desired = ''; Action = 'unmanaged'; State = $e.properties.provisioningState; Detail = (Format-Existing $e) })
    }
}

# -----------------------------------------------------------------------------
# Resumen
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "== resumen ==" -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String -Width 220 | Write-Host

$failed = @($results | Where-Object { $_.Action -in 'failed', 'timeout' })
$differs = @($results | Where-Object Action -eq 'differs')
if ($differs.Count) {
    Write-Host "$($differs.Count) deployment(s) existen con otra definicion; este script no los modifica." -ForegroundColor DarkYellow
}
if ($failed.Count) {
    Write-Host "$($failed.Count) deployment(s) fallidos o sin terminar." -ForegroundColor Red
    exit 1
}
