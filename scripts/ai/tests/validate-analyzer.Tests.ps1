<#
  Tests de scripts/ai/validate-analyzer.ps1 frente al sentido de la promocion
  (ADR-001: DEV -> PRE -> PRO). La validacion compara el destino con el
  origen del salto, no con PRO fijo.

  Solo cubren lo que se resuelve antes de la primera llamada de red: los
  parametros, el rechazo de saltos no permitidos y la carga de
  resources.<origen>.json / resources.<destino>.json. Para el caso valido se
  usa -Only con un id inexistente, que falla despues de resolver los recursos
  y antes de cualquier GET.

  Ejecutar:
    pwsh -NoProfile -Command "Invoke-Pester -Path ./scripts/ai/tests/validate-analyzer.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:validateScript = Join-Path $PSScriptRoot '..' 'validate-analyzer.ps1'
}

Describe 'validate-analyzer.ps1: parametros de la ruta' {
    BeforeAll {
        $script:params = (Get-Command $script:validateScript).Parameters
    }
    It '-Environment admite dev, pre y prod' {
        $set = ($script:params['Environment'].Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] }).ValidValues
        $set | Should -Be @('dev', 'pre', 'prod')
    }
    It '-SourceEnvironment admite dev, pre y prod' {
        $script:params.ContainsKey('SourceEnvironment') | Should -BeTrue
        $set = ($script:params['SourceEnvironment'].Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] }).ValidValues
        $set | Should -Be @('dev', 'pre', 'prod')
    }
    It '-FromProd es un switch' {
        $script:params['FromProd'].ParameterType | Should -Be ([switch])
    }
}

Describe 'validate-analyzer.ps1: saltos rechazados antes de tocar la red' {
    It 'dev sin origen se rechaza (no tiene salto anterior)' {
        { & $script:validateScript -Environment dev -DryRun } | Should -Throw '*salto anterior*'
    }
    It '<Source> -> <Target> se rechaza' -ForEach @(
        @{ Source = 'dev'; Target = 'prod' }
        @{ Source = 'pre'; Target = 'dev' }
    ) {
        { & $script:validateScript -Environment $Target -SourceEnvironment $Source -DryRun } | Should -Throw "*$Source -> $Target*"
    }
}

Describe 'validate-analyzer.ps1: salto valido resuelve los recursos de origen y destino' {
    It '<Hop> llega hasta el filtro -Only' -ForEach @(
        @{ Hop = 'dev -> pre'; Arguments = @{ Environment = 'pre' } }
        @{ Hop = 'pre -> prod'; Arguments = @{ Environment = 'prod' } }
        @{ Hop = 'prod -> dev'; Arguments = @{ Environment = 'dev'; SourceEnvironment = 'prod'; FromProd = $true } }
    ) {
        { & $script:validateScript @Arguments -Only NoExisteAnalyzer -DryRun } | Should -Throw '*no existe infra/ai/analyzers*NoExisteAnalyzer*'
    }
}
