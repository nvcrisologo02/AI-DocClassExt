BeforeAll {
    . (Join-Path $PSScriptRoot ".." "lib" "postdeploy-coverage.ps1")
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")).Path
    $script:casesDir = Join-Path $PSScriptRoot ".." "cases-validacion"
    $script:matrix   = Get-E2ECoverageMatrix -MatrixPath (Join-Path $PSScriptRoot ".." "coverage" "functional-matrix.json")
    $script:cases    = @(Get-ChildItem $script:casesDir -Filter "*-cases.json" | ForEach-Object {
        Get-Content -Raw $_.FullName | ConvertFrom-Json
    } | ForEach-Object { $_ })
}

Describe "Esquema de casos de validacion de markdown" {
    It "hay al menos un caso" {
        $script:cases.Count | Should -BeGreaterThan 0
    }
    It "caseKey unicos" {
        $keys = @($script:cases | ForEach-Object { $_.caseKey })
        @($keys | Sort-Object -Unique).Count | Should -Be $keys.Count
    }
    It "todos declaran covers contra ids MDW de la matriz" {
        $idsMdw = @($script:matrix | Where-Object { $_.area -eq "Markdown" } | ForEach-Object { $_.id })
        foreach ($c in $script:cases) {
            @($c.covers).Count | Should -BeGreaterThan 0 -Because $c.caseKey
            foreach ($ref in @($c.covers)) {
                $idsMdw | Should -Contain $ref -Because "$($c.caseKey) referencia $ref"
            }
        }
    }
    It "todos tienen documentPath existente" {
        foreach ($c in $script:cases) {
            Test-Path (Join-Path $script:repoRoot $c.documentPath) | Should -BeTrue -Because $c.caseKey
        }
    }
    It "todos tienen submittedBy propio" {
        foreach ($c in $script:cases) {
            $c.submittedBy | Should -Be "e2e-validacion-markdown" -Because $c.caseKey
        }
    }
    It "todos tienen seed con request; los que no son backfill tienen al menos una pasada" {
        foreach ($c in $script:cases) {
            $c.seed | Should -Not -BeNullOrEmpty -Because $c.caseKey
            $c.seed.request | Should -Not -BeNullOrEmpty -Because $c.caseKey
            $tipo = if ($c.PSObject.Properties['tipo']) { $c.tipo } else { $null }
            if ($tipo -ne "backfill") {
                @($c.pasadas).Count | Should -BeGreaterThan 0 -Because $c.caseKey
            }
        }
    }
    It "toda pasada tiene nombre, request y assertions" {
        foreach ($c in $script:cases) {
            foreach ($p in @($c.pasadas)) {
                $p.nombre     | Should -Not -BeNullOrEmpty -Because $c.caseKey
                $p.request    | Should -Not -BeNullOrEmpty -Because "$($c.caseKey)/$($p.nombre)"
                $p.assertions | Should -Not -BeNullOrEmpty -Because "$($c.caseKey)/$($p.nombre)"
            }
        }
    }
    It "toda mutacion usa solo columnas de la lista blanca" {
        $permitidas = @("MarkdownPaginas", "MarkdownCompleto", "Paginas")
        foreach ($c in $script:cases) {
            if ($null -eq $c.seed.mutacion) { continue }
            foreach ($prop in $c.seed.mutacion.PSObject.Properties) {
                $permitidas | Should -Contain $prop.Name -Because "$($c.caseKey) muta $($prop.Name)"
            }
        }
    }
    It "todo id MDW de la matriz tiene al menos un caso" {
        $idsMdw   = @($script:matrix | Where-Object { $_.area -eq "Markdown" } | ForEach-Object { $_.id })
        $cubiertos = @($script:cases | ForEach-Object { @($_.covers) } | Sort-Object -Unique)
        foreach ($id in $idsMdw) {
            $cubiertos | Should -Contain $id -Because "MDW sin caso: $id"
        }
    }
    It "toda mutacionEjecucion usa solo rutas de la lista blanca" {
        $permitidas = @("DetalleEjecucion.OrigenMarkdown")
        foreach ($c in $script:cases) {
            if (-not $c.seed.PSObject.Properties['mutacionEjecucion'] -or $null -eq $c.seed.mutacionEjecucion) { continue }
            foreach ($prop in $c.seed.mutacionEjecucion.PSObject.Properties) {
                $permitidas | Should -Contain $prop.Name -Because "$($c.caseKey) muta $($prop.Name)"
            }
        }
    }
    It "todo caso backfill exige estado OK en la siembra" {
        foreach ($c in ($script:cases | Where-Object { $_.PSObject.Properties['tipo'] -and $_.tipo -eq "backfill" })) {
            $c.seed.assertions | Should -Not -BeNullOrEmpty -Because $c.caseKey
            @($c.seed.assertions.expectedStatus) | Should -Contain "OK" -Because "$($c.caseKey): el caso seguro del backfill exige EstadoFinal = OK"
            $c.seed.mutacionEjecucion | Should -Not -BeNullOrEmpty -Because "$($c.caseKey): un caso backfill sin origen fijado no prueba discriminacion"
        }
    }
    It "seed.assertions, si existe, incluye expectedRuntimeStatus" {
        foreach ($c in $script:cases) {
            if (-not $c.seed.PSObject.Properties['assertions'] -or $null -eq $c.seed.assertions) { continue }
            $c.seed.assertions.expectedRuntimeStatus | Should -Be "Completed" -Because $c.caseKey
        }
    }
}
