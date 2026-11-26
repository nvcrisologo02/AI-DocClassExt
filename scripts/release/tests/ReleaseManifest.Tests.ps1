<#
  Tests de scripts/release/Get-ReleaseManifest.ps1 y Test-ReleaseManifest.ps1
  (convencion releases/<version>/release.json que lee la pipeline Release).

  Cubre: lectura de CURRENT (con CRLF y espacios), seleccion de _ensayo en dev,
  filtrado de pasos por entorno y las reglas de validacion del manifiesto
  (ficheros existentes, tipo, entornos, _ensayo solo en dev, version coherente).
  Usa ficheros reales en $TestDrive; no toca red ni BD.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/ReleaseManifest.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:get = Join-Path $PSScriptRoot '..' 'Get-ReleaseManifest.ps1'
    $script:test = Join-Path $PSScriptRoot '..' 'Test-ReleaseManifest.ps1'
    $script:repo = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')).Path

    function New-TestFile([string]$Root, [string]$Rel, [string]$Content) {
        $p = Join-Path $Root $Rel
        New-Item -ItemType Directory -Force -Path (Split-Path $p) | Out-Null
        [System.IO.File]::WriteAllText($p, $Content)
    }

    # Copia minima de releases/ con los ficheros que referencia el manifiesto.
    $script:fixtures = Join-Path $TestDrive 'fixtures'
    $stubPs1 = 'param([string]$Environment, [switch]$DryRun)'
    New-TestFile $fixtures 'scripts/migrations/gpt56-luna/aplicar-luna.ps1' $stubPs1
    New-TestFile $fixtures 'scripts/storage/copy-embeddings-artifacts.ps1' $stubPs1
    New-TestFile $fixtures 'scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql' 'SELECT 1;'
    New-TestFile $fixtures 'releases/_ensayo/ensayo-dev.sql' 'SELECT 1;'
    New-TestFile $fixtures 'releases/CURRENT' "v1.2.0 `r`n"
    New-Item -ItemType Directory -Force -Path (Join-Path $fixtures 'releases/v1.2.0') | Out-Null
    Copy-Item (Join-Path $script:repo 'releases/v1.2.0/release.json') (Join-Path $fixtures 'releases/v1.2.0/release.json') -Force
    Copy-Item (Join-Path $script:repo 'releases/_ensayo/release.json') (Join-Path $fixtures 'releases/_ensayo/release.json') -Force

    # Valida un manifiesto temporal dentro de $fixtures y devuelve el codigo de salida.
    function Invoke-ValidacionTemporal([string]$Carpeta, [string]$Json) {
        New-TestFile $script:fixtures "releases/$Carpeta/release.json" $Json
        $null = & $script:test -ManifestPath (Join-Path $script:fixtures "releases/$Carpeta/release.json") -RepoRoot $script:fixtures 6>&1
        return $LASTEXITCODE
    }
}

