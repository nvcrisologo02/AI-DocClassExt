param(
    [switch]$UseUser
)

function Fail([string]$msg) {
    Write-Error $msg
    exit 1
}

function DisableInvalidCertEnvVars {
    $envVars = @("REQUESTS_CA_BUNDLE", "SSL_CERT_FILE", "CURL_CA_BUNDLE")
    $disabled = @{}
    foreach ($v in $envVars) {
        $val = [Environment]::GetEnvironmentVariable($v, 'Process')
        if ($val) {
            if (-not (Test-Path $val)) {
                Write-Warning "Environment variable $v is set to '$val' but the file does not exist. Temporarily unsetting it to avoid TLS errors."
                $disabled[$v] = $val
                [Environment]::SetEnvironmentVariable($v, $null, 'Process')
            }
        }
    }
    return $disabled
}

function RestoreCertEnvVars([hashtable]$disabled) {
    foreach ($k in $disabled.Keys) {
        [Environment]::SetEnvironmentVariable($k, $disabled[$k], 'Process')
    }
}

$disabled = DisableInvalidCertEnvVars

if ($UseUser) {
    Write-Output "Logging in with user account (device code)..."
    $login = az login --use-device-code 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "az login (user) failed: $login"
        RestoreCertEnvVars $disabled
        exit $LASTEXITCODE
    }
} else {
    Write-Output "Assuming already logged in or using service principal."
}


$endpoints = @(
    "https://graph.microsoft.com/beta/privilegedAccess/azureResources/roleAssignments",
    "https://graph.microsoft.com/beta/privilegedAccess/azureResources/roleAssignmentRequests",
    "https://graph.microsoft.com/beta/privilegedAccess/azureResources/roleAssignmentScheduleInstances",
    "https://graph.microsoft.com/beta/identityGovernance/privilegedAccess/azureResources/roleAssignments",
    "https://graph.microsoft.com/beta/identityGovernance/privilegedAccess/azureResources/roleAssignmentRequests"
)

foreach ($ep in $endpoints) {
    Write-Output "\nTrying: $ep"
    $resp = az rest --method GET --uri $ep 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Request failed (status from az): $resp"
        continue
    }

    try {
        $json = $resp | ConvertFrom-Json -ErrorAction Stop
        if ($json.value -and $json.value.Count -gt 0) {
            Write-Output "Found $($json.value.Count) items at $ep"
            $i = 0
            foreach ($item in $json.value) {
                $i++
                Write-Output "--- Item $i ---"
                $item | ConvertTo-Json -Depth 5 | Write-Output
            }
        } else {
            Write-Output "No items returned or empty result. Full response:"
            $resp | Write-Output
        }
    } catch {
        Write-Output "Response not JSON or conversion failed. Raw response:"
        $resp | Write-Output
    }
}

Write-Output "Done. If none of the endpoints returned your eligible assignments, your account may lack required Graph permissions (PrivilegedAccess.Read.All, PrivilegedAccess.ReadWrite.AzureResources) or the API path may differ for your tenant. If you want, puedo try to construct the exact activation call once we identify the assignment id."
