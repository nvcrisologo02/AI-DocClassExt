<#
  Tests de scripts/release/Invoke-ReleaseSmoke.ps1.

  Usa un repo falso en $TestDrive (-RepoRoot) con un runner y un publicador falsos que escriben
  report.md ficticios y registran sus llamadas en un log (variable T9_LOG). Mockea Invoke-ReleaseAz.
  No toca el environments.json real, ni Azure, ni Azure DevOps.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/Invoke-ReleaseSmoke.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:smoke = Join-Path $PSScriptRoot '..' 'Invoke-ReleaseSmoke.ps1'
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
    $script:claveFalsa = 'CLAVE-FALSA-XYZ-123'

    $runnerFalso = @'
param([string]$Environment, [string]$Profile, [string]$CasesDir = '')
$existe = Test-Path -LiteralPath (Join-Path $PSScriptRoot 'config' 'environments.json')
$pasada = if ($CasesDir) { 'segunda' } else { 'primera' }
Add-Content -LiteralPath $env:T9_LOG -Value ("RUN {0} env={1} profile={2} cases={3} envjson={4}" -f $pasada, $Environment, $Profile, [bool]$CasesDir, $existe)
$arts = Join-Path $PSScriptRoot 'artifacts'
$n = @(Get-ChildItem -LiteralPath $arts -Directory -ErrorAction SilentlyContinue).Count + 1
$dir = Join-Path $arts ('{0:D3}-{1}-{2}' -f $n, $Environment, $Profile)
New-Item -ItemType Directory -Force -Path $dir | Out-Null
# Copia de environments.json en el informe: el script nunca debe llevarla a OutDir.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'config' 'environments.json') -Destination (Join-Path $dir 'environments.json')
$intentoPrimera = @(Get-Content -LiteralPath $env:T9_LOG | Where-Object { $_ -like 'RUN primera*' }).Count
$escenario = $env:T9_ESCENARIO
$filas = @()
if ($CasesDir) {
    $filas = @('| SMK-X | extra | PASS | ok | 1 |  |', '| SMK-Y | extra | PASS | ok | 1 |  |')
    $codigo = 0
}
else {
    $codigo = 0
    $filas = 1..6 | ForEach-Object { "| C$_ | caso $_ | PASS | ok | 1 |  |" }
    if ($escenario -eq 'error-then-ok' -and $intentoPrimera -eq 1) { $filas[5] = '| C6 | caso 6 | SKIP | ERROR | 1 | ERROR transitorio |'; $codigo = 1 }
    if ($escenario -eq 'error-siempre') { $filas[5] = '| C6 | caso 6 | SKIP | ERROR | 1 | ERROR transitorio |'; $codigo = 1 }
    if ($escenario -eq 'fail') { $filas[5] = '| C6 | caso 6 | FAIL | ERROR | 1 | fallo |'; $codigo = 1 }
    if ($escenario -eq 'config') { $codigo = 2 }
}
$cabecera = @('# Reporte E2E', '', '- Resultado: Total=6 PASS=6 FAIL=0 SKIP=0 N/A=0', '', '## Cobertura funcional', '', '| Area | Item | Estado |', '|---|---|---|', '| a | b | ERROR en cobertura |', '', '## Casos', '', '| Caso | Nombre | Status | Estado negocio | s | Detalle |', '|---|---|---|---|---|---|')
Set-Content -LiteralPath (Join-Path $dir 'report.md') -Value ($cabecera + $filas) -Encoding UTF8
exit $codigo
'@
    $publicadorFalso = @'
