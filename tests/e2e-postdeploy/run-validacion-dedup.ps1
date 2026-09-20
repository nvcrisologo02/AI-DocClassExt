<#
.SYNOPSIS
    Juego de pruebas de validacion de la reutilizacion por duplicado. Solo DEV.
.DESCRIPTION
    Demuestra las invariantes DUP-01..DUP-05 (AB#100258) contra un entorno real:
    que una peticion repetida se sirve con el contrato de otra ejecucion, que esa
    peticion deja traza propia vinculada al original, que no contamina los
    agregados de operacion ni el coste del periodo, y que la traza lleva el
    InstanceId de la peticion que la provoco.

    Cada caso limpia antes y despues, siembra su ejecucion de partida con el
    pipeline, ejecuta sus pasadas en orden y asevera sobre las FILAS DE EJECUCION
    del documento (no sobre la fila de Documentos: la deduplicacion no la toca).

    FAIL significa que la invariante esta violada. ERROR significa que el caso no
    pudo ejecutarse. La distincion es deliberada: una suite que informa FAIL cuando
    quiere decir "no pude conectar" ensena a ignorar los rojos.
.EXAMPLE
    pwsh ./tests/e2e-postdeploy/run-validacion-dedup.ps1 -Environment dev
    pwsh ./tests/e2e-postdeploy/run-validacion-dedup.ps1 -Environment dev -WhatIf
    pwsh ./tests/e2e-postdeploy/run-validacion-dedup.ps1 -Environment dev -SoloLimpieza
    pwsh ./tests/e2e-postdeploy/run-validacion-dedup.ps1 -Environment dev -CaseKey DUP-02,DUP-05
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

# Desde pwsh -File (p. ej. Git Bash), "a,b,c" llega como un unico elemento; desde una sesion
# PowerShell llega ya partido. Se aceptan las dos formas.
$CaseKey = @($CaseKey | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

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

# Solo el fichero de dedup: el de markdown es otro dominio, con otras columnas y
# otra area de la matriz de cobertura.
$casesPath = Join-Path $scriptRoot "cases-validacion" "dedup-cases.json"
$cases = @(Get-Content -Raw -Path $casesPath | ConvertFrom-Json)

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
        Write-Host "    siembra   : pipeline (forceReprocess)"
        foreach ($p in @($case.pasadas)) { Write-Host "    pasada    : $($p.nombre)" }
        if ($case.tipo -eq "agregados") { Write-Host "    api       : $(@($case.apiAssertions).Count) deltas sobre agregados y costes" }
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

    # Mismo saneado que en run-validacion-markdown.ps1: el sufijo termina en el
    # nombre de un fichero de artefacto (result-<caseKey>.json) y un caracter
    # invalido en una ruta de Windows lo truncaria en silencio.
    $saneado = ($Nombre -replace '[^A-Za-z0-9_-]', '-')
    return "p$Indice-$saneado"
}

# Metricas del Monitor tal y como las ve Admin: los agregados de operacion y los
# costes del periodo, sobre la MISMA ventana en las dos lecturas. La ventana se
# fija por caso y arranca antes de la siembra, de modo que la ejecucion sembrada
# entre en las dos fotos y el delta solo refleje lo que hizo la pasada.
function Get-MetricasMonitor {
    param([string]$BaseUrl, [string]$FunctionKey, [datetime]$DesdeUtc, [datetime]$HastaUtc)

    $headers = @{ "x-functions-key" = $FunctionKey }
    $q = "desde=$([uri]::EscapeDataString($DesdeUtc.ToString('o')))&hasta=$([uri]::EscapeDataString($HastaUtc.ToString('o')))"

    $agregados = Invoke-RestMethod -Uri "$BaseUrl/api/management/ejecuciones/agregados?$q" -Headers $headers -ErrorAction Stop
    $costes    = Invoke-RestMethod -Uri "$BaseUrl/api/management/ejecuciones/costes?$q"    -Headers $headers -ErrorAction Stop

    return [pscustomobject]@{
        totalEjecuciones = [int]$agregados.totalEjecuciones
        ok               = [int]$agregados.ok
        reutilizadas     = [int]$agregados.reutilizadas
        costeEvitadoEur  = [decimal]$agregados.costeEvitadoEur
        costeTotalEur    = [decimal]$costes.costeTotalEur
    }
}

function Test-ApiAssertions {
    param([pscustomobject]$Antes, [pscustomobject]$Despues, [array]$Assertions = @())

    $errores = @()
    foreach ($regla in @($Assertions)) {
        if ($null -eq $regla) { continue }
        $metrica = [string]$regla.metrica
        if ([string]::IsNullOrWhiteSpace($metrica)) { continue }

        # Simetrico a la guarda de columnas de Test-DbAssertions: una metrica mal
        # escrita en el fichero de casos daria $null bajo StrictMode -Off y la regla
        # pasaria sin comprobar nada.
        if (@($Despues.PSObject.Properties.Name) -notcontains $metrica) {
            $errores += "metrica desconocida '$metrica'"
            continue
        }

        # Las metricas de coste son decimales: se redondea a 6 decimales, que es la
        # escala de CosteIAEur en base de datos.
        $delta = [math]::Round([decimal]$Despues.$metrica - [decimal]$Antes.$metrica, 6)
        $esperado = [math]::Round([decimal]$regla.delta, 6)
        if ($delta -ne $esperado) {
            $errores += "$metrica cambio en $delta (antes=$($Antes.$metrica) despues=$($Despues.$metrica)), esperado $esperado"
        }
    }
    return [pscustomobject]@{ Success = ($errores.Count -eq 0); Errors = $errores }
}

function Invoke-CasoValidacion {
    param([pscustomobject]$Caso, [string]$Endpoint, [string]$RunDir)

    $resultadoBase = @{ CaseKey = $Caso.caseKey; Name = $Caso.name }
    $sha = Get-Sha256DeFichero -Ruta $Caso.documentPath
    $esDeAgregados = ($Caso.tipo -eq "agregados")

    # La ventana de los agregados empieza antes de la siembra para que la ejecucion
    # sembrada cuente en las dos lecturas.
    $desdeUtc = (Get-Date).ToUniversalTime().AddMinutes(-5)

    # 1. Limpiar antes: estos casos cuentan filas de ejecucion, asi que los restos
    #    de un run cortado falsearian todos los totales.
    [void](Remove-DocumentoPorSha256 -Connection $conexion -Sha256 $sha)

    # 2. Sembrar con el pipeline: una ejecucion real, con contrato, que sera la
    #    original de la reutilizacion.
    $asercionesSiembra = [pscustomobject]@{ expectedRuntimeStatus = "Completed" }
    if ($null -ne $Caso.seed.assertions) { $asercionesSiembra = $Caso.seed.assertions }
    $casoSiembra = ConvertTo-CasoDePasada -Caso $Caso -Request $Caso.seed.request -Assertions $asercionesSiembra -Sufijo "seed"
    $rSiembra = Invoke-DocumentIAE2ECase -Case $casoSiembra -Endpoint $Endpoint -ArtifactsDir $RunDir `
        -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds -FunctionKey $envConfig.FunctionKey
    if ($rSiembra.Status -ne "PASS") {
        return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "siembra fallida: $($rSiembra.Reason)" })
    }

    # 3. Precondicion: la siembra tiene que haber dejado exactamente una ejecucion
    #    propia y con contrato. Si no, no hay original que reutilizar y el
    #    resultado de las pasadas no significaria nada: ERROR, no FAIL.
    $antes = Get-EjecucionesSnapshot -Connection $conexion -Sha256 $sha
    if (-not $antes.Existe) {
        return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "la siembra completo pero no dejo ninguna fila en DocumentoEjecuciones (ExpectedType no resoluble u otro corte temprano)" })
    }
    if ($antes.Total -ne 1 -or $antes.Reutilizadas -ne 0) {
        return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "la siembra dejo Total=$($antes.Total) Reutilizadas=$($antes.Reutilizadas); se esperaba una unica ejecucion propia" })
    }
    if (-not $antes.UltimaTieneContrato) {
        return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "la ejecucion sembrada quedo sin contrato: no hay nada que reutilizar" })
    }
    $idSiembra = $antes.UltimaId

    # 4. Foto de las metricas del Monitor antes de la pasada, si el caso las mide.
    $metricasAntes = $null
    if ($esDeAgregados) {
        try {
            $metricasAntes = Get-MetricasMonitor -BaseUrl $envConfig.BaseUrl -FunctionKey $envConfig.FunctionKey `
                -DesdeUtc $desdeUtc -HastaUtc (Get-Date).ToUniversalTime().AddMinutes(5)
        }
        catch {
            return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "no se pudieron leer los agregados antes de la pasada: $($_.Exception.Message)" })
        }
    }

    # 5. Pasadas en orden. Se guarda el InstanceId de la ultima: DUP-05 asevera
    #    que la fila de reutilizacion lleva el de la peticion que la provoco.
    # DUP-01 no tiene pasadas: la propia siembra es la accion que se asevera.
    $instanceIdUltimaPasada = ""
    $pasadas = @($Caso.pasadas)
    for ($indice = 0; $indice -lt $pasadas.Count; $indice++) {
        $pasada = $pasadas[$indice]
        $sufijoPasada = ConvertTo-SufijoPasadaSaneado -Indice ($indice + 1) -Nombre $pasada.nombre
        $casoPasada = ConvertTo-CasoDePasada -Caso $Caso -Request $pasada.request -Assertions $pasada.assertions -Sufijo $sufijoPasada
        $rPasada = Invoke-DocumentIAE2ECase -Case $casoPasada -Endpoint $Endpoint -ArtifactsDir $RunDir `
            -MaxRetries $MaxRetries -DelaySeconds $DelaySeconds -FunctionKey $envConfig.FunctionKey
        if ($rPasada.Status -eq "SKIP") {
            return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "pasada '$($pasada.nombre)' omitida: $($rPasada.Reason)" })
        }
        # Invoke-DocumentIAE2ECase solo devuelve PASS/FAIL/SKIP: bajo su FAIL hay
        # tanto una asercion incumplida como un fallo de infraestructura.
        # Get-EstadoDePasada los separa leyendo el prefijo del Reason.
        $estadoPasada = Get-EstadoDePasada -Resultado $rPasada
        if ($estadoPasada -eq "ERROR") {
            return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "pasada '$($pasada.nombre)' fallo de infraestructura: $($rPasada.Reason)" })
        }
        if ($estadoPasada -ne "PASS") {
            return [pscustomobject]($resultadoBase + @{ Status = "FAIL"; Reason = "pasada '$($pasada.nombre)': $($rPasada.Reason)" })
        }
        if (-not [string]::IsNullOrWhiteSpace($rPasada.InstanceId)) { $instanceIdUltimaPasada = $rPasada.InstanceId }
    }

    # 6. Aserciones sobre las filas de ejecucion.
    $despues = Get-EjecucionesSnapshot -Connection $conexion -Sha256 $sha -InstanceId $instanceIdUltimaPasada -IdSiembra $idSiembra
    if (-not $despues.Existe) {
        return [pscustomobject]($resultadoBase + @{ Status = "FAIL"; Reason = "no queda ninguna fila en DocumentoEjecuciones al terminar las pasadas" })
    }
    $db = Test-DbAssertions -Antes $antes -Despues $despues -Assertions @($Caso.dbAssertions)
    if (-not $db.Success) {
        return [pscustomobject]($resultadoBase + @{ Status = "FAIL"; Reason = "filas: $($db.Errors -join ' | ')" })
    }

    # 7. Deltas de las metricas del Monitor.
    if ($esDeAgregados) {
        $metricasDespues = $null
        try {
            $metricasDespues = Get-MetricasMonitor -BaseUrl $envConfig.BaseUrl -FunctionKey $envConfig.FunctionKey `
                -DesdeUtc $desdeUtc -HastaUtc (Get-Date).ToUniversalTime().AddMinutes(5)
        }
        catch {
            return [pscustomobject]($resultadoBase + @{ Status = "ERROR"; Reason = "no se pudieron leer los agregados tras la pasada: $($_.Exception.Message)" })
        }
        $api = Test-ApiAssertions -Antes $metricasAntes -Despues $metricasDespues -Assertions @($Caso.apiAssertions)
        if (-not $api.Success) {
            return [pscustomobject]($resultadoBase + @{ Status = "FAIL"; Reason = "agregados: $($api.Errors -join ' | ')" })
        }
    }

    return [pscustomobject]($resultadoBase + @{ Status = "PASS"; Reason = "OK" })
}

