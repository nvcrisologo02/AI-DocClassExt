-- =====================================================================
-- 06 - PRE: alta del juego gpt-5-mini en ModeloConfigs
-- =====================================================================
-- El mirror de PRO a PRE (2026-08-14) dejo ModeloConfigs de PRE solo con
-- el juego gpt-4o-mini. Este script da de alta el juego gpt-5-mini
-- replicando el ya operativo en DEV (scripts 04/05, AB#100131):
--   - classification.gpt5-mini  <- clona classification.gpt4o-mini-fallback
--       (MaxTokens 2000: los reasoning tokens cuentan contra la salida)
--   - extraction.gpt5-mini      <- clona extraction.gpt4o-mini-fallback
--       (MaxTokens 16000; TimeoutSeconds 180: gpt-5-mini supera los 60 s
--        con documentos largos, leccion del caso FE-FE5 en DEV)
--   - prompt.gpt5-mini          <- clona default.gpt4o-mini
-- Sin IsDefault ni UseAsFallback: no entran en la resolucion por defecto
-- ni rompen la unicidad del fallback; solo se resuelven por modelKey.
-- El deployment gpt-5-mini ya existe en el recurso compartido
-- upe48-mm2avmdm-swedencentral (mismo endpoint que las filas clonadas).
--
-- Idempotente: no inserta si la Key ya existe para su Tipo.
-- La app refresca el registro de modelos en <= 5 min (cache de memoria).
--
-- Ejecutar con:
--   sqlcmd -S srbsqlpredocai.database.windows.net -d DocumentIA -G -i 06-pre-create-gpt5-set.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @altas TABLE (Nueva NVARCHAR(200), Origen NVARCHAR(200), Tipo INT,
                      NuevoMaxTokens INT NULL, NuevoTimeout INT NULL);
INSERT INTO @altas VALUES
    ('classification.gpt5-mini', 'classification.gpt4o-mini-fallback', 0, 2000,  NULL),
    ('extraction.gpt5-mini',     'extraction.gpt4o-mini-fallback',     1, 16000, 180),
    ('prompt.gpt5-mini',         'default.gpt4o-mini',                 2, NULL,  NULL);

INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, CreadoPor)
SELECT m.Tipo,
       a.Nueva,
       m.Provider,
       1,
       JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(m.ConfiguracionJson,
           '$.Key', a.Nueva),
           '$.DeploymentName', 'gpt-5-mini'),
           '$.IsDefault', CAST(0 AS BIT)),
           '$.UseAsFallback', CAST(0 AS BIT)),
           '$.MaxTokens', COALESCE(a.NuevoMaxTokens, JSON_VALUE(m.ConfiguracionJson, '$.MaxTokens'))),
       SYSUTCDATETIME(),
       'script-06-pre-gpt5-set'
FROM @altas a
JOIN dbo.ModeloConfigs m
  ON m.[Key] = a.Origen AND m.Tipo = a.Tipo AND m.Activo = 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ModeloConfigs x WHERE x.[Key] = a.Nueva AND x.Tipo = a.Tipo
);

DECLARE @insertadas INT = @@ROWCOUNT;
PRINT CONCAT('Filas gpt-5-mini insertadas: ', @insertadas);

-- TimeoutSeconds especifico (solo extraccion)
UPDATE m
SET ConfiguracionJson = JSON_MODIFY(m.ConfiguracionJson, '$.TimeoutSeconds', a.NuevoTimeout)
FROM dbo.ModeloConfigs m
JOIN @altas a ON a.Nueva = m.[Key] AND a.Tipo = m.Tipo
WHERE a.NuevoTimeout IS NOT NULL
  AND m.CreadoPor = 'script-06-pre-gpt5-set';

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
