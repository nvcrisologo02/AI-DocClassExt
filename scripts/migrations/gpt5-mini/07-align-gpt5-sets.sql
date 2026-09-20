-- =====================================================================
-- 07 - TODOS LOS ENTORNOS: alineado del juego gpt-5-mini y alias
-- =====================================================================
-- Deja ModeloConfigs identico en DEV/PRE/PRO respecto a azure-openai
-- (AB#100219; ejecutado en los 3 entornos el 2026-08-14):
--   1. Alta del juego gpt-5-mini canonico si falta (clona las filas gpt4
--      del propio entorno): classification.gpt5-mini (MaxTokens 2000),
--      extraction.gpt5-mini (MaxTokens 16000, TimeoutSeconds 180),
--      prompt.gpt5-mini. Sin IsDefault/UseAsFallback.
--   2. Alta de los alias de clasificacion 'gpt-4o-mini' y 'gpt-5-mini'
--      si faltan (modelKeys literales que piden los casos E2E
--      FC-FC4/FC-FC5; clonan las filas reales, sin flags).
--   3. Normaliza TimeoutSeconds de extraction.gpt5-mini a 180 (gpt-5-mini
--      supera los 60 s con documentos largos, leccion FE-FE5).
-- El set activo del pipeline (gpt-4o-mini) no se toca.
--
-- Idempotente: reejecutar no duplica ni cambia nada ya alineado.
-- Hace backup de la tabla antes de tocar nada (convencion del proyecto).
-- La app refresca el registro de modelos en <= 5 min (cache de memoria).
--
-- Ejecutar con (cambiar servidor segun entorno):
--   sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i 07-align-gpt5-sets.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0. Backup de la tabla destino
DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

BEGIN TRAN;

-- 1. Juego gpt-5-mini canonico (si falta)
DECLARE @altas TABLE (Nueva NVARCHAR(200), Origen NVARCHAR(200), Tipo INT, NuevoMaxTokens INT NULL);
INSERT INTO @altas VALUES
    ('classification.gpt5-mini', 'classification.gpt4o-mini-fallback', 0, 2000),
    ('extraction.gpt5-mini',     'extraction.gpt4o-mini-fallback',     1, 16000),
    ('prompt.gpt5-mini',         'default.gpt4o-mini',                 2, NULL);

INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, CreadoPor)
SELECT m.Tipo, a.Nueva, m.Provider, 1,
       JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(m.ConfiguracionJson,
           '$.Key', a.Nueva),
           '$.DeploymentName', 'gpt-5-mini'),
           '$.IsDefault', CAST(0 AS BIT)),
           '$.UseAsFallback', CAST(0 AS BIT)),
           '$.MaxTokens', COALESCE(a.NuevoMaxTokens, JSON_VALUE(m.ConfiguracionJson, '$.MaxTokens'))),
       SYSUTCDATETIME(),
       'script-07-align-gpt5'
FROM @altas a
JOIN dbo.ModeloConfigs m
  ON m.[Key] = a.Origen AND m.Tipo = a.Tipo AND m.Activo = 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ModeloConfigs x WHERE x.[Key] = a.Nueva AND x.Tipo = a.Tipo
);
PRINT CONCAT('Filas gpt-5-mini canonicas insertadas: ', @@ROWCOUNT);

-- 2. Alias de clasificacion para seleccion por modelKey literal (si faltan)
DECLARE @aliases TABLE (Alias NVARCHAR(200), Origen NVARCHAR(200));
INSERT INTO @aliases VALUES
    ('gpt-4o-mini', 'classification.gpt4o-mini-fallback'),
    ('gpt-5-mini',  'classification.gpt5-mini');

INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, CreadoPor)
SELECT m.Tipo, a.Alias, m.Provider, 1,
       JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(m.ConfiguracionJson,
           '$.Key', a.Alias),
           '$.IsDefault', CAST(0 AS BIT)),
           '$.UseAsFallback', CAST(0 AS BIT)),
       SYSUTCDATETIME(),
       'script-07-align-gpt5'
FROM @aliases a
JOIN dbo.ModeloConfigs m
  ON m.[Key] = a.Origen AND m.Tipo = 0 AND m.Activo = 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ModeloConfigs x WHERE x.[Key] = a.Alias AND x.Tipo = 0
);
PRINT CONCAT('Alias insertados: ', @@ROWCOUNT);

-- 3. Normalizacion del timeout de extraction.gpt5-mini
UPDATE dbo.ModeloConfigs
SET ConfiguracionJson = JSON_MODIFY(ConfiguracionJson, '$.TimeoutSeconds', 180),
    FechaActualizacion = SYSUTCDATETIME()
WHERE [Key] = 'extraction.gpt5-mini' AND Tipo = 1
  AND CAST(JSON_VALUE(ConfiguracionJson, '$.TimeoutSeconds') AS INT) <> 180;
PRINT CONCAT('Timeouts normalizados a 180: ', @@ROWCOUNT);

COMMIT;

-- Verificacion
SELECT Id, Tipo, [Key], Activo,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.MaxTokens')      AS MaxTokens,
       JSON_VALUE(ConfiguracionJson, '$.TimeoutSeconds') AS TimeoutSeconds,
       JSON_VALUE(ConfiguracionJson, '$.IsDefault')      AS IsDefault,
       JSON_VALUE(ConfiguracionJson, '$.UseAsFallback')  AS UseAsFallback
FROM dbo.ModeloConfigs
WHERE Provider = 'azure-openai'
ORDER BY Tipo, Id;
