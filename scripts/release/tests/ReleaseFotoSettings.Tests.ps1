<#
  Tests de scripts/release/Save-ReleaseFotoSettings.ps1 y Restore-ReleaseFotoSettings.ps1.

  Mockea Invoke-ReleaseAz: no toca Azure.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/ReleaseFotoSettings.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:save = Join-Path $PSScriptRoot '..' 'Save-ReleaseFotoSettings.ps1'
    $script:restore = Join-Path $PSScriptRoot '..' 'Restore-ReleaseFotoSettings.ps1'
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
    function global:New-T6Lista {
        param([hashtable]$Valores)
        $Valores.GetEnumerator() | ForEach-Object { [pscustomobject]@{ name = $_.Key; value = $_.Value; slotSetting = $false } }
    }
}

AfterAll {
    Remove-Variable -Name T6Llamadas, T6Lista -Scope Global -ErrorAction SilentlyContinue
    Remove-Item function:global:New-T6Lista -ErrorAction SilentlyContinue
}

Describe 'Save-ReleaseFotoSettings' {
    BeforeEach {
        $script:out = Join-Path ([IO.Path]::GetTempPath()) ('t6-' + [guid]::NewGuid().ToString('N'))
        $global:T6Lista = New-T6Lista @{
            'PromptDefaults__ModelKey'                = 'prompt.x'
            'GDC__BypassSslValidation'                = 'false'
            'AI__Resources__openai_primary__Endpoint' = 'https://ia.example/'
            'GDC__Endpoint'                           = 'https://gdc.example/'
            'SqlConnectionString'                     = 'Server=tcp:secreto;Password=XYZ123'
            'OtraCosa'                                = 'valor'
        }
    }
    AfterEach { Remove-Item $script:out -Recurse -Force -ErrorAction SilentlyContinue }

    It 'foto-settings.json lleva las claves de la lista blanca y excluye el resto' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $global:T6Lista }
        $null = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir $script:out 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $foto = Get-Content (Join-Path $script:out 'foto-settings.json') -Raw | ConvertFrom-Json
        $foto.'PromptDefaults__ModelKey' | Should -Be 'prompt.x'
        $foto.'GDC__BypassSslValidation' | Should -Be 'false'
        $foto.'AI__Resources__openai_primary__Endpoint' | Should -Be 'https://ia.example/'
        $foto.PSObject.Properties.Name | Should -Not -Contain 'SqlConnectionString'
        $foto.PSObject.Properties.Name | Should -Not -Contain 'GDC__Endpoint'
        $foto.PSObject.Properties.Name | Should -Not -Contain 'OtraCosa'
        $foto.PSObject.Properties.Name | Should -Contain 'AzureWebJobs.IngestDocument.Disabled'
        $foto.'AzureWebJobs.IngestDocument.Disabled' | Should -BeNullOrEmpty
    }

    It 'appsettings-ia-antes.txt lleva nombre=valor de los Endpoint salvo GDC__Endpoint' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $global:T6Lista }
        $null = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir $script:out 6>&1
        $txt = Get-Content (Join-Path $script:out 'appsettings-ia-antes.txt') -Raw
        $txt | Should -Match 'AI__Resources__openai_primary__Endpoint=https://ia.example/'
        $txt | Should -Not -Match 'GDC__Endpoint'
    }

    It 'appsettings-nombres-antes.txt solo lleva nombres (sin =)' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $global:T6Lista }
        $null = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir $script:out 6>&1
        $txt = Get-Content (Join-Path $script:out 'appsettings-nombres-antes.txt') -Raw
        $txt | Should -Not -Match '='
        $txt | Should -Match 'SqlConnectionString'
    }

    It 'el valor de la cadena de conexion no aparece en ningun fichero ni en la salida' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $global:T6Lista }
        $salida = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir $script:out 6>&1 | Out-String
        $salida | Should -Not -Match 'XYZ123'
        foreach ($f in Get-ChildItem $script:out -File) {
            (Get-Content $f.FullName -Raw) | Should -Not -Match 'XYZ123'
        }
    }

    It 'una clave AI__Resources__x__Key no entra en foto-settings.json' {
        $global:T6Lista = @($global:T6Lista) + (New-T6Lista @{ 'AI__Resources__x__Key' = 'clave-secreta' })
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $global:T6Lista }
        $null = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir $script:out 6>&1
        $LASTEXITCODE | Should -Be 0
        $foto = Get-Content (Join-Path $script:out 'foto-settings.json') -Raw
        $foto | Should -Not -Match 'AI__Resources__x__Key'
        $foto | Should -Not -Match 'clave-secreta'
    }

    It 'sin claves Endpoint escribe appsettings-ia-antes.txt vacio' {
        $global:T6Lista = New-T6Lista @{ 'OtraCosa' = 'v'; 'GDC__Endpoint' = 'https://gdc.example/' }
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $global:T6Lista }
        $null = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir $script:out 6>&1
        $LASTEXITCODE | Should -Be 0
        (Get-Item (Join-Path $script:out 'appsettings-ia-antes.txt')).Length | Should -Be 0
    }

    It 'un OutDir que no se puede crear termina con exit 2 sin anunciar la foto' {
        $fichero = Join-Path $script:out 'es-un-fichero'
        New-Item -ItemType Directory -Path $script:out | Out-Null
        [IO.File]::WriteAllText($fichero, 'x')
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $global:T6Lista }
        $salida = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir (Join-Path $fichero 'sub') 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $salida | Should -Not -Match 'Foto de'
        Test-Path (Join-Path $fichero 'sub' 'foto-settings.json') | Should -BeFalse
    }

    It 'un fallo de az termina con exit 2' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 1; $null }
        $null = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir $script:out 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'una lista vacia termina con exit 2' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $null }
        $null = & $script:save -ResourceGroup rg1 -FunctionApp app1 -OutDir $script:out 6>&1
        $LASTEXITCODE | Should -Be 2
    }
}

