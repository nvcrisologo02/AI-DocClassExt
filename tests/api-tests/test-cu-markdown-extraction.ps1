# Script para verificar que Azure Content Understanding extrae markdown
#
# USO: 
#   .\test-cu-markdown-extraction.ps1 -DocumentPath "C:\ruta\a\documento.pdf"
#

param(
    [Parameter(Mandatory = $true)]
    [string]$DocumentPath
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$endpoint = "http://localhost:7071/api/IngestDocument"

# Validar que existe el documento
if (-not [System.IO.Path]::IsPathRooted($DocumentPath)) {
    throw "El parametro -DocumentPath debe ser una ruta absoluta."
}

try {
    $resolvedPath = (Resolve-Path -Path $DocumentPath -ErrorAction Stop).Path
} catch {
    throw "No se encontro el fichero: '$DocumentPath'"
}

$documentBytes = [System.IO.File]::ReadAllBytes($resolvedPath)
$documentBase64 = [System.Convert]::ToBase64String($documentBytes)
$documentName = [System.IO.Path]::GetFileName($resolvedPath)

Write-Host ""
Write-Host "================================================"
Write-Host "  Verificando Extraccion de Markdown con CU"
Write-Host "================================================"
Write-Host ""
Write-Host "Documento    : $resolvedPath"
Write-Host "Nombre       : $documentName"
Write-Host "Tamanio      : $([math]::Round($documentBytes.Length / 1KB, 2)) KB"
Write-Host "Endpoint     : $endpoint"
Write-Host ""

# Crear request
$body = @{
    instrucciones = @{
        expectedType = "nota-simple@1.4"
        skipDuplicateCheck = $true
        forceReprocess = $true
        SkipGDCUpload = $true
        classification = @{
            model = "auto"
            umbral = 0.85
        }
        extraction = @{
            model = "auto"
            umbral = 0.80
        }
    }
    documento = @{
        name = $documentName
        content = @{
            base64 = $documentBase64
        }
    }
    trazabilidad = @{
        correlationId = "CU-MARKDOWN-CHECK-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        submittedBy = "verificacion@sareb.es"
        idGDC = $null
        idActivo = "TEST-001"
    }
} | ConvertTo-Json -Depth 10

Write-Host "Enviando documento a orchestrator..."
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $body -ContentType "application/json"
    
    Write-Host "[OK] Request aceptada"
    Write-Host "Instance ID : $($response.instanceId)"
    Write-Host ""
    
    # Esperar y consultar estado en loop
    $instanceId = $response.instanceId
    $statusUri = $response.statusQueryUri
    
    if ($statusUri -match "^http://localhost/" -and $statusUri -notmatch ":7071") {
        $statusUri = $statusUri -replace "^http://localhost/", "http://localhost:7071/"
    }
    
    Write-Host "Esperando completitud (max 120 segundos)..."
    Write-Host ""
    
    $maxWait = 120
    $waited = 0
    $completed = $false
    
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 2
        $waited += 2
        
        try {
            $status = Invoke-RestMethod -Uri $statusUri -Method Get
            
            if ($status.runtimeStatus -eq "Completed") {
                $completed = $true
                break
            } elseif ($status.runtimeStatus -eq "Failed" -or $status.runtimeStatus -eq "Terminated") {
                Write-Host "[ERROR] Orchestration falló: $($status.runtimeStatus)"
                Write-Host "Output: $($status.output | ConvertTo-Json)"
                exit 1
            }
            
            Write-Host "  [$waited/30s] Estado: $($status.runtimeStatus)..."
        } catch {
            Write-Host "  [$waited/30s] Esperando..."
        }
    }
    
    if (-not $completed) {
        Write-Host ""
        Write-Host "[TIMEOUT] La orquestación tardó más de 30 segundos"
        exit 1
    }
    
    # Obtener resultado final
    $finalStatus = Invoke-RestMethod -Uri $statusUri -Method Get
    $result = $finalStatus.output
    
    Write-Host ""
    Write-Host "================================================"
    Write-Host "  RESULTADOS DE EXTRACCION"
    Write-Host "================================================"
    Write-Host ""
    
    # Información general
    Write-Host "--- Estado General ---"
    Write-Host "Estado           : $($result.resultado.estado)"
    Write-Host "Tipología        : $($result.identificacion.tipologia)"
    Write-Host "Páginas          : $($result.identificacion.paginas)"
    Write-Host ""
    
    # Información de Extracción
    Write-Host "--- Extracción (Proveedor) ---"
    Write-Host "Proveedor        : $($result.detalleEjecucion.extraccion.modelo)"
    Write-Host "Fallback Usado   : $($result.detalleEjecucion.extraccion.fallbackUsado)"
    if ($result.detalleEjecucion.extraccion.fallbackUsado) {
        Write-Host "Fallback Razón   : $($result.detalleEjecucion.extraccion.fallbackRazon)"
    }
    Write-Host ""
    
    # ===== BUSCANDO MARKDOWN =====
    Write-Host "--- Markdown Extraído ---"
    
    if (-not $result.detalleEjecucion.extraccion) {
        Write-Host "[WARN] No hay información de extracción en respuesta"
    } else {
        # Buscar markdown en diferentes lugares
        $markdownEncontrado = $false
        
        # Lugar 1: En DatosExtraidos
        if ($result.datosExtraidos -and $result.datosExtraidos.Markdown) {
            Write-Host "[ENCONTRADO] DatosExtraidos['Markdown']:"
            Write-Host ""
            $markdown = $result.datosExtraidos.Markdown
            if ($markdown.Length -gt 500) {
                Write-Host $markdown.Substring(0, 500)
                Write-Host ""
                Write-Host "... (truncado, total: $($markdown.Length) caracteres)"
            } else {
                Write-Host $markdown
            }
            Write-Host ""
            $markdownEncontrado = $true
        } else {
            Write-Host "[NO ENCONTRADO] DatosExtraidos['Markdown']"
        }
        
        # Información adicional
        Write-Host ""
        Write-Host "--- Campos Extraídos (DatosExtraidos) ---"
        if ($result.datosExtraidos) {
            Write-Host "Total de campos: $($result.datosExtraidos.Count)"
            Write-Host "Primeros 10 campos:"
            $result.datosExtraidos.GetEnumerator() | Select-Object -First 10 | ForEach-Object {
                $valor = $_.Value
                if ($null -eq $valor) {
                    $valor = "[null]"
                } elseif ($valor -is [string] -and $valor.Length -gt 100) {
                    $valor = "$($valor.Substring(0, 97))..."
                }
                Write-Host "  - $($_.Key): $valor"
            }
        } else {
            Write-Host "[VACIO] No hay datos extraídos"
        }
    }
    
    Write-Host ""
    Write-Host "================================================"
    
    if ($markdownEncontrado) {
        Write-Host "[OK] MARKDOWN EXTRAIDO CORRECTAMENTE"
    } else {
        Write-Host "[FAIL] MARKDOWN NO ENCONTRADO - Revisa los logs de la funcion"
    }
    
    Write-Host "================================================"
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "[ERROR] Excepción durante procesamiento:"
    Write-Host $_.Exception.Message
    Write-Host ""
    Write-Host "Stack: $($_.Exception.StackTrace)"
    exit 1
}
