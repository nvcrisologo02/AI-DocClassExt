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

Describe 'Invoke-ReleaseAz diagnostico de fallos' {
    BeforeEach {
        function global:az { $global:LASTEXITCODE = 3; Write-Error 'ERROR: (AuthorizationFailed) sin permiso' -ErrorAction Continue; Write-Error 'segunda linea' -ErrorAction Continue }
    }
    AfterEach { Remove-Item function:global:az -ErrorAction SilentlyContinue }

    It 'escribe la linea con los tres primeros argumentos y el mensaje, sin el resto' {
        $out = & { $r = Invoke-ReleaseAz -Arguments @('keyvault', 'secret', 'list', '--account-key', 'VALOR-SECRETO', '--vault-name', 'kv1') } 6>&1 | Out-String
        $out | Should -Match 'az keyvault secret list fallo \(codigo 3\): ERROR: \(AuthorizationFailed\) sin permiso'
        $out | Should -Not -Match 'VALOR-SECRETO'
        $out | Should -Not -Match 'account-key'
        $out | Should -Not -Match 'segunda linea'
    }

    It 'mantiene el contrato: devuelve $null y deja el codigo de salida' {
        $r = Invoke-ReleaseAz -Arguments @('keyvault', 'secret', 'list') 6>$null
        $r | Should -BeNullOrEmpty
        Get-ReleaseAzUltimoExitCode | Should -Be 3
    }

    It 'sin ErrorRecord con ERROR usa la primera linea no vacia y recorta a 300' {
        function global:az { $global:LASTEXITCODE = 1; Write-Error ('x' * 500) -ErrorAction Continue }
        $out = & { $null = Invoke-ReleaseAz -Arguments @('webapp', 'config', 'show', '-n', 'a') } 6>&1 | Out-String
        $out | Should -Match 'az webapp config show fallo \(codigo 1\): x{300}\s*$'
        $out | Should -Not -Match 'x{301}'
    }

    It 'si az termina bien no escribe nada' {
        function global:az { $global:LASTEXITCODE = 0; '{"a":1}' }
        $out = & { $r = Invoke-ReleaseAz -Arguments @('webapp', 'show') } 6>&1 | Out-String
        $out.Trim() | Should -BeNullOrEmpty
    }
}

Describe 'ConvertTo-ReleaseAzArgumento' {
    It 'entrecomilla un argumento con parentesis y sin espacios' {
        ConvertTo-ReleaseAzArgumento -Argumento 'length(@)' -EsWindows $true | Should -Be '"length(@)"'
    }
    It 'entrecomilla un query con parentesis, corchetes y comillas simples' {
        ConvertTo-ReleaseAzArgumento -Argumento "[?starts_with(name,'X')].name" -EsWindows $true | Should -Be "`"[?starts_with(name,'X')].name`""
    }
    It 'entrecomilla con el caracter especial <Arg> y sin espacios' -ForEach @(
        @{ Arg = 'a&b' }, @{ Arg = 'a|b' }, @{ Arg = 'a<b' }, @{ Arg = 'a>b' }, @{ Arg = 'a^b' }
    ) {
        $esperado = '"' + $Arg + '"'
        ConvertTo-ReleaseAzArgumento -Argumento $Arg -EsWindows $true | Should -Be $esperado
    }
    It 'no toca un argumento con espacios' {
        ConvertTo-ReleaseAzArgumento -Argumento "[?name=='A' || name=='B']" -EsWindows $true | Should -Be "[?name=='A' || name=='B']"
    }
    It 'no toca un argumento que ya empieza por comilla doble' {
        ConvertTo-ReleaseAzArgumento -Argumento '"ya"' -EsWindows $true | Should -Be '"ya"'
    }
    It 'no toca un argumento con comillas dobles embebidas en cualquier posicion' {
        $json = '{"k":"a(b)"}'
        ConvertTo-ReleaseAzArgumento -Argumento $json -EsWindows $true | Should -Be $json
        ConvertTo-ReleaseAzArgumento -Argumento 'K="a&b"' -EsWindows $true | Should -Be 'K="a&b"'
    }
    It 'no toca un argumento sin caracteres especiales' {
        ConvertTo-ReleaseAzArgumento -Argumento 'abc' -EsWindows $true | Should -Be 'abc'
        ConvertTo-ReleaseAzArgumento -Argumento '--query' -EsWindows $true | Should -Be '--query'
    }
    It 'fuera de Windows no cambia nada' {
        ConvertTo-ReleaseAzArgumento -Argumento 'length(@)' -EsWindows $false | Should -Be 'length(@)'
    }
    It 'Invoke-ReleaseAz pasa a az los argumentos convertidos en Windows y el diagnostico usa los originales' -Skip:(-not $IsWindows) {
        $global:AzArgsVistos = $null
        function global:az { $global:AzArgsVistos = @($args); $global:LASTEXITCODE = 1; Write-Error 'ERROR: x' -ErrorAction Continue }
        try {
            $out = & { $null = Invoke-ReleaseAz -Arguments @('keyvault', 'secret', 'list', '--query', 'length(@)') } 6>&1 | Out-String
            $global:AzArgsVistos | Should -Contain '"length(@)"'
            $out | Should -Match 'az keyvault secret list fallo'
            $out | Should -Not -Match 'length'
        }
        finally { Remove-Item function:global:az -ErrorAction SilentlyContinue }
    }
}

Describe 'Get-ReleaseKuduToken' {
    BeforeEach { $script:tfPrev = $env:TF_BUILD }
    AfterEach {
        $env:TF_BUILD = $script:tfPrev
        Remove-Item function:global:az -ErrorAction SilentlyContinue
    }

    It 'devuelve el token de ARM y pide el recurso de management' {
        $env:TF_BUILD = $null
        $global:AzArgsKudu = $null
        function global:az { $global:AzArgsKudu = $args -join ' '; $global:LASTEXITCODE = 0; 'TOKEN-ARM-1' }
        Get-ReleaseKuduToken | Should -Be 'TOKEN-ARM-1'
        $global:AzArgsKudu | Should -Match 'account get-access-token --resource https://management.azure.com --query accessToken -o tsv'
        Remove-Variable AzArgsKudu -Scope Global -ErrorAction SilentlyContinue
    }

    It 'con TF_BUILD=True emite setsecret antes de devolver el token' {
        $env:TF_BUILD = 'True'
        function global:az { $global:LASTEXITCODE = 0; 'TOKEN-ARM-2' }
        $out = Get-ReleaseKuduToken 6>&1
        $textos = @($out | ForEach-Object { "$_" })
        $textos | Should -Contain '##vso[task.setsecret]TOKEN-ARM-2'
        $textos | Should -Contain 'TOKEN-ARM-2'
        [array]::IndexOf($textos, '##vso[task.setsecret]TOKEN-ARM-2') | Should -BeLessThan ([array]::IndexOf($textos, 'TOKEN-ARM-2'))
    }

    It 'sin TF_BUILD no emite setsecret' {
        $env:TF_BUILD = $null
        function global:az { $global:LASTEXITCODE = 0; 'TOKEN-ARM-3' }
        $out = Get-ReleaseKuduToken 6>&1 | Out-String
        $out | Should -Not -Match 'setsecret'
    }

    It 'devuelve $null si az falla o sale vacio' {
        $env:TF_BUILD = $null
        function global:az { $global:LASTEXITCODE = 1; $null }
        Get-ReleaseKuduToken 6>$null | Should -BeNullOrEmpty
        function global:az { $global:LASTEXITCODE = 0; '' }
        Get-ReleaseKuduToken 6>$null | Should -BeNullOrEmpty
    }
}
