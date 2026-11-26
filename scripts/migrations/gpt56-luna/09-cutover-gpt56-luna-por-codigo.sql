-- =====================================================================
-- 09 - Cutover a gpt-5.6-luna por codigo de tipologia (clasificacion GPT y extraccion)
-- =====================================================================
-- AB#99939. Pone gpt-5.6-luna como fallback de clasificacion (Tipo 0) y de
-- extraccion (Tipo 1) y como modelo de extraccion de la tipologia IBI_1.1.
-- Todo en una sola transaccion:
--   1. Alta, si no existen, de extraction.gpt56-luna (Tipo 1, a partir de
--      extraction.gpt4o-mini-fallback) y default.gpt56-luna_ex (Tipo 1, a partir
--      de default.gpt4o-mini_ex). Toman ResourceAlias, Endpoint (si lo trae) y
--      AuthMode de classification.gpt56-luna, que ya funciona con luna.
--      DeploymentName = gpt-5.6-luna y ReasoningEffort = none, sin promptCache
--      (no medido en extraccion), UseAsFallback = false al crearse.
--   2. UseAsFallback = true en classification.gpt56-luna y extraction.gpt56-luna;
--      false en classification.gpt4o-mini-fallback y
--      extraction.gpt4o-mini-fallback. Antes del COMMIT se comprueba que
--      hay exactamente una fila con UseAsFallback por Tipo 0 y Tipo 1; si no,
--      THROW y rollback (el registro lanza KeyNotFoundException con 0 o 2).
--   3. Tipologia IBI_1.1 (Codigo = 'IBI_1.1'): Extraction.ModelKey pasa de default.gpt4o-mini_ex
--      a default.gpt56-luna_ex (respeta el casing de la fila).
-- Las filas de gpt-4 no cambian de contenido, salvo el indicador de classification.gpt4o-mini-fallback y
-- extraction.gpt4o-mini-fallback.
-- Prerrequisitos: filas classification.gpt56-luna, default.gpt4o-mini_ex y
-- extraction.gpt4o-mini-fallback activas (scripts 01 y 03 en DEV; en PRE y PRO
-- llegan con el seed), deployment gpt-5.6-luna en el entorno. Los cambios llegan a la app
-- en <= 5 min (cache de registro). Para deshacerlo:
-- 10-rollback-cutover-gpt56-luna-por-codigo.sql.
--
-- Idempotente: si todo esta ya en el estado final, PRINT con 0 cambios, sin
-- backup. Backup previo (ModeloConfigs__bak_<ts> y Tipologias__bak_<ts>) solo
-- si hay algo que cambiar.
--
-- Cualquier entorno. Ejecutar con apply-sql-dev.ps1 -Server <servidor> -PermitirNoDev;
-- en PRE y PRO lo encadena aplicar-luna.ps1. En DEV, sin -Server:
--   pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/09-cutover-gpt56-luna-por-codigo.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0. Filas origen y casing (las claves de ModeloConfigs de estas filas son PascalCase)
DECLARE @cls NVARCHAR(MAX), @ex NVARCHAR(MAX), @fb NVARCHAR(MAX);
SELECT @cls = ConfiguracionJson FROM dbo.ModeloConfigs WHERE Tipo = 0 AND [Key] = N'classification.gpt56-luna' AND Activo = 1;
SELECT @ex  = ConfiguracionJson FROM dbo.ModeloConfigs WHERE Tipo = 1 AND [Key] = N'default.gpt4o-mini_ex' AND Activo = 1;
SELECT @fb  = ConfiguracionJson FROM dbo.ModeloConfigs WHERE Tipo = 1 AND [Key] = N'extraction.gpt4o-mini-fallback' AND Activo = 1;

IF @cls IS NULL
    THROW 50030, 'No existe la fila activa classification.gpt56-luna (Tipo 0): ejecutar antes 01-dev-insert-gpt56-luna-rows.sql.', 1;
IF @ex IS NULL
    THROW 50031, 'No existe la fila activa default.gpt4o-mini_ex (Tipo 1).', 1;
IF @fb IS NULL
    THROW 50032, 'No existe la fila activa extraction.gpt4o-mini-fallback (Tipo 1).', 1;
IF ISJSON(@cls) <> 1 OR ISJSON(@ex) <> 1 OR ISJSON(@fb) <> 1
    THROW 50033, 'Alguna ConfiguracionJson origen no es JSON valido.', 1;
IF JSON_VALUE(@cls, '$.UseAsFallback') IS NULL OR JSON_VALUE(@ex, '$.DeploymentName') IS NULL OR JSON_VALUE(@fb, '$.UseAsFallback') IS NULL
    THROW 50034, 'Las filas origen no usan claves PascalCase (UseAsFallback, DeploymentName): revisar el casing antes de aplicar.', 1;
