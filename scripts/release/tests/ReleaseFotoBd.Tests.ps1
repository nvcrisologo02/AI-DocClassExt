<#
  Tests de scripts/release/New-ReleaseFotoBd.ps1, Restore-ReleaseFotoBd.ps1 y New-ReleaseRestoreSql.

  Mockean Invoke-ReleaseSql e Invoke-ReleaseSqlTransaccion: no tocan ninguna base de datos.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/ReleaseFotoBd.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:foto = Join-Path $PSScriptRoot '..' 'New-ReleaseFotoBd.ps1'
    $script:restaura = Join-Path $PSScriptRoot '..' 'Restore-ReleaseFotoBd.ps1'
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
}

Describe 'New-ReleaseRestoreSql' {
    It 'genera UPDATE, INSERT con identity y DELETE' {
        $s = New-ReleaseRestoreSql -Tabla PromptTemplates -Copia PromptTemplates__rel_1 -ClavePrimaria @('Id') -Columnas @('Id', 'PromptKey', 'Version', 'Texto') -TieneIdentity $true
        $s.Pasada1 | Should -Match 'SET IDENTITY_INSERT \[dbo\]\.\[PromptTemplates\] ON'
        $s.Pasada1 | Should -Match 'SET IDENTITY_INSERT \[dbo\]\.\[PromptTemplates\] OFF'
        $s.Pasada1 | Should -Match 'EXCEPT'
        $s.Pasada1 | Should -Match 'UPDATE t SET \[PromptKey\] = f\.\[PromptKey\], \[Version\] = f\.\[Version\], \[Texto\] = f\.\[Texto\] FROM \[dbo\]\.\[PromptTemplates\] t JOIN \[dbo\]\.\[PromptTemplates__rel_1\] f ON t\.\[Id\] = f\.\[Id\]'
        $s.Pasada1 | Should -Match 'INSERT INTO \[dbo\]\.\[PromptTemplates\] \(\[Id\], \[PromptKey\], \[Version\], \[Texto\]\) SELECT'
        $s.Pasada2 | Should -Match 'DELETE t FROM \[dbo\]\.\[PromptTemplates\] t WHERE NOT EXISTS'
        $s.Pasada1 | Should -Not -Match 'MERGE'
        $s.Pasada2Delete | Should -Be $s.Pasada2
    }
    It 'sin identity no envuelve el INSERT en IDENTITY_INSERT' {
        $s = New-ReleaseRestoreSql -Tabla Tipologias -Copia Tipologias__rel_1 -ClavePrimaria @('Id') -Columnas @('Id', 'Nombre') -TieneIdentity $false
        $s.Pasada1 | Should -Not -Match 'IDENTITY_INSERT'
    }
    It 'une por todas las columnas de una clave primaria compuesta' {
        $s = New-ReleaseRestoreSql -Tabla PromptTemplates -Copia PromptTemplates__rel_1 -ClavePrimaria @('PromptKey', 'Version') -Columnas @('PromptKey', 'Version', 'Texto') -TieneIdentity $false
        $s.Pasada1Update | Should -Match 'ON t\.\[PromptKey\] = f\.\[PromptKey\] AND t\.\[Version\] = f\.\[Version\] WHERE'
        $s.Pasada1Insert | Should -Match 'WHERE t\.\[PromptKey\] = f\.\[PromptKey\] AND t\.\[Version\] = f\.\[Version\]\)'
        $s.Pasada2Delete | Should -Match 'WHERE f\.\[PromptKey\] = t\.\[PromptKey\] AND f\.\[Version\] = t\.\[Version\]\)'
        $s.Pasada1Update | Should -Not -Match 'SET \[PromptKey\]'
    }
    It 'omite el UPDATE si todas las columnas son clave' {
        $s = New-ReleaseRestoreSql -Tabla X -Copia X__rel_1 -ClavePrimaria @('A', 'B') -Columnas @('A', 'B') -TieneIdentity $false
        $s.Pasada1Update | Should -BeNullOrEmpty
        $s.Pasada1 | Should -Not -Match 'UPDATE'
    }
    It 'rechaza nombres con caracteres no validos' {
        { New-ReleaseRestoreSql -Tabla 'A];DROP' -Copia C -ClavePrimaria @('Id') -Columnas @('Id') -TieneIdentity $false } | Should -Throw
    }
}

