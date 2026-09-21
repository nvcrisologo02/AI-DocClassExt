<#
.SYNOPSIS
    Exporta definiciones de analyzers de Content Understanding (sin campos de solo lectura)
    a infra/ai/analyzers, junto con el inventario completo del Foundry origen.
.DESCRIPTION
    Operacion de solo lectura: unicamente hace GET contra el data-plane de Content
    Understanding. No crea, modifica ni borra ningun analyzer.

    El GET se hace con Invoke-WebRequest usando un token de "az account
    get-access-token" en vez de con "az rest", porque az rest re-codifica su
    salida por consola con la pagina de codigos activa (cp1252/850 segun el
    entorno) y sustituye cualquier caracter no representable (tildes, enies,
    comillas tipograficas) por el caracter de sustitucion U+FFFD, perdiendo
    contenido real de las definiciones (verificado: "Direccion" con tilde
    llegaba corrupto via az rest y correcto via Invoke-WebRequest). Leyendo los
    bytes de la respuesta y decodificandolos explicitamente como UTF-8 se evita
    ese problema.
.PARAMETER Ids
    Lista de analyzerId a exportar (los referenciados por filas activas de ModeloConfigs).
.PARAMETER SourceEndpoint
    Endpoint del recurso Foundry origen (por defecto, el primario de PRO).
.PARAMETER OutDir
    Carpeta donde se escriben los JSON individuales de cada analyzer.
.PARAMETER InventoryFile
    Fichero donde se escribe el inventario completo (todos los analyzers custom del recurso).
.PARAMETER ApiVersion
    Version de la API de Content Understanding a usar.
.EXAMPLE
    pwsh scripts/ai/export-analyzer-definitions.ps1 -Ids CU_NS_1.4_3,CU_NS_1.5_0,CU_NS_1.6_0_GGAA,CERA16_v1,CERA44_vado,CERA46
#>
param(
    [Parameter(Mandatory)][string[]]$Ids,
    [string]$SourceEndpoint = "https://upe48-mm2avmdm-swedencentral.services.ai.azure.com",
    [string]$OutDir = "infra/ai/analyzers",
    [string]$InventoryFile = "infra/ai/inventory-prod-foundry.json",
    [string]$ApiVersion = "2025-11-01"
)
$ErrorActionPreference = "Stop"

# El data-plane en este entorno pasa por un proxy TLS; sin esto tanto az CLI
# como Invoke-WebRequest fallan la verificacion del certificado.
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"

function Get-CuJson {
    param([string]$Url, [string]$Token)
    $resp = Invoke-WebRequest -Uri $Url -Headers @{ Authorization = "Bearer $Token" } `
        -UseBasicParsing -SkipCertificateCheck
    # Decodificar los bytes crudos como UTF-8 explicitamente: no confiar en que
    # PowerShell adivine la codificacion de la respuesta.
    $bytes = $resp.RawContentStream.ToArray()
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    return $text | ConvertFrom-Json
}

$readOnly = @('status', 'createdAt', 'lastModifiedAt', 'warnings', 'supportedModels')
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# El token en si es un JWT solo-ASCII: az account get-access-token no sufre el
# problema de recodificacion que sufre az rest con cuerpos de respuesta.
$tokenRaw = az account get-access-token --resource https://cognitiveservices.azure.com -o json 2>$null
if ($LASTEXITCODE -ne 0 -or -not $tokenRaw) {
    throw "no se pudo obtener el token de acceso (az account get-access-token, exit $LASTEXITCODE)"
}
$token = ($tokenRaw | ConvertFrom-Json).accessToken
if (-not $token) { throw "az account get-access-token no devolvio accessToken" }

$all = Get-CuJson -Url "$SourceEndpoint/contentunderstanding/analyzers?api-version=$ApiVersion" -Token $token
if ($all.nextLink) {
    Write-Warning "la respuesta trae nextLink; este script no pagina, el inventario quedara incompleto"
}
$custom = $all.value | Where-Object { $_.analyzerId -notlike 'prebuilt-*' }
$custom | Select-Object analyzerId, status, createdAt, @{n='referenced'; e={ $Ids -contains $_.analyzerId }} |
    ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 $InventoryFile
Write-Host "inventario: $($custom.Count) analyzers custom -> $InventoryFile"

foreach ($id in $Ids) {
    $def = Get-CuJson -Url "$SourceEndpoint/contentunderstanding/analyzers/$id`?api-version=$ApiVersion" -Token $token
    foreach ($f in $readOnly) { $def.PSObject.Properties.Remove($f) }
    $def | ConvertTo-Json -Depth 30 | Set-Content -Encoding utf8 (Join-Path $OutDir "$id.json")
    Write-Host "exportado $id"
}
