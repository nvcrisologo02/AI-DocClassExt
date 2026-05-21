param(
    [string]$PdfPath = "C:\temp\Para_IA\test\ESCR-06 11901439_TITULO.PDF",
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

# Función para resolver URI de status (localhost -> localhost:7071)
function Resolve-StatusUri {
    param([string]$StatusUri)
    if ($StatusUri -match "http://localhost/") {
        return ($StatusUri -replace "http://localhost/", "http://localhost:7071/")
    }
    return $StatusUri
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
        Clasificador = $null
    }
    
    # Extraer de DetalleEjecucion.Clasificacion (string JSON)
    if ($result.output -and $result.output.DetalleEjecucion) {
        $detalle = $result.output.DetalleEjecucion
        
        # La clasificación está como string JSON en la propiedad Clasificacion
        $clasifString = $detalle.Clasificacion
        if ([string]::IsNullOrWhiteSpace($clasifString)) {
            return $data
        }
        
        # Parsear string JSON de la clasificación
        try {
            # Limpiar y parsear el string
            $clasifJson = $clasifString | ConvertFrom-Json
            $data.Tipologia = $clasifJson.TipologiaDetectada
            $data.Confianza = $clasifJson.Confianza
            $data.Proveedor = $clasifJson.ProveedorClasif
            $data.FallbackLLM = $clasifJson.FallbackLLM
            $data.FallbackRazon = $clasifJson.FallbackRazon
            $data.DetalleProveedores = $clasifJson.DetalleProveedores
            $data.Clasificador = $clasifJson.Clasificador
        }
        catch {
            # Si falla, intentar extraer campos individuales del string
            if ($clasifString -match 'TipologiaDetectada=([^;]+)') {
                $data.Tipologia = $matches[1]
            }
            if ($clasifString -match 'Confianza=([0-9.]+)') {
                $data.Confianza = [double]$matches[1]
            }
            if ($clasifString -match 'ProveedorClasif=([^;]+)') {
                $data.Proveedor = $matches[1]
            }
        }
    }
    
    return $data
}

