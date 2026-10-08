#Requires -Version 7.0
<#
.SYNOPSIS
    Ensure-Defaults: comprueba y, si hace falta, fija los defaults de Azure AI
    Content Understanding (GET/PATCH /contentunderstanding/defaults) de una
    cuenta Foundry a partir de infra/ai/deployments.<env>.json.

.DESCRIPTION
    Libreria compartida: se carga con dot-source desde build-analyzers.ps1 y
    copy-cu-analyzers.ps1 (". $PSScriptRoot/lib/cu-defaults.ps1"). No hace
    llamadas HTTP por si misma: usa la funcion Invoke-Cu del script que la
    carga (mismo contrato en los dos: Status, Content, Headers, Error).

    Sin defaults el servicio rechaza cualquier build (DefaultsNotSet) y, aunque
    la Copy API no los exige, un analyzer copiado sin ellos falla en el primer
    analisis ("needs a 'completion' model deployment ... but none was
    resolved", visto en PRE el 2026-09-22). Por eso los dos caminos de
    promocion pasan por aqui antes de tocar la cuenta.

    Reglas:
      - El mapeo deseado es contentUnderstandingDefaults.<cuenta> del fichero
        de deployments; cada deployment referenciado debe figurar en
        accounts.<cuenta> (consistencia declarativa, se comprueba antes del
        PATCH).
      - Se fusiona con lo que la cuenta ya tenga: solo se PATCHea si falta o
        difiere algun alias declarado; los alias no declarados se conservan.
      - -RequiredAliases (los valores de "models" de los analyzers) deben
        quedar mapeados tras la fusion; si no, se para con error antes de
        escribir.
      - Con -DryRun se muestra el plan y no se hace PATCH.

    Devuelve el mapeo alias -> deployment que queda (o quedaria) vigente.
#>

function Ensure-Defaults {
    param(
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)][string]$Account,
        [Parameter(Mandatory)][string]$Token,
        [Parameter(Mandatory)][object]$Deployments,
        [string]$DeploymentsFile = 'deployments.<env>.json',
        [string[]]$RequiredAliases = @(),
        [string]$ApiVersion = '2025-11-01',
        [switch]$DryRun
    )
    $url = "$($Endpoint.TrimEnd('/'))/contentunderstanding/defaults?api-version=$ApiVersion"
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
    $decl = $Deployments.contentUnderstandingDefaults
    if ($decl -and $decl.PSObject.Properties.Name -contains $Account) {
        foreach ($p in $decl.$Account.PSObject.Properties) { $desired[$p.Name] = $p.Value }
    }
    # Consistencia declarativa: cada deployment del mapeo debe existir en la lista de la cuenta.
    $declaredDeployments = @()
    if ($Deployments.accounts -and $Deployments.accounts.PSObject.Properties.Name -contains $Account) {
        $declaredDeployments = @($Deployments.accounts.$Account | ForEach-Object { $_.name })
    }
    foreach ($alias in $desired.Keys) {
        if ($declaredDeployments -notcontains $desired[$alias]) {
            throw "contentUnderstandingDefaults.$Account.$alias apunta al deployment '$($desired[$alias])', que no figura en accounts.$Account de $DeploymentsFile"
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
    $missing = @($RequiredAliases | Where-Object { $_ -and -not $merged.ContainsKey($_) })
    if ($missing.Count -gt 0) {
        throw "los analyzers necesitan los alias [$($missing -join ', ')] y ni los defaults actuales de $Account ni contentUnderstandingDefaults.$Account en $DeploymentsFile los mapean"
    }
    if ($changes.Count -eq 0) {
        Write-Host "      defaults: OK ($($merged.Count) alias)" -ForegroundColor Green
        return $merged
    }
    Write-Host "      defaults: PATCH necesario" -ForegroundColor Yellow
    foreach ($c in $changes) { Write-Host "        $c" }
    if ($DryRun) {
        Write-Host "      [dry-run] no se hace PATCH /contentunderstanding/defaults" -ForegroundColor DarkGray
        return $merged
    }
    $patch = Invoke-Cu -Method Patch -Url $url -Token $Token -Body @{ modelDeployments = $merged }
    if ($patch.Status -notin 200, 201, 204) { throw "PATCH defaults de $Account -> HTTP $($patch.Status): $($patch.Error)" }
    Write-Host "      defaults: actualizados" -ForegroundColor Green
    return $merged
}
