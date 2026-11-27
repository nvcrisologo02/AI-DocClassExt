<#
  Tests de scripts/release/Save-ReleaseFotoCodigo.ps1.

  Mockea Invoke-ReleaseAz e Invoke-WebRequest: no toca Azure ni Kudu.

  Ejecutar:
    pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/release/tests/Save-ReleaseFotoCodigo.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    $script:save = Join-Path $PSScriptRoot '..' 'Save-ReleaseFotoCodigo.ps1'
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
    $script:apps = [ordered]@{ functions = 'appf'; admin = 'appa'; assetresolver = 'appr' }
    function global:New-T7Zip {
        param([string]$Ruta, [switch]$Vacio)
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        if ($Vacio) { [IO.File]::WriteAllBytes($Ruta, [byte[]]@()); return }
        $z = [IO.Compression.ZipFile]::Open($Ruta, 'Create')
        try {
            $e = $z.CreateEntry('host.json')
            $w = New-Object IO.StreamWriter($e.Open())
            $w.Write('{"version":"2.0"}')
            $w.Dispose()
        }
        finally { $z.Dispose() }
    }
}

AfterAll {
    Remove-Item function:global:New-T7Zip -ErrorAction SilentlyContinue
    Remove-Variable -Name T7Vacio, T7Llamadas -Scope Global -ErrorAction SilentlyContinue
}

Describe 'Save-ReleaseFotoCodigo' {
    BeforeEach {
        $script:out = Join-Path ([IO.Path]::GetTempPath()) ('t7-' + [guid]::NewGuid().ToString('N'))
        $global:T7Vacio = $false
        $global:T7Llamadas = [System.Collections.Generic.List[string]]::new()
        Mock Invoke-ReleaseAz {
            $global:T7Llamadas.Add(($Arguments -join ' '))
            $global:LASTEXITCODE = 0
            [pscustomobject]@{ u = '$usuario-kudu'; p = 'PWD-SECRETA-987' }
        }
        Mock Invoke-WebRequest {
            New-T7Zip -Ruta $OutFile -Vacio:$global:T7Vacio
        }
    }
    AfterEach { Remove-Item $script:out -Recurse -Force -ErrorAction SilentlyContinue }

    It 'foto-codigo.json lista las tres apps con bytes y sha256' {
        $null = & $script:save -ResourceGroup rg1 -Apps $script:apps -OutDir $script:out 6>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $foto = @(Get-Content (Join-Path $script:out 'foto-codigo.json') -Raw | ConvertFrom-Json)
        $foto.Count | Should -Be 3
        ($foto | ForEach-Object { $_.app }) | Should -Be @('appf', 'appa', 'appr')
        foreach ($f in $foto) {
            $f.bytes | Should -BeGreaterThan 0
            $f.sha256 | Should -Match '^[0-9a-f]{64}$'
            Test-Path (Join-Path $script:out $f.zip) | Should -BeTrue
        }
        ($foto | ForEach-Object { $_.zip }) | Should -Be @('functions.zip', 'admin.zip', 'assetresolver.zip')
    }

    It 'usa functionapp para functions y webapp para el resto' {
        $null = & $script:save -ResourceGroup rg1 -Apps $script:apps -OutDir $script:out 6>&1
        ($global:T7Llamadas | Where-Object { $_ -match '^functionapp deployment list-publishing-credentials' -and $_ -match 'appf' }).Count | Should -Be 1
        ($global:T7Llamadas | Where-Object { $_ -match '^webapp deployment list-publishing-credentials' }).Count | Should -Be 2
    }

    It 'un zip vacio termina con exit 2 y no escribe foto-codigo.json' {
        $global:T7Vacio = $true
        $null = & $script:save -ResourceGroup rg1 -Apps $script:apps -OutDir $script:out 6>&1
        $LASTEXITCODE | Should -Be 2
        Test-Path (Join-Path $script:out 'foto-codigo.json') | Should -BeFalse
    }

    It 'si falla az al leer las credenciales termina con exit 2' {
        Mock Invoke-ReleaseAz { $global:LASTEXITCODE = 1; $null }
        $null = & $script:save -ResourceGroup rg1 -Apps $script:apps -OutDir $script:out 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'si Kudu falla termina con exit 2' {
        Mock Invoke-WebRequest { throw 'HTTP 401' }
        $null = & $script:save -ResourceGroup rg1 -Apps $script:apps -OutDir $script:out 6>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'la contrasena no aparece en la salida ni en ningun fichero y se marca como secreto' {
        $salida = & $script:save -ResourceGroup rg1 -Apps $script:apps -OutDir $script:out 6>&1 | Out-String
        $salida | Should -Match '##vso\[task.setsecret\]PWD-SECRETA-987'
        ($salida -replace '##vso\[task.setsecret\]PWD-SECRETA-987', '') | Should -Not -Match 'PWD-SECRETA-987'
        foreach ($f in Get-ChildItem $script:out -File -Filter *.json) {
            (Get-Content $f.FullName -Raw) | Should -Not -Match 'PWD-SECRETA-987'
        }
    }

    It 'un OutDir que no se puede crear termina con exit 2' {
        New-Item -ItemType Directory -Path $script:out | Out-Null
        $fichero = Join-Path $script:out 'es-un-fichero'
        [IO.File]::WriteAllText($fichero, 'x')
        $null = & $script:save -ResourceGroup rg1 -Apps $script:apps -OutDir (Join-Path $fichero 'sub') 6>&1
        $LASTEXITCODE | Should -Be 2
    }
}
