param(
    [string]$CsvPath = "C:\Users\ubf0238\Downloads\cost-analysis.csv",
    [string]$SqlServer = "srbsqlprodocai.database.windows.net",
    [string]$Database = "DocumentIA",
    [string]$SqlUser = "docaisql",
    [string]$SqlPassword = "Sareb2013.",
    [string]$OutputDir = "artifacts\reports",
    [string]$FromDateInput,
    [string]$ToDateInput
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CsvPath)) {
    throw "CSV not found: $CsvPath"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$today = (Get-Date).Date
$fromDate = if ([string]::IsNullOrWhiteSpace($FromDateInput)) { $today.AddDays(-30) } else { [datetime]::Parse($FromDateInput).Date }
$toDate = if ([string]::IsNullOrWhiteSpace($ToDateInput)) { $today } else { [datetime]::Parse($ToDateInput).Date }

if ($toDate -lt $fromDate) {
    throw "ToDate cannot be earlier than FromDate"
}

if ($fromDate -is [System.Array]) { $fromDate = $fromDate[0] }
if ($toDate -is [System.Array]) { $toDate = $toDate[0] }

$fromDate = [datetime]$fromDate
$toDate = [datetime]$toDate

$costRows = Import-Csv $CsvPath

$dailyCost = @{}
foreach ($g in ($costRows | Group-Object UsageDate)) {
    $day = [datetime]::Parse($g.Name).ToString("yyyy-MM-dd")
    $platform = ($g.Group | Measure-Object -Property Cost -Sum).Sum
    $foundry = ($g.Group | Where-Object { $_.ServiceName -in @("Foundry Models", "Foundry Tools") } | Measure-Object -Property Cost -Sum).Sum

    $dailyCost[$day] = [pscustomobject]@{
        day = $day
        foundry_cost_eur = [decimal]$foundry
        platform_cost_eur = [decimal]$platform
    }
}

$fromSql = $fromDate.ToString("yyyy-MM-dd")
$toSql = $toDate.ToString("yyyy-MM-dd")

$query = @"
SET NOCOUNT ON;
WITH ExecBase AS
(
    SELECT
        CAST(e.FechaEjecucion AS date) AS [day],
        REPLACE(REPLACE(COALESCE(NULLIF(e.Tipologia, ''), 'desconocida'), CHAR(13), ''), CHAR(10), '') AS tipologia,
        REPLACE(REPLACE(COALESCE(
            NULLIF(JSON_VALUE(e.ContratoSalidaCompletoJson, '$.Identificacion.TipologiaVersion'), ''),
            NULLIF(JSON_VALUE(e.DatosFinalesJson, '$.Identificacion.TipologiaVersion'), ''),
            'n/a'
        ), CHAR(13), ''), CHAR(10), '') AS tipologia_version,
        CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM OPENJSON(e.ActivityTimelineJson) j
                WHERE JSON_VALUE(j.value, '$.Nombre') = 'Extraer'
                  AND
                  (
                      JSON_VALUE(j.value, '$.FallbackActivado') = 'true'
                      OR NULLIF(JSON_VALUE(j.value, '$.FallbackRazon'), '') IS NOT NULL
                      OR JSON_VALUE(j.value, '$.Mensaje') LIKE '%insufficient_extraction%'
                  )
            )
            THEN 'con_fallback'
            ELSE 'sin_fallback'
        END AS fallback_path,
        COALESCE(
            TRY_CONVERT(decimal(18, 4), NULLIF(JSON_VALUE(e.ContratoSalidaCompletoJson, '$.Identificacion.Paginas'), '')),
            TRY_CONVERT(decimal(18, 4), NULLIF(JSON_VALUE(e.DatosFinalesJson, '$.Identificacion.Paginas'), '')),
            TRY_CONVERT(decimal(18, 4), NULLIF(JSON_VALUE(e.ContratoSalidaCompletoJson, '$.Paginas'), '')),
            TRY_CONVERT(decimal(18, 4), NULLIF(JSON_VALUE(e.DatosFinalesJson, '$.Paginas'), '')),
            CAST(1 AS decimal(18, 4))
        ) AS paginas,
        CASE
            WHEN COALESCE(e.DuracionExtraccionMs, 0) > 0
              OR COALESCE(e.DuracionIntegracionMs, 0) > 0
              OR COALESCE(e.DuracionGDCMs, 0) > 0
            THEN 'exportacion'
            ELSE 'clasificacion'
        END AS fase
    FROM DocumentoEjecuciones e
    WHERE CAST(e.FechaEjecucion AS date) BETWEEN '$fromSql' AND '$toSql'
), Agg AS
(
    SELECT
        [day],
        tipologia,
        tipologia_version,
        fallback_path,
        fase,
        COUNT(*) AS documentos,
        SUM(paginas) AS paginas
    FROM ExecBase
    GROUP BY [day], tipologia, tipologia_version, fallback_path, fase
), DayAgg AS
(
    SELECT [day], COUNT(*) AS documentos_dia, SUM(paginas) AS paginas_dia
    FROM ExecBase
    GROUP BY [day]
)
SELECT
    CONVERT(varchar(10), a.[day], 23) AS [day],
    a.tipologia,
    a.tipologia_version,
    a.fallback_path,
    a.fase,
    a.documentos,
    a.paginas,
    d.documentos_dia,
    d.paginas_dia
