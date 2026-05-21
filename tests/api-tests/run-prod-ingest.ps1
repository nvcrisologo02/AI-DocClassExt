param(
    [Parameter(Mandatory = $true)]
    [string]$FunctionKey,

    [Parameter(Mandatory = $false)]
    [string]$DocumentPath = 'C:\temp\MVP\documento-ia-clasificacion-mvp\docs\auxiliares\Ejemplos\2815510_NS_2815510.pdf'
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

$CorrelationId = 'PROD-RETEST-' + (Get-Date -Format 'yyyyMMdd-HHmmss')

if (-not (Test-Path $DocumentPath)) {
    throw "No existe documento: $DocumentPath"
}

$documentBase64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($DocumentPath))
$body = @{
    instrucciones = @{
        skipDuplicateCheck = $true
        forceReprocess = $true
        SkipGDCUpload = $false
        classification = @{ provider = 'auto'; model = 'auto'; umbral = 0.5 }
        extraction = @{ model = 'auto'; umbral = 0.8 }
    }
    documento = @{
        name = '2815510_NS_2815510.pdf'
        content = @{ base64 = $documentBase64 }
    }
    trazabilidad = @{
        correlationId = $CorrelationId
        submittedBy = 'usuario.prueba@sareb.es'
        idGDC = $null
        idActivo = '354937'
    }
} | ConvertTo-Json -Depth 10

$uri = "https://srbappprodocai.azurewebsites.net/api/IngestDocument?code=$FunctionKey"

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType 'application/json; charset=utf-8'

    $result = [pscustomobject]@{
        instanceId = $response.instanceId
        correlationId = $response.correlationId
        statusQueryUri = $response.statusQueryUri
        submittedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }

    $result | ConvertTo-Json -Depth 5 | Tee-Object -FilePath 'tests\api-tests\last-prod-run.json'
}
catch {
    if ($_.Exception.Response) {
        $resp = $_.Exception.Response
        Write-Host ('StatusCode: ' + [int]$resp.StatusCode + ' ' + $resp.StatusCode)
        $reader = New-Object IO.StreamReader($resp.GetResponseStream())
        $txt = $reader.ReadToEnd()
        if ($txt) {
            Write-Host 'Body:'
            Write-Host $txt
        }
    }
    else {
        Write-Host $_.Exception.Message
    }

    exit 1
}
