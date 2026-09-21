#Requires -Version 7.0
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

    PRO tiene dos cuentas Foundry (upe48-mm2avmdm-swedencentral y
    srbaisrv-westeurope) y algunos analyzers existen en ambas. Cada JSON
    exportado lleva un objeto "_origin" (sourceAccount/sourceEndpoint/
    exportedAtUtc, derivados de -SourceEndpoint) como ultima clave del objeto,
    para poder distinguir de que cuenta salio cada copia y compararlas sin que
    esos campos viajen mezclados con el cuerpo del analyzer. Cuando
    -SourceEndpoint no es la cuenta primaria (-PrimaryEndpoint), el fichero se
    nombra "<id>@<cuenta>.json" y el inventario por defecto pasa a
    "infra/ai/inventory-prod-foundry-<cuenta>.json", para que reexportar la
    cuenta secundaria nunca pise los ficheros de la primaria. Usa
    -SkipInventory para exportar/comparar sin pisar el inventario de la
    cuenta primaria.

    Excepcion conocida: el plural "sourceAccounts"/"sourceEndpoints" de
    analyzers/CU_NS_1.6_0_GGAA.json es una decision manual (el analyzer es
    identico en las dos cuentas, asi que se fusionaron en un solo fichero con
    las dos referencias) que una reejecucion de este script NO reproduce -
    genera "CU_NS_1.6_0_GGAA@<cuenta>.json" por separado. Tras reexportar,
    comparar y fusionar a mano si sigue siendo identico en ambas cuentas.
.PARAMETER Ids
    Lista de analyzerId a exportar (los referenciados por filas activas de ModeloConfigs).
.PARAMETER SourceEndpoint
    Endpoint del recurso Foundry origen (por defecto, el primario de PRO).
.PARAMETER PrimaryEndpoint
    Endpoint de la cuenta Foundry primaria de PRO. Se compara contra
    -SourceEndpoint (sin barra final, sin distinguir mayusculas/minusculas)
    para decidir si el fichero de salida lleva sufijo "@<cuenta>" y si el
    inventario por defecto lleva el nombre de la cuenta.
.PARAMETER OutDir
    Carpeta donde se escriben los JSON individuales de cada analyzer.
.PARAMETER InventoryFile
    Fichero donde se escribe el inventario completo (todos los analyzers custom del recurso).
    Por defecto, "infra/ai/inventory-prod-foundry.json" para la cuenta primaria
    o "infra/ai/inventory-prod-foundry-<cuenta>.json" para cualquier otra.
.PARAMETER ApiVersion
    Version de la API de Content Understanding a usar.
.PARAMETER SkipInventory
    No lista ni escribe el inventario completo; solo exporta los -Ids indicados.
    Uso tipico: comparar un id concreto contra una segunda cuenta sin volver a
    listar (ni pisar el inventario) de la cuenta primaria.
.EXAMPLE
    pwsh scripts/ai/export-analyzer-definitions.ps1 -Ids CU_NS_1.4_3,CU_NS_1.5_0,CU_NS_1.6_0_GGAA,CERA16_v1,CERA44_vado,CERA46
.EXAMPLE
    pwsh scripts/ai/export-analyzer-definitions.ps1 -Ids CU_NS_1.5_0,CU_NS_1.6_0_GGAA `
        -SourceEndpoint https://srbaisrv-westeurope.services.ai.azure.com `
        -InventoryFile infra/ai/inventory-prod-foundry-westeurope.json
    # Cuenta secundaria: al no coincidir con -PrimaryEndpoint, escribe
    # CU_NS_1.5_0@srbaisrv-westeurope.json y CU_NS_1.6_0_GGAA@srbaisrv-westeurope.json
    # en el mismo -OutDir por defecto sin pisar los de la cuenta primaria.
#>
param(
    [Parameter(Mandatory)][string[]]$Ids,
    [string]$SourceEndpoint = "https://upe48-mm2avmdm-swedencentral.services.ai.azure.com",
    [string]$PrimaryEndpoint = "https://upe48-mm2avmdm-swedencentral.services.ai.azure.com",
    [string]$OutDir = "infra/ai/analyzers",
    [string]$InventoryFile,
    [string]$ApiVersion = "2025-11-01",
    [switch]$SkipInventory
)
$ErrorActionPreference = "Stop"

function Get-NormalizedEndpoint {
    param([string]$Endpoint)
    return $Endpoint.TrimEnd('/')
}

