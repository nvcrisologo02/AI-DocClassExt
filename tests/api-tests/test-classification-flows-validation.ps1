param(
    [string]$PdfPath = "C:\temp\Para_IA\test\ESCR-01 3e74f31cbc5de9f1bff264452e0864b9.pdf",
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",
    [string]$SubmittedBy = "usuario.prueba@sareb.es",
    [double]$ClassificationUmbral = 0.50,
    [int]$MaxPollRetries = 30,
    [int]$PollDelaySeconds = 2
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  TEST: VALIDACIÓN DE FLUJOS DE CLASIFICACIÓN                              ║" -ForegroundColor Cyan
Write-Host "║  Archivo: $(Split-Path $PdfPath -Leaf)  ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $PdfPath)) {
    Write-Host "❌ PDF no encontrado: $PdfPath" -ForegroundColor Red
    exit 1
}

Write-Host "📄 PDF encontrado: $(Get-Item $PdfPath | Select-Object -ExpandProperty FullName)" -ForegroundColor Green
Write-Host "🔌 Endpoint: $Endpoint" -ForegroundColor Green
Write-Host ""

$artifactDir = "C:\temp\MVP\documento-ia-clasificacion-mvp\tests\api-tests\artifacts"
if (-not (Test-Path $artifactDir)) {
    New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
}

function Encode-PdfToBase64 {
    param([string]$PdfPath)
    $bytes = [System.IO.File]::ReadAllBytes($PdfPath)
    return [Convert]::ToBase64String($bytes)
}

function Resolve-StatusUri {
    param([string]$StatusUri)
    if ($StatusUri -match "http://localhost/") {
        return ($StatusUri -replace "http://localhost/", "http://localhost:7071/")
    }
    return $StatusUri
}

function Extract-ClassificationData {
    param($result)
    $data = [ordered]@{
        Tipologia = $null
        Confianza = $null
        Proveedor = $null
        FallbackLLM = $null
        FallbackRazon = $null
        DetalleProveedores = $null
    }

    if ($result.extraccionResultado -and $result.extraccionResultado.Clasificacion) {
        $c = $result.extraccionResultado.Clasificacion
        $data.Tipologia = $c.TipologiaDetectada ?? $c.tipologiaDetectada
        $data.Confianza = $c.Confianza ?? $c.confianza
        $data.Proveedor = $c.ProveedorClasif ?? $c.proveedorClasif
        $data.FallbackLLM = $c.FallbackLLM ?? $c.fallbackLLM
        $data.FallbackRazon = $c.FallbackRazon ?? $c.fallbackRazon
        $data.DetalleProveedores = $c.DetalleProveedores ?? $c.detalleProveedores
        return $data
    }

    if ($result.output -and $result.output.DetalleEjecucion) {
        $detalle = $result.output.DetalleEjecucion
        if ($detalle.Clasificacion) {
            try {
                $clasif = $detalle.Clasificacion | ConvertFrom-Json
                $data.Tipologia = $clasif.TipologiaDetectada
                $data.Confianza = $clasif.Confianza
                $data.Proveedor = $clasif.ProveedorClasif
                $data.FallbackLLM = $clasif.FallbackLLM
                $data.FallbackRazon = $clasif.FallbackRazon
                $data.DetalleProveedores = $clasif.DetalleProveedores
                return $data
            }
            catch {
                if ($detalle.Clasificacion -match 'TipologiaDetectada=([^;]+)') {
                    $data.Tipologia = $matches[1]
                }
                if ($detalle.Clasificacion -match 'Confianza=([0-9.]+)') {
                    $data.Confianza = [double]$matches[1]
                }
                if ($detalle.Clasificacion -match 'ProveedorClasif=([^;]+)') {
                    $data.Proveedor = $matches[1]
                }
                return $data
            }
        }
    }

    return $data
}

