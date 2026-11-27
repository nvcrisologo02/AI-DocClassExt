<#
  Tests de scripts/release/Restart-ReleaseFunctionApp.ps1.

  Mockea Invoke-ReleaseAz e Invoke-WebRequest; las esperas se ponen a 0: no toca Azure.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/Restart-ReleaseFunctionApp.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:restart = Join-Path $PSScriptRoot '..' 'Restart-ReleaseFunctionApp.ps1'
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
    function script:Invoke-T9 {
        & $script:restart -ResourceGroup rg1 -FunctionApp app1 -HealthUrl 'https://app1.example/api/health?code=K' -OutFile $script:out -EsperaInicialSegundos 0 -Intentos 3 -IntervaloSegundos 0 6>&1 | Out-String
    }
}

AfterAll {
    Remove-Variable -Name T9Respuestas, T9Llamadas, T9AzExit -Scope Global -ErrorAction SilentlyContinue
}

Describe 'Restart-ReleaseFunctionApp' {
    BeforeEach {
        $script:out = Join-Path $TestDrive ('h-' + [guid]::NewGuid().ToString('N') + '.json')
        $global:T9Llamadas = [System.Collections.Generic.List[string]]::new()
        $global:T9AzExit = 0
        $global:T9Respuestas = [System.Collections.Generic.Queue[object]]::new()
        Mock Invoke-ReleaseAz {
            $global:T9Llamadas.Add(($Arguments -join ' '))
            $global:LASTEXITCODE = $global:T9AzExit
        }
        Mock Invoke-WebRequest {
            $r = if ($global:T9Respuestas.Count -gt 1) { $global:T9Respuestas.Dequeue() } else { $global:T9Respuestas.Peek() }
            if ($r -is [string]) { throw $r }
            $r
        }
    }

    It 'devuelve 0 cuando la segunda respuesta es 200 healthy' {
        $global:T9Respuestas.Enqueue([pscustomobject]@{ StatusCode = 200; Content = '{"status":"Unhealthy"}' })
        $global:T9Respuestas.Enqueue([pscustomobject]@{ StatusCode = 200; Content = '{"status":"Healthy"}' })
        $null = Invoke-T9
        $LASTEXITCODE | Should -Be 0
        $global:T9Llamadas[0] | Should -Be 'functionapp restart -n app1 -g rg1'
        Get-Content $script:out -Raw | Should -Match 'Healthy'
        Should -Invoke Invoke-WebRequest -Times 2 -Exactly
    }

    It 'devuelve 2 cuando siempre es unhealthy' {
        $global:T9Respuestas.Enqueue([pscustomobject]@{ StatusCode = 200; Content = '{"status":"unhealthy"}' })
        $null = Invoke-T9
        $LASTEXITCODE | Should -Be 2
        Should -Invoke Invoke-WebRequest -Times 3 -Exactly
        Get-Content $script:out -Raw | Should -Match 'unhealthy'
    }

    It 'un error de red o un HTTP 503 cuentan como intento fallido, no como fallo del script' {
        $global:T9Respuestas.Enqueue('sin conexion')
        $global:T9Respuestas.Enqueue([pscustomobject]@{ StatusCode = 503; Content = 'down' })
        $global:T9Respuestas.Enqueue([pscustomobject]@{ StatusCode = 200; Content = 'ok' })
        $null = Invoke-T9
        $LASTEXITCODE | Should -Be 0
    }

    It 'no imprime la URL con la clave y la marca como secreto' {
        $global:T9Respuestas.Enqueue([pscustomobject]@{ StatusCode = 200; Content = 'ok' })
        $salida = Invoke-T9
        ($salida -split "`n" | Where-Object { $_ -match 'code=K' -and $_ -notmatch 'setsecret' }) | Should -BeNullOrEmpty
        $salida | Should -Match 'setsecret'
    }

    It 'sale con 2 si el reinicio falla y no consulta el health' {
        $global:T9AzExit = 1
        $global:T9Respuestas.Enqueue([pscustomobject]@{ StatusCode = 200; Content = 'ok' })
        $null = Invoke-T9
        $LASTEXITCODE | Should -Be 2
        Should -Invoke Invoke-WebRequest -Times 0 -Exactly
    }

    It 'sale con 2 si no puede escribir OutFile' {
        $global:T9Respuestas.Enqueue([pscustomobject]@{ StatusCode = 200; Content = 'ok' })
        New-Item -ItemType Directory -Path $script:out | Out-Null
        $null = Invoke-T9
        $LASTEXITCODE | Should -Be 2
    }
}
