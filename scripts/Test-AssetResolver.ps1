param(
    [string]$BaseUrl = "http://localhost:5006",
    [string]$ApiKey = "dev-local-api-key-replace-in-prod",
    [string]$Escenario = "all",
    [switch]$MostrarDetalle,
    [string]$SampleIdufir = "11012000632521",
    [string]$SampleIdufirAacc = "30035000403354",
    [string]$SampleRefCatastral = "1612401VK6611S0099GA",
    [string]$SampleRefCatastralAacc = "8337302XG7683N0410HD",
    [string]$SampleDireccion = "SAN ROQUE CLUB 35, San Roque, 11310",
    [string]$SampleDireccionAacc = "GISBERT 7, Cartagena, 30202",
    # Campos para busqueda tipificada (todos opcionales, solo se filtran los informados)
    [string]$SamplePais = "",
    [string]$SampleProvincia = "",
    [string]$SampleComunidadAutonoma = "",
    [string]$SampleMunicipio = "Cartagena",
    [string]$SamplePoblacion = "",
    [string]$SampleTipoVia = "",
    [string]$SampleCalle = "GISBERT",
    [string]$SampleNumero = "7",
    [string]$SampleBloque = "",
    [string]$SamplePuerta = "",
    [string]$SampleCodigoPostal = "30202",
    [string]$SamplePlanta = "",
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Title {
    param([string]$Text)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Get-CorrelationId {
    return [Guid]::NewGuid().ToString()
}

function Build-BaseRequest {
    param([string]$CorrelationId)

    return @{
        CorrelationId = $CorrelationId
        DocumentType = "nota.simple.1_3"
        ExtractedData = @{}
        ModoCombinacionCriterios = "OR"
        AAII_Search = $true
        AACC_Search = $true
        BusquedaIdufirHabilitada = $true
        BusquedaReferenciaCatastralHabilitada = $true
        BusquedaDireccionHabilitada = $false
        BusquedaDireccionTipificadaHabilitada = $false
    }
}

function Invoke-AssetResolverRequest {
    param(
        [hashtable]$Body,
        [string]$ScenarioKey,
        [switch]$DryRunMode
    )

    if ($DryRunMode) {
        return @{
            IsError = $false
            StatusCode = 200
            Response = @{
                CorrelationId = $Body.CorrelationId
                Found = $false
                Count = 0
                CriterioUtilizado = "dry-run"
                DuracionMs = 0
                Message = "DryRun enabled, request not sent"
                Error = $null
                Activos = @()
                ActivosAAII = @()
                ActivosAACC = @()
                CountAAII = 0
                CountAACC = 0
                CamposConError = @()
                CamposConErrorAAII = @()
                CamposConErrorAACC = @()
            }
            ErrorText = $null
        }
    }

    $headers = @{
        "X-Api-Key" = $ApiKey
    }

    $uri = "$BaseUrl/api/assets/GetAAIIInfo"
    $jsonBody = $Body | ConvertTo-Json -Depth 12

    try {
        $response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $jsonBody -ContentType "application/json"
        return @{
            IsError = $false
            StatusCode = 200
            Response = $response
            ErrorText = $null
        }
    }
    catch {
        $errorText = $_.Exception.Message
        if ($_.Exception.Response) {
            try {
                $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
                $details = $reader.ReadToEnd()
                if (-not [string]::IsNullOrWhiteSpace($details)) {
                    $errorText = "$errorText | $details"
                }
            }
            catch {
                # Keep the original exception message if response body cannot be read.
            }
        }

        return @{
            IsError = $true
            StatusCode = 500
            Response = $null
            ErrorText = "[$ScenarioKey] $errorText"
        }
    }
}

function Get-OutcomeFromResponse {
    param(
        [hashtable]$InvocationResult,
        [AllowNull()]$ExpectedFound
    )

    if ($InvocationResult.IsError) {
        return "failed"
    }

    if ($null -ne $InvocationResult.Response -and $InvocationResult.Response.Message -eq "DryRun enabled, request not sent") {
        return "passed"
    }

    if ($null -eq $ExpectedFound) {
        return "passed"
    }

    $found = [bool]$InvocationResult.Response.Found
    if ($found -eq $ExpectedFound) {
        return "passed"
    }

    return "failed"
}

function Test-SplitResponseShape {
    param($Response)

    $props = @($Response.PSObject.Properties.Name)
    $required = @("ActivosAAII", "ActivosAACC", "CountAAII", "CountAACC", "Activos", "Count")

    foreach ($name in $required) {
        if ($props -notcontains $name) {
            return $false
        }
    }

    return $true
}

function Test-SplitResponseConsistency {
    param($Response)

    if (-not (Test-SplitResponseShape -Response $Response)) {
        return $false
    }

    $activosAaii = @($Response.ActivosAAII).Count
    $activosAacc = @($Response.ActivosAACC).Count
    $activosAgg = @($Response.Activos).Count

    $countAaii = [int]$Response.CountAAII
    $countAacc = [int]$Response.CountAACC
    $countAgg = [int]$Response.Count

    if ($countAaii -ne $activosAaii) { return $false }
    if ($countAacc -ne $activosAacc) { return $false }
    if ($countAgg -ne $activosAgg) { return $false }
    if ($countAgg -ne ($countAaii + $countAacc)) { return $false }

    return $true
}

function Get-ResolvedAssetIdsText {
    param([hashtable]$InvocationResult)

    if ($InvocationResult.IsError -or $null -eq $InvocationResult.Response) {
        return "-"
    }

    $resp = $InvocationResult.Response
    if ($null -eq $resp.Activos -or @($resp.Activos).Count -eq 0) {
        return "-"
    }

    $ids = @($resp.Activos |
        ForEach-Object { $_.IdActivo } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique)

    if ($ids.Count -eq 0) {
        return "-"
    }

    return ($ids -join ",")
}

function Show-ScenarioResult {
    param(
        [string]$ScenarioKey,
        [string]$ScenarioName,
        [string]$Outcome,
        [hashtable]$InvocationResult
    )

    if ($Outcome -eq "passed") {
        Write-Host "[OK] $ScenarioKey - $ScenarioName" -ForegroundColor Green
    }
    elseif ($Outcome -eq "blocked") {
        Write-Host "[BLOCKED] $ScenarioKey - $ScenarioName" -ForegroundColor Yellow
    }
    else {
        Write-Host "[FAIL] $ScenarioKey - $ScenarioName" -ForegroundColor Red
    }

    if ($InvocationResult.IsError) {
        Write-Host "  Error: $($InvocationResult.ErrorText)" -ForegroundColor Red
        return
    }

    $response = $InvocationResult.Response
    Write-Host "  Found: $($response.Found)" -ForegroundColor Gray
    Write-Host "  Count: $($response.Count)" -ForegroundColor Gray
    Write-Host "  Criterio: $($response.CriterioUtilizado)" -ForegroundColor Gray
    Write-Host "  DuracionMs: $($response.DuracionMs)" -ForegroundColor Gray

    if ($MostrarDetalle) {
        $response | ConvertTo-Json -Depth 10 | Write-Host
    }
}

Write-Title "ASSETRESOLVER TEST TOOL"
Write-Host "BaseUrl: $BaseUrl" -ForegroundColor Gray
Write-Host "DryRun: $DryRun" -ForegroundColor Gray

if ([string]::IsNullOrWhiteSpace($SampleIdufir) -or [string]::IsNullOrWhiteSpace($SampleRefCatastral) -or [string]::IsNullOrWhiteSpace($SampleDireccion)) {
    Write-Host "Aviso: faltan valores de muestra (SampleIdufir/SampleRefCatastral/SampleDireccion). Algunos escenarios pueden fallar por datos." -ForegroundColor Yellow
}

if ([string]::IsNullOrWhiteSpace($SampleIdufirAacc)) {
    Write-Host "Aviso: SampleIdufirAacc no informado. Se reutilizara SampleIdufir para escenarios AACC." -ForegroundColor Yellow
}

if ([string]::IsNullOrWhiteSpace($SampleRefCatastralAacc)) {
    Write-Host "Aviso: SampleRefCatastralAacc no informado. Se reutilizara SampleRefCatastral para escenarios AACC." -ForegroundColor Yellow
}

if ([string]::IsNullOrWhiteSpace($SampleDireccionAacc)) {
    Write-Host "Aviso: SampleDireccionAacc no informado. Se reutilizara SampleDireccion para escenarios AACC." -ForegroundColor Yellow
}

# Para el escenario tipificado, si no se pasan campos explícitos, construye un objeto de ejemplo
# con los campos que tenga informados (null = ignorado en el filtro SQL)
function Build-DireccionTipificadaObject {
    $dt = @{}
    if (-not [string]::IsNullOrWhiteSpace($SamplePais))              { $dt["Pais"]              = $SamplePais }
    if (-not [string]::IsNullOrWhiteSpace($SampleProvincia))         { $dt["Provincia"]         = $SampleProvincia }
    if (-not [string]::IsNullOrWhiteSpace($SampleComunidadAutonoma)) { $dt["ComunidadAutonoma"] = $SampleComunidadAutonoma }
    if (-not [string]::IsNullOrWhiteSpace($SampleMunicipio))         { $dt["Municipio"]         = $SampleMunicipio }
    if (-not [string]::IsNullOrWhiteSpace($SamplePoblacion))         { $dt["Poblacion"]         = $SamplePoblacion }
    if (-not [string]::IsNullOrWhiteSpace($SampleTipoVia))           { $dt["TipoVia"]           = $SampleTipoVia }
    if (-not [string]::IsNullOrWhiteSpace($SampleCalle))             { $dt["Calle"]             = $SampleCalle }
    if (-not [string]::IsNullOrWhiteSpace($SampleNumero))            { $dt["Numero"]            = $SampleNumero }
    if (-not [string]::IsNullOrWhiteSpace($SampleBloque))            { $dt["Bloque"]            = $SampleBloque }
    if (-not [string]::IsNullOrWhiteSpace($SamplePuerta))            { $dt["Puerta"]            = $SamplePuerta }
    if (-not [string]::IsNullOrWhiteSpace($SampleCodigoPostal))      { $dt["CodigoPostal"]      = $SampleCodigoPostal }
    if (-not [string]::IsNullOrWhiteSpace($SamplePlanta))            { $dt["Planta"]            = $SamplePlanta }
    return $dt
}

$hasCamposTipificados = (
    (-not [string]::IsNullOrWhiteSpace($SampleMunicipio)) -or
    (-not [string]::IsNullOrWhiteSpace($SampleCalle)) -or
    (-not [string]::IsNullOrWhiteSpace($SampleNumero)) -or
    (-not [string]::IsNullOrWhiteSpace($SampleCodigoPostal)) -or
    (-not [string]::IsNullOrWhiteSpace($SamplePais)) -or
    (-not [string]::IsNullOrWhiteSpace($SampleProvincia)) -or
    (-not [string]::IsNullOrWhiteSpace($SampleComunidadAutonoma)) -or
    (-not [string]::IsNullOrWhiteSpace($SamplePoblacion)) -or
    (-not [string]::IsNullOrWhiteSpace($SampleTipoVia)) -or
    (-not [string]::IsNullOrWhiteSpace($SampleBloque)) -or
    (-not [string]::IsNullOrWhiteSpace($SamplePuerta)) -or
    (-not [string]::IsNullOrWhiteSpace($SamplePlanta))
)

$sampleIdufirForAacc = if (-not [string]::IsNullOrWhiteSpace($SampleIdufirAacc)) { $SampleIdufirAacc } else { $SampleIdufir }
$sampleRefCatForAacc = if (-not [string]::IsNullOrWhiteSpace($SampleRefCatastralAacc)) { $SampleRefCatastralAacc } else { $SampleRefCatastral }
$sampleDireccionForAacc = if (-not [string]::IsNullOrWhiteSpace($SampleDireccionAacc)) { $SampleDireccionAacc } else { $SampleDireccion }
$hasIdufirForOrigenScenarios = -not [string]::IsNullOrWhiteSpace($sampleIdufirForAacc)

function Set-OrigenEnRequest {
    param(
        [hashtable]$Request,
        [ValidateSet("AAII", "AACC", "AMBOS")]
        [string]$Origen
    )

    switch ($Origen) {
        "AAII" {
            $Request.AAII_Search = $true
            $Request.AACC_Search = $false
        }
        "AACC" {
            $Request.AAII_Search = $false
            $Request.AACC_Search = $true
        }
        default {
            $Request.AAII_Search = $true
            $Request.AACC_Search = $true
        }
    }

    return $Request
}

$endpoint = "$BaseUrl/api/assets/GetAAIIInfo"

$scenarios = @(
    @{
        Key = "idufir-alias-default"
        Name = "IDUFIR directo por alias"
        ExpectedFound = $true
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.ExtractedData["IDUFIR"] = $SampleIdufir
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "idufir-mapeo-personalizado"
        Name = "IDUFIR con mapeo personalizado"
        ExpectedFound = $true
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.ExtractedData["ID_CRU"] = $SampleIdufir
            $req.MapeoIdufir = @("ID_CRU")
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "refcat-directa"
        Name = "Referencia catastral directa"
        ExpectedFound = $true
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.ExtractedData["ReferenciaCatastral"] = $SampleRefCatastral
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "idufir-override"
        Name = "IDUFIR con override"
        ExpectedFound = $true
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.IdufirOverride = $SampleIdufir
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "modo-or-dos-criterios"
        Name = "Modo OR con dos criterios"
        ExpectedFound = $true
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.ModoCombinacionCriterios = "OR"
            $req.ExtractedData["IDUFIR"] = $SampleIdufir
            $req.ExtractedData["ReferenciaCatastral"] = $SampleRefCatastral
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "modo-and-dos-criterios"
        Name = "Modo AND con dos criterios"
        ExpectedFound = $false
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.ModoCombinacionCriterios = "AND"
            $req.ExtractedData["IDUFIR"] = $SampleIdufir
            $req.ExtractedData["ReferenciaCatastral"] = $SampleRefCatastral
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "direccion-fuzzy"
        Name = "Busqueda fuzzy por direccion"
        ExpectedFound = $true
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.BusquedaDireccionHabilitada = $true
            $req.ExtractedData["Localizacion"] = $SampleDireccion
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "campos-all"
        Name = "RequestedFields con #ALL#"
        ExpectedFound = $true
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.ExtractedData["IDUFIR"] = $SampleIdufir
            $req.RequestedFields = @("#ALL#")
            return $req
        }
    },
    @{
        Key = "campos-limitados"
        Name = "RequestedFields limitados"
        ExpectedFound = $true
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.ExtractedData["IDUFIR"] = $SampleIdufir
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "direccion-tipificada"
        Name = "Busqueda tipificada por campos de direccion"
        ExpectedFound = $true
        Blocked = -not $hasCamposTipificados
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.BusquedaIdufirHabilitada = $false
            $req.BusquedaReferenciaCatastralHabilitada = $false
            $req.BusquedaDireccionHabilitada = $false
            $req.BusquedaDireccionTipificadaHabilitada = $true
            $req.DireccionTipificada = Build-DireccionTipificadaObject
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "direccion-tipificada-combinada"
        Name = "Tipificada + IDUFIR en modo OR"
        ExpectedFound = $true
        Blocked = -not $hasCamposTipificados
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.ModoCombinacionCriterios = "OR"
            $req.ExtractedData["IDUFIR"] = $SampleIdufir
            $req.BusquedaDireccionTipificadaHabilitada = $true
            $req.DireccionTipificada = Build-DireccionTipificadaObject
            $req.RequestedFields = @("DES_SERVICER", "DES_TIPO_AAII", "IMP_PT")
            return $req
        }
    },
    @{
        Key = "sin-datos"
        Name = "Request sin datos de busqueda"
        ExpectedFound = $false
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.BusquedaIdufirHabilitada = $false
            $req.BusquedaReferenciaCatastralHabilitada = $false
            $req.BusquedaDireccionHabilitada = $false
            $req.BusquedaDireccionTipificadaHabilitada = $false
            return $req
        }
    },
    @{
        Key = "origen-aaii-solo"
        Name = "Origen AAII habilitado y AACC deshabilitado"
        ExpectedFound = $null
        Blocked = [string]::IsNullOrWhiteSpace($SampleIdufir)
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAACC -eq 0) -and (@($resp.ActivosAACC).Count -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.AAII_Search = $true
            $req.AACC_Search = $false
            $req.ExtractedData["IDUFIR"] = $SampleIdufir
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "origen-aacc-solo"
        Name = "Origen AACC habilitado y AAII deshabilitado"
        ExpectedFound = $null
        Blocked = -not $hasIdufirForOrigenScenarios
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAAII -eq 0) -and (@($resp.ActivosAAII).Count -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.AAII_Search = $false
            $req.AACC_Search = $true
            $req.ExtractedData["IDUFIR"] = $sampleIdufirForAacc
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "origen-ambos-consistencia"
        Name = "Ambos origenes y consistencia de respuesta agregada"
        ExpectedFound = $null
        Blocked = -not $hasIdufirForOrigenScenarios
        AssertResponse = {
            param($resp)
            Test-SplitResponseConsistency -Response $resp
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req.AAII_Search = $true
            $req.AACC_Search = $true
            $req.ExtractedData["IDUFIR"] = $sampleIdufirForAacc
            $req.RequestedFields = @("ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "refcat-aaii-solo"
        Name = "Referencia catastral en origen AAII"
        ExpectedFound = $null
        Blocked = [string]::IsNullOrWhiteSpace($SampleRefCatastral)
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAACC -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AAII"
            $req.ExtractedData["ReferenciaCatastral"] = $SampleRefCatastral
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "refcat-aacc-solo"
        Name = "Referencia catastral en origen AACC"
        ExpectedFound = $null
        Blocked = [string]::IsNullOrWhiteSpace($sampleRefCatForAacc)
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAAII -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AACC"
            $req.ExtractedData["ReferenciaCatastral"] = $sampleRefCatForAacc
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "idufir-override-aaii-solo"
        Name = "IDUFIR override en origen AAII"
        ExpectedFound = $null
        Blocked = [string]::IsNullOrWhiteSpace($SampleIdufir)
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAACC -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AAII"
            $req.IdufirOverride = $SampleIdufir
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "idufir-override-aacc-solo"
        Name = "IDUFIR override en origen AACC"
        ExpectedFound = $null
        Blocked = [string]::IsNullOrWhiteSpace($sampleIdufirForAacc)
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAAII -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AACC"
            $req.IdufirOverride = $sampleIdufirForAacc
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "direccion-fuzzy-aaii-solo"
        Name = "Direccion fuzzy en origen AAII"
        ExpectedFound = $null
        Blocked = [string]::IsNullOrWhiteSpace($SampleDireccion)
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAACC -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AAII"
            $req.BusquedaDireccionHabilitada = $true
            $req.ExtractedData["Localizacion"] = $SampleDireccion
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "direccion-fuzzy-aacc-solo"
        Name = "Direccion fuzzy en origen AACC"
        ExpectedFound = $null
        Blocked = [string]::IsNullOrWhiteSpace($sampleDireccionForAacc)
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAAII -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AACC"
            $req.BusquedaDireccionHabilitada = $true
            $req.ExtractedData["Localizacion"] = $sampleDireccionForAacc
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "direccion-tipificada-aaii-solo"
        Name = "Direccion tipificada en origen AAII"
        ExpectedFound = $null
        Blocked = -not $hasCamposTipificados
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAACC -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AAII"
            $req.BusquedaIdufirHabilitada = $false
            $req.BusquedaReferenciaCatastralHabilitada = $false
            $req.BusquedaDireccionHabilitada = $false
            $req.BusquedaDireccionTipificadaHabilitada = $true
            $req.DireccionTipificada = Build-DireccionTipificadaObject
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "direccion-tipificada-aacc-solo"
        Name = "Direccion tipificada en origen AACC"
        ExpectedFound = $null
        Blocked = -not $hasCamposTipificados
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAAII -eq 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AACC"
            $req.BusquedaIdufirHabilitada = $false
            $req.BusquedaReferenciaCatastralHabilitada = $false
            $req.BusquedaDireccionHabilitada = $false
            $req.BusquedaDireccionTipificadaHabilitada = $true
            $req.DireccionTipificada = Build-DireccionTipificadaObject
            $req.RequestedFields = @("DES_SERVICER", "ID_ACTIVO_SAREB")
            return $req
        }
    },
    @{
        Key = "campos-all-aacc"
        Name = "RequestedFields #ALL# en origen AACC"
        ExpectedFound = $null
        Blocked = [string]::IsNullOrWhiteSpace($sampleIdufirForAacc)
        AssertResponse = {
            param($resp)
            (Test-SplitResponseShape -Response $resp) -and ([int]$resp.CountAAII -eq 0) -and ([int]$resp.CountAACC -ge 0)
        }
        BuildBody = {
            param($cid)
            $req = Build-BaseRequest -CorrelationId $cid
            $req = Set-OrigenEnRequest -Request $req -Origen "AACC"
            $req.ExtractedData["IDUFIR"] = $sampleIdufirForAacc
            $req.RequestedFields = @("#ALL#")
            return $req
        }
    }
)

