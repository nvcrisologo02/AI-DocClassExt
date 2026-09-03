-- =====================================================================
-- 04 - DEV: separar los juegos gpt-4o-mini y gpt-5-mini
-- =====================================================================
-- DEV quedo en modo mixto: las 4 filas con keys 'gpt4o-mini' (set activo
-- del pipeline via IsDefault/UseAsFallback) apuntan al deployment
-- gpt-5-mini, y las filas de prueba *-gpt5-mini-test duplican ese mismo
-- deployment con parametros incompletos.
--
-- Este script deja dos juegos separados y operativos:
--   1. GPT-4 (activo): revierte las 4 filas 'gpt4o-mini' al deployment
--      gpt-4o-mini (misma semantica que 03-rollback-gpt5-models.sql:
--      solo DeploymentName, y MaxTokens 150 en clasificacion).
--   2. GPT-5 (seleccionable por Key, sin flags): renombra las filas
--      quitando el sufijo '-test' (columna Key y $.Key del JSON) y
--      corrige MaxTokens (clasificacion 2000 por los reasoning tokens;
--      extraccion 16000 por paridad con el juego activo).
--
-- Hace backup de la tabla antes de tocar nada. NO idempotente: una
-- segunda ejecucion aborta en las guardas de recuento.
-- La app refresca el registro de modelos en <= 5 min (cache de memoria).
--
-- Ejecutar con:
--   sqlcmd -S srbsqldevdocai.database.windows.net -d DocumentIA -G -i 04-dev-split-gpt4-gpt5.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 1. Backup de la tabla destino (convencion del proyecto)
DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

BEGIN TRAN;

-- 2. Juego GPT-4: revertir las filas del set activo a gpt-4o-mini
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

DECLARE @gpt4 INT = @@ROWCOUNT;
IF @gpt4 <> 4
BEGIN
    ROLLBACK;
    RAISERROR('Juego GPT-4: se esperaban 4 filas y se actualizaron %d. ROLLBACK aplicado; revisar ModeloConfigs.', 16, 1, @gpt4);
    RETURN;
END

-- 3. Juego GPT-5: renombrar '-test' -> definitivo y ajustar MaxTokens
DECLARE @renames TABLE (Vieja NVARCHAR(200), Nueva NVARCHAR(200), NuevoMaxTokens INT NULL);
INSERT INTO @renames VALUES
    ('classification.gpt5-mini-test', 'classification.gpt5-mini', 2000),   -- 150 es insuficiente con reasoning tokens
    ('extraction.gpt5-mini-test',     'extraction.gpt5-mini',     16000),  -- paridad con el juego de extraccion activo
    ('prompt.gpt5-mini-test',         'prompt.gpt5-mini',         NULL);

UPDATE m
SET [Key] = r.Nueva,
    ConfiguracionJson =
        CASE WHEN r.NuevoMaxTokens IS NULL
             THEN JSON_MODIFY(m.ConfiguracionJson, '$.Key', r.Nueva)
             ELSE JSON_MODIFY(JSON_MODIFY(m.ConfiguracionJson, '$.Key', r.Nueva),
                              '$.MaxTokens', r.NuevoMaxTokens)
        END,
    FechaActualizacion = SYSUTCDATETIME()
FROM dbo.ModeloConfigs m
JOIN @renames r ON r.Vieja = m.[Key];

DECLARE @gpt5 INT = @@ROWCOUNT;
IF @gpt5 <> 3
BEGIN
    ROLLBACK;
    RAISERROR('Juego GPT-5: se esperaban 3 filas y se actualizaron %d. ROLLBACK aplicado; revisar ModeloConfigs.', 16, 1, @gpt5);
    RETURN;
END

COMMIT;
PRINT 'Separacion aplicada: 4 filas revertidas a gpt-4o-mini y 3 filas gpt-5 renombradas.';

-- 4. Verificacion
SELECT Id, Tipo, [Key], Activo,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.MaxTokens')      AS MaxTokens,
       JSON_VALUE(ConfiguracionJson, '$.IsDefault')      AS IsDefault,
       JSON_VALUE(ConfiguracionJson, '$.UseAsFallback')  AS UseAsFallback,
       FechaActualizacion
FROM dbo.ModeloConfigs
WHERE Provider = 'azure-openai'
ORDER BY Tipo, Id;
