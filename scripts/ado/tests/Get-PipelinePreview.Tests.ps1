<#
  Tests de scripts/ado/Get-PipelinePreview.ps1 (YAML expandido por la API de
  preview de pipelines, para comparar antes y despues de refactorizar).

  Cubre: URL y cuerpo con -WhatIf (sin llamar ni crear fichero), escritura de
  finalYaml en UTF-8 sin BOM creando la carpeta padre, codigo 2 si la respuesta
  no trae finalYaml, y codigo 2 con el mensaje de la API en errores 4xx/5xx.
  Mockea Invoke-RestMethod y inyecta el token con -AccessToken; no toca red ni az.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/ado/tests/Get-PipelinePreview.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:script = Join-Path $PSScriptRoot '..' 'Get-PipelinePreview.ps1'
}

Describe 'Get-PipelinePreview.ps1' {
    It 'construye la URL y el cuerpo con la rama y los parametros, sin llamar, con -WhatIf' {
        Mock Invoke-RestMethod { throw 'no debe llamarse' }
        $out = (& $script:script -PipelineId 807 -Branch 'feature/x' -TemplateParameters @{ targetEnvironment = 'dev' } -OutFile "$TestDrive/p.yml" -AccessToken 'tok-secreto' -WhatIf) | Out-String
        $LASTEXITCODE | Should -Be 0
        $out | Should -Match 'https://dev.azure.com/sareb/AI%20DocClassExt/_apis/pipelines/807/preview\?api-version=7.1-preview.1'
        $out | Should -Match 'refs/heads/feature/x'
        $out | Should -Match '"targetEnvironment":\s*"dev"'
        $out | Should -Match '"previewRun":\s*true'
        $out | Should -Not -Match 'tok-secreto'
        Test-Path "$TestDrive/p.yml" | Should -BeFalse
        Should -Invoke Invoke-RestMethod -Times 0
    }

    It 'envia POST con cabecera Bearer y cuerpo correcto, y escribe finalYaml sin BOM creando la carpeta' {
        Mock Invoke-RestMethod { @{ finalYaml = "stages:`n- stage: a`n" } }
        $dest = Join-Path $TestDrive 'sub/dir/p.yml'
        & $script:script -PipelineId 832 -Branch 'feature/y' -TemplateParameters @{ dryRun = 'true' } -OutFile $dest -AccessToken 'tok' | Out-Null
        $LASTEXITCODE | Should -Be 0
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Method -eq 'Post' -and
            $Uri -eq 'https://dev.azure.com/sareb/AI%20DocClassExt/_apis/pipelines/832/preview?api-version=7.1-preview.1' -and
            $Headers['Authorization'] -eq 'Bearer tok' -and
            $Body -match 'refs/heads/feature/y' -and $Body -match '"dryRun":\s*"true"'
        }
        Test-Path $dest | Should -BeTrue
        $bytes = [System.IO.File]::ReadAllBytes($dest)
        ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB) | Should -BeFalse
        [System.Text.Encoding]::UTF8.GetString($bytes) | Should -Match '^stages:'
    }

    It 'falla con codigo 2 si la respuesta no trae finalYaml' {
        Mock Invoke-RestMethod { @{} }
        $dest = Join-Path $TestDrive 'vacio.yml'
        & $script:script -PipelineId 807 -Branch 'feature/x' -TemplateParameters @{} -OutFile $dest -AccessToken 'tok' 2>&1 | Out-Null
        $LASTEXITCODE | Should -Be 2
        Test-Path $dest | Should -BeFalse
    }

    It 'falla con codigo 2 y reenvia el mensaje de la API cuando la llamada lanza error' {
        Mock Invoke-RestMethod { throw '{"message":"pipeline no encontrado"}' }
        $out = (& $script:script -PipelineId 1 -Branch 'feature/x' -TemplateParameters @{} -OutFile "$TestDrive/e.yml" -AccessToken 'tok' 2>&1) | Out-String
        $LASTEXITCODE | Should -Be 2
        $out | Should -Match 'pipeline no encontrado'
        Test-Path "$TestDrive/e.yml" | Should -BeFalse
    }
}
