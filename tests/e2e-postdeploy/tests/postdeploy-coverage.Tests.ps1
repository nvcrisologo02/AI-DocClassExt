BeforeAll {
    . (Join-Path $PSScriptRoot ".." "lib" "postdeploy-coverage.ps1")
    $script:matrixPath = Join-Path $PSScriptRoot ".." "coverage" "functional-matrix.json"
}

Describe "Get-E2ECoverageMatrix" {
    It "carga la matriz real con ids unicos" {
        $m = Get-E2ECoverageMatrix -MatrixPath $script:matrixPath
        $m.Count | Should -BeGreaterThan 20
        ($m.id | Sort-Object -Unique).Count | Should -Be $m.Count
    }
}

Describe "Assert-E2ECoverageRefs" {
    It "lanza si un caso referencia un id inexistente" {
        $matrix = @([pscustomobject]@{ id = "CLS-01"; area = "x"; descripcion = "y" })
        $cases  = @([pscustomobject]@{ caseKey = "S-1"; covers = @("CLS-01", "ZZZ-99") })
        { Assert-E2ECoverageRefs -Cases $cases -Matrix $matrix } | Should -Throw "*ZZZ-99*"
    }
    It "pasa con referencias validas" {
        $matrix = @([pscustomobject]@{ id = "CLS-01"; area = "x"; descripcion = "y" })
        $cases  = @([pscustomobject]@{ caseKey = "S-1"; covers = @("CLS-01") })
        { Assert-E2ECoverageRefs -Cases $cases -Matrix $matrix } | Should -Not -Throw
    }
}

Describe "Get-E2ECoverage" {
    BeforeAll {
        $script:matrix = @(
            [pscustomobject]@{ id = "A-01"; area = "A"; descripcion = "a" },
            [pscustomobject]@{ id = "A-02"; area = "A"; descripcion = "b" },
            [pscustomobject]@{ id = "G-01"; area = "G"; descripcion = "g"; conditional = "gdc" },
            [pscustomobject]@{ id = "X-01"; area = "X"; descripcion = "x" }
        )
        $script:cases = @(
            [pscustomobject]@{ caseKey = "S-1"; covers = @("A-01") },
            [pscustomobject]@{ caseKey = "S-2"; covers = @("A-02") },
            [pscustomobject]@{ caseKey = "G-1"; covers = @("G-01") }
        )
    }
    It "calcula estados y porcentaje sin condicion gdc activa" {
        $results = @(
            [pscustomobject]@{ CaseKey = "S-1"; Status = "PASS" },
            [pscustomobject]@{ CaseKey = "S-2"; Status = "FAIL" },
            [pscustomobject]@{ CaseKey = "G-1"; Status = "NA" }
        )
        $cov = Get-E2ECoverage -Matrix $script:matrix -Cases $script:cases -Results $results -ActiveConditions @()
        ($cov.Items | Where-Object Id -eq "A-01").Estado | Should -Be "cubierto-pass"
        ($cov.Items | Where-Object Id -eq "A-02").Estado | Should -Be "cubierto-fail"
        ($cov.Items | Where-Object Id -eq "G-01").Estado | Should -Be "no-aplicable"
        ($cov.Items | Where-Object Id -eq "X-01").Estado | Should -Be "sin-caso"
        $cov.Aplicables | Should -Be 3
        $cov.Cubiertos | Should -Be 2
        $cov.PorcentajeCobertura | Should -Be 66.7
    }
    It "caso no ejecutado (SKIP o fuera de perfil) marca no-ejecutado" {
        $results = @([pscustomobject]@{ CaseKey = "S-1"; Status = "SKIP" })
        $cov = Get-E2ECoverage -Matrix $script:matrix -Cases $script:cases -Results $results -ActiveConditions @("gdc")
        ($cov.Items | Where-Object Id -eq "A-01").Estado | Should -Be "no-ejecutado"
        ($cov.Items | Where-Object Id -eq "G-01").Estado | Should -Be "no-ejecutado"
    }
    It "redondea PorcentajeCobertura AwayFromZero en un empate real (.x5)" {
        # 1/16 = 6.25%: ToEven daria 6.2, AwayFromZero exige 6.3 — este dataset
        # discrimina el modo de redondeo (un revert del MidpointRounding rompe el test).
        $matrix16 = @(1..16 | ForEach-Object { [pscustomobject]@{ id = "Y-$_"; area = "Y"; descripcion = "y$_" } })
        $cases16 = @([pscustomobject]@{ caseKey = "C-1"; covers = @("Y-1") })
        $results16 = @([pscustomobject]@{ CaseKey = "C-1"; Status = "PASS" })
        $cov = Get-E2ECoverage -Matrix $matrix16 -Cases $cases16 -Results $results16 -ActiveConditions @()
        $cov.Aplicables | Should -Be 16
        $cov.Cubiertos | Should -Be 1
        $cov.PorcentajeCobertura | Should -Be 6.3
    }
}
