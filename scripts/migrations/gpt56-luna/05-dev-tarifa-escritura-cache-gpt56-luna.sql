-- =====================================================================
-- 05 - DEV: precio de escritura en cache de gpt-5.6-luna en tarifas.ia
-- =====================================================================
-- Prerrequisitos del cutover a gpt-5.6-luna (AB#99934, task AB#101412).
-- Anade EurEscrituraCachePor1M a la linea Modelo = 'gpt-5.6-luna' del
-- catalogo tarifas.ia (Tipo 4). La escritura en cache se factura aparte de la
-- entrada normal; sin este precio, las ejecuciones con TokensEscrituraCache
-- > 0 salen marcadas con '|sin-precio-escritura' en TarifaAplicada.
--
-- NO SE EJECUTA SIN PRECIO CONFIRMADO. No se inventa: mientras la variable
-- siga a NULL el script falla con THROW y no toca nada.
--
-- Idempotente: si la linea ya tiene ese valor, PRINT y sin backup ni cambios.
-- Falla si la linea gpt-5.6-luna no existe (ejecutar antes el script 02).
-- Backup previo de la tabla (ModeloConfigs__bak_<ts>) solo si hay cambio. El
-- JSON resultante se valida con ISJSON antes de guardar: el cargador es
-- tolerante a errores y un JSON invalido deja el catalogo vacio en silencio.
-- El registro se cachea en memoria hasta cinco minutos; las ejecuciones ya
-- persistidas no se recalculan.
--
-- Ejecutar SOLO en DEV:
--   pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/05-dev-tarifa-escritura-cache-gpt56-luna.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Precio pendiente de confirmar por el usuario (Tarea 8, paso 2); fuente y fecha: <rellenar>
DECLARE @EurEscrituraCachePor1M DECIMAL(12,6) = NULL;

IF @EurEscrituraCachePor1M IS NULL
    THROW 50040, 'Rellenar @EurEscrituraCachePor1M con el precio confirmado antes de ejecutar.', 1;

DECLARE @Id INT, @json NVARCHAR(MAX);
SELECT @Id = Id, @json = ConfiguracionJson
FROM dbo.ModeloConfigs
WHERE Tipo = 4 AND [Key] = N'tarifas.ia' AND Activo = 1;

IF @Id IS NULL
    THROW 50041, 'No existe la fila activa tarifas.ia: ejecutar antes costes-ia/01-seed-tarifas-ia.sql.', 1;
IF ISJSON(@json) <> 1
    THROW 50042, 'El catalogo tarifas.ia no es JSON valido.', 1;

DECLARE @modelo NVARCHAR(200) = N'gpt-5.6-luna';
DECLARE @idx INT = (
    SELECT TOP (1) CAST([key] AS INT)
    FROM OPENJSON(@json, '$.Tarifas')
    WHERE JSON_VALUE([value], '$.Modelo') = @modelo);

IF @idx IS NULL
    THROW 50043, 'No existe la linea gpt-5.6-luna en tarifas.ia: ejecutar antes 02-dev-tarifa-gpt56-luna.sql.', 1;

DECLARE @ruta NVARCHAR(100) = CONCAT('$.Tarifas[', @idx, '].EurEscrituraCachePor1M');
DECLARE @actual DECIMAL(12,6) = TRY_CAST(JSON_VALUE(@json, @ruta) AS DECIMAL(12,6));

IF @actual = @EurEscrituraCachePor1M
BEGIN
    PRINT CONCAT('EurEscrituraCachePor1M ya vale ', @EurEscrituraCachePor1M, ' en ', @modelo, '. Sin backup ni cambios.');
END
ELSE
BEGIN
    SET @json = JSON_MODIFY(@json, @ruta, @EurEscrituraCachePor1M);
    IF ISJSON(@json) <> 1
        THROW 50044, 'El catalogo resultante no es JSON valido; no se guarda.', 1;

    DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
    DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
    EXEC sp_executesql @sql;
    PRINT CONCAT('Backup creado: dbo.', @bak);

    BEGIN TRAN;
    UPDATE dbo.ModeloConfigs
    SET ConfiguracionJson = @json, FechaActualizacion = SYSUTCDATETIME()
    WHERE Id = @Id;
    COMMIT TRAN;
    PRINT CONCAT('EurEscrituraCachePor1M actualizado en ', @modelo, ': ', @EurEscrituraCachePor1M);
END

-- Verificacion
SELECT Modelo, VigenteDesde, EurEntradaPor1M, EurEntradaCachePor1M, EurEscrituraCachePor1M, EurSalidaPor1M
FROM OPENJSON((SELECT ConfiguracionJson FROM dbo.ModeloConfigs WHERE Id = @Id), '$.Tarifas')
WITH (
    Modelo NVARCHAR(200) '$.Modelo',
    VigenteDesde NVARCHAR(10) '$.VigenteDesde',
    EurEntradaPor1M DECIMAL(12, 6) '$.EurEntradaPor1M',
    EurEntradaCachePor1M DECIMAL(12, 6) '$.EurEntradaCachePor1M',
    EurEscrituraCachePor1M DECIMAL(12, 6) '$.EurEscrituraCachePor1M',
    EurSalidaPor1M DECIMAL(12, 6) '$.EurSalidaPor1M')
WHERE Modelo = N'gpt-5.6-luna';
