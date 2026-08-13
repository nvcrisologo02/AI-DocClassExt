Set-StrictMode -Version Latest

function Get-E2ECoverageMatrix {
    param([Parameter(Mandatory = $true)][string]$MatrixPath)
    if (-not (Test-Path -Path $MatrixPath)) { throw "No existe la matriz funcional: $MatrixPath" }
    return @(Get-Content -Raw -Path $MatrixPath | ConvertFrom-Json)
}

function Assert-E2ECoverageRefs {
    param([Parameter(Mandatory = $true)][array]$Cases, [Parameter(Mandatory = $true)][array]$Matrix)
    $validIds = @($Matrix | ForEach-Object { $_.id })
    $errors = @()
    foreach ($case in $Cases) {
        $covers = @()
        if ($null -ne $case.PSObject.Properties['covers']) { $covers = @($case.covers) }
        foreach ($ref in $covers) {
            if ($validIds -notcontains $ref) {
                $errors += "Caso $($case.caseKey): covers '$ref' no existe en la matriz funcional"
            }
        }
    }
    if ($errors.Count -gt 0) { throw ($errors -join "`n") }
}

function Get-E2ECoverage {
    param(
        [Parameter(Mandatory = $true)][array]$Matrix,
        [Parameter(Mandatory = $true)][array]$Cases,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][array]$Results,
        [string[]]$ActiveConditions = @()
    )
    $statusByKey = @{}
    foreach ($r in $Results) { $statusByKey[$r.CaseKey] = $r.Status }

    $items = foreach ($item in $Matrix) {
        $conditional = $null
        if ($null -ne $item.PSObject.Properties['conditional']) { $conditional = $item.conditional }
        $casesForItem = @($Cases | Where-Object {
            $null -ne $_.PSObject.Properties['covers'] -and @($_.covers) -contains $item.id
        })
        $estado =
            if ($casesForItem.Count -eq 0) { "sin-caso" }
            elseif (-not [string]::IsNullOrWhiteSpace($conditional) -and $ActiveConditions -notcontains $conditional) { "no-aplicable" }
            else {
                $executedKeys = @($casesForItem | Where-Object {
                    $statusByKey.ContainsKey($_.caseKey) -and $statusByKey[$_.caseKey] -in @("PASS", "FAIL")
                })
                if ($executedKeys.Count -eq 0) { "no-ejecutado" }
                elseif (@($executedKeys | Where-Object { $statusByKey[$_.caseKey] -eq "PASS" }).Count -gt 0) { "cubierto-pass" }
                else { "cubierto-fail" }
            }
        [pscustomobject]@{
            Id = $item.id; Area = $item.area; Descripcion = $item.descripcion
            Estado = $estado
            Casos = @($casesForItem | ForEach-Object { $_.caseKey })
        }
    }

    $itemsArr   = @($items)
    $aplicables = @($itemsArr | Where-Object { $_.Estado -ne "no-aplicable" })
    $cubiertos  = @($aplicables | Where-Object { $_.Estado -like "cubierto-*" })
    return [pscustomobject]@{
        Items               = $itemsArr
        TotalMatriz         = $itemsArr.Count
        Aplicables          = $aplicables.Count
        Cubiertos           = $cubiertos.Count
        PorcentajeCobertura = if ($aplicables.Count -gt 0) { [math]::Round(100.0 * $cubiertos.Count / $aplicables.Count, 1, [MidpointRounding]::AwayFromZero) } else { 0.0 }
    }
}
