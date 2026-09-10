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
}
