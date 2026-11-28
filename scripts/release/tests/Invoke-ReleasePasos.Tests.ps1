<#
  Tests de scripts/release/Invoke-ReleasePasos.ps1 (pasos del manifiesto de la stage Configuracion).

  Usa un repo de prueba en $TestDrive con scripts .ps1 falsos que escriben sus argumentos a un
  fichero de log (variable T8_LOG) y respetan -DryRun; mockea Get-ReleaseEstado y
  Register-ReleasePaso: no toca ningun entorno.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/Invoke-ReleasePasos.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:pasos = Join-Path $PSScriptRoot '..' 'Invoke-ReleasePasos.ps1'
    $script:repo = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')).Path
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseRegistro.psm1') -Force -Global

    function New-TestFile($Root, $Rel, $Content) {
        $p = Join-Path $Root $Rel
        New-Item -ItemType Directory -Force -Path (Split-Path $p -Parent) | Out-Null
        [System.IO.File]::WriteAllText($p, $Content)
    }

    $script:fixtures = Join-Path $TestDrive 'fixtures'
    # Script falso: registra sus argumentos y falla segun variables de entorno.
    $fake = @'
param([string]$Environment, [string]$Version, [string]$TargetEnvironment, [string]$SourceEnvironment, [switch]$DryRun)
Add-Content -LiteralPath $env:T8_LOG -Value ("{0} Environment={1} Version={2} Target={3} Source={4} DryRun={5}" -f (Split-Path $PSCommandPath -Leaf), $Environment, $Version, $TargetEnvironment, $SourceEnvironment, $DryRun.IsPresent)
if ($env:T8_FALLA_DRY -and $DryRun) { Write-Output 'fallo del ensayo'; exit 5 }
if ($env:T8_FALLA_REAL -and -not $DryRun) { exit 6 }
'@
    New-TestFile $script:fixtures 'scripts/migrations/gpt56-luna/aplicar-luna.ps1' $fake
    New-TestFile $script:fixtures 'scripts/storage/copy-embeddings-artifacts.ps1' $fake
    New-TestFile $script:fixtures 'releases/_ensayo/ensayo-dev.ps1' $fake
    New-TestFile $script:fixtures 'releases/_ensayo/ensayo-dev.sql' 'SELECT 1; -- ensayo'
    $plantilla = "DECLARE @Modo NVARCHAR(10) = N'off';`nDECLARE @Version NVARCHAR(10) = N'v0';`nSELECT 1;`n"
    New-TestFile $script:fixtures 'scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql' $plantilla
    New-TestFile $script:fixtures 'releases/CURRENT' "v1.2.0`n"
    New-Item -ItemType Directory -Force -Path (Join-Path $script:fixtures 'releases/v1.2.0') | Out-Null
    Copy-Item (Join-Path $script:repo 'releases/v1.2.0/release.json') (Join-Path $script:fixtures 'releases/v1.2.0/release.json') -Force
    Copy-Item (Join-Path $script:repo 'releases/_ensayo/release.json') (Join-Path $script:fixtures 'releases/_ensayo/release.json') -Force
    # apply-sql-dev falso: registra los argumentos y el SQL recibido.
    $fakeSql = @'
param([Parameter(Mandatory)][string]$ScriptPath, [string]$Server, [string]$Database, [switch]$PermitirNoDev)
Add-Content -LiteralPath $env:T8_LOG -Value ("APPLY ScriptPath={0} Server={1} Database={2} PermitirNoDev={3}" -f $ScriptPath, $Server, $Database, $PermitirNoDev.IsPresent)
Add-Content -LiteralPath $env:T8_LOG -Value ("SQL " + ([System.IO.File]::ReadAllText($ScriptPath) -replace '\s+', ' '))
if ($env:T8_FALLA_SQL) { throw 'fallo sql' }
'@
    New-TestFile $script:fixtures 'scripts/database/apply-sql-dev.ps1' $fakeSql
}

