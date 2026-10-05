-- =====================================================================
-- 02 - Tarifas de CERA16_v2 y gpt-4.1-cached en el catalogo tarifas.ia
-- =====================================================================
-- AB#100860 (hijo de AB#100779). Cierra dos huecos del catalogo detectados en la
-- verificacion de la Fase A del clasificador por embeddings (AB#100849): el
-- pipeline ya consume estos dos modelos y, sin linea, cada ejecucion que los
-- usa sale con TarifasCompletas = false y ModelosSinTarifa informado.
--
--   CERA16_v2 ...... extraction.cu.contextualizacion. Analyzer nuevo de la fila
--                    'cera.16.azure-cu' (activado en DEV el 2026-09-22 desde
--                    Admin). Misma tarifa de contextualizacion que CERA16_v1 y el
--                    resto de analyzers del catalogo: 0,859 EUR por millon.
--                    Vigente desde 2026-09-22.
--   gpt-4.1-cached . extraction.cu.modelo. Clave que devuelve Content
--                    Understanding en usage.tokens; UsoContentUnderstandingMapper
--                    la trata como un modelo mas y, al no llevar sufijo, cuenta
--                    sus tokens como entrada. Se tarifa con el precio cacheado de
--                    gpt-4.1 global ya presente en la linea 'gpt-4.1'
--                    (EurEntradaCachePor1M = 0,429), pero en EurEntradaPor1M
--                    porque la calculadora lee los tokens de TokensEntrada.
--                    Vigente desde 2026-04-01, como gpt-4.1.
--
-- SUPUESTO: no se sabe si la clave 'gpt-4.1-input' de Content Understanding
-- incluye ya los tokens cacheados. Si los incluyera, estas lineas sobrevaloran
-- el coste en 0,429 EUR por millon de tokens cacheados (frente a los 1,717 de
-- la entrada plena). Contrastar con la factura en la siguiente revision de
-- costes; si hay doble cuenta, corregir el mapeador, no el catalogo.
--
-- Idempotente: cada linea se anade solo si no existe. Backup previo.
-- Ejecutar en DEV, PRE y PRO (el catalogo es identico en los tres). El registro
-- se cachea en memoria: hasta cinco minutos para verse en ejecuciones nuevas.
-- Las ejecuciones ya persistidas no se recalculan.
--   sqlcmd -S srbsqldevdocai.database.windows.net -d DocumentIA -G -i 02-tarifas-cera16v2-gpt41-cached.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Id INT, @json NVARCHAR(MAX);
SELECT @Id = Id, @json = ConfiguracionJson
FROM dbo.ModeloConfigs
WHERE Tipo = 4 AND [Key] = N'tarifas.ia' AND Activo = 1;

IF @Id IS NULL
    THROW 50003, 'No existe la fila activa tarifas.ia: ejecutar antes 01-seed-tarifas-ia.sql.', 1;
IF ISJSON(@json) <> 1
    THROW 50004, 'El catalogo tarifas.ia no es JSON valido.', 1;

DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

BEGIN TRAN;

-- Una fila por linea a anadir, con su JSON completo: cada modelo lleva un campo
-- de precio distinto y asi el script no tiene que montarlo por partes.
DECLARE @lineas TABLE (Modelo NVARCHAR(200), Linea NVARCHAR(400));
INSERT INTO @lineas VALUES
    (N'CERA16_v2',      N'{"Modelo":"CERA16_v2","VigenteDesde":"2026-09-22","EurContextualizacionPor1M":0.859}'),
    (N'gpt-4.1-cached', N'{"Modelo":"gpt-4.1-cached","VigenteDesde":"2026-04-01","EurEntradaPor1M":0.429}');

DECLARE @modelo NVARCHAR(200), @linea NVARCHAR(400);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT Modelo, Linea FROM @lineas;
OPEN cur;
FETCH NEXT FROM cur INTO @modelo, @linea;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM OPENJSON(@json, '$.Tarifas') WITH (Modelo NVARCHAR(200) '$.Modelo')
        WHERE Modelo = @modelo)
    BEGIN
        SET @json = JSON_MODIFY(@json, 'append $.Tarifas', JSON_QUERY(@linea));
        PRINT CONCAT('Linea anadida: ', @modelo);
    END
    ELSE
        PRINT CONCAT('Linea ya presente: ', @modelo);
    FETCH NEXT FROM cur INTO @modelo, @linea;
END
CLOSE cur;
DEALLOCATE cur;

IF ISJSON(@json) <> 1
    THROW 50005, 'El catalogo resultante no es JSON valido; no se guarda.', 1;

UPDATE dbo.ModeloConfigs
SET ConfiguracionJson = @json, FechaActualizacion = SYSUTCDATETIME()
WHERE Id = @Id;

COMMIT TRAN;

-- Verificacion: las dos lineas nuevas junto a las que les sirven de referencia.
SELECT Modelo, VigenteDesde, EurEntradaPor1M, EurEntradaCachePor1M, EurContextualizacionPor1M
FROM OPENJSON((SELECT ConfiguracionJson FROM dbo.ModeloConfigs WHERE Id = @Id), '$.Tarifas')
WITH (
    Modelo NVARCHAR(200) '$.Modelo',
    VigenteDesde NVARCHAR(10) '$.VigenteDesde',
    EurEntradaPor1M DECIMAL(12, 6) '$.EurEntradaPor1M',
    EurEntradaCachePor1M DECIMAL(12, 6) '$.EurEntradaCachePor1M',
    EurContextualizacionPor1M DECIMAL(12, 6) '$.EurContextualizacionPor1M')
WHERE Modelo IN (N'CERA16_v2', N'CERA16_v1', N'gpt-4.1-cached', N'gpt-4.1')
ORDER BY Modelo;