Describe 'Restore-ReleaseFotoSettings' {
    BeforeEach {
        $script:dir = Join-Path ([IO.Path]::GetTempPath()) ('t6r-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:dir | Out-Null
        $script:foto = Join-Path $script:dir 'foto-settings.json'
        $global:T6Llamadas = @()
        $global:T6Lista = $null
    }
    AfterEach { Remove-Item $script:dir -Recurse -Force -ErrorAction SilentlyContinue }

    It 'set lleva los pares y nunca AzureWebJobs.IngestDocument.Disabled; relectura igual da exit 0' {
        [IO.File]::WriteAllText($script:foto, '{"PromptDefaults__ModelKey":"prompt.x","GDC__BypassSslValidation":"false","AzureWebJobs.IngestDocument.Disabled":"true"}')
        $global:T6Lista = New-T6Lista @{ 'PromptDefaults__ModelKey' = 'prompt.x'; 'GDC__BypassSslValidation' = 'false'; 'X' = 'y' }
        Mock Invoke-ReleaseAz {
            $global:T6Llamadas += , @($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'list') { $global:T6Lista } else { $null }
        }
        $null = & $script:restore -ResourceGroup rg1 -FunctionApp app1 -InFile $script:foto 6>&1
        $LASTEXITCODE | Should -Be 0
        $set = @($global:T6Llamadas | Where-Object { $_ -contains 'set' })
        $set.Count | Should -Be 1
        $set[0] | Should -Contain 'PromptDefaults__ModelKey=prompt.x'
        $set[0] | Should -Contain 'GDC__BypassSslValidation=false'
        ($set[0] -join ' ') | Should -Not -Match 'AzureWebJobs.IngestDocument.Disabled'
        @($global:T6Llamadas | Where-Object { $_ -contains 'delete' }).Count | Should -Be 0
    }

    It 'un fallo de az en set termina con exit 2' {
        [IO.File]::WriteAllText($script:foto, '{"PromptDefaults__ModelKey":"prompt.x"}')
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 1; $null }
        $null = & $script:restore -ResourceGroup rg1 -FunctionApp app1 -InFile $script:foto 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'una clave con null se borra con delete' {
        [IO.File]::WriteAllText($script:foto, '{"GDC__BypassSslValidation":null,"PromptDefaults__ModelKey":"prompt.x"}')
        $global:T6Lista = New-T6Lista @{ 'PromptDefaults__ModelKey' = 'prompt.x' }
        Mock Invoke-ReleaseAz {
            $global:T6Llamadas += , @($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'list') { $global:T6Lista } else { $null }
        }
        $null = & $script:restore -ResourceGroup rg1 -FunctionApp app1 -InFile $script:foto 6>&1
        $LASTEXITCODE | Should -Be 0
        $del = @($global:T6Llamadas | Where-Object { $_ -contains 'delete' })
        $del.Count | Should -Be 1
        $del[0] | Should -Contain 'GDC__BypassSslValidation'
    }

    It 'una relectura distinta termina con exit 2 y lista la diferencia' {
        [IO.File]::WriteAllText($script:foto, '{"PromptDefaults__ModelKey":"prompt.x"}')
        $global:T6Lista = New-T6Lista @{ 'PromptDefaults__ModelKey' = 'otro' }
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; if ($Arguments -contains 'list') { $global:T6Lista } else { $null } }
        $salida = & $script:restore -ResourceGroup rg1 -FunctionApp app1 -InFile $script:foto 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $salida | Should -Match 'PromptDefaults__ModelKey'
    }

    It 'una clave null que sigue presente en la relectura termina con exit 2' {
        [IO.File]::WriteAllText($script:foto, '{"GDC__BypassSslValidation":null}')
        $global:T6Lista = New-T6Lista @{ 'GDC__BypassSslValidation' = 'true' }
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; if ($Arguments -contains 'list') { $global:T6Lista } else { $null } }
        $null = & $script:restore -ResourceGroup rg1 -FunctionApp app1 -InFile $script:foto 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'un InFile inexistente termina con exit 2' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $null }
        $null = & $script:restore -ResourceGroup rg1 -FunctionApp app1 -InFile (Join-Path $script:dir 'no-existe.json') 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'un InFile no parseable termina con exit 2' {
        [IO.File]::WriteAllText($script:foto, '{no es json')
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $null }
        $null = & $script:restore -ResourceGroup rg1 -FunctionApp app1 -InFile $script:foto 6>&1
        $LASTEXITCODE | Should -Be 2
    }
}
