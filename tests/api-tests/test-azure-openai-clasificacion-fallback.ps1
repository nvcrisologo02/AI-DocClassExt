param(
    [string]$SettingsFile,
    [string]$Prompt = "Responde solo con el texto OK.",
    [int]$TimeoutSeconds = 30,
    [switch]$ShowRawResponse
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Write-Section {
    param([string]$Text)
    Write-Host ""
    Write-Host "========================================"
    Write-Host "  $Text"
    Write-Host "========================================"
}

function Write-Fail {
    param([string]$Text)
    Write-Host "[ERROR] $Text" -ForegroundColor Red
}

function Write-Ok {
    param([string]$Text)
    Write-Host "[OK] $Text" -ForegroundColor Green
}

function Get-ProjectRoot {
    $scriptDir = Split-Path -Parent $MyInvocation.ScriptName
    return (Resolve-Path (Join-Path $scriptDir "..\..")).Path
}

function Get-SettingValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Settings,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Settings -or $null -eq $Settings.Values) {
        return $null
    }

    $property = $Settings.Values.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return [string]$property.Value
}

if ([string]::IsNullOrWhiteSpace($SettingsFile)) {
    $projectRoot = Get-ProjectRoot
    $SettingsFile = Join-Path $projectRoot "src\backend\DocumentIA.Functions\local.settings.json"
}

if (-not (Test-Path $SettingsFile)) {
    throw "No se encontro local.settings.json en: $SettingsFile"
}

$settings = Get-Content -Raw -Path $SettingsFile | ConvertFrom-Json

$enabled = Get-SettingValue -Settings $settings -Name "Classification:GptFallback:Enabled"
$endpoint = Get-SettingValue -Settings $settings -Name "Classification:GptFallback:Endpoint"
$apiKey = Get-SettingValue -Settings $settings -Name "Classification:GptFallback:ApiKey"
$authMode = Get-SettingValue -Settings $settings -Name "Classification:GptFallback:AuthMode"
$deploymentName = Get-SettingValue -Settings $settings -Name "Classification:GptFallback:DeploymentName"

Write-Section "Smoke test Azure OpenAI fallback clasificacion"
Write-Host "Settings file : $SettingsFile"
Write-Host "Enabled       : $enabled"
Write-Host "AuthMode      : $authMode"
Write-Host "Endpoint      : $endpoint"
Write-Host "Deployment    : $deploymentName"

if ([string]::IsNullOrWhiteSpace($endpoint)) {
    throw "Falta Classification:GptFallback:Endpoint en local.settings.json"
}

if ([string]::IsNullOrWhiteSpace($deploymentName)) {
    throw "Falta Classification:GptFallback:DeploymentName en local.settings.json"
}

if (-not [string]::Equals($authMode, "ApiKey", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Este script solo soporta AuthMode=ApiKey. Valor actual: '$authMode'"
}

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw "Falta Classification:GptFallback:ApiKey en local.settings.json"
}

$normalizedEndpoint = $endpoint.TrimEnd("/")
$apiVersion = "2024-10-21"
$uri = "$normalizedEndpoint/openai/deployments/$deploymentName/chat/completions?api-version=$apiVersion"

$headers = @{
    "api-key" = $apiKey
    "Content-Type" = "application/json"
}

$body = @{
    messages = @(
        @{
            role = "system"
            content = "Eres un verificador tecnico. Responde exactamente con OK."
        },
        @{
            role = "user"
            content = $Prompt
        }
    )
    temperature = 0
    max_tokens = 20
} | ConvertTo-Json -Depth 10

Write-Host ""
Write-Host "Invocando deployment configurado..."
Write-Host "POST $uri"

try {
    $response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body -TimeoutSec $TimeoutSeconds

    $content = $response.choices[0].message.content
    Write-Ok "Llamada completada correctamente."
    Write-Host "Respuesta     : $content"

    if ($ShowRawResponse) {
        Write-Host ""
        Write-Host ($response | ConvertTo-Json -Depth 20)
    }
}
catch {
    Write-Fail "La llamada a Azure OpenAI ha fallado."

    $exception = $_.Exception
    Write-Host "Tipo          : $($exception.GetType().FullName)"
    Write-Host "Mensaje       : $($exception.Message)"

    if ($exception.Response -and $exception.Response.StatusCode) {
        Write-Host "StatusCode    : $([int]$exception.Response.StatusCode)"
        Write-Host "Status        : $($exception.Response.StatusDescription)"
    }

    $responseStream = $null
    try {
        $responseStream = $exception.Response.GetResponseStream()
        if ($null -ne $responseStream) {
            $reader = New-Object System.IO.StreamReader($responseStream)
            $responseText = $reader.ReadToEnd()
            if (-not [string]::IsNullOrWhiteSpace($responseText)) {
                Write-Host "ResponseBody  : $responseText"
            }
        }
    }
    catch {
    }

    throw
}