param(
    [string]$PdfPath = "C:\temp\Para_IA\test\ESCR-06 11901439_TITULO.PDF",
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",
    [string]$SubmittedBy = "usuario.prueba@sareb.es",
    [double]$ClassificationUmbral = 0.50
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  TEST: VALIDACIÓN DE FLUJOS DE CLASIFICACIÓN                              ║" -ForegroundColor Cyan
Write-Host "║  Archivo: $(Split-Path $PdfPath -Leaf)  ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Verificar que el archivo existe
if (-not (Test-Path $PdfPath)) {
    Write-Host "❌ PDF no encontrado: $PdfPath" -ForegroundColor Red
    exit 1
}

Write-Host "📄 PDF encontrado: $(Get-Item $PdfPath | Select-Object -ExpandProperty FullName)" -ForegroundColor Green
Write-Host "🔌 Endpoint: $Endpoint" -ForegroundColor Green
Write-Host ""

# Crear directorio de artefactos si no existe
$artifactDir = "C:\temp\MVP\documento-ia-clasificacion-mvp\tests\api-tests\artifacts"
if (-not (Test-Path $artifactDir)) {
    New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
}

# Función para codificar PDF en base64
function Encode-PdfToBase64 {
    param([string]$PdfPath)
    $bytes = [System.IO.File]::ReadAllBytes($PdfPath)
    return [Convert]::ToBase64String($bytes)
}

# Función para extraer clasificación de la respuesta
function Extract-ClassificationData {
    param($result, $flowName)
    
    $data = @{
        Tipologia = $null
        Confianza = $null
        Proveedor = $null
        FallbackLLM = $null
        FallbackRazon = $null
        DetalleProveedores = $null
    }
    
    # Intentar extraer de diferentes ubicaciones
    if ($result.extraccionResultado -and $result.extraccionResultado.Clasificacion) {
        $c = $result.extraccionResultado.Clasificacion
        $data.Tipologia = $c.TipologiaDetectada ?? $c.tipologiaDetectada
        $data.Confianza = $c.Confianza ?? $c.confianza
        $data.Proveedor = $c.ProveedorClasif ?? $c.proveedorClasif
        $data.FallbackLLM = $c.FallbackLLM ?? $c.fallbackLLM
        $data.FallbackRazon = $c.FallbackRazon ?? $c.fallbackRazon
        $data.DetalleProveedores = $c.DetalleProveedores ?? $c.detalleProveedores
    }
    elseif ($result.Clasificacion) {
        $c = $result.Clasificacion
        $data.Tipologia = $c.TipologiaDetectada
        $data.Confianza = $c.Confianza
        $data.Proveedor = $c.ProveedorClasif
        $data.FallbackLLM = $c.FallbackLLM
        $data.FallbackRazon = $c.FallbackRazon
        $data.DetalleProveedores = $c.DetalleProveedores
    }
    
    return $data
}

# Función para ejecutar prueba con flujo específico
function Test-ClassificationFlow {
    param(
        [string]$FlowName,
        [string]$ProviderKey,
        [string]$Description
    )
    
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
        $response = Invoke-WebRequest `
            -Uri $Endpoint `
            -Method POST `
            -ContentType "application/json" `
            -Body $requestBody `
            -ErrorAction Stop
        
        $elapsed = $(Get-Date) - $startTime
        $result = $response.Content | ConvertFrom-Json
        
        # Debug: guardar respuesta
        $debugPath = "$artifactDir\debug-response-$FlowName.json"
        $response.Content | Out-File -FilePath $debugPath -Encoding UTF8 -Force
        
        # Extraer datos
        $classData = Extract-ClassificationData -result $result -flowName $FlowName
        
        $tipoColor = if ($classData.Tipologia -eq "Desconocido" -or $classData.Tipologia -eq "RESTO") { "Red" } else { "Green" }
        $confColor = if ($classData.Confianza -ge $ClassificationUmbral) {"Green"} else {"Yellow"}
        
        Write-Host "✅ Respuesta exitosa (${($elapsed.TotalSeconds).ToString('0.00')}s)" -ForegroundColor Green
        Write-Host ""
        Write-Host "   📊 Resultado:" -ForegroundColor Cyan
        Write-Host "   ├─ Tipología:        $(if($classData.Tipologia) { $classData.Tipologia } else { '❓ N/A' })" -ForegroundColor $tipoColor
        
        if ($classData.Confianza) {
            Write-Host "   ├─ Confianza:        $($classData.Confianza.ToString('0.000'))" -ForegroundColor $confColor
        } else {
            Write-Host "   ├─ Confianza:        ❓ N/A" -ForegroundColor Gray
        }
        
        Write-Host "   ├─ Proveedor:        $(if($classData.Proveedor) { $classData.Proveedor } else { 'N/A' })" -ForegroundColor Cyan
        Write-Host "   ├─ Fallback LLM:     $(if($classData.FallbackLLM) {'✓ Sí'} else {'✗ No'})" -ForegroundColor $(if($classData.FallbackLLM) {"Yellow"} else {"Gray"})
        
        if ($classData.FallbackRazon) {
            Write-Host "   └─ Razón Fallback:   $($classData.FallbackRazon)" -ForegroundColor Yellow
        }
        
        # Detalles de evaluación de proveedores
        if ($classData.DetalleProveedores -and $classData.DetalleProveedores.Count -gt 0) {
            Write-Host ""
            Write-Host "   📋 Evaluación de proveedores:" -ForegroundColor Cyan
            foreach ($det in $classData.DetalleProveedores) {
                $tipDetalle = $det.Tipologia ?? $det.tipologia ?? "N/A"
                $confDetalle = if ($det.Confianza) { $det.Confianza.ToString('0.000') } else { "N/A" }
                $motivo = $det.MotivoDescarte ?? $det.motivoDescarte ?? "Satisfactorio"
                $proveedor = $det.Proveedor ?? $det.proveedor ?? "?"
                
                Write-Host "      • $proveedor`: $tipDetalle (conf: $confDetalle) - $motivo" -ForegroundColor Gray
            }
        }
        
        Write-Host ""
        return @{
            Success = $true
            FlowName = $FlowName
            Tipologia = $classData.Tipologia
            Confianza = $classData.Confianza
            Proveedor = $classData.Proveedor
            FallbackUsed = $classData.FallbackLLM
            Elapsed = $elapsed.TotalSeconds
            RawResponse = $result
        }
    }
    catch {
        Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        return @{
            Success = $false
            FlowName = $FlowName
            Error = $_.Exception.Message
        }
    }
}

