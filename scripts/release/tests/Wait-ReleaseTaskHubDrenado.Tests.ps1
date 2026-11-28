<#
  Tests de scripts/release/Wait-ReleaseTaskHubDrenado.ps1 (drenado del task hub con tope de tiempo).

  Mockea Invoke-ReleaseAz: no toca Azure.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/Wait-ReleaseTaskHubDrenado.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:espera = Join-Path $PSScriptRoot '..' 'Wait-ReleaseTaskHubDrenado.ps1'
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
    $global:T4Clave = 'CLAVEFALSA123=='
}

Describe 'Wait-ReleaseTaskHubDrenado' {
    BeforeEach {
        $global:T4Llamadas = @()
        $global:T4Consultas = 0
    }

    It 'termina con exit 0 cuando la segunda consulta no devuelve filas' {
        Mock Invoke-ReleaseAz {
            $global:T4Llamadas += , @($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'keys') { return $global:T4Clave }
            $global:T4Consultas++
            if ($global:T4Consultas -eq 1) { [pscustomobject]@{ items = @([pscustomobject]@{ RuntimeStatus = 'Running' }) } }
            else { [pscustomobject]@{ items = @() } }
        }
        $out = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $out | Should -Match 'Quedan 1 instancias'
        $out | Should -Match 'drenado tras'
    }

    It 'consulta la tabla del task hub con la clave y el filtro Running/Pending' {
        Mock Invoke-ReleaseAz {
            $global:T4Llamadas += , @($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'keys') { return $global:T4Clave }
            [pscustomobject]@{ items = @() }
        }
        $null = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1
        $q = $global:T4Llamadas | Where-Object { $_ -contains 'query' } | Select-Object -First 1
        $q | Should -Contain 'DocumentIAHubInstances'
        $q | Should -Contain '--account-key'
        $q | Should -Contain $global:T4Clave
        $q | Should -Contain "RuntimeStatus eq 'Running' or RuntimeStatus eq 'Pending'"
    }

    It 'exit 2 con Respuesta inesperada si la consulta devuelve null, texto o un objeto sin items' {
        foreach ($raro in @($null, 'texto', [pscustomobject]@{ otra = 1 })) {
            $global:T4Raro = $raro
            Mock Invoke-ReleaseAz {
                $global:LASTEXITCODE = 0
                if ($Arguments -contains 'keys') { return $global:T4Clave }
                $global:T4Raro
            }
            $out = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1 | Out-String
            $LASTEXITCODE | Should -Be 2
            $out | Should -Match 'Respuesta inesperada de la tabla DocumentIAHubInstances'
            $out | Should -Not -Match 'drenado tras'
        }
    }

    It 'una pagina vacia con nextMarker no es drenado: sigue con --marker y ve las filas (exit 3)' {
        Mock Invoke-ReleaseAz {
            $global:T4Llamadas += , @($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'keys') { return $global:T4Clave }
            if ($Arguments -contains '--marker') {
                [pscustomobject]@{ items = @([pscustomobject]@{ RuntimeStatus = 'Running' }); nextMarker = [pscustomobject]@{} }
            }
            else {
                [pscustomobject]@{ items = @(); nextMarker = [pscustomobject]@{ nextpartitionkey = 'p'; nextrowkey = 'r' } }
            }
        }
        $out = & $script:espera -StorageAccount stg1 -TimeoutMinutos 0.01 -IntervaloSegundos 0 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 3
        $out | Should -Not -Match 'drenado tras'
        $m = $global:T4Llamadas | Where-Object { $_ -contains '--marker' } | Select-Object -First 1
        $m | Should -Not -BeNullOrEmpty
        $m | Should -Contain 'nextpartitionkey=p'
        $m | Should -Contain 'nextrowkey=r'
    }

    It 'marcador con nextrowkey nulo: sigue con nextrowkey= vacio' {
        Mock Invoke-ReleaseAz {
            $global:T4Llamadas += , @($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'keys') { return $global:T4Clave }
            if ($Arguments -contains '--marker') { [pscustomobject]@{ items = @(); nextMarker = [pscustomobject]@{} } }
            else { [pscustomobject]@{ items = @(); nextMarker = [pscustomobject]@{ nextpartitionkey = 'p'; nextrowkey = $null } } }
        }
        $null = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1
        $LASTEXITCODE | Should -Be 0
        $m = $global:T4Llamadas | Where-Object { $_ -contains '--marker' } | Select-Object -First 1
        $m | Should -Contain 'nextrowkey='
    }

    It 'pagina vacia con nextMarker y siguiente pagina vacia sin marcador: drenado (exit 0)' {
        Mock Invoke-ReleaseAz {
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'keys') { return $global:T4Clave }
            if ($Arguments -contains '--marker') { [pscustomobject]@{ items = @(); nextMarker = [pscustomobject]@{} } }
            else { [pscustomobject]@{ items = @(); nextMarker = [pscustomobject]@{ nextpartitionkey = 'p'; nextrowkey = 'r' } } }
        }
        $null = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1
        $LASTEXITCODE | Should -Be 0
    }

    It 'exit 2 si las paginas vacias con marcador superan el tope' {
        Mock Invoke-ReleaseAz {
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'keys') { return $global:T4Clave }
            [pscustomobject]@{ items = @(); nextMarker = [pscustomobject]@{ nextpartitionkey = 'p'; nextrowkey = 'r' } }
        }
        $out = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'Respuesta inesperada'
    }

    It 'termina con exit 3 e informa de las que quedan al agotar el tiempo' {
        Mock Invoke-ReleaseAz {
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'keys') { return $global:T4Clave }
            [pscustomobject]@{ items = @([pscustomobject]@{ RuntimeStatus = 'Running' }) }
        }
        $out = & $script:espera -StorageAccount stg1 -TimeoutMinutos 0.01 -IntervaloSegundos 0 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 3
        $out | Should -Match 'Tiempo agotado: quedan 1 instancias'
    }

    It 'termina con exit 2 si no se puede leer la clave' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 1; $null }
        $out = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'No se pudo leer la clave de la cuenta stg1'
    }

    It 'termina con exit 2 si la primera consulta a la tabla falla' {
        Mock Invoke-ReleaseAz {
            if ($Arguments -contains 'keys') { $global:LASTEXITCODE = 0; return $global:T4Clave }
            $global:LASTEXITCODE = 1; $null
        }
        $out = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'No se pudo leer la tabla DocumentIAHubInstances'
        $out | Should -Not -Match 'Tiempo agotado'
    }

    It 'la clave solo aparece en la linea task.setsecret' {
        Mock Invoke-ReleaseAz {
            $global:LASTEXITCODE = 0
            if ($Arguments -contains 'keys') { return $global:T4Clave }
            [pscustomobject]@{ items = @() }
        }
        $out = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1 | Out-String
        $out | Should -Match 'task\.setsecret\]CLAVEFALSA123'
        $resto = ($out -split "`r?`n" | Where-Object { $_ -notmatch 'task\.setsecret' }) -join "`n"
        $resto | Should -Not -Match 'CLAVEFALSA123'
    }

    It 'la clave no aparece en los mensajes de error de tabla' {
        Mock Invoke-ReleaseAz {
            if ($Arguments -contains 'keys') { $global:LASTEXITCODE = 0; return $global:T4Clave }
            $global:LASTEXITCODE = 1; $null
        }
        $out = & $script:espera -StorageAccount stg1 -IntervaloSegundos 0 6>&1 | Out-String
        ($out -split "`r?`n" | Where-Object { $_ -notmatch 'task\.setsecret' }) -join "`n" | Should -Not -Match 'CLAVEFALSA123'
    }
}
