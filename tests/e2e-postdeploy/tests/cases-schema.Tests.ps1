BeforeAll {
    . (Join-Path $PSScriptRoot ".." "lib" "postdeploy-coverage.ps1")
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")).Path
    $script:casesDir = Join-Path $PSScriptRoot ".." "cases"
    $script:matrix = Get-E2ECoverageMatrix -MatrixPath (Join-Path $PSScriptRoot ".." "coverage" "functional-matrix.json")
    $script:allCases = @(Get-ChildItem $script:casesDir -Filter "*-cases.json" | ForEach-Object {
        Get-Content -Raw $_.FullName | ConvertFrom-Json
    } | ForEach-Object { $_ })
}

Describe "Esquema de casos e2e-postdeploy" {
    It "hay casos smoke y full" {
        @($script:allCases | Where-Object { $_.profiles -contains "smoke" }).Count | Should -BeGreaterThan 3
        @($script:allCases | Where-Object { $_.profiles -contains "full" }).Count | Should -BeGreaterThan 12
    }
    It "caseKey unicos" {
        $keys = @($script:allCases | ForEach-Object { $_.caseKey })
        ($keys | Sort-Object -Unique).Count | Should -Be $keys.Count
    }
    It "todos los casos declaran profiles y covers" {
        foreach ($c in $script:allCases) {
            @($c.profiles).Count | Should -BeGreaterThan 0 -Because $c.caseKey
            @($c.covers).Count | Should -BeGreaterThan 0 -Because $c.caseKey
        }
    }
    It "covers referencian ids validos de la matriz" {
        { Assert-E2ECoverageRefs -Cases $script:allCases -Matrix $script:matrix } | Should -Not -Throw
    }
    It "documentPath relativos existen (salvo malformedJson)" {
        foreach ($c in ($script:allCases | Where-Object {
            $payloadMode = if ($_.PSObject.Properties['payloadMode']) { $_.payloadMode } else { $null }
            $payloadMode -ne "malformedJson"
        })) {
            $full = Join-Path $script:repoRoot $c.documentPath
            Test-Path $full | Should -BeTrue -Because "$($c.caseKey): $($c.documentPath)"
        }
    }
    It "todo caso gdc declara requires gdc" {
        foreach ($c in ($script:allCases | Where-Object { $_.skipGDCUpload -eq $false })) {
            @($c.requires) | Should -Contain "gdc" -Because $c.caseKey
        }
    }
    It "los items de matriz sin caso son solo los documentados" {
        $covered = @($script:allCases | ForEach-Object { @($_.covers) } | Sort-Object -Unique)
        # Las areas Markdown y Dedup se excluyen aqui: sus casos viven en
        # cases-validacion/, no en cases/, y los valida un runner propio
        # (ver cases-validacion-schema.Tests.ps1 y cases-validacion-dedup-schema.Tests.ps1).
        $areasDeValidacion = @("Markdown", "Dedup")
        $sinCaso = @($script:matrix | Where-Object { $covered -notcontains $_.id -and $areasDeValidacion -notcontains $_.area } | ForEach-Object { $_.id })
        # PIP-06 sin caso por diseno (requiere tipologia limitada); OPS-01 lo
        # cubre el pseudo-caso de health del runner. EXT-04 (modelo de
        # extraccion alternativo por request) se retiro de la matriz: la
        # seleccion de provider/modelo de extraccion por request se descarto
        # por decision de producto (AB#100132).
        $sinCaso | Sort-Object | Should -Be @("OPS-01", "PIP-06")
    }
}
