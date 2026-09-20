Set-StrictMode -Version Latest

# Unico servidor SQL admitido. El ValidateSet del runner protege el parametro;
# esta comprobacion protege la configuracion, que vive en un environments.json
# gitignored y editado a mano. Este modulo emite DELETE.
$script:ServidorDevEsperado = "srbsqldevdocai"

function Assert-DbServidorEsDev {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$SqlServer)

    if ([string]::IsNullOrWhiteSpace($SqlServer)) {
        throw "sqlServer vacio en environments.json. Este juego de pruebas solo opera contra DEV."
    }
    $valor = $SqlServer.Trim()
    $fqdnEsperado = "$script:ServidorDevEsperado.database.windows.net"
    if ($valor -ne $script:ServidorDevEsperado -and $valor -ne $fqdnEsperado) {
        throw "Servidor '$SqlServer' no es el de DEV ($script:ServidorDevEsperado). Abortado: este script emite DELETE."
    }
}

# Construye la cadena de conexion con SqlConnectionStringBuilder en vez de
# concatenar texto. La concatenacion dejaba una via de fuga: en una cadena
# ADO.NET, si una clave se repite gana la ultima, asi que un SqlDatabase con
# ";Server=..." embebido (via un environments.json con una errata o manipulado)
# redirigiria la conexion real a otro servidor. El builder escapa el valor
# (lo entrecomilla si contiene ';' o '"'), asi que ese contenido llega siempre
# como Initial Catalog literal, nunca como una clave nueva.
function New-DocumentIAConnectionString {
    param(
        [Parameter(Mandatory = $true)][string]$SqlServer,
        [Parameter(Mandatory = $true)][string]$SqlDatabase
    )

    $fqdn = if ($SqlServer -like "*.database.windows.net") { $SqlServer } else { "$SqlServer.database.windows.net" }
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder["Data Source"] = "tcp:$fqdn,1433"
    $builder["Initial Catalog"] = $SqlDatabase
    $builder["Encrypt"] = $true
    $builder["TrustServerCertificate"] = $false
    $builder["Connect Timeout"] = 30
    return $builder.ConnectionString
}

function Connect-DocumentIADb {
    param(
        [Parameter(Mandatory = $true)][string]$SqlServer,
        [Parameter(Mandatory = $true)][string]$SqlDatabase
    )

    # Primera barrera: verifica la CONFIGURACION (sqlServer en environments.json).
    Assert-DbServidorEsDev -SqlServer $SqlServer

    $token = (az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv)
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "No se obtuvo token de az. Ejecuta 'az login' desde red corporativa."
    }

    $cn = New-Object System.Data.SqlClient.SqlConnection
    $cn.ConnectionString = New-DocumentIAConnectionString -SqlServer $SqlServer -SqlDatabase $SqlDatabase
    $cn.AccessToken = $token
    $cn.Open()

    # Segunda barrera, independiente de la primera: verifica el SERVIDOR
    # REALMENTE CONECTADO, no el texto de configuracion. SERVERPROPERTY('ServerName')
    # en Azure SQL devuelve el nombre logico del servidor (verificado contra DEV:
    # "srbsqldevdocai", sin sufijo). Si algo (una redireccion via Initial Catalog
    # que burlase el escapado del builder, un environments.json manipulado en
    # tiempo de ejecucion, etc.) hiciera que la conexion real fuese a otro
    # servidor, esta comprobacion lo detiene antes de la primera sentencia DELETE.
    $cmdServidor = $cn.CreateCommand()
    $cmdServidor.CommandText = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(256))"
    $servidorConectado = [string]$cmdServidor.ExecuteScalar()
    try {
        Assert-DbServidorEsDev -SqlServer $servidorConectado
    }
    catch {
        $cn.Close()
        throw
    }

    return $cn
}