$selectedScenarios = @(
    if ($Escenario -eq "all") {
        $scenarios
    }
    else {
        $scenarios | Where-Object { $_.Key -eq $Escenario }
    }
)

if ($selectedScenarios.Count -eq 0) {
    throw "Escenario '$Escenario' no encontrado. Usa -Escenario all o uno de: $($scenarios.Key -join ', ')"
}

$results = @()

foreach ($scenario in $selectedScenarios) {
    $scenarioKey = [string]$scenario.Key
    $scenarioName = [string]$scenario.Name
    $correlationId = Get-CorrelationId

    Write-Host "" 
    Write-Host "Ejecutando escenario: $scenarioKey" -ForegroundColor Yellow

    $bodyBuilder = $scenario.BuildBody
    $body = & $bodyBuilder $correlationId

    # Escenario bloqueado si faltan datos de muestra requeridos
    $isBlocked = $scenario.ContainsKey("Blocked") -and [bool]$scenario.Blocked
    if ($isBlocked) {
        Show-ScenarioResult -ScenarioKey $scenarioKey -ScenarioName $scenarioName -Outcome "blocked" -InvocationResult @{ IsError = $false; Response = @{ Found = $false; Count = 0; CriterioUtilizado = "n/a"; DuracionMs = 0 } }
        $results += @{ Key = $scenarioKey; Name = $scenarioName; Outcome = "blocked"; CorrelationId = $correlationId; IdsActivos = "-" }
        continue
    }

    $invocation = Invoke-AssetResolverRequest -Body $body -ScenarioKey $scenarioKey -DryRunMode:$DryRun
    $outcome = Get-OutcomeFromResponse -InvocationResult $invocation -ExpectedFound $scenario.ExpectedFound

    if ($outcome -eq "passed" -and -not $invocation.IsError -and $scenario.ContainsKey("AssertResponse")) {
        $assertion = & $scenario.AssertResponse $invocation.Response
        if (-not [bool]$assertion) {
            $outcome = "failed"
        }
    }

    $idsActivos = Get-ResolvedAssetIdsText -InvocationResult $invocation

    Show-ScenarioResult -ScenarioKey $scenarioKey -ScenarioName $scenarioName -Outcome $outcome -InvocationResult $invocation

    $results += @{
        Key = $scenarioKey
        Name = $scenarioName
        Outcome = $outcome
        CorrelationId = $correlationId
        IdsActivos = $idsActivos
    }
}

