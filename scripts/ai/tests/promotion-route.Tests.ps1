<#
  Tests de scripts/ai/lib/promotion-route.ps1 (sentido de la promocion de
  artefactos de IA, ADR-001: DEV -> PRE -> PRO).

  Fija la tabla completa de saltos: los permitidos (dev->pre, pre->prod), la
  recuperacion desde PRO (prod->dev|pre) que exige -FromProd, y los rechazos
  (mismo entorno, saltarse PRE, ir hacia atras sin pasar por PRO).

  Ejecutar:
    pwsh -NoProfile -Command "Invoke-Pester -Path ./scripts/ai/tests/promotion-route.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    . $PSScriptRoot/../lib/promotion-route.ps1
}

Describe 'Resolve-PromotionRoute' {
    Context 'origen deducido del salto anterior' {
        It 'pre sin origen explicito viene de dev' {
            $r = Resolve-PromotionRoute -Environment pre
            $r.Source | Should -Be 'dev'
            $r.Target | Should -Be 'pre'
        }
        It 'prod sin origen explicito viene de pre' {
            $r = Resolve-PromotionRoute -Environment prod
            $r.Source | Should -Be 'pre'
            $r.Target | Should -Be 'prod'
        }
        It 'origen vacio (lo que reenvian los scripts sin -SourceEnvironment) equivale a no pasarlo' {
            $r = Resolve-PromotionRoute -Environment pre -SourceEnvironment '' -FromProd:$false
            $r.Source | Should -Be 'dev'
        }
        It 'dev sin origen explicito se rechaza: no tiene salto anterior' {
            { Resolve-PromotionRoute -Environment dev } | Should -Throw '*dev*-SourceEnvironment prod -FromProd*'
        }
    }

    Context 'origen explicito en un salto permitido' {
        It 'dev -> pre' {
            (Resolve-PromotionRoute -Environment pre -SourceEnvironment dev).Source | Should -Be 'dev'
        }
        It 'pre -> prod' {
            (Resolve-PromotionRoute -Environment prod -SourceEnvironment pre).Source | Should -Be 'pre'
        }
    }

    Context 'recuperacion desde PRO' {
        It 'prod -> <_> con -FromProd se admite' -ForEach @('dev', 'pre') {
            $r = Resolve-PromotionRoute -Environment $_ -SourceEnvironment prod -FromProd
            $r.Source | Should -Be 'prod'
            $r.Target | Should -Be $_
        }
        It 'prod -> <_> sin -FromProd se rechaza' -ForEach @('dev', 'pre') {
            { Resolve-PromotionRoute -Environment $_ -SourceEnvironment prod } | Should -Throw '*-FromProd*'
        }
        It '-FromProd con un origen que no es prod se rechaza' {
            { Resolve-PromotionRoute -Environment pre -SourceEnvironment dev -FromProd } | Should -Throw '*-FromProd*prod*'
        }
    }

    Context 'saltos no permitidos' {
        It '<Source> -> <Target> se rechaza' -ForEach @(
            @{ Source = 'dev'; Target = 'prod' }
            @{ Source = 'pre'; Target = 'dev' }
            @{ Source = 'dev'; Target = 'dev' }
            @{ Source = 'pre'; Target = 'pre' }
            @{ Source = 'prod'; Target = 'prod' }
        ) {
            { Resolve-PromotionRoute -Environment $Target -SourceEnvironment $Source -FromProd } | Should -Throw "*$Source -> $Target*"
        }
        It 'un entorno desconocido se rechaza' {
            { Resolve-PromotionRoute -Environment test } | Should -Throw
        }
    }
}

Describe 'Get-LabelingStorageAccount' {
    It '<Environment> -> <Account>' -ForEach @(
        @{ Environment = 'dev'; Account = 'srbstgdevdocai' }
        @{ Environment = 'pre'; Account = 'srbstgpredocai' }
        @{ Environment = 'prod'; Account = 'srbstgprodocai' }
    ) {
        Get-LabelingStorageAccount -Environment $Environment | Should -Be $Account
    }
}
