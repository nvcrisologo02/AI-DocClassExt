-- =====================================================================
-- 01 - DEV: filas classification.gpt56-luna y prompt.gpt56-luna
-- =====================================================================
-- Prueba en DEV de gpt-5.6-luna como sustituto de gpt-4.1-mini (AB#99934,
-- task AB#101372). Da de alta dos filas en ModeloConfigs clonando el juego
-- gpt-5-mini canonico del propio entorno:
--   classification.gpt56-luna (Tipo 0) <- classification.gpt5-mini
--   prompt.gpt56-luna         (Tipo 2) <- prompt.gpt5-mini
-- Cambios sobre la fila clonada: Key, DeploymentName = 'gpt-5.6-luna',
-- IsDefault = 0, UseAsFallback = 0 y ReasoningEffort = 'none' (el helper
-- de capacidades envia reasoning_effort y max_completion_tokens solo con
-- esfuerzo valido). MaxTokens se conserva (2000 en clasificacion): bajarlo
-- es decision del cutover, no de la prueba.
--
-- Las filas quedan Activo = 1 pero sin IsDefault ni UseAsFallback: solo se
-- resuelven pidiendo la clave (Model = classification.gpt56-luna en el
-- harness o en la peticion). El flujo por defecto de DEV no cambia.
--
-- Idempotente: no inserta si la clave ya existe para su Tipo. Backup previo
-- de la tabla (ModeloConfigs__bak_<ts>) solo si hay algo que insertar. La app
-- refresca el registro de modelos en <= 5 min (cache de memoria). Requiere el
-- deployment creado con create-gpt56-luna-deployment.sh; si no existe, las
-- filas resuelven pero las llamadas fallaran con 404.
--
-- Ejecutar SOLO en DEV:
--   pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/01-dev-insert-gpt56-luna-rows.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0. Comprobar que las filas origen existen; si no, no hay nada que clonar.
IF NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs WHERE [Key] = N'classification.gpt5-mini' AND Tipo = 0 AND Activo = 1)
    THROW 50010, 'No existe la fila activa classification.gpt5-mini (Tipo 0): ejecutar antes gpt5-mini/07-align-gpt5-sets.sql.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs WHERE [Key] = N'prompt.gpt5-mini' AND Tipo = 2 AND Activo = 1)
    THROW 50011, 'No existe la fila activa prompt.gpt5-mini (Tipo 2): ejecutar antes gpt5-mini/07-align-gpt5-sets.sql.', 1;

-- 1. Altas pendientes
DECLARE @altas TABLE (Nueva NVARCHAR(200), Origen NVARCHAR(200), Tipo INT);
INSERT INTO @altas VALUES
    (N'classification.gpt56-luna', N'classification.gpt5-mini', 0),
    (N'prompt.gpt56-luna',         N'prompt.gpt5-mini',         2);

DECLARE @pendientes INT = (
    SELECT COUNT(*) FROM @altas a
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs x WHERE x.[Key] = a.Nueva AND x.Tipo = a.Tipo));

IF @pendientes = 0
    PRINT 'Nada que insertar: las dos filas gpt56-luna ya existen. Sin backup ni cambios.';
ELSE
BEGIN
    -- 2. Backup de la tabla destino
    DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
    DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
    EXEC sp_executesql @sql;
    PRINT CONCAT('Backup creado: dbo.', @bak);

    BEGIN TRAN;

    INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, CreadoPor)
    SELECT m.Tipo, a.Nueva, m.Provider, 1,
           JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(m.ConfiguracionJson,
               '$.Key', a.Nueva),
               '$.DeploymentName', N'gpt-5.6-luna'),
               '$.IsDefault', CAST(0 AS BIT)),
               '$.UseAsFallback', CAST(0 AS BIT)),
               '$.ReasoningEffort', N'none'),
           SYSUTCDATETIME(),
           N'script-01-gpt56-luna'
    FROM @altas a
    JOIN dbo.ModeloConfigs m
      ON m.[Key] = a.Origen AND m.Tipo = a.Tipo AND m.Activo = 1
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ModeloConfigs x WHERE x.[Key] = a.Nueva AND x.Tipo = a.Tipo
    );
    PRINT CONCAT('Filas gpt56-luna insertadas: ', @@ROWCOUNT);

    COMMIT;
END

-- Verificacion
SELECT Id, Tipo, [Key], Activo, Provider,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName')  AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.ReasoningEffort') AS ReasoningEffort,
       JSON_VALUE(ConfiguracionJson, '$.MaxTokens')       AS MaxTokens,
       JSON_VALUE(ConfiguracionJson, '$.IsDefault')       AS IsDefault,
       JSON_VALUE(ConfiguracionJson, '$.UseAsFallback')   AS UseAsFallback,
       ISJSON(ConfiguracionJson)                          AS JsonValido
FROM dbo.ModeloConfigs
WHERE [Key] LIKE N'%gpt56-luna%'
ORDER BY Tipo, Id;
