param(
    [Parameter(Mandatory = $true)]
    [string]$ObjectIdsFile,

    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",

    [ValidateSet("TDN1", "TDN1_TDN2")]
    [string]$NivelClasificacion = "TDN1_TDN2",

    [ValidateSet("auto", "rules", "gpt", "di", "hybrid-tdn", "hybrid-rules-gpt-di", "hybrid-rules-di-gpt")]
    [string]$Provider = "rules",

    [int]$MaxRetries = 60,

    [int]$DelaySeconds = 3,

    [switch]$SkipFinalResultWait
)

<#
.SYNOPSIS
    Script para ejecutar clasificación (classificationOnly) de documentos usando ObjectIds de GDC.

.DESCRIPTION
    Lee un archivo de texto con ObjectIds de GDC (uno por línea) e invoca el proceso de clasificación
    contra http://localhost:7071 usando el proveedor y nivel de clasificación especificados.

    Por defecto, ejecuta clasificación basada en reglas (rules) primero. Si no es satisfactoria,
    automáticamente usa GPT como fallback (según configuración GlobalFallback en appsettings.json).

    Genera el resumen automáticamente si está habilitado en la configuración.

.PARAMETER ObjectIdsFile
    Ruta al archivo txt con ObjectIds de GDC, uno por línea.

.PARAMETER Endpoint
    URL del endpoint IngestDocument. Por defecto: http://localhost:7071/api/IngestDocument

.PARAMETER NivelClasificacion
    (NO USADO - mantenido solo para referencia) El backend usa su configuración por defecto (TDN1_TDN2)

.PARAMETER Provider
    Proveedor o flujo de clasificación a usar. Opciones:
    - auto: Usa el flujo por defecto configurado (hybrid-rules-gpt-di)
    - rules: Solo clasificación basada en reglas + fallback GPT si no satisfactorio (por defecto)
    - gpt: Solo clasificación con GPT
    - di: Solo Azure Document Intelligence
    - hybrid-tdn: Clasificador híbrido TDN
    - hybrid-rules-gpt-di: Flujo rules → gpt → di
    - hybrid-rules-di-gpt: Flujo rules → di → gpt

.PARAMETER MaxRetries
    Número máximo de reintentos para polling del estado de la orquestación. Por defecto: 60

.PARAMETER DelaySeconds
    Segundos de espera entre cada reintento de polling. Por defecto: 3

.PARAMETER SkipFinalResultWait
    Si se especifica, no espera al resultado final de cada procesamiento (solo envía y continúa).

.EXAMPLE
    .\test-classification-only-from-gdc-objectids.ps1 -ObjectIdsFile "objectids.txt"

    Ejecuta clasificación con rules + fallback GPT (comportamiento por defecto)

.EXAMPLE
    .\test-classification-only-from-gdc-objectids.ps1 -ObjectIdsFile "objectids.txt" -Provider "gpt"

    Ejecuta clasificación solo con GPT (sin reglas primero)

.EXAMPLE
    .\test-classification-only-from-gdc-objectids.ps1 -ObjectIdsFile "objectids.txt" -NivelClasificacion "TDN1" -SkipFinalResultWait
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# ============================================================================
# FUNCIONES AUXILIARES
# ============================================================================

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Error2 {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Resolve-StatusUri {
    param([string]$StatusUri)

    if ($StatusUri -match "http://localhost/") {
        return ($StatusUri -replace "http://localhost/", "http://localhost:7071/")
    }
    if ($StatusUri -match "https://localhost/") {
        return ($StatusUri -replace "https://localhost/", "https://localhost:7071/")
    }

    return $StatusUri
}

function Invoke-DocumentClassification {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ObjectIdGDC,

        [Parameter(Mandatory = $true)]
        [string]$Endpoint,

        [Parameter(Mandatory = $true)]
        [string]$NivelClasificacion,

        [Parameter(Mandatory = $true)]
        [string]$Provider
    )

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $correlationId = "CLASSIFY-GDC-$ObjectIdGDC-$timestamp"
    # Provider=rules activa RuleBasedTdnClassifier; el Model debe quedarse en auto para que el fallback GPT
    # resuelva su modelo por defecto configurado en backend.
    $classificationModel = "auto"

    $body = @{
        instrucciones = @{
            skipDuplicateCheck                    = $true
            forceReprocess                        = $true
            skipGDCUpload                         = $true
            classificationOnly                    = $true
            maxPagesForClassificationOnly         = 10
            executeIntegrarWhenClassificationOnly = $false
            forzarResumenPorDefecto               = $true
            classification                        = @{
                Provider = $Provider
                Model    = $classificationModel
                Umbral   = 0.50
            }
            extraction                            = @{
                Model  = "auto"
                Umbral = 0.80
            }
        }
        documento     = @{
            name        = "GDC-$ObjectIdGDC.pdf"
            ObjectIdGDC = $ObjectIdGDC
            content     = @{
                base64 = ""
            }
        }
        trazabilidad  = @{
            correlationId = $correlationId
            submittedBy   = "classification-batch@sareb.es"
            idGDC         = $ObjectIdGDC
            idActivo      = $null
        }
    } | ConvertTo-Json -Depth 10

    # DEBUG: Mostrar el payload
    Write-Host "DEBUG - Payload enviado:" -ForegroundColor Magenta
    Write-Host $body -ForegroundColor Gray

    try {
        $response = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $body -ContentType "application/json"
        $statusUri = Resolve-StatusUri -StatusUri $response.statusQueryUri

        return [pscustomobject]@{
            Success       = $true
            InstanceId    = $response.instanceId
            StatusUri     = $statusUri
            CorrelationId = $correlationId
            ObjectIdGDC   = $ObjectIdGDC
            Error         = $null
        }
    }
    catch {
        return [pscustomobject]@{
            Success       = $false
            InstanceId    = $null
            StatusUri     = $null
            CorrelationId = $correlationId
            ObjectIdGDC   = $ObjectIdGDC
            Error         = $_.Exception.Message
        }
    }
}

