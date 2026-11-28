<#
.SYNOPSIS
    Restaura las tablas de configuracion desde sus copias <Tabla>__rel_<BuildId>.

.DESCRIPTION
    Comprueba que existen todas las copias (si falta alguna, exit 2). Por tabla lee de sys.columns,
    sys.indexes y sys.index_columns la clave primaria, las columnas (sin las calculadas) y si hay
    identity. Lee tambien las columnas de la copia y restaura solo las presentes en las dos (la foto es
    anterior a Migraciones, que puede anadir columnas a la tabla viva); si a la copia le falta una columna
    de la clave primaria, exit 2. Una columna nueva NOT NULL sin valor por defecto sigue haciendo fallar
    el INSERT de las filas borradas por la release: el fallo es ruidoso y hace rollback. Construye con New-ReleaseRestoreSql todas las pasadas 1 (UPDATE e INSERT) en el orden de
    -Tablas y despues todas las pasadas 2 (DELETE) en orden inverso, y las ejecuta en UNA transaccion
    (Invoke-ReleaseSqlTransaccion): o se restaura todo o no cambia nada. Asi la FK
    CatalogoTdn2.Tdn1Id -> CatalogoTdn1 nunca se rompe. Imprime actualizadas/insertadas/borradas por tabla
    y las escribe en restauracion-bd.json. Codigo 0 si todo va bien y 2 si falla una validacion.

.PARAMETER Server
    Servidor SQL (con o sin .database.windows.net).

.PARAMETER Database
    Base de datos.

.PARAMETER BuildId
    Identificador de la build usado al crear la foto.

.PARAMETER OutDir
    Carpeta donde se escribe restauracion-bd.json (se crea si no existe).

.PARAMETER Tablas
    Tablas a restaurar, en orden padre a hija. Por defecto CatalogoTdn1, CatalogoTdn2, Tipologias, ModeloConfigs, PromptTemplates.

.EXAMPLE
    pwsh -NoProfile -File ./scripts/release/Restore-ReleaseFotoBd.ps1 -Server srbsqlpredocai -Database DocumentIA -BuildId 12345 -OutDir ./out
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Server,
    [Parameter(Mandatory)][string]$Database,
    [Parameter(Mandatory)][string]$BuildId,
    [Parameter(Mandatory)][string]$OutDir,
    [string[]]$Tablas = @('CatalogoTdn1', 'CatalogoTdn2', 'Tipologias', 'ModeloConfigs', 'PromptTemplates')
)

$ErrorActionPreference = 'Stop'
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

foreach ($n in @($BuildId) + @($Tablas)) {
    if ($n -cnotmatch '^[A-Za-z0-9_]+$') {
        Write-Host "Nombre no valido (solo letras, digitos y guion bajo): '$n'"
        exit 2
    }
}

# (a) todas las copias deben existir
$faltan = @()
foreach ($t in $Tablas) {
    $id = Invoke-ReleaseSql -Server $Server -Database $Database -Scalar -Query 'SELECT OBJECT_ID(@Nombre)' -Parameters @{ Nombre = "dbo.[${t}__rel_$BuildId]" }
    if ($null -eq $id -or $id -is [System.DBNull]) { $faltan += "${t}__rel_$BuildId" }
}
if ($faltan.Count -gt 0) {
    Write-Host "Faltan copias, no se restaura nada: $($faltan -join ', ')"
    exit 2
}

# (b) metadatos por tabla
$queryMeta = 'SELECT c.name AS Columna, c.is_identity AS EsIdentity, c.is_computed AS EsCalculada, CASE WHEN ic.column_id IS NOT NULL THEN 1 ELSE 0 END AS EsClave ' +
    'FROM sys.columns c LEFT JOIN sys.indexes i ON i.object_id = c.object_id AND i.is_primary_key = 1 ' +
    'LEFT JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.column_id = c.column_id ' +
    'WHERE c.object_id = OBJECT_ID(@Tabla) ORDER BY c.column_id'
