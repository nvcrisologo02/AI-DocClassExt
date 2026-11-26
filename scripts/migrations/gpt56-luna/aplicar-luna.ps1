#requires -Version 7
<#
.SYNOPSIS
    Aplica en PRE o PRO la configuracion de gpt-5.6-luna (filas, tarifa, cache, cutover y
    herencia del modelo de prompt) encadenando los scripts SQL de esta carpeta (AB#101570).

.DESCRIPTION
    ESTE SCRIPT ESCRIBE EN LA BASE DE DATOS del entorno indicado (salvo con -DryRun).

    Ejecutalo siempre ANTES del pipeline 832 con applyMissingConfig: si ConfigSeed diera de
    alta antes las filas luna de DEV, entrarian con UseAsFallback = true y quedarian dos
    fallbacks por Tipo (ADR-005). Las filas luna las crea este script, nunca ConfigSeed.

    Comprobacion previa (solo lectura, antes de escribir nada). Deben existir activas:
      classification.gpt5-mini (Tipo 0), prompt.gpt5-mini (Tipo 2),
      classification.gpt4o-mini-fallback (Tipo 0), extraction.gpt4o-mini-fallback (Tipo 1),
      default.gpt4o-mini_ex (Tipo 1) y la tipologia activa Codigo = IBI_1.1; y no puede haber
      tipologias con los objetos PromptConfig y promptConfig a la vez (el 08 lanza THROW 50053 y
      desharia todo tras aplicar el 09); ademas, las filas origen del 09 deben ser JSON valido
      con claves PascalCase, IBI_1.1 debe tener JSON valido y Extraction.ModelKey
      default.gpt4o-mini_ex o default.gpt56-luna_ex, y las unicas filas con UseAsFallback = true
      de Tipo 0 y Tipo 1 deben ser las gpt4o-mini-fallback (antes del cutover) o las gpt56-luna
      (ya aplicado); otra fila con el indicador haria fallar el 09 con THROW 50038.
      Si algo falla, lista lo que falta y termina con codigo 2 sin tocar nada.

    PRERREQUISITO QUE EL SCRIPT NO PUEDE COMPROBAR: ejecutalo solo tras el 832 del paso 1 en
    verde (el deployment gpt-5.6-luna debe existir; no se puede comprobar por ARM desde aqui).

    Orden fijo de los scripts (cada uno es idempotente, con backup solo si cambia, y se para en
    el primero que falle):
      01-dev-insert-gpt56-luna-rows.sql
      02-dev-tarifa-gpt56-luna.sql
      05-dev-tarifa-escritura-cache-gpt56-luna.sql
      03-dev-promptcache-on-gpt56-luna.sql
      09-cutover-gpt56-luna-por-codigo.sql
      08-dev-tipologias-heredan-modelo-prompt.sql

    Verificacion final: debe haber exactamente un UseAsFallback = true activo por Tipo 0 y por
    Tipo 1, que sean classification.gpt56-luna y extraction.gpt56-luna, y ninguna tipologia con default.gpt4o-mini como clave de prompt. Se imprimen ambas.

    En DEV no se usa: alli se ejecutan los scripts uno a uno con apply-sql-dev.ps1.

    Servidores (base DocumentIA):
      pre   srbsqlpredocai.database.windows.net
      prod  srbsqlprodocai.database.windows.net

    Requiere az login con permisos de lectura y escritura en la BD. Los scripts se aplican con
    apply-sql-dev.ps1 -PermitirNoDev y las consultas con query-sql.ps1.

    Codigo de salida: 0 todo correcto; 2 la comprobacion previa fallo (nada tocado);
    1 cualquier otro error (parametros, fallo de un script, verificacion final distinta de lo
    esperado).

.PARAMETER Environment
    Entorno: pre o prod. dev se rechaza.
.PARAMETER DryRun
    Solo lectura: ejecuta la comprobacion previa e imprime el servidor y el orden; no aplica nada.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/migrations/gpt56-luna/aplicar-luna.ps1 -Environment pre -DryRun
    pwsh -NoProfile -File ./scripts/migrations/gpt56-luna/aplicar-luna.ps1 -Environment pre
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$servidores = @{
    pre  = 'srbsqlpredocai.database.windows.net'
    prod = 'srbsqlprodocai.database.windows.net'
}
$orden = @(
    '01-dev-insert-gpt56-luna-rows.sql',
    '02-dev-tarifa-gpt56-luna.sql',
    '05-dev-tarifa-escritura-cache-gpt56-luna.sql',
    '03-dev-promptcache-on-gpt56-luna.sql',
    '09-cutover-gpt56-luna-por-codigo.sql',
    '08-dev-tipologias-heredan-modelo-prompt.sql'
)
# Tipo y Key de las filas activas que deben existir antes de aplicar.
$filasRequeridas = @(
    @{ Tipo = 0; Key = 'classification.gpt5-mini' },
    @{ Tipo = 2; Key = 'prompt.gpt5-mini' },
    @{ Tipo = 0; Key = 'classification.gpt4o-mini-fallback' },
    @{ Tipo = 1; Key = 'extraction.gpt4o-mini-fallback' },
    @{ Tipo = 1; Key = 'default.gpt4o-mini_ex' }
)
$dirBd = Join-Path $PSScriptRoot '..\..\database'

# Ejecuta una consulta de solo lectura con query-sql.ps1 y devuelve las filas como objetos.
function Invoke-ConsultaFilas {
    param([string]$Consulta, [string]$Servidor)
    $tmp = [IO.Path]::GetTempFileName()
    try {
        $null = & (Join-Path $dirBd 'query-sql.ps1') -Query $Consulta -Server $Servidor -OutJson $tmp
        $texto = [IO.File]::ReadAllText($tmp)
        if ([string]::IsNullOrWhiteSpace($texto)) { return @() }
        return @($texto | ConvertFrom-Json)
    }
    finally {
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    }
}

# Comprobacion previa: devuelve la lista de problemas (vacia si todo esta bien).
function Test-ComprobacionPrevia {
    param([string]$Servidor)
    $problemas = New-Object System.Collections.Generic.List[string]

    $claves = ($filasRequeridas | ForEach-Object { "N'" + $_.Key + "'" }) -join ', '
    $filas = Invoke-ConsultaFilas -Servidor $Servidor -Consulta "SELECT Tipo, [Key] FROM dbo.ModeloConfigs WHERE Activo = 1 AND [Key] IN ($claves)"
    foreach ($r in $filasRequeridas) {
        $hay = @($filas | Where-Object { $_.Tipo -eq $r.Tipo -and $_.Key -eq $r.Key }).Count
        if ($hay -eq 0) { $problemas.Add("Falta la fila activa $($r.Key) (Tipo $($r.Tipo)) en dbo.ModeloConfigs.") }
    }

    $ibi = Invoke-ConsultaFilas -Servidor $Servidor -Consulta "SELECT Id, Codigo FROM dbo.Tipologias WHERE Activa = 1 AND Codigo = N'IBI_1.1'"
    if ($ibi.Count -eq 0) { $problemas.Add("Falta la tipologia activa Codigo = IBI_1.1 en dbo.Tipologias.") }

    # Mismo predicado que el THROW 50053 del 08: los dos OBJETOS PromptConfig y promptConfig a la vez.
    $mixtas = Invoke-ConsultaFilas -Servidor $Servidor -Consulta @'
SELECT Id, Codigo FROM dbo.Tipologias
WHERE ISJSON(ConfiguracionJson) = 1
  AND JSON_QUERY(ConfiguracionJson, '$.PromptConfig') IS NOT NULL
  AND JSON_QUERY(ConfiguracionJson, '$.promptConfig') IS NOT NULL
'@
    foreach ($m in $mixtas) {
        $problemas.Add("Casing mixto: la tipologia Id $($m.Id) ($($m.Codigo)) tiene los objetos PromptConfig y promptConfig a la vez (el 08 lanzaria THROW 50053 y desharia todo).")
    }

    # Precondiciones del 09 sobre filas que ya existen: JSON valido y claves PascalCase.
    $casing = Invoke-ConsultaFilas -Servidor $Servidor -Consulta @'
SELECT [Key] FROM dbo.ModeloConfigs
WHERE Activo = 1
  AND ((Tipo = 0 AND [Key] = N'classification.gpt4o-mini-fallback')
    OR (Tipo = 1 AND [Key] = N'extraction.gpt4o-mini-fallback')
    OR (Tipo = 1 AND [Key] = N'default.gpt4o-mini_ex'))
  AND (ISJSON(ConfiguracionJson) <> 1
    OR ([Key] LIKE N'%fallback' AND JSON_VALUE(ConfiguracionJson, '$.UseAsFallback') IS NULL)
    OR ([Key] = N'default.gpt4o-mini_ex' AND JSON_VALUE(ConfiguracionJson, '$.DeploymentName') IS NULL))
'@
    foreach ($c in $casing) {
        $problemas.Add("La fila $($c.Key) no es JSON valido o no usa las claves PascalCase (UseAsFallback / DeploymentName) que exige el 09.")
    }

    # Precondicion del 09: Extraction.ModelKey de IBI_1.1 (cualquier casing) ya conocido.
    $mk = Invoke-ConsultaFilas -Servidor $Servidor -Consulta @'
SELECT Id, ISJSON(ConfiguracionJson) AS Valido,
       CASE WHEN ISJSON(ConfiguracionJson) = 1
            THEN COALESCE(JSON_VALUE(ConfiguracionJson, '$.extraction.modelKey'), JSON_VALUE(ConfiguracionJson, '$.Extraction.ModelKey')) END AS ModelKey
FROM dbo.Tipologias
WHERE Activa = 1 AND Codigo = N'IBI_1.1'
'@
    foreach ($x in $mk) {
        if ([int]$x.Valido -ne 1) {
            $problemas.Add("La tipologia IBI_1.1 (Id $($x.Id)) no tiene ConfiguracionJson valido (el 09 lanzaria THROW 50036).")
        }
        elseif ($null -eq $x.ModelKey -or $x.ModelKey.ToLowerInvariant() -notin @('default.gpt4o-mini_ex', 'default.gpt56-luna_ex')) {
            $problemas.Add("La tipologia IBI_1.1 (Id $($x.Id)) tiene Extraction.ModelKey '$($x.ModelKey)': el 09 solo admite default.gpt4o-mini_ex o default.gpt56-luna_ex.")
        }
    }

    # Unicidad del fallback: el 09 lanza THROW 50038 si, tras el cambio, no queda exactamente un
    # UseAsFallback por Tipo. Los unicos con el indicador deben ser el par gpt4o-mini (antes del
    # cutover) o el par luna (entorno ya en cutover o re-ejecucion).
    $banderas = Invoke-ConsultaFilas -Servidor $Servidor -Consulta @'
SELECT Tipo, [Key] FROM dbo.ModeloConfigs
WHERE Activo = 1 AND Tipo IN (0, 1)
  AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.UseAsFallback'), JSON_VALUE(ConfiguracionJson, '$.useAsFallback')) = 'true'
'@
    $permitidas = @(
        @{ Tipo = 0; Claves = @('classification.gpt4o-mini-fallback', 'classification.gpt56-luna') },
        @{ Tipo = 1; Claves = @('extraction.gpt4o-mini-fallback', 'extraction.gpt56-luna') }
    )
    foreach ($b in $banderas) {
        $ok = @($permitidas | Where-Object { $_.Tipo -eq [int]$b.Tipo -and $_.Claves -contains $b.Key }).Count -gt 0
        if (-not $ok) {
            $problemas.Add("La fila $($b.Key) (Tipo $($b.Tipo)) tiene UseAsFallback = true y no es gpt4o-mini-fallback ni gpt56-luna: el 09 fallaria con THROW 50038 tras aplicar 01, 02, 05 y 03.")
        }
    }
    return $problemas
}

try {
    if ($Environment -eq 'dev') {
        Write-Host "ERROR: -Environment dev no se admite: en DEV los scripts se ejecutan uno a uno con apply-sql-dev.ps1."
        exit 1
    }
    $servidor = $servidores[$Environment]
    $sufijo = if ($DryRun) { '  (DryRun)' } else { '' }
    Write-Host "Entorno: $Environment  Servidor: $servidor  Base de datos: DocumentIA$sufijo"

    Write-Host "Comprobacion previa (solo lectura)..."
    $problemas = Test-ComprobacionPrevia -Servidor $servidor
    if ($problemas.Count -gt 0) {
        Write-Host "COMPROBACION PREVIA FALLIDA: no se ha tocado nada."
        $problemas | ForEach-Object { Write-Host "  - $_" }
        exit 2
    }
    Write-Host "Comprobacion previa OK: filas requeridas, casing PascalCase de las filas origen, tipologia IBI_1.1 (JSON valido y Extraction.ModelKey), sin PromptConfig/promptConfig a la vez y fallbacks solo en el par gpt4o-mini o el par luna."

    Write-Host "Orden de aplicacion:"
    $n = 0
    foreach ($s in $orden) { $n++; Write-Host "  $n. $s" }

    if ($DryRun) {
        Write-Host "DryRun: 0 escrituras."
        exit 0
    }

    foreach ($s in $orden) {
        $ruta = Join-Path $PSScriptRoot $s
        Write-Host ""
        Write-Host "=== $s ==="
        & (Join-Path $dirBd 'apply-sql-dev.ps1') -ScriptPath $ruta -Server $servidor -PermitirNoDev
    }

    Write-Host ""
    Write-Host "=== Verificacion final ==="
    $fb = Invoke-ConsultaFilas -Servidor $servidor -Consulta @'
SELECT Tipo, COUNT(*) AS N FROM dbo.ModeloConfigs
WHERE Activo = 1
  AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.UseAsFallback'), JSON_VALUE(ConfiguracionJson, '$.useAsFallback')) = 'true'
GROUP BY Tipo
'@
    $n0 = [int](@($fb | Where-Object { $_.Tipo -eq 0 } | ForEach-Object { $_.N })[0])
    $n1 = [int](@($fb | Where-Object { $_.Tipo -eq 1 } | ForEach-Object { $_.N })[0])
    Write-Host "UseAsFallback activos: Tipo 0 = $n0, Tipo 1 = $n1 (esperado 1 y 1)"

    $g4 = Invoke-ConsultaFilas -Servidor $servidor -Consulta @'
SELECT COUNT(*) AS N FROM dbo.Tipologias
WHERE ISJSON(ConfiguracionJson) = 1
  AND (LOWER(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey')) = N'default.gpt4o-mini'
    OR LOWER(JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey')) = N'default.gpt4o-mini')
'@
    $nG4 = $g4[0].N
    Write-Host "Tipologias con default.gpt4o-mini como clave de prompt: $nG4 (esperado 0)"

    $flagLuna = Invoke-ConsultaFilas -Servidor $servidor -Consulta @'
SELECT Tipo, [Key] FROM dbo.ModeloConfigs
WHERE Activo = 1 AND Tipo IN (0, 1)
  AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.UseAsFallback'), JSON_VALUE(ConfiguracionJson, '$.useAsFallback')) = 'true'
'@
    $esLuna = (@($flagLuna | Where-Object { $_.Tipo -eq 0 -and $_.Key -eq 'classification.gpt56-luna' }).Count -eq 1) -and
              (@($flagLuna | Where-Object { $_.Tipo -eq 1 -and $_.Key -eq 'extraction.gpt56-luna' }).Count -eq 1)
    Write-Host ("Filas con UseAsFallback: " + ((@($flagLuna | ForEach-Object { "$($_.Key) (Tipo $($_.Tipo))" })) -join ', ') + " (esperado classification.gpt56-luna y extraction.gpt56-luna)")

    if ($n0 -ne 1 -or $n1 -ne 1 -or $nG4 -ne 0 -or -not $esLuna) {
        Write-Host "VERIFICACION FINAL FUERA DE LO ESPERADO: revisar el entorno."
        exit 1
    }
    Write-Host "Verificacion final OK."
    exit 0
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)"
    exit 1
}