FROM Agg a
INNER JOIN DayAgg d ON d.[day] = a.[day]
ORDER BY a.[day], a.tipologia, a.tipologia_version, a.fallback_path, a.fase
FOR JSON PATH;
"@

$json = sqlcmd -S $SqlServer -d $Database -U $SqlUser -P $SqlPassword -w 65535 -y 0 -Y 0 -Q $query
$rawText = ($json -join "`n").Trim()

$start = $rawText.IndexOf('[')
$end = $rawText.LastIndexOf(']')
$jsonText = if ($start -ge 0 -and $end -gt $start) { $rawText.Substring($start, $end - $start + 1) } else { "" }

if ([string]::IsNullOrWhiteSpace($jsonText)) {
    throw "No execution data returned from SQL"
}

$parsedRows = $jsonText | ConvertFrom-Json
$execRows = @()

if ($parsedRows -is [System.Array]) {
    $execRows = @($parsedRows)
}
elseif ($null -ne $parsedRows) {
    $dayProp = $parsedRows.PSObject.Properties['day']
    $dayValue = if ($null -ne $dayProp) { $dayProp.Value } else { $null }
    if ($dayValue -is [System.Array]) {
        $rowCount = $dayValue.Count
        for ($i = 0; $i -lt $rowCount; $i++) {
            $execRows += [pscustomobject]@{
                day = $parsedRows.day[$i]
                tipologia = $parsedRows.tipologia[$i]
                tipologia_version = $parsedRows.tipologia_version[$i]
                fallback_path = $parsedRows.fallback_path[$i]
                fase = $parsedRows.fase[$i]
                documentos = $parsedRows.documentos[$i]
                paginas = $parsedRows.paginas[$i]
                documentos_dia = $parsedRows.documentos_dia[$i]
                paginas_dia = $parsedRows.paginas_dia[$i]
            }
        }
    }
    else {
        $execRows = @($parsedRows)
    }
}

function Get-ScalarValue {
    param([object]$Value)
    if ($Value -is [System.Array]) {
        if ($Value.Count -gt 0) { return $Value[0] }
        return $null
    }
    return $Value
}

$detail = foreach ($r in $execRows) {
    $day = Get-ScalarValue $r.day
    if ([string]::IsNullOrWhiteSpace("$day")) { continue }
    $tipologiaClean = ("$(Get-ScalarValue $r.tipologia)" -replace "\s+", " ").Trim()
    $versionClean = ("$(Get-ScalarValue $r.tipologia_version)" -replace "\s", "").Trim()
    $docsDay = [decimal](Get-ScalarValue $r.documentos_dia)
    $docsCase = [decimal](Get-ScalarValue $r.documentos)
    $pagesDay = [decimal](Get-ScalarValue $r.paginas_dia)
    $pagesCase = [decimal](Get-ScalarValue $r.paginas)
    $hasCost = $dailyCost.ContainsKey($day)

    if ($hasCost -and $docsDay -gt 0) {
        $foundryUnit = [decimal]$dailyCost[$day].foundry_cost_eur / $docsDay
        $platformUnit = [decimal]$dailyCost[$day].platform_cost_eur / $docsDay
        $foundryTotalCase = $foundryUnit * $docsCase
        $platformTotalCase = $platformUnit * $docsCase
    }
    else {
        $foundryUnit = $null
        $platformUnit = $null
        $foundryTotalCase = $null
        $platformTotalCase = $null
    }

    if ($hasCost -and $pagesDay -gt 0) {
        $foundryUnitPage = [decimal]$dailyCost[$day].foundry_cost_eur / $pagesDay
        $platformUnitPage = [decimal]$dailyCost[$day].platform_cost_eur / $pagesDay
        $foundryTotalCasePages = $foundryUnitPage * $pagesCase
        $platformTotalCasePages = $platformUnitPage * $pagesCase
    }
    else {
        $foundryUnitPage = $null
        $platformUnitPage = $null
        $foundryTotalCasePages = $null
        $platformTotalCasePages = $null
    }

    [pscustomobject]@{
        day = $day
        tipologia = $tipologiaClean
        tipologia_version = $versionClean
        fallback_path = (Get-ScalarValue $r.fallback_path)
        fase = (Get-ScalarValue $r.fase)
        documentos = [int]$docsCase
        paginas = [decimal]$pagesCase
        documentos_dia = [int]$docsDay
        paginas_dia = [decimal]$pagesDay
        cost_data_available = $hasCost
        foundry_cost_unitario_doc_eur = $foundryUnit
        foundry_cost_total_case_day_eur = $foundryTotalCase
        foundry_cost_unitario_pagina_eur = $foundryUnitPage
        foundry_cost_total_case_day_paginas_eur = $foundryTotalCasePages
        platform_cost_unitario_doc_eur = $platformUnit
        platform_cost_total_case_day_eur = $platformTotalCase
        platform_cost_unitario_pagina_eur = $platformUnitPage
        platform_cost_total_case_day_paginas_eur = $platformTotalCasePages
    }
}

