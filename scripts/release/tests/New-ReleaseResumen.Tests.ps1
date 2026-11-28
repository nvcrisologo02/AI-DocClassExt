<#
  Tests de scripts/release/New-ReleaseResumen.ps1.

  Solo fixtures en TestDrive: no toca Azure ni Azure DevOps.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/New-ReleaseResumen.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:resumen = Join-Path $PSScriptRoot '..' 'New-ReleaseResumen.ps1'

    function global:New-T10Json {
        param([string]$Ruta, $Objeto)
        $dir = Split-Path -Parent $Ruta
        if (-not (Test-Path -LiteralPath $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force }
        Set-Content -LiteralPath $Ruta -Value (ConvertTo-Json -InputObject $Objeto -Depth 5) -Encoding utf8
    }

    function global:New-T10Evidencias {
        param([string]$Raiz)
        New-T10Json (Join-Path $Raiz 'bd/foto-bd.json') @(@{ tabla = 'Tipologias'; copia = 'Tipologias_rel'; filas = 12 })
        New-T10Json (Join-Path $Raiz 'rollback/restauracion-bd.json') @(@{ tabla = 'Tipologias'; actualizadas = 1; insertadas = 2; borradas = 3 })
        New-T10Json (Join-Path $Raiz 'settings/foto-settings.json') ([ordered]@{ 'GDC__BypassSslValidation' = 'SECRETO-VALOR'; 'AI__Resources__x__Endpoint' = 'https://valor.example'; 'PromptDefaults__ModelKey' = $null })
        New-T10Json (Join-Path $Raiz 'codigo/foto-codigo.json') @(@{ app = 'appf'; zip = 'functions.zip'; bytes = 100; sha256 = 'abc123' })
        New-T10Json (Join-Path $Raiz 'pasos/pasos.json') @(@{ ruta = 'releases/v1/01.sql'; hash = 'h1'; resultado = 'Aplicado' })
        $a = Join-Path $Raiz 'cfg/config-apply-pre'
        New-Item -ItemType Directory -Path $a -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $a 'aplicacion.txt') -Value @('Inicio de altas', 'linea intermedia', 'Fin: 3 altas') -Encoding utf8
        Set-Content -LiteralPath (Join-Path $Raiz 'cfg/diff-config-antes.txt') -Value @('== altas: 3', 'fila nueva') -Encoding utf8
        New-T10Json (Join-Path $Raiz 'smoke/smoke-resumen.json') ([ordered]@{ passPrimeraPasada = 8; passEsperados = 8; intentosPrimeraPasada = 1; exitSegundaPasada = 0; publicado = $true })
        $r = Join-Path $Raiz 'smoke/report1'
        New-Item -ItemType Directory -Path $r -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $r 'report.md') -Value @('# Reporte', '', '## Casos', '', '| Caso | Status |', '|---|---|', '| SMK-1 | PASS |', '', '## Otra', 'x') -Encoding utf8
        Set-Content -LiteralPath (Join-Path $r 'ado-run-77.json') -Value '{"runId":77,"runUrl":"https://ado/run/77"}' -Encoding utf8
    }

    function global:Invoke-T10 {
        param([string]$Dir, [string]$Resultado = 'Rojo', [string]$Stages = '[{"nombre":"Guard","resultado":"succeeded"},{"nombre":"Smoke","resultado":"failed"}]', [string]$FotoSettingsPath = '')
        $script:out = Join-Path $TestDrive "resumen-$([guid]::NewGuid().ToString('N')).md"
        $extra = @{}
        if ($FotoSettingsPath) { $extra['FotoSettingsPath'] = $FotoSettingsPath }
        $null = & $script:resumen -Environment pre -Version v1.2.0 -Commit abc1234 -BuildId 99 -Resultado $Resultado -EvidenciasDir $Dir -StageResults $Stages -OutFile $script:out @extra 6>&1
        return $LASTEXITCODE
    }
}

AfterAll {
    Remove-Item function:global:New-T10Json, function:global:New-T10Evidencias, function:global:Invoke-T10 -ErrorAction SilentlyContinue
}