DECLARE @c03 NVARCHAR(MAX);
SELECT @c03 = ConfiguracionJson FROM dbo.ModeloConfigs WHERE Tipo = 0 AND [Key] = N'classification.gpt4o-mini-fallback' AND Activo = 1;
IF @c03 IS NULL
    THROW 50035, 'No existe la fila activa classification.gpt4o-mini-fallback (Tipo 0).', 1;
IF ISJSON(@c03) <> 1 OR JSON_VALUE(@c03, '$.UseAsFallback') IS NULL
    THROW 50040, 'classification.gpt4o-mini-fallback no es JSON valido o no usa la clave PascalCase UseAsFallback: revisar el casing antes de aplicar.', 1;

-- Filas a dar de alta: Nueva, Origen
DECLARE @altas TABLE (Nueva NVARCHAR(200), Origen NVARCHAR(200));
INSERT INTO @altas VALUES
    (N'extraction.gpt56-luna',  N'extraction.gpt4o-mini-fallback'),
    (N'default.gpt56-luna_ex',  N'default.gpt4o-mini_ex');

-- Estado destino de UseAsFallback por clave (Tipo 0 y 1)
DECLARE @flags TABLE (Tipo INT, [Key] NVARCHAR(200), Valor BIT);
INSERT INTO @flags VALUES
    (0, N'classification.gpt56-luna',          1),
    (0, N'classification.gpt4o-mini-fallback', 0),
    (1, N'extraction.gpt56-luna',              1),
    (1, N'extraction.gpt4o-mini-fallback',     0);

-- Tipologia IBI_1.1: casing de la ruta y estado actual
DECLARE @tipo NVARCHAR(MAX), @tmk NVARCHAR(200), @pascal BIT;
SELECT @tipo = ConfiguracionJson FROM dbo.Tipologias WHERE Codigo = N'IBI_1.1';
IF @tipo IS NULL OR ISJSON(@tipo) <> 1
    THROW 50036, 'No existe la tipologia IBI_1.1 o su ConfiguracionJson no es JSON valido.', 1;
IF JSON_VALUE(@tipo, '$.extraction.modelKey') IS NOT NULL
    SELECT @pascal = 0, @tmk = JSON_VALUE(@tipo, '$.extraction.modelKey');
ELSE IF JSON_VALUE(@tipo, '$.Extraction.ModelKey') IS NOT NULL
    SELECT @pascal = 1, @tmk = JSON_VALUE(@tipo, '$.Extraction.ModelKey');
IF @tmk IS NULL OR LOWER(@tmk) NOT IN (N'default.gpt4o-mini_ex', N'default.gpt56-luna_ex')
    THROW 50037, 'La tipologia IBI_1.1 no tiene Extraction.ModelKey = default.gpt4o-mini_ex (ni ya default.gpt56-luna_ex): no se toca.', 1;

-- Cambios pendientes (las altas de extraction.gpt56-luna cuentan ademas su indicador al insertar)
DECLARE @pAltas INT = (SELECT COUNT(*) FROM @altas a
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs x WHERE x.Tipo = 1 AND x.[Key] = a.Nueva));
DECLARE @pFlags INT = (SELECT COUNT(*) FROM @flags f
    JOIN dbo.ModeloConfigs m ON m.Tipo = f.Tipo AND m.[Key] = f.[Key]
    WHERE ISNULL(JSON_VALUE(m.ConfiguracionJson, '$.UseAsFallback'), 'x') <> CASE f.Valor WHEN 1 THEN 'true' ELSE 'false' END);
DECLARE @pTipo INT = CASE WHEN LOWER(@tmk) = N'default.gpt4o-mini_ex' THEN 1 ELSE 0 END;
DECLARE @pendientes INT = @pAltas + @pFlags + @pTipo;