# Función para hacer polling del resultado
function Wait-ForClassificationResult {
    param(
        [string]$StatusUri,
        [string]$FlowName
    )
    
    $resolvedUri = Resolve-StatusUri $StatusUri
    Write-Host "   ⏳ Esperando resultado ($MaxPollRetries intentos, cada ${PollDelaySeconds}s)..." -ForegroundColor Gray
    
    for ($i = 0; $i -lt $MaxPollRetries; $i++) {
        Start-Sleep -Seconds $PollDelaySeconds
        Write-Host "   ⏱️ Intento $($i+1)/$MaxPollRetries..." -ForegroundColor Gray
        
        try {
            $statusResponse = Invoke-WebRequest `
                -Uri $resolvedUri `
                -Method GET `
                -ErrorAction SilentlyContinue
            
            if ($statusResponse.StatusCode -eq 200) {
                $result = $statusResponse.Content | ConvertFrom-Json
                
                # Salvar respuesta
                $debugPath = "$artifactDir\result-$FlowName.json"
                $statusResponse.Content | Out-File -FilePath $debugPath -Encoding UTF8 -Force
                
                return $result
            }
        }
        catch {
            # Continuar con el polling si no está listo
        }
    }
    
    return $null
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
        
        Write-Host "   📤 Enviando solicitud..." -ForegroundColor Cyan
        $startTime = Get-Date
        $response = Invoke-WebRequest `
            -Uri $Endpoint `
            -Method POST `
            -ContentType "application/json" `
            -Body $requestBody `
            -ErrorAction Stop
        
        $initResponse = $response.Content | ConvertFrom-Json
        Write-Host "   ✓ Solicitud aceptada (instancia: $($initResponse.instanceId.Substring(0,8))...)" -ForegroundColor Cyan
        
        # Hacer polling del resultado
        $result = Wait-ForClassificationResult -StatusUri $initResponse.statusQueryUri -FlowName $FlowName
        $elapsed = $(Get-Date) - $startTime
        
        if (-not $result) {
            Write-Host "❌ Timeout esperando resultado" -ForegroundColor Red
            Write-Host ""
            return @{
                Success = $false
                FlowName = $FlowName
                Error = "Timeout: No se obtuvo resultado en $($MaxPollRetries * $PollDelaySeconds)s"
            }
        }
        
        # Extraer datos
        $classData = Extract-ClassificationData -result $result -flowName $FlowName
        
        $tipoColor = if ($classData.Tipologia -eq "Desconocido" -or $classData.Tipologia -eq "RESTO") { "Red" } elseif ($classData.Tipologia) { "Green" } else { "Gray" }
        $confColor = if ($classData.Confianza -ge $ClassificationUmbral) {"Green"} else {"Yellow"}
        
        Write-Host "✅ Resultado obtenido (${($elapsed.TotalSeconds).ToString('0.00')}s)" -ForegroundColor Green
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
Write-Host "  • DefaultFlow (auto - reglas→di→fallback): $($defaultFlow.Tipologia ?? 'ERROR') (conf: $(if($defaultFlow.Confianza) { $defaultFlow.Confianza.ToString('0.000') } else { '?' }), proveedor: $($defaultFlow.Proveedor ?? '?'))" -ForegroundColor Cyan
Write-Host "  • DI Only: $($diOnly.Tipologia ?? 'ERROR') (conf: $(if($diOnly.Confianza) { $diOnly.Confianza.ToString('0.000') } else { '?' }), proveedor: $($diOnly.Proveedor ?? '?'))" -ForegroundColor Cyan
Write-Host "  • GPT Only: $($gptOnly.Tipologia ?? 'ERROR') (conf: $(if($gptOnly.Confianza) { $gptOnly.Confianza.ToString('0.000') } else { '?' }), proveedor: $($gptOnly.Proveedor ?? '?'))" -ForegroundColor Cyan

Write-Host ""
Write-Host "🎯 CONCLUSIONES:" -ForegroundColor Yellow

# Comparativa de resultados
$defaultOK = $defaultFlow.Success -and $defaultFlow.Tipologia -and $defaultFlow.Tipologia -ne "Desconocido" -and $defaultFlow.Tipologia -ne "RESTO"
$diOK = $diOnly.Success -and $diOnly.Tipologia -and $diOnly.Tipologia -ne "Desconocido" -and $diOnly.Tipologia -ne "RESTO"
$gptOK = $gptOnly.Success -and $gptOnly.Tipologia -and $gptOnly.Tipologia -ne "Desconocido" -and $gptOnly.Tipologia -ne "RESTO"

if ($defaultOK) {
    Write-Host "  ✅ Pipeline de flujos funciona correctamente" -ForegroundColor Green
    Write-Host "     • Clasificación: $($defaultFlow.Tipologia) con confianza $($defaultFlow.Confianza.ToString('0.000'))" -ForegroundColor Green
    if ($defaultFlow.FallbackUsed) {
        Write-Host "     • Fallback global fue activado según lo esperado" -ForegroundColor Green
    } else {
        Write-Host "     • Clasificación satisfactoria sin necesidad de fallback" -ForegroundColor Blue
    }
} else {
    Write-Host "  ⚠️ Pipeline completo devolvió clasificación insatisfactoria" -ForegroundColor Yellow
    if (-not $defaultFlow.Success) {
        Write-Host "     Razón: $($defaultFlow.Error)" -ForegroundColor Yellow
    }
}

Write-Host ""
if ($diOK -and $gptOK -and $defaultOK) {
    Write-Host "  ✅ Todos los flujos funcionan correctamente (reglas, di, gpt)" -ForegroundColor Green
} elseif ($diOK -or $gptOK) {
    Write-Host "  ℹ️ Algunos flujos funcionan (posible fallback necesario)" -ForegroundColor Blue
}

Write-Host ""
Write-Host "📁 Resultados guardados en: $artifactDir" -ForegroundColor Gray
Write-Host "   • result-DefaultFlow.json" -ForegroundColor Gray
Write-Host "   • result-DI Only.json" -ForegroundColor Gray
Write-Host "   • result-GPT Only.json" -ForegroundColor Gray
Write-Host ""