Describe 'New-ReleaseResumen con evidencias completas' {
    BeforeAll {
        $script:dir = Join-Path $TestDrive 'completo'
        New-T10Evidencias $script:dir
        $script:exit = Invoke-T10 -Dir $script:dir
        $script:md = Get-Content -LiteralPath $script:out -Raw
    }
    It 'sale con 0 y contiene el resultado y la cabecera' {
        $script:exit | Should -Be 0
        $script:md | Should -Match 'Resultado: Rojo'
        $script:md | Should -Match 'Entorno: pre'
        $script:md | Should -Match 'Commit: abc1234'
    }
    It 'incluye las cinco tablas' {
        $script:md | Should -Match '\| Stage \| Resultado \|'
        $script:md | Should -Match '\| Tabla \| Copia \| Filas \|'
        $script:md | Should -Match '\| App \| Zip \| Bytes \| SHA256 \|'
        $script:md | Should -Match '\| Paso \| Hash \| Resultado \|'
        $script:md | Should -Match '\| SMK-1 \| PASS \|'
    }
    It 'dice Revertido por la presencia de restauracion-bd.json' {
        $script:md | Should -Match 'Revertido'
    }
    It 'altas: primera y ultima linea; diff entero; run del test plan' {
        $script:md | Should -Match 'Inicio de altas'
        $script:md | Should -Match 'Fin: 3 altas'
        $script:md | Should -Not -Match 'linea intermedia'
        $script:md | Should -Match 'fila nueva'
        $script:md | Should -Match 'https://ado/run/77'
    }
    It 'no publica valores de settings' {
        $script:md | Should -Not -Match 'SECRETO-VALOR'
        $script:md | Should -Not -Match 'valor\.example'
        $script:md | Should -Match 'GDC__BypassSslValidation'
    }
    It 'lleva el aviso fijo' {
        $script:md | Should -Match 'los analyzers de CU y las migraciones EF no se deshacen \(ADR-007\)'
    }
}

Describe 'New-ReleaseResumen: resultado real de la vuelta atras' {
    BeforeAll {
        $script:dirV = Join-Path $TestDrive 'vuelta'
        New-T10Evidencias $script:dirV
        function global:New-T10Stages {
            param([hashtable]$Resultados)
            $filas = foreach ($n in 'Restaurar BD y settings', 'Revertir Functions', 'Revertir Admin', 'Revertir AssetResolver', 'Registro e ingesta', 'Reinicio') {
                $r = if ($Resultados.ContainsKey($n)) { $Resultados[$n] } else { 'Succeeded' }
                [ordered]@{ nombre = "VueltaAtras: $n"; resultado = $r }
            }
            ConvertTo-Json -InputObject @($filas) -Compress
        }
    }
    AfterAll { Remove-Item function:global:New-T10Stages -ErrorAction SilentlyContinue }

    It 'con un job de reversion Failed dice INCOMPLETA, lo nombra y no afirma que redespliega' {
        $st = New-T10Stages @{ 'Revertir Admin' = 'Failed' }
        (Invoke-T10 -Dir $script:dirV -Stages $st) | Should -Be 0
        $md = Get-Content -LiteralPath $script:out -Raw
        $md | Should -Match 'Vuelta atras: INCOMPLETA'
        $md | Should -Match 'INCOMPLETA \([^)]*Revertir Admin[^)]*Failed'
        $md | Should -Not -Match 'redespliega desde ella'
        $md | Should -Match 'Admin: NO se redesplego'
    }
    It 'con todos Succeeded o Skipped dice completa' {
        $st = New-T10Stages @{ 'Revertir AssetResolver' = 'Skipped' }
        (Invoke-T10 -Dir $script:dirV -Stages $st) | Should -Be 0
        $md = Get-Content -LiteralPath $script:out -Raw
        $md | Should -Match '(?m)^- Vuelta atras: completa'
        $md | Should -Not -Match 'INCOMPLETA'
        $md | Should -Match 'AssetResolver: no se redesplego: su job de Codigo no llego a correr'
        $md | Should -Match 'Functions: redesplegado desde la foto'
    }
    It 'la reposicion de settings sale del resultado del job Restaurar, no de inferirla' {
        $st = New-T10Stages @{ 'Restaurar BD y settings' = 'Failed' }
        (Invoke-T10 -Dir $script:dirV -Stages $st) | Should -Be 0
        $md = Get-Content -LiteralPath $script:out -Raw
        $md | Should -Match 'INCOMPLETA'
        $md | Should -Match 'No se confirmo la reposicion de la foto de settings'
    }
    It 'sin filas VueltaAtras o con resultado Verde no hay linea de vuelta atras' {
        (Invoke-T10 -Dir $script:dirV) | Should -Be 0
        (Get-Content -LiteralPath $script:out -Raw) | Should -Not -Match 'Vuelta atras:'
        (Invoke-T10 -Dir $script:dirV -Resultado Verde -Stages (New-T10Stages @{})) | Should -Be 0
        (Get-Content -LiteralPath $script:out -Raw) | Should -Not -Match 'Vuelta atras:'
    }
    It 'con -FotoSettingsPath lista los nombres de la foto (aunque no este en evidencias) y ningun valor' {
        $vacio = Join-Path $TestDrive 'sin-settings'
        New-Item -ItemType Directory -Path $vacio | Out-Null
        $foto = Join-Path $TestDrive 'aparte/foto-settings.json'
        New-T10Json $foto ([ordered]@{ 'GDC__BypassSslValidation' = 'SECRETO-VALOR'; 'AI__Resources__x__Endpoint' = 'https://valor.example'; 'PromptDefaults__ModelKey' = $null })
        (Invoke-T10 -Dir $vacio -FotoSettingsPath $foto) | Should -Be 0
        $md = Get-Content -LiteralPath $script:out -Raw
        $md | Should -Match 'Foto previa de 3 settings'
        $md | Should -Match 'AI__Resources__x__Endpoint, GDC__BypassSslValidation, PromptDefaults__ModelKey'
        $md | Should -Not -Match 'SECRETO-VALOR'
        $md | Should -Not -Match 'valor\.example'
    }
    It 'con -FotoSettingsPath que no existe el bloque de settings dice sin datos' {
        $vacio = Join-Path $TestDrive 'sin-settings'
        (Invoke-T10 -Dir $vacio -FotoSettingsPath (Join-Path $TestDrive 'no/existe.json')) | Should -Be 0
        (Get-Content -LiteralPath $script:out -Raw) | Should -Match '## App settings\s+sin datos'
    }
}

