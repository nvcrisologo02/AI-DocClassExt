-- =====================================================================
-- 10 - Rollback del cutover a gpt-5.6-luna
-- =====================================================================
-- AB#99939. Deshace 09-cutover-gpt56-luna-por-codigo.sql en una sola transaccion:
--   1. UseAsFallback = true en classification.gpt4o-mini-fallback y
--      extraction.gpt4o-mini-fallback; false en classification.gpt56-luna
--      y extraction.gpt56-luna.
--   2. Tipologia IBI_1.1 (Codigo = 'IBI_1.1'): Extraction.ModelKey vuelve a default.gpt4o-mini_ex
--      (respeta el casing de la fila).
--   3. Antes del COMMIT, comprobacion de exactamente un UseAsFallback por Tipo 0
--      y Tipo 1; si no, THROW y rollback.
-- NO borra filas: extraction.gpt56-luna y default.gpt56-luna_ex se quedan sin
-- indicador, como estaban classification.gpt56-luna y prompt.gpt56-luna antes del
-- cutover. Las filas de gpt-4 no cambian de contenido, salvo el indicador.
-- El prompt libre y el resumen se devuelven con el app setting
-- PromptDefaults__ModelKey = default.gpt4o-mini (no con este script); activacion
-- y rollback mueven a la vez el script y el setting. Los cambios llegan a la app
-- en <= 5 min (cache de registro).
--
-- Idempotente: si todo esta ya en el estado de gpt-4, PRINT con 0 cambios, sin
-- backup. Backup previo (ModeloConfigs__bak_<ts> y Tipologias__bak_<ts>) solo
-- si hay algo que cambiar.
--
-- Cualquier entorno. Ejecutar con apply-sql-dev.ps1 -Server <servidor> -PermitirNoDev.
-- En DEV, sin -Server:
--   pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/10-rollback-cutover-gpt56-luna-por-codigo.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0. Comprobar las filas y el casing
DECLARE @rows TABLE (Tipo INT, [Key] NVARCHAR(200), Valor BIT);
INSERT INTO @rows VALUES
    (0, N'classification.gpt4o-mini-fallback', 1),
    (0, N'classification.gpt56-luna',          0),
    (1, N'extraction.gpt4o-mini-fallback',     1),
    (1, N'extraction.gpt56-luna',              0);

IF (SELECT COUNT(*) FROM @rows r JOIN dbo.ModeloConfigs m ON m.Tipo = r.Tipo AND m.[Key] = r.[Key] AND m.Activo = 1) <> 4
    THROW 50040, 'Faltan filas activas (classification.gpt4o-mini-fallback, extraction.gpt4o-mini-fallback, classification.gpt56-luna o extraction.gpt56-luna): no se puede deshacer. Si 09 no se aplico, no hay nada que deshacer.', 1;
IF EXISTS (SELECT 1 FROM @rows r JOIN dbo.ModeloConfigs m ON m.Tipo = r.Tipo AND m.[Key] = r.[Key]
           WHERE ISJSON(m.ConfiguracionJson) <> 1 OR JSON_VALUE(m.ConfiguracionJson, '$.UseAsFallback') IS NULL)
    THROW 50041, 'Alguna fila no es JSON valido o no usa la clave PascalCase UseAsFallback: revisar el casing antes de aplicar.', 1;

DECLARE @tipo NVARCHAR(MAX), @tmk NVARCHAR(200), @pascal BIT;
SELECT @tipo = ConfiguracionJson FROM dbo.Tipologias WHERE Codigo = N'IBI_1.1';
IF @tipo IS NULL OR ISJSON(@tipo) <> 1
    THROW 50042, 'No existe la tipologia IBI_1.1 o su ConfiguracionJson no es JSON valido.', 1;
IF JSON_VALUE(@tipo, '$.extraction.modelKey') IS NOT NULL
    SELECT @pascal = 0, @tmk = JSON_VALUE(@tipo, '$.extraction.modelKey');
ELSE IF JSON_VALUE(@tipo, '$.Extraction.ModelKey') IS NOT NULL
    SELECT @pascal = 1, @tmk = JSON_VALUE(@tipo, '$.Extraction.ModelKey');
IF @tmk IS NULL OR LOWER(@tmk) NOT IN (N'default.gpt4o-mini_ex', N'default.gpt56-luna_ex')
    THROW 50043, 'La tipologia IBI_1.1 no tiene Extraction.ModelKey = default.gpt56-luna_ex (ni ya default.gpt4o-mini_ex): no se toca.', 1;

