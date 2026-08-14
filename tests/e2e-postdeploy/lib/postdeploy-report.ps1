Set-StrictMode -Version Latest

function New-E2EReport {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$RunInfo,
        [Parameter(Mandatory = $true)][array]$Results,
        [Parameter(Mandatory = $true)][pscustomobject]$Coverage,
        [Parameter(Mandatory = $true)][string]$OutDir
    )
    if (-not (Test-Path -Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

    $pass = @($Results | Where-Object Status -eq "PASS").Count
    $fail = @($Results | Where-Object Status -eq "FAIL").Count
    $skip = @($Results | Where-Object Status -eq "SKIP").Count
    $na   = @($Results | Where-Object Status -eq "NA").Count

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# Reporte E2E post-despliegue DocumentIA")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- Entorno: **$($RunInfo.Environment)** | Perfil: **$($RunInfo.Profile)** | GDC: $(if ($RunInfo.IncludeGdc) { 'activado' } else { 'desactivado (N/A)' })")
    [void]$sb.AppendLine("- Inicio: $($RunInfo.StartedAtUtc) | Fin: $($RunInfo.FinishedAtUtc)")
    [void]$sb.AppendLine("- Resultado: Total=$($Results.Count) PASS=$pass FAIL=$fail SKIP=$skip N/A=$na")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Cobertura funcional")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("**$($Coverage.PorcentajeCobertura)%** ($($Coverage.Cubiertos) de $($Coverage.Aplicables) items aplicables; matriz total: $($Coverage.TotalMatriz))")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Area | Item | Estado | Casos | Descripcion |")
    [void]$sb.AppendLine("|---|---|---|---|---|")
    foreach ($item in ($Coverage.Items | Sort-Object Area, Id)) {
        $icon = switch ($item.Estado) {
            "cubierto-pass" { "OK" }
            "cubierto-fail" { "FALLO" }
            "no-ejecutado"  { "no ejecutado" }
            "no-aplicable"  { "N/A" }
            "sin-caso"      { "SIN CASO" }
            default         { $item.Estado }
        }
        [void]$sb.AppendLine("| $($item.Area) | $($item.Id) | $icon | $($item.Casos -join ', ') | $($item.Descripcion) |")
    }
    $huecos = @($Coverage.Items | Where-Object { $_.Estado -in @("sin-caso", "no-ejecutado") })
    if ($huecos.Count -gt 0) {
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("### Items no cubiertos en esta ejecucion")
        foreach ($h in $huecos) { [void]$sb.AppendLine("- **$($h.Id)** ($($h.Estado)): $($h.Descripcion)") }
    }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Casos")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Caso | Nombre | Status | Estado negocio | s | Detalle |")
    [void]$sb.AppendLine("|---|---|---|---|---|---|")
    foreach ($r in $Results) {
        $estado  = if ($null -ne $r.PSObject.Properties['Estado']) { $r.Estado } else { "" }
        $elapsed = if ($null -ne $r.PSObject.Properties['ElapsedSec']) { $r.ElapsedSec } else { "" }
        $detalle = if ($r.Status -ne "PASS") { $r.Reason } else { "" }
        [void]$sb.AppendLine("| $($r.CaseKey) | $($r.Name) | $($r.Status) | $estado | $elapsed | $detalle |")
    }

    $reportPath = Join-Path $OutDir "report.md"
    $sb.ToString() | Out-File -FilePath $reportPath -Encoding UTF8 -Force

    $coverageJson = [pscustomobject]@{
        entorno             = $RunInfo.Environment
        perfil              = $RunInfo.Profile
        gdcActivado         = [bool]$RunInfo.IncludeGdc
        inicioUtc           = $RunInfo.StartedAtUtc
        finUtc              = $RunInfo.FinishedAtUtc
        totales             = [pscustomobject]@{ total = $Results.Count; pass = $pass; fail = $fail; skip = $skip; na = $na }
        porcentajeCobertura = $Coverage.PorcentajeCobertura
        aplicables          = $Coverage.Aplicables
        cubiertos           = $Coverage.Cubiertos
        items               = @($Coverage.Items | ForEach-Object {
            [pscustomobject]@{ id = $_.Id; area = $_.Area; estado = $_.Estado; casos = $_.Casos; descripcion = $_.Descripcion }
        })
    }
    $coverageJsonPath = Join-Path $OutDir "coverage.json"
    $coverageJson | ConvertTo-Json -Depth 6 | Out-File -FilePath $coverageJsonPath -Encoding UTF8 -Force

    return [pscustomobject]@{ ReportPath = $reportPath; CoverageJsonPath = $coverageJsonPath }
}