function Wait-ForClassificationResult {
    param([string]$StatusUri, [string]$FlowName)

    $resolvedUri = Resolve-StatusUri $StatusUri
    Write-Host "   ⏳ Esperando resultado ($MaxPollRetries intentos, cada ${PollDelaySeconds}s)..." -ForegroundColor Gray

    for ($i = 0; $i -lt $MaxPollRetries; $i++) {
        Start-Sleep -Seconds $PollDelaySeconds
        Write-Host "   ⏱️ Intento $($i+1)/$MaxPollRetries..." -ForegroundColor Gray
        try {
            $statusResponse = Invoke-WebRequest -Uri $resolvedUri -Method GET -ErrorAction Stop
            if ($statusResponse.StatusCode -eq 200) {
                $result = $statusResponse.Content | ConvertFrom-Json
                $debugPath = "$artifactDir\result-$FlowName.json"
                $statusResponse.Content | Out-File -FilePath $debugPath -Encoding UTF8 -Force
                return $result
            }
        }
        catch {
        }
    }
    return $null
}

function Test-ClassificationFlow {
    param([string]$FlowName, [string]$ProviderKey, [string]$Description)

    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
    Write-Host "🧪 Flujo: $FlowName" -ForegroundColor Yellow
    Write-Host "   Descripción: $Description" -ForegroundColor Gray
    Write-Host "   Provider/Flow Key: $ProviderKey" -ForegroundColor Gray
    Write-Host ""

    try {
        $pdfBase64 = Encode-PdfToBase64 $PdfPath
        $fileName = Split-Path $PdfPath -Leaf

        $instrucciones = [ordered]@{
            skipDuplicateCheck = $true
            forceReprocess = $true
            skipGDCUpload = $true
            classificationOnly = $true
            classification = [ordered]@{
                provider = $ProviderKey
                model = "auto"
                umbral = $ClassificationUmbral
            }
            extraction = [ordered]@{
                provider = "auto"
                model = "auto"
                umbral = 0.80
            }
            assetResolver = [ordered]@{
                enabled = $false
            }
        }

        $requestBody = [ordered]@{
            instrucciones = $instrucciones
            documento = [ordered]@{
                name = $fileName
                content = [ordered]@{
                    base64 = $pdfBase64
                }
            }
            trazabilidad = [ordered]@{
                correlationId = "FLOW-TEST-$(Get-Date -Format 'yyyyMMdd-HHmmss-fff')-$FlowName"
                submittedBy = $SubmittedBy
                idGDC = $null
                idActivo = $null
            }
        } | ConvertTo-Json -Depth 20

        $startTime = Get-Date
        $response = Invoke-WebRequest -Uri $Endpoint -Method POST -ContentType "application/json" -Body $requestBody -ErrorAction Stop
        $initResponse = $response.Content | ConvertFrom-Json
        Write-Host "   ✓ Solicitud aceptada (instancia: $($initResponse.instanceId.Substring(0,8))...)" -ForegroundColor Cyan

        $result = Wait-ForClassificationResult -StatusUri $initResponse.statusQueryUri -FlowName $FlowName
        $elapsed = $(Get-Date) - $startTime

        if (-not $result) {
            Write-Host "❌ Timeout esperando resultado" -ForegroundColor Red
            return @{ Success = $false; FlowName = $FlowName; Error = "Timeout" }
        }

        $classData = Extract-ClassificationData -result $result
        $tipoColor = if ($classData.Tipologia -eq "Desconocido" -or $classData.Tipologia -eq "RESTO") { "Red" } elseif ($classData.Tipologia) { "Green" } else { "Gray" }
        $confColor = if ($classData.Confianza -ge $ClassificationUmbral) { "Green" } else { "Yellow" }

        Write-Host "✅ Resultado obtenido (${($elapsed.TotalSeconds).ToString('0.00')}s)" -ForegroundColor Green
        Write-Host ""
        Write-Host "   📊 Resultado:" -ForegroundColor Cyan
        Write-Host "   ├─ Tipología:        $(if($classData.Tipologia) { $classData.Tipologia } else { 'N/A' })" -ForegroundColor $tipoColor
        Write-Host "   ├─ Confianza:        $(if($classData.Confianza) { $classData.Confianza.ToString('0.000') } else { 'N/A' })" -ForegroundColor $confColor
        Write-Host "   ├─ Proveedor:        $(if($classData.Proveedor) { $classData.Proveedor } else { 'N/A' })" -ForegroundColor Cyan
        Write-Host "   ├─ Fallback LLM:     $(if($classData.FallbackLLM) {'✓ Sí'} else {'✗ No'})" -ForegroundColor $(if($classData.FallbackLLM) {"Yellow"} else {"Gray"})
        if ($classData.FallbackRazon) { Write-Host "   └─ Razón Fallback:   $($classData.FallbackRazon)" -ForegroundColor Yellow }

        if ($classData.DetalleProveedores -and $classData.DetalleProveedores.Count -gt 0) {
            Write-Host ""
            Write-Host "   📋 Evaluación de proveedores:" -ForegroundColor Cyan
            foreach ($item in $classData.DetalleProveedores) {
                $tipDetalle = $item.Tipologia ?? $item.tipologia ?? "N/A"
                $confDetalle = if ($item.Confianza) { $item.Confianza.ToString('0.000') } else { "N/A" }
                $motivo = $item.MotivoDescarte ?? $item.motivoDescarte ?? "Satisfactorio"
                $proveedorDetalle = $item.Proveedor ?? $item.proveedor ?? "?"
                Write-Host "      • $proveedorDetalle`: $tipDetalle (conf: $confDetalle) - $motivo" -ForegroundColor Gray
            }
        }

        return @{ Success = $true; FlowName = $FlowName; Tipologia = $classData.Tipologia; Confianza = $classData.Confianza; Proveedor = $classData.Proveedor; FallbackUsed = $classData.FallbackLLM; Elapsed = $elapsed.TotalSeconds }
    }
    catch {
        Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        return @{ Success = $false; FlowName = $FlowName; Error = $_.Exception.Message }
    }
}

