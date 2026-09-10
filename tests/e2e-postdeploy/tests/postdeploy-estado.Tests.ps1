BeforeAll {
    . (Join-Path $PSScriptRoot ".." "lib" "postdeploy-estado.ps1")
}

Describe "Get-EstadoDePasada" {
    It "PASS pasa" {
        $r = [pscustomobject]@{ Status = "PASS"; Reason = "OK" }
        Get-EstadoDePasada -Resultado $r | Should -Be "PASS"
    }

    It "SKIP es ERROR (documentPath no existe: texto real de documentia-e2e-common.ps1)" {
        $r = [pscustomobject]@{ Status = "SKIP"; Reason = "documentPath no existe: /ruta/que/no/esta.pdf" }
        Get-EstadoDePasada -Resultado $r | Should -Be "ERROR"
    }

    It "Reason que empieza por 'Excepcion:' es ERROR (catch generico de Invoke-DocumentIAE2ECase)" {
        $r = [pscustomobject]@{ Status = "FAIL"; Reason = "Excepcion: No se pudo resolver el nombre remoto" }
        Get-EstadoDePasada -Resultado $r | Should -Be "ERROR"
    }

    It "runtimeStatus=Running tras N intentos es ERROR (timeout, no termino la orquestacion)" {
        $r = [pscustomobject]@{ Status = "FAIL"; Reason = "runtimeStatus=Running tras 60 intentos" }
        Get-EstadoDePasada -Resultado $r | Should -Be "ERROR"
    }

    It "runtimeStatus=Pending tras N intentos tambien es ERROR" {
        $r = [pscustomobject]@{ Status = "FAIL"; Reason = "runtimeStatus=Pending tras 60 intentos" }
        Get-EstadoDePasada -Resultado $r | Should -Be "ERROR"
    }

    It "runtimeStatus=Failed tras N intentos es FAIL (la orquestacion corrio y fallo de verdad)" {
        $r = [pscustomobject]@{ Status = "FAIL"; Reason = "runtimeStatus=Failed tras 60 intentos" }
        Get-EstadoDePasada -Resultado $r | Should -Be "FAIL"
    }

    It "una asercion de contenido incumplida es FAIL" {
        $r = [pscustomobject]@{ Status = "FAIL"; Reason = "output.DetalleEjecucion.MarkdownFuente='Layout' esperado='BaseDatos'" }
        Get-EstadoDePasada -Resultado $r | Should -Be "FAIL"
    }
}
