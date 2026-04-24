function Log-Step($step, $status, $payload = "", $response = "") {
    Write-Host "--- STEP: $step ---"
    Write-Host "STATUS: $status"
    if ($payload) { Write-Host "PAYLOAD: $payload" }
    if ($response) { Write-Host "RESPONSE: $response" }
    Write-Host "-------------------"
}

$baseUrl = "http://localhost:7071/api"
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$familia = "smoke-family-$timestamp"
$codigoA = "smoke.a2.$().a"
$codigoB = "smoke.a2.$().b"
$versionA = "1.0"
$versionB = "1.1"
$usuario = "smoke-test-user"

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

$idA = $null
$idB = $null

# 2) POST Tipologia A
$configA = @{
    tipologiaId = "$familia"
    tipologiaNombre = "Smoke Family"
    version = "$versionA"
    extraction = @{ enabled = $false }
    promptConfig = @{ enabled = $false }
    fields = @()
} | ConvertTo-Json -Compress

$bodyA = @{
    codigo = "$codigoA"
    nombre = "Smoke A"
    version = "$versionA"
    configuracionJson = $configA
    usuario = "$usuario"
} | ConvertTo-Json

try {
    $resA = Invoke-RestMethod -Uri "$baseUrl/management/tipologias" -Method Post -Body $bodyA -ContentType "application/json"
    $idA = $resA.id
    Log-Step "2. Create Tipologia A" "PASS" $bodyA ($resA | ConvertTo-Json -Compress)
    $results.Pass++
} catch {
    Log-Step "2. Create Tipologia A" "FAIL" $bodyA $_.Exception.Message
    $results.Fail++
}

# 3) POST Tipologia B
$configB = @{
    tipologiaId = "$familia"
    tipologiaNombre = "Smoke Family"
    version = "$versionB"
    extraction = @{ enabled = $false }
    promptConfig = @{ enabled = $false }
    fields = @()
} | ConvertTo-Json -Compress

$bodyB = @{
    codigo = "$codigoB"
    nombre = "Smoke B"
    version = "$versionB"
    configuracionJson = $configB
    usuario = "$usuario"
} | ConvertTo-Json

try {
    $resB = Invoke-RestMethod -Uri "$baseUrl/management/tipologias" -Method Post -Body $bodyB -ContentType "application/json"
    $idB = $resB.id
    Log-Step "3. Create Tipologia B" "PASS" $bodyB ($resB | ConvertTo-Json -Compress)
    $results.Pass++
} catch {
    Log-Step "3. Create Tipologia B" "FAIL" $bodyB $_.Exception.Message
    $results.Fail++
}

# 4) GET Versions
if ($idA) {
    try {
        $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/$idA/versions" -Method Get
        $ids = $res | Select-Object -ExpandProperty id
        if ($ids -contains $idA -and $ids -contains $idB) {
            Log-Step "4. Get Versions" "PASS" "" ($res | ConvertTo-Json -Compress)
            $results.Pass++
        } else {
            Log-Step "4. Get Versions" "FAIL" "" "Expected IDs not found in versions list"
            $results.Fail++
        }
    } catch {
        Log-Step "4. Get Versions" "FAIL" "" $_.Exception.Message
        $results.Fail++
    }
} else { $results.Fail++; Log-Step "4. Get Versions" "FAIL" "" "No ID A available" }

# 5) DIFF
if ($idA -and $idB) {
    try {
        $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/$idA/diff/$idB" -Method Get
        if ($res.totalChanges -ge 0) {
            Log-Step "5. Diff" "PASS" "" ($res | ConvertTo-Json -Compress)
            $results.Pass++
        } else {
            Log-Step "5. Diff" "FAIL" "" "Unexpected response structure"
            $results.Fail++
        }
    } catch {
        Log-Step "5. Diff" "FAIL" "" $_.Exception.Message
        $results.Fail++
    }
} else { $results.Fail++; Log-Step "5. Diff" "FAIL" "" "Missing IDs for Diff" }

# 6) Audit
if ($idA) {
    try {
        $res = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/$idA/audit" -Method Get
        $created = $res | Where-Object { $_.accion -eq "Created" -or $_.action -eq "Created" }
        if ($created) {
            Log-Step "6. Audit" "PASS" "" ($res | ConvertTo-Json -Compress)
            $results.Pass++
        } else {
            Log-Step "6. Audit" "FAIL" "" "No Created action found"
            $results.Fail++
        }
    } catch {
        Log-Step "6. Audit" "FAIL" "" $_.Exception.Message
        $results.Fail++
    }
} else { $results.Fail++; Log-Step "6. Audit" "FAIL" "" "No ID A available" }

# 7) Export
$zipBytes = $null
if ($idA) {
    try {
        $res = Invoke-WebRequest -Uri "$baseUrl/management/tipologias/$idA/export" -Method Get
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
} else { $results.Fail++; Log-Step "7. Export" "FAIL" "" "No ID A available" }

# 8) Import (Simulated ZIP with Manifest and Validation JSON)
try {
    $importCodigo = "import.$timestamp"
    $manifest = @{
        codigo = "$importCodigo"
        nombre = "Imported Smoke"
        version = "1.0"
        source = "smoke-test"
    } | ConvertTo-Json -Compress
    
    $validation = @{
        tipologiaId = "$importCodigo"
        version = "1.0"
        extraction = @{ enabled = $false }
        promptConfig = @{ enabled = $false }
        fields = @()
    } | ConvertTo-Json -Compress

    # Creating a real ZIP in memory for the import tool
    $ms = New-Object System.IO.MemoryStream
    $zip = New-Object System.IO.Compression.ZipArchive($ms, [System.IO.Compression.ZipArchiveMode]::Create)
    
    $entry1 = $zip.CreateEntry("manifest.json")
    $writer1 = New-Object System.IO.StreamWriter($entry1.Open())
    $writer1.Write($manifest)
    $writer1.Close()
    
    $entry2 = $zip.CreateEntry("tipologia.validation.json")
    $writer2 = New-Object System.IO.StreamWriter($entry2.Open())
    $writer2.Write($validation)
    $writer2.Close()
    
    $zip.Dispose()
    $zipBase64 = [Convert]::ToBase64String($ms.ToArray())
    $ms.Close()

    $importBody = @{
        zipBase64 = $zipBase64
        usuario = "$usuario"
    } | ConvertTo-Json
    
    $resImport = Invoke-RestMethod -Uri "$baseUrl/management/tipologias/import" -Method Post -Body $importBody -ContentType "application/json"
    Log-Step "8. Import" "PASS" "Importing as $importCodigo" ($resImport | ConvertTo-Json -Compress)
    $results.Pass++
    $importedEntity = $resImport
} catch {
    Log-Step "8. Import" "FAIL" "" $_.Exception.Message
    $results.Fail++
}

# 9) Validate Import State
if ($importedEntity) {
    if ($importedEntity.estado -eq "Draft" -or $importedEntity.status -eq "Draft" -or $importedEntity.state -eq "Draft") {
        Log-Step "9. Validate Import State" "PASS" "" ($importedEntity | ConvertTo-Json -Compress)
        $results.Pass++
    } else {
        Log-Step "9. Validate Import State" "FAIL" "" "Status is not Draft. Got: $(.estado)"
        $results.Fail++
    }
} else {
    Log-Step "9. Validate Import State" "FAIL" "" "No imported entity"
    $results.Fail++
}

Write-Host "
Summary: Total: $(.Total), Pass: $(.Pass), Fail: $(.Fail)"
Write-Host "IDs: A=$idA, B=$idB"
Write-Host "Codes: $codigoA, $codigoB, $importCodigo"
