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
    It "todos tienen seed con request y al menos una pasada" {
        foreach ($c in $script:cases) {
            $c.seed | Should -Not -BeNullOrEmpty -Because $c.caseKey
            $c.seed.request | Should -Not -BeNullOrEmpty -Because $c.caseKey
            @($c.pasadas).Count | Should -BeGreaterThan 0 -Because $c.caseKey
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
    # Skip temporal: quedan 8 de los 9 ids MDW sin caso a proposito hasta la
    # Tarea 7 (solo se escribe el caso piloto MDW-03 en esta tarea). La Tarea 7
    # anade los ocho casos restantes y quita este -Skip.
    It "todo id MDW de la matriz tiene al menos un caso" -Skip {
        $idsMdw   = @($script:matrix | Where-Object { $_.area -eq "Markdown" } | ForEach-Object { $_.id })
        $cubiertos = @($script:cases | ForEach-Object { @($_.covers) } | Sort-Object -Unique)
        foreach ($id in $idsMdw) {
            $cubiertos | Should -Contain $id -Because "MDW sin caso: $id"
        }
    }
}
