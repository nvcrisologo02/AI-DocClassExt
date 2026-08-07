-- =====================================================================
-- 03 - ROLLBACK: revertir las filas GPT a deployment gpt-4o-mini
-- =====================================================================
-- Inversa determinista del script 02 (DeploymentName y MaxTokens de
-- clasificacion). Alternativa de restauracion completa: volcar desde la
-- tabla de backup dbo.ModeloConfigs__bak_<timestamp> creada por el 02:
--
--   UPDATE m SET m.ConfiguracionJson = b.ConfiguracionJson,
--                m.FechaActualizacion = SYSUTCDATETIME()
--   FROM dbo.ModeloConfigs m
--   JOIN dbo.ModeloConfigs__bak_<timestamp> b ON b.Id = m.Id
--   WHERE m.[Key] IN ('classification.gpt4o-mini-fallback','default.gpt4o-mini_ex',
--                     'extraction.gpt4o-mini-fallback','default.gpt4o-mini');
--
-- Ejecutar con:
--   sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i 03-rollback-gpt5-models.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE m
SET ConfiguracionJson =
        CASE WHEN m.[Key] = 'classification.gpt4o-mini-fallback'
             THEN JSON_MODIFY(JSON_MODIFY(m.ConfiguracionJson, '$.DeploymentName', 'gpt-4o-mini'),
                              '$.MaxTokens', 150)
             ELSE JSON_MODIFY(m.ConfiguracionJson, '$.DeploymentName', 'gpt-4o-mini')
        END,
    FechaActualizacion = SYSUTCDATETIME()
FROM dbo.ModeloConfigs m
WHERE m.[Key] IN ('classification.gpt4o-mini-fallback', 'default.gpt4o-mini_ex',
                  'extraction.gpt4o-mini-fallback', 'default.gpt4o-mini')
  AND JSON_VALUE(m.ConfiguracionJson, '$.DeploymentName') = 'gpt-5-mini';

PRINT CONCAT('Filas revertidas a gpt-4o-mini: ', @@ROWCOUNT);
COMMIT;

SELECT Id, Tipo, [Key],
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.MaxTokens')      AS MaxTokens
FROM dbo.ModeloConfigs
WHERE [Key] IN ('classification.gpt4o-mini-fallback', 'default.gpt4o-mini_ex',
                'extraction.gpt4o-mini-fallback', 'default.gpt4o-mini');
