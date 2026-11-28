BeforeAll {
    # ReleaseCommon se importa en global para poder mockear Invoke-ReleaseSql dentro del modulo.
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseCommon.psm1') -Force -Global
    Import-Module (Join-Path $PSScriptRoot '..' 'ReleaseRegistro.psm1') -Force
    function New-Filas {
        param([object[]]$Filas)
        $t = [System.Data.DataTable]::new()
        foreach ($c in 'Paso', 'HashContenido', 'Resultado') { [void]$t.Columns.Add($c) }
        [void]$t.Columns.Add('FechaUtc', [datetime])
        foreach ($f in $Filas) { [void]$t.Rows.Add($f.Paso, $f.HashContenido, $f.Resultado, [datetime]::UtcNow) }
        return , $t
    }
}

Describe 'Get-ReleaseEstado' {
    It 'devuelve Cerrada si hay fila __cierre__ Aplicado' {
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro {
            New-Filas @(
                @{ Paso = 'a'; HashContenido = 'h1'; Resultado = 'Aplicado' },
                @{ Paso = '__cierre__'; HashContenido = ''; Resultado = 'Aplicado' })
        }
        $r = Get-ReleaseEstado -Server s -Database d -Version v1.2.0
        $r.Estado | Should -Be 'Cerrada'
        @($r.PasosAplicados).Paso | Should -Be @('a')
    }
    It 'devuelve Parcial si hay pasos sin cierre' {
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro {
            New-Filas @(@{ Paso = 'a'; HashContenido = 'h1'; Resultado = 'Aplicado' })
        }
        (Get-ReleaseEstado -Server s -Database d -Version v1.2.0).Estado | Should -Be 'Parcial'
    }
    It 'ignora filas Revertido' {
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro {
            New-Filas @(
                @{ Paso = 'a'; HashContenido = 'h1'; Resultado = 'Aplicado' },
                @{ Paso = 'b'; HashContenido = 'h2'; Resultado = 'Revertido' })
        }
        $r = Get-ReleaseEstado -Server s -Database d -Version v1.2.0
        @($r.PasosAplicados).Paso | Should -Be @('a')
        $r.Estado | Should -Be 'Parcial'
    }
    It 'devuelve SinAplicar con lista vacia si todo esta Revertido' {
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro {
            New-Filas @(@{ Paso = 'b'; HashContenido = 'h2'; Resultado = 'Revertido' })
        }
        $r = Get-ReleaseEstado -Server s -Database d -Version v1.2.0
        $r.Estado | Should -Be 'SinAplicar'
        @($r.PasosAplicados).Count | Should -Be 0
    }
    It 'trata la tabla inexistente como SinAplicar' {
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro { throw "Invalid object name 'dbo.ReleaseAplicada'." }
        $r = Get-ReleaseEstado -Server s -Database d -Version v1.2.0
        $r.Estado | Should -Be 'SinAplicar'
        @($r.PasosAplicados).Count | Should -Be 0
    }
    It 'relanza otros errores' {
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro { throw 'Login failed' }
        { Get-ReleaseEstado -Server s -Database d -Version v1.2.0 } | Should -Throw '*Login failed*'
    }
    It 'con -IgnorarRegistro no consulta' {
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro { }
        $r = Get-ReleaseEstado -Server s -Database d -Version v1.2.0 -IgnorarRegistro
        $r.Estado | Should -Be 'SinAplicar'
        @($r.PasosAplicados).Count | Should -Be 0
        Should -Invoke Invoke-ReleaseSql -ModuleName ReleaseRegistro -Times 0
    }
}

Describe 'Register-ReleasePaso' {
    It 'parametriza los cinco valores y no concatena' {
        $script:cap = $null
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro { $script:cap = @{ Query = $Query; Parameters = $Parameters } }
        Register-ReleasePaso -Server s -Database d -Version 'v1.2.0' -Paso 'scripts/x.sql' -HashContenido 'abc' -BuildId '123'
        $cap.Query | Should -Match '@Version'
        $cap.Query | Should -Match '@Paso'
        $cap.Query | Should -Match '@HashContenido'
        $cap.Query | Should -Match '@Resultado'
        $cap.Query | Should -Match '@BuildId'
        $cap.Query | Should -Not -Match 'v1\.2\.0'
        $cap.Parameters.Version | Should -Be 'v1.2.0'
        $cap.Parameters.Paso | Should -Be 'scripts/x.sql'
        $cap.Parameters.HashContenido | Should -Be 'abc'
        $cap.Parameters.BuildId | Should -Be '123'
        $cap.Parameters.Resultado | Should -Be 'Aplicado'
    }
}

Describe 'Register-ReleaseCierre' {
    It 'registra Paso __cierre__ con hash vacio' {
        $script:cap = $null
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro { $script:cap = @{ Query = $Query; Parameters = $Parameters } }
        Register-ReleaseCierre -Server s -Database d -Version 'v1.2.0' -BuildId '123'
        $cap.Parameters.Paso | Should -Be '__cierre__'
        $cap.Parameters.HashContenido | Should -Be ''
    }
}

Describe 'Set-ReleaseRevertida' {
    It 'solo toca filas Aplicado del BuildId y devuelve las afectadas' {
        $script:cap = $null
        Mock Invoke-ReleaseSql -ModuleName ReleaseRegistro { $script:cap = @{ Query = $Query; Parameters = $Parameters }; 3 }
        $n = Set-ReleaseRevertida -Server s -Database d -BuildId '123'
        $cap.Query | Should -Match "Resultado = 'Aplicado'"
        $cap.Query | Should -Match 'BuildId = @BuildId'
        $cap.Parameters.BuildId | Should -Be '123'
        $n | Should -Be 3
    }
}
