-- =====================================================================
-- 02 - Tarifas de los deployments de embeddings del clasificador A (AB#100779)
-- =====================================================================
-- Anade al catalogo 'tarifas.ia' (Tipo=4) una linea por nombre de deployment:
--   text-embedding-3-large-030358 y text-embedding-3-large-010650
-- a 0,112 EUR por millon de tokens de entrada (mismo medidor que la linea
-- 'text-embedding-3-large' que ya tarifa los embeddings de CU: medidor
-- "text-embedding-3-large-glbl", 0,0001 EUR por 1K tokens, ver 01-seed-tarifas-ia.sql).
--
-- El consumo del clasificador se registra con Modelo = nombre del deployment
-- (UsoEmbeddingsMapper), asi que sin estas lineas TarifasCompletas quedaria a
-- false y ClasificacionEur no sumaria la llamada (criterio 5 del PBI).
--
-- Idempotente: cada linea se anade solo si no existe. Backup previo.
-- Ejecutar en DEV, PRE y PRO (el catalogo es identico en los tres).
--   sqlcmd -S srbsqldevdocai.database.windows.net -d DocumentIA -G -i 02-tarifas-embeddings-deployments.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Id INT, @json NVARCHAR(MAX);
SELECT @Id = Id, @json = ConfiguracionJson
FROM dbo.ModeloConfigs
WHERE Tipo = 4 AND [Key] = N'tarifas.ia' AND Activo = 1;

IF @Id IS NULL
    THROW 50003, 'No existe la fila activa tarifas.ia: ejecutar antes scripts/migrations/costes-ia/01-seed-tarifas-ia.sql.', 1;
IF ISJSON(@json) <> 1
    THROW 50004, 'El catalogo tarifas.ia no es JSON valido.', 1;

DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

BEGIN TRAN;

DECLARE @modelos TABLE (Modelo NVARCHAR(200));
INSERT INTO @modelos VALUES (N'text-embedding-3-large-030358'), (N'text-embedding-3-large-010650');

DECLARE @modelo NVARCHAR(200);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT Modelo FROM @modelos;
OPEN cur;
FETCH NEXT FROM cur INTO @modelo;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM OPENJSON(@json, '$.Tarifas') WITH (Modelo NVARCHAR(200) '$.Modelo')
        WHERE Modelo = @modelo)
    BEGIN
        SET @json = JSON_MODIFY(@json, 'append $.Tarifas',
            JSON_QUERY(CONCAT(N'{"Modelo":"', @modelo, N'","VigenteDesde":"2026-04-01","EurEntradaPor1M":0.112}')));
        PRINT CONCAT('Linea anadida: ', @modelo);
    END
    ELSE
        PRINT CONCAT('Linea ya presente: ', @modelo);
    FETCH NEXT FROM cur INTO @modelo;
END
CLOSE cur;
DEALLOCATE cur;

IF ISJSON(@json) <> 1
    THROW 50005, 'El catalogo resultante no es JSON valido; no se guarda.', 1;

UPDATE dbo.ModeloConfigs
SET ConfiguracionJson = @json, FechaActualizacion = SYSUTCDATETIME()
WHERE Id = @Id;

COMMIT TRAN;

-- Verificacion
SELECT Modelo, VigenteDesde, EurEntradaPor1M
FROM OPENJSON((SELECT ConfiguracionJson FROM dbo.ModeloConfigs WHERE Id = @Id), '$.Tarifas')
WITH (Modelo NVARCHAR(200) '$.Modelo', VigenteDesde NVARCHAR(10) '$.VigenteDesde', EurEntradaPor1M DECIMAL(12, 6) '$.EurEntradaPor1M')
WHERE Modelo LIKE N'text-embedding-3-large%';
