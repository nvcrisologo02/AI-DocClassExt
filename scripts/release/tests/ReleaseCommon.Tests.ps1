BeforeAll {
    $script:repo = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')).Path
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force
}

Describe 'ConvertTo-ReleaseArgumentList' {
    It 'separa por espacios y sustituye <env>' {
        ConvertTo-ReleaseArgumentList -Argumentos '-Environment <env> -Version v5' -Environment pre | Should -Be @('-Environment', 'pre', '-Version', 'v5')
    }
    It 'respeta comillas y no evalua' {
        ConvertTo-ReleaseArgumentList -Argumentos '-Ruta "a b" -X $(Get-Date);rm' -Environment dev | Should -Be @('-Ruta', 'a b', '-X', '$(Get-Date);rm')
    }
    It 'respeta comillas simples' {
        ConvertTo-ReleaseArgumentList -Argumentos "-A 'x y' -B" -Environment dev | Should -Be @('-A', 'x y', '-B')
    }
    It 'conserva el caracter | dentro del token' {
        ConvertTo-ReleaseArgumentList -Argumentos '-A a|b -B c' -Environment dev | Should -Be @('-A', 'a|b', '-B', 'c')
    }
    It 'sustituye <env> repetido dentro de un token y entre tokens' {
        ConvertTo-ReleaseArgumentList -Argumentos '<env>-<env> x<env> <env>' -Environment pre | Should -Be @('pre-pre', 'xpre', 'pre')
    }
    It 'lanza con comillas sin cerrar' {
        { ConvertTo-ReleaseArgumentList -Argumentos '-A "sin cerrar' -Environment dev } | Should -Throw
        { ConvertTo-ReleaseArgumentList -Argumentos "-A 'sin cerrar" -Environment dev } | Should -Throw
    }
    It 'devuelve lista vacia con entrada vacia o en blanco' {
        @(ConvertTo-ReleaseArgumentList -Argumentos '   ' -Environment dev).Count | Should -Be 0
        @(ConvertTo-ReleaseArgumentList -Argumentos '' -Environment dev).Count | Should -Be 0
    }
}

Describe 'Get-ReleaseContentHash' {
    It 'da el SHA-256 conocido de "abc"' {
        Get-ReleaseContentHash -Content 'abc' | Should -Be 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad'
    }
}

Describe 'New-EmbeddingsSql' {
    It 'sustituye los dos DECLARE del 07 del repo' {
        $s = New-EmbeddingsSql -TemplatePath "$repo/scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql" -Version v6 -Modo sombra
        $s | Should -Match "DECLARE @Modo NVARCHAR\(10\) = N'sombra';"
        $s | Should -Match "DECLARE @Version NVARCHAR\(10\) = N'v6';"
        $s | Should -Not -Match "N'v5'"
    }
    It 'falla si la plantilla no tiene los DECLARE' {
        Set-Content -LiteralPath "$TestDrive/x.sql" -Value 'SELECT 1;'
        { New-EmbeddingsSql -TemplatePath "$TestDrive/x.sql" -Version v1 -Modo off } | Should -Throw
    }
    It 'falla si restringidoModo difiere de modo' {
        { New-EmbeddingsSql -TemplatePath "$repo/scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql" -Version v6 -Modo sombra -RestringidoModo hibrido } | Should -Throw
    }
}

Describe 'Get-ReleaseBranchEsperada' {
    It 'pre->develop, prod->master, dev->null' {
        Get-ReleaseBranchEsperada -Environment pre | Should -Be 'refs/heads/develop'
        Get-ReleaseBranchEsperada -Environment prod | Should -Be 'refs/heads/master'
        Get-ReleaseBranchEsperada -Environment dev | Should -BeNullOrEmpty
    }
}

Describe 'ConvertTo-EnvironmentAlias' {
    It 'prod->pro y el resto igual' {
        ConvertTo-EnvironmentAlias -Environment prod | Should -Be 'pro'
        ConvertTo-EnvironmentAlias -Environment dev | Should -Be 'dev'
        ConvertTo-EnvironmentAlias -Environment pre | Should -Be 'pre'
    }
}

Describe 'Get-DryRunSwitchName' {
    It 'detecta [switch]$DryRun' {
        Set-Content -LiteralPath "$TestDrive/a.ps1" -Value 'param([Parameter(Mandatory)][string]$X, [switch]$DryRun)'
        Get-DryRunSwitchName -ScriptPath "$TestDrive/a.ps1" | Should -Be 'DryRun'
    }
    It 'detecta SupportsShouldProcess como WhatIf' {
        Set-Content -LiteralPath "$TestDrive/b.ps1" -Value '[CmdletBinding(SupportsShouldProcess)]', 'param([string]$X)'
        Get-DryRunSwitchName -ScriptPath "$TestDrive/b.ps1" | Should -Be 'WhatIf'
    }
    It 'falla si no admite ninguno' {
        Set-Content -LiteralPath "$TestDrive/c.ps1" -Value 'param([string]$X)'
        { Get-DryRunSwitchName -ScriptPath "$TestDrive/c.ps1" } | Should -Throw
    }
}
