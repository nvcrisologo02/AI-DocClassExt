BeforeAll {
    . (Join-Path $PSScriptRoot ".." "lib" "postdeploy-db.ps1")
}

Describe "Assert-DbServidorEsDev" {
    It "acepta el servidor de DEV" {
        { Assert-DbServidorEsDev -SqlServer "srbsqldevdocai.database.windows.net" } | Should -Not -Throw
    }
    It "acepta el servidor de DEV sin sufijo de dominio" {
        { Assert-DbServidorEsDev -SqlServer "srbsqldevdocai" } | Should -Not -Throw
    }
    It "rechaza el servidor de PRO" {
        { Assert-DbServidorEsDev -SqlServer "srbsqlprodocai.database.windows.net" } | Should -Throw
    }
    It "rechaza el servidor de PRE" {
        { Assert-DbServidorEsDev -SqlServer "srbsqlpredocai.database.windows.net" } | Should -Throw
    }
    It "rechaza vacio" {
        { Assert-DbServidorEsDev -SqlServer "" } | Should -Throw
    }
    It "rechaza un subdominio que suplanta el nombre de DEV" {
        { Assert-DbServidorEsDev -SqlServer "srbsqldevdocai.attacker.com" } | Should -Throw
    }
    It "rechaza un sufijo anadido tras el FQDN legitimo" {
        { Assert-DbServidorEsDev -SqlServer "srbsqldevdocai.database.windows.net.attacker.com" } | Should -Throw
    }
    It "acepta el nombre de DEV en mayusculas" {
        { Assert-DbServidorEsDev -SqlServer "SRBSQLDEVDOCAI" } | Should -Not -Throw
    }
    It "tolera espacios alrededor del nombre" {
        { Assert-DbServidorEsDev -SqlServer "  srbsqldevdocai  " } | Should -Not -Throw
    }
}

Describe "Test-DbAssertions" {
    BeforeAll {
        $script:antes = [pscustomobject]@{
            Existe = $true; MarkdownPaginas = 12; MarkdownCompleto = $true
            LongitudGzip = 5000; LongitudCompressed = 6800
        }
    }

    It "acepta un valor exacto que coincide" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 12; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; esperado = 12 }
        )
        $r.Success | Should -BeTrue
    }

    It "rechaza un valor exacto que no coincide" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 3; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; esperado = 12 }
        )
        $r.Success | Should -BeFalse
        $r.Errors[0] | Should -BeLike "*MarkdownPaginas*"
    }

    It "noDisminuye acepta que suba" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 20; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; noDisminuye = $true }
        )
        $r.Success | Should -BeTrue
    }

    It "noDisminuye rechaza que baje" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 3; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; noDisminuye = $true }
        )
        $r.Success | Should -BeFalse
    }

    It "noEncoge rechaza que el blob se reduzca" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 12; MarkdownCompleto = $true; LongitudGzip = 1200; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "LongitudGzip"; noEncoge = $true }
        )
        $r.Success | Should -BeFalse
        $r.Errors[0] | Should -BeLike "*encog*"
    }

    It "noEsNulo rechaza que MarkdownPaginas pase a nulo" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = $null; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; noEsNulo = $true }
        )
        $r.Success | Should -BeFalse
    }

    It "acumula varios errores en la misma evaluacion" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 3; MarkdownCompleto = $false; LongitudGzip = 100; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; esperado = 12 },
            @{ columna = "MarkdownCompleto"; esperado = $true },
            @{ columna = "LongitudGzip"; noEncoge = $true }
        )
        $r.Success | Should -BeFalse
        $r.Errors.Count | Should -Be 3
    }

    It "falla si la fila no existe despues" {
        $despues = [pscustomobject]@{ Existe = $false; MarkdownPaginas = $null; MarkdownCompleto = $false; LongitudGzip = 0; LongitudCompressed = 0 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; esperado = 12 }
        )
        $r.Success | Should -BeFalse
        $r.Errors[0] | Should -BeLike "*no existe*"
    }

    It "esperado con valor nulo asevera que la columna quedo en nulo" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 12; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; esperado = $null }
        )
        $r.Success | Should -BeFalse
    }

    It "esperado con valor nulo pasa cuando la columna si quedo en nulo" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = $null; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; esperado = $null }
        )
        $r.Success | Should -BeTrue
    }

    It "noDisminuye sin base de comparacion no pasa en silencio" {
        $antesNulo = [pscustomobject]@{ Existe = $true; MarkdownPaginas = $null; MarkdownCompleto = $false; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $despues   = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 0; MarkdownCompleto = $false; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $antesNulo -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; noDisminuye = $true }
        )
        $r.Success | Should -BeFalse
        $r.Errors[0] | Should -BeLike "*base de comparacion*"
    }
}

Describe "Get-Sha256DeFichero" {
    It "calcula el hash de un fichero conocido" {
        $tmp = Join-Path $TestDrive "muestra.txt"
        [System.IO.File]::WriteAllText($tmp, "documentia")
        $hash = Get-Sha256DeFichero -Ruta $tmp
        $hash | Should -Match "^[0-9A-F]{64}$"
    }
    It "es estable entre llamadas" {
        $tmp = Join-Path $TestDrive "muestra2.txt"
        [System.IO.File]::WriteAllText($tmp, "documentia")
        (Get-Sha256DeFichero -Ruta $tmp) | Should -Be (Get-Sha256DeFichero -Ruta $tmp)
    }
    It "lanza si el fichero no existe" {
        { Get-Sha256DeFichero -Ruta (Join-Path $TestDrive "no-existe.bin") } | Should -Throw
    }
}

Describe "New-MutacionSql" {
    It "devuelve null cuando no hay mutacion" {
        New-MutacionSql -Mutacion $null | Should -BeNullOrEmpty
    }
    It "genera un UPDATE con una columna" {
        $sql = New-MutacionSql -Mutacion @{ MarkdownPaginas = $null }
        $sql | Should -BeLike "UPDATE Documentos SET*MarkdownPaginas = @MarkdownPaginas*WHERE SHA256 = @sha*"
    }
    It "genera un UPDATE con varias columnas" {
        $sql = New-MutacionSql -Mutacion @{ MarkdownPaginas = 3; MarkdownCompleto = $true }
        $sql | Should -BeLike "*MarkdownPaginas = @MarkdownPaginas*"
        $sql | Should -BeLike "*MarkdownCompleto = @MarkdownCompleto*"
    }
    It "rechaza una columna que no esta en la lista blanca" {
        { New-MutacionSql -Mutacion @{ SHA256 = "x" } } | Should -Throw
    }
    It "rechaza intento de inyeccion en el nombre de columna" {
        { New-MutacionSql -Mutacion @{ "MarkdownPaginas = 1; DROP TABLE Documentos --" = 1 } } | Should -Throw
    }
}