function Wait-OrchestrationCompletion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatusUri,

        [Parameter(Mandatory = $true)]
        [string]$InstanceId,

        [Parameter(Mandatory = $true)]
        [int]$MaxRetries,

        [Parameter(Mandatory = $true)]
        [int]$DelaySeconds
    )

    $attempt = 0
    $completed = $false
    $finalStatus = $null

    while ($attempt -lt $MaxRetries -and -not $completed) {
        $attempt++

        try {
            $status = Invoke-RestMethod -Uri $StatusUri -Method Get

            switch ($status.runtimeStatus) {
                "Completed" {
                    $completed = $true
                    $finalStatus = $status
                    break
                }
                "Failed" {
                    $completed = $true
                    $finalStatus = $status
                    break
                }
                "Terminated" {
                    $completed = $true
                    $finalStatus = $status
                    break
                }
                default {
                    # Running, Pending, etc.
                    if ($attempt % 5 -eq 0) {
                        Write-Info "  [$InstanceId] Estado: $($status.runtimeStatus) (intento $attempt/$MaxRetries)"
                    }
                    Start-Sleep -Seconds $DelaySeconds
                }
            }
        }
        catch {
            Write-Warning "  Error al consultar estado (intento $attempt/$MaxRetries): $($_.Exception.Message)"
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    if (-not $completed) {
        return [pscustomobject]@{
            Success       = $false
            RuntimeStatus = "Timeout"
            Output        = $null
            Error         = "Timeout después de $MaxRetries intentos"
        }
    }

    return [pscustomobject]@{
        Success       = ($finalStatus.runtimeStatus -eq "Completed")
        RuntimeStatus = $finalStatus.runtimeStatus
        Output        = $finalStatus.output
        Error         = if ($finalStatus.runtimeStatus -ne "Completed") { "Estado final: $($finalStatus.runtimeStatus)" } else { $null }
    }
}

function Get-ClassificationResult {
    param([object]$Output)

    if ($null -eq $Output) {
        return [pscustomobject]@{
            Estado          = "Desconocido"
            Tdn1            = $null
            FamiliaId       = $null
            FamiliaNombre   = $null
            TipologiaId     = $null
            TipologiaNombre = $null
            Modelo          = $null
            Confidence      = $null
            Resumen         = $null
            TipologiaVirtual = $false
            Virtual         = $false
        }
    }

    # Acceso directo a propiedades con manejo de nulls
    $resultado = if ($Output.Resultado) { $Output.Resultado } elseif ($Output.resultado) { $Output.resultado } else { $null }
    $identificacion = if ($Output.Identificacion) { $Output.Identificacion } elseif ($Output.identificacion) { $Output.identificacion } else { $null }
    $detalleEjecucion = if ($Output.DetalleEjecucion) { $Output.DetalleEjecucion } elseif ($Output.detalleEjecucion) { $Output.detalleEjecucion } else { $null }
    $clasificacion = if ($detalleEjecucion -and $detalleEjecucion.Clasificacion) { $detalleEjecucion.Clasificacion } elseif ($detalleEjecucion -and $detalleEjecucion.clasificacion) { $detalleEjecucion.clasificacion } else { $null }
    $datosExtraidos = if ($Output.DatosExtraidos) { $Output.DatosExtraidos } elseif ($Output.datosExtraidos) { $Output.datosExtraidos } else { $null }

    $estado = if ($resultado -and $resultado.Estado) { $resultado.Estado } elseif ($resultado -and $resultado.estado) { $resultado.estado } else { "Desconocido" }
    $familiaId = if ($identificacion -and $identificacion.TipologiaFamilia) { $identificacion.TipologiaFamilia } elseif ($identificacion -and $identificacion.tipologiaFamilia) { $identificacion.tipologiaFamilia } else { $null }
    $tipologiaId = if ($identificacion -and $identificacion.Tipologia) { $identificacion.Tipologia } elseif ($identificacion -and $identificacion.tipologia) { $identificacion.tipologia } else { $null }
    $tipologiaNombre = if ($identificacion -and $identificacion.TipologiaNombre) { $identificacion.TipologiaNombre } elseif ($identificacion -and $identificacion.tipologiaNombre) { $identificacion.tipologiaNombre } else { $null }
    $confidence = if ($clasificacion -and $clasificacion.Confianza) { $clasificacion.Confianza } elseif ($clasificacion -and $clasificacion.confianza) { $clasificacion.confianza } else { $null }
    $tdn2 = if ($clasificacion -and $clasificacion.Tdn2) { $clasificacion.Tdn2 } elseif ($clasificacion -and $clasificacion.tdn2) { $clasificacion.tdn2 } else { $null }
    $modelo = if ($detalleEjecucion -and $detalleEjecucion.ModeloLLMUsado) { $detalleEjecucion.ModeloLLMUsado } elseif ($clasificacion -and $clasificacion.Modelo) { $clasificacion.Modelo } else { $null }

    # Extraer resumen de múltiples ubicaciones posibles
    $resumen = $null
    # 1. Intentar desde DatosExtraidos.Resumen
    if ($datosExtraidos) {
        $resumen = if ($datosExtraidos.Resumen) { $datosExtraidos.Resumen } elseif ($datosExtraidos.resumen) { $datosExtraidos.resumen } else { $null }
    }
    # 2. Si no existe, intentar desde DetalleEjecucion.Clasificacion.ResumenCombinado
    if (-not $resumen -and $clasificacion) {
        $resumen = if ($clasificacion.ResumenCombinado) { $clasificacion.ResumenCombinado } elseif ($clasificacion.resumenCombinado) { $clasificacion.resumenCombinado } else { $null }
        if (-not $resumen) {
            $resumen = if ($clasificacion.ResultadoPromptCombinado) { $clasificacion.ResultadoPromptCombinado } elseif ($clasificacion.resultadoPromptCombinado) { $clasificacion.resultadoPromptCombinado } else { $null }
        }
    }
    # 3. Si aún no existe, intentar desde DetalleEjecucion.Extraccion (si existe)
    if (-not $resumen -and $detalleEjecucion) {
        $extraccion = if ($detalleEjecucion.Extraccion) { $detalleEjecucion.Extraccion } elseif ($detalleEjecucion.extraccion) { $detalleEjecucion.extraccion } else { $null }
        if ($extraccion) {
            $resumen = if ($extraccion.ResumenCombinado) { $extraccion.ResumenCombinado } elseif ($extraccion.resumenCombinado) { $extraccion.resumenCombinado } else { $null }
        }
    }

    # Determinar si es tipología virtual
    $esVirtual = $false
    if ($clasificacion) {
        $clasificacionParcial = if ($clasificacion.ClasificacionParcial) { $clasificacion.ClasificacionParcial } elseif ($clasificacion.clasificacionParcial) { $clasificacion.clasificacionParcial } else { $false }
        $fallbackRazon = if ($clasificacion.FallbackRazon) { $clasificacion.FallbackRazon } elseif ($clasificacion.fallbackRazon) { $clasificacion.fallbackRazon } else { "" }
        $propuestaTipologia = if ($identificacion -and $identificacion.PropuestaTipologia) { $identificacion.PropuestaTipologia } elseif ($identificacion -and $identificacion.propuestaTipologia) { $identificacion.propuestaTipologia } else { $null }

        if ($clasificacionParcial -and ($fallbackRazon -match "virtual|propuesta|extraido" -or $propuestaTipologia)) {
            $esVirtual = $true
        }
    }

    return [pscustomobject]@{
        Estado          = $estado
        Tdn1            = $familiaId
        FamiliaId       = $familiaId
        FamiliaNombre   = $familiaId
        Tdn2            = $tdn2
        TipologiaId     = $tipologiaId
        TipologiaNombre = $tipologiaNombre
        Modelo          = $modelo
        Confidence      = $confidence
        Resumen         = $resumen
        TipologiaVirtual = $esVirtual
        Virtual         = $esVirtual
    }
}

# ============================================================================
# MAIN
# ============================================================================

Write-Info "==================================================================="
Write-Info " Script de Clasificación (ClassificationOnly) desde ObjectIds GDC"
Write-Info "==================================================================="
Write-Info "Archivo de ObjectIds: $ObjectIdsFile"
Write-Info "Endpoint: $Endpoint"
Write-Info "Provider: $Provider"
Write-Info "Nivel de Clasificación: (default del backend - típicamente TDN1_TDN2)"
Write-Info "-------------------------------------------------------------------"
Write-Host ""

# Validar que el archivo existe
if (-not (Test-Path -Path $ObjectIdsFile)) {
    Write-Error2 "El archivo de ObjectIds no existe: $ObjectIdsFile"
    exit 1
}

# Leer los ObjectIds del archivo
$objectIds = Get-Content -Path $ObjectIdsFile |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { $_.Trim() }

if ($objectIds.Count -eq 0) {
    Write-Error2 "El archivo no contiene ObjectIds válidos."
    exit 1
}

Write-Info "Total de ObjectIds a procesar: $($objectIds.Count)"
Write-Host ""

# Procesar cada ObjectId
$results = @()
$successCount = 0
$failCount = 0

foreach ($objectId in $objectIds) {
    Write-Info "-------------------------------------------------------------------"
    Write-Info "Procesando ObjectId: $objectId"

    # Invocar clasificación
    $invokeResult = Invoke-DocumentClassification `
        -ObjectIdGDC $objectId `
        -Endpoint $Endpoint `
        -NivelClasificacion $NivelClasificacion `
        -Provider $Provider

    if (-not $invokeResult.Success) {
        Write-Error2 "  Falló la invocación: $($invokeResult.Error)"
        $failCount++

        $results += [pscustomobject]@{
            ObjectIdGDC     = $objectId
            InstanceId      = $null
            StatusUri       = $null
            RuntimeStatus   = "InvokeFailed"
            Estado          = "Error"
            Tdn1            = $null
            FamiliaId       = $null
            FamiliaNombre   = $null
            Tdn2            = $null
            TipologiaId     = $null
            TipologiaNombre = $null
            Modelo          = $null
            Confidence      = $null
            Error           = $invokeResult.Error
            Resumen         = $null
            TipologiaVirtual = $false
            Virtual         = $false
        }

        continue
    }

    Write-Success "  Invocado correctamente"
    Write-Info "  InstanceId: $($invokeResult.InstanceId)"
    Write-Info "  StatusUri: $($invokeResult.StatusUri)"

    if ($SkipFinalResultWait) {
        Write-Info "  (Saltando espera de resultado final)"

        $results += [pscustomobject]@{
            ObjectIdGDC     = $objectId
            InstanceId      = $invokeResult.InstanceId
            StatusUri       = $invokeResult.StatusUri
            RuntimeStatus   = "Submitted"
            Estado          = "Pendiente"
            Tdn1            = $null
            FamiliaId       = $null
            FamiliaNombre   = $null
            Tdn2            = $null
            TipologiaId     = $null
            TipologiaNombre = $null
            Modelo          = $null
            Confidence      = $null
            Error           = $null
            Resumen         = $null
            TipologiaVirtual = $false
            Virtual         = $false
        }

        $successCount++
        continue
    }

    # Esperar resultado final
    Write-Info "  Esperando resultado final..."
    $waitResult = Wait-OrchestrationCompletion `
        -StatusUri $invokeResult.StatusUri `
        -InstanceId $invokeResult.InstanceId `
        -MaxRetries $MaxRetries `
        -DelaySeconds $DelaySeconds

    if (-not $waitResult.Success) {
        Write-Error2 "  Falló el procesamiento: $($waitResult.Error)"
        $failCount++

        $results += [pscustomobject]@{
            ObjectIdGDC     = $objectId
            InstanceId      = $invokeResult.InstanceId
            StatusUri       = $invokeResult.StatusUri
            RuntimeStatus   = $waitResult.RuntimeStatus
            Estado          = "Error"
            Tdn1            = $null
            FamiliaId       = $null
            FamiliaNombre   = $null
            Tdn2            = $null
            TipologiaId     = $null
            TipologiaNombre = $null
            Modelo          = $null
            Confidence      = $null
            Error           = $waitResult.Error
            Resumen         = $null
            TipologiaVirtual = $false
            Virtual         = $false
        }

        continue
    }

    # Extraer resultado de clasificación
    $classification = Get-ClassificationResult -Output $waitResult.Output

    Write-Success "  Completado: $($classification.Estado)"
    Write-Info "  Modelo: $($classification.Modelo)"
    Write-Info "  TDN1: $($classification.Tdn1)"
    Write-Info "  Familia: [$($classification.FamiliaId)] $($classification.FamiliaNombre)"
    if ($classification.Tdn2) {
        Write-Info "  TDN2: $($classification.Tdn2)"
    }
    Write-Info "  Tipología: [$($classification.TipologiaId)] $($classification.TipologiaNombre)"
    Write-Info "  Confidence: $($classification.Confidence)"
    if ($classification.Resumen) {
        Write-Info "  Resumen: $($classification.Resumen)"
    }
    if ($classification.Virtual) {
        Write-Warning "  Virtual: Sí (Tipología parcial o propuesta)"
    }

    $results += [pscustomobject]@{
        ObjectIdGDC     = $objectId
        InstanceId      = $invokeResult.InstanceId
        StatusUri       = $invokeResult.StatusUri
        RuntimeStatus   = $waitResult.RuntimeStatus
        Estado          = $classification.Estado
        Tdn1            = $classification.Tdn1
        FamiliaId       = $classification.FamiliaId
        FamiliaNombre   = $classification.FamiliaNombre
        Tdn2            = $classification.Tdn2
        TipologiaId     = $classification.TipologiaId
        TipologiaNombre = $classification.TipologiaNombre
        Modelo          = $classification.Modelo
        Confidence      = $classification.Confidence
        Error           = $null
        Resumen         = $classification.Resumen
        TipologiaVirtual = $classification.TipologiaVirtual
        Virtual         = $classification.Virtual
    }

    $successCount++
}

# ============================================================================
# RESUMEN FINAL
# ============================================================================

Write-Host ""
Write-Info "==================================================================="
Write-Info " RESUMEN FINAL"
Write-Info "==================================================================="
Write-Info "Total procesados: $($objectIds.Count)"
Write-Success "Exitosos: $successCount"
Write-Error2 "Fallidos: $failCount"
Write-Info "-------------------------------------------------------------------"
Write-Host ""

if ($results.Count -gt 0) {
    Write-Info "Detalle de resultados:"
    Write-Host ""

    $results | Format-Table -AutoSize -Property ObjectIdGDC, InstanceId, RuntimeStatus, Estado, Modelo, Tdn1, FamiliaId, Tdn2, TipologiaId, Confidence, TipologiaVirtual, Virtual, Error

    # Guardar resultados en archivo JSON
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $outputFileJson = "classification-results-$timestamp.json"
    $results | ConvertTo-Json -Depth 10 | Out-File -FilePath $outputFileJson -Encoding UTF8
    Write-Success "Resultados guardados en JSON: $outputFileJson"

    # Guardar resultados en archivo CSV con las columnas solicitadas
    $outputFileCsv = "classification-results-$timestamp.csv"
    $csvData = $results | Select-Object -Property `
        ObjectIdGDC, `
        InstanceId, `
        RuntimeStatus, `
        Estado, `
        Modelo, `
        Tdn1, `
        FamiliaId, `
        Tdn2, `
        TipologiaId, `
        Confidence, `
        Error, `
        @{Name='Resumen'; Expression={ if ($_.Resumen) { $_.Resumen -replace "`r`n", " " -replace "`n", " " -replace ",", ";" } else { "" } }}, `
        @{Name='TipologiaVirtual'; Expression={ if ($_.TipologiaVirtual) { "Sí" } else { "No" } }}, `
        @{Name='Virtual'; Expression={ if ($_.Virtual) { "Sí" } else { "No" } }}

    $csvData | Export-Csv -Path $outputFileCsv -NoTypeInformation -Encoding UTF8
    Write-Success "Resultados guardados en CSV: $outputFileCsv"
}

Write-Host ""
Write-Info "==================================================================="
Write-Info " Proceso completado"
Write-Info "==================================================================="