param([string]$Environment, [string]$Candidate, [string[]]$ReportDirs, [string]$Version, [string]$Pat, [switch]$WhatIf)
Add-Content -LiteralPath $env:T9_LOG -Value ("PUB whatif={0} pat={1} dirs={2} cand={3} ver={4}" -f $WhatIf.IsPresent, $Pat, $ReportDirs.Count, $Candidate, $Version)
if ($WhatIf) { if ($env:T9_PUB_WHATIF_EXIT) { exit [int]$env:T9_PUB_WHATIF_EXIT }; exit 0 }
if ($env:T9_PUB_EXIT) { exit [int]$env:T9_PUB_EXIT }
Set-Content -LiteralPath (Join-Path $ReportDirs[-1] 'ado-run-77.json') -Value '{"runId":77}'
exit 0
'@
    function global:New-T9Repo {
        param([string]$Raiz)
        $e2e = Join-Path $Raiz 'tests' 'e2e-postdeploy'
        New-Item -ItemType Directory -Force -Path (Join-Path $e2e 'ado'), (Join-Path $e2e 'cases-smk') | Out-Null
        Set-Content -LiteralPath (Join-Path $e2e 'run-e2e-postdeploy.ps1') -Value $script:runnerFalsoTexto
        Set-Content -LiteralPath (Join-Path $e2e 'ado' 'publish-smk-run.ps1') -Value $script:publicadorFalsoTexto
    }
    $script:runnerFalsoTexto = $runnerFalso
    $script:publicadorFalsoTexto = $publicadorFalso
}

AfterAll {
    Remove-Item function:global:New-T9Repo -ErrorAction SilentlyContinue
    Remove-Variable -Name T9AzExit, T9Clave -Scope Global -ErrorAction SilentlyContinue
    foreach ($v in 'T9_LOG', 'T9_ESCENARIO', 'T9_PUB_EXIT', 'T9_PUB_WHATIF_EXIT') { Remove-Item "env:$v" -ErrorAction SilentlyContinue }
}

