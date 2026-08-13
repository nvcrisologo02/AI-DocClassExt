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
    escribe de nuevo cases/ado-mapping.json.
#>
param(
    [string]$Org = "https://sareb.visualstudio.com",
    [string]$Project = "AI DocClassExt",
    [string]$PlanName = "E2E Post-despliegue DocumentIA",
    [string]$Pat = $env:ADO_PAT
)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# ── Autenticacion: PAT (Basic) con prioridad; si no hay, token AAD (Bearer) ──
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
    param([string]$Pat)
    if (-not [string]::IsNullOrWhiteSpace($Pat)) {
        $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$Pat"))
        return @{ Authorization = "Basic $base64Auth" }
    }
    $token = Get-AdoBearerToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Falta ADO_PAT y no hay sesion 'az' con acceso a Azure DevOps. Define `$env:ADO_PAT o ejecuta 'az login'."
    }
    Write-Host "[ADO] ADO_PAT no informado; usando token AAD de la sesion 'az' (fallback)." -ForegroundColor DarkGray
    return @{ Authorization = "Bearer $token" }
}

$headers = Resolve-AdoAuthHeaders -Pat $Pat
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

# 4) Test Case WI por caso (buscar por titulo exacto; crear si falta) y anadirlo a su suite
$mapping = @{}
$created = 0
$linked = 0
foreach ($case in $allCases) {
    $title = "[E2E-PD] $($case.caseKey) - $($case.name)"
    $wiql = @{ query = "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Test Case' AND [System.Title] = '$($title.Replace("'", "''"))'" } | ConvertTo-Json
    $found = Invoke-RestMethod -Uri "$Org/$proj/_apis/wit/wiql?api-version=7.1" -Method Post -Headers $headers -Body $wiql -ContentType "application/json"
    if (@($found.workItems).Count -gt 0) {
        $tcId = [int]$found.workItems[0].id
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
        Write-Host "[WARN] No se pudo asociar Test Case $tcId a suite $($suite.id): $($_.Exception.Message)" -ForegroundColor Yellow
    }
    $mapping[$case.caseKey] = @{ testCaseId = $tcId; suiteId = [int]$suite.id }
}
Write-Host "Test Cases nuevos: $created | Asociaciones OK: $linked / $($allCases.Count)"

# 5) Escribir mapping
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
