BeforeAll {
    . (Join-Path $PSScriptRoot ".." "lib" "postdeploy-config.ps1")
    $script:samplePath = Join-Path $PSScriptRoot ".." "config" "environments.sample.json"
}

Describe "Get-E2EEnvironment" {
    It "carga el entorno dev desde el sample" {
        $env = Get-E2EEnvironment -ConfigPath $script:samplePath -Environment "dev"
        $env.Name | Should -Be "dev"
        $env.BaseUrl | Should -Be "https://srbappdevdocai.azurewebsites.net"
        $env.HealthCheck | Should -BeTrue
    }
    It "normaliza baseUrl sin slash final" {
        $tmp = Join-Path $TestDrive "envs.json"
        '{"x":{"baseUrl":"https://host/","functionKey":"k"}}' | Set-Content $tmp
        (Get-E2EEnvironment -ConfigPath $tmp -Environment "x").BaseUrl | Should -Be "https://host"
    }
    It "falla si el entorno no existe" {
        { Get-E2EEnvironment -ConfigPath $script:samplePath -Environment "nope" } | Should -Throw "*no definido*"
    }
    It "falla si no existe el fichero" {
        { Get-E2EEnvironment -ConfigPath (Join-Path $TestDrive "no.json") -Environment "dev" } | Should -Throw "*environments.sample.json*"
    }
    It "healthCheck=false se respeta (local)" {
        (Get-E2EEnvironment -ConfigPath $script:samplePath -Environment "local").HealthCheck | Should -BeFalse
    }
    It "falla con mensaje claro (no excepcion cruda) si el JSON esta malformado" {
        $tmp = Join-Path $TestDrive "malformed.json"
        '{ "dev": { "baseUrl": ' | Set-Content $tmp
        { Get-E2EEnvironment -ConfigPath $tmp -Environment "dev" } | Should -Throw "*no contiene JSON valido*"
    }
    It "falla con mensaje claro si el entorno esta presente pero es null" {
        $tmp = Join-Path $TestDrive "nullenv.json"
        '{ "dev": null }' | Set-Content $tmp
        { Get-E2EEnvironment -ConfigPath $tmp -Environment "dev" } | Should -Throw "*vacio*"
    }
}