Describe 'New-ReleaseResumen sin evidencias' {
    It 'con EvidenciasDir vacio sale con 0 y cada bloque dice sin datos' {
        $dir = Join-Path $TestDrive 'vacio'
        New-Item -ItemType Directory -Path $dir | Out-Null
        (Invoke-T10 -Dir $dir -Stages '') | Should -Be 0
        $md = Get-Content -LiteralPath $script:out -Raw
        ([regex]::Matches($md, 'sin datos')).Count | Should -BeGreaterOrEqual 8
        $md | Should -Match 'Sin evidencia de vuelta atras'
        $md | Should -Not -Match 'Revertido'
    }
    It 'con EvidenciasDir inexistente y JSON de stages invalido tampoco falla' {
        (Invoke-T10 -Dir (Join-Path $TestDrive 'no-existe') -Stages '{no es json') | Should -Be 0
    }
    It 'con un JSON de evidencia mal formado no falla' {
        $dir = Join-Path $TestDrive 'roto'
        New-Item -ItemType Directory -Path $dir | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'foto-bd.json') -Value '{roto'
        (Invoke-T10 -Dir $dir) | Should -Be 0
    }
    It 'con YaAplicada dice que no se toco nada' {
        $dir = Join-Path $TestDrive 'vacio'
        (Invoke-T10 -Dir $dir -Resultado YaAplicada) | Should -Be 0
        (Get-Content -LiteralPath $script:out -Raw) | Should -Match 'no se toco nada'
    }
    It 'sale con 2 si no puede escribir OutFile' {
        $dir = Join-Path $TestDrive 'vacio'
        $bloqueo = Join-Path $TestDrive 'fichero-no-dir'
        Set-Content -LiteralPath $bloqueo -Value 'x'
        $null = & $script:resumen -Environment pre -Version v1 -Commit c -BuildId 1 -Resultado Verde -EvidenciasDir $dir -OutFile (Join-Path $bloqueo 'resumen.md') 6>&1
        $LASTEXITCODE | Should -Be 2
    }
}