IF @pendientes = 0
    PRINT 'Nada que cambiar: el cutover ya esta aplicado (0 cambios). Sin backup.';
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

    -- 2. Altas (Endpoint, ResourceAlias y AuthMode de classification.gpt56-luna; el resto, de la fila origen)
    INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, CreadoPor)
    SELECT m.Tipo, a.Nueva, m.Provider, 1,
           JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
               m.ConfiguracionJson,
               '$.Key', a.Nueva),
               '$.DeploymentName', N'gpt-5.6-luna'),
               '$.IsDefault', CAST(0 AS BIT)),
               '$.UseAsFallback', CAST(0 AS BIT)),
               '$.ReasoningEffort', N'none'),
               '$.promptCache', NULL),
               '$.PromptCache', NULL),
               '$.ResourceAlias', COALESCE(JSON_VALUE(@cls, '$.ResourceAlias'), JSON_VALUE(m.ConfiguracionJson, '$.ResourceAlias'))),
               '$.AuthMode', COALESCE(JSON_VALUE(@cls, '$.AuthMode'), JSON_VALUE(m.ConfiguracionJson, '$.AuthMode'))),
               '$.Endpoint', COALESCE(JSON_VALUE(@cls, '$.Endpoint'), JSON_VALUE(m.ConfiguracionJson, '$.Endpoint'), N'')),
           SYSUTCDATETIME(),
           N'script-09-gpt56-luna'
    FROM @altas a
    JOIN dbo.ModeloConfigs m ON m.Tipo = 1 AND m.[Key] = a.Origen AND m.Activo = 1
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs x WHERE x.Tipo = 1 AND x.[Key] = a.Nueva);
    PRINT CONCAT('Filas luna insertadas: ', @@ROWCOUNT);

    -- 3. Indicadores UseAsFallback (solo donde difieren)
    UPDATE m
    SET ConfiguracionJson = JSON_MODIFY(m.ConfiguracionJson, '$.UseAsFallback', CAST(f.Valor AS BIT)),
        FechaActualizacion = SYSUTCDATETIME()
    FROM dbo.ModeloConfigs m
    JOIN @flags f ON f.Tipo = m.Tipo AND f.[Key] = m.[Key]
    WHERE ISNULL(JSON_VALUE(m.ConfiguracionJson, '$.UseAsFallback'), 'x') <> CASE f.Valor WHEN 1 THEN 'true' ELSE 'false' END;
    PRINT CONCAT('Indicadores UseAsFallback cambiados: ', @@ROWCOUNT);

    -- 4. Tipologia IBI_1.1 (casing de la fila)
    IF @pTipo = 1
    BEGIN
        UPDATE dbo.Tipologias
        SET ConfiguracionJson = CASE WHEN @pascal = 1
                THEN JSON_MODIFY(ConfiguracionJson, '$.Extraction.ModelKey', N'default.gpt56-luna_ex')
                ELSE JSON_MODIFY(ConfiguracionJson, '$.extraction.modelKey', N'default.gpt56-luna_ex') END
        WHERE Codigo = N'IBI_1.1';
        PRINT CONCAT('Tipologia IBI_1.1 actualizada: ', @@ROWCOUNT);
    END

    -- 5. Comprobacion: exactamente un fallback por Tipo 0 y Tipo 1
    IF (SELECT COUNT(*) FROM dbo.ModeloConfigs WHERE Tipo = 0 AND Activo = 1 AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.UseAsFallback'), JSON_VALUE(ConfiguracionJson, '$.useAsFallback')) = 'true') <> 1
       OR (SELECT COUNT(*) FROM dbo.ModeloConfigs WHERE Tipo = 1 AND Activo = 1 AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.UseAsFallback'), JSON_VALUE(ConfiguracionJson, '$.useAsFallback')) = 'true') <> 1
        THROW 50038, 'Tras el cambio no hay exactamente un UseAsFallback en Tipo 0 y en Tipo 1: se deshace todo.', 1;
    IF EXISTS (SELECT 1 FROM dbo.ModeloConfigs WHERE [Key] LIKE N'%gpt56-luna%' AND ISJSON(ConfiguracionJson) <> 1)
        THROW 50039, 'Alguna fila luna quedo con JSON no valido: se deshace todo.', 1;

    COMMIT TRAN;
    PRINT 'Cutover a gpt-5.6-luna aplicado.';
END

-- Verificacion
SELECT Tipo, COUNT(*) AS FilasUseAsFallback
FROM dbo.ModeloConfigs
WHERE Activo = 1 AND JSON_VALUE(ConfiguracionJson, '$.UseAsFallback') = 'true'
GROUP BY Tipo ORDER BY Tipo;

SELECT Id, Tipo, [Key], Activo,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName')  AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.ReasoningEffort') AS ReasoningEffort,
       JSON_VALUE(ConfiguracionJson, '$.MaxTokens')       AS MaxTokens,
       JSON_VALUE(ConfiguracionJson, '$.UseAsFallback')   AS UseAsFallback,
       JSON_VALUE(ConfiguracionJson, '$.ResourceAlias')   AS ResourceAlias,
       ISJSON(ConfiguracionJson)                          AS JsonValido
FROM dbo.ModeloConfigs
WHERE [Key] LIKE N'%gpt56-luna%' OR [Key] IN (N'classification.gpt4o-mini-fallback', N'extraction.gpt4o-mini-fallback')
ORDER BY Tipo, Id;

SELECT Id,
       COALESCE(JSON_VALUE(ConfiguracionJson, '$.extraction.modelKey'), JSON_VALUE(ConfiguracionJson, '$.Extraction.ModelKey')) AS ExtractionModelKey
FROM dbo.Tipologias WHERE Codigo = N'IBI_1.1';
