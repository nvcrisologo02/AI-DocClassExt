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
    [ValidateRange(1, 16)]
    [int]$MaxParallelInstances = 4,
    [switch]$SaveArtifacts,
    [string]$ResumeArtifactsPath
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

function Normalize-DocumentKey {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Name)) {
        return $null
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($Name)
    if ([string]::IsNullOrWhiteSpace($baseName)) {
        $baseName = $Name
    }

    return ($baseName -replace '[^a-zA-Z0-9._-]', '_')
}

function Get-ArtifactDocumentKey {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArtifactBaseName
    )

    $withoutInstance = $ArtifactBaseName
    $lastDash = $ArtifactBaseName.LastIndexOf('-')
    if ($lastDash -gt 0) {
        $withoutInstance = $ArtifactBaseName.Substring(0, $lastDash)
    }

    return Normalize-DocumentKey -Name $withoutInstance
}

function Get-ProviderDetail {
    param(
        [object[]]$Providers,
        [string[]]$Names
    )
    if (-not $Providers) { return $null }
    foreach ($n in $Names) {
        $hit = $Providers | Where-Object { $_.Proveedor -eq $n } | Select-Object -First 1
        if ($null -ne $hit) { return $hit }
    }
    return $null
}

function Convert-ArtifactJsonToSummaryRow {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$JsonFile
    )

    try {
        $artifact = Get-Content -Path $JsonFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        Write-Host "[WARN] Error leyendo JSON $($JsonFile.Name): $($_.Exception.Message)" -ForegroundColor Yellow
        return $null
    }

    $runtimeStatus = "$(Get-FieldValue -Object $artifact -Names @('runtimeStatus', 'RuntimeStatus'))"
    if ($runtimeStatus -ne 'Completed') {
        return $null
    }

    try {
        $output = Get-FieldValue -Object $artifact -Names @('output', 'Output')
        if ($null -eq $output) {
            return $null
        }

        $identificacion = Get-FieldValue -Object $output -Names @('Identificacion', 'identificacion')
        $resultadoFinal = Get-FieldValue -Object $output -Names @('Resultado', 'resultado')
        $detalle = Get-FieldValue -Object $output -Names @('DetalleEjecucion', 'detalleEjecucion')
        $clasificacion = Get-FieldValue -Object $detalle -Names @('Clasificacion', 'clasificacion')

        # Format confidence con porcentaje
        $confidence = ""
        if ($null -ne $clasificacion) {
            $rawConf = $clasificacion.Confianza
            if ($null -ne $rawConf -and "$rawConf" -ne "") {
                $rawConfNum = [double]$rawConf
                $pct = [math]::Round($rawConfNum * 100, 1)
                $pctStr = $pct.ToString("0.0", [System.Globalization.CultureInfo]::GetCultureInfo("es-ES"))
                $rawStr = $rawConfNum.ToString("0.####", [System.Globalization.CultureInfo]::InvariantCulture)
                $confidence = "$pctStr % (raw: $rawStr)"
            }

            # Extract provider details
            $providers = @($clasificacion.DetalleProveedores)
            $provReglas = Get-ProviderDetail -Providers $providers -Names @("Reglas")
            $provDI = Get-ProviderDetail -Providers $providers -Names @("DI", "DocumentIntelligence")
            $provFoundry = Get-ProviderDetail -Providers $providers -Names @("FoundryRescue")
        }

        $duracion = ""
        if ($null -ne $detalle -and $null -ne $detalle.Seguimiento) {
            $duracion = $detalle.Seguimiento.DuracionTotalMs
        }
        if ([string]::IsNullOrWhiteSpace($duracion) -and $null -ne $artifact.customStatus) {
            $duracion = $artifact.customStatus.duracionTotalMs
        }

        return [PSCustomObject]@{
            FileName = Get-FieldValue -Object $identificacion -Names @('Documento', 'documento')
            Status = Get-FieldValue -Object $resultadoFinal -Names @('Estado', 'estado')
            Typology = Get-FieldValue -Object $identificacion -Names @('Tipologia', 'tipologia')
            TipologiaFamilia = Get-FieldValue -Object $identificacion -Names @('TipologiaFamilia', 'tipologiaFamilia')
            TipologiaVersion = Get-FieldValue -Object $identificacion -Names @('TipologiaVersion', 'tipologiaVersion')
            FechaProceso = Get-FieldValue -Object $identificacion -Names @('FechaProceso', 'fechaProceso')
            PaginasIncluidas = Get-FieldValue -Object $detalle -Names @('PaginasIncluidas', 'paginasIncluidas')
            Paginas = Get-FieldValue -Object $identificacion -Names @('Paginas', 'paginas')
            Tdn1 = Get-FieldValue -Object $identificacion -Names @('Tdn1', 'tdn1')
            Tdn2 = Get-FieldValue -Object $identificacion -Names @('Tdn2', 'tdn2')
            Matricula = Get-FieldValue -Object $identificacion -Names @('Matricula', 'matricula')
            Clasificador = Get-FieldValue -Object $clasificacion -Names @('Clasificador', 'clasificador', 'Modelo', 'modelo')
            Confidence = $confidence
            FallbackLLM = if ($null -eq $clasificacion.FallbackLLM) { "" } else { $clasificacion.FallbackLLM.ToString().ToLowerInvariant() }
            DuracionTotalMs = $duracion
            'DetalleProveedores.Reglas.Tipologia' = $provReglas.Tipologia
            'DetalleProveedores.Reglas.Confianza' = $provReglas.Confianza
            'DetalleProveedores.Reglas.MotivoDescarte' = $provReglas.MotivoDescarte
            'DetalleProveedores.DI.Tipologia' = $provDI.Tipologia
            'DetalleProveedores.DI.Confianza' = $provDI.Confianza
            'DetalleProveedores.DI.MotivoDescarte' = $provDI.MotivoDescarte
            'DetalleProveedores.FoundryRescue.Tipologia' = $provFoundry.Tipologia
            'DetalleProveedores.FoundryRescue.Confianza' = $provFoundry.Confianza
            'DetalleProveedores.FoundryRescue.MotivoDescarte' = $provFoundry.MotivoDescarte
            ReutilizadaPorDuplicado = if ($null -eq $resultadoFinal.ReutilizadaPorDuplicado) { "" } else { $resultadoFinal.ReutilizadaPorDuplicado.ToString().ToLowerInvariant() }
            ArtifactLastWriteTimeUtc = $JsonFile.LastWriteTimeUtc
        }
    }
    catch {
        Write-Host "[WARN] Error procesando JSON $($JsonFile.Name): $($_.Exception.Message)" -ForegroundColor Yellow
        return $null
    }
}

function Get-ProcessedDocumentsFromArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArtifactsPath
    )

    $processedNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

    $jsonFiles = Get-ChildItem -Path $ArtifactsPath -Filter "*.json" -File -ErrorAction SilentlyContinue
    foreach ($jsonFile in $jsonFiles) {
        try {
            $artifact = Get-Content -Path $jsonFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
            $runtime = "$(Get-FieldValue -Object $artifact -Names @('runtimeStatus', 'RuntimeStatus'))"
            if ($runtime -ne "Completed") {
                continue
            }

            $documentKey = Get-ArtifactDocumentKey -ArtifactBaseName $jsonFile.BaseName
            if (-not [string]::IsNullOrWhiteSpace($documentKey)) {
                $processedNames.Add($documentKey) | Out-Null
            }
        }
        catch {
            # Ignora JSONs que no corresponden a artefactos de ejecución.
        }
    }

    return [PSCustomObject]@{
        Names = $processedNames
    }
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

$resumeArtifactsResolved = $null
$alreadyProcessed = $null
$skippedByResume = 0
$persistArtifacts = $SaveArtifacts.IsPresent -or -not [string]::IsNullOrWhiteSpace($ResumeArtifactsPath)

if (-not [string]::IsNullOrWhiteSpace($ResumeArtifactsPath)) {
    try {
        $resumeArtifactsResolved = (Resolve-Path -Path $ResumeArtifactsPath -ErrorAction Stop).Path
    }
    catch {
        throw "No se encontró la carpeta de artefactos para reanudar: '$ResumeArtifactsPath'"
    }

    $alreadyProcessed = Get-ProcessedDocumentsFromArtifacts -ArtifactsPath $resumeArtifactsResolved

    $pendingFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    foreach ($f in $files) {
        $fileKey = Normalize-DocumentKey -Name $f.Name
        if ($alreadyProcessed.Names.Contains($fileKey)) {
            $skippedByResume++
            continue
        }

        $pendingFiles.Add($f) | Out-Null
    }

    $files = @($pendingFiles)
}

