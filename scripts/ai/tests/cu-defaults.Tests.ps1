<#
  Tests de scripts/ai/lib/cu-defaults.ps1 (Ensure-Defaults).

  La libreria no llama a Azure directamente: usa Invoke-Cu del script que la
  carga. Aqui se define un Invoke-Cu de sustitucion y se mockea con Pester para
  fijar las respuestas de GET/PATCH /contentunderstanding/defaults y comprobar
  que se hace PATCH solo cuando falta o difiere algun alias, nunca en dry-run,
  y que los errores de configuracion se detectan antes de escribir.

  Ejecutar:
    pwsh -NoProfile -Command "Invoke-Pester -Path ./scripts/ai/tests/cu-defaults.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    . $PSScriptRoot/../lib/cu-defaults.ps1

    # Sustituto del Invoke-Cu de build-analyzers.ps1 / copy-cu-analyzers.ps1 (mismo contrato).
    function Invoke-Cu {
        param([string]$Method, [string]$Url, [string]$Token, [object]$Body)
        throw "Invoke-Cu sin mock: $Method $Url"
    }

    function New-Deployments {
        # deployments.<env>.json minimo: una cuenta con dos deployments y un mapeo de defaults.
        param([hashtable]$Defaults = @{ 'gpt-4.1' = 'gpt-4.1-111'; 'text-embedding-3-large' = 'emb-222' })
        return [pscustomobject]@{
            environment = 'test'
            accounts    = [pscustomobject]@{
                cuentaA = @(
                    [pscustomobject]@{ name = 'gpt-4.1-111'; model = 'gpt-4.1' },
                    [pscustomobject]@{ name = 'emb-222'; model = 'text-embedding-3-large' }
                )
            }
            contentUnderstandingDefaults = [pscustomobject]@{ cuentaA = [pscustomobject]$Defaults }
        }
    }

    function New-GetResponse {
        param([int]$Status, [hashtable]$ModelDeployments, [string]$InnerCode)
        if ($Status -eq 200) {
            return [pscustomobject]@{ Status = 200; Content = [pscustomobject]@{ modelDeployments = [pscustomobject]$ModelDeployments }; Error = $null }
        }
        $content = if ($InnerCode) { [pscustomobject]@{ error = [pscustomobject]@{ innererror = [pscustomobject]@{ code = $InnerCode } } } } else { $null }
        return [pscustomobject]@{ Status = $Status; Content = $content; Error = "HTTP $Status" }
    }

    $script:common = @{ Endpoint = 'https://cuentaA.services.ai.azure.com'; Account = 'cuentaA'; Token = 't'; DeploymentsFile = 'deployments.test.json'; ApiVersion = '2025-11-01' }
}

