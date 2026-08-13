<#
.SYNOPSIS
    Crea (idempotente) el Test Plan "E2E Post-despliegue DocumentIA" con suites
    Smoke y Full, un Test Case WI por caso de la bateria, y escribe
    cases/ado-mapping.json.
.DESCRIPTION
    Reutiliza el patron REST de tests/api-tests/ado-testplans-common.ps1. Se
    autentica con $env:ADO_PAT (Basic) si esta disponible; si no, cae de forma
    automatica a un token AAD de la sesion `az` activa (Bearer), valido para
    la Azure DevOps Test Plans/Work Items REST API (resource
    499b84ac-1321-427f-aa17-267ca6975798).
.NOTES
    Idempotente: reejecutar no crea plan/suites/Test Cases duplicados; solo
    escribe de nuevo cases/ado-mapping.json (y solo si el 100% de las
    asociaciones caso-suite de esta ejecucion fueron correctas).
#>
param(
    [string]$Org = "https://sareb.visualstudio.com",
    [string]$Project = "AI DocClassExt",
    [string]$PlanName = "E2E Post-despliegue DocumentIA",
    [string]$Pat = $env:ADO_PAT,
    [string]$ConfigPath = ""
)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# ── Autenticacion: PAT (Basic) con prioridad; si no hay, adoPat de
#    environments.json; si tampoco, token AAD (Bearer) de la sesion 'az' ──
function Get-AdoPatFromConfig {
    param([string]$ConfigPath)
    if ([string]::IsNullOrWhiteSpace($ConfigPath) -or -not (Test-Path -Path $ConfigPath)) { return "" }
    try {
        $cfg = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
    }
    catch { return "" }
    if ($null -ne $cfg.PSObject.Properties['adoPat']) { return [string]$cfg.adoPat }
    return ""
}

function Get-AdoBearerToken {
    # az puede escribir avisos (p.ej. InsecureRequestWarning) por stderr sin
    # que la llamada falle; se aisla el ErrorActionPreference para no
    # convertir esos avisos en excepciones terminantes.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $token = az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv 2>$null
    }
    catch { $token = $null }
    finally { $ErrorActionPreference = $prevEap }
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) { return $null }
    return $token.Trim()
}