Describe 'New-ReleaseFotoBd' {
    BeforeEach { $global:T5Out = Join-Path $TestDrive ([guid]::NewGuid().ToString('N')) }

    It 'no sobrescribe una copia existente (exit 2 y ningun SELECT INTO)' {
        Mock Invoke-ReleaseSql { 1 } -ParameterFilter { $Query -match 'OBJECT_ID' }
        Mock Invoke-ReleaseSql { } -ParameterFilter { $Query -match 'INTO' }
        & $script:foto -Server s -Database d -BuildId 7 -OutDir $global:T5Out 6>&1 | Out-Null
        $LASTEXITCODE | Should -Be 2
        Should -Invoke Invoke-ReleaseSql -Times 0 -ParameterFilter { $Query -match 'INTO' }
    }
    It 'rechaza un BuildId con caracteres no validos' {
        Mock Invoke-ReleaseSql { }
        & $script:foto -Server s -Database d -BuildId '7;DROP' -OutDir $global:T5Out 6>&1 | Out-Null
        $LASTEXITCODE | Should -Be 2
        Should -Invoke Invoke-ReleaseSql -Times 0
    }
    It 'copia las cinco tablas por defecto y escribe foto-bd.json con tabla, copia y filas' {
        Mock Invoke-ReleaseSql { $null } -ParameterFilter { $Query -match 'OBJECT_ID' }
        Mock Invoke-ReleaseSql { } -ParameterFilter { $Query -match 'INTO' }
        Mock Invoke-ReleaseSql { 3 } -ParameterFilter { $Query -match 'COUNT' }
        & $script:foto -Server s -Database d -BuildId 7 -OutDir $global:T5Out 6>&1 | Out-Null
        $LASTEXITCODE | Should -Be 0
        Should -Invoke Invoke-ReleaseSql -Times 5 -Exactly -ParameterFilter { $Query -match '^SELECT \* INTO \[dbo\]\.\[\w+__rel_7\] FROM \[dbo\]\.\[\w+\]$' }
        $json = @(Get-Content -LiteralPath (Join-Path $global:T5Out 'foto-bd.json') -Raw | ConvertFrom-Json)
        $json.Count | Should -Be 5
        $json[0].tabla | Should -Be 'CatalogoTdn1'
        $json[0].copia | Should -Be 'CatalogoTdn1__rel_7'
        $json[0].filas | Should -Be 3
        $json[4].tabla | Should -Be 'PromptTemplates'
    }
}

