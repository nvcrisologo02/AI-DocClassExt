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

    It "mayorQue acepta un valor mayor que el umbral" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 12; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; mayorQue = 0 }
        )
        $r.Success | Should -BeTrue
    }

    It "mayorQue rechaza un valor igual al umbral" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 0; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; mayorQue = 0 }
        )
        $r.Success | Should -BeFalse
        $r.Errors[0] | Should -BeLike "*MarkdownPaginas*"
    }

    It "mayorQue rechaza un valor nulo" {
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = $null; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antes -Despues $despues -Assertions @(
            @{ columna = "MarkdownPaginas"; mayorQue = 0 }
        )
        $r.Success | Should -BeFalse
        $r.Errors[0] | Should -BeLike "*MarkdownPaginas*"
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
    It "el hash coincide con un valor calculado por un camino independiente" {
        # Valor obtenido fuera de este modulo (sha256sum / hashlib.sha256) sobre el
        # contenido literal "documentia". Ata el resultado a un valor conocido: si el
        # refactor a streaming de ComputeHash cambiara el resultado, este es el test
        # que lo detecta; los otros tres pasarian igual con un hash distinto siempre
        # que fuera estable y tuviera el formato correcto.
        $tmp = Join-Path $TestDrive "muestra-conocida.txt"
        [System.IO.File]::WriteAllText($tmp, "documentia")
        $hash = Get-Sha256DeFichero -Ruta $tmp
        $hash | Should -Be "87F6F83AF6B7D51A2FB35AC46D37226E5813B2FF2074553887BEA96768D90057"
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
    It "rechaza OrigenMarkdown porque no es columna de Documentos" {
        { New-MutacionSql -Mutacion @{ OrigenMarkdown = "Extraccion" } } | Should -Throw
    }
}

Describe "Guarda de Sha256 vacio o en blanco" {
    # Connection = $null: la guarda debe lanzar antes de tocar la conexion, asi que
    # estos tres casos no necesitan base de datos.
    #
    # "" ya queda bloqueado por el propio enlazado de parametros de PowerShell (Sha256
    # es [string] Mandatory sin AllowEmptyString), con o sin la guarda anadida: ambos
    # casos lanzan igual, por lo que aqui Should -Throw no distingue la guarda nueva de
    # la validacion de tipo preexistente. Se deja como red de seguridad de todas formas:
    # si alguien relajase el tipo del parametro (p.ej. a [object] o con
    # [AllowEmptyString()]), este test si se pondria en rojo.
    It "Remove-DocumentoPorSha256 rechaza un sha256 vacio" {
        { Remove-DocumentoPorSha256 -Connection $null -Sha256 "" } | Should -Throw
    }
    # "   " (solo espacios) si pasa el enlazado de parametros: aqui es la guarda la
    # unica linea de defensa. Se comprueba el mensaje, no solo que lance, porque sin
    # la guarda tambien lanza (al llamar CreateCommand sobre una conexion nula) y un
    # Should -Throw a secas no distinguiria las dos causas.
    It "Remove-DocumentoPorSha256 rechaza un sha256 en blanco" {
        { Remove-DocumentoPorSha256 -Connection $null -Sha256 "   " } | Should -Throw -ExpectedMessage "*Sha256*"
    }
    It "Invoke-DocumentoMutacion rechaza un sha256 vacio" {
        { Invoke-DocumentoMutacion -Connection $null -Sha256 "" -Mutacion @{ MarkdownPaginas = 3 } } | Should -Throw
    }
    It "Invoke-DocumentoMutacion rechaza un sha256 en blanco" {
        { Invoke-DocumentoMutacion -Connection $null -Sha256 "   " -Mutacion @{ MarkdownPaginas = 3 } } | Should -Throw -ExpectedMessage "*Sha256*"
    }
}

Describe "New-MutacionEjecucionSql" {
    It "devuelve null cuando no hay mutacion" {
        New-MutacionEjecucionSql -Mutacion $null | Should -BeNullOrEmpty
    }
    It "genera un UPDATE con JSON_MODIFY sobre la ultima ejecucion del documento" {
        $sql = New-MutacionEjecucionSql -Mutacion @{ "DetalleEjecucion.OrigenMarkdown" = "Extraccion" }
        $sql | Should -BeLike 'UPDATE e*JSON_MODIFY(e.ContratoSalidaCompletoJson, ''$.DetalleEjecucion.OrigenMarkdown'', @OrigenMarkdown)*'
        $sql | Should -BeLike '*FROM dbo.DocumentoEjecuciones e2*'
        $sql | Should -BeLike '*WHERE d.SHA256 = @sha*'
        $sql | Should -BeLike '*ORDER BY e2.FechaEjecucion DESC*'
    }
    It "rechaza una ruta que no esta en la lista blanca" {
        { New-MutacionEjecucionSql -Mutacion @{ "DetalleEjecucion.MarkdownFuente" = "Layout" } } | Should -Throw -ExpectedMessage "*no admitida*"
    }
    It "rechaza intento de inyeccion en la ruta" {
        { New-MutacionEjecucionSql -Mutacion @{ "DetalleEjecucion.OrigenMarkdown'), '$.x', (SELECT 1" = "x" } } | Should -Throw -ExpectedMessage "*no admitida*"
    }
    It "rechaza la ruta con otra capitalizacion" {
        # La ruta JSON de SQL Server distingue mayusculas; la lista blanca debe
        # exigir coincidencia exacta, no solo case-insensitive como el ContainsKey
        # por defecto de una hashtable de PowerShell.
        { New-MutacionEjecucionSql -Mutacion @{ "detalleejecucion.origenmarkdown" = "x" } } | Should -Throw -ExpectedMessage "*no admitida*"
    }
}

Describe "Test-DbAssertions con reglas pscustomobject (ruta de produccion)" {
    # Los tests de arriba prueban Test-ReglaTieneClave/Get-ValorRegla (internas a
    # Test-DbAssertions) solo por la rama de hashtable (@{...} escrito a mano). Pero
    # en produccion las reglas llegan de markdown-cases.json via ConvertFrom-Json,
    # que produce pscustomobject, no hashtable. Si la deteccion de presencia fallara
    # por esa rama, las aserciones se saltarian en silencio y el runner marcaria PASS
    # sin haber comprobado nada. Estos tests construyen las reglas con
    # ConvertFrom-Json de una cadena JSON real, no a mano con @{...}, para ejercitar
    # exactamente esa ruta.
    BeforeAll {
        $script:antesJson = [pscustomobject]@{
            Existe = $true; MarkdownPaginas = 12; MarkdownCompleto = $true
            LongitudGzip = 5000; LongitudCompressed = 6800
        }
    }

    It "una regla de ConvertFrom-Json es pscustomobject, no hashtable" {
        $reglas = ConvertFrom-Json -InputObject '[{ "columna": "MarkdownPaginas", "esperado": 12 }]'
        $reglas[0] | Should -BeOfType ([System.Management.Automation.PSCustomObject])
    }

    It "acepta un valor exacto que coincide, con la regla venida de JSON" {
        $reglas = @(ConvertFrom-Json -InputObject '[{ "columna": "MarkdownPaginas", "esperado": 12 }]')
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 12; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antesJson -Despues $despues -Assertions $reglas
        $r.Success | Should -BeTrue
    }

    It "distingue esperado=null (aseveracion real) de la clave ausente, ambas via JSON" {
        $reglasConNulo   = @(ConvertFrom-Json -InputObject '[{ "columna": "MarkdownPaginas", "esperado": null }]')
        $reglasSinClave  = @(ConvertFrom-Json -InputObject '[{ "columna": "MarkdownPaginas" }]')
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 12; MarkdownCompleto = $true; LongitudGzip = 5000; LongitudCompressed = 6800 }

        # esperado=null SI es una aseveracion (la columna deberia ser nula): con un
        # valor no nulo en Despues, debe fallar.
        $rConNulo = Test-DbAssertions -Antes $script:antesJson -Despues $despues -Assertions $reglasConNulo
        $rConNulo.Success | Should -BeFalse

        # sin la clave "esperado": no asevera nada sobre el valor, pasa siempre.
        $rSinClave = Test-DbAssertions -Antes $script:antesJson -Despues $despues -Assertions $reglasSinClave
        $rSinClave.Success | Should -BeTrue
    }

    It "noDisminuye y noEncoge como booleanos de JSON siguen detectandose por pscustomobject" {
        $reglas = @(ConvertFrom-Json -InputObject '[{ "columna": "LongitudGzip", "noEncoge": true }, { "columna": "MarkdownPaginas", "noDisminuye": true }]')
        $despues = [pscustomobject]@{ Existe = $true; MarkdownPaginas = 3; MarkdownCompleto = $true; LongitudGzip = 1000; LongitudCompressed = 6800 }
        $r = Test-DbAssertions -Antes $script:antesJson -Despues $despues -Assertions $reglas
        $r.Success | Should -BeFalse
        $r.Errors.Count | Should -Be 2
    }
}
