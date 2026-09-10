<#
.SYNOPSIS
    Juego de pruebas de validacion de la cobertura de markdown. Solo DEV.
.DESCRIPTION
    Demuestra las invariantes MDW-01..MDW-09 contra un entorno real, incluyendo
    el estado que queda en base de datos. Cada caso limpia antes y despues, siembra
    su fila de partida con el pipeline, comprueba la precondicion, ejecuta sus
    pasadas en orden y asevera sobre la fila resultante.

    FAIL significa que la invariante esta violada. ERROR significa que el caso no
    pudo ejecutarse. La distincion es deliberada: una suite que informa FAIL cuando
    quiere decir "no pude conectar" ensena a ignorar los rojos.
.EXAMPLE
    pwsh ./tests/e2e-postdeploy/run-validacion-markdown.ps1 -Environment dev
    pwsh ./tests/e2e-postdeploy/run-validacion-markdown.ps1 -Environment dev -WhatIf
    pwsh ./tests/e2e-postdeploy/run-validacion-markdown.ps1 -Environment dev -SoloLimpieza
    pwsh ./tests/e2e-postdeploy/run-validacion-markdown.ps1 -Environment dev -CaseKey MDW-MDW1,MDW-MDW6
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet("dev")][string]$Environment,
    [switch]$WhatIf,
    [switch]$SoloLimpieza,
    [int]$MaxRetries = 60,
    [int]$DelaySeconds = 10,
    # Filtra el run a los caseKey indicados (por ejemplo, para relanzar solo
    # los casos afectados por una correccion sin repetir el juego completo).
    # Vacio por defecto: sin filtro, se ejecutan todos los casos.
    [string[]]$CaseKey = @()
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$repoRoot   = (Resolve-Path (Join-Path $scriptRoot ".." "..")).Path

. (Join-Path $repoRoot "tests" "api-tests" "documentia-e2e-common.ps1")
. (Join-Path $scriptRoot "lib" "postdeploy-config.ps1")
. (Join-Path $scriptRoot "lib" "postdeploy-coverage.ps1")
. (Join-Path $scriptRoot "lib" "postdeploy-report.ps1")
. (Join-Path $scriptRoot "lib" "postdeploy-db.ps1")
. (Join-Path $scriptRoot "lib" "postdeploy-estado.ps1")

# documentia-e2e-common.ps1 usa acceso dinamico a propiedades opcionales
# asumiendo modo no estricto, igual que en run-e2e-postdeploy.ps1.
Set-StrictMode -Off

try {
    $envConfig = Get-E2EEnvironment -ConfigPath (Join-Path $scriptRoot "config" "environments.json") -Environment $Environment
}
catch { Write-Host "[CONFIG] $($_.Exception.Message)" -ForegroundColor Red; exit 2 }

if ([string]::IsNullOrWhiteSpace($envConfig.FunctionKey)) {
    Write-Host "[CONFIG] functionKey vacia para '$Environment'" -ForegroundColor Red; exit 2
}
if ([string]::IsNullOrWhiteSpace($envConfig.SqlServer) -or [string]::IsNullOrWhiteSpace($envConfig.SqlDatabase)) {
    Write-Host "[CONFIG] falta sqlServer o sqlDatabase para '$Environment' en environments.json" -ForegroundColor Red; exit 2
}

$casesDir = Join-Path $scriptRoot "cases-validacion"
$cases = @(Get-ChildItem -Path $casesDir -Filter "*-cases.json" | ForEach-Object {
    Get-Content -Raw -Path $_.FullName | ConvertFrom-Json
} | ForEach-Object { $_ })

foreach ($case in $cases) {
    $case.documentPath = Join-Path $repoRoot $case.documentPath
}

if ($CaseKey.Count -gt 0) {
    $cases = @($cases | Where-Object { $CaseKey -contains $_.caseKey })
    if ($cases.Count -eq 0) { Write-Host "[CONFIG] ningun caso coincide con -CaseKey $($CaseKey -join ', ')" -ForegroundColor Red; exit 2 }
}