Describe 'Restore-ReleaseFotoBd' {
    BeforeEach {
        $global:T5Out = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $global:T5Queries = $null
    }

    It 'ejecuta las pasadas 2 en orden inverso (Tdn2 antes que Tdn1), en una sola transaccion' {
        Mock Invoke-ReleaseSql { 1 } -ParameterFilter { $Query -match 'SELECT OBJECT_ID\(@Nombre\)' }
        Mock Invoke-ReleaseSql {
            [pscustomobject]@{ Columna = 'Id'; EsIdentity = $true; EsCalculada = $false; EsClave = 1 }
            [pscustomobject]@{ Columna = 'Nombre'; EsIdentity = $false; EsCalculada = $false; EsClave = 0 }
        } -ParameterFilter { $Query -match 'sys\.columns' }
        Mock Invoke-ReleaseSqlTransaccion { $global:T5Queries = $Queries; , ([int[]]::new($Queries.Count)) }
        & $script:restaura -Server s -Database d -BuildId 7 -OutDir $global:T5Out -Tablas CatalogoTdn1, CatalogoTdn2 6>&1 | Out-Null
        $LASTEXITCODE | Should -Be 0
        Should -Invoke Invoke-ReleaseSqlTransaccion -Times 1 -Exactly
        # Etiqueta de cada sentencia por su comentario inicial; las consecutivas iguales (UPDATE e INSERT de una tabla) se funden.
        $etiquetas = foreach ($q in $global:T5Queries) { if ($q -match '^-- Pasada (\d): (\w+)') { "P$($Matches[1]) $($Matches[2])" } }
        $orden = @()
        foreach ($e in $etiquetas) { if ($orden.Count -eq 0 -or $orden[-1] -ne $e) { $orden += $e } }
        $orden | Should -Be @('P1 CatalogoTdn1', 'P1 CatalogoTdn2', 'P2 CatalogoTdn2', 'P2 CatalogoTdn1')
        @($etiquetas).Count | Should -Be 6
    }
    It 'sale con exit 2 si falta alguna copia y no ejecuta nada' {
        Mock Invoke-ReleaseSql { $null } -ParameterFilter { $Query -match 'SELECT OBJECT_ID\(@Nombre\)' }
        Mock Invoke-ReleaseSqlTransaccion { }
        $out = & $script:restaura -Server s -Database d -BuildId 7 -OutDir $global:T5Out -Tablas CatalogoTdn1 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'CatalogoTdn1__rel_7'
        Should -Invoke Invoke-ReleaseSqlTransaccion -Times 0
    }
    It 'excluye las columnas calculadas y usa la clave primaria leida' {
        Mock Invoke-ReleaseSql { 1 } -ParameterFilter { $Query -match 'SELECT OBJECT_ID\(@Nombre\)' }
        Mock Invoke-ReleaseSql {
            [pscustomobject]@{ Columna = 'Id'; EsIdentity = $false; EsCalculada = $false; EsClave = 1 }
            [pscustomobject]@{ Columna = 'Valor'; EsIdentity = $false; EsCalculada = $false; EsClave = 0 }
            [pscustomobject]@{ Columna = 'Hash'; EsIdentity = $false; EsCalculada = $true; EsClave = 0 }
        } -ParameterFilter { $Query -match 'sys\.columns' }
        Mock Invoke-ReleaseSqlTransaccion { $global:T5Queries = $Queries; , ([int[]]::new($Queries.Count)) }
        & $script:restaura -Server s -Database d -BuildId 7 -OutDir $global:T5Out -Tablas Tipologias 6>&1 | Out-Null
        $LASTEXITCODE | Should -Be 0
        ($global:T5Queries -join "`n") | Should -Not -Match '\[Hash\]'
        ($global:T5Queries -join "`n") | Should -Match '\[Valor\]'
        ($global:T5Queries -join "`n") | Should -Not -Match 'IDENTITY_INSERT'
    }
    It 'sale con exit 2 si una tabla no tiene clave primaria' {
        Mock Invoke-ReleaseSql { 1 } -ParameterFilter { $Query -match 'SELECT OBJECT_ID\(@Nombre\)' }
        Mock Invoke-ReleaseSql {
            [pscustomobject]@{ Columna = 'Valor'; EsIdentity = $false; EsCalculada = $false; EsClave = 0 }
        } -ParameterFilter { $Query -match 'sys\.columns' }
        Mock Invoke-ReleaseSqlTransaccion { }
        & $script:restaura -Server s -Database d -BuildId 7 -OutDir $global:T5Out -Tablas Tipologias 6>&1 | Out-Null
        $LASTEXITCODE | Should -Be 2
        Should -Invoke Invoke-ReleaseSqlTransaccion -Times 0
    }
    It 'rechaza un BuildId con caracteres no validos' {
        Mock Invoke-ReleaseSql { }
        & $script:restaura -Server s -Database d -BuildId '7 OR 1=1' -OutDir $global:T5Out 6>&1 | Out-Null
        $LASTEXITCODE | Should -Be 2
        Should -Invoke Invoke-ReleaseSql -Times 0
    }
    It 'atribuye las filas a cada tabla y escribe restauracion-bd.json' {
        Mock Invoke-ReleaseSql { 1 } -ParameterFilter { $Query -match 'SELECT OBJECT_ID\(@Nombre\)' }
        Mock Invoke-ReleaseSql {
            [pscustomobject]@{ Columna = 'Id'; EsIdentity = $true; EsCalculada = $false; EsClave = 1 }
            [pscustomobject]@{ Columna = 'Nombre'; EsIdentity = $false; EsCalculada = $false; EsClave = 0 }
        } -ParameterFilter { $Query -match 'sys\.columns' }
        # Orden esperado: UPD T1, INS T1, UPD T2, INS T2, DEL T2, DEL T1
        Mock Invoke-ReleaseSqlTransaccion { , ([int[]]@(2, 1, 5, 0, 4, 3)) }
        $out = & $script:restaura -Server s -Database d -BuildId 7 -OutDir $global:T5Out -Tablas CatalogoTdn1, CatalogoTdn2 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $out | Should -Match 'CatalogoTdn1: 2 actualizadas, 1 insertadas, 3 borradas'
        $out | Should -Match 'CatalogoTdn2: 5 actualizadas, 0 insertadas, 4 borradas'
        $json = @(Get-Content -LiteralPath (Join-Path $global:T5Out 'restauracion-bd.json') -Raw | ConvertFrom-Json)
        $json.Count | Should -Be 2
        $json[1].tabla | Should -Be 'CatalogoTdn2'
        $json[1].actualizadas | Should -Be 5
        $json[1].borradas | Should -Be 4
    }
}