Describe 'Ensure-Defaults' {
    Context 'la cuenta ya tiene los defaults declarados' {
        BeforeAll {
            Mock Invoke-Cu { New-GetResponse -Status 200 -ModelDeployments @{ 'gpt-4.1' = 'gpt-4.1-111'; 'text-embedding-3-large' = 'emb-222'; 'extra' = 'otro' } }
        }
        It 'no hace PATCH y devuelve el mapeo vigente con los alias que ya habia' {
            $r = Ensure-Defaults @common -Deployments (New-Deployments) -RequiredAliases @('gpt-4.1')
            $r['gpt-4.1'] | Should -Be 'gpt-4.1-111'
            $r['extra'] | Should -Be 'otro'
            Should -Invoke Invoke-Cu -Times 1 -Exactly -ParameterFilter { $Method -eq 'Get' }
            Should -Invoke Invoke-Cu -Times 0 -Exactly -ParameterFilter { $Method -eq 'Patch' }
        }
    }

    Context 'la cuenta esta en DefaultsNotSet' {
        BeforeAll {
            Mock Invoke-Cu { New-GetResponse -Status 400 -InnerCode 'DefaultsNotSet' } -ParameterFilter { $Method -eq 'Get' }
            Mock Invoke-Cu { [pscustomobject]@{ Status = 200; Content = $null; Error = $null } } -ParameterFilter { $Method -eq 'Patch' }
        }
        It 'hace PATCH con el mapeo declarado completo' {
            $r = Ensure-Defaults @common -Deployments (New-Deployments)
            $r.Count | Should -Be 2
            Should -Invoke Invoke-Cu -Times 1 -Exactly -ParameterFilter {
                $Method -eq 'Patch' -and $Body.modelDeployments['gpt-4.1'] -eq 'gpt-4.1-111' -and $Body.modelDeployments['text-embedding-3-large'] -eq 'emb-222'
            }
        }
        It 'en dry-run no hace PATCH pero devuelve el mapeo que quedaria' {
            $r = Ensure-Defaults @common -Deployments (New-Deployments) -DryRun
            $r['gpt-4.1'] | Should -Be 'gpt-4.1-111'
            Should -Invoke Invoke-Cu -Times 0 -Exactly -ParameterFilter { $Method -eq 'Patch' }
        }
    }

    Context 'la cuenta tiene un alias apuntando a otro deployment' {
        BeforeAll {
            Mock Invoke-Cu { New-GetResponse -Status 200 -ModelDeployments @{ 'gpt-4.1' = 'gpt-4.1-viejo'; 'text-embedding-3-large' = 'emb-222' } } -ParameterFilter { $Method -eq 'Get' }
            Mock Invoke-Cu { [pscustomobject]@{ Status = 204; Content = $null; Error = $null } } -ParameterFilter { $Method -eq 'Patch' }
        }
        It 'hace PATCH fusionando: corrige el alias y conserva el resto' {
            $r = Ensure-Defaults @common -Deployments (New-Deployments)
            $r['gpt-4.1'] | Should -Be 'gpt-4.1-111'
            $r['text-embedding-3-large'] | Should -Be 'emb-222'
            Should -Invoke Invoke-Cu -Times 1 -Exactly -ParameterFilter { $Method -eq 'Patch' -and $Body.modelDeployments.Count -eq 2 }
        }
    }

    Context 'errores que deben parar antes de escribir' {
        It 'falla si los analyzers necesitan un alias que nadie mapea' {
            Mock Invoke-Cu { New-GetResponse -Status 200 -ModelDeployments @{ 'gpt-4.1' = 'gpt-4.1-111'; 'text-embedding-3-large' = 'emb-222' } }
            { Ensure-Defaults @common -Deployments (New-Deployments) -RequiredAliases @('gpt-4.1', 'gpt-4.1-mini') } | Should -Throw '*gpt-4.1-mini*'
            Should -Invoke Invoke-Cu -Times 0 -Exactly -ParameterFilter { $Method -eq 'Patch' }
        }
        It 'falla si el mapeo declarado apunta a un deployment que no figura en accounts' {
            Mock Invoke-Cu { New-GetResponse -Status 400 -InnerCode 'DefaultsNotSet' }
            $dep = New-Deployments -Defaults @{ 'gpt-4.1' = 'no-existe' }
            { Ensure-Defaults @common -Deployments $dep } | Should -Throw '*no-existe*'
            Should -Invoke Invoke-Cu -Times 0 -Exactly -ParameterFilter { $Method -eq 'Patch' }
        }
        It 'falla con mensaje de rol si el GET devuelve 403' {
            Mock Invoke-Cu { New-GetResponse -Status 403 }
            { Ensure-Defaults @common -Deployments (New-Deployments) } | Should -Throw '*Cognitive Services User*'
        }
        It 'falla si el PATCH no devuelve 2xx' {
            Mock Invoke-Cu { New-GetResponse -Status 400 -InnerCode 'DefaultsNotSet' } -ParameterFilter { $Method -eq 'Get' }
            Mock Invoke-Cu { [pscustomobject]@{ Status = 500; Content = $null; Error = 'boom' } } -ParameterFilter { $Method -eq 'Patch' }
            { Ensure-Defaults @common -Deployments (New-Deployments) } | Should -Throw '*PATCH defaults*'
        }
    }

    Context 'cuenta sin mapeo declarado' {
        It 'no hace PATCH y devuelve lo que ya tenga la cuenta' {
            Mock Invoke-Cu { New-GetResponse -Status 200 -ModelDeployments @{ 'gpt-4.1' = 'x' } }
            $dep = New-Deployments
            $dep.contentUnderstandingDefaults = [pscustomobject]@{}
            $r = Ensure-Defaults @common -Deployments $dep
            $r['gpt-4.1'] | Should -Be 'x'
            Should -Invoke Invoke-Cu -Times 0 -Exactly -ParameterFilter { $Method -eq 'Patch' }
        }
    }
}
