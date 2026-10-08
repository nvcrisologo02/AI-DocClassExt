BeforeAll {
    $script:sut = Join-Path $PSScriptRoot '..' 'verificar-rutas-doc.ps1'
    $script:tmp = Join-Path ([IO.Path]::GetTempPath()) ("vrd-" + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $script:tmp | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $script:tmp 'scripts/ai') | Out-Null
    Set-Content (Join-Path $script:tmp 'scripts/ai/existe.ps1') 'x'
}
AfterAll { Remove-Item $script:tmp -Recurse -Force }

Describe 'verificar-rutas-doc' {
    It 'devuelve 0 cuando todas las rutas existen' {
        $md = Join-Path $script:tmp 'ok.md'
        Set-Content $md 'Usar `scripts/ai/existe.ps1` y ver [doc](scripts/ai/existe.ps1). Plantilla `docs/releases/vX.Y.Z/release.md` se ignora.'
        $out = & pwsh -NoProfile -File $script:sut -Path $md -RepoRoot $script:tmp
        $LASTEXITCODE | Should -Be 0
        ($out -join "`n") | Should -Match 'OK: 1 rutas comprobadas'
    }
    It 'devuelve 1 y lista la ruta cuando falta alguna' {
        $md = Join-Path $script:tmp 'ko.md'
        Set-Content $md 'Usar `scripts/ai/no-existe.ps1`.'
        $out = & pwsh -NoProfile -File $script:sut -Path $md -RepoRoot $script:tmp
        $LASTEXITCODE | Should -Be 1
        ($out -join "`n") | Should -Match 'NO EXISTE: scripts/ai/no-existe.ps1'
    }
}