# Array para almacenar resultados
$resultados = @()

# Test 1: DefaultFlow (auto = rules → di → fallback gpt)
$resultados += Test-ClassificationFlow `
    -FlowName "DefaultFlow" `
    -ProviderKey "auto" `
    -Description "Flujo por defecto: rules → di → fallback gpt"

# Test 2: Solo DI (di only)
$resultados += Test-ClassificationFlow `
    -FlowName "DI Only" `
    -ProviderKey "di" `
    -Description "Solo Azure Document Intelligence"

# Test 3: Solo GPT (gpt)
$resultados += Test-ClassificationFlow `
    -FlowName "GPT Only" `
    -ProviderKey "gpt" `
    -Description "Solo GPT-4o-mini"

# Resumen final
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  📊 RESUMEN DE PRUEBAS                                                   ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$tabla = @()
foreach ($r in $resultados) {
    if ($r.Success) {
        $tabla += [PSCustomObject]@{
            Flujo = $r.FlowName
            Tipologia = $r.Tipologia ?? "?"
            Confianza = if($r.Confianza) { $r.Confianza.ToString('0.000') } else { "?" }
            Proveedor = $r.Proveedor ?? "?"
            Fallback = if($r.FallbackUsed) {"✓"} else {"-"}
            Tiempo = "$($r.Elapsed.ToString('0.00'))s"
            Estado = if($r.Tipologia -and $r.Tipologia -ne "Desconocido" -and $r.Tipologia -ne "RESTO") {"✅"} else {"⚠️"}
        }
    } else {
        $tabla += [PSCustomObject]@{
            Flujo = $r.FlowName
            Tipologia = "ERROR"
            Confianza = "N/A"
            Proveedor = "N/A"
            Fallback = "-"
            Tiempo = "N/A"
            Estado = "❌"
        }
    }
}

$tabla | Format-Table -Property Estado, Flujo, Tipologia, Confianza, Proveedor, Fallback, Tiempo -AutoSize

Write-Host ""
$exitosos = @($resultados | Where-Object { $_.Success }).Count
$totalPruebas = $resultados.Count
Write-Host "✅ Pruebas exitosas: $exitosos/$totalPruebas" -ForegroundColor Green

# Análisis
$defaultFlow = $resultados[0]
$diOnly = $resultados[1]
$gptOnly = $resultados[2]

Write-Host ""
Write-Host "📈 ANÁLISIS:" -ForegroundColor Yellow
Write-Host "  • DefaultFlow (rules→di→fallback) clasificó como: $($defaultFlow.Tipologia ?? 'ERROR') (confianza: $(if($defaultFlow.Confianza) { $defaultFlow.Confianza.ToString('0.000') } else { '?' }))" -ForegroundColor Cyan
Write-Host "  • DI solo clasificó como: $($diOnly.Tipologia ?? 'ERROR') (confianza: $(if($diOnly.Confianza) { $diOnly.Confianza.ToString('0.000') } else { '?' }))" -ForegroundColor Cyan
Write-Host "  • GPT solo clasificó como: $($gptOnly.Tipologia ?? 'ERROR') (confianza: $(if($gptOnly.Confianza) { $gptOnly.Confianza.ToString('0.000') } else { '?' }))" -ForegroundColor Cyan

Write-Host ""
Write-Host "🎯 CONCLUSIONES:" -ForegroundColor Yellow

# Verificar si el DefaultFlow fue más efectivo
if ($defaultFlow.Success -and $defaultFlow.Tipologia -and $defaultFlow.Tipologia -ne "Desconocido" -and $defaultFlow.Tipologia -ne "RESTO") {
    Write-Host "  ✅ Pipeline de flujos funciona correctamente" -ForegroundColor Green
    if ($defaultFlow.FallbackUsed) {
        Write-Host "  ✅ Fallback global fue activado según la lógica esperada" -ForegroundColor Green
    } else {
        Write-Host "  ℹ️ Clasificación satisfactoria sin necesidad de fallback global" -ForegroundColor Blue
    }
} else {
    Write-Host "  ⚠️ Pipeline completo devolvió clasificación insatisfactoria" -ForegroundColor Yellow
    if (-not $defaultFlow.Success) {
        Write-Host "     Razón: $($defaultFlow.Error)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "📁 Respuestas JSON guardadas en: $artifactDir" -ForegroundColor Gray
Write-Host ""
