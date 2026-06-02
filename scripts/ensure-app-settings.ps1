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

if (-not (Get-Command az -ErrorActionSilentlyContinue)) {
    throw 'Azure CLI (az) no esta instalado o no esta en PATH.'
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

$setArgs = @(
    $commandPrefix[0], $commandPrefix[1], $commandPrefix[2], 'set',
    '--resource-group', $ResourceGroup,
    '--name', $Name,
    '--settings'
) + @($missingSettings.ToArray()) + @('--only-show-errors', '--output', 'none')

& az @setArgs

if ($LASTEXITCODE -ne 0) {
    throw "Fallo al aplicar App Settings en $Name"
}

Write-Host "[OK] Settings nuevos aplicados sin sobrescribir los existentes." -ForegroundColor Green