$summary = $detail |
    Group-Object tipologia, tipologia_version, fallback_path, fase |
    ForEach-Object {
        $rows = $_.Group
        $docsTotal = ($rows | Measure-Object documentos -Sum).Sum
        $rowsWithCost = $rows | Where-Object { $_.cost_data_available -eq $true }
        $docsWithCost = ($rowsWithCost | Measure-Object documentos -Sum).Sum
        $pagesTotal = ($rows | Measure-Object paginas -Sum).Sum
        $pagesWithCost = ($rowsWithCost | Measure-Object paginas -Sum).Sum
        if ($null -eq $docsWithCost) { $docsWithCost = 0 }
        if ($null -eq $pagesTotal) { $pagesTotal = 0 }
        if ($null -eq $pagesWithCost) { $pagesWithCost = 0 }

        $foundryTotal = ($rowsWithCost | Measure-Object foundry_cost_total_case_day_eur -Sum).Sum
        $platformTotal = ($rowsWithCost | Measure-Object platform_cost_total_case_day_eur -Sum).Sum
        $foundryTotalPages = ($rowsWithCost | Measure-Object foundry_cost_total_case_day_paginas_eur -Sum).Sum
        $platformTotalPages = ($rowsWithCost | Measure-Object platform_cost_total_case_day_paginas_eur -Sum).Sum

        if ($null -eq $foundryTotal) { $foundryTotal = 0 }
        if ($null -eq $platformTotal) { $platformTotal = 0 }
        if ($null -eq $foundryTotalPages) { $foundryTotalPages = 0 }
        if ($null -eq $platformTotalPages) { $platformTotalPages = 0 }

        $foundryUnit = if ($docsWithCost -gt 0) { [decimal]$foundryTotal / [decimal]$docsWithCost } else { $null }
        $platformUnit = if ($docsWithCost -gt 0) { [decimal]$platformTotal / [decimal]$docsWithCost } else { $null }
        $foundryUnitPage = if ($pagesWithCost -gt 0) { [decimal]$foundryTotalPages / [decimal]$pagesWithCost } else { $null }
        $platformUnitPage = if ($pagesWithCost -gt 0) { [decimal]$platformTotalPages / [decimal]$pagesWithCost } else { $null }

        [pscustomobject]@{
            tipologia = $rows[0].tipologia
            tipologia_version = $rows[0].tipologia_version
            fallback_path = $rows[0].fallback_path
            fase = $rows[0].fase
            documentos_periodo = [int]$docsTotal
            documentos_con_coste_real = [int]$docsWithCost
            paginas_periodo = [decimal]$pagesTotal
            paginas_con_coste_real = [decimal]$pagesWithCost
            foundry_cost_total_eur = [decimal]$foundryTotal
            foundry_cost_unitario_doc_eur = $foundryUnit
            foundry_cost_unitario_pagina_eur = $foundryUnitPage
            platform_cost_total_eur = [decimal]$platformTotal
            platform_cost_unitario_doc_eur = $platformUnit
            platform_cost_unitario_pagina_eur = $platformUnitPage
        }
    } |
    Sort-Object tipologia, tipologia_version, fallback_path, fase

$dateTag = $today.ToString("yyyy-MM-dd")
$detailPath = Join-Path $OutputDir "reporte_coste_real_unitario_ultimo_mes_detalle_$dateTag.csv"
$summaryPath = Join-Path $OutputDir "reporte_coste_real_unitario_ultimo_mes_resumen_$dateTag.csv"
$coveragePath = Join-Path $OutputDir "reporte_coste_real_unitario_ultimo_mes_cobertura_$dateTag.csv"

$detail | Export-Csv -Path $detailPath -NoTypeInformation -Encoding UTF8
$summary | Export-Csv -Path $summaryPath -NoTypeInformation -Encoding UTF8

$daysInRange = @()
for ($d = $fromDate; $d -le $toDate; $d = $d.AddDays(1)) {
    $daysInRange += $d.ToString("yyyy-MM-dd")
}

$daysWithCost = @($dailyCost.Keys | Sort-Object)
$coverage = [pscustomobject]@{
    from_date = $fromSql
    to_date = $toSql
    days_in_period = $daysInRange.Count
    days_with_real_cost = $daysWithCost.Count
    days_without_real_cost = $daysInRange.Count - $daysWithCost.Count
    real_cost_coverage_pct = if ($daysInRange.Count -gt 0) { [math]::Round(($daysWithCost.Count * 100.0) / $daysInRange.Count, 2) } else { 0 }
    days_with_real_cost_list = ($daysWithCost -join ';')
}
$coverage | Export-Csv -Path $coveragePath -NoTypeInformation -Encoding UTF8

Write-Host "Generated: $detailPath"
Write-Host "Generated: $summaryPath"
Write-Host "Generated: $coveragePath"
