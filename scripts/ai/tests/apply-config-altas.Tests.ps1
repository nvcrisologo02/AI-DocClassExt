<#
  Tests de scripts/ai/apply-config-altas.ps1 (AB#101075).

  Cubren las guardas que se evaluan antes de conectar a la BD: sin INSERT no conecta,
  y un fichero con sentencias distintas de INSERT o con tablas fuera de configuracion
  se rechaza. La ejecucion real (copias __bak, transaccion, recuento) se valida con el
  ensayo DEV -> PRE del pipeline 832.

  El servidor es inalcanzable a proposito: si una guarda dejara pasar el fichero, el
  test fallaria por el mensaje de error, no por una escritura.

  Ejecutar:
    pwsh -NoProfile -Command "Invoke-Pester -Path ./scripts/ai/tests/apply-config-altas.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:Script = Join-Path $PSScriptRoot '../apply-config-altas.ps1'

    function Invoke-Altas([string]$Sql) {
        $file = Join-Path $TestDrive 'altas.sql'
        Set-Content -LiteralPath $file -Value $Sql -Encoding utf8
        $out = & pwsh -NoProfile -File $script:Script -Server 'no-existe.invalid' -SqlFile $file -BackupSuffix 'test' 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($out | Out-String) }
    }
}

Describe 'apply-config-altas.ps1' {
    It 'sin INSERT termina en 0 sin conectar' {
        $r = Invoke-Altas "SET XACT_ABORT ON;`nBEGIN TRANSACTION;`n`nCOMMIT TRANSACTION;`n"
        $r.ExitCode | Should -Be 0
        $r.Output | Should -Match 'Sin altas'
    }

    It 'rechaza un UPDATE' {
        $r = Invoke-Altas "INSERT INTO [dbo].[Tipologias] ([Codigo])`nSELECT N'a'`nWHERE NOT EXISTS (SELECT 1 FROM [dbo].[Tipologias] WHERE [Codigo] = N'a');`nUPDATE [dbo].[Tipologias] SET [Nombre] = N'x';`n"
        $r.ExitCode | Should -Not -Be 0
        $r.Output | Should -Match 'no permitidas: UPDATE'
    }

    It 'rechaza altas en tablas fuera de configuracion' {
        $r = Invoke-Altas "INSERT INTO [dbo].[Documentos] ([Id])`nSELECT 1`nWHERE NOT EXISTS (SELECT 1 FROM [dbo].[Documentos] WHERE [Id] = 1);`n"
        $r.ExitCode | Should -Not -Be 0
        $r.Output | Should -Match 'fuera de configuracion: Documentos'
    }

    It 'no confunde texto de un literal con una sentencia' {
        $r = Invoke-Altas "INSERT INTO [dbo].[PromptTemplates] ([Content])`nSELECT N'linea 1`nupdate del resumen`nDelete lo sobrante'`nWHERE NOT EXISTS (SELECT 1 FROM [dbo].[PromptTemplates] WHERE [Content] = N'x');`n"
        $r.Output | Should -Not -Match 'no permitidas'
        $r.ExitCode | Should -Not -Be 0
    }
}
