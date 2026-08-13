BeforeAll {
    . (Join-Path $PSScriptRoot ".." ".." "api-tests" "documentia-e2e-common.ps1")
}

Describe "New-DocumentIARequestBody extensiones" {
    BeforeAll {
        $script:baseCase = [pscustomobject]@{
            domain = "pd"; id = "T1"; group = "S"
            skipDuplicateCheck = $true; forceReprocess = $true
            skipGDCUpload = $true; classificationOnly = $false
        }
    }
    It "default: extraction auto/0.80 y submittedBy actual (retrocompatibilidad)" {
        $json = New-DocumentIARequestBody -Case $script:baseCase -DocumentBase64 "QUJD" -DocumentName "x.pdf" | ConvertFrom-Json
        $json.instrucciones.extraction.model | Should -Be "auto"
        $json.instrucciones.extraction.umbral | Should -Be 0.80
        $json.trazabilidad.submittedBy | Should -Be "documentia-e2e@sareb.es"
        $json.instrucciones.PSObject.Properties['prompt'] | Should -BeNullOrEmpty
    }
    It "extractionModel y extractionUmbral del caso van al payload" {
        $case = $script:baseCase.PSObject.Copy()
        $case | Add-Member extractionModel "modelo-x"
        $case | Add-Member extractionUmbral 0.95
        $json = New-DocumentIARequestBody -Case $case -DocumentBase64 "QUJD" -DocumentName "x.pdf" | ConvertFrom-Json
        $json.instrucciones.extraction.model | Should -Be "modelo-x"
        $json.instrucciones.extraction.umbral | Should -Be 0.95
    }
    It "submittedBy del caso sobreescribe el default" {
        $case = $script:baseCase.PSObject.Copy()
        $case | Add-Member submittedBy "e2e-postdeploy"
        $json = New-DocumentIARequestBody -Case $case -DocumentBase64 "QUJD" -DocumentName "x.pdf" | ConvertFrom-Json
        $json.trazabilidad.submittedBy | Should -Be "e2e-postdeploy"
    }
    It "prompt del caso se incluye en instrucciones.prompt" {
        $case = $script:baseCase.PSObject.Copy()
        $case | Add-Member prompt ([pscustomobject]@{ systemPrompt = "s"; userPromptTemplate = "u {{CONTENT}}" })
        $json = New-DocumentIARequestBody -Case $case -DocumentBase64 "QUJD" -DocumentName "x.pdf" | ConvertFrom-Json
        $json.instrucciones.prompt.systemPrompt | Should -Be "s"
        $json.instrucciones.prompt.userPromptTemplate | Should -Be "u {{CONTENT}}"
    }
    It "forzarProcesadoSinLimitePaginas se incluye si el caso lo define" {
        $case = $script:baseCase.PSObject.Copy()
        $case | Add-Member forzarProcesadoSinLimitePaginas $true
        $json = New-DocumentIARequestBody -Case $case -DocumentBase64 "QUJD" -DocumentName "x.pdf" | ConvertFrom-Json
        $json.instrucciones.forzarProcesadoSinLimitePaginas | Should -BeTrue
    }
}

Describe "Test-CaseAssertions nuevas aserciones" {
    It "expectOutputPathsEmpty falla si el path tiene valor" {
        $case = [pscustomobject]@{ assertions = [pscustomobject]@{ expectOutputPathsEmpty = @("Identificacion.Tdn2") } }
        $status = [pscustomobject]@{ runtimeStatus = "Completed"; output = [pscustomobject]@{ Identificacion = [pscustomobject]@{ Tdn2 = "0203" } } }
        (Test-CaseAssertions -Case $case -Status $status -History @()).Success | Should -BeFalse
    }
    It "expectOutputPathsEmpty pasa si el path esta vacio" {
        $case = [pscustomobject]@{ assertions = [pscustomobject]@{ expectOutputPathsEmpty = @("Identificacion.Tdn2") } }
        $status = [pscustomobject]@{ runtimeStatus = "Completed"; output = [pscustomobject]@{ Identificacion = [pscustomobject]@{ Tdn2 = $null } } }
        (Test-CaseAssertions -Case $case -Status $status -History @()).Success | Should -BeTrue
    }
    It "expectOutputPathContains hace match parcial" {
        $case = [pscustomobject]@{ assertions = [pscustomobject]@{ expectOutputPathContains = @([pscustomobject]@{ path = "DetalleEjecucion.Clasificacion.ProveedorClasif"; value = "GPT" }) } }
        $status = [pscustomobject]@{ runtimeStatus = "Completed"; output = [pscustomobject]@{ DetalleEjecucion = [pscustomobject]@{ Clasificacion = [pscustomobject]@{ ProveedorClasif = "AzureOpenAI-GPT" } } } }
        (Test-CaseAssertions -Case $case -Status $status -History @()).Success | Should -BeTrue
    }
}
