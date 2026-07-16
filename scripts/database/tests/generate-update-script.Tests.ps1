<#
  Tests de generate-update-script.ps1.

  Cubren las funciones puras. Las funciones de BBDD (Get-TableMeta, Get-DuplicateKeys,
  Read-TableRows, New-SqlConnection) requieren conexion y se validan generando contra
  dev y ejecutando el .sql resultante contra la propia dev: debe casar todo, no dar
  ningun aviso y dejar el CHECKSUM_AGG de la tabla intacto.

  El BeforeAll dot-sourcea el script. Funciona porque el script guarda su cuerpo de
  ejecucion tras 'if ($MyInvocation.InvocationName -ne ".")' y porque sus parametros
  no son Mandatory (uno mandatory abriria un prompt y colgaria la suite).

  AVISO: no sustituyas 'Should -Match ([regex]::Escape(...))' por 'Should -BeLike'.
  En los comodines de PowerShell, [...] es una clase de caracteres, no texto literal:
  "UPDATE [dbo].[T]" -like "*UPDATE [dbo].[T]*" devuelve False. Como todo el SQL que
  generamos va lleno de identificadores entre corchetes, aqui se compara siempre con
  -Match sobre el patron escapado.

  Ejecutar:
    pwsh -NoProfile -Command "Invoke-Pester -Path ./scripts/database/tests/generate-update-script.Tests.ps1 -Output Detailed"
#>

BeforeAll {
    . $PSScriptRoot/../generate-update-script.ps1
}

Describe 'Quote-Id' {
    It 'envuelve el identificador en corchetes' {
        Quote-Id 'Codigo' | Should -Be '[Codigo]'
    }
    It 'escapa el corchete de cierre' {
        Quote-Id 'mal]nombre' | Should -Be '[mal]]nombre]'
    }
}

Describe 'ConvertTo-SqlLiteral' {
    It 'convierte $null a NULL' {
        ConvertTo-SqlLiteral $null | Should -Be 'NULL'
    }
    It 'convierte DBNull a NULL' {
        ConvertTo-SqlLiteral ([DBNull]::Value) | Should -Be 'NULL'
    }
    It 'escapa la comilla simple duplicandola' {
        ConvertTo-SqlLiteral "O'Brien" | Should -Be "N'O''Brien'"
    }
    It 'preserva acentos y saltos de linea' {
        ConvertTo-SqlLiteral "Clasificación`nde documentos" | Should -Be "N'Clasificación`nde documentos'"
    }
    It 'convierte $true a 1 y $false a 0' {
        ConvertTo-SqlLiteral $true  | Should -Be '1'
        ConvertTo-SqlLiteral $false | Should -Be '0'
    }
    It 'formatea datetime en ISO con 7 decimales' {
        $d = [datetime]::new(2026, 7, 16, 10, 22, 31, 123)
        ConvertTo-SqlLiteral $d | Should -Be "'2026-07-16T10:22:31.1230000'"
    }
    It 'formatea decimales con punto aunque el locale sea español' {
        # Sin InvariantCulture esto saldria como 1,5 y romperia el SQL.
        ConvertTo-SqlLiteral ([decimal]'1.5') | Should -Be '1.5'
    }
    It 'formatea enteros sin separador de millares' {
        ConvertTo-SqlLiteral ([int]1234567) | Should -Be '1234567'
    }
    It 'convierte byte[] a literal hexadecimal' {
        ConvertTo-SqlLiteral ([byte[]]@(0xDE, 0xAD)) | Should -Be '0xDEAD'
    }
    It 'convierte byte[] vacio a 0x' {
        ConvertTo-SqlLiteral ([byte[]]@()) | Should -Be '0x'
    }
    It 'convierte guid a literal entrecomillado' {
        $g = [guid]'11112222-3333-4444-5555-666677778888'
        ConvertTo-SqlLiteral $g | Should -Be "'11112222-3333-4444-5555-666677778888'"
    }
}

