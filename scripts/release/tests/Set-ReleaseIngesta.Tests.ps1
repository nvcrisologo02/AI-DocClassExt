<#
  Tests de scripts/release/Set-ReleaseIngesta.ps1 (pausa y reactivacion de IngestDocument).

  Mockea Invoke-ReleaseAz: no toca Azure.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/Set-ReleaseIngesta.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:ingesta = Join-Path $PSScriptRoot '..' 'Set-ReleaseIngesta.ps1'
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
}

Describe 'Set-ReleaseIngesta' {
    BeforeEach {
        $global:T4Llamadas = @()
    }

    It 'Pausada invoca set con el literal y termina con exit 0 si la relectura es true' {
        Mock Invoke-ReleaseAz {
            $global:T4Llamadas += , @($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'list') { 'true' } else { $null }
        }
        $out = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Pausada 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $set = $global:T4Llamadas | Where-Object { $_ -contains 'set' }
        $set | Should -Not -BeNullOrEmpty
        $set | Should -Contain 'AzureWebJobs.IngestDocument.Disabled=true'
        $out | Should -Match 'Pausada'
    }

    It 'Pausada acepta la relectura como array de un elemento' {
        Mock Invoke-ReleaseAz {
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'list') { , @('True') } else { $null }
        }
        $null = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Pausada 6>&1
        $LASTEXITCODE | Should -Be 0
    }

    It 'Activa invoca delete con el nombre del setting y termina con exit 0 si la relectura esta vacia' {
        Mock Invoke-ReleaseAz {
            $global:T4Llamadas += , @($Arguments)
            $global:LASTEXITCODE = 0
            $null
        }
        $out = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Activa 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $del = $global:T4Llamadas | Where-Object { $_ -contains 'delete' }
        $del | Should -Not -BeNullOrEmpty
        $del | Should -Contain 'AzureWebJobs.IngestDocument.Disabled'
        $out | Should -Match 'Activa'
    }

    It 'Activa con relectura vacia como array vacio termina con exit 0' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; , @() }
        $null = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Activa 6>&1
        $LASTEXITCODE | Should -Be 0
    }

    It 'Pausada con relectura distinta de true termina con exit 2' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; if ($Arguments -contains 'list') { 'false' } else { $null } }
        $null = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Pausada 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'Pausada con relectura vacia termina con exit 2' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $null }
        $null = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Pausada 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'Activa con el setting aun presente termina con exit 2' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; if ($Arguments -contains 'list') { 'true' } else { $null } }
        $null = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Activa 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'termina con exit 2 si az falla al cambiar el setting' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 1; $null }
        $null = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Pausada 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'termina con exit 2 si az falla en la relectura' {
        Mock Invoke-ReleaseAz {
            if ($Arguments -contains 'list') { $global:LASTEXITCODE = 1; $null } else { $global:LASTEXITCODE = 0; $null }
        }
        $null = & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Activa 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'rechaza un Estado no valido' {
        { & $script:ingesta -ResourceGroup rg1 -FunctionApp app1 -Estado Otra } | Should -Throw
    }
}