Describe 'Invoke-ReleaseSmoke' {
    BeforeEach {
        $base = Join-Path $TestDrive ('r-' + [guid]::NewGuid().ToString('N'))
        $script:repo = Join-Path $base 'repo'
        $script:out = Join-Path $base 'out'
        $script:envJson = Join-Path $script:repo 'tests' 'e2e-postdeploy' 'config' 'environments.json'
        New-T9Repo -Raiz $script:repo
        $env:T9_LOG = Join-Path $base 'log.txt'
        Set-Content -LiteralPath $env:T9_LOG -Value ''
        $env:T9_ESCENARIO = 'ok'
        Remove-Item env:T9_PUB_EXIT, env:T9_PUB_WHATIF_EXIT -ErrorAction SilentlyContinue
        $global:T9AzExit = 0
        $global:T9Clave = $script:claveFalsa
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = $global:T9AzExit; $global:T9Clave }
        function script:Invoke-T9Smoke {
            $null = & $script:smoke -Environment pre -ResourceGroup rg1 -FunctionApp app1 -BaseUrl 'https://app1.example' -RepoRoot $script:repo -Candidate abc1234 -Version v1.2.0 -AdoToken 'TOKEN-ADO-FALSO' -OutDir $script:out 6>&1
        }
        function script:Get-T9Log { @(Get-Content -LiteralPath $env:T9_LOG | Where-Object { $_ }) }
    }

    It 'camino feliz: exit 0, environments.json existe durante el runner y no despues' {
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 0
        $log = Get-T9Log
        ($log | Where-Object { $_ -like 'RUN*' }) | ForEach-Object { $_ | Should -Match 'envjson=True' }
        ($log | Where-Object { $_ -like 'RUN primera*' }) | Should -Match 'env=pre profile=smoke cases=False'
        ($log | Where-Object { $_ -like 'RUN segunda*' }) | Should -Match 'cases=True'
        Test-Path $script:envJson | Should -BeFalse
    }

    It 'el alias de prod es pro' {
        $null = & $script:smoke -Environment prod -ResourceGroup rg1 -FunctionApp app1 -BaseUrl 'https://app1.example' -RepoRoot $script:repo -Candidate abc1234 -Version v1.2.0 -AdoToken 'T' -OutDir $script:out 6>&1
        (Get-T9Log | Where-Object { $_ -like 'RUN primera*' }) | Should -Match 'env=pro '
    }

    It 'reintenta una vez cuando hay ERROR y ningun FAIL' {
        $env:T9_ESCENARIO = 'error-then-ok'
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 0
        @(Get-T9Log | Where-Object { $_ -like 'RUN primera*' }).Count | Should -Be 2
    }

    It 'no reintenta mas de una vez y devuelve 2 si el ERROR persiste' {
        $env:T9_ESCENARIO = 'error-siempre'
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 2
        @(Get-T9Log | Where-Object { $_ -like 'RUN primera*' }).Count | Should -Be 2
    }

    It 'no reintenta con FAIL y devuelve 2 (se publica igualmente el resultado)' {
        $env:T9_ESCENARIO = 'fail'
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 2
        @(Get-T9Log | Where-Object { $_ -like 'RUN primera*' }).Count | Should -Be 1
        @(Get-T9Log | Where-Object { $_ -like 'PUB whatif=False*' }).Count | Should -Be 1
    }

    It 'llama al publicador con -WhatIf antes que sin el, con -Pat del token y los dos directorios' {
        Invoke-T9Smoke
        $pub = @(Get-T9Log | Where-Object { $_ -like 'PUB*' })
        $pub.Count | Should -Be 2
        $pub[0] | Should -Match 'whatif=True pat=TOKEN-ADO-FALSO dirs=2 cand=abc1234 ver=v1.2.0'
        $pub[1] | Should -Match 'whatif=False pat=TOKEN-ADO-FALSO dirs=2'
    }

    It 'si el ensayo -WhatIf falla no publica de verdad y devuelve 2' {
        $env:T9_PUB_WHATIF_EXIT = '2'
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 2
        @(Get-T9Log | Where-Object { $_ -like 'PUB whatif=False*' }).Count | Should -Be 0
        Test-Path $script:envJson | Should -BeFalse
    }

    It 'si la publicacion real falla devuelve 2' {
        $env:T9_PUB_EXIT = '2'
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 2
    }

    It 'la clave y environments.json no aparecen en OutDir; si estan los informes y ado-run' {
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 0
        (Get-ChildItem $script:out -Recurse -File | Select-String -SimpleMatch $script:claveFalsa) | Should -BeNullOrEmpty
        (Get-ChildItem $script:out -Recurse -File | Where-Object Name -like 'environments*') | Should -BeNullOrEmpty
        @(Get-ChildItem $script:out -Directory).Count | Should -Be 2
        (Get-ChildItem $script:out -Recurse -File | Where-Object Name -eq 'ado-run-77.json') | Should -Not -BeNullOrEmpty
        (Get-ChildItem $script:out -Recurse -File | Where-Object Name -eq 'report.md').Count | Should -Be 2
    }

    It 'si environments.json ya existia lo repone intacto' {
        New-Item -ItemType Directory -Force -Path (Split-Path $script:envJson) | Out-Null
        $original = '{"dev":{"baseUrl":"https://x","functionKey":"ORIGINAL"}}'
        [IO.File]::WriteAllText($script:envJson, $original)
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 0
        [IO.File]::ReadAllText($script:envJson) | Should -Be $original
    }

    It 'lee la clave maestra (masterKey), no functionKeys.default' {
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 0
        Should -Invoke Invoke-ReleaseAz -ParameterFilter { $Arguments -contains 'masterKey' -and $Arguments -notcontains 'functionKeys.default' } -Times 1 -Exactly
    }

    It 'sale con 2 si masterKey esta vacia y no lanza nada' {
        $global:T9Clave = ''
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 2
        Get-T9Log | Should -BeNullOrEmpty
        Test-Path $script:envJson | Should -BeFalse
    }

    It 'sale con 2 si az falla al leer la clave' {
        $global:T9AzExit = 1
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 2
        Get-T9Log | Should -BeNullOrEmpty
    }

    It 'si el runner sale con 2 no sigue, devuelve 2 y repone el estado de environments.json' {
        $env:T9_ESCENARIO = 'config'
        Invoke-T9Smoke
        $LASTEXITCODE | Should -Be 2
        @(Get-T9Log | Where-Object { $_ -like 'PUB*' }).Count | Should -Be 0
        Test-Path $script:envJson | Should -BeFalse
    }
}