DECLARE @pFlags INT = (SELECT COUNT(*) FROM @rows r
    JOIN dbo.ModeloConfigs m ON m.Tipo = r.Tipo AND m.[Key] = r.[Key]
    WHERE JSON_VALUE(m.ConfiguracionJson, '$.UseAsFallback') <> CASE r.Valor WHEN 1 THEN 'true' ELSE 'false' END);
DECLARE @pTipo INT = CASE WHEN LOWER(@tmk) = N'default.gpt56-luna_ex' THEN 1 ELSE 0 END;

IF @pFlags + @pTipo = 0
    PRINT 'Nada que cambiar: el rollback ya esta aplicado (0 cambios). Sin backup.';
ELSE
BEGIN
    -- 1. Backups
    DECLARE @ts NVARCHAR(20) = FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss');
    DECLARE @bakM SYSNAME = CONCAT('ModeloConfigs__bak_', @ts);
    DECLARE @bakT SYSNAME = CONCAT('Tipologias__bak_', @ts);
    DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bakM), ' FROM dbo.ModeloConfigs;');
    EXEC sp_executesql @sql;
    SET @sql = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bakT), ' FROM dbo.Tipologias;');
    EXEC sp_executesql @sql;
    PRINT CONCAT('Backups creados: dbo.', @bakM, ' y dbo.', @bakT);

    BEGIN TRAN;

    -- 2. Indicadores (solo donde difieren)
    UPDATE m
    SET ConfiguracionJson = JSON_MODIFY(m.ConfiguracionJson, '$.UseAsFallback', CAST(r.Valor AS BIT)),
        FechaActualizacion = SYSUTCDATETIME()
    FROM dbo.ModeloConfigs m
    JOIN @rows r ON r.Tipo = m.Tipo AND r.[Key] = m.[Key]
    WHERE JSON_VALUE(m.ConfiguracionJson, '$.UseAsFallback') <> CASE r.Valor WHEN 1 THEN 'true' ELSE 'false' END;
    PRINT CONCAT('Indicadores UseAsFallback cambiados: ', @@ROWCOUNT);

    -- 3. Tipologia IBI_1.1 (casing de la fila)
    IF @pTipo = 1
    BEGIN
        UPDATE dbo.Tipologias
        SET ConfiguracionJson = CASE WHEN @pascal = 1
                THEN JSON_MODIFY(ConfiguracionJson, '$.Extraction.ModelKey', N'default.gpt4o-mini_ex')
                ELSE JSON_MODIFY(ConfiguracionJson, '$.extraction.modelKey', N'default.gpt4o-mini_ex') END
        WHERE Codigo = N'IBI_1.1';
        PRINT CONCAT('Tipologia IBI_1.1 actualizada: ', @@ROWCOUNT);
    END

    -- 4. Comprobacion: exactamente un fallback por Tipo 0 y Tipo 1
    IF (SELECT COUNT(*) FROM dbo.ModeloConfigs WHERE Tipo = 0 AND Activo = 1 AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.UseAsFallback'), JSON_VALUE(ConfiguracionJson, '$.useAsFallback')) = 'true') <> 1
       OR (SELECT COUNT(*) FROM dbo.ModeloConfigs WHERE Tipo = 1 AND Activo = 1 AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.UseAsFallback'), JSON_VALUE(ConfiguracionJson, '$.useAsFallback')) = 'true') <> 1
        THROW 50044, 'Tras el rollback no hay exactamente un UseAsFallback en Tipo 0 y en Tipo 1: se deshace todo.', 1;

    COMMIT TRAN;
    PRINT 'Rollback del cutover aplicado (filas luna conservadas, sin indicador).';
END

-- Verificacion
SELECT Tipo, COUNT(*) AS FilasUseAsFallback
FROM dbo.ModeloConfigs
WHERE Activo = 1 AND JSON_VALUE(ConfiguracionJson, '$.UseAsFallback') = 'true'
GROUP BY Tipo ORDER BY Tipo;

SELECT Id, Tipo, [Key], Activo,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.UseAsFallback')  AS UseAsFallback,
       ISJSON(ConfiguracionJson)                         AS JsonValido
FROM dbo.ModeloConfigs
WHERE [Key] LIKE N'%gpt56-luna%' OR [Key] IN (N'classification.gpt4o-mini-fallback', N'extraction.gpt4o-mini-fallback')
ORDER BY Tipo, Id;

SELECT Id,
       COALESCE(JSON_VALUE(ConfiguracionJson, '$.extraction.modelKey'), JSON_VALUE(ConfiguracionJson, '$.Extraction.ModelKey')) AS ExtractionModelKey
FROM dbo.Tipologias WHERE Codigo = N'IBI_1.1';
