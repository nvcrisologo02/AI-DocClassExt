Set-StrictMode -Version Latest

function Get-E2EEnvironment {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][string]$Environment
    )
    if (-not (Test-Path -Path $ConfigPath)) {
        throw "No existe $ConfigPath. Copia environments.sample.json a environments.json (misma carpeta) y rellena las function keys."
    }
    $raw = Get-Content -Raw -Path $ConfigPath
    try {
        $config = $raw | ConvertFrom-Json
    }
    catch {
        throw "El fichero $ConfigPath no contiene JSON valido: $($_.Exception.Message)"
    }
    $entry = $config.PSObject.Properties[$Environment]
    if ($null -eq $entry) {
        throw "Entorno '$Environment' no definido en $ConfigPath. Disponibles: $($config.PSObject.Properties.Name -join ', ')"
    }
    $value = $entry.Value
    if ($null -eq $value) {
        throw "Entorno '$Environment' esta definido pero vacio (null) en $ConfigPath."
    }
    if ([string]::IsNullOrWhiteSpace($value.baseUrl)) {
        throw "baseUrl vacio para el entorno '$Environment' en $ConfigPath"
    }
    $healthCheck = $true
    if ($null -ne ($value.PSObject.Properties['healthCheck'])) { $healthCheck = [bool]$value.healthCheck }
    $functionKey = ""
    if ($null -ne ($value.PSObject.Properties['functionKey'])) { $functionKey = [string]$value.functionKey }
    return [pscustomobject]@{
        Name        = $Environment
        BaseUrl     = ([string]$value.baseUrl).TrimEnd('/')
        FunctionKey = $functionKey
        HealthCheck = $healthCheck
    }
}
