param(
    [int]$Port = 8082,
    [switch]$NoReload
)

$ErrorActionPreference = "Stop"

$pluginPath = Join-Path $PSScriptRoot "src\enrichments\ActivoEnrichment"

if (-not (Test-Path $pluginPath)) {
    throw "No se encontro la carpeta del plugin en: $pluginPath"
}

Push-Location $pluginPath
try {
    $uvicornArgs = @("-m", "uvicorn", "main:app", "--port", "$Port")
    if (-not $NoReload) {
        $uvicornArgs += "--reload"
    }

    python @uvicornArgs
}
finally {
    Pop-Location
}