function Get-DocumentoSnapshot {
    param(
        [Parameter(Mandatory = $true)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256
    )

    $sql = @"
SELECT TOP 1
    MarkdownPaginas,
    CAST(MarkdownCompleto AS int)                          AS MarkdownCompleto,
    ISNULL(DATALENGTH(NormalizacionMarkdownGzip), 0)       AS LongitudGzip,
    ISNULL(DATALENGTH(NormalizacionMarkdownCompressed), 0) AS LongitudCompressed,
    Paginas
FROM Documentos
WHERE SHA256 = @sha
"@

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    $lector = $cmd.ExecuteReader()
    try {
        if (-not $lector.Read()) {
            return [pscustomobject]@{
                Existe = $false; MarkdownPaginas = $null; MarkdownCompleto = $false
                LongitudGzip = 0; LongitudCompressed = 0; Paginas = 0
            }
        }
        $paginas = if ($lector["MarkdownPaginas"] -is [System.DBNull]) { $null } else { [int]$lector["MarkdownPaginas"] }
        return [pscustomobject]@{
            Existe             = $true
            MarkdownPaginas    = $paginas
            MarkdownCompleto   = ([int]$lector["MarkdownCompleto"] -eq 1)
            LongitudGzip       = [int]$lector["LongitudGzip"]
            LongitudCompressed = [int]$lector["LongitudCompressed"]
            Paginas            = [int]$lector["Paginas"]
        }
    }
    finally { $lector.Close() }
}

# AB#100258: instantanea de las EJECUCIONES de un documento, no de su fila en
# Documentos. La deduplicacion no toca Documentos (ese es justo su punto): lo que
# hay que observar es cuantas filas de ejecucion existen, cual es la ultima y si
# esa ultima es una reutilizacion con su vinculo al original.
#
# InstanceId e IdSiembra son opcionales porque no todos los casos los tienen: las
# columnas que dependen de ellos salen a 0 / $false en vez de omitirse, para que
# Test-DbAssertions siga rechazando por "columna desconocida" solo lo que de
# verdad esta mal escrito en el fichero de casos.
function Get-EjecucionesSnapshot {
    param(
        [Parameter(Mandatory = $true)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256,
        [string]$InstanceId = "",
        [AllowNull()][object]$IdSiembra = $null
    )

    $sql = @"
WITH Ej AS (
    SELECT e.Id, e.InstanceId, e.EstadoFinal, e.FechaEjecucion,
           e.ReutilizadaPorDuplicado, e.EjecucionOriginalId,
           e.ContratoSalidaCompletoJson, e.CosteIAEur
    FROM dbo.DocumentoEjecuciones e
    INNER JOIN dbo.Documentos d ON d.Id = e.DocumentoId
    WHERE d.SHA256 = @sha
)
SELECT
    (SELECT COUNT(*) FROM Ej)                                   AS Total,
    (SELECT COUNT(*) FROM Ej WHERE ReutilizadaPorDuplicado = 1)  AS Reutilizadas,
    (SELECT COUNT(*) FROM Ej WHERE ReutilizadaPorDuplicado = 0)  AS Propias,
    (SELECT COUNT(*) FROM Ej
      WHERE @instanceId IS NOT NULL AND InstanceId = @instanceId) AS FilasConInstanceIdPasada,
    (SELECT COUNT(*) FROM Ej
      WHERE @instanceId IS NOT NULL AND InstanceId = @instanceId
        AND ReutilizadaPorDuplicado = 1)                         AS ReutilizadasConInstanceIdPasada,
    u.Id                                                         AS UltimaId,
    u.InstanceId                                                 AS UltimaInstanceId,
    u.EstadoFinal                                                AS UltimaEstadoFinal,
    CAST(u.ReutilizadaPorDuplicado AS int)                       AS UltimaReutilizadaInt,
    u.EjecucionOriginalId                                        AS UltimaEjecucionOriginalId,
    CASE WHEN u.ContratoSalidaCompletoJson IS NULL THEN 0 ELSE 1 END AS UltimaTieneContratoInt,
    CASE WHEN u.CosteIAEur IS NULL THEN 1 ELSE 0 END             AS UltimaCosteEsNuloInt
FROM (SELECT TOP 1 * FROM Ej ORDER BY FechaEjecucion DESC, Id DESC) u
"@

    $vacia = [pscustomobject]@{
        Existe = $false; Total = 0; Reutilizadas = 0; Propias = 0
        FilasConInstanceIdPasada = 0; ReutilizadasConInstanceIdPasada = 0
        UltimaId = $null; UltimaInstanceId = $null; UltimaEstadoFinal = $null
        UltimaReutilizada = $false; UltimaEjecucionOriginalId = $null
        UltimaTieneContrato = $false; UltimaCosteEsNulo = $false
        UltimaApuntaASiembra = $false
    }

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    $valorInstancia = if ([string]::IsNullOrWhiteSpace($InstanceId)) { [System.DBNull]::Value } else { $InstanceId }
    [void]$cmd.Parameters.AddWithValue("@instanceId", $valorInstancia)

    $lector = $cmd.ExecuteReader()
    try {
        if (-not $lector.Read()) { return $vacia }

        $originalId = if ($lector["UltimaEjecucionOriginalId"] -is [System.DBNull]) { $null } else { [int]$lector["UltimaEjecucionOriginalId"] }
        $instanciaUltima = if ($lector["UltimaInstanceId"] -is [System.DBNull]) { $null } else { [string]$lector["UltimaInstanceId"] }

        # El vinculo se compara aqui y no en SQL: IdSiembra lo conoce el runner (es
        # el Id que dejo la siembra), no la base de datos. Sin siembra conocida la
        # columna vale $false, que es lo unico honesto que se puede afirmar.
        $apuntaASiembra = $false
        if ($null -ne $IdSiembra -and $null -ne $originalId) {
            $apuntaASiembra = ([int]$IdSiembra -eq $originalId)
        }

        return [pscustomobject]@{
            Existe                          = $true
            Total                           = [int]$lector["Total"]
            Reutilizadas                    = [int]$lector["Reutilizadas"]
            Propias                         = [int]$lector["Propias"]
            FilasConInstanceIdPasada        = [int]$lector["FilasConInstanceIdPasada"]
            ReutilizadasConInstanceIdPasada = [int]$lector["ReutilizadasConInstanceIdPasada"]
            UltimaId                        = [int]$lector["UltimaId"]
            UltimaInstanceId                = $instanciaUltima
            UltimaEstadoFinal               = [string]$lector["UltimaEstadoFinal"]
            UltimaReutilizada               = ([int]$lector["UltimaReutilizadaInt"] -eq 1)
            UltimaEjecucionOriginalId       = $originalId
            UltimaTieneContrato             = ([int]$lector["UltimaTieneContratoInt"] -eq 1)
            UltimaCosteEsNulo               = ([int]$lector["UltimaCosteEsNuloInt"] -eq 1)
            UltimaApuntaASiembra            = $apuntaASiembra
        }
    }
    finally { $lector.Close() }
}

# Lista blanca de columnas mutables. Cualquier otra se rechaza: el bloque
# "mutacion" viene de un fichero JSON y no debe poder construir SQL arbitrario.
$script:ColumnasMutables = @("MarkdownPaginas", "MarkdownCompleto", "Paginas")

function Get-Sha256DeFichero {
    param([Parameter(Mandatory = $true)][string]$Ruta)

    if (-not (Test-Path -Path $Ruta)) { throw "No existe el fichero: $Ruta" }
    $sha    = [System.Security.Cryptography.SHA256]::Create()
    $stream = $null
    try {
        $stream = [System.IO.File]::OpenRead((Resolve-Path -Path $Ruta).Path)
        return ([System.BitConverter]::ToString($sha.ComputeHash($stream)) -replace '-', '').ToUpperInvariant()
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
        $sha.Dispose()
    }
}

function New-MutacionSql {
    param([hashtable]$Mutacion)

    if ($null -eq $Mutacion -or $Mutacion.Keys.Count -eq 0) { return $null }

    $asignaciones = @()
    foreach ($columna in $Mutacion.Keys) {
        if ($script:ColumnasMutables -notcontains $columna) {
            throw "Columna '$columna' no admitida en mutacion. Admitidas: $($script:ColumnasMutables -join ', ')"
        }
        $asignaciones += "$columna = @$columna"
    }
    return "UPDATE Documentos SET $($asignaciones -join ', ') WHERE SHA256 = @sha"
}

function Invoke-DocumentoMutacion {
    param(
        # AllowNull sobre el tipo fuerte: la guarda de Sha256 de abajo debe poder
        # probarse sin abrir conexion real, y un parametro Mandatory sin AllowNull
        # rechaza $null en el enlazado antes de que el cuerpo de la funcion se
        # ejecute. AllowNull permite pasar $null en el test sin renunciar a que
        # cualquier otro tipo distinto de SqlConnection siga rechazandose ahi mismo.
        [Parameter(Mandatory = $true)][AllowNull()][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256,
        [hashtable]$Mutacion
    )

    if ([string]::IsNullOrWhiteSpace($Sha256)) {
        throw "Invoke-DocumentoMutacion: Sha256 vacio o en blanco no admitido"
    }

    $sql = New-MutacionSql -Mutacion $Mutacion
    if ($null -eq $sql) { return 0 }

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    foreach ($columna in $Mutacion.Keys) {
        $valor = $Mutacion[$columna]
        if ($null -eq $valor) { $valor = [System.DBNull]::Value }
        [void]$cmd.Parameters.AddWithValue("@$columna", $valor)
    }
    return $cmd.ExecuteNonQuery()
}

function Remove-DocumentoPorSha256 {
    param(
        # AllowNull sobre el tipo fuerte, mismo motivo que en Invoke-DocumentoMutacion.
        [Parameter(Mandatory = $true)][AllowNull()][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256
    )

    if ([string]::IsNullOrWhiteSpace($Sha256)) {
        throw "Remove-DocumentoPorSha256: Sha256 vacio o en blanco no admitido"
    }

    # El borrado en cascada configurado en DocumentIADbContext arrastra
    # Ejecuciones y sus postprocesos y validaciones.
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = "DELETE FROM Documentos WHERE SHA256 = @sha"
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    return $cmd.ExecuteNonQuery()
}

# Rutas del JSON del contrato de la ultima ejecucion que un caso puede mutar. Una sola:
# el backfill decide por OrigenMarkdown y no hay otro motivo para tocar ese JSON.
# Clave = ruta sin el prefijo "$.", valor = nombre del parametro SQL.
$script:RutasJsonMutables = @{ "DetalleEjecucion.OrigenMarkdown" = "OrigenMarkdown" }

function New-MutacionEjecucionSql {
    param([hashtable]$Mutacion)

    if ($null -eq $Mutacion -or $Mutacion.Keys.Count -eq 0) { return $null }

    $expresion = "e.ContratoSalidaCompletoJson"
    foreach ($ruta in $Mutacion.Keys) {
        # La ruta JSON de SQL Server distingue mayusculas: una hashtable de PowerShell
        # comparada con ContainsKey es case-insensitive por defecto y dejaria pasar una
        # ruta con otra capitalizacion, que JSON_MODIFY interpretaria como una clave
        # nueva sin tocar la real. -ceq fuerza coincidencia exacta contra la clave
        # canonica de la lista blanca.
        $canonica = @($script:RutasJsonMutables.Keys | Where-Object { $_ -ceq $ruta })
        if ($canonica.Count -ne 1) {
            throw "Ruta JSON '$ruta' no admitida en mutacionEjecucion (la comparacion distingue mayusculas). Admitidas: $($script:RutasJsonMutables.Keys -join ', ')"
        }
        $parametro = $script:RutasJsonMutables[$canonica[0]]
        $expresion = "JSON_MODIFY($expresion, '`$.$($canonica[0])', @$parametro)"
    }

    # Misma definicion de "ultima ejecucion" que el backfill: TOP 1 por FechaEjecucion DESC.
    # AB#100258: "ultima ejecucion" significa "ultima CON CONTRATO". Una reutilizacion por
    # duplicado es la fila mas reciente del documento pero no tiene contrato que mutar:
    # sin este filtro la mutacion caeria sobre ella y las aserciones leerian NULL.
    return @"
UPDATE e
SET e.ContratoSalidaCompletoJson = $expresion
FROM dbo.DocumentoEjecuciones e
WHERE e.Id = (
    SELECT TOP 1 e2.Id
    FROM dbo.DocumentoEjecuciones e2
    INNER JOIN dbo.Documentos d ON d.Id = e2.DocumentoId
    WHERE d.SHA256 = @sha
      AND e2.ReutilizadaPorDuplicado = 0
    ORDER BY e2.FechaEjecucion DESC
)
"@
}

function Invoke-EjecucionMutacion {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256,
        [hashtable]$Mutacion
    )

    if ([string]::IsNullOrWhiteSpace($Sha256)) { throw "Invoke-EjecucionMutacion: Sha256 vacio o en blanco no admitido" }
    $sql = New-MutacionEjecucionSql -Mutacion $Mutacion
    if ($null -eq $sql) { return 0 }

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    foreach ($ruta in $Mutacion.Keys) {
        $valor = $Mutacion[$ruta]
        if ($null -eq $valor) { $valor = [System.DBNull]::Value }
        # Mismo criterio -ceq que New-MutacionEjecucionSql, para que ambas funciones
        # resuelvan la misma clave canonica de la lista blanca.
        $canonica = @($script:RutasJsonMutables.Keys | Where-Object { $_ -ceq $ruta })
        [void]$cmd.Parameters.AddWithValue("@$($script:RutasJsonMutables[$canonica[0]])", $valor)
    }
    return $cmd.ExecuteNonQuery()
}

function Test-DbAssertions {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Antes,
        [Parameter(Mandatory = $true)][pscustomobject]$Despues,
        [array]$Assertions = @()
    )

    # Acceso a claves opcionales sin dot-notation directa: con Set-StrictMode -Version
    # Latest (activo en este fichero desde la Tarea 1), $regla.claveQueNoExiste lanza
    # PropertyNotFoundException en vez de devolver null. Estas funciones evitan ese
    # lanzamiento tanto para hashtables (@{...}) como para pscustomobject.
    #
    # Se exponen presencia y valor por separado: una clave presente con valor $null
    # (por ejemplo "esperado = $null", que asevera que la columna quedo en NULL) no es
    # lo mismo que la clave ausente (que no asevera nada sobre esa columna).
    function Test-ReglaTieneClave {
        param($Regla, [string]$Nombre)
        if ($Regla -is [System.Collections.IDictionary]) {
            return $Regla.ContainsKey($Nombre)
        }
        return (@($Regla.PSObject.Properties.Name) -contains $Nombre)
    }

    function Get-ValorRegla {
        param($Regla, [string]$Nombre)
        if ($Regla -is [System.Collections.IDictionary]) {
            if ($Regla.ContainsKey($Nombre)) { return $Regla[$Nombre] }
            return $null
        }
        if (@($Regla.PSObject.Properties.Name) -contains $Nombre) { return $Regla.$Nombre }
        return $null
    }

    $errores = @()

    if (-not $Despues.Existe) {
        return [pscustomobject]@{
            Success = $false
            Errors  = @("la fila no existe en Documentos al terminar las pasadas")
        }
    }

    foreach ($regla in @($Assertions)) {
        if ($null -eq $regla) { continue }
        $columna = [string](Get-ValorRegla -Regla $regla -Nombre "columna")
        if ([string]::IsNullOrWhiteSpace($columna)) { continue }

        # Bajo el runner (Set-StrictMode -Off tras el dot-sourcing), una columna que
        # no existe en la instantanea devolveria $null en vez de lanzar, y la regla
        # pasaria como si hubiera comprobado algo. Se rechaza explicitamente en vez
        # de confiar en StrictMode, que aqui (en el modulo) esta activo pero en
        # produccion no lo esta.
        if (@($Despues.PSObject.Properties.Name) -notcontains $columna) {
            $errores += "columna desconocida '$columna'"
            continue
        }

        $valorDespues = $Despues.$columna
        $valorAntes   = $Antes.$columna

        $tieneEsperado    = Test-ReglaTieneClave -Regla $regla -Nombre "esperado"
        $tieneNoDisminuye = Test-ReglaTieneClave -Regla $regla -Nombre "noDisminuye"
        $tieneNoEncoge    = Test-ReglaTieneClave -Regla $regla -Nombre "noEncoge"
        $tieneNoEsNulo    = Test-ReglaTieneClave -Regla $regla -Nombre "noEsNulo"
        $tieneMayorQue    = Test-ReglaTieneClave -Regla $regla -Nombre "mayorQue"

        $esperado    = Get-ValorRegla -Regla $regla -Nombre "esperado"
        $noDisminuye = Get-ValorRegla -Regla $regla -Nombre "noDisminuye"
        $noEncoge    = Get-ValorRegla -Regla $regla -Nombre "noEncoge"
        $noEsNulo    = Get-ValorRegla -Regla $regla -Nombre "noEsNulo"
        $mayorQue    = Get-ValorRegla -Regla $regla -Nombre "mayorQue"

        if ($tieneNoEsNulo -and [bool]$noEsNulo -and $null -eq $valorDespues) {
            $errores += "$columna es nulo y no deberia serlo"
            continue
        }

        if ($tieneEsperado) {
            if ([string]$valorDespues -ne [string]$esperado) {
                $errores += "$columna='$valorDespues' esperado='$esperado'"
            }
        }

        if ($tieneNoDisminuye -and [bool]$noDisminuye) {
            if ($null -eq $valorDespues) {
                $errores += "$columna paso a nulo y la regla es noDisminuye"
            }
            elseif ($null -eq $valorAntes) {
                $errores += "no hay base de comparacion para ${columna}: el valor previo era nulo"
            }
            elseif ([int]$valorDespues -lt [int]$valorAntes) {
                $errores += "$columna bajo de $valorAntes a $valorDespues"
            }
        }

        if ($tieneNoEncoge -and [bool]$noEncoge) {
            if ($null -eq $valorDespues) {
                $errores += "$columna paso a nulo y la regla es noEncoge"
            }
            elseif ($null -eq $valorAntes) {
                $errores += "no hay base de comparacion para ${columna}: el valor previo era nulo"
            }
            elseif ([int]$valorDespues -lt [int]$valorAntes) {
                $errores += "$columna encogio de $valorAntes a $valorDespues"
            }
        }

        if ($tieneMayorQue) {
            if ($null -eq $valorDespues) {
                $errores += "$columna es nulo, esperado mayor que $mayorQue"
            }
            elseif ([int]$valorDespues -le [int]$mayorQue) {
                $errores += "$columna=$valorDespues no es mayor que $mayorQue"
            }
        }
    }

    return [pscustomobject]@{ Success = ($errores.Count -eq 0); Errors = $errores }
}