function Resolve-AdoAuthHeaders {
    param([string]$Pat, [string]$ConfigPath)
    $resolvedPat = $Pat
    if ([string]::IsNullOrWhiteSpace($resolvedPat)) { $resolvedPat = Get-AdoPatFromConfig -ConfigPath $ConfigPath }
    if (-not [string]::IsNullOrWhiteSpace($resolvedPat)) {
        $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$resolvedPat"))
        return @{ Authorization = "Basic $base64Auth" }
    }
    $token = Get-AdoBearerToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Falta ADO_PAT/adoPat y no hay sesion 'az' con acceso a Azure DevOps. Define `$env:ADO_PAT, informa 'adoPat' en environments.json o ejecuta 'az login'."
    }
    Write-Host "[ADO] ADO_PAT/adoPat no informado; usando token AAD de la sesion 'az' (fallback)." -ForegroundColor DarkGray
    return @{ Authorization = "Bearer $token" }
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot ".." "config" "environments.json"
}
$headers = Resolve-AdoAuthHeaders -Pat $Pat -ConfigPath $ConfigPath
$proj = [Uri]::EscapeDataString($Project)

# 1) Plan (buscar por nombre; crear si no existe)
$plans = Invoke-RestMethod -Uri "$Org/$proj/_apis/testplan/plans?api-version=7.1" -Headers $headers -Method Get
$plan = @($plans.value) | Where-Object { $_.name -eq $PlanName } | Select-Object -First 1
if ($null -eq $plan) {
    $body = @{ name = $PlanName } | ConvertTo-Json
    $plan = Invoke-RestMethod -Uri "$Org/$proj/_apis/testplan/plans?api-version=7.1" -Method Post -Headers $headers -Body $body -ContentType "application/json"
    Write-Host "Plan creado: $($plan.id)"
}
else { Write-Host "Plan existente: $($plan.id)" }
$rootSuiteId = [int]$plan.rootSuite.id

# 2) Suites Smoke / Full bajo la raiz
function Get-OrCreateSuite {
    param([string]$Name)
    $suites = Invoke-RestMethod -Uri "$Org/$proj/_apis/testplan/plans/$($plan.id)/suites?api-version=7.1" -Headers $headers -Method Get
    $suite = @($suites.value) | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    if ($null -ne $suite) {
        Write-Host "Suite existente: $($suite.id) ($Name)"
        return $suite
    }
    $body = @{ suiteType = "staticTestSuite"; name = $Name; parentSuite = @{ id = $rootSuiteId } } | ConvertTo-Json
    $created = Invoke-RestMethod -Uri "$Org/$proj/_apis/testplan/plans/$($plan.id)/suites?api-version=7.1" -Method Post -Headers $headers -Body $body -ContentType "application/json"
    Write-Host "Suite creada: $($created.id) ($Name)"
    return $created
}
$suiteSmoke = Get-OrCreateSuite -Name "Smoke"
$suiteFull = Get-OrCreateSuite -Name "Full"

# 3) Cargar casos de la bateria
$casesDir = Join-Path $PSScriptRoot ".." "cases"
$allCases = @(Get-ChildItem $casesDir -Filter "*-cases.json" | ForEach-Object { Get-Content -Raw $_.FullName | ConvertFrom-Json } | ForEach-Object { $_ })
Write-Host "Casos cargados: $($allCases.Count)"

# 4) Test Case WI por caso (buscar por prefijo estable de caseKey; crear si
#    falta; si el titulo cambio de nombre, actualizar el WI existente en vez
#    de crear un duplicado) y anadirlo a su suite
$mapping = @{}
$created = 0
$linked = 0
$associationErrors = @()
foreach ($case in $allCases) {
    $title = "[E2E-PD] $($case.caseKey) - $($case.name)"
    $titlePrefix = "[E2E-PD] $($case.caseKey) -"
    $wiql = @{ query = "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Test Case' AND [System.Title] CONTAINS '$($titlePrefix.Replace("'", "''"))'" } | ConvertTo-Json
    $found = Invoke-RestMethod -Uri "$Org/$proj/_apis/wit/wiql?api-version=7.1" -Method Post -Headers $headers -Body $wiql -ContentType "application/json"
    if (@($found.workItems).Count -gt 0) {
        $tcId = [int]$found.workItems[0].id
        $wi = Invoke-RestMethod -Uri "$Org/$proj/_apis/wit/workitems/${tcId}?api-version=7.1" -Headers $headers -Method Get
        $currentTitle = $wi.fields.'System.Title'
        if ($currentTitle -ne $title) {
            $patch = @(@{ op = "replace"; path = "/fields/System.Title"; value = $title }) | ConvertTo-Json -AsArray
            Invoke-RestMethod -Uri "$Org/$proj/_apis/wit/workitems/${tcId}?api-version=7.1" -Method Patch -Headers $headers -Body $patch -ContentType "application/json-patch+json" | Out-Null
            Write-Host "Test Case actualizado (titulo cambiado): $tcId ('$currentTitle' -> '$title')"
        }
    }
    else {
        $patch = @(@{ op = "add"; path = "/fields/System.Title"; value = $title }) | ConvertTo-Json -AsArray
        $wi = Invoke-RestMethod -Uri "$Org/$proj/_apis/wit/workitems/`$Test%20Case?api-version=7.1" -Method Post -Headers $headers -Body $patch -ContentType "application/json-patch+json"
        $tcId = [int]$wi.id
        $created++
        Write-Host "Test Case creado: $tcId ($title)"
    }
    $suite = if (@($case.profiles) -contains "smoke") { $suiteSmoke } else { $suiteFull }
    # Asociar a la suite (endpoint del area Test Management, no Test Plans; ver
    # skill azure-devops-testplans). Idempotente: reasociar no duplica.
    try {
        Invoke-RestMethod -Uri "$Org/$proj/_apis/test/Plans/$($plan.id)/suites/$($suite.id)/testcases/${tcId}?api-version=7.1" -Method Post -Headers $headers -Body "{}" -ContentType "application/json" | Out-Null
        $linked++
    }
    catch {
        $associationErrors += "$($case.caseKey): Test Case $tcId no se pudo asociar a suite $($suite.id): $($_.Exception.Message)"
        Write-Host "[WARN] No se pudo asociar Test Case $tcId a suite $($suite.id): $($_.Exception.Message)" -ForegroundColor Yellow
    }
    $mapping[$case.caseKey] = @{ testCaseId = $tcId; suiteId = [int]$suite.id }
}
Write-Host "Test Cases nuevos: $created | Asociaciones OK: $linked / $($allCases.Count)"

if ($associationErrors.Count -gt 0) {
    Write-Host ""
    Write-Host "[ERROR] Fallaron $($associationErrors.Count) de $($allCases.Count) asociaciones caso-suite:" -ForegroundColor Red
    foreach ($e in $associationErrors) { Write-Host " - $e" -ForegroundColor Red }
    Write-Host "[ERROR] No se escribe cases/ado-mapping.json: solo se persiste si el 100% de las asociaciones de esta ejecucion fueron correctas." -ForegroundColor Red
    exit 1
}

# 5) Escribir mapping (solo si el 100% de las asociaciones caso-suite fueron OK)
$outPath = Join-Path $casesDir "ado-mapping.json"
$mappingObj = [ordered]@{
    planId     = [int]$plan.id
    planName   = $PlanName
    rootSuiteId = $rootSuiteId
    cases      = $mapping
}
$json = $mappingObj | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($outPath, $json + "`n", (New-Object System.Text.UTF8Encoding($true)))
Write-Host "Mapping escrito en $outPath"
