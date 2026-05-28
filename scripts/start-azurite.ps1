param(
    [int]$BlobPort = 20000,
    [int]$QueuePort = 20001,
    [int]$TablePort = 20002,
    [string]$DataPath = ".azurite",
    [bool]$SkipApiVersionCheck = $true
)

function Test-PortListening {
    param([int]$Port)

    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
        $ok = $async.AsyncWaitHandle.WaitOne(600)
        if (-not $ok) {
            $client.Close()
            return $false
        }

        $client.EndConnect($async)
        $client.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Resolve-AzuriteCommand {
    $azuriteCommand = Get-Command azurite -ErrorAction SilentlyContinue
    if ($azuriteCommand) {
        return @{ FilePath = $azuriteCommand.Source; ArgumentList = @() }
    }

    $extensionRoot = Join-Path $env:USERPROFILE ".vscode\extensions"
    $azuriteExtension = Get-ChildItem -Path $extensionRoot -Directory -Filter "azurite.azurite-*" -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1

    if (-not $azuriteExtension) {
        throw "No se encontro la extension de Azurite en VS Code ni el comando 'azurite' en PATH."
    }

    $azuriteJs = Join-Path $azuriteExtension.FullName "dist\src\azurite.js"
    if (-not (Test-Path $azuriteJs)) {
        throw "No se encontro azurite.js en la extension: $azuriteJs"
    }

    $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
    if (-not $nodeCommand) {
        throw "No se encontro 'node' en PATH para ejecutar azurite.js."
    }

    return @{ FilePath = $nodeCommand.Source; ArgumentList = @($azuriteJs) }
}

$requiredPorts = @($BlobPort, $QueuePort, $TablePort)
$alreadyRunning = $requiredPorts | ForEach-Object { Test-PortListening -Port $_ }
if ($alreadyRunning -notcontains $false) {
    Write-Host "Azurite ya esta activo en puertos $BlobPort/$QueuePort/$TablePort" -ForegroundColor Green
    exit 0
}

if (-not (Test-Path $DataPath)) {
    New-Item -Path $DataPath -ItemType Directory -Force | Out-Null
}

$resolved = Resolve-AzuriteCommand
$args = @(
    $resolved.ArgumentList
    "--location", $DataPath,
    "--blobPort", $BlobPort,
    "--queuePort", $QueuePort,
    "--tablePort", $TablePort
)

if ($SkipApiVersionCheck) {
    $args += "--skipApiVersionCheck"
}

Write-Host "Iniciando Azurite en puertos $BlobPort/$QueuePort/$TablePort..." -ForegroundColor Cyan
Start-Process -FilePath $resolved.FilePath -ArgumentList $args -WindowStyle Hidden | Out-Null

$timeoutSeconds = 20
$started = $false
for ($i = 0; $i -lt $timeoutSeconds; $i++) {
    Start-Sleep -Seconds 1
    $allListening = $requiredPorts | ForEach-Object { Test-PortListening -Port $_ }
    if ($allListening -notcontains $false) {
        $started = $true
        break
    }
}

if (-not $started) {
    throw "Azurite no quedo escuchando en los puertos esperados tras $timeoutSeconds segundos."
}

Write-Host "Azurite listo en puertos $BlobPort/$QueuePort/$TablePort" -ForegroundColor Green