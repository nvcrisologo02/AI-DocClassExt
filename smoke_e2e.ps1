function Log-Step($step, $status, $payload = "", $response = "") {
    Write-Host "--- STEP: $step ---"
    Write-Host "STATUS: $status"
    if ($payload) { Write-Host "PAYLOAD: $payload" }
    if ($response) { Write-Host "RESPONSE: $response" }
    Write-Host "-------------------"
}

$baseUrl = "http://localhost:7071/api"
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$tipologiaId = "SmokeTest_$timestamp"
$versionA = "1.0.0"
$versionB = "1.1.0"
$results = @{ Total = 9; Pass = 0; Fail = 0 }

# 1) Wait for GET /management/tipologias
$start = Get-Date
$ready = $false
while (((Get-Date) - $start).TotalSeconds -lt 60) {
    try {
        $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias" -Method Get -ErrorAction Stop
        $ready = $true
        break
    } catch {
        Start-Sleep -Seconds 2
    }
}
if ($ready) {
    Log-Step "1. Wait for service" "PASS"
    $results.Pass++
} else {
    Log-Step "1. Wait for service" "FAIL" "Timeout 60s"
    $results.Fail++
}

# 2) POST Tipologia A
$bodyA = @{
    tipologiaId = "$tipologiaId"
    version = "$versionA"
    extraction = @{ enabled = $false }
    promptConfig = @{ enabled = $false }
    fields = @()
} | ConvertTo-Json
try {
    $resA = Invoke-RestMethod -Uri "$baseUrl/management/tipologias" -Method Post -Body $bodyA -ContentType "application/json"
    Log-Step "2. Create Tipologia A" "PASS" $bodyA ($resA | ConvertTo-Json -Compress)
    $results.Pass++
} catch {
    Log-Step "2. Create Tipologia A" "FAIL" $bodyA $_.Exception.Message
    $results.Fail++
}

# 3) POST Tipologia B
$bodyB = @{
    tipologiaId = "$tipologiaId"
    version = "$versionB"
    extraction = @{ enabled = $false }
    promptConfig = @{ enabled = $false }
    fields = @()
} | ConvertTo-Json
try {
    $resB = Invoke-RestMethod -Uri "$baseUrl/management/tipologias" -Method Post -Body $bodyB -ContentType "application/json"
    Log-Step "3. Create Tipologia B" "PASS" $bodyB ($resB | ConvertTo-Json -Compress)
    $results.Pass++
} catch {
    Log-Step "3. Create Tipologia B" "FAIL" $bodyB $_.Exception.Message
    $results.Fail++
}

# 4) GET Versions
try {
    $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/$tipologiaId/versions" -Method Get
    $versions = $res | Select-Object -ExpandProperty version
    if ($versions -contains $versionA -and $versions -contains $versionB) {
        Log-Step "4. Get Versions" "PASS" "" ($res | ConvertTo-Json -Compress)
        $results.Pass++
    } else {
        Log-Step "4. Get Versions" "FAIL" "" "Expected versions not found"
        $results.Fail++
    }
} catch {
    Log-Step "4. Get Versions" "FAIL" "" $_.Exception.Message
    $results.Fail++
}

# 5) DIFF
try {
    $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/$tipologiaId/diff/$versionA/$versionB" -Method Get
    if ($res.totalChanges -ge 1) {
        Log-Step "5. Diff" "PASS" "" ($res | ConvertTo-Json -Compress)
        $results.Pass++
    } else {
        Log-Step "5. Diff" "FAIL" "" "TotalChanges < 1"
        $results.Fail++
    }
} catch {
    try {
        $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/$tipologiaId/diff/$versionB?baseVersion=$versionA" -Method Get
        if ($res.totalChanges -ge 1) {
            Log-Step "5. Diff" "PASS" "" ($res | ConvertTo-Json -Compress)
            $results.Pass++
        } else { Log-Step "5. Diff" "FAIL" "" "TotalChanges < 1"; $results.Fail++ }
    } catch {
        Log-Step "5. Diff" "FAIL" "" $_.Exception.Message
        $results.Fail++
    }
}

# 6) Audit
try {
    $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/$tipologiaId/audit" -Method Get
    $created = $res | Where-Object { $_.action -eq "Created" -or $_.action -eq "Insert" }
    if ($created) {
        Log-Step "6. Audit" "PASS" "" ($res | ConvertTo-Json -Compress)
        $results.Pass++
    } else {
        Log-Step "6. Audit" "FAIL" "" "No Created/Insert action found"
        $results.Fail++
    }
} catch {
    Log-Step "6. Audit" "FAIL" "" $_.Exception.Message
    $results.Fail++
}

# 7) Export
try {
    $res = Invoke-WebRequest -Uri "$baseUrl/management/tipologias/$tipologiaId/$versionA/export" -Method Get
    if ($res.Headers["Content-Type"] -match "zip" -and $res.Content.Length -gt 0) {
        Log-Step "7. Export" "PASS" "" "Bytes: $($res.Content.Length)"
        $results.Pass++
        $zipBytes = $res.Content
    } else {
        Log-Step "7. Export" "FAIL" "" "ContentType: $($res.Headers["Content-Type"]), Len: $($res.Content.Length)"
        $results.Fail++
    }
} catch {
    Log-Step "7. Export" "FAIL" "" $_.Exception.Message
    $results.Fail++
}

# 8) Import
try {
    $importId = "Import_$timestamp"
    $b64 = if ($zipBytes) { [Convert]::ToBase64String($zipBytes) } else { "UEsFBgAAAAAAAAAAAAAAAAAAAAAAAA==" }
    $importBody = @{
        tipologiaId = "$importId"
        fileBase = $b64
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/import" -Method Post -Body $importBody -ContentType "application/json"
    Log-Step "8. Import" "PASS" "Importing as $importId" ($res | ConvertTo-Json -Compress)
    $results.Pass++
    $importedEntity = $res
} catch {
    Log-Step "8. Import" "FAIL" "" $_.Exception.Message
    $results.Fail++
}

# 9) Validate Import State
if ($importedEntity) {
    if ($importedEntity.status -eq "Draft" -or $importedEntity.state -eq "Draft") {
        Log-Step "9. Validate Import State" "PASS" "" ($importedEntity | ConvertTo-Json -Compress)
        $results.Pass++
    } else {
        Log-Step "9. Validate Import State" "FAIL" "" "Status is not Draft"
        $results.Fail++
    }
} else {
    Log-Step "9. Validate Import State" "FAIL" "" "No imported entity"
    $results.Fail++
}

Write-Host "
Summary: Total: $($results.Total), Pass: $($results.Pass), Fail: $($results.Fail)"
Write-Host "Created IDs: $tipologiaId, $importId"
