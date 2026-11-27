<#
  Tests de scripts/release/Test-ReleasePermisos.ps1 (lecturas de permisos de la service connection).

  Mockea Invoke-ReleaseAz, Invoke-ReleaseSql e Invoke-WebRequest: no toca red ni BD.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/Test-ReleasePermisos.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:permisos = Join-Path $PSScriptRoot '..' 'Test-ReleasePermisos.ps1'
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
}

Describe 'Test-ReleasePermisos' {
    BeforeEach {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; 'valor' }
        Mock Invoke-WebRequest { [pscustomobject]@{ StatusCode = 200 } }
        Mock Invoke-ReleaseSql { 1 }
    }

    It 'falla con exit 2 y nombra el recurso cuando una lectura falla' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 1; $null } -ParameterFilter { $Arguments -contains 'keyvault' }
        $out = & $script:permisos -Environment pre -SqlServer srbsqlpredocai 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'srbkvpredocai'
        $out | Should -Match 'PERMISOS_SPN_RELEASE\.md'
    }

    It 'en prod comprueba lectura sobre srbstgpredocai (origen de la copia)' {
        $null = & $script:permisos -Environment prod -SqlServer srbsqlprodocai 6>&1
        Should -Invoke Invoke-ReleaseAz -ParameterFilter { ($Arguments -join ' ') -match 'srbstgpredocai' }
    }

    It 'con todas las lecturas correctas termina con exit 0' {
        $null = & $script:permisos -Environment pre -SqlServer srbsqlpredocai 6>&1
        $LASTEXITCODE | Should -Be 0
    }

    It 'falla si Kudu no es alcanzable' {
        Mock Invoke-WebRequest { throw 'sin red' }
        $out = & $script:permisos -Environment dev -SqlServer srbsqldevdocai 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'srbappdevdocai'
    }

    It 'un resultado vacio con exit 0 (contenedor vacio) no es un fallo de permiso' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 0; $null } -ParameterFilter { $Arguments -contains 'blob' }
        $null = & $script:permisos -Environment pre -SqlServer srbsqlpredocai 6>&1
        $LASTEXITCODE | Should -Be 0
    }
}