param(
    [string]$TenantId = $env:AZ_TENANT_ID,
    [string]$ClientId = $env:AZ_CLIENT_ID,
    [string]$ClientSecret = $env:AZ_CLIENT_SECRET,
    [string]$ActivationUri = $env:ACTIVATION_URI,
    [string]$BodyFile = $env:ACTIVATION_BODY_FILE,
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

if (-not $TenantId -or -not $ClientId -or -not $ClientSecret) {
    Fail "Missing service principal credentials. Set AZ_TENANT_ID, AZ_CLIENT_ID and AZ_CLIENT_SECRET environment variables or pass parameters."
}

# handle possibly-broken TLS CA env vars
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
    Write-Output "Logging in with service principal..."
    if (-not $TenantId -or -not $ClientId -or -not $ClientSecret) {
        RestoreCertEnvVars $disabled
        Fail "Missing service principal credentials. Set AZ_TENANT_ID, AZ_CLIENT_ID and AZ_CLIENT_SECRET environment variables or pass parameters."
    }
    $login = az login --service-principal -u $ClientId -p $ClientSecret --tenant $TenantId 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "az login failed: $login"
        RestoreCertEnvVars $disabled
        exit $LASTEXITCODE
    }
}

if (-not $ActivationUri) {
    Fail "Activation URI not provided. Set ACTIVATION_URI or pass -ActivationUri."
}

if ($BodyFile) {
    if (-not (Test-Path $BodyFile)) { Fail "Body file not found: $BodyFile" }
    $body = Get-Content $BodyFile -Raw
} else {
    $body = "{}"
}

Write-Output "Calling activation endpoint: $ActivationUri"
$resp = az rest --method POST --uri $ActivationUri --body $body --headers "Content-Type=application/json" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "az rest failed: $resp"
    RestoreCertEnvVars $disabled
    exit $LASTEXITCODE
}

Write-Output "Response:"
Write-Output $resp

RestoreCertEnvVars $disabled