Describe 'Invoke-ReleasePasos' {
    BeforeEach {
        $script:log = Join-Path $TestDrive ("log-{0}.txt" -f [guid]::NewGuid())
        $script:out = Join-Path $TestDrive ("out-{0}" -f [guid]::NewGuid())
        $env:T8_LOG = $script:log
        Remove-Item Env:T8_FALLA_DRY, Env:T8_FALLA_REAL, Env:T8_FALLA_SQL -ErrorAction SilentlyContinue
        $global:LASTEXITCODE = 0
        Mock Get-ReleaseEstado { [pscustomobject]@{ Estado = 'SinAplicar'; PasosAplicados = [object[]]@() } }
        Mock Register-ReleasePaso { }
    }
    AfterAll { Remove-Item Env:T8_LOG, Env:T8_FALLA_DRY, Env:T8_FALLA_REAL, Env:T8_FALLA_SQL -ErrorAction SilentlyContinue }

    BeforeAll {
        function Invoke-Pasos([string]$Env = 'pre', [switch]$Ignorar) {
            $texto = & $script:pasos -Environment $Env -RepoRoot $script:fixtures -SqlServer srvtest -SqlDatabase dbtest `
                -BuildId 'b1' -OutDir $script:out -IgnorarRegistro:$Ignorar 6>&1 | Out-String
            $script:exit = $LASTEXITCODE
            return $texto
        }
        function Get-Log { if (Test-Path $script:log) { @(Get-Content $script:log) } else { @() } }
    }

    It 'ejecuta DryRun antes de lo real y pasa los argumentos resueltos' {
        $null = Invoke-Pasos -Env 'dev'
        $script:exit | Should -Be 0
        $ll = @(Get-Log | Where-Object { $_ -like 'ensayo-dev.ps1*' })
        $ll.Count | Should -Be 2
        $ll[0] | Should -Match 'Environment=dev .*DryRun=True'
        $ll[1] | Should -Match 'Environment=dev .*DryRun=False'
    }

    It 'no ejecuta lo real si el DryRun falla y sale con 2' {
        $env:T8_FALLA_DRY = '1'
        $null = Invoke-Pasos -Env 'dev'
        $script:exit | Should -Be 2
        @(Get-Log | Where-Object { $_ -like 'ensayo-dev.ps1*' }).Count | Should -Be 1
        Should -Invoke Register-ReleasePaso -Times 1 -Exactly   # solo el sql previo
    }

    It 'sale con 2 si falla la ejecucion real y no registra ese paso' {
        $env:T8_FALLA_REAL = '1'
        $null = Invoke-Pasos -Env 'dev'
        $script:exit | Should -Be 2
        Should -Invoke Register-ReleasePaso -Times 1 -Exactly   # solo el sql previo
    }

    It 'genera el 07 con los DECLARE del manifiesto y lo aplica con -PermitirNoDev' {
        $null = Invoke-Pasos -Env 'pre'
        $script:exit | Should -Be 0
        $apply = Get-Log | Where-Object { $_ -like 'APPLY*generado*' }
        $apply | Should -Not -BeNullOrEmpty
        $apply | Should -Match 'PermitirNoDev=True'
        $apply | Should -Match 'Server=srvtest\.database\.windows\.net'
        $apply | Should -Match 'Database=dbtest'
        $sql = Get-Log | Where-Object { $_ -like 'SQL*N''v5''*' }
        $sql | Should -Match "N'hibrido'"
        Test-Path (Join-Path $script:out 'generado/07-modeloconfig-embeddings-modo-version.sql') | Should -BeTrue
    }

    It 'sale con 2 y no registra si falla el apply SQL' {
        $env:T8_FALLA_SQL = '1'
        $null = Invoke-Pasos -Env 'dev'
        $script:exit | Should -Be 2
        Should -Invoke Register-ReleasePaso -Times 0 -Exactly
    }

    It 'salta un paso ya registrado con el mismo hash' {
        $rutaSql = 'releases/_ensayo/ensayo-dev.sql'
        $hash = Get-ReleaseContentHash -Content ([System.IO.File]::ReadAllText((Join-Path $script:fixtures $rutaSql)) -replace "`r`n", "`n")
        Mock Get-ReleaseEstado {
            [pscustomobject]@{ Estado = 'Parcial'; PasosAplicados = [object[]]@([pscustomobject]@{ Paso = $rutaSql; HashContenido = $hash; FechaUtc = [datetime]'2026-11-27T10:00:00Z' }) }
        }
        $texto = Invoke-Pasos -Env 'dev'
        $script:exit | Should -Be 0
        $texto | Should -Match 'SALTADO \(ya aplicado en'
        @(Get-Log | Where-Object { $_ -like 'APPLY*' }).Count | Should -Be 0
        $res = @(Get-Content (Join-Path $script:out 'pasos.json') -Raw | ConvertFrom-Json)
        $res[0].resultado | Should -Be 'Saltado'
        $res[1].resultado | Should -Be 'Aplicado'
    }

    It 'vuelve a aplicar si el hash cambio' {
        $rutaSql = 'releases/_ensayo/ensayo-dev.sql'
        Mock Get-ReleaseEstado {
            [pscustomobject]@{ Estado = 'Parcial'; PasosAplicados = [object[]]@([pscustomobject]@{ Paso = $rutaSql; HashContenido = 'otro'; FechaUtc = [datetime]'2026-11-27T10:00:00Z' }) }
        }
        $texto = Invoke-Pasos -Env 'dev'
        $script:exit | Should -Be 0
        $texto | Should -Not -Match 'SALTADO'
        @(Get-Log | Where-Object { $_ -like 'APPLY*' }).Count | Should -Be 1
    }

    It 'registra cada paso aplicado con el hash del contenido efectivo' {
        $null = Invoke-Pasos -Env 'dev'
        Should -Invoke Register-ReleasePaso -Times 2 -Exactly
        Should -Invoke Register-ReleasePaso -Times 1 -Exactly -ParameterFilter {
            $Paso -eq 'releases/_ensayo/ensayo-dev.sql' -and $HashContenido -match '^[0-9a-f]{64}$' -and $Version -eq '_ensayo' -and $BuildId -eq 'b1'
        }
    }

    It 'escribe la salida de cada paso numerada y el resumen pasos.json' {
        $null = Invoke-Pasos -Env 'dev'
        Test-Path (Join-Path $script:out 'paso-1-ensayo-dev.txt') | Should -BeTrue
        Test-Path (Join-Path $script:out 'paso-2-ensayo-dev.txt') | Should -BeTrue
        $res = @(Get-Content (Join-Path $script:out 'pasos.json') -Raw | ConvertFrom-Json)
        $res.Count | Should -Be 2
        $res[0].ruta | Should -Be 'releases/_ensayo/ensayo-dev.sql'
        $res[0].hash | Should -Match '^[0-9a-f]{64}$'
    }

    It 'sale con 2 si no puede escribir en OutDir' {
        $bloqueo = Join-Path $TestDrive 'es-un-fichero'
        [System.IO.File]::WriteAllText($bloqueo, 'x')
        $script:out = Join-Path $bloqueo 'sub'
        $null = Invoke-Pasos -Env 'dev'
        $script:exit | Should -Be 2
    }

    It 'sale con 2 si el registro falla' {
        Mock Register-ReleasePaso { throw 'sin BD' }
        $null = Invoke-Pasos -Env 'dev'
        $script:exit | Should -Be 2
    }
}
