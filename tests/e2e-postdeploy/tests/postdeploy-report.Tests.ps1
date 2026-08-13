BeforeAll {
    . (Join-Path $PSScriptRoot ".." "lib" "postdeploy-report.ps1")
}

Describe "New-E2EReport" {
    BeforeAll {
        $script:runInfo = [pscustomobject]@{
            Environment = "dev"; Profile = "smoke"; IncludeGdc = $false
            StartedAtUtc = "2026-08-13T10:00:00Z"; FinishedAtUtc = "2026-08-13T10:12:00Z"
        }
        $script:results = @(
            [pscustomobject]@{ CaseKey = "S-1"; Name = "caso uno"; Status = "PASS"; Reason = "OK"; Estado = "OK"; ElapsedSec = 42.1 },
            [pscustomobject]@{ CaseKey = "S-2"; Name = "caso dos"; Status = "FAIL"; Reason = "estado inesperado"; Estado = "ERROR"; ElapsedSec = 10.0 },
            [pscustomobject]@{ CaseKey = "G-1"; Name = "caso gdc"; Status = "NA"; Reason = "requiere gdc" }
        )
        $script:coverage = [pscustomobject]@{
            Items = @(
                [pscustomobject]@{ Id = "A-01"; Area = "A"; Descripcion = "a"; Estado = "cubierto-pass"; Casos = @("S-1") },
                [pscustomobject]@{ Id = "X-01"; Area = "X"; Descripcion = "x"; Estado = "sin-caso"; Casos = @() }
            )
            TotalMatriz = 2; Aplicables = 2; Cubiertos = 1; PorcentajeCobertura = 50.0
        }
        $script:out = New-E2EReport -RunInfo $script:runInfo -Results $script:results -Coverage $script:coverage -OutDir $TestDrive
    }
    It "genera report.md con resumen, casos y cobertura" {
        $md = Get-Content -Raw $script:out.ReportPath
        $md | Should -Match "PASS=1"
        $md | Should -Match "FAIL=1"
        $md | Should -Match "N/A=1"
        $md | Should -Match "50%|50\.0"
        $md | Should -Match "X-01"
        $md | Should -Match "estado inesperado"
    }
    It "genera coverage.json valido con items" {
        $json = Get-Content -Raw $script:out.CoverageJsonPath | ConvertFrom-Json
        $json.porcentajeCobertura | Should -Be 50.0
        @($json.items).Count | Should -Be 2
    }
}