# Todo lo que sigue usa $conexion. El finally la cierra pase lo que pase, para que
# ni SoloLimpieza ni un error tardio dejen la conexion abierta ni los casos
# restantes sin ejecutar ni limpiar.
try {
    if ($SoloLimpieza) {
        $borradas = 0
        foreach ($case in $cases) { $borradas += Clear-CasoEnBd -Caso $case }
        Write-Host "Limpieza: $borradas filas borradas en Documentos (cascada incluida)." -ForegroundColor Yellow
        exit 0
    }

    $startedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    $runDir = Join-Path $scriptRoot "artifacts" ("{0}-{1}-validacion-dedup" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $Environment)
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null

    $endpoint = "$($envConfig.BaseUrl)/api/IngestDocument"
    Write-Host ""
    Write-Host "  Validacion de reutilizacion por duplicado | Entorno: $Environment | Casos: $($cases.Count)" -ForegroundColor Cyan
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
            # Un fallo transitorio de BD o de red no debe abortar el script ni
            # saltarse la limpieza de este caso ni los casos restantes.
            $r = [pscustomobject]@{ CaseKey = $case.caseKey; Name = $case.name; Status = "ERROR"; Reason = "Excepcion: $($_.Exception.Message)" }
        }
        finally {
            try { [void](Clear-CasoEnBd -Caso $case) }
            catch { Write-Host "  [LIMPIEZA] no se pudo limpiar $($case.caseKey): $($_.Exception.Message)" -ForegroundColor Yellow }
        }
        $color = switch ($r.Status) { "PASS" { "Green" } "FAIL" { "Red" } "ERROR" { "Magenta" } default { "Yellow" } }
        Write-Host "  --> $($r.Status) : $($r.Reason)" -ForegroundColor $color
        $results += $r
    }

    $matrix    = Get-E2ECoverageMatrix -MatrixPath (Join-Path $scriptRoot "coverage" "functional-matrix.json")
    $matrixDup = @($matrix | Where-Object { $_.area -eq "Dedup" })
    $coverage  = Get-E2ECoverage -Matrix $matrixDup -Cases $cases -Results $results -ActiveConditions @()
    $runInfo   = [pscustomobject]@{
        Environment = $Environment; Profile = "validacion-dedup"; IncludeGdc = $false
        StartedAtUtc = $startedAtUtc; FinishedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }
    $out = New-E2EReport -RunInfo $runInfo -Results $results -Coverage $coverage -OutDir $runDir

    $pass  = @($results | Where-Object Status -eq "PASS").Count
    $fail  = @($results | Where-Object Status -eq "FAIL").Count
    # Ojo: $Error es variable automatica de PowerShell. No usarla como contador.
    $errores = @($results | Where-Object Status -eq "ERROR").Count
    $lineaResumen = "RESUMEN: Total=$($results.Count) PASS=$pass FAIL=$fail ERROR=$errores"

    # New-E2EReport no distingue ERROR de FAIL en su cabecera ni en su tabla. Se
    # anade esta linea aparte para que el artefacto que perdura no ensene un verde
    # enganoso, igual que en run-validacion-markdown.ps1.
    Add-Content -Path $out.ReportPath -Value "`n$lineaResumen`n" -Encoding UTF8

    Write-Host ""
    Write-Host "  $lineaResumen" -ForegroundColor Cyan
    Write-Host "  Reporte: $($out.ReportPath)" -ForegroundColor Gray

    if ($fail -gt 0 -or $errores -gt 0) { exit 1 }
    exit 0
}
finally {
    if ($null -ne $conexion -and $conexion.State -eq 'Open') { $conexion.Close() }
}
