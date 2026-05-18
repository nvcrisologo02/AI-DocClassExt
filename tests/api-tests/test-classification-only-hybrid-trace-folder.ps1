param(
    [Parameter(Mandatory = $true)]
    [string]$InputFolder,
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",
    [string]$SubmittedBy = "usuario.prueba@sareb.es",
    [double]$ClassificationUmbral = 0.50,
    [int]$MaxPagesForClassificationOnly = 0,
    [int]$MaxPollRetries = 90,
    [int]$PollDelaySeconds = 2,
    [switch]$EnableGdcUpload,
    [switch]$ForceClassificationOnly,
    [switch]$SaveArtifacts
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Get-FieldValue {
    param(
        [Parameter(Mandatory = $false)]
        [object]$Object,
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    if ($null -eq $Object) {
        return $null
    }

    foreach ($name in $Names) {
        $prop = $Object.PSObject.Properties[$name]
        if ($null -ne $prop) {
            return $prop.Value
        }
    }

    return $null
}

function Resolve-StatusUri {
    param([Parameter(Mandatory = $true)][string]$StatusUri)

    if ($StatusUri -match "http://localhost/") {
        return ($StatusUri -replace "http://localhost/", "http://localhost:7071/")
    }

    return $StatusUri
}

function Decode-FallbackReason {
    param([string]$FallbackReason)

    if ([string]::IsNullOrWhiteSpace($FallbackReason)) {
        return "Sin fallback explícito"
    }

    if ($FallbackReason.StartsWith("low_confidence:")) {
        return "DI devolvió baja confianza y se activó fallback al siguiente clasificador"
    }

    if ($FallbackReason.StartsWith("resto_classification:")) {
        return "DI clasificó como RESTO, por lo que se forzó fallback"
    }

    if ($FallbackReason.StartsWith("exception:")) {
        return "La clasificación principal lanzó excepción y se activó fallback"
    }

    if ($FallbackReason.StartsWith("fallback_attempt_failed:")) {
        return "Se intentó fallback pero falló; se mantuvo el resultado anterior"
    }

    if ($FallbackReason -eq "fallback_unclassified") {
        return "No se logró clasificar de forma concluyente"
    }

    return "Fallback informado por el pipeline: $FallbackReason"
}

function Build-ClassificationJustification {
    param(
        [object]$Output,
        [object]$Seguimiento
    )

    $detalle = Get-FieldValue -Object $Output -Names @("DetalleEjecucion", "detalleEjecucion")
    $clasificacion = Get-FieldValue -Object $detalle -Names @("Clasificacion", "clasificacion")
    $identificacion = Get-FieldValue -Object $Output -Names @("Identificacion", "identificacion")

    $clasificador = Get-FieldValue -Object $clasificacion -Names @("Clasificador", "clasificador", "Modelo", "modelo")
    $proveedor = Get-FieldValue -Object $clasificacion -Names @("ProveedorClasif", "proveedorClasif")
    $fallback = Get-FieldValue -Object $clasificacion -Names @("FallbackLLM", "fallbackLLM")
    $fallbackReason = Get-FieldValue -Object $clasificacion -Names @("FallbackRazon", "fallbackRazon")
    $confianza = Get-FieldValue -Object $clasificacion -Names @("Confianza", "confianza")
    $tipologia = Get-FieldValue -Object $identificacion -Names @("Tipologia", "tipologia")
    $pagesProcessed = Get-FieldValue -Object $clasificacion -Names @("PagesProcessed", "pagesProcessed")
    $totalPages = Get-FieldValue -Object $identificacion -Names @("Paginas", "paginas")
    $classificationOnly = Get-FieldValue -Object $detalle -Names @("ClassificationOnly", "classificationOnly")
    $recorteAplicado = Get-FieldValue -Object $detalle -Names @("RecorteAplicado", "recorteAplicado")
    $paginasIncluidas = Get-FieldValue -Object $detalle -Names @("PaginasIncluidas", "paginasIncluidas")
    $markdownGenerado = Get-FieldValue -Object $detalle -Names @("MarkdownGenerado", "markdownGenerado")
    $origenMarkdown = Get-FieldValue -Object $detalle -Names @("OrigenMarkdown", "origenMarkdown")
    $modeloLlm = Get-FieldValue -Object $detalle -Names @("ModeloLLMUsado", "modeloLLMUsado")

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("Tipología final: $tipologia")
    $lines.Add("Clasificador final: $clasificador | Proveedor final: $proveedor | Confianza: $confianza")
    $lines.Add("ClassificationOnly salida: $classificationOnly")

    if ($clasificador -eq "RuleBasedTDN") {
        $lines.Add("Decisión: heurística aceptada (reglas superaron el umbral de confianza).")
    }
    elseif ($clasificador -eq "DocumentIntelligence") {
        $lines.Add("Decisión: heurística insuficiente; DI resolvió la clasificación con confianza suficiente.")
    }
    elseif ($clasificador -eq "FoundryRescue") {
        $lines.Add("Decisión: heurística y DI no resolvieron con calidad requerida; se usó rescate LLM.")
    }
    elseif ($clasificador -eq "expectedtype-input") {
        $lines.Add("Decisión: clasificación forzada por ExpectedType de entrada.")
    }

    if ($fallback -eq $true) {
        $lines.Add("Fallback activado: SI")
        $lines.Add("Motivo fallback: $(Decode-FallbackReason -FallbackReason $fallbackReason)")
    }
    elseif (-not [string]::IsNullOrWhiteSpace($fallbackReason)) {
        $lines.Add("Fallback activado: NO")
        $lines.Add("Motivo reportado por pipeline: $(Decode-FallbackReason -FallbackReason $fallbackReason)")
    }

    if ($null -ne $recorteAplicado -or $paginasIncluidas -gt 0) {
        $lines.Add("Recorte clasificación: aplicado=$recorteAplicado | páginas incluidas=$paginasIncluidas | total=$totalPages")
    }
    elseif ($pagesProcessed -gt 0) {
        $lines.Add("Recorte/ventana clasificación: páginas procesadas=$pagesProcessed de total=$totalPages")
    }
    elseif ($totalPages -gt 0) {
        $lines.Add("Recorte/ventana clasificación: no expuesto explícitamente en salida; páginas totales detectadas=$totalPages")
    }

    if ($markdownGenerado -eq $true) {
        $lines.Add("Markdown generado: SI | origen=$origenMarkdown")
    }

    if (-not [string]::IsNullOrWhiteSpace($modeloLlm)) {
        $lines.Add("Modelo LLM usado: $modeloLlm")
    }

    if ($Seguimiento -and $Seguimiento.Actividades) {
        $clasificarStep = $Seguimiento.Actividades | Where-Object { $_.Nombre -eq "Clasificar" -or $_.nombre -eq "Clasificar" } | Select-Object -First 1
        if ($null -ne $clasificarStep) {
            $mensaje = Get-FieldValue -Object $clasificarStep -Names @("Mensaje", "mensaje")
            if (-not [string]::IsNullOrWhiteSpace($mensaje)) {
                $lines.Add("Mensaje actividad Clasificar: $mensaje")
            }
        }
    }

    return $lines
}

function New-RequestPayload {
    param(
        [Parameter(Mandatory = $true)][string]$DocumentName,
        [Parameter(Mandatory = $true)][string]$DocumentBase64,
        [Parameter(Mandatory = $true)][string]$SubmittedBy,
        [Parameter(Mandatory = $true)][double]$ClassificationUmbral,
        [Parameter(Mandatory = $true)][int]$MaxPagesForClassificationOnly,
        [Parameter(Mandatory = $true)][bool]$SkipGdcUpload,
        [Parameter(Mandatory = $true)][bool]$ForceClassificationOnly
    )

    $instrucciones = [ordered]@{
        skipDuplicateCheck = $true
        forceReprocess = $true
        skipGDCUpload = $SkipGdcUpload
        classification = [ordered]@{
            provider = "hybrid-tdn"
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

    if ($ForceClassificationOnly) {
        $instrucciones["classificationOnly"] = $true
        if ($MaxPagesForClassificationOnly -gt 0) {
            $instrucciones["maxPagesForClassificationOnly"] = $MaxPagesForClassificationOnly
        }
    }

    return [ordered]@{
        instrucciones = $instrucciones
        documento = [ordered]@{
            name = $DocumentName
            content = [ordered]@{
                base64 = $DocumentBase64
            }
        }
        trazabilidad = [ordered]@{
            correlationId = "HYBRID-CLASS-ONLY-$(Get-Date -Format 'yyyyMMdd-HHmmss-fff')"
            submittedBy = $SubmittedBy
            idGDC = $null
            idActivo = $null
        }
    } | ConvertTo-Json -Depth 20
}

if ($MaxPollRetries -lt 1) {
    throw "MaxPollRetries debe ser mayor que 0"
}

if ($PollDelaySeconds -lt 1) {
    throw "PollDelaySeconds debe ser mayor que 0"
}

try {
    $resolvedFolder = (Resolve-Path -Path $InputFolder -ErrorAction Stop).Path
}
catch {
    throw "No se encontró la carpeta de entrada: '$InputFolder'"
}

$files = Get-ChildItem -Path $resolvedFolder -File -Recurse |
    Where-Object {
        $_.Extension -match '^\.(pdf|tif|tiff|jpg|jpeg|png)$'
    } |
    Sort-Object FullName

if ($files.Count -eq 0) {
    throw "No se encontraron documentos compatibles en: $resolvedFolder"
}

$artifactFolder = $null
if ($SaveArtifacts) {
    $artifactFolder = Join-Path $PSScriptRoot ("artifacts\\classification-hybrid-trace-{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -Path $artifactFolder -ItemType Directory -Force | Out-Null
}

Write-Host ""
Write-Host "============================================================"
Write-Host " Test clasificación hybrid (traza detallada por carpeta)"
Write-Host "============================================================"
Write-Host "Entrada                 : $resolvedFolder"
Write-Host "Documentos detectados   : $($files.Count)"
Write-Host "Endpoint                : $Endpoint"
Write-Host "Provider clasificación  : hybrid-tdn"
Write-Host "Umbral clasificación    : $ClassificationUmbral"
Write-Host "SkipGDCUpload           : $(-not $EnableGdcUpload.IsPresent)"
Write-Host "ClassificationOnly flag : $ForceClassificationOnly"
Write-Host "Max pages class-only    : $MaxPagesForClassificationOnly"
Write-Host ""

$results = New-Object System.Collections.Generic.List[object]

foreach ($file in $files) {
    Write-Host "------------------------------------------------------------"
    Write-Host "Documento: $($file.FullName)"

    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $base64 = [System.Convert]::ToBase64String($bytes)
    $requestBody = New-RequestPayload `
        -DocumentName $file.Name `
        -DocumentBase64 $base64 `
        -SubmittedBy $SubmittedBy `
        -ClassificationUmbral $ClassificationUmbral `
        -MaxPagesForClassificationOnly $MaxPagesForClassificationOnly `
        -SkipGdcUpload (-not $EnableGdcUpload.IsPresent) `
        -ForceClassificationOnly $ForceClassificationOnly.IsPresent

    Write-Host "Carga local            : $([Math]::Round($bytes.Length / 1KB, 2)) KB"
    Write-Host "Paso envío             : POST IngestDocument"

    $status = $null
    $instanceId = $null
    $statusUri = $null

    try {
        $response = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $requestBody -ContentType "application/json"
        $instanceId = $response.instanceId
        $statusUri = Resolve-StatusUri -StatusUri $response.statusQueryUri

        Write-Host "InstanceId             : $instanceId"
        Write-Host "StatusUri              : $statusUri"

        $retry = 0
        do {
            Start-Sleep -Seconds $PollDelaySeconds
            $retry++

            try {
                $status = Invoke-RestMethod -Uri $statusUri -Method Get
                $runtime = Get-FieldValue -Object $status -Names @("runtimeStatus", "RuntimeStatus")

                $line = "[$retry/$MaxPollRetries] Estado: $runtime"
                if ($runtime -eq "Running" -and $status.customStatus) {
                    $current = Get-FieldValue -Object $status.customStatus -Names @("actividadActual", "ActividadActual", "currentActivity")
                    $completed = Get-FieldValue -Object $status.customStatus -Names @("actividadesCompletadas", "ActividadesCompletadas", "completedActivities")
                    $elapsed = Get-FieldValue -Object $status.customStatus -Names @("duracionTotalMs", "DuracionTotalMs", "elapsedMs")

                    if (-not [string]::IsNullOrWhiteSpace($current)) {
                        $line += " | Actual: $current"
                    }

                    if ($completed -is [System.Collections.IEnumerable]) {
                        $line += " | Completadas: $($completed.Count)"
                    }

                    if ($null -ne $elapsed) {
                        $line += " | Duración(ms): $elapsed"
                    }
                }

                Write-Host $line
            }
            catch {
                Write-Host "[$retry/$MaxPollRetries] Error consultando estado: $($_.Exception.Message)"
            }
        }
        while ($status.runtimeStatus -in @("Running", "Pending") -and $retry -lt $MaxPollRetries)

        $runtimeStatus = Get-FieldValue -Object $status -Names @("runtimeStatus", "RuntimeStatus")

        if ($runtimeStatus -eq "Completed") {
            $output = $status.output
            $identificacion = Get-FieldValue -Object $output -Names @("Identificacion", "identificacion")
            $resultadoFinal = Get-FieldValue -Object $output -Names @("Resultado", "resultado")
            $detalle = Get-FieldValue -Object $output -Names @("DetalleEjecucion", "detalleEjecucion")
            $clasificacion = Get-FieldValue -Object $detalle -Names @("Clasificacion", "clasificacion")
            $seguimiento = Get-FieldValue -Object $detalle -Names @("Seguimiento", "seguimiento")

            $tipologia = Get-FieldValue -Object $identificacion -Names @("Tipologia", "tipologia")
            $estado = Get-FieldValue -Object $resultadoFinal -Names @("Estado", "estado")
            $confianza = Get-FieldValue -Object $clasificacion -Names @("Confianza", "confianza")
            $clasificador = Get-FieldValue -Object $clasificacion -Names @("Clasificador", "clasificador", "Modelo", "modelo")
            $fallback = Get-FieldValue -Object $clasificacion -Names @("FallbackLLM", "fallbackLLM")
            $fallbackReason = Get-FieldValue -Object $clasificacion -Names @("FallbackRazon", "fallbackRazon")
            $classificationOnlySalida = Get-FieldValue -Object $detalle -Names @("ClassificationOnly", "classificationOnly")
            $recorteAplicado = Get-FieldValue -Object $detalle -Names @("RecorteAplicado", "recorteAplicado")
            $paginasIncluidas = Get-FieldValue -Object $detalle -Names @("PaginasIncluidas", "paginasIncluidas")
            $markdownGenerado = Get-FieldValue -Object $detalle -Names @("MarkdownGenerado", "markdownGenerado")
            $origenMarkdown = Get-FieldValue -Object $detalle -Names @("OrigenMarkdown", "origenMarkdown")
            $modeloLlm = Get-FieldValue -Object $detalle -Names @("ModeloLLMUsado", "modeloLLMUsado")
            $motivoErrorTipologia = Get-FieldValue -Object $detalle -Names @("MotivoErrorTipologia", "motivoErrorTipologia")

            Write-Host ""
            Write-Host "[OK] COMPLETADO"
            Write-Host "Estado final            : $estado"
            Write-Host "Tipología               : $tipologia"
            Write-Host "ClassificationOnly      : $classificationOnlySalida"
            Write-Host "Clasificador            : $clasificador"
            Write-Host "Confianza clasificación : $confianza"
            Write-Host "Fallback LLM            : $fallback"
            Write-Host "Fallback razón          : $fallbackReason"
            Write-Host "Recorte aplicado        : $recorteAplicado"
            Write-Host "Páginas incluidas       : $paginasIncluidas"
            Write-Host "Markdown generado       : $markdownGenerado"
            Write-Host "Origen markdown         : $origenMarkdown"
            Write-Host "Modelo LLM usado        : $modeloLlm"
            if (-not [string]::IsNullOrWhiteSpace($motivoErrorTipologia)) {
                Write-Host "Motivo error tipología  : $motivoErrorTipologia"
            }

            if ($seguimiento -and $seguimiento.Actividades) {
                Write-Host ""
                Write-Host "Traza de actividades:"
                foreach ($a in $seguimiento.Actividades) {
                    $nombre = Get-FieldValue -Object $a -Names @("Nombre", "nombre")
                    $estadoA = Get-FieldValue -Object $a -Names @("Estado", "estado")
                    $duracion = Get-FieldValue -Object $a -Names @("DuracionMs", "duracionMs")
                    $fallbackA = Get-FieldValue -Object $a -Names @("FallbackActivado", "fallbackActivado")
                    $fallbackRazonA = Get-FieldValue -Object $a -Names @("FallbackRazon", "fallbackRazon")
                    $mensajeA = Get-FieldValue -Object $a -Names @("Mensaje", "mensaje")

                    Write-Host " - $nombre | estado=$estadoA | duracionMs=$duracion | fallback=$fallbackA"
                    if (-not [string]::IsNullOrWhiteSpace($fallbackRazonA)) {
                        Write-Host "   razon fallback: $fallbackRazonA"
                    }
                    if (-not [string]::IsNullOrWhiteSpace($mensajeA)) {
                        Write-Host "   mensaje      : $mensajeA"
                    }
                }
            }

            $justificationLines = Build-ClassificationJustification -Output $output -Seguimiento $seguimiento
            Write-Host ""
            Write-Host "Justificación del camino de clasificación:"
            foreach ($j in $justificationLines) {
                Write-Host " - $j"
            }

            $record = [PSCustomObject]@{
                File = $file.FullName
                InstanceId = $instanceId
                RuntimeStatus = $runtimeStatus
                ResultadoEstado = $estado
                Tipologia = $tipologia
                ClassificationOnly = $classificationOnlySalida
                Clasificador = $clasificador
                Confianza = $confianza
                FallbackLLM = $fallback
                FallbackRazon = $fallbackReason
                RecorteAplicado = $recorteAplicado
                PaginasIncluidas = $paginasIncluidas
                MarkdownGenerado = $markdownGenerado
                OrigenMarkdown = $origenMarkdown
                ModeloLLMUsado = $modeloLlm
            }
            $results.Add($record) | Out-Null

            if ($SaveArtifacts) {
                $safeName = [IO.Path]::GetFileNameWithoutExtension($file.Name)
                $safeName = ($safeName -replace '[^a-zA-Z0-9._-]', '_')
                $jsonPath = Join-Path $artifactFolder ("{0}-{1}.json" -f $safeName, $instanceId)
                $summaryPath = Join-Path $artifactFolder ("{0}-{1}-justificacion.txt" -f $safeName, $instanceId)

                $status | ConvertTo-Json -Depth 50 | Out-File -FilePath $jsonPath -Encoding utf8
                $justificationLines | Out-File -FilePath $summaryPath -Encoding utf8
            }
        }
        else {
            Write-Host "[WARN] No completado en tiempo límite. Estado actual: $runtimeStatus"
            $results.Add([PSCustomObject]@{
                File = $file.FullName
                InstanceId = $instanceId
                RuntimeStatus = $runtimeStatus
                ResultadoEstado = "TIMEOUT_OR_PENDING"
                Tipologia = $null
                ClassificationOnly = $null
                Clasificador = $null
                Confianza = $null
                FallbackLLM = $null
                FallbackRazon = $null
                RecorteAplicado = $null
                PaginasIncluidas = $null
                MarkdownGenerado = $null
                OrigenMarkdown = $null
                ModeloLLMUsado = $null
            }) | Out-Null
        }
    }
    catch {
        Write-Host "[ERROR] Fallo en documento $($file.Name): $($_.Exception.Message)"
        $results.Add([PSCustomObject]@{
            File = $file.FullName
            InstanceId = $instanceId
            RuntimeStatus = "ERROR"
            ResultadoEstado = "ERROR"
            Tipologia = $null
            ClassificationOnly = $null
            Clasificador = $null
            Confianza = $null
            FallbackLLM = $null
            FallbackRazon = $_.Exception.Message
            RecorteAplicado = $null
            PaginasIncluidas = $null
            MarkdownGenerado = $null
            OrigenMarkdown = $null
            ModeloLLMUsado = $null
        }) | Out-Null
    }
}

Write-Host ""
Write-Host "============================================================"
Write-Host " Resumen final"
Write-Host "============================================================"

$results | Format-Table -AutoSize

if ($SaveArtifacts) {
    $csvPath = Join-Path $artifactFolder "summary.csv"
    $results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
    Write-Host "Artefactos guardados en : $artifactFolder"
    Write-Host "Resumen CSV             : $csvPath"
}

$completed = ($results | Where-Object { $_.RuntimeStatus -eq "Completed" }).Count
$errors = ($results | Where-Object { $_.RuntimeStatus -eq "ERROR" }).Count
$timeouts = ($results | Where-Object { $_.RuntimeStatus -ne "Completed" -and $_.RuntimeStatus -ne "ERROR" }).Count

if ($errors -gt 0) {
    exit 1
}

if ($timeouts -gt 0) {
    exit 2
}

exit 0
