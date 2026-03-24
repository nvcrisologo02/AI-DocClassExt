<#
.SYNOPSIS
    Test aislado de consulta GDC (searchEntities + create opcional).
    Reproduce el caso: searchEntities dice "no existe" pero create devuelve DOC_OBJECT_EXISTS.

.DESCRIPTION
    Envía las mismas peticiones SOAP que GdcService.cs directamente al endpoint GDC,
    sin pasar por el pipeline de Functions. Muestra el XML de petición y respuesta
    completos para diagnosticar problemas de parsing/filtro.

.PARAMETER IdActivo
    ID del activo a buscar. Default: "9988776655"

.PARAMETER MD5
    Hash MD5/checksum del documento. Default: "b708a49a16515f4062e41c529ec22c45"

.PARAMETER Matricula
    Matrícula del documento. Default: "AI-01-NOTS-01"

.PARAMETER SettingsFile
    Ruta al local.settings.json. Por defecto busca en la ubicación estándar del proyecto.

.PARAMETER AlsoCreate
    Si se especifica, también prueba la operación create (con contenido dummy)
    para confirmar DOC_OBJECT_EXISTS.

.PARAMETER UseMock
    Si se especifica, apunta al mock local (http://localhost:8083) en vez del settings.

.EXAMPLE
    .\test-gdc-consultar-aislado.ps1
    .\test-gdc-consultar-aislado.ps1 -IdActivo "9988776655" -MD5 "b708a49a16515f4062e41c529ec22c45" -AlsoCreate
    .\test-gdc-consultar-aislado.ps1 -UseMock
#>
[CmdletBinding()]
param(
    [string]$IdActivo  = "9988776655",
    [string]$MD5       = "b708a49a16515f4062e41c529ec22c45",
    [string]$Matricula = "AI-01-NOTS-01",
    [string]$SettingsFile = "",
    [switch]$AlsoCreate,
    [switch]$UseMock
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# -- Helpers ------------------------------------------------------------------

function Write-Section([string]$title) {
    Write-Host ""
    Write-Host ("-" * 60) -ForegroundColor DarkGray
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host ("-" * 60) -ForegroundColor DarkGray
}

function Write-Ok([string]$msg)   { Write-Host "  [OK]   $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Fail([string]$msg) { Write-Host "  [FAIL] $msg" -ForegroundColor Red }
function Write-Info([string]$msg) { Write-Host "  [INFO] $msg" -ForegroundColor Gray }

function PrettyXml([string]$xml) {
    try {
        $xdoc = New-Object System.Xml.XmlDocument
        $xdoc.LoadXml($xml)
        $sw = New-Object System.IO.StringWriter
        $writer = New-Object System.Xml.XmlTextWriter($sw)
        $writer.Formatting = [System.Xml.Formatting]::Indented
        $writer.Indentation = 2
        $xdoc.WriteTo($writer)
        return $sw.ToString()
    } catch {
        return $xml
    }
}

function XmlEscape([string]$s) {
    return $s -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;' -replace '"','&quot;' -replace "'","&apos;"
}

# -- Load settings ------------------------------------------------------------

$scriptDir   = Split-Path $MyInvocation.MyCommand.Path -Parent
$projectRoot = Resolve-Path (Join-Path $scriptDir "..\..")

if ($UseMock) {
    $Endpoint        = "http://localhost:8083/sintws/IDocService"
    $ApplicationId   = "TEST"
    $Username        = "test"
    $Password        = "test"
    $NominalUser     = ""
    $ClaseExpediente = "AI04"
    $Servicer        = ""
    $EntidadOrigen   = ""
    $ProcesoCarga    = ""
    $TipoExpediente  = "AI"
    $Publico         = "verdadero"
    $OrigenDocumento = "TEST"
    $RepositoryId    = ""
    $RepositoryName  = ""
    $HttpBasicUser   = ""
    $HttpBasicPass   = ""
    Write-Warn "Modo MOCK: apuntando a $Endpoint"
} else {
    if (-not $SettingsFile) {
        $SettingsFile = Join-Path $projectRoot "src\backend\DocumentIA.Functions\local.settings.json"
    }
    if (-not (Test-Path $SettingsFile)) {
        Write-Fail "No se encontro local.settings.json en: $SettingsFile"
        exit 1
    }

    $cfg = Get-Content $SettingsFile | ConvertFrom-Json
    $v   = $cfg.Values

    $Endpoint        = $v."GDC:Endpoint"
    $ApplicationId   = $v."GDC:ApplicationId"
    $Username        = $v."GDC:Username"
    $Password        = $v."GDC:Password"
    $NominalUser     = $v."GDC:NominalUser"
    $ClaseExpediente = $v."GDC:ClaseExpediente"
    $Servicer        = $v."GDC:Servicer"
    $EntidadOrigen   = $v."GDC:EntidadOrigen"
    $ProcesoCarga    = $v."GDC:ProcesoCarga"
    $TipoExpediente  = if ($v."GDC:TipoExpediente") { $v."GDC:TipoExpediente" } else { "AI" }
    $Publico         = if ($v."GDC:Publico")         { $v."GDC:Publico" }         else { "verdadero" }
    $OrigenDocumento = $v."GDC:OrigenDocumento"
    $RepositoryId    = $v."GDC:RepositoryId"
    $RepositoryName  = $v."GDC:RepositoryName"
    $HttpBasicUser   = $v."GDC:HttpBasicUsername"
    $HttpBasicPass   = $v."GDC:HttpBasicPassword"
}

# -- Summary ------------------------------------------------------------------

Write-Section "PARAMETROS DE PRUEBA"
Write-Info "Endpoint         : $Endpoint"
Write-Info "ApplicationId    : $ApplicationId"
Write-Info "Username         : $Username"
Write-Info "ClaseExpediente  : '$ClaseExpediente'"
Write-Info "RepositoryId     : '$RepositoryId'"
Write-Info ""
Write-Info "IdActivo         : $IdActivo"
Write-Info "MD5/checksum     : $MD5"
Write-Info "Matricula        : $Matricula"

$estrategia = if ([string]::IsNullOrWhiteSpace($ClaseExpediente)) { "checksum-only (ClaseExpediente vacio)" } else { "EntityExpression IN expediente.id_expediente + EQUALS checksum" }
Write-Info ""
Write-Info "Estrategia filtro: $estrategia"

# -- HTTP helper --------------------------------------------------------------

function Invoke-Soap([string]$soapAction, [string]$bodyXml, [string]$operacion) {
    $envelope = '<?xml version="1.0" encoding="utf-8"?>' +
        '<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">' +
        '<soap:Body>' + $bodyXml + '</soap:Body>' +
        '</soap:Envelope>'

    Write-Section "REQUEST: $operacion"
    Write-Host (PrettyXml $envelope) -ForegroundColor DarkCyan

    # Skip SSL for dev (same as DangerousAcceptAnyServerCertificateValidator in code)
    if (-not ("TrustAll" -as [type])) {
        Add-Type @"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public class TrustAll {
    public static void Enable() {
        ServicePointManager.ServerCertificateValidationCallback =
            (object s, X509Certificate c, X509Chain ch, SslPolicyErrors e) => true;
    }
}
"@
    }
    [TrustAll]::Enable()

    $headers = @{ "Content-Type" = "application/soap+xml; charset=utf-8"; "SOAPAction" = $soapAction }

    # HTTP Basic Auth if configured
    if ($HttpBasicUser) {
        $pair  = "${HttpBasicUser}:${HttpBasicPass}"
        $bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
        $b64   = [Convert]::ToBase64String($bytes)
        $headers["Authorization"] = "Basic $b64"
    }

    try {
        $response = Invoke-WebRequest -Uri $Endpoint -Method POST -Body $envelope `
            -Headers $headers -UseBasicParsing -TimeoutSec 30
        return $response.Content
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.Value__
        $rawResp    = $null
        try {
            $reader  = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $rawResp = $reader.ReadToEnd()
        } catch {}
        Write-Warn "HTTP $statusCode devuelto por el servidor"
        if ($rawResp) { return $rawResp }
        throw
    }
}

# -- BUILD: Identity element --------------------------------------------------

$safeAppId  = XmlEscape $ApplicationId
$safeUser   = XmlEscape $Username
$safeNominal= XmlEscape $NominalUser

$identityXml = "<ns0:applicationId>$safeAppId</ns0:applicationId>" +
               "<ns0:nominalUser>$safeNominal</ns0:nominalUser>" +
               "<ns0:username>$safeUser</ns0:username>"

# -- BUILD: searchEntities filter (mirrors GdcService.cs logic) --------------

$safeIdActivo = XmlEscape $IdActivo
$safeMd5      = XmlEscape $MD5

if (-not [string]::IsNullOrWhiteSpace($ClaseExpediente)) {
    $filterXml =
        '<ns2:filter xsi:type="ns2:SetExpression">' +
        '<ns2:expressions>' +
        '<ns2:Expression xsi:type="ns2:EntityExpression">' +
        '<ns2:condition>IN</ns2:condition>' +
        '<ns2:entityName>expediente</ns2:entityName>' +
        '<ns2:fieldName>id_expediente</ns2:fieldName>' +
        '<ns2:value xsi:type="ns2:StringValueList">' +
        "<ns2:values><ns1:string>$safeIdActivo</ns1:string></ns2:values>" +
        '</ns2:value>' +
        '</ns2:Expression>' +
        '<ns2:Expression xsi:type="ns2:FieldExpression">' +
        '<ns2:condition>EQUALS</ns2:condition>' +
        '<ns2:fieldName>checksum</ns2:fieldName>' +
        '<ns2:value xsi:type="ns2:StringValue">' +
        "<ns2:value>$safeMd5</ns2:value>" +
        '</ns2:value>' +
        '</ns2:Expression>' +
        '</ns2:expressions>' +
        '<ns2:operator>AND</ns2:operator>' +
        '</ns2:filter>'
} else {
    $filterXml =
        '<ns2:filter xsi:type="ns2:FieldExpression">' +
        '<ns2:condition>EQUALS</ns2:condition>' +
        '<ns2:fieldName>checksum</ns2:fieldName>' +
        '<ns2:value xsi:type="ns2:StringValue">' +
        "<ns2:value>$safeMd5</ns2:value>" +
        '</ns2:value>' +
        '</ns2:filter>'
}

$repoXml = if ([string]::IsNullOrWhiteSpace($RepositoryId)) {
    "<ns1:arg2/>"
} else {
    "<ns1:arg2>" +
    '<ns2:DocRepository xmlns:ns2="http://doc.model.api.sint.sareb.es" xsi:type="ns2:DocRepository">' +
    "<ns2:id>$(XmlEscape $RepositoryId)</ns2:id>" +
    "<ns2:name>$(XmlEscape $RepositoryName)</ns2:name>" +
    "</ns2:DocRepository>" +
    "</ns1:arg2>"
}

$searchBody =
    '<ns1:searchEntities xmlns:ns1="http://services.api.sint.sareb.es/" xmlns:ns0="http://auth.model.api.sint.sareb.es" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' +
    '<ns1:arg0 xsi:type="ns0:Identity">' + $identityXml + '</ns1:arg0>' +
    '<ns1:arg1 xmlns:ns2="http://search.model.api.sint.sareb.es" xmlns:ns3="http://data.model.api.sint.sareb.es" xsi:type="ns2:Query">' +
    '<ns2:entityTypeId>document</ns2:entityTypeId>' +
    $filterXml +
    '<ns2:firstResultIndex>1</ns2:firstResultIndex>' +
    '<ns2:maxResults>1</ns2:maxResults>' +
    '<ns2:orderingField xsi:type="ns2:OrderingField">' +
    '<ns2:ascending>false</ns2:ascending>' +
    '<ns2:fieldName>create_date</ns2:fieldName>' +
    '</ns2:orderingField>' +
    '<ns2:resultsProfile xsi:type="ns3:EntityProfile">' +
    '<ns3:fieldNames>' +
    '<ns1:string>checksum</ns1:string>' +
    '<ns1:string>nombre_fichero</ns1:string>' +
    '</ns3:fieldNames>' +
    '<ns3:ignoreContent>true</ns3:ignoreContent>' +
    '<ns3:ignoreMetadata>false</ns3:ignoreMetadata>' +
    '</ns2:resultsProfile>' +
    '</ns1:arg1>' +
    $repoXml +
    '</ns1:searchEntities>'

# -- CALL: searchEntities -----------------------------------------------------

$rawSearch = Invoke-Soap "" $searchBody "searchEntities"

Write-Section "RESPONSE: searchEntities (RAW)"
Write-Host (PrettyXml $rawSearch) -ForegroundColor White

# -- PARSE: search response (mirrors GdcService.cs parsing) ------------------

Write-Section "ANALISIS searchEntities"

$xdoc = New-Object System.Xml.XmlDocument
try {
    $xdoc.LoadXml($rawSearch)

    # Check SOAP Fault
    $faults = $xdoc.GetElementsByTagName("Fault")
    if ($faults.Count -gt 0) {
        Write-Fail "Respuesta contiene SOAP Fault -> ConsultarDocumento devolveria (false, null)"
        Write-Info "Fault text: $($faults[0].InnerText)"
    } else {
        # Check totalItemsResult (current parsing logic)
        $totalNodes = $xdoc.GetElementsByTagName("totalItemsResult")
        if ($totalNodes.Count -gt 0) {
            $total = $totalNodes[0].InnerText
            Write-Info "totalItemsResult = '$total'"
            if ([int]$total -gt 0) {
                $idNodes = $xdoc.GetElementsByTagName("id")
                $objId   = if ($idNodes.Count -gt 0) { $idNodes[0].InnerText } else { "(no id tag)" }
                Write-Ok  "Documento ENCONTRADO - ObjectId = $objId"
            } else {
                Write-Fail "totalItemsResult = 0 -> GdcService dice: NO EXISTE"
                Write-Warn "Esto es el bug: searchEntities no encuentra el documento."
            }
        } else {
            Write-Warn "No hay elemento <totalItemsResult> en la respuesta."
            Write-Info "El parsing actual requiere totalItemsResult > 0 para considerar que existe."

            # Show all tags present to help diagnose alternate response structures
            $allTags = $xdoc.SelectNodes("//*") | ForEach-Object { $_.LocalName } | Sort-Object -Unique
            Write-Info "Elementos XML presentes: $($allTags -join ', ')"

            # Try to find any entity/id-like node
            $idCandidates = @("entityId","ObjectId","objectId","id","object_id","entities")
            foreach ($tag in $idCandidates) {
                $nodes = $xdoc.GetElementsByTagName($tag)
                if ($nodes.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($nodes[0].InnerText)) {
                    Write-Warn "Tag alternativo encontrado: <$tag> = '$($nodes[0].InnerText)'"
                    Write-Warn "-> El parsing no lo detecta porque busca totalItemsResult."
                }
            }
        }
    }
} catch {
    Write-Fail "No se pudo parsear el XML de respuesta: $_"
}

# -- OPTIONAL: create ---------------------------------------------------------

if ($AlsoCreate) {
    Write-Section "REPRODUCCION: create (contenido dummy, mismo checksum)"
    Write-Warn "Esto intentara crear el documento. Si ya existe -> DOC_OBJECT_EXISTS confirmado."

    function BuildStringField([string]$name, [string]$value) {
        return '<ns3:Field xsi:type="ns3:SingleField">' +
               "<ns3:name>$(XmlEscape $name)</ns3:name>" +
               '<ns3:fieldValue xsi:type="ns4:StringFieldValue">' +
               "<ns4:value>$(XmlEscape $value)</ns4:value>" +
               '</ns3:fieldValue>' +
               '</ns3:Field>'
    }

    $expXml = if (-not [string]::IsNullOrWhiteSpace($ClaseExpediente)) {
        '<ns3:Field xsi:type="ns3:SingleField">' +
        '<ns3:name>expediente</ns3:name>' +
        '<ns3:fieldValue xsi:type="ns4:EntityFieldValue">' +
        '<ns4:extraFields>' +
        '<ns3:Field xsi:type="ns3:SingleField">' +
        '<ns3:name>id_expediente</ns3:name>' +
        '<ns3:fieldValue xsi:type="ns4:StringFieldValue">' +
        "<ns4:value>$safeIdActivo</ns4:value>" +
        '</ns3:fieldValue>' +
        '</ns3:Field>' +
        '<ns3:Field xsi:type="ns3:SingleField">' +
        '<ns3:name>clase_expediente</ns3:name>' +
        '<ns3:fieldValue xsi:type="ns4:StringFieldValue">' +
        "<ns4:value>$(XmlEscape $ClaseExpediente)</ns4:value>" +
        '</ns3:fieldValue>' +
        '</ns3:Field>' +
        '</ns4:extraFields>' +
        '</ns3:fieldValue>' +
        '</ns3:Field>'
    } else { "" }

    $contentFieldName = "Content"
    $dummyBase64 = "SGVsbG8gd29ybGQh"  # "Hello world!" -- minimal content

    $createBody =
        '<ns1:create xmlns:ns1="http://services.api.sint.sareb.es/"' +
        ' xmlns:ns0="http://auth.model.api.sint.sareb.es"' +
        ' xmlns:ns2="http://data.model.api.sint.sareb.es"' +
        ' xmlns:ns3="http://field.data.model.api.sint.sareb.es"' +
        ' xmlns:ns4="http://fieldvalue.data.model.api.sint.sareb.es"' +
        ' xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' +
        '<ns1:arg0 xsi:type="ns0:Identity">' + $identityXml + '</ns1:arg0>' +
        '<ns1:arg1 xsi:type="ns2:Entity">' +
        '<ns2:typeId>document</ns2:typeId>' +
        '<ns2:fields>' +
        (if ($OrigenDocumento) { BuildStringField "origen_documento" $OrigenDocumento } else { "" }) +
        (if ($EntidadOrigen)   { BuildStringField "entidad_origen"   $EntidadOrigen   } else { "" }) +
        (if ($ProcesoCarga)    { BuildStringField "proceso_carga"    $ProcesoCarga    } else { "" }) +
        (BuildStringField "publico"         $Publico) +
        (if ($Servicer)        { BuildStringField "servicer"         $Servicer        } else { "" }) +
        (if ($TipoExpediente)  { BuildStringField "tipo_expediente"  $TipoExpediente  } else { "" }) +
        (BuildStringField "nombre_documento" "test-aislado.pdf") +
        (BuildStringField "nombre_fichero"   "test-aislado.pdf") +
        (BuildStringField "matricula_doc"    $Matricula) +
        (BuildStringField "checksum"         $MD5) +
        $expXml +
        '<ns3:Field xsi:type="ns3:SingleField">' +
        "<ns3:name>$(XmlEscape $contentFieldName)</ns3:name>" +
        '<ns3:fieldValue xsi:type="ns4:FileContentFieldValue">' +
        "<ns4:dataSource>$dummyBase64</ns4:dataSource>" +
        '</ns3:fieldValue>' +
        '</ns3:Field>' +
        '</ns2:fields>' +
        '</ns1:arg1>' +
        '</ns1:create>'

    $rawCreate = Invoke-Soap "" $createBody "create"

    Write-Section "RESPONSE: create (RAW)"
    Write-Host (PrettyXml $rawCreate) -ForegroundColor White

    Write-Section "ANALISIS create"
    $xdoc2 = New-Object System.Xml.XmlDocument
    try {
        $xdoc2.LoadXml($rawCreate)
        $faults2 = $xdoc2.GetElementsByTagName("Fault")
        if ($faults2.Count -gt 0) {
            $errCode = $xdoc2.GetElementsByTagName("errorCode")
            $errMsg  = $xdoc2.GetElementsByTagName("faultstring")
            $code    = if ($errCode.Count -gt 0) { $errCode[0].InnerText } else { "" }
            $msg     = if ($errMsg.Count -gt 0)  { $errMsg[0].InnerText  } else { "" }
            if ($code -eq "DOC_OBJECT_EXISTS") {
                Write-Fail "BUG CONFIRMADO: errorCode=DOC_OBJECT_EXISTS -- el documento SI existe pero searchEntities no lo encontro."
                Write-Info "faultstring : $msg"
            } else {
                Write-Warn "SOAP Fault inesperado: errorCode=$code faultstring=$msg"
            }
        } else {
            $retNodes = $xdoc2.GetElementsByTagName("return")
            if ($retNodes.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($retNodes[0].InnerText)) {
                Write-Ok "create exitoso - nuevo ObjectId = $($retNodes[0].InnerText)"
            } else {
                Write-Warn "create sin Fault y sin <return>. Ver XML completo arriba."
            }
        }
    } catch {
        Write-Fail "No se pudo parsear respuesta create: $_"
    }
}

Write-Section "FIN"
Write-Info "Si searchEntities devolvio 0 resultados pero create dice DOC_OBJECT_EXISTS, las causas posibles son:"
Write-Info "  1) El documento fue creado SIN campo expediente (ClaseExpediente estaba vacio entonces)"
Write-Info "     -> Fix: buscar tambien por checksum-only y unir resultados (OR), o usar checksum solo."
Write-Info "  2) La respuesta de searchEntities no contiene <totalItemsResult> -> parsing falla silenciosamente"
Write-Info "     -> Fix: revisar tags alternativos en la respuesta (ver 'Elementos XML presentes' arriba)"
Write-Info "  3) El filtro EntityExpression IN no es compatible con esta version del servidor SINTWS"
Write-Info "     -> Fix: cambiar a FieldExpression EQUALS sobre un campo plano."