$sqlPorTabla = [ordered]@{}
foreach ($t in $Tablas) {
    $res = Invoke-ReleaseSql -Server $Server -Database $Database -Query $queryMeta -Parameters @{ Tabla = "dbo.[$t]" }
    $cols = if ($res -is [System.Data.DataTable]) { @($res.Rows) } else { @($res) }
    $validas = @($cols | Where-Object { -not [bool]$_.EsCalculada })
    $clave = @($validas | Where-Object { [int]$_.EsClave -eq 1 } | ForEach-Object { [string]$_.Columna })
    if ($clave.Count -eq 0) {
        Write-Host "La tabla $t no tiene clave primaria (o no existe): no se puede restaurar."
        exit 2
    }
    # La foto se hizo antes de Migraciones: una columna anadida despues no esta en la copia. Solo se
    # restauran las columnas presentes en las dos tablas; si falta una columna de la clave, no se puede.
    $resCopia = Invoke-ReleaseSql -Server $Server -Database $Database -Query $queryMeta -Parameters @{ Tabla = "dbo.[${t}__rel_$BuildId]" }
    $colsCopia = if ($resCopia -is [System.Data.DataTable]) { @($resCopia.Rows) } else { @($resCopia) }
    $enCopia = @($colsCopia | ForEach-Object { [string]$_.Columna })
    $claveFalta = @($clave | Where-Object { $enCopia -cnotcontains $_ })
    if ($claveFalta.Count -gt 0) {
        Write-Host "A la copia ${t}__rel_$BuildId le faltan columnas de la clave primaria ($($claveFalta -join ', ')): no se puede restaurar."
        exit 2
    }
    $comunes = @($validas | Where-Object { $enCopia -ccontains [string]$_.Columna })
    $nombres = @($comunes | ForEach-Object { [string]$_.Columna })
    $identity = @($comunes | Where-Object { [bool]$_.EsIdentity }).Count -gt 0
    $sqlPorTabla[$t] = New-ReleaseRestoreSql -Tabla $t -Copia "${t}__rel_$BuildId" -ClavePrimaria $clave -Columnas $nombres -TieneIdentity $identity
}

# (c) lista ordenada: pasadas 1 en el orden de -Tablas, pasadas 2 en orden inverso
$plan = [System.Collections.Generic.List[object]]::new()
foreach ($t in $Tablas) {
    if ($sqlPorTabla[$t].Pasada1Update) { $plan.Add([pscustomobject]@{ Tabla = $t; Tipo = 'actualizadas'; Sql = $sqlPorTabla[$t].Pasada1Update }) }
    $plan.Add([pscustomobject]@{ Tabla = $t; Tipo = 'insertadas'; Sql = $sqlPorTabla[$t].Pasada1Insert })
}
$inverso = @($Tablas)
[array]::Reverse($inverso)
foreach ($t in $inverso) { $plan.Add([pscustomobject]@{ Tabla = $t; Tipo = 'borradas'; Sql = $sqlPorTabla[$t].Pasada2Delete }) }

$cuentas = Invoke-ReleaseSqlTransaccion -Server $Server -Database $Database -Queries @($plan | ForEach-Object { $_.Sql })
$cuentas = @($cuentas)

# (d) filas por tabla y tipo
$resumen = [ordered]@{}
foreach ($t in $Tablas) { $resumen[$t] = [ordered]@{ actualizadas = 0; insertadas = 0; borradas = 0 } }
for ($i = 0; $i -lt $plan.Count; $i++) { $resumen[$plan[$i].Tabla][$plan[$i].Tipo] += [int]$cuentas[$i] }

$resultado = @()
foreach ($t in $Tablas) {
    $r = $resumen[$t]
    Write-Host "Restaurada ${t}: $($r.actualizadas) actualizadas, $($r.insertadas) insertadas, $($r.borradas) borradas"
    $resultado += [pscustomobject]@{ tabla = $t; actualizadas = $r.actualizadas; insertadas = $r.insertadas; borradas = $r.borradas }
}

# (e)
if (-not (Test-Path -LiteralPath $OutDir)) { $null = New-Item -ItemType Directory -Path $OutDir -Force }
ConvertTo-Json -InputObject @($resultado) -Depth 3 | Set-Content -LiteralPath (Join-Path $OutDir 'restauracion-bd.json') -Encoding utf8
exit 0