$resultados = @()
$resultados += Test-ClassificationFlow -FlowName "DefaultFlow" -ProviderKey "auto" -Description "Flujo por defecto: rules → di → fallback gpt"
$resultados += Test-ClassificationFlow -FlowName "DI Only" -ProviderKey "di" -Description "Solo Azure Document Intelligence"
$resultados += Test-ClassificationFlow -FlowName "GPT Only" -ProviderKey "gpt" -Description "Solo GPT-4o-mini"

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  📊 RESUMEN DE PRUEBAS                                                   ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$tabla = @()
foreach ($r in $resultados) {
    if ($r.Success) {
        $tabla += [PSCustomObject]@{ Flujo = $r.FlowName; Tipologia = $r.Tipologia ?? "?"; Confianza = if($r.Confianza) { $r.Confianza.ToString('0.000') } else { "?" }; Proveedor = $r.Proveedor ?? "?"; Fallback = if($r.FallbackUsed) {"✓"} else {"-"}; Tiempo = "$($r.Elapsed.ToString('0.00'))s"; Estado = if($r.Tipologia -and $r.Tipologia -ne "Desconocido" -and $r.Tipologia -ne "RESTO") {"✅"} else {"⚠️"} }
    } else {
        $tabla += [PSCustomObject]@{ Flujo = $r.FlowName; Tipologia = "ERROR"; Confianza = "N/A"; Proveedor = "N/A"; Fallback = "-"; Tiempo = "N/A"; Estado = "❌" }
    }
}

$tabla | Format-Table -Property Estado, Flujo, Tipologia, Confianza, Proveedor, Fallback, Tiempo -AutoSize

Write-Host ""
$exitosos = @($resultados | Where-Object { $_.Success }).Count
$totalPruebas = $resultados.Count
Write-Host "✅ Pruebas exitosas: $exitosos/$totalPruebas" -ForegroundColor Green

Write-Host ""
Write-Host "📁 Resultados guardados en: $artifactDir" -ForegroundColor Gray
