-- =====================================================================
-- 08 - DEV: las tipologias heredan el modelo de prompt
-- =====================================================================
-- AB#99939. Elimina la clave de modelo de prompt (PromptConfig.ModelKey en
-- PascalCase o promptConfig.modelKey en camelCase, segun la fila) en las
-- tipologias de cualquier Estado cuyo valor sea default.gpt4o-mini (sin
-- distinguir mayusculas). Sin la propiedad, el modelo de prompt se hereda de
-- PromptDefaults:ModelKey, de modo que el proximo cambio de modelo es un app
-- setting. No toca las que fijan otra clave y lo informa (hoy 0).
-- El casing de la fila decide la ruta: escribir en el casing contrario no
-- borraria nada y la herencia no se produciria. JSON_MODIFY(..., NULL) solo
-- elimina la propiedad, no deja claves duplicadas.
--
-- Seguro con el codigo actual: sin la propiedad, el codigo de hoy deserializa
-- default.gpt4o-mini por defecto, asi que puede aplicarse antes o despues del
-- despliegue. No forma parte del rollback (el rollback de prompt y resumen es
-- el setting PromptDefaults__ModelKey); restaurar desde Tipologias__bak_<ts> es
-- el ultimo recurso.
--
-- Idempotente: si no queda ninguna con la clave, PRINT con 0 cambios, sin
-- backup. Backup previo de la tabla (Tipologias__bak_<ts>) solo si hay cambios.
--
-- Ejecutar SOLO en DEV:
--   pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/08-dev-tipologias-heredan-modelo-prompt.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0. Estado inicial por casing (tipologias con JSON valido)
DECLARE @total INT = (SELECT COUNT(*) FROM dbo.Tipologias);
DECLARE @invalidas INT = (SELECT COUNT(*) FROM dbo.Tipologias WHERE ISJSON(ConfiguracionJson) <> 1);
DECLARE @antesPascal INT = (SELECT COUNT(*) FROM dbo.Tipologias
    WHERE ISJSON(ConfiguracionJson) = 1 AND LOWER(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey')) = N'default.gpt4o-mini');
DECLARE @antesCamel INT = (SELECT COUNT(*) FROM dbo.Tipologias
    WHERE ISJSON(ConfiguracionJson) = 1 AND LOWER(JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey')) = N'default.gpt4o-mini');
DECLARE @otraClave INT = (SELECT COUNT(*) FROM dbo.Tipologias
    WHERE ISJSON(ConfiguracionJson) = 1
      AND LOWER(COALESCE(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey'), JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey'), N'default.gpt4o-mini')) <> N'default.gpt4o-mini');
PRINT CONCAT('Antes: tipologias=', @total, ', JSON no valido=', @invalidas,
             ', con default.gpt4o-mini (Pascal)=', @antesPascal, ', (camel)=', @antesCamel,
             ', con otra clave de prompt (no se tocan)=', @otraClave);

IF @antesPascal + @antesCamel = 0
    PRINT 'Nada que cambiar: ninguna tipologia fija default.gpt4o-mini (0 cambios). Sin backup.';
ELSE
BEGIN
    -- 1. Backup de la tabla
    DECLARE @bak SYSNAME = CONCAT('Tipologias__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
    DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.Tipologias;');
    EXEC sp_executesql @sql;
    PRINT CONCAT('Backup creado: dbo.', @bak);

    BEGIN TRAN;

    -- 2. Rama PascalCase
    UPDATE dbo.Tipologias
    SET ConfiguracionJson = JSON_MODIFY(ConfiguracionJson, '$.PromptConfig.ModelKey', NULL)
    WHERE ISJSON(ConfiguracionJson) = 1
      AND LOWER(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey')) = N'default.gpt4o-mini';
    DECLARE @camb1 INT = @@ROWCOUNT;

    -- 3. Rama camelCase
    UPDATE dbo.Tipologias
    SET ConfiguracionJson = JSON_MODIFY(ConfiguracionJson, '$.promptConfig.modelKey', NULL)
    WHERE ISJSON(ConfiguracionJson) = 1
      AND LOWER(JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey')) = N'default.gpt4o-mini';
    DECLARE @camb2 INT = @@ROWCOUNT;

    PRINT CONCAT('Cambiadas: Pascal=', @camb1, ', camel=', @camb2, ', total=', @camb1 + @camb2);

    -- 4. Comprobaciones antes del COMMIT
    IF @camb1 <> @antesPascal OR @camb2 <> @antesCamel
        THROW 50050, 'El numero de filas cambiadas no coincide con el recuento previo: se deshace todo.', 1;
    IF (SELECT COUNT(*) FROM dbo.Tipologias WHERE ISJSON(ConfiguracionJson) <> 1) <> @invalidas
        THROW 50051, 'Alguna tipologia quedo con JSON no valido: se deshace todo.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Tipologias
               WHERE ISJSON(ConfiguracionJson) = 1
                 AND (LOWER(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey')) = N'default.gpt4o-mini'
                   OR LOWER(JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey')) = N'default.gpt4o-mini'))
        THROW 50052, 'Quedan tipologias con default.gpt4o-mini tras el cambio: se deshace todo.', 1;
    -- Sin claves duplicadas: ninguna fila con la propiedad en los dos casings a la vez
    IF EXISTS (SELECT 1 FROM dbo.Tipologias
               WHERE ISJSON(ConfiguracionJson) = 1
                 AND JSON_QUERY(ConfiguracionJson, '$.PromptConfig') IS NOT NULL
                 AND JSON_QUERY(ConfiguracionJson, '$.promptConfig') IS NOT NULL)
        THROW 50053, 'Hay tipologias con PromptConfig y promptConfig a la vez: se deshace todo.', 1;

    COMMIT TRAN;
    PRINT 'Las tipologias heredan ahora PromptDefaults:ModelKey.';
END

-- Verificacion
SELECT
    COUNT(*)                                                                                                            AS Tipologias,
    SUM(CASE WHEN LOWER(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey')) = N'default.gpt4o-mini'
              OR  LOWER(JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey')) = N'default.gpt4o-mini' THEN 1 ELSE 0 END) AS AunFijanGpt4oMini,
    SUM(CASE WHEN COALESCE(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey'), JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey')) IS NOT NULL
              AND LOWER(COALESCE(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey'), JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey'))) <> N'default.gpt4o-mini' THEN 1 ELSE 0 END) AS FijanOtraClave,
    SUM(CASE WHEN COALESCE(JSON_VALUE(ConfiguracionJson, '$.PromptConfig.ModelKey'), JSON_VALUE(ConfiguracionJson, '$.promptConfig.modelKey')) IS NULL THEN 1 ELSE 0 END) AS HeredanDelDefecto,
    SUM(CASE WHEN ISJSON(ConfiguracionJson) <> 1 THEN 1 ELSE 0 END)                                                      AS JsonNoValido
FROM dbo.Tipologias;