$passedCount  = @($results | Where-Object { $_.Outcome -eq "passed"  }).Count
$failedCount  = @($results | Where-Object { $_.Outcome -eq "failed"  }).Count
$blockedCount = @($results | Where-Object { $_.Outcome -eq "blocked" }).Count
$totalCount   = @($results).Count

Write-Title "RESUMEN"

# Calcular anchos de columna
$maxKeyLen  = ($results | ForEach-Object { $_.Key.Length  } | Measure-Object -Maximum).Maximum
$maxNameLen = ($results | ForEach-Object { $_.Name.Length } | Measure-Object -Maximum).Maximum
$maxIdsLen  = ($results | ForEach-Object { $_.IdsActivos.Length } | Measure-Object -Maximum).Maximum
$maxKeyLen  = [Math]::Max($maxKeyLen,  8)   # min "Escenario"
$maxNameLen = [Math]::Max($maxNameLen, 11)  # min "Descripcion"
$maxIdsLen  = [Math]::Max($maxIdsLen, 10)   # min "IdsActivos"
$colResult  = 10

$sepLine = "+-" + ("-" * $maxKeyLen) + "-+-" + ("-" * $maxNameLen) + "-+-" + ("-" * $colResult) + "-+-" + ("-" * $maxIdsLen) + "-+"
$header  = "| " + "Escenario".PadRight($maxKeyLen) + " | " + "Descripcion".PadRight($maxNameLen) + " | " + "Resultado".PadRight($colResult) + " | " + "IdsActivos".PadRight($maxIdsLen) + " |"