Describe 'Get-ReleaseManifest' {
    It 'lee la version de CURRENT y devuelve el manifiesto de esa version para pre' {
        $m = & $script:get -Environment pre -RepoRoot $script:fixtures
        $m.version | Should -Be 'v1.2.0'
        $m.pasos.Count | Should -Be 3
        $m.pasos[0].tipo | Should -Be 'ps1'
        $m.pasos[0].ruta | Should -Be 'scripts/migrations/gpt56-luna/aplicar-luna.ps1'
        $m.pasos[0].argumentos | Should -Be '-Environment <env>'
        $m.embeddings.version | Should -Be 'v5'
        $m.embeddings.modo | Should -Be 'hibrido'
    }
    It 'tolera CRLF y espacios en CURRENT' {
        New-TestFile $script:fixtures 'releases/CURRENT' "  v1.2.0  `r`n"
        (& $script:get -Environment prod -RepoRoot $script:fixtures).version | Should -Be 'v1.2.0'
        New-TestFile $script:fixtures 'releases/CURRENT' "v1.2.0`n"
        (& $script:get -Environment prod -RepoRoot $script:fixtures).version | Should -Be 'v1.2.0'
    }
    It 'selecciona _ensayo en dev aunque CURRENT apunte a v1.2.0' {
        $m = & $script:get -Environment dev -RepoRoot $script:fixtures
        $m.manifestPath | Should -Match '_ensayo'
        $m.version | Should -Be '_ensayo'
        $m.embeddings | Should -BeNullOrEmpty
        $m.pasos.Count | Should -Be 1
    }
    It 'filtra los pasos por entorno' {
        $sql07 = 'scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql'
        $json = '{"version":"v9.8.0","pasos":[{"tipo":"sql","ruta":"releases/_ensayo/ensayo-dev.sql","entornos":["pre"]},{"tipo":"sql","ruta":"' + $sql07 + '","entornos":["prod"]}]}'
        New-TestFile $script:fixtures 'releases/v9.8.0/release.json' $json
        New-TestFile $script:fixtures 'releases/CURRENT' 'v9.8.0'
        $pre = & $script:get -Environment pre -RepoRoot $script:fixtures
        $prod = & $script:get -Environment prod -RepoRoot $script:fixtures
        New-TestFile $script:fixtures 'releases/CURRENT' "v1.2.0`r`n"
        $pre.pasos.Count | Should -Be 1
        $pre.pasos[0].ruta | Should -Be 'releases/_ensayo/ensayo-dev.sql'
        $prod.pasos.Count | Should -Be 1
        $prod.pasos[0].ruta | Should -Be $sql07
    }
    It 'rechaza CURRENT que no sea vX.Y.Z' {
        foreach ($valor in '_ensayo', '..\x', 'v1.2') {
            New-TestFile $script:fixtures 'releases/CURRENT' $valor
            { & $script:get -Environment pre -RepoRoot $script:fixtures } | Should -Throw '*CURRENT debe ser una version vX.Y.Z*'
        }
        New-TestFile $script:fixtures 'releases/CURRENT' "v1.2.0`r`n"
    }
}

Describe 'Test-ReleaseManifest' {
    It 'acepta el manifiesto de la v1.2.0 del repo' {
        & $script:test -ManifestPath "$script:repo/releases/v1.2.0/release.json" -RepoRoot $script:repo 6>&1 | Out-Null
        $LASTEXITCODE | Should -Be 0
    }
    It 'rechaza paso con fichero inexistente' {
        $json = '{"version":"v9.9.9","pasos":[{"tipo":"sql","ruta":"scripts/no-existe.sql","entornos":["pre"]}]}'
        Invoke-ValidacionTemporal 'v9.9.9' $json | Should -Be 2
    }
    It 'rechaza _ensayo fuera de dev' {
        $json = '{"version":"_ensayo","pasos":[{"tipo":"sql","ruta":"releases/_ensayo/ensayo-dev.sql","entornos":["pre"]}]}'
        Invoke-ValidacionTemporal '_ensayo' $json | Should -Be 2
        New-TestFile $script:fixtures 'releases/_ensayo/release.json' (Get-Content (Join-Path $script:repo 'releases/_ensayo/release.json') -Raw)
    }
    It 'rechaza tipo distinto de sql o ps1 y entornos vacios' {
        $tipo = '{"version":"v9.9.8","pasos":[{"tipo":"bat","ruta":"releases/_ensayo/ensayo-dev.sql","entornos":["pre"]}]}'
        Invoke-ValidacionTemporal 'v9.9.8' $tipo | Should -Be 2
        $vacios = '{"version":"v9.9.7","pasos":[{"tipo":"sql","ruta":"releases/_ensayo/ensayo-dev.sql","entornos":[]}]}'
        Invoke-ValidacionTemporal 'v9.9.7' $vacios | Should -Be 2
    }
}
