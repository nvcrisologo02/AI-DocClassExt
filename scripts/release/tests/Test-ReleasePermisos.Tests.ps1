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
        Mock Get-ReleaseKuduToken { 'TOKEN-KUDU-XYZ' }
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

    It 'Kudu usa Bearer (no Basic) contra /api/settings con timeout' {
        $null = & $script:permisos -Environment dev -SqlServer srbsqldevdocai 6>&1
        Should -Invoke Invoke-WebRequest -Times 3 -Exactly -ParameterFilter {
            $Uri -match '^https://(srbappdevdocai|srbwebadmindevdocai|srbwebpluginassetresolverdev)\.scm\.azurewebsites\.net/api/settings$' -and
            $Headers.Authorization -ceq 'Bearer TOKEN-KUDU-XYZ' -and $TimeoutSec -eq 30 -and $SkipHttpErrorCheck
        }
        Should -Not -Invoke Invoke-WebRequest -ParameterFilter { $Headers.Authorization -like 'Basic*' }
    }

    It 'pide el token una sola vez para las tres apps' {
        $null = & $script:permisos -Environment dev -SqlServer srbsqldevdocai 6>&1
        Should -Invoke Get-ReleaseKuduToken -Times 1 -Exactly
    }

    It 'un estado distinto de 200 falla con la linea Kudu <app>: HTTP <estado>' {
        Mock Invoke-WebRequest { [pscustomobject]@{ StatusCode = 404 } }
        $out = & $script:permisos -Environment dev -SqlServer srbsqldevdocai 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'Kudu srbappdevdocai: HTTP 404'
    }

    It 'una excepcion de red escribe Kudu <app>: <mensaje> y falla' {
        Mock Invoke-WebRequest { throw 'sin red' }
        $out = & $script:permisos -Environment dev -SqlServer srbsqldevdocai 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'Kudu srbappdevdocai: sin red'
    }

    It 'nunca imprime el token y sin token falla Kudu' {
        $out1 = & $script:permisos -Environment dev -SqlServer srbsqldevdocai 6>&1 | Out-String
        $out1 | Should -Not -Match 'TOKEN-KUDU-XYZ'
        Mock Get-ReleaseKuduToken { $null }
        $out2 = & $script:permisos -Environment dev -SqlServer srbsqldevdocai 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out2 | Should -Match 'Kudu srbappdevdocai'
    }

    It 'no llama a list-publishing-credentials' {
        $null = & $script:permisos -Environment dev -SqlServer srbsqldevdocai 6>&1
        Should -Not -Invoke Invoke-ReleaseAz -ParameterFilter { $Arguments -contains 'list-publishing-credentials' }
    }

    Context 'switches -SinKudu y -SoloKudu' {
        It '-SinKudu no pide token ni llama a Kudu y hace el resto de lecturas' {
            $null = & $script:permisos -Environment dev -SqlServer srbsqldevdocai -SinKudu 6>&1
            $LASTEXITCODE | Should -Be 0
            Should -Not -Invoke Invoke-WebRequest
            Should -Not -Invoke Get-ReleaseKuduToken
            Should -Invoke Invoke-ReleaseAz -ParameterFilter { $Arguments -contains 'keyvault' }
            Should -Invoke Invoke-ReleaseSql -Times 1 -Exactly
        }
        It '-SinKudu no falla aunque Kudu no se resuelva' {
            Mock Invoke-WebRequest { throw 'NODATA' }
            $out = & $script:permisos -Environment dev -SqlServer srbsqldevdocai -SinKudu 6>&1 | Out-String
            $LASTEXITCODE | Should -Be 0
            $out | Should -Not -Match 'Kudu'
        }
        It '-SoloKudu comprueba las tres apps y no lee KV, storage, claves ni SQL' {
            $null = & $script:permisos -Environment dev -SqlServer srbsqldevdocai -SoloKudu 6>&1
            $LASTEXITCODE | Should -Be 0
            Should -Invoke Invoke-WebRequest -Times 3 -Exactly
            Should -Not -Invoke Invoke-ReleaseAz
            Should -Not -Invoke Invoke-ReleaseSql
        }
        It '-SoloKudu sale con 2 si Kudu falla' {
            Mock Invoke-WebRequest { throw 'NODATA' }
            $out = & $script:permisos -Environment dev -SqlServer srbsqldevdocai -SoloKudu 6>&1 | Out-String
            $LASTEXITCODE | Should -Be 2
            $out | Should -Match 'Kudu srbappdevdocai: NODATA'
        }
        It '-SinKudu y -SoloKudu juntos son incompatibles y salen con 2' {
            $out = & $script:permisos -Environment dev -SqlServer srbsqldevdocai -SinKudu -SoloKudu 6>&1 | Out-String
            $LASTEXITCODE | Should -Be 2
            $out | Should -Match 'incompatibles'
            Should -Not -Invoke Invoke-WebRequest
        }
    }
}