Describe 'Resolve-UpdateColumns' {
    BeforeEach {
        # Refleja CatalogoTdn1: Id identidad, Codigo clave de negocio.
        $script:meta = New-TableMetaObject -Schema 'dbo' -Table 'CatalogoTdn1' `
            -Columns @('Id', 'Codigo', 'Nombre', 'Descripcion', 'TDN2_Prompt') `
            -IdentityCol 'Id' -ComputedCols @() -NullableCols @('Descripcion', 'TDN2_Prompt')
    }

    It 'por defecto devuelve todas menos clave, identidad y computadas' {
        Resolve-UpdateColumns -Meta $script:meta -KeyColumns @('Codigo') -Columns $null |
            Should -Be @('Nombre', 'Descripcion', 'TDN2_Prompt')
    }
    It 'excluye las columnas computadas' {
        $m = New-TableMetaObject -Schema 'dbo' -Table 'T' `
            -Columns @('Id', 'K', 'A', 'Calc') -IdentityCol 'Id' `
            -ComputedCols @('Calc') -NullableCols @()
        Resolve-UpdateColumns -Meta $m -KeyColumns @('K') -Columns $null | Should -Be @('A')
    }
    It 'respeta el subconjunto indicado en -Columns' {
        Resolve-UpdateColumns -Meta $script:meta -KeyColumns @('Codigo') -Columns @('TDN2_Prompt') |
            Should -Be @('TDN2_Prompt')
    }
    It 'lanza si -Columns incluye la columna identidad' {
        { Resolve-UpdateColumns -Meta $script:meta -KeyColumns @('Codigo') -Columns @('Id') } |
            Should -Throw '*identidad*'
    }
    It 'lanza si -Columns incluye una columna clave' {
        { Resolve-UpdateColumns -Meta $script:meta -KeyColumns @('Codigo') -Columns @('Codigo') } |
            Should -Throw '*clave*'
    }
    It 'lanza si -Columns incluye una columna computada' {
        $m = New-TableMetaObject -Schema 'dbo' -Table 'T' `
            -Columns @('Id', 'K', 'Calc') -IdentityCol 'Id' `
            -ComputedCols @('Calc') -NullableCols @()
        { Resolve-UpdateColumns -Meta $m -KeyColumns @('K') -Columns @('Calc') } |
            Should -Throw '*computada*'
    }
    It 'lanza si -Columns incluye una columna inexistente' {
        { Resolve-UpdateColumns -Meta $script:meta -KeyColumns @('Codigo') -Columns @('NoExiste') } |
            Should -Throw '*NoExiste*'
    }
    It 'lanza si no queda ninguna columna que actualizar' {
        $m = New-TableMetaObject -Schema 'dbo' -Table 'T' -Columns @('Id', 'K') `
            -IdentityCol 'Id' -ComputedCols @() -NullableCols @()
        { Resolve-UpdateColumns -Meta $m -KeyColumns @('K') -Columns $null } |
            Should -Throw '*ninguna columna*'
    }
}

Describe 'Assert-KeyColumns' {
    BeforeEach {
        $script:meta = New-TableMetaObject -Schema 'dbo' -Table 'PromptTemplates' `
            -Columns @('Id', 'PromptKey', 'Version', 'Content', 'IsActive') `
            -IdentityCol 'Id' -ComputedCols @() -NullableCols @()
    }

    It 'acepta una clave simple existente' {
        { Assert-KeyColumns -Meta $script:meta -KeyColumns @('PromptKey') } | Should -Not -Throw
    }
    It 'acepta una clave compuesta existente' {
        { Assert-KeyColumns -Meta $script:meta -KeyColumns @('PromptKey', 'Version') } | Should -Not -Throw
    }
    It 'lanza si una columna clave no existe' {
        { Assert-KeyColumns -Meta $script:meta -KeyColumns @('NoExiste') } | Should -Throw '*NoExiste*'
    }
    It 'lanza si no se indica ninguna clave' {
        { Assert-KeyColumns -Meta $script:meta -KeyColumns @() } | Should -Throw '*-KeyColumns*'
    }
}

Describe 'New-UpdateStatement' {
    BeforeEach {
        $script:meta = New-TableMetaObject -Schema 'dbo' -Table 'CatalogoTdn1' `
            -Columns @('Id', 'Codigo', 'Nombre', 'TDN2_Prompt') `
            -IdentityCol 'Id' -ComputedCols @() -NullableCols @('TDN2_Prompt')
    }

    It 'emite el UPDATE con SET y WHERE de clave simple' {
        $row = @{ Codigo = 'ESC'; Nombre = 'Escrituras'; TDN2_Prompt = 'Eres un clasificador' }
        $sql = New-UpdateStatement -Meta $script:meta -Row $row `
            -KeyColumns @('Codigo') -SetColumns @('Nombre', 'TDN2_Prompt')

        $sql | Should -Match ([regex]::Escape('UPDATE [dbo].[CatalogoTdn1]'))
        $sql | Should -Match ([regex]::Escape("SET [Nombre] = N'Escrituras',"))
        $sql | Should -Match ([regex]::Escape("[TDN2_Prompt] = N'Eres un clasificador'"))
        $sql | Should -Match ([regex]::Escape("WHERE [Codigo] = N'ESC';"))
    }
    It 'emite el aviso IF @@ROWCOUNT = 0 tras el UPDATE' {
        $row = @{ Codigo = 'ESC'; Nombre = 'Escrituras' }
        $sql = New-UpdateStatement -Meta $script:meta -Row $row `
            -KeyColumns @('Codigo') -SetColumns @('Nombre')

        $sql | Should -Match ([regex]::Escape("IF @@ROWCOUNT = 0 PRINT N'AVISO: sin match en destino -> Codigo = ESC';"))
    }
    It 'une las columnas de una clave compuesta con AND' {
        $m = New-TableMetaObject -Schema 'dbo' -Table 'PromptTemplates' `
            -Columns @('Id', 'PromptKey', 'Version', 'Content') `
            -IdentityCol 'Id' -ComputedCols @() -NullableCols @()
        $row = @{ PromptKey = 'classification.phase1.system'; Version = 3; Content = 'texto' }
        $sql = New-UpdateStatement -Meta $m -Row $row `
            -KeyColumns @('PromptKey', 'Version') -SetColumns @('Content')

        $sql | Should -Match ([regex]::Escape("WHERE [PromptKey] = N'classification.phase1.system' AND [Version] = 3;"))
        $sql | Should -Match ([regex]::Escape('PromptKey = classification.phase1.system, Version = 3'))
    }
    It 'escapa la comilla simple en el valor de una columna' {
        $row = @{ Codigo = 'ESC'; Nombre = "O'Brien" }
        $sql = New-UpdateStatement -Meta $script:meta -Row $row `
            -KeyColumns @('Codigo') -SetColumns @('Nombre')

        $sql | Should -Match ([regex]::Escape("SET [Nombre] = N'O''Brien'"))
    }
    It 'escapa la comilla simple tambien dentro del mensaje del PRINT' {
        # Sin escapar, una clave con comilla romperia el literal del PRINT.
        $row = @{ Codigo = "O'X"; Nombre = 'Algo' }
        $sql = New-UpdateStatement -Meta $script:meta -Row $row `
            -KeyColumns @('Codigo') -SetColumns @('Nombre')

        $sql | Should -Match ([regex]::Escape("WHERE [Codigo] = N'O''X';"))
        $sql | Should -Match ([regex]::Escape("PRINT N'AVISO: sin match en destino -> Codigo = O''X';"))
    }
    It 'emite NULL sin comillas para un valor nulo' {
        $row = @{ Codigo = 'ESC'; TDN2_Prompt = $null }
        $sql = New-UpdateStatement -Meta $script:meta -Row $row `
            -KeyColumns @('Codigo') -SetColumns @('TDN2_Prompt')

        $sql | Should -Match ([regex]::Escape('SET [TDN2_Prompt] = NULL'))
    }
}

Describe 'New-UpdateScript' {
    BeforeEach {
        $script:meta = New-TableMetaObject -Schema 'dbo' -Table 'CatalogoTdn1' `
            -Columns @('Id', 'Codigo', 'Nombre') `
            -IdentityCol 'Id' -ComputedCols @() -NullableCols @()
        $script:rows = @(
            @{ Codigo = 'ESC'; Nombre = 'Escrituras' },
            @{ Codigo = 'NOT'; Nombre = 'Notas simples' }
        )
    }

    It 'incluye la cabecera con origen, tabla, clave, columnas y recuento' {
        $sql = New-UpdateScript -Meta $script:meta -Rows $script:rows `
            -KeyColumns @('Codigo') -SetColumns @('Nombre') `
            -SourceLabel 'srbsqldevdocai/DocumentIA' -WhereClause '' -Timestamp '2026-07-16 10:22:31'

        $sql | Should -Match ([regex]::Escape('Origen : srbsqldevdocai/DocumentIA'))
        $sql | Should -Match ([regex]::Escape('Tabla  : [dbo].[CatalogoTdn1]'))
        $sql | Should -Match ([regex]::Escape('Clave  : [Codigo]'))
        $sql | Should -Match ([regex]::Escape('Set    : [Nombre]'))
        $sql | Should -Match ([regex]::Escape('Filas  : 2'))
        $sql | Should -Match ([regex]::Escape('Generado: 2026-07-16 10:22:31'))
        $sql | Should -Match ([regex]::Escape('Filtro : (ninguno)'))
    }
    It 'refleja el filtro -Where en la cabecera cuando se indica' {
        $sql = New-UpdateScript -Meta $script:meta -Rows $script:rows `
            -KeyColumns @('Codigo') -SetColumns @('Nombre') `
            -SourceLabel 'x/y' -WhereClause 'IsActive = 1' -Timestamp '2026-07-16 10:22:31'

        $sql | Should -Match ([regex]::Escape('Filtro : IsActive = 1'))
    }
    It 'advierte en la cabecera de que la semantica es solo UPDATE' {
        $sql = New-UpdateScript -Meta $script:meta -Rows $script:rows `
            -KeyColumns @('Codigo') -SetColumns @('Nombre') `
            -SourceLabel 'x/y' -WhereClause '' -Timestamp '2026-07-16 10:22:31'

        $sql | Should -Match ([regex]::Escape('SOLO UPDATE: no inserta ni borra filas.'))
    }
    It 'envuelve todo en una transaccion con XACT_ABORT' {
        $sql = New-UpdateScript -Meta $script:meta -Rows $script:rows `
            -KeyColumns @('Codigo') -SetColumns @('Nombre') `
            -SourceLabel 'x/y' -WhereClause '' -Timestamp '2026-07-16 10:22:31'

        $sql | Should -Match ([regex]::Escape('SET XACT_ABORT ON;'))
        $sql | Should -Match ([regex]::Escape('SET NOCOUNT ON;'))
        $sql | Should -Match ([regex]::Escape('BEGIN TRANSACTION;'))
        $sql | Should -Match ([regex]::Escape('COMMIT TRANSACTION;'))
    }
    It 'emite un UPDATE por cada fila' {
        $sql = New-UpdateScript -Meta $script:meta -Rows $script:rows `
            -KeyColumns @('Codigo') -SetColumns @('Nombre') `
            -SourceLabel 'x/y' -WhereClause '' -Timestamp '2026-07-16 10:22:31'

        ([regex]::Matches($sql, [regex]::Escape('UPDATE [dbo].[CatalogoTdn1]'))).Count | Should -Be 2
    }
    It 'nunca emite INSERT, DELETE ni MERGE' {
        $sql = New-UpdateScript -Meta $script:meta -Rows $script:rows `
            -KeyColumns @('Codigo') -SetColumns @('Nombre') `
            -SourceLabel 'x/y' -WhereClause '' -Timestamp '2026-07-16 10:22:31'

        $sql | Should -Not -Match ([regex]::Escape('INSERT INTO'))
        $sql | Should -Not -Match ([regex]::Escape('DELETE FROM'))
        $sql | Should -Not -Match ([regex]::Escape('MERGE'))
    }
    It 'genera un script valido aunque el origen no devuelva filas' {
        $sql = New-UpdateScript -Meta $script:meta -Rows @() `
            -KeyColumns @('Codigo') -SetColumns @('Nombre') `
            -SourceLabel 'x/y' -WhereClause '' -Timestamp '2026-07-16 10:22:31'

        $sql | Should -Match ([regex]::Escape('Filas  : 0'))
        $sql | Should -Match ([regex]::Escape('(el origen no devolvio filas; nada que actualizar)'))
        $sql | Should -Match ([regex]::Escape('COMMIT TRANSACTION;'))
    }
}

Describe 'Resolve-OutputPath' {
    It 'respeta la ruta indicada por el usuario' {
        Resolve-OutputPath -OutputFile 'c:/tmp/mio.sql' -Table 'CatalogoTdn1' -Timestamp '20260716-102231' |
            Should -Be 'c:/tmp/mio.sql'
    }
    It 'por defecto compone la ruta bajo artifacts/db-config' {
        $p = Resolve-OutputPath -OutputFile '' -Table 'CatalogoTdn1' -Timestamp '20260716-102231'
        $p | Should -BeLike '*artifacts*db-config*update_CatalogoTdn1_20260716-102231.sql'
    }
}

Describe 'Write-SqlFile' {
    BeforeEach {
        $script:dir  = Join-Path ([System.IO.Path]::GetTempPath()) "genupd-$([guid]::NewGuid())"
        $script:path = Join-Path $script:dir 'salida.sql'
    }
    AfterEach {
        if (Test-Path -LiteralPath $script:dir) {
            Remove-Item -LiteralPath $script:dir -Recurse -Force
        }
    }

    It 'crea el directorio destino si no existe' {
        Write-SqlFile -Path $script:path -Content 'SELECT 1;'
        Test-Path -LiteralPath $script:path | Should -BeTrue
    }
    It 'escribe el fichero con BOM de UTF-8' {
        # Sin BOM, sqlcmd aplica la code page actual y corrompe los acentos.
        Write-SqlFile -Path $script:path -Content "-- Clasificación`r`nSELECT 1;"
        $bytes = [System.IO.File]::ReadAllBytes($script:path)
        $bytes[0] | Should -Be 0xEF
        $bytes[1] | Should -Be 0xBB
        $bytes[2] | Should -Be 0xBF
    }
    It 'preserva los acentos al releer como UTF-8' {
        Write-SqlFile -Path $script:path -Content "-- Clasificación de documentos"
        [System.IO.File]::ReadAllText($script:path) | Should -BeLike '*Clasificación de documentos*'
    }
}

Describe 'Invoke-Main validacion de parametros' {
    It 'lanza si falta -Table' {
        { Invoke-Main } | Should -Throw '*-Table*'
    }
}