$artifactFolder = $null
if ($persistArtifacts) {
    if (-not [string]::IsNullOrWhiteSpace($resumeArtifactsResolved)) {
        $artifactFolder = $resumeArtifactsResolved
    }
    else {
        $artifactFolder = Join-Path $PSScriptRoot ("artifacts\\classification-hybrid-trace-{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
        New-Item -Path $artifactFolder -ItemType Directory -Force | Out-Null
    }
}

Write-Host ""
Write-Host "============================================================"
Write-Host " Test clasificación hybrid (traza detallada por carpeta)"
Write-Host "============================================================"
Write-Host "Entrada                 : $resolvedFolder"
Write-Host "Documentos detectados   : $($files.Count)"
if (-not [string]::IsNullOrWhiteSpace($resumeArtifactsResolved)) {
    Write-Host "Reanudar desde          : $resumeArtifactsResolved"
    Write-Host "Saltados por reanudar   : $skippedByResume"
}
Write-Host "Endpoint                : $Endpoint"
Write-Host "Provider clasificación  : hybrid-tdn"
Write-Host "Umbral clasificación    : $ClassificationUmbral"
Write-Host "SkipGDCUpload           : $(-not $EnableGdcUpload.IsPresent)"
Write-Host "ClassificationOnly flag : $ForceClassificationOnly"
Write-Host "Max pages class-only    : $MaxPagesForClassificationOnly"
Write-Host "Instancias paralelas    : $MaxParallelInstances"
Write-Host ""

$results = New-Object System.Collections.Generic.List[object]
$totalPending = $files.Count
$startedCount = 0
$completedCount = 0
$fileQueue = New-Object System.Collections.Generic.Queue[System.IO.FileInfo]
foreach ($queued in $files) {
    $fileQueue.Enqueue($queued)
}

$inFlight = @{}

while ($fileQueue.Count -gt 0 -or $inFlight.Count -gt 0) {
    while ($fileQueue.Count -gt 0 -and $inFlight.Count -lt $MaxParallelInstances) {
        $file = $fileQueue.Dequeue()
        $startedCount++

        Write-Host "------------------------------------------------------------"
        Write-Host "Documento [$startedCount/$totalPending] (pendientes de lanzar: $($fileQueue.Count), en vuelo: $($inFlight.Count)) : $($file.FullName)"

        try {
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

            $response = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $requestBody -ContentType "application/json"
            $instanceId = $response.instanceId
            $statusUri = Resolve-StatusUri -StatusUri $response.statusQueryUri

            $inFlight[$instanceId] = [PSCustomObject]@{
                File = $file
                InstanceId = $instanceId
                StatusUri = $statusUri
                Retry = 0
            }

            Write-Host "InstanceId             : $instanceId"
            Write-Host "StatusUri              : $statusUri"
        }
        catch {
            Write-Host "[ERROR] Fallo enviando documento $($file.Name): $($_.Exception.Message)"
            $completedCount++
            $results.Add([PSCustomObject]@{
                File = $file.FullName
                InstanceId = $null
                RuntimeStatus = "ERROR"
                ResultadoEstado = "ERROR"
                IdentificacionDocumento = $null
                IdentificacionGuid = $null
                Tipologia = $null
                TipologiaFamilia = $null
                TipologiaVersion = $null
                FechaProceso = $null
                Paginas = $null
                Tdn1 = $null
                Tdn2 = $null
                Matricula = $null
                TipologiaNombre = $null
                TipologiaMGDCMatricula = $null
                GdcTipoDocumento = $null
                GdcSubtipoDocumento = $null
                GdcSerie = $null
                GptDescripcion = $null
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

    if ($inFlight.Count -eq 0) {
        continue
    }

    Start-Sleep -Seconds $PollDelaySeconds

    foreach ($kv in @($inFlight.GetEnumerator())) {
        $ctx = $kv.Value
        $ctx.Retry++

        $status = $null
        $runtimeStatus = $null
        try {
            $status = Invoke-RestMethod -Uri $ctx.StatusUri -Method Get
            $runtimeStatus = Get-FieldValue -Object $status -Names @("runtimeStatus", "RuntimeStatus")

            if ($runtimeStatus -eq "Running" -and ($ctx.Retry % 3 -eq 0)) {
                $current = Get-FieldValue -Object $status.customStatus -Names @("actividadActual", "ActividadActual", "currentActivity")
                Write-Host "[RUNNING] $($ctx.File.Name) | intento=$($ctx.Retry)/$MaxPollRetries | actividad=$current"
            }
        }
        catch {
            Write-Host "[WARN] Error consultando estado de $($ctx.File.Name): $($_.Exception.Message)"
        }

        $completedOrEnded = $runtimeStatus -in @("Completed", "Failed", "Terminated")
        $timeout = $ctx.Retry -ge $MaxPollRetries
        if (-not $completedOrEnded -and -not $timeout) {
            continue
        }

        $null = $inFlight.Remove($ctx.InstanceId)
        $completedCount++

        if ($runtimeStatus -eq "Completed") {
            $output = $status.output
            $identificacion = Get-FieldValue -Object $output -Names @("Identificacion", "identificacion")
            $resultadoFinal = Get-FieldValue -Object $output -Names @("Resultado", "resultado")
            $detalle = Get-FieldValue -Object $output -Names @("DetalleEjecucion", "detalleEjecucion")
            $clasificacion = Get-FieldValue -Object $detalle -Names @("Clasificacion", "clasificacion")
            $seguimiento = Get-FieldValue -Object $detalle -Names @("Seguimiento", "seguimiento")

            $tipologia = Get-FieldValue -Object $identificacion -Names @("Tipologia", "tipologia")
            $tdn1 = Get-FieldValue -Object $identificacion -Names @("Tdn1", "tdn1")
            $tdn2 = Get-FieldValue -Object $identificacion -Names @("Tdn2", "tdn2")
            $matricula = Get-FieldValue -Object $identificacion -Names @("Matricula", "matricula")
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

            Write-Host "[OK] COMPLETADO [$completedCount/$totalPending] $($ctx.File.Name) | Tipología=$tipologia | Estado=$estado"

            $record = [PSCustomObject]@{
                File = $ctx.File.FullName
                InstanceId = $ctx.InstanceId
                RuntimeStatus = $runtimeStatus
                ResultadoEstado = $estado
                IdentificacionDocumento = Get-FieldValue -Object $identificacion -Names @("Documento", "documento")
                IdentificacionGuid = Get-FieldValue -Object $identificacion -Names @("Guid", "guid")
                Tipologia = $tipologia
                TipologiaFamilia = Get-FieldValue -Object $identificacion -Names @("TipologiaFamilia", "tipologiaFamilia")
                TipologiaVersion = Get-FieldValue -Object $identificacion -Names @("TipologiaVersion", "tipologiaVersion")
                FechaProceso = Get-FieldValue -Object $identificacion -Names @("FechaProceso", "fechaProceso")
                Paginas = Get-FieldValue -Object $identificacion -Names @("Paginas", "paginas")
                Tdn1 = $tdn1
                Tdn2 = $tdn2
                Matricula = $matricula
                TipologiaNombre = Get-FieldValue -Object $identificacion -Names @("TipologiaNombre", "tipologiaNombre")
                TipologiaMGDCMatricula = Get-FieldValue -Object $identificacion -Names @("TipologiaMGDCMatricula", "tipologiaMGDCMatricula")
                GdcTipoDocumento = Get-FieldValue -Object $identificacion -Names @("GdcTipoDocumento", "gdcTipoDocumento")
                GdcSubtipoDocumento = Get-FieldValue -Object $identificacion -Names @("GdcSubtipoDocumento", "gdcSubtipoDocumento")
                GdcSerie = Get-FieldValue -Object $identificacion -Names @("GdcSerie", "gdcSerie")
                GptDescripcion = Get-FieldValue -Object $identificacion -Names @("GptDescripcion", "gptDescripcion")
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

            if ($persistArtifacts) {
                $safeName = [IO.Path]::GetFileNameWithoutExtension($ctx.File.Name)
                $safeName = ($safeName -replace '[^a-zA-Z0-9._-]', '_')
                $jsonPath = Join-Path $artifactFolder ("{0}-{1}.json" -f $safeName, $ctx.InstanceId)
                $summaryPath = Join-Path $artifactFolder ("{0}-{1}-justificacion.txt" -f $safeName, $ctx.InstanceId)
                $justificationLines = Build-ClassificationJustification -Output $output -Seguimiento $seguimiento

                $artifactPayload = [ordered]@{
                    name = Get-FieldValue -Object $status -Names @("name", "Name")
                    instanceId = $ctx.InstanceId
                    runtimeStatus = $runtimeStatus
                    createdTime = Get-FieldValue -Object $status -Names @("createdTime", "CreatedTime")
                    lastUpdatedTime = Get-FieldValue -Object $status -Names @("lastUpdatedTime", "LastUpdatedTime")
                    customStatus = Get-FieldValue -Object $status -Names @("customStatus", "CustomStatus")
                    output = $output
                }

                $artifactPayload | ConvertTo-Json -Depth 50 | Out-File -FilePath $jsonPath -Encoding utf8
                $justificationLines | Out-File -FilePath $summaryPath -Encoding utf8
            }
        }
        else {
            $fallbackError = if ($timeout) { "Timeout de polling" } else { "Estado final: $runtimeStatus" }
            Write-Host "[WARN] Finaliza sin completar [$completedCount/$totalPending] $($ctx.File.Name) | $fallbackError"
            $results.Add([PSCustomObject]@{
                File = $ctx.File.FullName
                InstanceId = $ctx.InstanceId
                RuntimeStatus = if ($timeout) { "TIMEOUT" } else { "$runtimeStatus" }
                ResultadoEstado = "TIMEOUT_OR_PENDING"
                IdentificacionDocumento = $null
                IdentificacionGuid = $null
                Tipologia = $null
                TipologiaFamilia = $null
                TipologiaVersion = $null
                FechaProceso = $null
                Paginas = $null
                Tdn1 = $null
                Tdn2 = $null
                Matricula = $null
                TipologiaNombre = $null
                TipologiaMGDCMatricula = $null
                GdcTipoDocumento = $null
                GdcSubtipoDocumento = $null
                GdcSerie = $null
                GptDescripcion = $null
                ClassificationOnly = $null
                Clasificador = $null
                Confianza = $null
                FallbackLLM = $null
                FallbackRazon = $fallbackError
                RecorteAplicado = $null
                PaginasIncluidas = $null
                MarkdownGenerado = $null
                OrigenMarkdown = $null
                ModeloLLMUsado = $null
            }) | Out-Null
        }
    }
}

Write-Host ""
Write-Host "============================================================"
Write-Host " Resumen final"
Write-Host "============================================================"

$results | Format-Table -AutoSize

if ($persistArtifacts) {
    $csvPath = Join-Path $artifactFolder "summary.csv"

    # Reconstruir el summary con TODOS los artefactos JSON del folder de salida.
    $rowsFromArtifacts = New-Object System.Collections.Generic.List[object]
    $artifactJsonFiles = Get-ChildItem -Path $artifactFolder -Filter "*.json" -File -ErrorAction SilentlyContinue
    foreach ($artifactJsonFile in $artifactJsonFiles) {
        $row = Convert-ArtifactJsonToSummaryRow -JsonFile $artifactJsonFile
        if ($null -ne $row) {
            $rowsFromArtifacts.Add($row) | Out-Null
        }
    }

    $latestByFile = @{}
    foreach ($row in $rowsFromArtifacts) {
        $fileKey = "$(Get-FieldValue -Object $row -Names @('FileName', 'fileName'))"
        if ([string]::IsNullOrWhiteSpace($fileKey)) {
            continue
        }

        if (-not $latestByFile.ContainsKey($fileKey)) {
            $latestByFile[$fileKey] = $row
            continue
        }

        if ($row.ArtifactLastWriteTimeUtc -gt $latestByFile[$fileKey].ArtifactLastWriteTimeUtc) {
            $latestByFile[$fileKey] = $row
        }
    }

    $rowsToPersist = $latestByFile.Values | Sort-Object FileName
    $rowsToPersist | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
    Write-Host "Artefactos guardados en : $artifactFolder"
    Write-Host "Resumen CSV             : $csvPath"
    Write-Host "Filas consolidadas CSV  : $(@($rowsToPersist).Count)"
}

$completed = ($results | Where-Object { $_.RuntimeStatus -eq "Completed" }).Count
$errors = ($results | Where-Object { $_.RuntimeStatus -eq "ERROR" }).Count
$timeouts = ($results | Where-Object { $_.RuntimeStatus -ne "Completed" -and $_.RuntimeStatus -ne "ERROR" }).Count

if (-not [string]::IsNullOrWhiteSpace($resumeArtifactsResolved)) {
    Write-Host "Saltados por reanudar   : $skippedByResume"
}

if ($errors -gt 0) {
    exit 1
}

if ($timeouts -gt 0) {
    exit 2
}

exit 0
