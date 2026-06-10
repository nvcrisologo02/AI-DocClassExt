$ErrorActionPreference = "Stop"

function To-DoubleValue {
  param([object]$v)
  if ($null -eq $v) { return 0.0 }
  $s = "$v".Trim()
  if ([string]::IsNullOrWhiteSpace($s)) { return 0.0 }
  $lastDot = $s.LastIndexOf('.')
  $lastComma = $s.LastIndexOf(',')
  if ($lastDot -ge 0 -and $lastComma -ge 0) {
    if ($lastComma -gt $lastDot) {
      # 1.234,56 -> 1234.56
      $s = $s -replace '\.', ''
      $s = $s -replace ',', '.'
    }
    else {
      # 1,234.56 -> 1234.56
      $s = $s -replace ',', ''
    }
  }
  elseif ($lastComma -ge 0) {
    # 123,45 -> 123.45
    $s = $s -replace ',', '.'
  }
  $n = 0.0
  if ([double]::TryParse($s, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$n)) {
    return $n
  }
  return 0.0
}

function Sum-PropertyValues {
  param([object[]]$rows, [string]$propertyName)
  $sum = 0.0
  foreach ($r in $rows) {
    $sum += (To-DoubleValue $r.$propertyName)
  }
  return $sum
}

$in = "artifacts\reports\reporte_coste_real_unitario_ultimo_mes_detalle_2026-05-11.csv"
$out = "artifacts\reports\reporte_impacto_infra_8_10_mayo_2026-05-11.csv"
$outDaily = "artifacts\reports\reporte_impacto_infra_8_10_mayo_resumen_diario_2026-05-11.csv"

$rows = Import-Csv $in | Where-Object { $_.day -in @("2026-05-08", "2026-05-09", "2026-05-10") }

$report = $rows |
  Group-Object day, tipologia, tipologia_version, fallback_path, fase |
  ForEach-Object {
    $g = $_.Group
    $docs = [double](Sum-PropertyValues $g 'documentos')
    $pages = [double](Sum-PropertyValues $g 'paginas')

    $foundryTotal = [double](Sum-PropertyValues $g 'foundry_cost_total_case_day_eur')
    $platformTotal = [double](Sum-PropertyValues $g 'platform_cost_total_case_day_eur')
    $infraRestoTotal = $platformTotal - $foundryTotal

    $foundryUnitDoc = if ($docs -gt 0) { $foundryTotal / $docs } else { $null }
    $platformUnitDoc = if ($docs -gt 0) { $platformTotal / $docs } else { $null }
    $infraRestoUnitDoc = if ($docs -gt 0) { $infraRestoTotal / $docs } else { $null }

    $foundryUnitPage = if ($pages -gt 0) { $foundryTotal / $pages } else { $null }
    $platformUnitPage = if ($pages -gt 0) { $platformTotal / $pages } else { $null }
    $infraRestoUnitPage = if ($pages -gt 0) { $infraRestoTotal / $pages } else { $null }

    $infraRestoPctTotal = if ($platformTotal -gt 0) { 100 * $infraRestoTotal / $platformTotal } else { $null }
    $foundryPctTotal = if ($platformTotal -gt 0) { 100 * $foundryTotal / $platformTotal } else { $null }

    [pscustomobject]@{
      day = $g[0].day
      tipologia = $g[0].tipologia
      tipologia_version = $g[0].tipologia_version
      fallback_path = $g[0].fallback_path
      fase = $g[0].fase
      documentos = [int]$docs
      paginas = [decimal]$pages
      foundry_cost_total_eur = [math]::Round($foundryTotal, 6)
      foundry_cost_unitario_doc_eur = [math]::Round($foundryUnitDoc, 6)
      foundry_cost_unitario_pagina_eur = [math]::Round($foundryUnitPage, 6)
      platform_cost_total_eur = [math]::Round($platformTotal, 6)
      platform_cost_unitario_doc_eur = [math]::Round($platformUnitDoc, 6)
      platform_cost_unitario_pagina_eur = [math]::Round($platformUnitPage, 6)
      infra_resto_cost_total_eur = [math]::Round($infraRestoTotal, 6)
      infra_resto_cost_unitario_doc_eur = [math]::Round($infraRestoUnitDoc, 6)
      infra_resto_cost_unitario_pagina_eur = [math]::Round($infraRestoUnitPage, 6)
      foundry_pct_sobre_total = [math]::Round($foundryPctTotal, 2)
      infra_resto_pct_sobre_total = [math]::Round($infraRestoPctTotal, 2)
    }
  } |
  Sort-Object day, tipologia, tipologia_version, fallback_path, fase

$daily = $report |
  Group-Object day |
  ForEach-Object {
    $docs = [double](Sum-PropertyValues $_.Group 'documentos')
    $pages = [double](Sum-PropertyValues $_.Group 'paginas')
    $foundry = [double](Sum-PropertyValues $_.Group 'foundry_cost_total_eur')
    $platform = [double](Sum-PropertyValues $_.Group 'platform_cost_total_eur')
    $infra = [double](Sum-PropertyValues $_.Group 'infra_resto_cost_total_eur')
    $fallbackDocs = [double](Sum-PropertyValues ($_.Group | Where-Object { $_.fallback_path -eq "con_fallback" }) 'documentos')
    $fallbackPct = if ($docs -gt 0) { [math]::Round((100 * $fallbackDocs / $docs), 2) } else { 0 }

    [pscustomobject]@{
      day = $_.Name
      documentos = [int]$docs
      paginas = [decimal]$pages
      fallback_docs = [int]$fallbackDocs
      fallback_pct = $fallbackPct
      foundry_cost_total_eur = [math]::Round($foundry, 6)
      infra_resto_cost_total_eur = [math]::Round($infra, 6)
      platform_cost_total_eur = [math]::Round($platform, 6)
      foundry_pct_sobre_total = if ($platform -gt 0) { [math]::Round(100 * $foundry / $platform, 2) } else { 0 }
      infra_resto_pct_sobre_total = if ($platform -gt 0) { [math]::Round(100 * $infra / $platform, 2) } else { 0 }
      cost_unitario_doc_total_eur = if ($docs -gt 0) { [math]::Round($platform / $docs, 6) } else { 0 }
      cost_unitario_pagina_total_eur = if ($pages -gt 0) { [math]::Round($platform / $pages, 6) } else { 0 }
    }
  } | Sort-Object day

$report | Export-Csv -Path $out -NoTypeInformation -Encoding UTF8
$daily | Export-Csv -Path $outDaily -NoTypeInformation -Encoding UTF8

Write-Host "Exportado caso a caso: $out"
Write-Host "Exportado resumen diario: $outDaily"
Write-Host "=== RESUMEN DIARIO 8-10 MAYO ==="
$daily | Format-Table -AutoSize
