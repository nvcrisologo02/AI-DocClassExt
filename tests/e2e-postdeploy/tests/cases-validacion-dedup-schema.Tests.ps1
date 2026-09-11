BeforeAll {
    . (Join-Path $PSScriptRoot ".." "lib" "postdeploy-coverage.ps1")
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")).Path
    $script:casesDir = Join-Path $PSScriptRoot ".." "cases-validacion"
    $script:matrix   = Get-E2ECoverageMatrix -MatrixPath (Join-Path $PSScriptRoot ".." "coverage" "functional-matrix.json")
    $script:cases    = @(Get-Content -Raw (Join-Path $script:casesDir "dedup-cases.json") | ConvertFrom-Json)

    # Columnas que expone Get-EjecucionesSnapshot. Una "columna" mal escrita en
    # dbAssertions no reventaria en el runner (StrictMode -Off): Test-DbAssertions
    # la rechaza en ejecucion, pero eso solo se ve tras gastar una ingesta real
    # contra DEV. Aqui se detecta sin tocar nada.
    $script:ColumnasSnapshot = @(
        "Existe", "Total", "Reutilizadas", "Propias",
        "FilasConInstanceIdPasada", "ReutilizadasConInstanceIdPasada",
        "UltimaId", "UltimaInstanceId", "UltimaEstadoFinal",
        "UltimaReutilizada", "UltimaEjecucionOriginalId",
        "UltimaTieneContrato", "UltimaCosteEsNulo", "UltimaApuntaASiembra"
    )
    # Metricas que expone Get-MetricasMonitor en run-validacion-dedup.ps1.
    $script:MetricasMonitor = @("totalEjecuciones", "ok", "reutilizadas", "costeEvitadoEur", "costeTotalEur")
}

Describe "Esquema de casos de validacion de dedup" {
    It "hay al menos un caso" {
        $script:cases.Count | Should -BeGreaterThan 0
    }
    It "caseKey unicos" {
        $keys = @($script:cases | ForEach-Object { $_.caseKey })
        @($keys | Sort-Object -Unique).Count | Should -Be $keys.Count
    }
    It "todos declaran covers contra ids DUP de la matriz" {
        $idsDup = @($script:matrix | Where-Object { $_.area -eq "Dedup" } | ForEach-Object { $_.id })
        foreach ($c in $script:cases) {
            @($c.covers).Count | Should -BeGreaterThan 0 -Because $c.caseKey
            foreach ($ref in @($c.covers)) {
                $idsDup | Should -Contain $ref -Because "$($c.caseKey) referencia $ref"
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
            $c.submittedBy | Should -Be "e2e-validacion-dedup" -Because $c.caseKey
        }
    }
    It "la siembra fuerza reproceso: sin eso no hay ejecucion original que reutilizar" {
        foreach ($c in $script:cases) {
            $c.seed | Should -Not -BeNullOrEmpty -Because $c.caseKey
            $c.seed.request | Should -Not -BeNullOrEmpty -Because $c.caseKey
            $c.seed.request.forceReprocess | Should -BeTrue -Because $c.caseKey
        }
    }
    It "ninguna peticion salta la comprobacion de duplicados" {
        # skipDuplicateCheck = true desactiva justo el mecanismo que este juego
        # demuestra: un caso con esa opcion pasaria en verde sin probar nada.
        foreach ($c in $script:cases) {
            $peticiones = @($c.seed.request) + @($c.pasadas | ForEach-Object { $_.request })
            foreach ($r in ($peticiones | Where-Object { $null -ne $_ })) {
                $r.skipDuplicateCheck | Should -BeFalse -Because $c.caseKey
            }
        }
    }
    It "toda pasada tiene nombre, request y assertions con expectedRuntimeStatus" {
        foreach ($c in $script:cases) {
            foreach ($p in @($c.pasadas)) {
                $p.nombre     | Should -Not -BeNullOrEmpty -Because $c.caseKey
                $p.request    | Should -Not -BeNullOrEmpty -Because "$($c.caseKey)/$($p.nombre)"
                $p.assertions | Should -Not -BeNullOrEmpty -Because "$($c.caseKey)/$($p.nombre)"
                $p.assertions.expectedRuntimeStatus | Should -Be "Completed" -Because "$($c.caseKey)/$($p.nombre)"
            }
        }
    }
    It "dbAssertions usan solo columnas de la instantanea de ejecuciones" {
        foreach ($c in $script:cases) {
            @($c.dbAssertions).Count | Should -BeGreaterThan 0 -Because "$($c.caseKey) no asevera nada sobre las filas"
            foreach ($regla in @($c.dbAssertions)) {
                if ($null -eq $regla) { continue }
                $script:ColumnasSnapshot | Should -Contain $regla.columna -Because "$($c.caseKey) dbAssertions referencia $($regla.columna)"
            }
        }
    }
    It "apiAssertions solo en los casos de tipo agregados, y con metricas conocidas" {
        foreach ($c in $script:cases) {
            $tieneApi = ($c.PSObject.Properties['apiAssertions'] -and @($c.apiAssertions).Count -gt 0)
            $tipo = if ($c.PSObject.Properties['tipo']) { $c.tipo } else { $null }
            if (-not $tieneApi) { continue }
            $tipo | Should -Be "agregados" -Because "$($c.caseKey) declara apiAssertions sin ser de tipo agregados: el runner no las evaluaria"
            foreach ($regla in @($c.apiAssertions)) {
                $script:MetricasMonitor | Should -Contain $regla.metrica -Because "$($c.caseKey) apiAssertions referencia $($regla.metrica)"
                $regla.PSObject.Properties['delta'] | Should -Not -BeNullOrEmpty -Because "$($c.caseKey)/$($regla.metrica) sin delta esperado"
            }
        }
    }
    It "todo caso de tipo agregados declara apiAssertions" {
        foreach ($c in ($script:cases | Where-Object { $_.PSObject.Properties['tipo'] -and $_.tipo -eq "agregados" })) {
            @($c.apiAssertions).Count | Should -BeGreaterThan 0 -Because "$($c.caseKey): un caso de agregados sin deltas no prueba nada"
        }
    }
    It "todo id DUP de la matriz tiene al menos un caso" {
        $idsDup    = @($script:matrix | Where-Object { $_.area -eq "Dedup" } | ForEach-Object { $_.id })
        $cubiertos = @($script:cases | ForEach-Object { @($_.covers) } | Sort-Object -Unique)
        foreach ($id in $idsDup) {
            $cubiertos | Should -Contain $id -Because "DUP sin caso: $id"
        }
    }
}
