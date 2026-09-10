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
