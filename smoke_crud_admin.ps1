# ============================================================================
# Smoke Tests E2E para CRUD Admin - TDN1 y TDN2 Catalogs
# Propósito: Validar endpoints de gestión de catálogos administrativos
# AB#99768: QA Validation CRUD for Admin endpoints
# ============================================================================

param(
    [string]$baseUrl = "http://localhost:7071/api",
    [int]$timeout = 300  # 5 min total timeout
)

$ErrorActionPreference = "Continue"
$VerbosePreference = "Continue"

function Log-Result {
    param(
        [string]$TestName,
        [string]$Result,  # "PASS", "FAIL", "SKIP"
        [string]$Details = ""
    )
    
    $timestamp = Get-Date -Format "HH:mm:ss.fff"
    $resultColor = if ($Result -eq "PASS") { "Green" } 
                  elseif ($Result -eq "FAIL") { "Red" }
                  else { "Yellow" }
    
    Write-Host "[${timestamp}] ${TestName}: " -NoNewline
    Write-Host $Result -ForegroundColor $resultColor
    if ($Details) { Write-Host "       └─ $Details" }
}

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Uri,
        [string]$Method = "GET",
        [object]$Body = $null,
        [int]$ExpectedCode = 200
    )
    
    try {
        $params = @{
            Uri = $Uri
            Method = $Method
            ErrorAction = "Stop"
        }
        
        if ($null -ne $Body) {
            if ($Body -is [string]) {
                $params["Body"] = $Body
            } else {
                $params["Body"] = $Body | ConvertTo-Json -Depth 10
            }
            $params["ContentType"] = "application/json"
        }
        
        $response = Invoke-RestMethod @params
        
        if ($response.value -or $response.id -or $response -is [array] -or ($response -is [object] -and $response.PSObject.Properties.Count -gt 0)) {
            Log-Result $Name "PASS" "Status: 200, Response received"
            return $response
        } else {
            Log-Result $Name "FAIL" "Empty response"
            return $null
        }
    }
    catch [System.Net.Http.HttpRequestException] {
        $statusCode = $_.Exception.Response.StatusCode.Value__
        if ($statusCode -eq $ExpectedCode) {
            Log-Result $Name "PASS" "Expected error code: $statusCode"
            return $null
        } else {
            $errorDetail = ""
            if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
                $errorDetail = " Body=$($_.ErrorDetails.Message)"
            }

            Log-Result $Name "FAIL" "HTTP $statusCode (expected $ExpectedCode): $($_.Exception.Message)$errorDetail"
            return $null
        }
    }
    catch {
        Log-Result $Name "FAIL" "$($_.Exception.Message)"
        return $null
    }
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($null -eq $prop) {
        return $null
    }

    return $prop.Value
}

# ─────────────────────────────────────────────────────────────────────────────
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SMOKE E2E TESTS: Admin CRUD Endpoints" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Base URL: $baseUrl" -ForegroundColor Gray
Write-Host ""

$testCount = 0
$passCount = 0
$failCount = 0
$startTime = Get-Date

# TEST 1: Health check - Wait for service to be ready
Write-Host "=== Phase 1: Service Health Check ===" -ForegroundColor Yellow
$testCount++
$readyCheck = $false
$checkStart = Get-Date

