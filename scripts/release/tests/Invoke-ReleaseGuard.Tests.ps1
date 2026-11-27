<#
  Tests de scripts/release/Invoke-ReleaseGuard.ps1 (stage Guard de la pipeline Release).

  Usa un repo de prueba en $TestDrive (copia de releases/ con ficheros stub), siempre con
  -SinPermisos, y mockea Get-ReleaseEstado: no toca red ni BD.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/Invoke-ReleaseGuard.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:guard = Join-Path $PSScriptRoot '..' 'Invoke-ReleaseGuard.ps1'
    $script:repo = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')).Path
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseRegistro.psm1') -Force -Global

    function New-TestFile([string]$Root, [string]$Rel, [string]$Content) {
        $p = Join-Path $Root $Rel
        New-Item -ItemType Directory -Force -Path (Split-Path $p) | Out-Null
        [System.IO.File]::WriteAllText($p, $Content)
    }

    $script:fixtures = Join-Path $TestDrive 'fixtures'
    $stubPs1 = 'param([string]$Environment, [switch]$DryRun)'
    New-TestFile $script:fixtures 'scripts/migrations/gpt56-luna/aplicar-luna.ps1' $stubPs1
    New-TestFile $script:fixtures 'scripts/storage/copy-embeddings-artifacts.ps1' $stubPs1
    New-TestFile $script:fixtures 'scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql' 'SELECT 1;'
    New-TestFile $script:fixtures 'releases/_ensayo/ensayo-dev.sql' 'SELECT 1;'
    New-TestFile $script:fixtures 'releases/_ensayo/ensayo-dev.ps1' $stubPs1
    New-TestFile $script:fixtures 'releases/CURRENT' "v1.2.0`n"
    New-Item -ItemType Directory -Force -Path (Join-Path $script:fixtures 'releases/v1.2.0') | Out-Null
    Copy-Item (Join-Path $script:repo 'releases/v1.2.0/release.json') (Join-Path $script:fixtures 'releases/v1.2.0/release.json') -Force
    Copy-Item (Join-Path $script:repo 'releases/_ensayo/release.json') (Join-Path $script:fixtures 'releases/_ensayo/release.json') -Force

    $script:aprobOk = Join-Path $TestDrive 'aprobadores-ok.yml'
    $script:aprobVacio = Join-Path $TestDrive 'aprobadores-vacio.yml'
    $script:aprobSin = Join-Path $TestDrive 'aprobadores-sin.yml'
    $yml = "variables:`n  - name: aprobador1`n    value: a@sareb.es`n  - name: aprobador2`n    value: {0}`n"
    [System.IO.File]::WriteAllText($script:aprobOk, ($yml -f 'b@sareb.es'))
    [System.IO.File]::WriteAllText($script:aprobVacio, ($yml -f "''"))
    [System.IO.File]::WriteAllText($script:aprobSin, "variables:`n  - name: aprobador1`n    value: a@sareb.es`n")

    function Invoke-Guard([string]$Env, [string]$Branch, [string]$Aprob = $script:aprobOk, [string]$Simular = '') {
        $script:out = & $script:guard -Environment $Env -SourceBranch $Branch -RepoRoot $script:fixtures `
            -SqlServer srbsqltest -AprobadoresPath $Aprob -SimularFalloEn $Simular -SinPermisos 6>&1 | Out-String
        $script:code = $LASTEXITCODE
    }
}

Describe 'Invoke-ReleaseGuard' {
    BeforeEach {
        Mock Get-ReleaseEstado { [pscustomobject]@{ Estado = 'SinAplicar'; PasosAplicados = @() } }
    }

    It 'rechaza rama distinta de la esperada por entorno' {
        Invoke-Guard 'pre' 'refs/heads/master'
        $script:code | Should -Be 2
        $script:out | Should -Match 'task\.logissue type=error'
    }
    It 'acepta cualquier rama en dev y fija ignorarRegistro=true' {
        Invoke-Guard 'dev' 'refs/heads/feature/x'
        $script:code | Should -Be 0
        $script:out | Should -Match 'variable=ignorarRegistro;isOutput=true\]true'
        $script:out | Should -Match 'variable=version;isOutput=true\]_ensayo'
    }
    It 'rechaza SimularFalloEn fuera de dev' {
        Invoke-Guard 'pre' 'refs/heads/develop' $script:aprobOk 'Codigo'
        $script:code | Should -Be 2
    }
    It 'rechaza aprobadores vacios' {
        Invoke-Guard 'pre' 'refs/heads/develop' $script:aprobVacio
        $script:code | Should -Be 2
        $script:out | Should -Match 'aprobador2'
    }
    It 'rechaza aprobadores ausentes' {
        Invoke-Guard 'pre' 'refs/heads/develop' $script:aprobSin
        $script:code | Should -Be 2
    }
    It 'marca yaAplicada cuando el registro dice Cerrada' {
        Mock Get-ReleaseEstado { [pscustomobject]@{ Estado = 'Cerrada'; PasosAplicados = @() } }
        Invoke-Guard 'pre' 'refs/heads/develop'
        $script:code | Should -Be 0
        $script:out | Should -Match 'variable=yaAplicada;isOutput=true\]true'
    }
    It 'emite version y manifestPath' {
        Invoke-Guard 'pre' 'refs/heads/develop'
        $script:code | Should -Be 0
        $script:out | Should -Match 'variable=version;isOutput=true\]v1\.2\.0'
        $script:out | Should -Match 'variable=manifestPath;isOutput=true\]'
        $script:out | Should -Match 'variable=yaAplicada;isOutput=true\]false'
        $script:out | Should -Match 'variable=ignorarRegistro;isOutput=true\]false'
    }
    It 'acumula todos los fallos en una sola ejecucion' {
        Invoke-Guard 'pre' 'refs/heads/master' $script:aprobVacio 'Codigo'
        $script:code | Should -Be 2
        ([regex]::Matches($script:out, 'task\.logissue type=error')).Count | Should -BeGreaterOrEqual 3
    }

    Context 'comprobacion de permisos (sin -SinPermisos)' {
        BeforeAll {
            function New-PermisosStub([string]$Name, [string]$Body) {
                $f = Join-Path $TestDrive $Name
                [System.IO.File]::WriteAllText($f, 'param([string]$Environment, [string]$SqlServer, [string]$SqlDatabase)' + "`n" + $Body)
                return $f
            }
            function Invoke-GuardPermisos([string]$Stub) {
                $script:out = & $script:guard -Environment pre -SourceBranch refs/heads/develop -RepoRoot $script:fixtures `
                    -SqlServer srbsqltest -AprobadoresPath $script:aprobOk -PermisosScriptPath $Stub 6>&1 | Out-String
                $script:code = $LASTEXITCODE
            }
        }
        It 'falla si el script de permisos no existe' {
            Invoke-GuardPermisos (Join-Path $TestDrive 'no-existe.ps1')
            $script:code | Should -Be 2
            $script:out | Should -Match 'No existe el script de permisos'
        }
        It 'falla si el script de permisos lanza una excepcion' {
            Invoke-GuardPermisos (New-PermisosStub 'permisos-crash.ps1' 'throw "boom"')
            $script:code | Should -Be 2
            $script:out | Should -Match 'boom'
        }
        It 'falla con exit 2 y un logissue por linea cuando el script de permisos devuelve 2' {
            Invoke-GuardPermisos (New-PermisosStub 'permisos-2.ps1' 'Write-Host "recursoA - rol - ref"; Write-Host "recursoB - rol - ref"; exit 2')
            $script:code | Should -Be 2
            $script:out | Should -Match 'logissue type=error\]recursoA'
            $script:out | Should -Match 'logissue type=error\]recursoB'
        }
        It 'pasa con exit 0 cuando el script de permisos devuelve 0' {
            Invoke-GuardPermisos (New-PermisosStub 'permisos-0.ps1' 'exit 0')
            $script:code | Should -Be 0
            $script:out | Should -Match 'variable=version;isOutput=true\]v1\.2\.0'
        }
    }

    Context 'switches -SinKudu y -SoloKudu' {
        BeforeAll {
            $script:stubArgs = Join-Path $TestDrive 'permisos-args.ps1'
            [System.IO.File]::WriteAllText($script:stubArgs, 'param([string]$Environment, [string]$SqlServer, [string]$SqlDatabase, [switch]$SinKudu, [switch]$SoloKudu)' + "`n" + '$global:StubArgs = "ARGS sin=$($SinKudu.IsPresent) solo=$($SoloKudu.IsPresent)"; exit 0')
        }
        It 'sin switches pasa SinKudu=False y SoloKudu=False al script de permisos' {
            $global:StubArgs = $null
            $o = & $script:guard -Environment pre -SourceBranch refs/heads/develop -RepoRoot $script:fixtures -SqlServer srbsqltest -AprobadoresPath $script:aprobOk -PermisosScriptPath $script:stubArgs 6>&1 | Out-String
            $global:StubArgs | Should -Be 'ARGS sin=False solo=False'
        }
        It '-SinKudu se pasa al script de permisos y se publican las salidas' {
            $global:StubArgs = $null
            $o = & $script:guard -Environment pre -SourceBranch refs/heads/develop -RepoRoot $script:fixtures -SqlServer srbsqltest -AprobadoresPath $script:aprobOk -PermisosScriptPath $script:stubArgs -SinKudu 6>&1 | Out-String
            $LASTEXITCODE | Should -Be 0
            $global:StubArgs | Should -Be 'ARGS sin=True solo=False'
            $o | Should -Match 'variable=version;isOutput=true\]v1\.2\.0'
        }
        It '-SoloKudu solo ejecuta el script de permisos con -SoloKudu, sin manifiesto ni salidas' {
            $global:StubArgs = $null
            $o = & $script:guard -Environment pre -SourceBranch refs/heads/otra -RepoRoot $script:fixtures -SqlServer srbsqltest -AprobadoresPath (Join-Path $TestDrive 'no-existe.yml') -PermisosScriptPath $script:stubArgs -SoloKudu 6>&1 | Out-String
            $LASTEXITCODE | Should -Be 0
            $global:StubArgs | Should -Be 'ARGS sin=False solo=True'
            $o | Should -Not -Match 'isOutput=true'
            $o | Should -Not -Match 'no valida'
        }
        It '-SoloKudu sale con 2 si el script de permisos devuelve 2' {
            $f = Join-Path $TestDrive 'permisos-solo-2.ps1'
            [System.IO.File]::WriteAllText($f, 'param([string]$Environment, [string]$SqlServer, [string]$SqlDatabase, [switch]$SinKudu, [switch]$SoloKudu)' + "`n" + 'Write-Host "Kudu x: NODATA"; exit 2')
            $o = & $script:guard -Environment pre -SourceBranch refs/heads/develop -RepoRoot $script:fixtures -SqlServer srbsqltest -AprobadoresPath $script:aprobOk -PermisosScriptPath $f -SoloKudu 6>&1 | Out-String
            $LASTEXITCODE | Should -Be 2
            $o | Should -Match 'logissue type=error\]Kudu x: NODATA'
        }
        It '-SinKudu y -SoloKudu juntos salen con 2' {
            $o = & $script:guard -Environment pre -SourceBranch refs/heads/develop -RepoRoot $script:fixtures -SqlServer srbsqltest -AprobadoresPath $script:aprobOk -PermisosScriptPath $script:stubArgs -SinKudu -SoloKudu 6>&1 | Out-String
            $LASTEXITCODE | Should -Be 2
            $o | Should -Match 'incompatibles'
        }
    }
}
