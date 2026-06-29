param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('functionapp', 'webapp')]
    [string]$AppKind,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string[]]$Settings
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Comprobar disponibilidad de Azure CLI de forma robusta
try {
    $azCmds = Get-Command -Name az -ErrorAction SilentlyContinue
}
catch {
    # En algunos entornos Get-Command puede fallar; dejamos que la comprobación por invocación actúe como fallback
    $azCmds = $null
}

if (-not $azCmds) {
    try {
        & az --version > $null 2>&1
        if ($LASTEXITCODE -ne 0) { throw }
    }
    catch {
        throw 'Azure CLI (az) no esta instalado o no esta en PATH.'
    }
}

# Validar parámetros obligatorios con mensajes claros para fallos en CI
if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
    throw 'Parametro obligatorio "-ResourceGroup" vacio o no establecido. Revisa la variable de pipeline $(AZURE_RESOURCE_GROUP).'
}

if ([string]::IsNullOrWhiteSpace($Name)) {
    throw 'Parametro obligatorio "-Name" vacio o no establecido. Revisa la variable de pipeline correspondiente (p.ej. $(AZURE_FUNCTIONS_APP_NAME)).'
}

if (-not $Settings -or $Settings.Count -eq 0) {
    Write-Host "[INFO] No se han pasado settings a aplicar para $Name; nada que hacer." -ForegroundColor Yellow
    exit 0
}

function Invoke-AzJson {
    param([string[]]$AzArgs)

    $result = & az @AzArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($AzArgs -join ' ')`n$result"
    }

    $lines = @($result | ForEach-Object { [string]$_ })
    $jsonStartIndex = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $trimmed = $lines[$index].TrimStart()
        if ($trimmed.StartsWith('{') -or $trimmed.StartsWith('[')) {
            $jsonStartIndex = $index
            break
        }
    }

    $text = if ($jsonStartIndex -ge 0) {
        ($lines[$jsonStartIndex..($lines.Count - 1)] -join [Environment]::NewLine).Trim()
    }
    else {
        ($lines | Out-String).Trim()
    }

    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text | ConvertFrom-Json
}

function Get-AppSettingsCommandPrefix {
    param([string]$Kind)

    switch ($Kind) {
        'functionapp' { return @('functionapp', 'config', 'appsettings') }
        'webapp' { return @('webapp', 'config', 'appsettings') }
        default { throw "Tipo de app no soportado: $Kind" }
    }
}

$commandPrefix = Get-AppSettingsCommandPrefix -Kind $AppKind

$existingSettings = Invoke-AzJson -AzArgs @(
    $commandPrefix[0], $commandPrefix[1], $commandPrefix[2], 'list',
    '--resource-group', $ResourceGroup,
    '--name', $Name,
    '--only-show-errors',
    '-o', 'json'
)

$existingNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($setting in @($existingSettings)) {
    if ($null -ne $setting.name -and -not [string]::IsNullOrWhiteSpace([string]$setting.name)) {
        [void]$existingNames.Add([string]$setting.name)
    }
}

$missingSettings = New-Object 'System.Collections.Generic.List[string]'

foreach ($setting in $Settings) {
    if ([string]::IsNullOrWhiteSpace($setting)) {
        continue
    }

    if ($setting -notmatch '^([^=]+)=(.*)$') {
        throw "Formato de setting invalido (esperado KEY=VALUE): $setting"
    }

    $key = $Matches[1].Trim()
    if ($existingNames.Contains($key)) {
        Write-Host "  [SKIP] $key ya existe; se conserva el valor actual" -ForegroundColor DarkYellow
        continue
    }

    [void]$missingSettings.Add($setting)
}

if ($missingSettings.Count -eq 0) {
    Write-Host "[OK] No hay settings nuevos para aplicar en $Name." -ForegroundColor Green
    exit 0
}

Write-Host "[INFO] Aplicando $($missingSettings.Count) setting(s) nuevos en $Name..." -ForegroundColor Cyan

# Convertir settings a JSON para evitar rotura en cmd con caracteres especiales (@, ;, parens)
$settingsHash = @{}
foreach ($setting in $missingSettings) {
    $parts = $setting -split "=", 2
    $key = $parts[0].Trim()
    $value = if ($parts.Count -gt 1) { $parts[1] } else { "" }
    $settingsHash[$key] = $value
}

$tempJsonPath = Join-Path ([System.IO.Path]::GetTempPath()) ("appsettings-" + [guid]::NewGuid().ToString("N") + ".json")
try {
    $settingsJson = $settingsHash | ConvertTo-Json -Depth 10 -Compress
    Set-Content -LiteralPath $tempJsonPath -Value $settingsJson -Encoding utf8NoBOM -NoNewline

    # Pasar settings por JSON file evita rotura en cmd con caracteres especiales.
    $setArgs = @(
        $commandPrefix[0], $commandPrefix[1], $commandPrefix[2], 'set',
        '--resource-group', $ResourceGroup,
        '--name', $Name,
        '--settings', "@$tempJsonPath",
        '--only-show-errors', '--output', 'none'
    )

    & az @setArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Fallo al aplicar App Settings en $Name"
    }
}
finally {
    if (Test-Path -LiteralPath $tempJsonPath) {
        Remove-Item -LiteralPath $tempJsonPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "[OK] Settings nuevos aplicados sin sobrescribir los existentes." -ForegroundColor Green