Write-Host $sepLine  -ForegroundColor Cyan
Write-Host $header   -ForegroundColor Cyan
Write-Host $sepLine  -ForegroundColor Cyan

foreach ($r in $results) {
    $icon = switch ($r.Outcome) {
        "passed"  { "OK      " }
        "failed"  { "FAIL    " }
        "blocked" { "BLOCKED " }
        default   { $r.Outcome.PadRight($colResult) }
    }
    $color = switch ($r.Outcome) {
        "passed"  { "Green"  }
        "failed"  { "Red"    }
        "blocked" { "Yellow" }
        default   { "Gray"   }
    }

    $rowKey  = $r.Key.PadRight($maxKeyLen)
    $rowName = $r.Name.PadRight($maxNameLen)
    $rowIcon = $icon.PadRight($colResult)
    $rowIds  = $r.IdsActivos.PadRight($maxIdsLen)

    Write-Host ("| " + $rowKey + " | " + $rowName + " | " + $rowIcon + " | " + $rowIds + " |") -ForegroundColor $color
}

Write-Host $sepLine -ForegroundColor Cyan
Write-Host ""
Write-Host ("  Total: $totalCount   Passed: $passedCount   Failed: $failedCount   Blocked: $blockedCount") -ForegroundColor $(if ($failedCount -gt 0) { "Red" } elseif ($blockedCount -gt 0) { "Yellow" } else { "Green" })
Write-Host $sepLine -ForegroundColor Cyan

if ($failedCount -gt 0) {
    exit 1
}

exit 0