if ($WhatIf) {
    Write-Host ""
    Write-Host "PLAN (no se ejecuta nada, no se toca la base de datos)" -ForegroundColor Cyan
    foreach ($case in $cases) {
        $sha = Get-Sha256DeFichero -Ruta $case.documentPath
        Write-Host ""
        Write-Host "  [$($case.caseKey)] $($case.name)" -ForegroundColor Cyan
        Write-Host "    documento : $([System.IO.Path]::GetFileName($case.documentPath))  sha256=$($sha.Substring(0,16))..."
        Write-Host "    cubre     : $($case.covers -join ', ')"
        $mut = if ($null -eq $case.seed.mutacion) { "ninguna" } else { ($case.seed.mutacion.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ', ' }
        Write-Host "    siembra   : pipeline + mutacion($mut)"
        foreach ($p in @($case.pasadas)) { Write-Host "    pasada    : $($p.nombre)" }
        Write-Host "    fila      : $(@($case.dbAssertions).Count) aserciones"
    }
    Write-Host ""
    exit 0
}

$conexion = $null
try {
    $conexion = Connect-DocumentIADb -SqlServer $envConfig.SqlServer -SqlDatabase $envConfig.SqlDatabase
}
catch {
    Write-Host "[BD] $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

function Clear-CasoEnBd {
    param([pscustomobject]$Caso)
    $sha = Get-Sha256DeFichero -Ruta $Caso.documentPath
    return Remove-DocumentoPorSha256 -Connection $conexion -Sha256 $sha
}

# Las reglas del JSON nombran columnas de negocio; la instantanea guarda
# longitudes. Se traduce aqui para que el fichero de casos hable de columnas.
$mapaColumnas = @{
    "NormalizacionMarkdownGzip"       = "LongitudGzip"
    "NormalizacionMarkdownCompressed" = "LongitudCompressed"
}

function ConvertTo-CasoDePasada {
    param([pscustomobject]$Caso, [pscustomobject]$Request, [pscustomobject]$Assertions, [string]$Sufijo)

    # New-DocumentIARequestBody lee las opciones del objeto de caso plano, no de
    # un bloque anidado: hay que fusionar el request de la pasada en la raiz.
    $sintetico = [pscustomobject]@{
        caseKey = "$($Caso.caseKey)-$Sufijo"; group = $Caso.group; id = "$($Caso.id)$Sufijo"
        domain = $Caso.domain; name = "$($Caso.name) [$Sufijo]"
        documentPath = $Caso.documentPath; submittedBy = $Caso.submittedBy
        assertions = $Assertions
    }
    foreach ($prop in $Request.PSObject.Properties) {
        $sintetico | Add-Member -NotePropertyName $prop.Name -NotePropertyValue $prop.Value -Force
    }
    return $sintetico
}

function ConvertTo-SufijoPasadaSaneado {
    param([int]$Indice, [string]$Nombre)

    # El nombre de pasada es texto libre del fichero de casos (puede llevar
    # ":", espacios, etc.), pero el sufijo termina en un nombre de fichero de
    # artefacto (result-<caseKey>.json). Un caracter invalido en una ruta de
    # Windows trunca el nombre silenciosamente y el artefacto se pierde. Solo
    # se admite [A-Za-z0-9_-]; el resto se sustituye por "-". El indice de
    # posicion (1, 2, ...) va delante para que dos pasadas con el mismo nombre
    # saneado no colisionen en el mismo fichero.
    $saneado = ($Nombre -replace '[^A-Za-z0-9_-]', '-')
    return "p$Indice-$saneado"
}

function ConvertTo-ReglasDeInstantanea {
    param([array]$Reglas)
    return @($Reglas | ForEach-Object {
        $copia = @{}
        foreach ($p in $_.PSObject.Properties) { $copia[$p.Name] = $p.Value }
        if ($mapaColumnas.ContainsKey([string]$copia["columna"])) {
            $copia["columna"] = $mapaColumnas[[string]$copia["columna"]]
        }
        [pscustomobject]$copia
    })
}

function Invoke-CasoValidacion {
    param([pscustomobject]$Caso, [string]$Endpoint, [string]$RunDir)

    $resultadoBase = @{ CaseKey = $Caso.caseKey; Name = $Caso.name }
    $sha = Get-Sha256DeFichero -Ruta $Caso.documentPath

    # 1. Limpiar antes: los casos de reutilizacion necesitan partir sin fila, y
    #    los restos de un run cortado envenenarian este.
    [void](Remove-DocumentoPorSha256 -Connection $conexion -Sha256 $sha)

    # 2. Sembrar con el pipeline.
    $casoSiembra = ConvertTo-CasoDePasada -Caso $Caso -Request $Caso.seed.request `
        -Assertions ([pscustomobject]@{ expectedRuntimeStatus = "Completed" }) -Sufijo "seed"
    $rSiembra = Invoke-DocumentIAE2ECase -Case $casoSiembra -Endpoint $Endpoint -ArtifactsDir $RunDir `
        -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds -FunctionKey $envConfig.FunctionKey
    if ($rSiembra.Status -ne "PASS") {
        return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "siembra fallida: $($rSiembra.Reason)" })
    }

    # 3. Mutar la fila si el caso lo pide.
    if ($null -ne $Caso.seed.mutacion) {
        $mut = @{}
        foreach ($p in $Caso.seed.mutacion.PSObject.Properties) { $mut[$p.Name] = $p.Value }
        $filas = Invoke-DocumentoMutacion -Connection $conexion -Sha256 $sha -Mutacion $mut
        if ($filas -ne 1) {
            return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "la mutacion afecto a $filas filas, esperada 1" })
        }
    }

    # 4. Precondicion. Si no se cumple, no hubo estado de partida y el resultado
    #    de las pasadas no significaria nada: ERROR, no FAIL.
    $antes = Get-DocumentoSnapshot -Connection $conexion -Sha256 $sha

    # La siembra puede llegar a Completed sin dejar fila (dedup, ExpectedType no
    # resoluble, u otro corte temprano). Sin esta guarda, un caso sin
    # "precondicion" seguiria hasta el paso 6 y saldria FAIL "la fila no existe",
    # que en realidad es ERROR: no hubo estado de partida sobre el que demostrar
    # nada.
    if (-not $antes.Existe) {
        return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "la siembra completo pero no dejo fila en Documentos (dedup, ExpectedType no resoluble u otro corte temprano)" })
    }

    if ($null -ne $Caso.seed.precondicion) {
        $reglasPre = ConvertTo-ReglasDeInstantanea -Reglas @($Caso.seed.precondicion)
        $pre = Test-DbAssertions -Antes $antes -Despues $antes -Assertions $reglasPre
        if (-not $pre.Success) {
            return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "precondicion no cumplida: $($pre.Errors -join ' | ')" })
        }
    }

    # 5. Pasadas en orden.
    $pasadas = @($Caso.pasadas)
    for ($i = 0; $i -lt $pasadas.Count; $i++) {
        $pasada = $pasadas[$i]
        $sufijoPasada = ConvertTo-SufijoPasadaSaneado -Indice ($i + 1) -Nombre $pasada.nombre
        $casoPasada = ConvertTo-CasoDePasada -Caso $Caso -Request $pasada.request -Assertions $pasada.assertions -Sufijo $sufijoPasada
        $rPasada = Invoke-DocumentIAE2ECase -Case $casoPasada -Endpoint $Endpoint -ArtifactsDir $RunDir `
            -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds -FunctionKey $envConfig.FunctionKey
        if ($rPasada.Status -eq "SKIP") {
            return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "pasada '$($pasada.nombre)' omitida: $($rPasada.Reason)" })
        }
        # Invoke-DocumentIAE2ECase solo devuelve PASS/FAIL/SKIP: bajo su FAIL hay
        # tanto una asercion incumplida (invariante violada de verdad) como un
        # fallo de infraestructura (timeout con la orquestacion aun en
        # Running/Pending, o una excepcion HTTP/red). Get-EstadoDePasada separa
        # ambos leyendo el prefijo del Reason que la libreria ya produce, sin
        # tocar esa libreria (es compartida con run-e2e-postdeploy.ps1).
        $estadoPasada = Get-EstadoDePasada -Resultado $rPasada
        if ($estadoPasada -eq "ERROR") {
            return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "pasada '$($pasada.nombre)' fallo de infraestructura: $($rPasada.Reason)" })
        }
        if ($estadoPasada -ne "PASS") {
            return [pscustomobject]($resultadoBase + @{ Status = "FAIL"; Reason = "pasada '$($pasada.nombre)': $($rPasada.Reason)" })
        }
    }

    # 6. Aserciones sobre la fila.
    $despues = Get-DocumentoSnapshot -Connection $conexion -Sha256 $sha
    $reglas  = ConvertTo-ReglasDeInstantanea -Reglas @($Caso.dbAssertions)
    $db = Test-DbAssertions -Antes $antes -Despues $despues -Assertions $reglas
    if (-not $db.Success) {
        return [pscustomobject]($resultadoBase + @{ Status = "FAIL"; Reason = "fila: $($db.Errors -join ' | ')" })
    }

    return [pscustomobject]($resultadoBase + @{ Status = "PASS"; Reason = "OK" })
}

# Todo lo que sigue usa $conexion. Se envuelve en un try/finally cuyo finally
# cierra la conexion pase lo que pase (SoloLimpieza, error de configuracion
# tardio, o el camino normal hasta el informe): sin esto, una excepcion aqui
# dejaria la conexion abierta y, mas grave, el resto de casos sin ejecutar ni
# limpiar en el camino normal.
try {
    if ($SoloLimpieza) {
        $borradas = 0
        foreach ($case in $cases) { $borradas += Clear-CasoEnBd -Caso $case }
        Write-Host "Limpieza: $borradas filas borradas en Documentos (cascada incluida)." -ForegroundColor Yellow
        exit 0
    }

    $startedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    $runDir = Join-Path $scriptRoot "artifacts" ("{0}-{1}-validacion" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $Environment)
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null

    $endpoint = "$($envConfig.BaseUrl)/api/IngestDocument"
    Write-Host ""
    Write-Host "  Validacion de cobertura de markdown | Entorno: $Environment | Casos: $($cases.Count)" -ForegroundColor Cyan
    Write-Host "  Endpoint: $endpoint" -ForegroundColor Cyan
    Write-Host "  Artifacts: $runDir" -ForegroundColor Cyan

    $results = @()
    foreach ($case in $cases) {
        Write-Host ""
        Write-Host "  [$($case.caseKey)] $($case.name)" -ForegroundColor Cyan
        $r = $null
        try {
            $r = Invoke-CasoValidacion -Caso $case -Endpoint $endpoint -RunDir $runDir
        }
        catch {
            # Un fallo transitorio de BD (Invoke-DocumentoMutacion,
            # Get-DocumentoSnapshot, Remove-DocumentoPorSha256, Get-Sha256DeFichero
            # lanzan con $ErrorActionPreference = "Stop") no debe abortar el script
            # ni saltarse la limpieza de este caso ni los casos restantes.
            $r = [pscustomobject]@{ CaseKey = $case.caseKey; Name = $case.name; Status = "ERROR"; Reason = "Excepcion: $($_.Exception.Message)" }
        }
        finally {
            # Limpiar despues, pase lo que pase. Si la limpieza tambien falla (por
            # ejemplo la conexion se cayo), no debe enmascarar el resultado del
            # caso ni tirar el bucle: solo se avisa.
            try { [void](Clear-CasoEnBd -Caso $case) }
            catch { Write-Host "  [LIMPIEZA] no se pudo limpiar $($case.caseKey): $($_.Exception.Message)" -ForegroundColor Yellow }
        }
        $color = switch ($r.Status) { "PASS" { "Green" } "FAIL" { "Red" } "ERROR" { "Magenta" } default { "Yellow" } }
        Write-Host "  --> $($r.Status) : $($r.Reason)" -ForegroundColor $color
        $results += $r
    }

    $matrix    = Get-E2ECoverageMatrix -MatrixPath (Join-Path $scriptRoot "coverage" "functional-matrix.json")
    $matrixMdw = @($matrix | Where-Object { $_.area -eq "Markdown" })
    $coverage  = Get-E2ECoverage -Matrix $matrixMdw -Cases $cases -Results $results -ActiveConditions @()
    $runInfo   = [pscustomobject]@{
        Environment = $Environment; Profile = "validacion"; IncludeGdc = $false
        StartedAtUtc = $startedAtUtc; FinishedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }
    $out = New-E2EReport -RunInfo $runInfo -Results $results -Coverage $coverage -OutDir $runDir

    $pass  = @($results | Where-Object Status -eq "PASS").Count
    $fail  = @($results | Where-Object Status -eq "FAIL").Count
    # Ojo: $Error es variable automatica de PowerShell. No usarla como contador.
    $errores = @($results | Where-Object Status -eq "ERROR").Count
    Write-Host ""
    Write-Host "  RESUMEN: Total=$($results.Count) PASS=$pass FAIL=$fail ERROR=$errores" -ForegroundColor Cyan
    Write-Host "  Reporte: $($out.ReportPath)" -ForegroundColor Gray

    if ($fail -gt 0 -or $errores -gt 0) { exit 1 }
    exit 0
}
finally {
    # Cierra la conexion en cualquier salida de este bloque: SoloLimpieza, el
    # camino normal, o una excepcion no atrapada mas arriba (p.ej. al cargar la
    # matriz de cobertura o generar el informe).
    if ($null -ne $conexion -and $conexion.State -eq 'Open') { $conexion.Close() }
}
