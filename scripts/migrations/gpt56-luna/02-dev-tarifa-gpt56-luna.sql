-- =====================================================================
-- 02 - DEV: linea gpt-5.6-luna en el catalogo tarifas.ia
-- =====================================================================
-- Prueba en DEV de gpt-5.6-luna (AB#99934, task AB#101372). Sin linea de
-- tarifa, cada ejecucion que use el modelo sale con CosteClasificacionEur
-- nulo, TarifasCompletas = false y ModelosSinTarifa informado, y la medicion
-- de coste de la prueba no vale.
--
-- Precios: Azure Retail Prices API, consultada el 2026-11-17 en EUR, region
-- westeurope, productName 'Azure OpenAI GPT5', medidores
-- '5.6 luna ShortCo * Std DZ 1M Tokens' (contexto corto, Standard,
-- Data Zone, que es el SKU del deployment):
--   Inp  (entrada)          0.1975 EUR por millon
--   Cd Inp (entrada cache)  0.0197 EUR por millon
--   Opt  (salida)           1.1850 EUR por millon
-- La spec partia de 0,20 / 0,02 / 1,19 (redondeo de la lista publica); se
-- guardan los del medidor de West Europe, que son los que facturan. Son
-- precios de lista, no efectivos (sin descuentos de contrato). Los medidores
-- 'LongCo' (contexto largo) doblan el precio y no aplican: la clasificacion
-- envia a lo sumo 10 paginas de texto.
-- VigenteDesde 2026-11-17 (fecha del alta del deployment en DEV).
--
-- Idempotente: la linea se anade solo si no existe. Si no hay nada que anadir
-- no hace backup ni toca la fila. El JSON resultante se valida con ISJSON
-- antes de guardar: el cargador es tolerante a errores y un JSON invalido
-- deja el catalogo vacio en silencio. El registro se cachea en memoria hasta
-- cinco minutos; las ejecuciones ya persistidas no se recalculan.
--
-- Ejecutar SOLO en DEV (PRE y PRO son del cutover, AB#99939):
--   pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/02-dev-tarifa-gpt56-luna.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Id INT, @json NVARCHAR(MAX);
SELECT @Id = Id, @json = ConfiguracionJson
FROM dbo.ModeloConfigs
WHERE Tipo = 4 AND [Key] = N'tarifas.ia' AND Activo = 1;

IF @Id IS NULL
    THROW 50003, 'No existe la fila activa tarifas.ia: ejecutar antes costes-ia/01-seed-tarifas-ia.sql.', 1;
IF ISJSON(@json) <> 1
    THROW 50004, 'El catalogo tarifas.ia no es JSON valido.', 1;

DECLARE @modelo NVARCHAR(200) = N'gpt-5.6-luna';
DECLARE @linea NVARCHAR(400) = N'{"Modelo":"gpt-5.6-luna","VigenteDesde":"2026-11-17","EurEntradaPor1M":0.1975,"EurEntradaCachePor1M":0.0197,"EurSalidaPor1M":1.185}';

IF ISJSON(@linea) <> 1
    THROW 50006, 'La linea de tarifa a anadir no es JSON valido.', 1;

IF EXISTS (
    SELECT 1 FROM OPENJSON(@json, '$.Tarifas') WITH (Modelo NVARCHAR(200) '$.Modelo')
    WHERE Modelo = @modelo)
BEGIN
    PRINT CONCAT('Linea ya presente: ', @modelo, '. Sin backup ni cambios.');
END
ELSE
BEGIN
    SET @json = JSON_MODIFY(@json, 'append $.Tarifas', JSON_QUERY(@linea));
    IF ISJSON(@json) <> 1
        THROW 50005, 'El catalogo resultante no es JSON valido; no se guarda.', 1;

    DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
    DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
    EXEC sp_executesql @sql;
    PRINT CONCAT('Backup creado: dbo.', @bak);

    BEGIN TRAN;
    UPDATE dbo.ModeloConfigs
    SET ConfiguracionJson = @json, FechaActualizacion = SYSUTCDATETIME()
    WHERE Id = @Id;
    COMMIT TRAN;
    PRINT CONCAT('Linea anadida: ', @modelo);
END

-- Verificacion: la linea nueva junto a las GPT que le sirven de referencia.
SELECT Modelo, VigenteDesde, EurEntradaPor1M, EurEntradaCachePor1M, EurSalidaPor1M
FROM OPENJSON((SELECT ConfiguracionJson FROM dbo.ModeloConfigs WHERE Id = @Id), '$.Tarifas')
WITH (
    Modelo NVARCHAR(200) '$.Modelo',
    VigenteDesde NVARCHAR(10) '$.VigenteDesde',
    EurEntradaPor1M DECIMAL(12, 6) '$.EurEntradaPor1M',
    EurEntradaCachePor1M DECIMAL(12, 6) '$.EurEntradaCachePor1M',
    EurSalidaPor1M DECIMAL(12, 6) '$.EurSalidaPor1M')
WHERE Modelo IN (N'gpt-5.6-luna', N'gpt-5-mini', N'gpt-4.1-mini', N'gpt-4o-mini')
ORDER BY Modelo;