$isPrimarySource = (Get-NormalizedEndpoint $SourceEndpoint) -eq (Get-NormalizedEndpoint $PrimaryEndpoint)

# El data-plane en este entorno pasa por un proxy TLS; sin esto tanto az CLI
# como Invoke-WebRequest fallan la verificacion del certificado.
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"

function Get-CuJson {
    param([string]$Url, [string]$Token)
    try {
        $resp = Invoke-WebRequest -Uri $Url -Headers @{ Authorization = "Bearer $Token" } `
            -UseBasicParsing -SkipCertificateCheck
    } catch {
        $status = "sin respuesta"
        $body = ""
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
            $body = $_.ErrorDetails.Message
        } elseif ($_.Exception.Message) {
            $body = $_.Exception.Message
        }
        throw "GET $Url -> $status`: $body"
    }
    # Decodificar los bytes crudos como UTF-8 explicitamente: no confiar en que
    # PowerShell adivine la codificacion de la respuesta.
    $bytes = $resp.RawContentStream.ToArray()
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    return $text | ConvertFrom-Json
}

function Write-JsonFile {
    param([string]$Path, [object]$Object, [int]$Depth = 30)
    # .NET resuelve una ruta relativa contra el cwd del proceso, no contra el
    # de PowerShell: sin esto, escribir con una ubicacion relativa tras un
    # Set-Location puede acabar en la carpeta equivocada.
    $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    $json = $Object | ConvertTo-Json -Depth $Depth
    # LF sin BOM y con newline final, para que coincida con el resto de infra/ai.
    $json = $json -replace "`r`n", "`n"
    [IO.File]::WriteAllText($Path, ($json + "`n"), [Text.UTF8Encoding]::new($false))
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

$sourceAccount = ([Uri]$SourceEndpoint).Host.Split('.')[0]
$exportedAtUtc = (Get-Date).ToUniversalTime().ToString("o")

if (-not $PSBoundParameters.ContainsKey('InventoryFile')) {
    $InventoryFile = if ($isPrimarySource) {
        "infra/ai/inventory-prod-foundry.json"
    } else {
        "infra/ai/inventory-prod-foundry-$sourceAccount.json"
    }
}

if (-not $SkipInventory) {
    $allAnalyzers = @()
    $url = "$SourceEndpoint/contentunderstanding/analyzers?api-version=$ApiVersion"
    while ($url) {
        $page = Get-CuJson -Url $url -Token $token
        $allAnalyzers += $page.value
        $url = $page.nextLink
    }

    $custom = $allAnalyzers | Where-Object { $_.analyzerId -notlike 'prebuilt-*' } | Sort-Object analyzerId
    $inventory = [ordered]@{
        account        = $sourceAccount
        endpoint       = $SourceEndpoint
        apiVersion     = $ApiVersion
        generatedAtUtc = $exportedAtUtc
        analyzers      = @($custom | Select-Object analyzerId, status, createdAt, @{n='referenced'; e={ $Ids -contains $_.analyzerId }})
    }
    Write-JsonFile -Path $InventoryFile -Object $inventory -Depth 4
    Write-Host "inventario: $($custom.Count) analyzers custom -> $InventoryFile"
}

foreach ($id in $Ids) {
    $def = Get-CuJson -Url "$SourceEndpoint/contentunderstanding/analyzers/$id`?api-version=$ApiVersion" -Token $token
    foreach ($f in $readOnly) { $def.PSObject.Properties.Remove($f) }
    # Los campos de origen van agrupados bajo una unica clave raiz, colocada
    # al final del objeto: no forman parte del cuerpo de un futuro PUT y asi
    # no se pueden confundir con el resto de campos del analyzer.
    $origin = [ordered]@{
        sourceAccount  = $sourceAccount
        sourceEndpoint = $SourceEndpoint
        exportedAtUtc  = $exportedAtUtc
    }
    $def | Add-Member -NotePropertyName _origin -NotePropertyValue $origin
    # Si la cuenta origen no es la primaria, el fichero lleva sufijo "@<cuenta>"
    # para no pisar la copia de la cuenta primaria en una reejecucion.
    $fileName = if ($isPrimarySource) { "$id.json" } else { "$id@$sourceAccount.json" }
    Write-JsonFile -Path (Join-Path $OutDir $fileName) -Object $def -Depth 30
    Write-Host "exportado $id -> $fileName"
}