while (((Get-Date) - $checkStart).TotalSeconds -lt 30) {
    try {
        $health = Test-Endpoint "GET /management/tipologias" "$baseUrl/management/tipologias"
        if ($health) {
            $readyCheck = $true
            $passCount++
            break
        }
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $readyCheck) {
    Log-Result "Service Ready" "FAIL" "Timeout waiting for API"
    exit 1
}

Write-Host ""

# TEST 2-4: GET Catalog Endpoints
Write-Host "=== Phase 2: GET Catalog Queries ===" -ForegroundColor Yellow

$testCount++
$catalogos1 = Test-Endpoint "GET /management/catalogotdn1" "$baseUrl/management/catalogotdn1"
if ($catalogos1) { $passCount++ } else { $failCount++ }

Write-Host ""
$testCount++
$catalogos2 = Test-Endpoint "GET /management/catalogotdn2" "$baseUrl/management/catalogotdn2"
if ($catalogos2) { $passCount++ } else { $failCount++ }

Write-Host ""

# TEST 5: GET by TDN1 Code (query parameter)
Write-Host "=== Phase 3: Catalog Query by TDN1 Code ===" -ForegroundColor Yellow
$testCount++
$queryByCode = Test-Endpoint "GET /management/catalogotdn2/by-tdn1/ACTE" "$baseUrl/management/catalogotdn2/by-tdn1/ACTE"
if ($queryByCode) { $passCount++ } else { $failCount++ }

Write-Host ""

# TEST 6-7: Create new catalog entries (POST)
Write-Host "=== Phase 4: Create Catalog Entries ===" -ForegroundColor Yellow

$timestamp = Get-Date -Format "HHmmss"
$newCatalog1 = @{
    codigo = "SMK$timestamp"
    nombre = "Smoke Test TDN1 Entry"
    descripcion = "Smoke test catalog tdn1"
}

$testCount++
$created1 = Test-Endpoint "POST /management/catalogotdn1" "$baseUrl/management/catalogotdn1" "POST" $newCatalog1 201
if ($created1) { $passCount++ } else { $failCount++ }

Write-Host ""

$newCatalog2 = @{
    codigo = "SMKT2$timestamp"
    nombre = "Smoke Test TDN2 Entry"
    codigoTdn1 = "ACTE"
    descripcion = "Smoke test catalog tdn2"
}

$testCount++
$created2 = Test-Endpoint "POST /management/catalogotdn2" "$baseUrl/management/catalogotdn2" "POST" $newCatalog2 201
if ($created2) { $passCount++ } else { $failCount++ }

Write-Host ""

# TEST 8: Verify created entries can be retrieved
Write-Host "=== Phase 5: Verify Created Entries ===" -ForegroundColor Yellow

$testCount++
$verify1 = Test-Endpoint "GET /management/catalogotdn1?filter=smoke" "$baseUrl/management/catalogotdn1"
$createdTdn1Code = Get-PropertyValue $created1 "codigo"
$createdTdn1Id = Get-PropertyValue $created1 "id"

if ($verify1 -and ($verify1 | Where-Object { (Get-PropertyValue $_ "codigo") -eq $createdTdn1Code })) {
    Log-Result "Verify TDN1 Entry" "PASS" "Entry found in catalog"
    $passCount++
} else {
    Log-Result "Verify TDN1 Entry" "FAIL" "Entry not found after creation"
    $failCount++
}

Write-Host ""

# TEST 9: Update catalog entry (PUT)
Write-Host "=== Phase 6: Update Catalog Entries ===" -ForegroundColor Yellow

$updateCatalog = @{
    codigo = $createdTdn1Code
    nombre = "Smoke Test TDN1 Entry (Updated)"
    descripcion = "Smoke test catalog tdn1 updated"
}

$testCount++
$updated = Test-Endpoint "PUT /management/catalogotdn1/{id}" "$baseUrl/management/catalogotdn1/$createdTdn1Id" "PUT" $updateCatalog 200
if ($updated) { $passCount++ } else { $failCount++ }

Write-Host ""

# TEST 10: Delete catalog entry (DELETE)
Write-Host "=== Phase 7: Delete Catalog Entries ===" -ForegroundColor Yellow

$testCount++
$deleteUri = "$baseUrl/management/catalogotdn1/$createdTdn1Id"
try {
    Invoke-RestMethod -Uri $deleteUri -Method DELETE -ErrorAction Stop | Out-Null
    Log-Result "DELETE /management/catalogotdn1/{id}" "PASS" "Entry deleted successfully"
    $passCount++
} catch {
    Log-Result "DELETE /management/catalogotdn1/{id}" "FAIL" "$($_.Exception.Message)"
    $failCount++
}

Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# SUMMARY
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SMOKE TEST SUMMARY" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Cyan

$duration = (Get-Date) - $startTime

Write-Host ""
Write-Host "Total Tests:  $testCount" -ForegroundColor Gray
Write-Host "PASS:         " -NoNewline -ForegroundColor Gray
Write-Host $passCount -ForegroundColor Green
Write-Host "FAIL:         " -NoNewline -ForegroundColor Gray
Write-Host $failCount -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "Duration:     $($duration.ToString('mm\:ss'))" -ForegroundColor Gray

Write-Host ""

if ($failCount -eq 0) {
    Write-Host "✓ All smoke tests PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ Some tests FAILED - Please review" -ForegroundColor Red
    exit 1
}
